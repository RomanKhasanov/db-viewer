﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using SkbKontur.DbViewer.DataTypes;
using SkbKontur.DbViewer.Helpers;
using SkbKontur.DbViewer.Schemas;

using FileInfo = SkbKontur.DbViewer.DataTypes.FileInfo;

namespace SkbKontur.DbViewer
{
    public class DbViewerApi
    {
        public DbViewerApi(ISchemaRegistry schemaRegistry)
        {
            this.schemaRegistry = schemaRegistry;
        }

        public ObjectIdentifier[] GetNames()
        {
            return schemaRegistry.GetAllSchemas()
                                 .SelectMany(s => s.Types.Select(x => new ObjectIdentifier {Identifier = x.TypeIdentifier, SchemaDescription = s.Description}))
                                 .ToArray();
        }

        public ObjectDescription GetMeta(string objectIdentifier)
        {
            var type = schemaRegistry.GetTypeByTypeIdentifier(objectIdentifier);
            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            var typeMeta = PropertyHelpers.BuildTypeMetaInformation(null, type, type, schema.PropertyDescriptionBuilder, schema.CustomPropertyConfigurationProvider);
            return new ObjectDescription
                {
                    Identifier = objectIdentifier,
                    SchemaDescription = schema.Description,
                    TypeMetaInformation = typeMeta,
                };
        }

        public async Task<SearchResult> SearchObjects(string objectIdentifier, ObjectSearchRequest query, bool isSuperUser)
        {
            var type = schemaRegistry.GetTypeByTypeIdentifier(objectIdentifier);
            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            if (!schema.Description.AllowReadAll && !query.Conditions.Any())
                throw new InvalidOperationException($"Reading without filters is not allowed for {objectIdentifier}");
            if (!schema.Description.AllowSort && query.Sorts.Any())
                throw new InvalidOperationException($"Sorting is not allowed for {objectIdentifier}");

            var offset = query.Offset ?? 0;
            var count = query.Count ?? 20;
            var countLimit = isSuperUser ? schema.Description.CountLimitForSuperUser : schema.Description.CountLimit;
            if (offset + count > countLimit)
                throw new InvalidOperationException($"Expected offset ({offset}) + count ({count}) to be less than countLimit ({countLimit})");

            var connector = schemaRegistry.GetConnector(objectIdentifier);
            var counts = await connector.Count(query.Conditions, countLimit + 1).ConfigureAwait(false);
            var results = counts == null
                              ? await connector.Search(query.Conditions, query.Sorts, 0, countLimit + 1).ConfigureAwait(false)
                              : await connector.Search(query.Conditions, query.Sorts, offset, count).ConfigureAwait(false);
            var objects = results.Select(x => ObjectsConverter.StoredToApi(type, x, schema.CustomPropertyConfigurationProvider)).ToArray();

            return new SearchResult
                {
                    Count = counts,
                    CountLimit = countLimit,
                    Items = objects,
                };
        }

