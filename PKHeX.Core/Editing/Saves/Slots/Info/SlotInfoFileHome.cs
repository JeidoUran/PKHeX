namespace PKHeX.Core;

/// <summary>A read-only HOME source; the cached entity is only a game-format projection.</summary>
public sealed record SlotInfoFileHome(string FilePath, HomeFile Home) : SlotInfoFileMulti(FilePath, 0, 1);
