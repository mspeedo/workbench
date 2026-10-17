# Streets of Rogue 1 — BepInEx 5 plugins

## LargerText v2.5.0 — production release

**Based on v2.4.0, confirmed by the user to work well in-game.** Version 2.5 removes the temporary per-element diagnostic and log-processing code while preserving the actual scaling and fitting logic.

### Defaults and behavior

- **Multiplier = 1.20** — enlarge eligible fonts by 20%.
- **ProtectLargeText = true** — exclude titles/headings and clearly larger labels.
- **PreventTextClipping = true** — preserve text visibility by respecting Unity's Best Fit and vertical overflow rules and trying smaller integer font sizes only when needed.
- **Best Fit maximum-size support** — scales `resizeTextMaxSize` alongside eligible Unity UI text, allowing descriptions with available space to grow.
- Supports Unity UI Text, TextMesh, TMP Text, and tk2d text types when their size/scale properties are available.
- **No per-element diagnostics or text-content logging.** The temporary `LogTextDecisions` and `TextFilter` settings from v2.4 are no longer read, and any old config entries with those names are simply ignored.
- **No periodic scene scans, no overlay, no one-time settings migrations.** Retains v2.0/v2.4's one scan at startup and after scene loading, plus event hooks for UI text updates.

The production cleanup eliminates the v2.4 diagnostic work on every text event (reading and truncating descriptions, building diagnostic reason strings, deduplicating log lines, and writing up to 1,500 verbose records per scene). It also removes unused counters from the scene-load scan. Rendering behavior and the per-font fallback sequence are intentionally unchanged.

### Install / upgrade

Copy `LargerText.dll` into `<game>/BepInEx/plugins/`, replacing the old DLL. Do not keep multiple plugin versions in this directory.

Existing BepInEx settings are preserved: if you previously set `Multiplier = 1.35`, it will remain 1.35 until you change it manually. For the default 1.20× zoom, set `Multiplier = 1.2` in `BepInEx/config/mspeedo.sor.larger-text.cfg`, or delete **only that configuration file** while the game is closed to regenerate defaults.

Diagnostics were removed, but normal BepInEx error/warning logging remains available. If a future regression occurs, compare v2.5 with v2.4.

### Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
```

The GitHub Actions workflow builds both plugins against public NuGet reference assemblies and packages their DLLs without redistributing game assemblies.

## UnlimitedRetries v0.2.0 — unchanged

Uses the built-in **Continue?** mutator and replenishes the game's continue counter to enable effectively unlimited attempts at a floor. No manual restart without dying is implemented.
