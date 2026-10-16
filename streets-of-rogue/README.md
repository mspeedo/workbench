# Streets of Rogue — BepInEx 5 mods

Two independent plugins for the original *Streets of Rogue* (Windows, or SteamOS running the Windows game under Proton).

## LargerText v1.1.1 — event-driven small fonts

This replaces the v1.0 adaptive percentile model. V1.0 could enlarge even the largest heading when a still larger font appeared elsewhere. It also scanned every active text component every 0.5 seconds and caused stutters on the handheld.

**v1.1 changes:**

- **No recurring scene scan.** No `Update()`, no `Resources.FindObjectsOfTypeAll`, no overlay, no periodic diagnostic logging. Harmony subscribes to Unity UI text enabling and changes to its `fontSize` setter. Only an affected text element is processed when it appears or changes font size.
- **Stable absolute size safeguards:** only font sizes below BOTH `MaxOriginalFontSize` (default 40) AND `MaxVisualFontSize` (default 25 approximate screen pixels) are eligible. Titles, headings, buttons, logos and big text objects are explicitly excluded by name. Enlarged text is capped at the visual size bound.
- **Zoom default 1.35x** (`Multiplier=1.35`). Dynamic reference percentiles are removed.
- BepInEx's Unity UI Best Fit sizing is disabled only for enlarged small text, and restored when not enlarged.
- Legacy `tk2dTextMesh` can be enabled for experiments, but is **off by default** because tk2d's coordinate scaling differs from Unity UI.
- **No one-time settings migration.** Code does not read or write `SettingsVersion`, and never rewrites your old configuration values during startup.

### Installation

Copy `LargerText.dll` to `<game>/BepInEx/plugins/`, replacing the previous DLL.

**Important when upgrading from v0.4 or v1.0:** BepInEx persists old settings. Close the game, delete only `<game>/BepInEx/config/mspeedo.sor.larger-text.cfg` and start it again **if you want the new defaults**. Alternately, retain the file and edit `[General] Multiplier = 1.35`. Obsolete values such as `SmallTextFraction`, `ShowOverlay`, `ForceAllText`, and `SettingsVersion` are ignored, not migrated.

If small text remains unchanged, try raising `MaxVisualFontSize` from 25 to 30 in the config; avoid raising it excessively because that could affect headings. The filter also blocks font sizes originally 40 or larger; names containing `title`, `header`, `heading`, `logo`, `bigtext`, `largetext`, `bigmessage`, or `button` in the first three ancestor objects are protected. Legacy tk2d support is optional and has no polling.

### Diagnostics

Check `BepInEx/LogOutput.log` for a single `Larger Text 1.1 loaded:` line showing patch count. Set `LogEligibleText=true` to log resized text at property change / activation events. The diagnostic overlay from v0.4 is intentionally removed.

This version is compiled but needs in-game validation for the exact game's UI sizes and lifecycle hooks.

## UnlimitedRetries v0.2 — unchanged

Enables effectively unlimited retries through the game's built-in **Continue?** mutator by replenishing its active continues counter. Requires the mutator, does not add a manual restart option.

## Build

```bash
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

GitHub Actions compiles against public reference packages and creates installable plugin DLLs, without committing proprietary game DLLs.
