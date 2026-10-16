# Streets of Rogue 1: BepInEx 5 mods

## LargerText v2.0.0 — simple 1.35× for **all** text

This is a **fresh rewrite** after v1.1–v1.2's selective font logic failed on the user's installed game.

- Increases the original font size of **every supported text component** by **35%**, no thresholds, percentiles, title detection, or menu exclusions.
- Supports `UnityEngine.UI.Text`, `UnityEngine.TextMesh`, `TMPro.TMP_Text` and `tk2dTextMesh` where their writable font-size/scale properties are available.
- Scans game UI objects **once at startup and once after each scene load** (including inactive menu elements). No `Update()`, no recurring 0.5-second scene scan and no overlay.
- Also hooks `UnityEngine.UI.Text` font/text setter updates and UI activation to cover many text elements created after the initial scan. The expensive complete search never runs on a 0.5-second gameplay timer.
- Disables UI Text `resizeTextForBestFit` on resized text so Unity doesn't immediately shrink it back to its original visual size.
- Tracks each component's original size to prevent exponential growth on repeat updates.
- No settings migration. BepInEx persists prior config values; if this build should use default 1.35, set `[General] Multiplier = 1.35` or delete the config with the game closed.
- Only startup and scene scan counts are written to the log, no per-frame diagnostic output.

**Limitation:** Making literally all text larger can cause overlap or clipping, especially already-large menus and HUD labels. This is requested and intentional. The initial scene-load search can cost some time during load; it no longer runs periodically in active gameplay. The new build is not yet in-game validated.

### Installation

1. Install BepInEx 5 Windows x64, as in previous versions; SteamOS/Proton launch options: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
2. Replace `<game>/BepInEx/plugins/LargerText.dll` with the DLL from the archive.
3. With the game closed, delete `<game>/BepInEx/config/mspeedo.sor.larger-text.cfg` to get default `Multiplier = 1.35`; this is **not** a migration.
4. Start game and look for `Larger Text v2.0 loaded` and `Larger Text scene scan` in `BepInEx/LogOutput.log`.

## UnlimitedRetries v0.2.0 (unchanged)

Uses the built-in **Continue?** mutator to replenish the continues counter and allow nearly unlimited retrying of the same floor. It does not provide a manual restart without dying.

## Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

The GitHub Actions workflow builds both DLLs against public NuGet packages and packages `BepInEx/plugins/`. The repository contains no proprietary game DLLs.
