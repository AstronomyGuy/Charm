﻿namespace Tiger;

/// <summary>
/// Represents the file behind the Tag, handles all the file-based processing including reading data.
/// To make this thread-safe (particularly the MemoryStream handle) we ensure the byte[] data is cached and only ever
/// read once, then generate a new handle each time GetReader is called. This should only really ever be used
/// in a dispose-oriented system, as otherwise you will have many leftover streams causing leakage.
/// We store the data in this file instead of in PackageHandler BytesCache as we expect one DestinyFile to be made
/// per tag, never more (as long as PackageHandler GetTag is used).
/// </summary>
[SchemaType(0x4)]
public class TigerFile
{
    public readonly FileHash Hash;
    public readonly TigerHash ReferenceHash;
    private byte[]? _data = null;

    public TigerFile(FileHash hash)
    {
        Hash = hash;
    }

    public TigerReader GetReader()
    {
        return new TigerReader(GetStream());
    }

    public MemoryStream GetStream()
    {
        return new MemoryStream(GetData());
    }

    public byte[] GetData(bool shouldCache = true)
    {
        if (shouldCache)
        {
            if (_data == null)
            {
                _data = PackageResourcer.Get().GetFileData(Hash);
            }

            return _data;
        }
        else
        {
            return PackageResourcer.Get().GetFileData(Hash);
        }
    }

    public override int GetHashCode()
    {
        return (int)Hash.Hash32;
    }

    public override bool Equals(object? obj)
    {
        if (obj is TigerFile other)
        {
            return Hash == other.Hash;
        }

        return false;
    }
}

public class TigerReferenceFile<THeader> : Tag<THeader> where THeader : struct
{
    protected FileHash ReferenceHash;

    public TigerReferenceFile(FileHash fileHash) : base(fileHash)
    {
        ReferenceHash = fileHash.GetReferenceHash();
    }

    public TigerReader GetReferenceReader()
    {
        return new TigerReader(GetReferenceStream());
    }

    public MemoryStream GetReferenceStream()
    {
        return new MemoryStream(GetReferenceData());
    }

    public byte[] GetReferenceData()
    {
        return PackageResourcer.Get().GetFileData(ReferenceHash);
    }
}
