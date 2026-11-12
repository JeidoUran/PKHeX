# Adaptive HOME workspace preview

Built on `feature/home-pkm-database`. This is a separate experimental UI branch; neither `net10` nor the HOME source format changes.

## Main window

The main window is resizable/maximizable. Its minimum preserves the previous editor width and adds space for a visible status control below the seven navigation entries. The status control retains the existing local game-copy behavior; no HOME write/injection is introduced. With no status icon it displays an explicit Status label instead of an invisible empty target.

The menu bar keeps a fixed height. Automatic layout reserves a compact left editor and uses additional width for a second editor column once it fits. The save pane receives the remaining width. The splitter can be dragged manually; doing so disables automatic allocation. Options → Automatic pane layout restores it.

## Embedded box views

The save's Boxes tab fits as many complete native-size grids as its available width/height allow, up to the save's box count. It retains the original primary box, pop-out button, standalone viewers and box search.

Extra views show sequential boxes by default. Moving the primary box advances all following views and wraps around the final box. Selecting a box or using its arrows in a secondary view unchecks Follow the first box for that view only. Checking Follow reattaches it to the sequence. The independent selection survives a shrink/expand cycle in the same save; opening another save clears these selections.

Every view reuses BoxEditor, the same SlotChangeManager, context menu and slot publisher. Existing local-save editing, drag/drop and undo/redo routes are retained. Removing a view unsubscribes/disposes it, and changing saves rebuilds views against the new save. Merely arranging, pinning and navigating views does not change Pokémon box bytes. Primary navigation retains PKHeX's existing current-box metadata behavior.

## Simultaneous editor tabs

The left editor fits complete content panes horizontally and vertically. The original navigation controls the primary pane. Additional panes have a selector for any other available tab. Selecting an already-shown secondary tab swaps panes; choosing a secondary tab through the original navigation swaps it with the previous primary tab. No editable field or Pokémon object is duplicated: the original controls are moved between hosts, preserving pending input. Shrinking restores them to their original tab pages.

Tabs unavailable in the current generation are excluded. The HOME inspector remains a read-only view of the immutable source, while ordinary fields continue to edit a disposable game-format copy. This preview does not add the deferred encounter/RNG summary to Met.

## Database format filter

The existing Gen1/Gen8/Gen9/Gen8b values are PKHeX game contexts, not filename extensions or original encounter generations. Database-only HOME (PH4/EH4) filters by SlotInfoFileHome regardless of the projection used for columns/search. HOME has no numeric ordering relative to Gen8/Gen9, so the comparison is locked to equality for this option. Selecting a normal context restores its existing comparison control. Any includes both HOME and ordinary entries.

Origin generation and origin version filters are unchanged: HOME data version 4 does not imply a Gen4 Pokémon. Non-format filters still operate on the existing game projection, not all stored blocks simultaneously.

## Validation

The Windows integration harness in Tests/PKHeX.WinForms.Home.Tests covers mixed-source filtering, minimum status geometry, expanded boxes, toolbar visibility, linked/pinned/wrap navigation, shrink-expand cleanup and pin retention, pending editor text, tab selector changes, original source hashes, ordinary slot notification/undo/redo, and changes between Violet, Brilliant Diamond, Ruby, Crystal and Red blank saves. Synthetic edits occur only in memory. It produces compact/large rendering captures.

No real game save or HOME source is rewritten by these checks. A separate application instance is used for native visual inspection. Real mouse drag/drop under the user's workflow and display scaling across different monitors still merit user testing; these are not established by compilation alone. Layout choices/window size are not persisted as a new setting in this first preview.
