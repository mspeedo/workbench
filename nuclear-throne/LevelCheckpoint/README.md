# Nuclear Throne Together — Level Checkpoint v1.3.2

**Production release**, promoted directly from the user-tested v1.3.1 Beta. The NTT character-initialization fix has **not** been altered. The only mod-source change is its version/comment header.

## Game behavior

- At character selection, prints the saved character and checkpoint stage, e.g. `[LCP] Checkpoint: Plant 3-2 L0`.
- Choosing **any character**, including Random or a character that matches the checkpoint, starts a **fresh run** without loading that checkpoint.
- Choosing in-game **Retry while alive or after death** automatically resumes the **latest saved checkpoint**, restoring the saved character (including its actor setup), saved level, health, weapons, ammunition, mutations, and captured progress.
- The first checkpoint is saved **at 1-2**, after the first portal. No checkpoint is created at the initial 1-1. Subsequent supported levels are saved automatically.
- The first successful save for any **new run** replaces the previous run's checkpoint in both backup slots regardless of character. If a new run ends before reaching 1-2, its predecessor's checkpoint remains available via Retry.
- Saves use two alternating files, `levelcheckpoint-a.json` and `levelcheckpoint-b.json`, in the mod's private data folder. The existing **schema 1** file format is preserved; no save migration or deletion is required.

## Installation / upgrade

1. Optionally make a copy of the two `levelcheckpoint-*.json` checkpoint files as a backup.
2. Replace `levelcheckpoint.mod.gml` in the game's `mods` directory with the file from this ZIP.
3. Keep the existing `/loadmod levelcheckpoint` line in `mods/startup.txt`.
4. Fully restart NTT to load the new source.

## Commands

- `/cpauto on` or `/cpauto off` — enable/disable automatic saving (default on).
- `/cpautoload on` or `/cpautoload off` — enable/disable resume after Retry (default on).
- `/cpsave` — manually save the current stable supported level (cannot save initial 1-1).
- `/cpclear confirm` — permanently erase both checkpoint files.

## Supported scope

Target: Steam `ntt_development`, Nuclear Throne Together v100.034. Single-player built-in characters (including the v100 Cuz character) and stages covered by the current mod. Custom modded characters and multiplayer are unsupported. Unusual/secret stages have additional version-dependent warp requirements.

The build keeps short checkpoint notifications, command responses, and actionable error messages. Experimental Random-resume logic, `/cpstatus`, `/cpresume`, and verbose diagnostics are not included.

## Verification

The user confirmed that **v1.3.1 Beta restores the saved character correctly** using Retry; this production release uses identical executable mod code. Five source/API/field/character/policy regression suites were run against this package. The public API snapshot used by the audit is NTT v100.022 rather than an exact dump from v100.034. The production ZIP has not been independently played inside the game.
