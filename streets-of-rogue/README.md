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
