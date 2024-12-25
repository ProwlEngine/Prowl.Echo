﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Text;

namespace Prowl.Echo;

/// <summary>
/// Specifies the binary encoding mode for the Echo format.
/// </summary>
public enum BinaryEncodingMode
{
    /// <summary>
    /// Optimized for performance with fixed-width integers.
    /// Results in larger file sizes but faster read/write operations.
    /// </summary>
    Performance,

    /// <summary>
    /// Optimized for size using LEB128 encoding for integers.
    /// Results in smaller file sizes but slightly slower read/write operations.
    /// </summary>
    Size
}

/// <summary>
/// Configuration options for binary serialization.
/// </summary>
public class BinarySerializationOptions
{
    /// <summary>
    /// Gets or sets the encoding mode for binary serialization.
    /// </summary>
    public BinaryEncodingMode EncodingMode { get; set; } = BinaryEncodingMode.Performance;

    /// <summary>
    /// Creates a new instance of BinarySerializationOptions with default settings.
    /// </summary>
    public static BinarySerializationOptions Default => new();
}

internal static class BinaryTagConverter
{
    private static readonly Dictionary<string, int> SharedEncodeDictionary = new(4096);
    private static readonly Dictionary<int, string> SharedDecodeDictionary = new(4096);
    private static readonly StringBuilder SharedStringBuilder = new(4096);
    private static readonly List<int> SharedCodeList = new(1024);

    #region Writing
    public static void WriteToFile(EchoObject tag, FileInfo file, BinarySerializationOptions? options = null)
    {
        using var stream = file.OpenWrite();
        using var writer = new BinaryWriter(stream);
        WriteTo(tag, writer, options);
    }

    public static void WriteTo(EchoObject tag, BinaryWriter writer, BinarySerializationOptions? options = null)
    {
        options ??= BinarySerializationOptions.Default;
        WriteTag(tag, writer, options);
    }

    private static void WriteCompound(EchoObject tag, BinaryWriter writer, BinarySerializationOptions options)
    {
        if (options.EncodingMode == BinaryEncodingMode.Size)
        {
            LEB128.WriteUnsigned(writer, (ulong)tag.Count);

            // Reuse shared dictionary
            SharedEncodeDictionary.Clear();
            SharedCodeList.Clear();
            for (int i = 0; i < 256; i++)
                SharedEncodeDictionary[((char)i).ToString()] = i;

            int nextCode = 256;

            foreach (var subTag in tag.Tags)
            {
                SharedCodeList.Clear();
                ReadOnlySpan<char> keySpan = subTag.Key.AsSpan();
                if (keySpan.Length == 0)
                {
                    LEB128.WriteUnsigned(writer, 0ul);
                    WriteTag(subTag.Value, writer, options);
                    continue;
                }

                string current = keySpan[0].ToString();
                for (int i = 1; i < keySpan.Length; i++)
                {
                    string combined = current + keySpan[i];
                    if (SharedEncodeDictionary.TryGetValue(combined, out int code))
                    {
                        current = combined;
                    }
                    else
                    {
                        SharedCodeList.Add(SharedEncodeDictionary[current]);
                        // Only add new entries if we haven't exceeded dictionary limit
                        if (nextCode < 4096)
                        {
                            SharedEncodeDictionary[combined] = nextCode++;
                        }
                        current = keySpan[i].ToString();
                    }
                }

                if (current.Length > 0)
                    SharedCodeList.Add(SharedEncodeDictionary[current]);

                // Write compressed field name
                LEB128.WriteUnsigned(writer, (ulong)SharedCodeList.Count);
                foreach (int code in SharedCodeList)
                    LEB128.WriteUnsigned(writer, (ulong)code);

                WriteTag(subTag.Value, writer, options);
            }
        }
        else
        {
            writer.Write(tag.Count);
            foreach (var subTag in tag.Tags)
            {
                byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(subTag.Key);
                writer.Write(stringBytes.Length);
                writer.Write(stringBytes);
                WriteTag(subTag.Value, writer, options);
            }
        }
    }

