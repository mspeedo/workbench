# Streets of Rogue 1 — BepInEx 5 plugins

## LargerText v2.3.0 — layout-aware zoom

This version builds on v2.2.0, which the user confirmed kept text visible. It addresses the over-conservative `PreventTextClipping` guard that also suppressed enlargement for modifier descriptions and many other UI labels. There are **no menu-specific or modifier-specific exceptions**.

### Defaults

- `Multiplier = 1.20` — increase eligible text by up to 20%.
- `ProtectLargeText = true` — title/heading protection and a one-time large-font tier per scene, as in v2.1/v2.2.
- `PreventTextClipping = true` — protect only genuine new clipping introduced by the mod.

### How v2.3's clipping guard works

For **all Unity UI text components**:

1. Leave the game's original `resizeTextForBestFit` setting intact. If Best Fit is enabled, let Unity handle fitting; do not reject the change solely because `preferredHeight` exceeds the rectangle.
2. If Unity explicitly uses `verticalOverflow = Overflow`, do **not** apply an inappropriate rectangle-height cutoff: the text is designed to extend beyond its nominal UI rectangle.
3. If Unity uses `verticalOverflow = Truncate` without Best Fit, check whether the enlarged text height exceeds its real text rectangle.
4. When that happens, compare against the same text at the **original font size**. Only fall back if the original fit and the enlargement would newly cause truncation. The measurement temporarily changes the fontSize within a re-entrancy guard, restores the enlarged size, and only runs for suspected clipping.

This guards all labels, descriptions, dialogue, and menu elements using the same rule. It avoids the previous mistake of treating every `preferredHeight > rect.height` as a failure even when the game's layout intentionally allows overflow or adjusts font sizes.

### Performance

Keeps the **v2.0** startup/scene-load scan and event hooks, which worked in the game. No 0.5-second recurring scans, no `Update()`, no overlay, and no settings migration.

### Install

Replace `BepInEx/plugins/LargerText.dll`. To restore v2.3 defaults, while the game is closed delete only `BepInEx/config/mspeedo.sor.larger-text.cfg`, or manually set `Multiplier = 1.2`, `ProtectLargeText = true`, and `PreventTextClipping = true` in the config.

This version has been compiled, but the revised clipping logic still needs in-game testing.

## UnlimitedRetries v0.2.0 — unchanged

Works with the game's built-in **Continue?** mutator by replenishing its active counter. Manual restart of the current floor without dying is not implemented.

## Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

Compiled by GitHub Actions using public NuGet references only, with no proprietary game DLLs in the repository.
