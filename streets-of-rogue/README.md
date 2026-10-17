# Streets of Rogue 1 — BepInEx 5 mods

## LargerText v2.4.0 — Best Fit support and diagnostic release

This release retains the **working v2.0/v2.3 scene-load scan and event hooks**. It does not use a 0.5-second update timer, overlay, special cases for mutators/quests or settings migration.

### What changed

- **Best Fit ceilings are scaled too.** Previously we only changed `fontSize`. Unity UI text using `resizeTextForBestFit` has its own `resizeTextMaxSize` that could prevent any visible increase. For eligible labels, v2.4 increases that maximum by the same factor as the font size (default **1.20×**), while leaving automatic Best Fit **enabled** to prevent disappearing/clipped descriptions.
- **Partial enlargement in truncating fields.** For Unity UI Text with `verticalOverflow=Truncate` and Best Fit disabled, we compare the enlarged and original `preferredHeight` values. If full enlargement would *introduce* truncation, the mod now tries each intermediate font size and uses the **largest size that fits**; it returns to vanilla size only if none fits.
- **Per-text decision diagnostics.** The temporary v2.4 diagnostic option `LogTextDecisions=true` logs the text object, short content preview, baseline and applied font sizes, original and applied Best Fit maximum, measured rectangle/height, and decision reason. It is **event-based**, deduplicated per object/text change, and capped at 1,500 entries per scene, not a periodic full scene scan. Set to false after diagnosis.
- **No arbitrary per-menu exceptions.** This works the same for mutators, quests, menus, dialogue, tooltips, HUD and other supported text components.

### Configuration

`BepInEx/config/mspeedo.sor.larger-text.cfg`:

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| General | `Multiplier` | `1.2` | Maximum requested zoom |
| General | `ProtectLargeText` | `true` | Protect headings, logos and clearly oversized font tier |
| General | `PreventTextClipping` | `true` | Keep genuine truncation from being newly introduced |
| Diagnostics | `LogTextDecisions` | `true` | **Temporary test setting**, turn off after collecting log |
| Diagnostics | `TextFilter` | empty | Optional substring of a text object name or its displayed text |

**No settings migration.** BepInEx retains `Multiplier` and other existing keys in your config. New diagnostic keys are generated at first load. If you have `Multiplier=1.35` from the old install, manually set it to `1.20`. To reset all plugin options, delete only that plugin's config file while the game is closed.

### Collecting diagnostics

1. Install the DLL in `<game>/BepInEx/plugins/LargerText.dll` and start the game.
2. Open a screen with a description you expected to grow and switch between a few entries. The active text is logged when its content changes; most other UI elements are logged when the scene loads.
3. Close the game. Inspect `BepInEx/LogOutput.log` and search for **`[LT-DIAG]`**. A useful record looks like:

```
[LT-DIAG] ... reason=bestfit-max-adjusted ... original=28 applied=34 bestFit=True bestFitMaxOriginal=32 bestFitMaxApplied=38 ...
```

4. If the log is too large, set `TextFilter` to a distinctive phrase appearing in the description (or an element name), restart and repeat. Send relevant `[LT-DIAG]` lines alongside a screenshot. Set `LogTextDecisions = false` after diagnosis.

The `reason` field differentiates `protected-name:...`, `protected-large-tier:...`, `bestfit`/`bestfit-max-adjusted`, `partially-fitted`, and `new-truncation`.

**Build status:** GitHub Actions compiles the DLL. The Best Fit and fitting changes need in-game testing; no claim is made that the current changes visibly enlarge every intended label.

## UnlimitedRetries v0.2.0 — unchanged

Uses the built-in **Continue?** mutator and replenishes the active continue counter for effectively unlimited retrying. Does not add manual restart without dying.

## Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

CI builds both plugins using public NuGet packages and does not publish game assemblies.
