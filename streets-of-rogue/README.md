# Streets of Rogue 1 — BepInEx 5 quality-of-life mods

## LargerText v2.2.0 — prevents missing/clipped labels

v2.1.0 introduced a visibility regression: even when a label originally used Unity UI **Best Fit**, the plugin disabled it for enlarged text. Dialogue, mutator selection, and menus have fixed-size rectangles; Unity can truncate text that no longer fits. v2.2.0 preserves Best Fit instead of overriding it, and checks Unity UI text's preferred height against its RectTransform height. If enlargement would exceed the rectangle, the label is returned to its original font size.

The fit check runs whenever the text changes as well, not only when the font-size property changes. This protects longer replacement strings in an existing dialogue label.

### Behavior

- Default **Multiplier = 1.20**, enlarging eligible text by 20%.
- **ProtectLargeText = true**: protects names containing title/header/heading/logo/banner etc., plus the clearly larger size tier calculated once per scene from original sizes.
- **PreventTextClipping = true**: preserves `resizeTextForBestFit` and reverts text to its original size if a larger label's `preferredHeight` exceeds its available `RectTransform` height.
- **No periodic polling:** retains the v2.0 scene-load scan and UI text event hooks. No `Update()` and no overlay.
- **No settings migration** or automatic multiplier rewriting. Existing BepInEx configuration values take precedence.
- If a text label cannot safely fit enlarged, it deliberately stays its original size rather than disappearing.

*This is an in-game unverified build. Unity's layout methods and clipping rules vary with the game's UI; please test dialogues, mutators, main menu headings, and long descriptions. Some screens may still need more targeted adjustments.*

### Install

With the game closed, replace `<game>/BepInEx/plugins/LargerText.dll`. To ensure `Multiplier = 1.2`, either edit that value in `<game>/BepInEx/config/mspeedo.sor.larger-text.cfg` or delete this config file (with the game closed) to use the latest defaults. Do not install two versions of `LargerText.dll` simultaneously.

The `BepInEx/LogOutput.log` should show `Larger Text v2.2 loaded`. For isolation, `ProtectLargeText = false` disables the large-text filter while keeping the clipping safeguard enabled.

If any text remains absent, try restoring the original v2.0 plugin or removing LargerText.dll to restore the game's UI, then provide a screenshot and `BepInEx/LogOutput.log`.

## UnlimitedRetries v0.2.0 — unchanged

Uses the game's built-in **Continue?** mutator and replenishes the continues counter for effectively unlimited floor retries. Requires the mutator; manual restart without death is not implemented.

## Build

```sh
dotnet build streets-of-rogue/LargerText/LargerText.csproj -c Release
dotnet build streets-of-rogue/UnlimitedRetries/UnlimitedRetries.csproj -c Release
```

GitHub Actions packages both DLLs using public reference assemblies; copyrighted game DLLs are not committed.
