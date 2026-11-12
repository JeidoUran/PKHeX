# HOME database follow-up

## Deferred: encounter reconstruction in the Met tab

Requested on 2026-11-12. Do not implement while the PH4 database/inspector work is in progress.

Display the existing verbose legality encounter/RNG summary directly in the **Met** tab, while retaining its existing hover and legality-report locations. Include the encounter type, inferred original location, PID/RNG method, origin/initial seeds, frame and compatible date/time information when the analyzer provides them.

Reuse the existing analyzer output rather than introducing an independent reconstruction engine. Distinguish inferred compatible origins and possible ambiguities from facts stored in the Pokémon: not every reconstructed frame/date/location uniquely proves its actual historical capture. The request is illustrated by the user's GBA and DS legality-report screenshots.
