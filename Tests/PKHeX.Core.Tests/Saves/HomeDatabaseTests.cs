using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core.Tests.Saves;

public sealed class HomeDatabaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsPlainAndEncryptedWithoutChangingInput(bool encrypt)
    {
        var data = MakeHome(HomeGameDataFormat.PK8, HomeGameDataFormat.PK9);
        if (encrypt) data = HomeCrypto.Encrypt(data);
        var before = data.ToArray();
        Assert.True(HomeFile.TryCreate(data, out var source));
        Assert.Equal(before, data);
        Assert.Equal(before, source.GetOriginalData());
        Assert.Equal((ushort)25, source.CreateEntity().Species);
    }

    [Fact]
    public void ExplicitHomeWinsOver432ByteGP1Collision()
    {
        var data = MakeHome(HomeGameDataFormat.PK8, HomeGameDataFormat.PK9, HomeGameDataFormat.PB8);
        Assert.Equal(432, data.Length);
        data[0x28] = 1; // Satisfies the legacy GP1 size/value heuristic.
        Assert.IsType<PKH>(FileUtil.GetSupportedFile(data, ".ph4"));
        Assert.True(HomeFile.TryCreate(data, out _));
    }

    [Fact]
    public void KeepsEveryBlockIncludingChampions()
    {
        var formats = new[] { HomeGameDataFormat.PK8, HomeGameDataFormat.PK9, HomeGameDataFormat.PC9 };
        Assert.True(HomeFile.TryCreate(MakeHome(formats), out var source));
        Assert.Equal(formats, source.AvailableFormats.ToArray());
        Assert.Equal(25, source.GetBlockBytes(HomeGameDataFormat.PC9).Length);
        Assert.IsType<GameDataPC9>(source.GetBlock(HomeGameDataFormat.PC9));
        Assert.Null(source.CreateGameEntity(HomeGameDataFormat.PA9));
    }

    [Fact]
    public void ViewsCannotMutateTheSource()
    {
        var data = MakeHome(HomeGameDataFormat.PK8, HomeGameDataFormat.PK9, HomeGameDataFormat.PC9);
        Assert.True(HomeFile.TryCreate(data, out var source));
        var original = source.GetOriginalData();
        source.GetOriginalData()[0] = 99;
        source.CreateEntity().Species = 1;
        var game = source.CreateProjection()!;
        game.Move1 = 999; game.Nickname = "Changed"; game.ForcePartyData();
        ((GameDataPK9)source.GetBlock(HomeGameDataFormat.PK9)!).Move1 = 777;
        source.GetBlockBytes(HomeGameDataFormat.PC9)[0] = 255;
        Assert.Equal(original, source.GetOriginalData());
        Assert.Equal((ushort)25, source.CreateEntity().Species);
        Assert.Equal((ushort)0, source.CreateGameEntity(HomeGameDataFormat.PK9)!.Move1);
    }

    [Fact]
    public void IndexProjectionDoesNotDependOnOpenSave()
    {
        var data = MakeHome(HomeGameDataFormat.PK8);
        WriteUInt16LittleEndian(data.AsSpan(0x12 + 0xD), (ushort)Species.Machamp);
        Assert.True(HomeFile.TryCreate(data, out var source));
        Assert.IsType<PK8>(source.CreateProjection());
        Assert.Null(source.CreateCompatibleEntity(typeof(PK9)));
        Assert.IsType<PK8>(source.CreateCompatibleEntity(typeof(PK8)));
    }

    [Fact]
    public void ExistingBlocksArePreferredAndReconstructionIsExplicit()
    {
        Assert.True(HomeFile.TryCreate(MakeHome(HomeGameDataFormat.PK8), out var source));
        Assert.Null(source.CreateGameEntity(HomeGameDataFormat.PK9));
        Assert.IsType<PK9>(source.CreateGameEntity(HomeGameDataFormat.PK9, true));
        Assert.False(source.HasFormat(HomeGameDataFormat.PK9));
    }

    [Theory]
    [InlineData(".ph4")]
    [InlineData(".eh4")]
    [InlineData(".PH4")]
    public void DatabaseLoadsOneReadonlySource(string extension)
    {
        var data = MakeHome(HomeGameDataFormat.PK8, HomeGameDataFormat.PK9);
        if (extension == ".eh4") data = HomeCrypto.Encrypt(data);
        WithFile(data, extension, path =>
        {
            var entries = new ConcurrentBag<SlotCache>();
            // The HOME path must work without adding its extensions to the ordinary PKM list.
            SlotInfoLoader.AddFromLocalFile(path, entries, new SimpleTrainerInfo(GameVersion.VL), new HashSet<string>());
            var entry = Assert.Single(entries);
            Assert.IsType<PK9>(entry.Entity);
            var home = Assert.IsType<SlotInfoFileHome>(entry.Source);
            Assert.False(home.CanWriteTo(new SAV9SV()));
            Assert.False(entry.Source is SlotInfoFileSingle);
            Assert.Equal(path, home.Path);
            entry.Entity.ForcePartyData(); // Never call this on raw PKH.
            Assert.Equal(data, File.ReadAllBytes(path));
            Assert.IsType<PKH>(FileUtil.GetSingleFromPath(path, new SimpleTrainerInfo(GameVersion.VL)));
        });
    }

    [Fact]
    public void OrdinaryPK9LoaderStillWorks()
    {
        var entity = new PK9 { Species = 25, Version = GameVersion.VL };
        entity.RefreshChecksum();
        var data = new byte[entity.SIZE_PARTY]; entity.WriteDecryptedDataParty(data);
        WithFile(data, ".pk9", path =>
        {
            var entries = new ConcurrentBag<SlotCache>();
            SlotInfoLoader.AddFromLocalFile(path, entries, new SimpleTrainerInfo(GameVersion.VL), new HashSet<string> { ".pk9" });
            Assert.IsType<SlotInfoFileSingle>(Assert.Single(entries).Source);
        });
    }

    [Fact]
    public void RejectsTruncatedUnknownAndMalformedFiles()
    {
        Assert.False(HomeFile.TryCreate([], out _));
        var data = MakeHome(HomeGameDataFormat.PK8);
        Assert.False(HomeFile.TryCreate(data[..^1], out _));
        var unknown = data.ToArray(); WriteUInt16LittleEndian(unknown, 99);
        Assert.False(HomeFile.TryCreate(unknown, out _));
        var badSize = data.ToArray(); WriteUInt16LittleEndian(badSize.AsSpan(0xE), ushort.MaxValue);
        Assert.False(HomeFile.TryCreate(badSize, out _));
        var badSide = data.ToArray(); WriteUInt16LittleEndian(badSide.AsSpan(0x12 + 0xC4), ushort.MaxValue);
        Assert.False(HomeFile.TryCreate(badSide, out _));
        var badChecksum = HomeCrypto.Encrypt(data); badChecksum[0xA] ^= 1;
        Assert.False(HomeFile.TryCreate(badChecksum, out _));
        Assert.Null(FileUtil.GetSupportedFile(unknown, ".ph4"));
    }

    private static void WithFile(byte[] data, string extension, Action<string> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pkhex-home-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "sample" + extension);
        try { File.WriteAllBytes(path, data); action(path); }
        finally { File.Delete(path); Directory.Delete(dir); }
    }

    private static byte[] MakeHome(params HomeGameDataFormat[] formats)
    {
        int Size(HomeGameDataFormat f) => f switch
        {
            HomeGameDataFormat.PK8 => 72, HomeGameDataFormat.PK9 => 74,
            HomeGameDataFormat.PB8 => 47, HomeGameDataFormat.PB7 => 63,
            HomeGameDataFormat.PA8 or HomeGameDataFormat.PA9 => 64,
            HomeGameDataFormat.PC9 => 25, _ => throw new ArgumentOutOfRangeException(nameof(f)),
        };
        var gameSize = formats.Sum(f => 3 + Size(f));
        var payload = (4 + 196 + gameSize + 15) & ~15;
        var data = new byte[16 + payload];
        WriteUInt16LittleEndian(data, 4);
        WriteUInt64LittleEndian(data.AsSpan(2), 0x1122334455667788);
        WriteUInt16LittleEndian(data.AsSpan(14), (ushort)payload);
        WriteUInt16LittleEndian(data.AsSpan(16), 196);
        WriteUInt16LittleEndian(data.AsSpan(0x12 + 0xD), 25);
        data[0x12 + 0x91] = (byte)GameVersion.SW;
        WriteUInt16LittleEndian(data.AsSpan(0x12 + 196), (ushort)gameSize);
        var offset = 0x14 + 196;
        foreach (var format in formats)
        {
            data[offset] = (byte)format;
            WriteUInt16LittleEndian(data.AsSpan(offset + 1), (ushort)Size(format));
            offset += 3 + Size(format);
        }
        return data;
    }
}