        public async Task<DownloadResult> CountObjects(string objectIdentifier, ObjectSearchRequest query, bool isSuperUser)
        {
            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            if (!schema.Description.AllowReadAll && !query.Conditions.Any())
                throw new InvalidOperationException($"Reading without filters is not allowed for {objectIdentifier}");
            if (!schema.Description.AllowSort && query.Sorts.Any())
                throw new InvalidOperationException($"Sorting is not allowed for {objectIdentifier}");

            var downloadLimit = isSuperUser ? schema.Description.DownloadLimitForSuperUser : schema.Description.DownloadLimit;

            var connector = schemaRegistry.GetConnector(objectIdentifier);
            var count = await connector.Count(query.Conditions, downloadLimit + 1).ConfigureAwait(false);
            return new DownloadResult
                {
                    FileQuery = count > downloadLimit ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(query))),
                    Count = count,
                    CountLimit = downloadLimit,
                };
        }

        public async Task<FileInfo> DownloadObjects(string objectIdentifier, string searchQuery, bool isSuperUser)
        {
            var query = JsonConvert.DeserializeObject<ObjectSearchRequest>(Encoding.UTF8.GetString(Convert.FromBase64String(searchQuery)));
            var type = schemaRegistry.GetTypeByTypeIdentifier(objectIdentifier);
            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            if (!schema.Description.AllowReadAll && !query.Conditions.Any())
                throw new InvalidOperationException($"Reading without filters is not allowed for {objectIdentifier}");
            if (!schema.Description.AllowSort && query.Sorts.Any())
                throw new InvalidOperationException($"Sorting is not allowed for {objectIdentifier}");

            var downloadLimit = isSuperUser ? schema.Description.DownloadLimitForSuperUser : schema.Description.DownloadLimit;

            var connector = schemaRegistry.GetConnector(objectIdentifier);
            var results = await connector.Search(query.Conditions, query.Sorts, 0, downloadLimit + 1).ConfigureAwait(false);

            var properties = new List<string>();
            var getters = new List<Func<object?, object?>>();

            PropertyHelpers.BuildGettersForProperties(type, "", x => x, properties, getters, schema.CustomPropertyConfigurationProvider);

            var excludedIndices = properties.Select((x, i) => (x, i)).Where(x => query.ExcludedFields.Contains(x.x)).Select(x => x.i).ToArray();
            var filteredProperties = properties.Where((x, i) => !excludedIndices.Contains(i)).ToArray();
            var filteredGetters = getters.Where((x, i) => !excludedIndices.Contains(i)).ToArray();

            var csvWriter = new CsvWriter(filteredProperties);
            foreach (var item in results.Take(downloadLimit))
                csvWriter.AddRow(filteredGetters.Select(f => PropertyHelpers.ToString(f, item)).ToArray());

            return new FileInfo
                {
                    Content = new MemoryStream(csvWriter.GetBytes()),
                    ContentType = "text/csv",
                    Name = $"{objectIdentifier}-{DateTime.UtcNow:yyyy-MM-dd-HHmm}.csv"
                };
        }

        public async Task<ObjectDetails> ReadObject(string objectIdentifier, ObjectReadRequest query)
        {
            var type = schemaRegistry.GetTypeByTypeIdentifier(objectIdentifier);
            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            var result = await schemaRegistry.GetConnector(objectIdentifier).Read(query.Conditions).ConfigureAwait(false);
            var typeMeta = PropertyHelpers.BuildTypeMetaInformation(result, type, type, schema.PropertyDescriptionBuilder, schema.CustomPropertyConfigurationProvider);
            var obj = ObjectsConverter.StoredToApiDeep(result, schema.CustomPropertyConfigurationProvider);
            return new ObjectDetails
                {
                    Object = obj,
                    Meta = new ObjectDescription
                        {
                            Identifier = objectIdentifier,
                            SchemaDescription = schema.Description,
                            TypeMetaInformation = typeMeta,
                        }
                };
        }

        public Task DeleteObject(string objectIdentifier, ObjectReadRequest query, bool isSuperUser)
        {
            if (!isSuperUser)
                throw new InvalidOperationException("User cannot delete object");

            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            if (!schema.Description.AllowDelete)
                throw new InvalidOperationException($"Deleting is not allowed for {objectIdentifier}");

            return schemaRegistry.GetConnector(objectIdentifier).Delete(query.Conditions);
        }

        public async Task UpdateObject(string objectIdentifier, ObjectUpdateRequest query, bool isSuperUser)
        {
            if (!isSuperUser)
                throw new InvalidOperationException("User cannot update object");

            var schema = schemaRegistry.GetSchemaByTypeIdentifier(objectIdentifier);
            if (!schema.Description.AllowEdit)
                throw new InvalidOperationException($"Updating is not allowed for {objectIdentifier}");

            var connector = schemaRegistry.GetConnector(objectIdentifier);
            var oldObject = await connector.Read(query.Conditions).ConfigureAwait(false);
            if (oldObject == null)
                throw new InvalidOperationException("Expected edited object to exist");

            var updatedObject = ObjectPropertyEditor.SetValue(oldObject, query.Path, query.Value, schema.CustomPropertyConfigurationProvider);
            await connector.Write(updatedObject).ConfigureAwait(false);
        }

        private readonly ISchemaRegistry schemaRegistry;
    }
}