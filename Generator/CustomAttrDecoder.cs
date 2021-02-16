﻿// <copyright file="CustomAttrDecoder.cs" company="https://github.com/marlersoft">
// Copyright (c) https://github.com/marlersoft. All rights reserved.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type

namespace JsonWin32Generator
{
    using System;
    using System.Collections.Generic;
    using System.Reflection.Metadata;
    using System.Runtime.InteropServices;

    internal class CustomAttrDecoder : ICustomAttributeTypeProvider<CustomAttrType>
    {
        internal static readonly CustomAttrDecoder Instance = new CustomAttrDecoder();

        private CustomAttrDecoder()
        {
        }

        public CustomAttrType GetPrimitiveType(PrimitiveTypeCode code)
        {
            if (code == PrimitiveTypeCode.Boolean)
            {
                return CustomAttrType.Bool;
            }

            if (code == PrimitiveTypeCode.String)
            {
                return CustomAttrType.Str;
            }

            if (code == PrimitiveTypeCode.Int16)
            {
                return CustomAttrType.Int16;
            }

            if (code == PrimitiveTypeCode.Int32)
            {
                return CustomAttrType.Int32;
            }

            throw new NotImplementedException("Only string and bool primitive types have been implemented for custom attributes");
        }

        public CustomAttrType GetSystemType() => CustomAttrType.SystemType;

        public CustomAttrType GetSZArrayType(CustomAttrType elementType) => throw new NotImplementedException();

        public CustomAttrType GetTypeFromDefinition(MetadataReader mr, TypeDefinitionHandle handle, byte rawTypeKind) => throw new NotImplementedException();

        public CustomAttrType GetTypeFromReference(MetadataReader mr, TypeReferenceHandle handle, byte rawTypeKind)
        {
            TypeReference typeRef = mr.GetTypeReference(handle);
            string @namespace = mr.GetString(typeRef.Namespace);
            string name = mr.GetString(typeRef.Name);
            if (@namespace == "System.Runtime.InteropServices")
            {
                if (name == "CallingConvention")
                {
                    return CustomAttrType.CallConv;
                }

                if (name == "UnmanagedType")
                {
                    return CustomAttrType.UnmanagedType;
                }
            }

            throw new NotImplementedException();
        }

        public CustomAttrType GetTypeFromSerializedName(string name)
        {
            if (name == "System.Runtime.InteropServices.UnmanagedType")
            {
                return CustomAttrType.UnmanagedType;
            }

            throw new NotImplementedException();
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(CustomAttrType type)
        {
            if (object.ReferenceEquals(type, CustomAttrType.CallConv))
            {
                return PrimitiveTypeCode.Int32; // !!!!!!!! TODO: is this right???? What is this doing???
            }

            if (object.ReferenceEquals(type, CustomAttrType.UnmanagedType))
            {
                return PrimitiveTypeCode.Int32; // !!!!!!!! TODO: is this right???? What is this doing???
            }

            throw new NotImplementedException();
        }

        public bool IsSystemType(CustomAttrType type) => object.ReferenceEquals(type, CustomAttrType.SystemType);
    }

    internal class CustomAttrType
    {
        internal static readonly CustomAttrType Bool = new CustomAttrType();

        internal static readonly CustomAttrType Int16 = new CustomAttrType();

        internal static readonly CustomAttrType Int32 = new CustomAttrType();

        internal static readonly CustomAttrType UnmanagedType = new CustomAttrType();

        internal static readonly CustomAttrType CallConv = new CustomAttrType();

        internal static readonly CustomAttrType SystemType = new CustomAttrType();

        internal static readonly CustomAttrType Str = new CustomAttrType();

        private static readonly Dictionary<Type, CustomAttrType> ClrTypeToCustomAttrTypeMap = new Dictionary<Type, CustomAttrType>();

        static CustomAttrType()
        {
            ClrTypeToCustomAttrTypeMap.Add(typeof(bool), Bool);
            ClrTypeToCustomAttrTypeMap.Add(typeof(short), Int16);
            ClrTypeToCustomAttrTypeMap.Add(typeof(int), Int32);
            ClrTypeToCustomAttrTypeMap.Add(typeof(System.Runtime.InteropServices.UnmanagedType), UnmanagedType);
        }

        internal static CustomAttrType ToCustomAttrType(Type type)
        {
            if (ClrTypeToCustomAttrTypeMap.TryGetValue(type, out CustomAttrType? result))
            {
                return result;
            }

            throw new ArgumentException(Fmt.In($"converting type '{type}' to a CustomAttrType is not implemented"));
        }
    }

    internal class CustomAttr
    {
        internal static CustomAttr Decode(MetadataReader mr, CustomAttributeHandle attrHandle)
        {
            CustomAttribute attr = mr.GetCustomAttribute(attrHandle);
            NamespaceAndName attrName = attr.GetAttrTypeName(mr);
            CustomAttributeValue<CustomAttrType> attrArgs = attr.DecodeValue(CustomAttrDecoder.Instance);
            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "ConstAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 0);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return CustomAttr.Const.Instance;
            }

            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "NativeTypeInfoAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 1);
                UnmanagedType unmanagedType = Enforce.AttrFixedArgAsUnmanagedType(attrArgs.FixedArguments[0]);
                bool isNullTerminated = false;
                bool isNullNullTerminated = false;
                short? sizeParamIndex = null;
                UnmanagedType? arraySubType = null;
                int? sizeConst = null;

