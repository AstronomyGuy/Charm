﻿using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Internal.Fbx;
using Microsoft.VisualBasic.FileIO;
using Tiger;

namespace Tiger;

// Tag has a custom deserialiser that discards the inherited SchemaType attribute, as can be Tag32 or Tag64
public class Tag<T> : TigerFile where T : struct
{
    protected T _tag;
    // separated as it should be a red flag if we're using this
    [Obsolete("Use TagData sparingly as it breaks the Law of Demeter; instead isolate code in owning structures.")]
    public T TagData => _tag;

    // todo verify that T is valid for the hash we get given by checking SchemaStruct against hash reference
    protected Tag(FileHash fileHash, bool shouldParse = true) : base(fileHash)
    {
        if (shouldParse)
        {
            Initialise(fileHash);
        }
    }

    protected Tag(FileHash fileHash) : base(fileHash)
    {
        Initialise(fileHash);
    }

    private void Initialise(FileHash fileHash)
    {
        if (fileHash.IsValid())
        {
            Deserialize();
        }
        else
        {
            _tag = default;
        }
    }

    private void Deserialize()
    {
        using (TigerReader reader = GetReader())
        {
            _tag = SchemaDeserializer.Get().DeserializeSchema<T>(reader);
        }
    }
}

/// <summary>
///  A stub-type of <see cref="Tag{T}"/> that simply represents we know a file is there, but we don't care about it.
/// </summary>
public class Tag : TigerFile
{
    public Tag(FileHash fileHash) : base(fileHash)
    {
    }
}
