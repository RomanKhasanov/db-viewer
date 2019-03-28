﻿namespace Kontur.DBViewer.Core.TypeInformation
{
    public class ByteTypeInfo : TypeInfo
    {
        public ByteTypeInfo(bool canBeNull)
        {
            CanBeNull = canBeNull;
        }

        public bool CanBeNull { get; }
        public override PrimitiveType Type => PrimitiveType.Byte;
    }
}