# Nuclear Throne Together — Level Checkpoint v1.3.4

**Production release**, promoted from v1.3.3 Beta after successful in-game restoration at Palace 7-1. No gameplay-code changes from v1.3.3 Beta. The previous working v1.3.2 production release is the foundation. The character-initialization fix, checkpoint format, autosave behavior and native Retry-only policy are unchanged. The early refusal to warp into Palace and secret stages without `can_advance_stage` has been removed.

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

Target: Steam `ntt_development`, Nuclear Throne Together v100.034. Single-player built-in characters (including the v100 Cuz character) and stages covered by the current mod. Custom modded characters and multiplayer are unsupported. Palace (7-x) and secret stages (100-x–107-x) now attempt the standard native portal warp when `can_advance_stage` is absent. The mod checks the actual destination before restoring player state; the Palace 7-1 path was **verified in-game**; other Palace and secret-area warps are **not yet verified**.

The build keeps short checkpoint notifications, command responses, and actionable error messages. Experimental Random-resume logic, `/cpstatus`, `/cpresume`, and verbose diagnostics are not included.

## Verification

The user confirmed that **v1.3.1 Beta restores the saved character correctly** using Retry, and **v1.3.3 Beta restores Palace 7-1**. This release retains that tested v1.3.3 gameplay logic exactly. Five source/API/field/character/policy regression suites were run against this package. The public API snapshot used by the audit is NTT v100.022 rather than an exact dump from v100.034. The production ZIP has not been independently played inside the game.

## Unusual-area Retry behavior (v1.3.3–v1.3.4)

The previous version refused Palace and secret checkpoints on NTT v100.034 because `GameCont.can_advance_stage` was not exposed. This production version retains the existing native portal fallback (`subarea - 1`, `hard - 1`) for those stages too. Portal handling varies by area. **Palace 7-1 is confirmed working**; other unusual stages remain untested. The saved checkpoint files are never deleted or overwritten on a failed resume, but a failed portal can leave the live run at a different stage.

When resuming checkpoints at 7-2, 7-3 or secret stages, verify both the restored stage and character. Report the saved stage label and exact console output if it fails. Ordinary stages and character initialization are unchanged.
