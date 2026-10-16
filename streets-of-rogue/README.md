# Streets of Rogue 1: BepInEx 5 mods

## LargerText v2.1.0 — 1.20× with large-text protection

This release is a **small extension of the v2.0 implementation confirmed working in-game**. It deliberately retains the same startup / scene-load scan and UI text change hooks, not the nonworking v1.1/v1.2 approach.

### Font scaling

- Default **Multiplier = 1.20** (20% larger).
- Default **ProtectLargeText = true**.
- Protection first excludes obvious title/heading/logo/banner object names and nearby parent objects.
- On a scene-load scan, it also compares **original visual sizes** (original font size × transform scale) **separately per renderer type**. When there's a clear size gap (max at least 1.35× the median), it protects the largest tier of text, using a cutoff halfway between median and max. Other text is resized by the configured multiplier.
- Scenes with nearly uniform text sizes have no automatic size tier. This prevents accidentally protecting everything on screens that use a single font size.
- **No periodic scene scans**: the size tier is computed during the existing one-time scan on load, and later UI text changes use cached thresholds. There is no `Update()` or diagnostic overlay.
- Existing font size bookkeeping is preserved so sizes are not multiplied repeatedly; for protected UI text, Unity's original Best Fit setting is restored if it was previously changed.
- No config migrations or automatic rewriting of `Multiplier` values.

The large-font protection is heuristic: Unity UI object names and scales depend on the game screen. It is designed to keep the biggest menu headings unchanged while enlarging descriptions and smaller labels, but it still needs in-game checking.

### Installation

1. Install BepInEx 5 (Windows x64) for the Windows game. Under Proton launch options use `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
2. Replace `<game>/BepInEx/plugins/LargerText.dll` with the DLL from the ZIP.
3. **Important:** BepInEx keeps your previous multiplier. To use the new default, while the game is closed set `Multiplier = 1.2` in `BepInEx/config/mspeedo.sor.larger-text.cfg`, or delete only that config file for fresh defaults.
4. Launch the game; confirm startup in `BepInEx/LogOutput.log`.

You can turn off the new protection with `ProtectLargeText = false` to return to v2.0-style all-font resizing (at the chosen multiplier).

## UnlimitedRetries v0.2.0 — unchanged

Works with the game's **Continue?** mutator. Replenishes the continues counter for effectively unlimited retries of the current floor. No manual floor reset is implemented.

## Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

GitHub Actions produces DLLs for BepInEx 5 without publishing proprietary game assemblies.
