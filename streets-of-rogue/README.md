# Streets of Rogue 1 — BepInEx 5 quality-of-life plugins

Two independent plugins for the original *Streets of Rogue*, intended for Windows or SteamOS under Proton.

## LargerText v1.2.0

**Fixes the v1.1.x regression where smaller fonts no longer enlarged.** In v1.1, guessed hard cutoffs for font size excluded the real UI labels; property setter / enable hooks could also miss initially rendered labels. v1.2 instead hooks **UnityEngine.UI.Text.OnPopulateMesh** so actual text geometry creation registers each label. The UI assembly can load after BepInEx startup, so hook installation also listens for assembly loading rather than assuming it is already present.

- Default **Multiplier = 1.35** (35% enlargement).
- Enlarge only text smaller than **78% of the reference text** within the same Unity Canvas, calculated using originally rendered font-size × transform scale.
- The reference is the 85th percentile of the observed Canvas text sizes. This means the largest text is above the cutoff and is not modified. Headings, buttons, titles, logos, big messages are protected by object names as a second guard.
- **No 0.5-second scene scans:** no `Update()`, `OnGUI()`, or `Resources.FindObjectsOfTypeAll()`. Text appearance or size changes set a dirty flag; a lightweight `Canvas.willRenderCanvases` listener processes **only registered text components when dirty**. No periodic full-game enumeration.
- **No configuration migrations.** BepInEx keeps older values; obsolete options are ignored.
- Diagnostics are off by default. Optional `[Diagnostics] LogResizes = true` records actual changes.
- In v1.2 the supported production text system is `UnityEngine.UI.Text`, which is present throughout this version of Streets of Rogue. No tk2d scaling is enabled.

### Install

Copy `LargerText.dll` to `<game>/BepInEx/plugins/`, replacing the previous DLL. **Before starting, remove `<game>/BepInEx/config/mspeedo.sor.larger-text.cfg` to load the new settings without any migration.** If keeping the old config, manually ensure `[General] Multiplier = 1.35` and `Enabled = true`; v1.1's old absolute text-size cutoff settings will be ignored.

Look for `Larger Text v1.2.0 active` in `BepInEx/LogOutput.log`.

**Status:** Successfully compiling is not the same as passing an in-game test. Please check small tooltips and regular-sized menu headings separately. If labels still do not change, send the plugin's startup line and a short log sample.

## UnlimitedRetries v0.2.0 (unchanged)

Uses the game's built-in **Continue?** mutator to replenish `SessionData.continues` so a run can retry the current floor effectively indefinitely. Requires Continue? to be active. No manual restart-floor option is implemented.

## Build

```bash
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

GitHub Actions produces DLLs in a ZIP using public reference packages and does not publish game DLLs.
