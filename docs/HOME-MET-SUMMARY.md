# Fixed HOME consultation build

This branch starts from the original HOME database branch, not the adaptive workspace. It preserves the HOME source filter and visible status control, and adds one read-only encounter summary to the existing Met tab. There is no resizable window, multi-box workspace, simultaneous-tab system or new layout option. Standard Box Viewer windows remain unchanged.

The fixed window keeps its original width and gains 192 logical pixels of static height for the summary. The original Met fields occupy the upper area; the summary is docked below them. The field area can scroll if a game's conditional fields need more space. The status control is positioned below the actual navigation entries, avoiding the seventh-tab overlap.

## Encounter data

`EncounterSummaryView` calls `LegalityFormatting.AddEncounterInfo` with the editor's existing `LegalityAnalysis` and current language. This is the same section builder used by the default verbose legality report. It does not parse human-readable report text or introduce a second RNG reconstruction engine. The hover and legality report remain unchanged, and no verbose-report setting needs to be enabled to see this section in Met.

The summary is formatted lazily when Met is selected. It is invalidated when the editor updates its analysis, preventing an old Pokémon's seeds/frames remaining visible. Changing language reformats the same analysis. An unavailable/empty analysis replaces the previous text. Valid and invalid Pokémon with parsed analyses can both expose the original formatter's candidate encounter data.

The exact fields depend on PKHeX's available correlation: encounter type, candidate original location, PID/RNG method, seed, initial seed/frame, time or compatible date/time when the existing formatter provides them. No value is invented when a method does not provide it. The inference note distinguishes a compatible reconstruction from uniquely proven historical capture facts.

Only display controls and their layout change. The formatter may populate cached analysis information, as it already does for the verbose report; Pokémon bytes and files are not rewritten. HOME source containers remain immutable.

## Retained source filter

Database Format → HOME (PH4/EH4) filters by the HOME source type, independently of the game-format projection used for ordinary database columns. Existing Gen8/Gen9/Gen8b context filters keep their behavior. HOME has no numeric ordering among those contexts, so this source choice uses equality. Origin game/generation filters are not changed.

## Validation

The Windows integration harness checks fixed-window behavior, absence of adaptive panes, the status/HOME geometry, mixed HOME and ordinary source filtering, GBA/DS frames, exact equality with the existing verbose report block in English and French, entity/file immutability, lazy refresh, clearing stale reconstruction text and non-overlapping Met areas. It reuses upstream legality fixtures and the local HOME collection without versioning personal Pokémon files.

The adaptive branch and prior application archive are retained separately as a proof of concept. They are not part of this branch and are not a prerequisite for these consultation features.
