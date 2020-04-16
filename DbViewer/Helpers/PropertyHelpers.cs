﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using SkbKontur.DbViewer.Configuration;
using SkbKontur.DbViewer.DataTypes;

namespace SkbKontur.DbViewer.Helpers
{
    public static class PropertyHelpers
    {
        public static void BuildGettersForProperties([NotNull] Type type, [NotNull] string currentName, [NotNull] Func<object, object> currentGetter,
                                                     [NotNull, ItemNotNull] List<string> properties, [NotNull, ItemNotNull] List<Func<object, object>> getters,
                                                     ICustomPropertyConfigurationProvider propertyConfigurationProvider,
                                                     [CanBeNull] Type[] usedTypes = null)
        {
            usedTypes = (usedTypes ?? new Type[0]).ToArray();
            if (usedTypes.Contains(type) || typeof(IEnumerable).IsAssignableFrom(type))
            {
                properties.Add(currentName);
                getters.Add(currentGetter);
                return;
            }

            usedTypes = usedTypes.Concat(new[] {type}).ToArray();
            foreach (var propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyConfiguration = propertyConfigurationProvider.TryGetConfiguration(propertyInfo);
                var propertyType = propertyConfiguration?.ResolvedType ?? propertyInfo.PropertyType;
                var propertyName = propertyInfo.Name;
                var propertyGetter = propertyInfo.GetGetMethod();

                var name = string.IsNullOrEmpty(currentName) ? propertyName : $"{currentName}.{propertyName}";
                Func<object, object> getter = x =>
                    {
                        var o = currentGetter(x);
                        if (o == null)
                            return null;

                        var propertyValue = propertyGetter.Invoke(o, new object[0]);
                        return propertyConfiguration == null ? propertyValue : propertyConfiguration.StoredToApi(propertyValue);
                    };

                if (IsSimpleType(propertyType))
                {
                    properties.Add(name);
                    getters.Add(getter);
                    continue;
                }

                BuildGettersForProperties(propertyType, name, getter, properties, getters, propertyConfigurationProvider, usedTypes);
            }
        }

        [NotNull]
        public static string ToString([NotNull] Func<object, object> getter, [CanBeNull] object value)
        {
            var property = getter(value);
            return ToString(property);
        }

        public static TypeMetaInformation BuildTypeMetaInformation([NotNull] Type type, IPropertyDescriptionBuilder propertyDescriptionBuilder,
                                                                   ICustomPropertyConfigurationProvider propertyConfigurationProvider,
                                                                   [CanBeNull] [ItemNotNull] Type[] usedTypes = null)
        {
            return BuildTypeMetaInformation(@object : null, type, propertyDescriptionBuilder, propertyConfigurationProvider, usedTypes);
        }

        [CanBeNull]
        public static TypeMetaInformation BuildTypeMetaInformation(object @object, [NotNull] Type type, IPropertyDescriptionBuilder propertyDescriptionBuilder,
                                                                   ICustomPropertyConfigurationProvider propertyConfigurationProvider,
                                                                   [CanBeNull] [ItemNotNull] Type[] usedTypes = null)
        {
            usedTypes = (usedTypes ?? new Type[0]).ToArray();
            if (usedTypes.Contains(type))
                return null;
            usedTypes = usedTypes.Concat(new[] {type}).ToArray();
            if (type.IsArray || type.HasElementType)
            {
                return new TypeMetaInformation
                    {
                        TypeName = type.Name,
                        IsArray = true,
                        Properties = new PropertyMetaInformation[0],
                        GenericTypeArguments = new[] {BuildTypeMetaInformation(type.GetElementType(), propertyDescriptionBuilder, propertyConfigurationProvider, usedTypes)},
                    };
            }

            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return TypeMetaInformation.ForSimpleType(type.GetGenericArguments()[0].Name, isNullable : true);

                return new TypeMetaInformation
                    {
                        TypeName = new Regex(@"`.*").Replace(type.GetGenericTypeDefinition().Name, ""),
                        IsArray = true,
                        Properties = new PropertyMetaInformation[0],
                        GenericTypeArguments = type.GetGenericArguments()
                                                   .Select(x => BuildTypeMetaInformation(x, propertyDescriptionBuilder, propertyConfigurationProvider, usedTypes))
                                                   .ToArray(),
                    };
            }

            if (IsSimpleType(type))
                return TypeMetaInformation.ForSimpleType(type.Name);

            return new TypeMetaInformation
                {
                    TypeName = type.Name,
                    GenericTypeArguments = new TypeMetaInformation[0],
                    Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                     .Select(x => BuildPropertyInfo(@object, x, propertyDescriptionBuilder, propertyConfigurationProvider, usedTypes))
                                     .ToArray(),
                };
        }

        private static bool IsSimpleType([NotNull] Type type)
        {
            return type.IsEnum ||
                   type.IsPrimitive ||
                   type.IsValueType ||
                   new[]
                       {
                           typeof(string),
                           typeof(ushort), typeof(uint), typeof(ulong),
                           typeof(short), typeof(int), typeof(long),
                       }.Contains(type);
        }

        [NotNull]
        private static PropertyMetaInformation BuildPropertyInfo(object @object, [NotNull] PropertyInfo propertyInfo,
                                                                 IPropertyDescriptionBuilder propertyDescriptionBuilder,
                                                                 ICustomPropertyConfigurationProvider propertyConfigurationProvider,
                                                                 [NotNull, ItemNotNull] Type[] types)
        {
            var customConfiguration = @object == null
                                          ? propertyConfigurationProvider.TryGetConfiguration(propertyInfo)
                                          : propertyConfigurationProvider.TryGetConfiguration(@object, propertyInfo);

            var propertyType = customConfiguration?.ResolvedType ?? propertyInfo.PropertyType;
            var underlyingType = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>) ? propertyType.GetGenericArguments()[0] : propertyType;
            var propertyDescription = propertyDescriptionBuilder.Build(propertyInfo, propertyType);
            return new PropertyMetaInformation
                {
                    Name = propertyInfo.Name,
                    AvailableFilters = propertyDescription.AvailableFilters,
                    AvailableValues = underlyingType.IsEnum ? Enum.GetNames(underlyingType) : new string[0],
                    IsIdentity = propertyDescription.IsIdentity,
                    IsRequired = propertyDescription.IsRequired,
                    IsSearchable = propertyDescription.IsSearchable,
                    IsSortable = propertyDescription.IsSortable,
                    Type = BuildTypeMetaInformation(propertyType, propertyDescriptionBuilder, propertyConfigurationProvider, types),
                };
        }

        [NotNull]
        private static string ToString([CanBeNull] object property)
        {
            if (property == null)
                return string.Empty;

            if (property is DateTime time)
                return time.ToString("O");

            if (property is IList collection)
                return string.Join(", ", collection.Cast<object>().Select(ToString));

            return property.ToString();
        }
    }
}