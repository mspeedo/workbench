# Streets of Rogue 1 — BepInEx 5 mods

Two independent plugins for the original **Streets of Rogue** using BepInEx 5 (Windows / SteamOS via Proton).

## LargerText v1.0.0 — production defaults

Based on the **v0.4 text-scaling implementation confirmed to work in-game**. No changes were made to the font detection/scaling algorithm in v1.0, only defaults, logging and config migration.

- **Multiplier = 1.50** (maximum 150% of original size)
- **SmallTextFraction = 0.82** (enlarge only text smaller than 82% of the scene reference font size)
- **ReferencePercentile = 0.85** (normal-size text reference)
- **ShowOverlay = false** by default (optional diagnostic overlay can still be enabled)
- **LogSamples = false** by default (optional diagnostic logging can still be enabled)
- **ForceAllText = false** (diagnostic force-all mode disabled)
- **ScanSeconds = 0.5**

Works with `UnityEngine.UI.Text`, `UnityEngine.TextMesh`, `TMPro.TMP_Text`, and `tk2dTextMesh`. Scans and scales only active, enabled scene components. For Unity text, uses fontSize and optional Best Fit override; for tk2d, uses component-local mesh scale. Existing large menu text stays unchanged when `ForceAllText = false`.

**Existing configuration migration:** BepInEx preserves configured values across DLL updates. The plugin's first v1.0 startup updates old v0.4 default values: `Multiplier = 1.70` to `1.50`, `ShowOverlay = true` to `false`, `LogSamples = true` to `false`, and disables `ForceAllText`. Other user choices remain unchanged (for example a prior custom multiplier of 1.35 will stay 1.35). Migration is saved once via `SettingsVersion`. You can always edit `BepInEx/config/mspeedo.sor.larger-text.cfg` if you prefer another zoom level.

## UnlimitedRetries v0.2.0 — unchanged

Works with the built-in **Continue?** mutator. Replenishes `SessionData.continues` to 10,000 so the game's original continue/restore workflow can repeat effectively indefinitely. Requires the mutator to be selected. A manual restart-floor button is **not** implemented.

## Install

1. Install BepInEx **5 Windows x64** in the game's directory, alongside `StreetsOfRogue.exe`.
2. Under Steam/Proton launch options, set `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
3. Copy `LargerText.dll` and/or `UnlimitedRetries.dll` to `Streets of Rogue/BepInEx/plugins/`.
4. Launch the game. Settings are saved in `BepInEx/config/mspeedo.sor.larger-text.cfg` and `BepInEx/config/mspeedo.sor.unlimited-retries.cfg`.
5. To use unlimited retries, enable **Continue?** in mutators before starting a run.

**Testing and caveats:** v0.4 font scaling was confirmed functional in-game. v1.0 has new defaults but still requires in-game validation. Some enlarged text may clip because fixed game UI layout regions do not grow. Back up game saves before using experimental retry mechanics.

## Build

```bash
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
```

The GitHub Actions workflow builds against public NuGet reference packages and uploads an installable ZIP. **No proprietary game DLLs are committed to this repository.**
