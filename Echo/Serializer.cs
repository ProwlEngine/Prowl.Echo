﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Echo.Logging;
using Prowl.Echo.Formatters;

namespace Prowl.Echo;

public static class Serializer
{
    public static IEchoLogger Logger { get; set; } = new NullEchoLogger();


    private static readonly List<ISerializationFormat> formats = new();

    static Serializer()
    {
        // Register built-in formats in order of precedence
        formats.Add(new PrimitiveFormat());
        formats.Add(new NullableFormat());
        formats.Add(new DateTimeFormat());
        formats.Add(new GuidFormat());
        formats.Add(new EnumFormat());
        formats.Add(new HashSetFormat());
        formats.Add(new CollectionFormat());
        formats.Add(new DictionaryFormat());
        formats.Add(new AnyObjectFormat()); // Fallback format - must be last
    }

    public static void RegisterFormat(ISerializationFormat format)
    {
        formats.Insert(0, format); // Add to start for precedence - Also ensures ObjectFormat is last
    }

    public static EchoObject Serialize(object? value) => Serialize(value, new SerializationContext());

    public static EchoObject Serialize(object? value, SerializationContext context)
    {
        if (value == null) return new EchoObject(PropertyType.Null, null);

        if (value is EchoObject property)
        {
            EchoObject clone = property.Clone();
            HashSet<Guid> deps = new();
            clone.GetAllAssetRefs(ref deps);
            foreach (Guid dep in deps)
                context.AddDependency(dep);
            return clone;
        }

        ISerializationFormat? format = formats.FirstOrDefault(f => f.CanHandle(value.GetType()))
            ?? throw new NotSupportedException($"No format handler found for type {value.GetType()}");

        return format.Serialize(value, context);
    }

    public static T? Deserialize<T>(EchoObject value) => (T?)Deserialize(value, typeof(T));
    public static object? Deserialize(EchoObject value, Type targetType) => Deserialize(value, targetType, new SerializationContext());
    public static T? Deserialize<T>(EchoObject value, SerializationContext context) => (T?)Deserialize(value, typeof(T), context);
    public static object? Deserialize(EchoObject value, Type targetType, SerializationContext context)
    {
        if (value == null || value.TagType == PropertyType.Null) return null;

        if (value.GetType() == targetType) return value;

        ISerializationFormat format = formats.FirstOrDefault(f => f.CanHandle(targetType))
            ?? throw new NotSupportedException($"No format handler found for type {targetType}");

        return format.Deserialize(value, targetType, context);
    }
}
