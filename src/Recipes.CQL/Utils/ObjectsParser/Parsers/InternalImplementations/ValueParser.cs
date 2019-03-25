﻿using System;

namespace Kontur.DBViewer.Recipes.CQL.Utils.ObjectsParser.Parsers.InternalImplementations
{
    internal class ValueParser
    {
        public static ValueParser<string> CreateSimpleStringParser()
        {
            return new ValueParser<string>(null);
        }

        public static ValueParser<T> Create<T>(TryParseDelegate<T> parse)
        {
            return new ValueParser<T>(parse);
        }
    }

    internal class ValueParser<T> : IValueParser
    {
        public ValueParser(TryParseDelegate<T> tryParse)
        {
            this.tryParse = tryParse;
        }

        public bool TryParse(Type type, string value, out object result)
        {
            if(tryParse == null)
            {
                result = value;
                return true;
            }
            T temp;
            var tryParseResult = tryParse(value, out temp);
            result = temp;
            return tryParseResult;
        }

        private readonly TryParseDelegate<T> tryParse;
    }
}