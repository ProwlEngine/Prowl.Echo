﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Echo;

namespace Tests.Types;

public class CustomSerializableObject : ISerializable
{
    public int Value = 42;
    public string Text = "Custom";

    public void Serialize(ref EchoObject compound, SerializationContext ctx)
    {
        compound.Add("customValue", new EchoObject(EchoType.Int, Value));
        compound.Add("customText", new EchoObject(EchoType.String, Text));
    }

    public void Deserialize(EchoObject tag, SerializationContext ctx)
    {
        Value = tag.Get("customValue").IntValue;
        Text = tag.Get("customText").StringValue;
    }
}
