# Boss Half Health v1.0.0-beta

A small standalone global `.mod.gml` mod for **Nuclear Throne Together (NTT)**, targeting the `ntt_development` v100.034 build. **Not compatible with the standalone/official Nuclear Throne executable.**

## What it does

Once per boss instance, reduces both current HP and maximum HP to **50%** of their current values (rounded up, minimum 1 HP). Other enemy types and player health are not changed. Works again for newly spawned boss instances, including loop encounters, without repeatedly halving existing instances every frame.

Includes Big Bandit, Big Dog (awake), Lil' Hunter, Nuclear Throne, Throne II, Mom, Hyper Crystal, Technomancer, I.D.P.D. Captain, Oasis Boss, and Crown Guardian. Other regular/elite enemies and nonboss minibosses (e.g., the Big Green Rat) are intentionally unaffected. Bosses created by third-party mods are not automatically affected.

Bosses which regenerate or internally overwrite their HP after creation may require additional handling. This has not yet been verified against a running game, so the release is marked beta.

## Installation

1. Copy `bosshalfhealth.mod.gml` to your NTT game's `mods` folder, beside `levelcheckpoint.mod.gml` if installed.
2. Load in the NTT chat: `/loadmod bosshalfhealth`.
3. To autoload, put both lines in `mods/startup.txt` (one per line):

   ```text
   /loadmod levelcheckpoint
   /loadmod bosshalfhealth
   ```

4. Restart NTT after modifying `startup.txt`.

You can edit `#macro BHH_HEALTH_FACTOR 0.5` in the mod file to experiment with another multiplier. `0.5` is the default. Avoid using values at or below zero.

## Compatibility and implementation

- Independent of Level Checkpoint and does not access or alter its save files.
- Hooks the normal global `step` callback documented by NTT and targets built-in boss object types.
- Only changes `my_health`, `maxhealth`, and adds an instance-specific `bhh_health_adjusted` marker.
- No commands, runtime logs, or settings files.
- Loop scaling already applied by NTT is respected because the 50% reduction uses the boss's own initialized HP.
- If a boss appears for the first time during a frame, scaling occurs on the next `step` callback.
- The source is a text mod; compilation is not required.

## Validation status

Basic static source checks pass. Runtime behavior in NTT v100.034 **has not yet been tested**.
