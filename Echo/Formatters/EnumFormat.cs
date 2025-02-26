﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

namespace Prowl.Echo.Formatters;

internal sealed class EnumFormat : ISerializationFormat
{
    public bool CanHandle(Type type) => type.IsEnum;

    public EchoObject Serialize(Type? targetType, object value, SerializationContext context)
    {
        if (value is Enum e)
            return new(EchoType.Int, Convert.ToInt32(e));

        throw new NotSupportedException($"Type '{value.GetType()}' is not supported by EnumFormat.");
    }

    public object? Deserialize(EchoObject value, Type targetType, SerializationContext context)
    {
        if (value.TagType != EchoType.Int)
            throw new Exception($"Expected Int type for Enum, got {value.TagType}");

        return Enum.ToObject(targetType, value.IntValue);
    }
}
