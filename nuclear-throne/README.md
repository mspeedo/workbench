# Nuclear Throne

Standalone mods for **Nuclear Throne Together**, Steam `ntt_development` v100.034.

## Mods

- [Level Checkpoint v1.3.4 — Production](LevelCheckpoint/README.md) — Autosaves each supported level and restores the saved level, character and equipment using native Retry. Palace 7-1 restoration was confirmed in-game. [Source](LevelCheckpoint/levelcheckpoint.mod.gml).
- [Boss Half Health v1.0.0 — Beta](BossHalfHealth/README.md) — Reduces current and maximum HP of 11 built-in bosses by 50% once per instance. Normal enemies are unaffected. Static tests pass, but gameplay has **not yet been verified**. [Source](BossHalfHealth/bosshalfhealth.mod.gml).

## Install

Place the two `.mod.gml` files in NTT's `mods` folder and add these lines to `mods/startup.txt`:

```text
/loadmod levelcheckpoint
/loadmod bosshalfhealth
```

Restart NTT. The mods are independent and can also be loaded separately. Neither works with the unmodified official Nuclear Throne executable. No compilation or GitHub Actions workflow is needed. See each mod's README for details and caveats.