    private static void WriteTag(EchoObject tag, BinaryWriter writer, BinarySerializationOptions options)
    {
        var type = tag.TagType;
        writer.Write((byte)type);

        if (type == EchoType.Null) { } // Nothing for Null
        else if (type == EchoType.Byte) writer.Write(tag.ByteValue);
        else if (type == EchoType.sByte) writer.Write(tag.sByteValue);
        else if (type == EchoType.Short)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteSigned(writer, tag.ShortValue);
            else
                writer.Write(tag.ShortValue);
        }
        else if (type == EchoType.Int)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteSigned(writer, tag.IntValue);
            else
                writer.Write(tag.IntValue);
        }
        else if (type == EchoType.Long)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteSigned(writer, tag.LongValue);
            else
                writer.Write(tag.LongValue);
        }
        else if (type == EchoType.UShort)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteUnsigned(writer, tag.UShortValue);
            else
                writer.Write(tag.UShortValue);
        }
        else if (type == EchoType.UInt)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteUnsigned(writer, tag.UIntValue);
            else
                writer.Write(tag.UIntValue);
        }
        else if (type == EchoType.ULong)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteUnsigned(writer, tag.ULongValue);
            else
                writer.Write(tag.ULongValue);
        }
        else if (type == EchoType.Float) writer.Write(tag.FloatValue);
        else if (type == EchoType.Double) writer.Write(tag.DoubleValue);
        else if (type == EchoType.Decimal) writer.Write(tag.DecimalValue);
        else if (type == EchoType.String)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
            {
                ReadOnlySpan<char> valueSpan = tag.StringValue.AsSpan();

                SharedEncodeDictionary.Clear();
                SharedCodeList.Clear();
                for (int i = 0; i < 256; i++)
                    SharedEncodeDictionary[((char)i).ToString()] = i;

                int nextCode = 256;

                if (valueSpan.Length == 0)
                {
                    LEB128.WriteUnsigned(writer, 0ul);
                    return;
                }

                string current = valueSpan[0].ToString();
                for (int i = 1; i < valueSpan.Length; i++)
                {
                    string combined = current + valueSpan[i];
                    if (SharedEncodeDictionary.TryGetValue(combined, out int code))
                    {
                        current = combined;
                    }
                    else
                    {
                        SharedCodeList.Add(SharedEncodeDictionary[current]);
                        SharedEncodeDictionary[combined] = nextCode++;
                        current = valueSpan[i].ToString();
                    }
                }

                if (current.Length > 0)
                    SharedCodeList.Add(SharedEncodeDictionary[current]);

                LEB128.WriteUnsigned(writer, (ulong)SharedCodeList.Count);
                foreach (int code in SharedCodeList)
                    LEB128.WriteUnsigned(writer, (ulong)code);
            }
            else
            {
                byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(tag.StringValue);
                writer.Write(stringBytes.Length);
                writer.Write(stringBytes);
            }
        }
        else if (type == EchoType.ByteArray)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteUnsigned(writer, (ulong)tag.ByteArrayValue.Length);
            else
                writer.Write(tag.ByteArrayValue.Length);
            writer.Write(tag.ByteArrayValue);
        }
        else if (type == EchoType.Bool) writer.Write(tag.BoolValue);
        else if (type == EchoType.List)
        {
            var listTag = tag;
            if (options.EncodingMode == BinaryEncodingMode.Size)
                LEB128.WriteUnsigned(writer, (ulong)listTag.Count);
            else
                writer.Write(listTag.Count);
            foreach (var subTag in listTag.List)
                WriteTag(subTag, writer, options);
        }
        else if (type == EchoType.Compound) WriteCompound(tag, writer, options);
        else throw new Exception($"Unknown tag type: {type}");
    }
    #endregion

    #region Reading
    public static EchoObject ReadFromFile(FileInfo file, BinarySerializationOptions? options = null)
    {
        using var stream = file.OpenRead();
        using var reader = new BinaryReader(stream);
        return ReadFrom(reader, options);
    }

    public static EchoObject ReadFrom(BinaryReader reader, BinarySerializationOptions? options = null)
    {
        return ReadTag(reader, options ?? BinarySerializationOptions.Default);
    }

    private static EchoObject ReadCompound(BinaryReader reader, BinarySerializationOptions options)
    {
        EchoObject tag = EchoObject.NewCompound();
        int tagCount;

        if (options.EncodingMode == BinaryEncodingMode.Size)
        {
            tagCount = (int)LEB128.ReadUnsigned(reader);

            SharedDecodeDictionary.Clear();
            SharedCodeList.Clear();
            SharedStringBuilder.Clear();

            for (int i = 0; i < 256; i++)
                SharedDecodeDictionary[i] = ((char)i).ToString();

            int nextCode = 256;  // Track dictionary size

            for (int i = 0; i < tagCount; i++)
            {
                int nameCodesCount = (int)LEB128.ReadUnsigned(reader);
                if (nameCodesCount == 0)
                {
                    tag.Add(string.Empty, ReadTag(reader, options));
                    continue;
                }

                SharedCodeList.Clear();
                for (int j = 0; j < nameCodesCount; j++)
                    SharedCodeList.Add((int)LEB128.ReadUnsigned(reader));

                SharedStringBuilder.Clear();
                string current = SharedDecodeDictionary[SharedCodeList[0]];
                SharedStringBuilder.Append(current);

                for (int j = 1; j < SharedCodeList.Count; j++)
                {
                    int code = SharedCodeList[j];
                    string entry;

                    if (SharedDecodeDictionary.TryGetValue(code, out string? value))
                    {
                        entry = value;
                    }
                    else if (code == nextCode) 
                    {
                        entry = current + current[0];
                    }
                    else
                    {
                        throw new Exception($"Invalid compressed field name data: code {code} not found in dictionary");
                    }

                    SharedStringBuilder.Append(entry);

                    // Only add new entries if we haven't exceeded dictionary limit
                    if (nextCode < 4096)
                    {
                        SharedDecodeDictionary[nextCode++] = current + entry[0];
                    }
                    current = entry;
                }

                tag.Add(SharedStringBuilder.ToString(), ReadTag(reader, options));
            }
        }
        else
        {
            tagCount = reader.ReadInt32();
            for (int i = 0; i < tagCount; i++)
            {
                int nameLength = reader.ReadInt32();
                byte[] nameBytes = reader.ReadBytes(nameLength);
                string name = System.Text.Encoding.UTF8.GetString(nameBytes);
                tag.Add(name, ReadTag(reader, options));
            }
        }
        return tag;
    }

    private static EchoObject ReadTag(BinaryReader reader, BinarySerializationOptions options)
    {
        var type = (EchoType)reader.ReadByte();

        if (type == EchoType.Null) return new(EchoType.Null, null);
        else if (type == EchoType.Byte) return new(EchoType.Byte, reader.ReadByte());
        else if (type == EchoType.sByte) return new(EchoType.sByte, reader.ReadSByte());
        else if (type == EchoType.Short)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                (short)LEB128.ReadSigned(reader) : reader.ReadInt16();
            return new(EchoType.Short, value);
        }
        else if (type == EchoType.Int)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                (int)LEB128.ReadSigned(reader) : reader.ReadInt32();
            return new(EchoType.Int, value);
        }
        else if (type == EchoType.Long)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                LEB128.ReadSigned(reader) : reader.ReadInt64();
            return new(EchoType.Long, value);
        }
        else if (type == EchoType.UShort)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                (ushort)LEB128.ReadUnsigned(reader) : reader.ReadUInt16();
            return new(EchoType.UShort, value);
        }
        else if (type == EchoType.UInt)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                (uint)LEB128.ReadUnsigned(reader) : reader.ReadUInt32();
            return new(EchoType.UInt, value);
        }
        else if (type == EchoType.ULong)
        {
            var value = options.EncodingMode == BinaryEncodingMode.Size ?
                LEB128.ReadUnsigned(reader) : reader.ReadUInt64();
            return new(EchoType.ULong, value);
        }
        else if (type == EchoType.Float) return new(EchoType.Float, reader.ReadSingle());
        else if (type == EchoType.Double) return new(EchoType.Double, reader.ReadDouble());
        else if (type == EchoType.Decimal) return new(EchoType.Decimal, reader.ReadDecimal());
        else if (type == EchoType.String)
        {
            if (options.EncodingMode == BinaryEncodingMode.Size)
            {
                int codesCount = (int)LEB128.ReadUnsigned(reader);
                if (codesCount == 0)
                    return new(EchoType.String, string.Empty);

                // Reuse shared collections
                SharedDecodeDictionary.Clear();
                SharedCodeList.Clear();
                SharedStringBuilder.Clear();

                for (int i = 0; i < 256; i++)
                    SharedDecodeDictionary[i] = ((char)i).ToString();

                // Read all codes at once
                for (int i = 0; i < codesCount; i++)
                    SharedCodeList.Add((int)LEB128.ReadUnsigned(reader));

                // Fast decompression
                string current = SharedDecodeDictionary[SharedCodeList[0]];
                SharedStringBuilder.Append(current);
                int nextCode = 256;

                // Process remaining codes
                for (int i = 1; i < codesCount; i++)
                {
                    int code = SharedCodeList[i];
                    string entry;

                    if (SharedDecodeDictionary.TryGetValue(code, out string? value))
                        entry = value;
                    else if (code == nextCode)
                        entry = current + current[0];
                    else
                        throw new Exception("Invalid compressed string data");

                    SharedStringBuilder.Append(entry);
                    SharedDecodeDictionary[nextCode++] = current + entry[0];
                    current = entry;
                }

                return new(EchoType.String, SharedStringBuilder.ToString());
            }
            else
            {
                int length = reader.ReadInt32();
                byte[] stringBytes = reader.ReadBytes(length);
                return new(EchoType.String, System.Text.Encoding.UTF8.GetString(stringBytes));
            }
        }
        else if (type == EchoType.ByteArray)
        {
            int length;
            if (options.EncodingMode == BinaryEncodingMode.Size)
                length = (int)LEB128.ReadUnsigned(reader);
            else
                length = reader.ReadInt32();
            return new(EchoType.ByteArray, reader.ReadBytes(length));
        }
        else if (type == EchoType.Bool) return new(EchoType.Bool, reader.ReadBoolean());
        else if (type == EchoType.List)
        {
            var listTag = EchoObject.NewList();
            int tagCount;
            if (options.EncodingMode == BinaryEncodingMode.Size)
                tagCount = (int)LEB128.ReadUnsigned(reader);
            else
                tagCount = reader.ReadInt32();
            for (int i = 0; i < tagCount; i++)
                listTag.ListAdd(ReadTag(reader, options));
            return listTag;
        }
        else if (type == EchoType.Compound) return ReadCompound(reader, options);
        else throw new Exception($"Unknown tag type: {type}");
    }
    #endregion
}