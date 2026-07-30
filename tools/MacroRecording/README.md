# Macro recording tools

Development-only tools and reference data used to record the RIVALS list-layout
weapon positions.

- `ListWeaponPositionRecorder.ahk` records each weapon position in order.
- `GridWeaponPositionRecorder.ahk` records the same weapons in Grid view.
- `list_weapon_positions.csv` contains the latest recorded reference positions.

Both recorders use `1` (or Numpad `1`) to capture the mouse position,
`Ctrl+Z` to undo, `F8` to show progress, and `F10` to reset.

These files are not required at runtime; SleepStrap embeds the finalized position
data in the application.
