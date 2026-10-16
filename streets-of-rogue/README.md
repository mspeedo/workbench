# Streets of Rogue 1 – BepInEx 5 quality-of-life mods

Two **independent** plugins for the original *Streets of Rogue*, designed for Windows or SteamOS via Proton.

* **UnlimitedRetries**: works with the game's built-in **Continue?** mutator. Before `SaveGame.Save`, refreshes `GameController.sessionData.continues` to 10,000 so that the game's existing checkpoint / floor restore system can still handle death and reloading. Does not enable Continue? when the mutator is absent. Does not override **End Run**. The 10,000 per-floor budget is effectively unlimited; using an intentionally finite number avoids overflow.
* **LargerText**: enlarges small Unity UI, TMPro and tk2d text (the game contains `tk2dTextMesh`), default 1.35x. Uses the `fontSize` property rather than changing transforms, so sprites and other UI content should not be scaled unintentionally.

## Install
1. Install BepInEx **5 Windows x64** in your game's directory, alongside `StreetsOfRogue.exe`.
2. Under Steam/Proton launch options: `WINEDLLOVERRIDES="winhttp=n,b" %command%`
3. Copy `UnlimitedRetries.dll` and `LargerText.dll` into `Streets of Rogue/BepInEx/plugins/`.
4. Start the game and inspect `BepInEx/LogOutput.log`. Settings appear under `BepInEx/config/mspeedo.sor.*.cfg`.
5. In the game's mutator selection, enable **Continue?** before starting the run. It begins working only after completing floor 1, just as in vanilla.

### Testing caveats
These are **beta builds**. Game assembly metadata was inspected from the user's October 2026 Streets of Rogue binaries, but in-game operation must still be tested. Initially back up saves, and test each plugin separately. The font plugin may cause text clipping because original UI layouts were not designed for larger labels; reduce its multiplier or tighten `MaxOriginalFontSize` as needed. The retry plugin intentionally does not alter other game rules or remove death/restore UI. The **manual restart floor without dying** feature is not implemented.

## Build
```bash
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
```
Uses public NuGet reference packages; **no proprietary game assemblies are committed** to this repository. The game assembly metadata is only used for understanding internal names and behavior. GitHub Actions publishes two ready-to-install DLLs in a ZIP, not Unity or game DLL dependencies.


## Selective small-font mode (LargerText v0.3)

**The normal menu font stays unchanged.** The mod now works in two paths:

- Unity UI, TextMesh, TMP: scales only text with an original font size strictly below `MaxOriginalFontSize` (default **18**). It also caps the enlarged text at 18, so small text cannot become larger than the menu cutoff.
- 2D Toolkit `tk2dTextMesh` (used by Streets of Rogue): scales the actual text mesh, **not** its parent UI transform. A sampled large-text reference is computed every scan; only text below `Tk2dSmallTextFraction` (default **0.80**) of that reference is enlarged. The enlarged result is capped at the reference size. Text at or near the reference stays unchanged.

The original 1.35× `Multiplier` remains configurable. To make fewer elements eligible, lower `MaxOriginalFontSize` to 16, or lower `Tk2dSmallTextFraction` to 0.7. Because the game uses multiple text renderers and sizes, this is a **heuristic**; compare in-game menus and item descriptions and tune as needed. Enable `LogFontSizes` temporarily for tk2d measurements in `BepInEx/LogOutput.log`.

**Config upgrade note:** BepInEx preserves existing settings. If you previously ran v0.2 with `MaxOriginalFontSize = 22`, manually change it to `18` in `BepInEx/config/mspeedo.sor.larger-text.cfg`. Restart the game.

Install only the updated `LargerText.dll` to upgrade text scaling; `UnlimitedRetries.dll` is unchanged.
