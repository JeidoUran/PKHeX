# HOME PKM Database preview

This branch adds native, read-only PH4/EH4 sources to the PKM Database and a HOME inspector after OT/Misc. It is based on the user's PKHeX checkout at `697c553bb3789be8ad2acef2c220c177e24831e9`, including the personal icon. It is not an official PKHeX release.

## Behavior

- Individual HOME v4 files are scanned recursively under `LocalResources.DatabasePath`, including absolute paths. `Backup.OtherSaveFileExtensions` applies to save discovery and is not the solution for individual HOME files.
- Each supported file creates one database entry backed by immutable original bytes. A disposable projection from an existing game block supplies the ordinary database columns, search and sprites. Search results therefore describe that projection, not a merged history of all game blocks.
- The full HOME collection is retained independently of the open save by default. `EntityDb.FilterHomeByCurrentSave` opts back into the existing species-presence filter when `FilterUnavailableSpecies` is enabled. Other search filters still apply normally.
- View uses a fresh current-game projection when upstream PKHeX can provide one. The database follows normal trainer adaptation on that disposable copy. When conversion is unavailable, a separate read-only inspector opens; the main editor is left unchanged.
- HOME shows shared fields and every original game block, including the current game's block. Public scalar fields, set TM/TR flags and the exact decoded bytes of the selected block are accessible. Existing playable game blocks can be exported as separate game-format files. Common/Champions blocks have no game-file export here.
- Other editor tabs operate on a disposable game-format copy. Changes there are not written into the HOME source or reflected in the source inspector. Opening an ordinary Pokémon clears the previous HOME source context.
- HOME entries cannot be deleted through the database's individual-file or clone-delete paths. This does not prevent normal game-copy editing or ordinary file/save export elsewhere in PKHeX.

## Format handling and boundaries

HOME files get an explicit extension path before the generic GP1 heuristic. At 432 bytes, valid HOME files can otherwise be mistaken for GP1. Basic length, version, core/side-block bounds, duplicates and encrypted checksum validation run before indexing. Supported input is HOME data version 4, bounded to the plugin's 0x330-byte slot. A supported existing game block is required for an index projection; no block is invented just to index a file. Malformed/unsupported files are skipped by the database loader.

The raw PKH object never enters the database's party-statistics path. This avoids upstream recursion between `PKM.CurrentLevel` and `PKH.Stat_Level`. Original containers are never rebuilt or cloned through PKH's rebuild path, preserving optional blocks such as PC9. This branch does not change HOME memory offsets, botbase, format structures or the upstream conversion algorithms.

Retain one representation of each HOME source in an indexed folder. If both PH4 and EH4 copies are present, both are separate entries; this branch does not silently deduplicate them. No Pokémon files or private fixtures are included in source control or the application package.

## Validation

Synthetic xUnit tests:

```powershell
dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj -c Release --filter FullyQualifiedName~HomeDatabaseTests
```

The Windows integration harness accepts an existing PH4 collection with its `manifest-sha256.json`, an output directory, optionally a sample PH4 path and a clean preview configuration path. It uses the real database loader and View handler, tests all input projections, verifies source hashes before/after, tests the optional species filter, inspects the actual HOME tab, checks location labels and ordinary-load context reset. It never rewrites the collection.

```powershell
dotnet run --project Tests/PKHeX.WinForms.Home.Tests -c Release -- 'C:\path\to\HOME-collection' 'C:\path\to\test-output'
```

Build with a Windows .NET 10-compatible SDK; run with .NET Desktop Runtime 10 x64. The harness is deliberately separate from the normal test solution because it instantiates Windows Forms. Compile/publish does not establish compatibility with a real Switch. This feature works on files already extracted from HOME and adds no console communication.

The encounter/RNG summary in Met is recorded separately in `HOME-DATABASE-BACKLOG.md` and is not implemented in this preview.
