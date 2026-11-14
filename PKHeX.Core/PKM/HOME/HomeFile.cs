using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>An immutable HOME source, separate from disposable game-format views.</summary>
public sealed class HomeFile
{
    private readonly byte[] Original;
    private readonly HomeGameDataFormat[] Formats;
    public ReadOnlySpan<HomeGameDataFormat> AvailableFormats => Formats;

    private HomeFile(byte[] original, HomeGameDataFormat[] formats)
    {
        Original = original;
        Formats = formats;
    }

    public static bool IsHomeExtension(ReadOnlySpan<char> extension) =>
        extension.Equals(".ph4", StringComparison.OrdinalIgnoreCase) || extension.Equals(".eh4", StringComparison.OrdinalIgnoreCase);

    public static bool TryCreate(ReadOnlySpan<byte> data, [NotNullWhen(true)] out HomeFile? file)
    {
        file = null;
        if (data.Length is < (HomeCrypto.SIZE_1HEADER + 4 + HomeCrypto.SIZE_CORE) or > 0x330)
            return false;
        if (ReadUInt16LittleEndian(data) != HomeCrypto.Version4)
            return false;
        if (HomeCrypto.SIZE_1HEADER + ReadUInt16LittleEndian(data[0xE..]) != data.Length)
            return false;
        try
        {
            if (HomeCrypto.GetIsEncrypted(data, HomeCrypto.Version4)
                && ReadUInt32LittleEndian(data[0xA..]) != HomeCrypto.GetChecksum1(data))
                return false;
            var original = data.ToArray();
            var home = new PKH(original.AsMemory().ToArray());
            if (home.CoreDataSize != HomeCrypto.SIZE_CORE || home.Species is 0 or >= (ushort)Species.MAX_COUNT)
                return false;
            // Constructors validate each optional block. Also require its size field to fit.
            var span = home.Data;
            if (home.GameDataSize > span.Length - (HomeCrypto.SIZE_1HEADER + 4 + home.CoreDataSize))
                return false;
            var formats = new List<HomeGameDataFormat>();
            var side = span.Slice(HomeCrypto.SIZE_1HEADER + 4 + home.CoreDataSize, home.GameDataSize);
            while (!side.IsEmpty)
            {
                if (side.Length < HomeOptional1.HeaderSize) return false;
                var format = (HomeGameDataFormat)side[0];
                var size = ReadUInt16LittleEndian(side[1..]);
                if (size > side.Length - HomeOptional1.HeaderSize || formats.Contains(format)) return false;
                formats.Add(format);
                side = side[(HomeOptional1.HeaderSize + size)..];
            }
            file = new HomeFile(original, formats.ToArray());
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException or InvalidDataException)
        {
            return false;
        }
    }

    public byte[] GetOriginalData() => (byte[])Original.Clone();
    public PKH CreateEntity() => new(GetOriginalData());
    public bool HasFormat(HomeGameDataFormat format) => Array.IndexOf(Formats, format) >= 0;

    /// <summary>Use an existing game block for indexing, independent of the open save.</summary>
    public PKM? CreateProjection()
    {
        var home = CreateEntity();
        if (home.DataPK9 is { } pk9) return pk9.ConvertToPKM(home);
        if (home.DataPA9 is { } pa9) return pa9.ConvertToPKM(home);
        if (home.DataPK8 is { } pk8) return pk8.ConvertToPKM(home);
        if (home.DataPB8 is { } pb8) return pb8.ConvertToPKM(home);
        if (home.DataPA8 is { } pa8) return pa8.ConvertToPKM(home);
        if (home.DataPB7 is { } pb7) return pb7.ConvertToPKM(home);
        return null;
    }

    public PKM? CreateGameEntity(HomeGameDataFormat format, bool allowReconstruction = false)
    {
        if (!allowReconstruction && !HasFormat(format)) return null;
        return CreateEntity().ConvertToPKM(format);
    }

    public PKM? CreateCompatibleEntity(Type destination)
    {
        var home = CreateEntity();
        bool present = destination == typeof(PK9) ? PersonalTable.SV.IsPresentInGame(home.Species, home.Form)
            : destination == typeof(PK8) ? PersonalTable.SWSH.IsPresentInGame(home.Species, home.Form)
            : destination == typeof(PB8) ? PersonalTable.BDSP.IsPresentInGame(home.Species, home.Form)
            : destination == typeof(PA8) ? PersonalTable.LA.IsPresentInGame(home.Species, home.Form)
            : destination == typeof(PA9) ? PersonalTable.ZA.IsPresentInGame(home.Species, home.Form)
            : destination == typeof(PB7) && PersonalTable.GG.IsPresentInGame(home.Species, home.Form);
        return present ? home.ConvertToPKM(PKH.GetType(destination)) : null;
    }

    /// <summary>Returns a block belonging to a fresh copy; callers cannot alter the source.</summary>
    public object? GetBlock(HomeGameDataFormat format)
    {
        var home = CreateEntity();
        return format switch
        {
            HomeGameDataFormat.None => home.Core,
            HomeGameDataFormat.PK8 => home.DataPK8,
            HomeGameDataFormat.PB7 => home.DataPB7,
            HomeGameDataFormat.PB8 => home.DataPB8,
            HomeGameDataFormat.PA8 => home.DataPA8,
            HomeGameDataFormat.PK9 => home.DataPK9,
            HomeGameDataFormat.PA9 => home.DataPA9,
            HomeGameDataFormat.PC9 => home.DataPC9,
            _ => null,
        };
    }

    public byte[] GetBlockBytes(HomeGameDataFormat format)
    {
        var home = CreateEntity();
        if (format == HomeGameDataFormat.None)
            return home.Data.Slice(HomeCrypto.SIZE_1HEADER + 2, home.CoreDataSize).ToArray();
        var side = home.Data.Slice(HomeCrypto.SIZE_1HEADER + 4 + home.CoreDataSize, home.GameDataSize);
        while (!side.IsEmpty)
        {
            var size = ReadUInt16LittleEndian(side[1..]);
            if ((HomeGameDataFormat)side[0] == format)
                return side.Slice(HomeOptional1.HeaderSize, size).ToArray();
            side = side[(HomeOptional1.HeaderSize + size)..];
        }
        return [];
    }
}