                foreach (CustomAttributeNamedArgument<CustomAttrType> namedArg in attrArgs.NamedArguments)
                {
                    if (namedArg.Name == "IsNullTerminated")
                    {
                        Enforce.Data(isNullTerminated == false);
                        isNullTerminated = Enforce.NamedAttrAs<bool>(namedArg);
                    }
                    else if (namedArg.Name == "IsNullNullTerminated")
                    {
                        Enforce.Data(isNullNullTerminated == false);
                        isNullNullTerminated = Enforce.NamedAttrAs<bool>(namedArg);
                    }
                    else if (namedArg.Name == "SizeParamIndex")
                    {
                        Enforce.Data(!sizeParamIndex.HasValue);
                        sizeParamIndex = Enforce.NamedAttrAs<short>(namedArg);
                    }
                    else if (namedArg.Name == "ArraySubType")
                    {
                        Enforce.Data(!arraySubType.HasValue);
                        arraySubType = Enforce.NamedAttrAs<UnmanagedType>(namedArg);
                    }
                    else if (namedArg.Name == "SizeConst")
                    {
                        Enforce.Data(!sizeConst.HasValue);
                        sizeConst = Enforce.NamedAttrAs<int>(namedArg);
                    }
                    else
                    {
                        Violation.Data();
                    }
                }

                return new CustomAttr.NativeTypeInfo(
                    unmanagedType: unmanagedType,
                    isNullTerminated: isNullTerminated,
                    isNullNullTerminated: isNullNullTerminated,
                    sizeParamIndex: sizeParamIndex,
                    arraySubType: arraySubType,
                    sizeConst: sizeConst);
            }

            if (attrName == new NamespaceAndName("System", "ObsoleteAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 1);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return new CustomAttr.Obsolete(Enforce.AttrFixedArgAsString(attrArgs.FixedArguments[0]));
            }

            if (attrName == new NamespaceAndName("System.Runtime.InteropServices", "GuidAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 1);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return new CustomAttr.Guid(Enforce.AttrFixedArgAsString(attrArgs.FixedArguments[0]));
            }

            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "RAIIFreeAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 1);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return new CustomAttr.RaiiFree(Enforce.AttrFixedArgAsString(attrArgs.FixedArguments[0]));
            }

            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "NativeTypedefAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 0);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return CustomAttr.NativeTypedef.Instance;
            }

            if (attrName == new NamespaceAndName("System.Runtime.InteropServices", "UnmanagedFunctionPointerAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 1);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return CustomAttr.UnmanagedFunctionPointer.Instance;
            }

            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "ComOutPtrAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 0);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return CustomAttr.ComOutPtr.Instance;
            }

            if (attrName == new NamespaceAndName("Windows.Win32.Interop", "GuidConstAttribute"))
            {
                Enforce.AttrFixedArgCount(attrName, attrArgs, 0);
                Enforce.AttrNamedArgCount(attrName, attrArgs, 0);
                return CustomAttr.GuidConstAttribute.Instance;
            }

            throw new NotImplementedException(Fmt.In($"unhandled custom attribute \"{attrName.Namespace}\", \"{attrName.Name}\""));
        }

        internal class Const : CustomAttr
        {
            internal static readonly Const Instance = new Const();

            private Const()
            {
            }
        }

        internal class NativeTypeInfo : CustomAttr
        {
            internal NativeTypeInfo(UnmanagedType unmanagedType, bool isNullTerminated, bool isNullNullTerminated, short? sizeParamIndex, UnmanagedType? arraySubType, int? sizeConst)
            {
                this.UnmanagedType = unmanagedType;
                this.IsNullTerminated = isNullTerminated;
                this.IsNullNullTerminated = isNullNullTerminated;
                this.SizeParamIndex = sizeParamIndex;
                this.ArraySubType = arraySubType;
                this.SizeConst = sizeConst;
            }

            internal UnmanagedType UnmanagedType { get; }

            internal bool IsNullTerminated { get; }

            internal bool IsNullNullTerminated { get; }

            internal short? SizeParamIndex { get; }

            internal UnmanagedType? ArraySubType { get; }

            internal int? SizeConst { get; }
        }

        internal class Obsolete : CustomAttr
        {
            internal Obsolete(string message)
            {
                this.Message = message;
            }

            internal string Message { get; }
        }

        internal class Guid : CustomAttr
        {
            internal Guid(string value)
            {
                this.Value = value;
            }

            internal string Value { get; }
        }

        internal class RaiiFree : CustomAttr
        {
            internal RaiiFree(string freeFunc)
            {
                this.FreeFunc = freeFunc;
            }

            internal string FreeFunc { get; }
        }

        internal class NativeTypedef : CustomAttr
        {
            internal static readonly NativeTypedef Instance = new NativeTypedef();

            private NativeTypedef()
            {
            }
        }

        internal class UnmanagedFunctionPointer : CustomAttr
        {
            internal static readonly UnmanagedFunctionPointer Instance = new UnmanagedFunctionPointer();

            private UnmanagedFunctionPointer()
            {
            }
        }

        internal class ComOutPtr : CustomAttr
        {
            internal static readonly ComOutPtr Instance = new ComOutPtr();

            private ComOutPtr()
            {
            }
        }

        internal class GuidConstAttribute : CustomAttr
        {
            internal static readonly GuidConstAttribute Instance = new GuidConstAttribute();

            private GuidConstAttribute()
            {
            }
        }
    }
}