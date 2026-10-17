# Enter the Gungeon — BepInEx 5 mods

This directory groups Enter the Gungeon mods, with each mod kept in its own subdirectory.

## Mods

- [Bonus Enemy Hearts v1.0.6](BonusEnemyHearts/README.md) — configurable bonus half-heart (10%) and full-heart (2%) drops per eligible enemy kill. Includes console commands, automatic status logging and tests.

## Build

Use the [Enter the Gungeon GitHub Actions workflow](../.github/workflows/gungeon-build.yml) to compile, test, and package `BonusEnemyHearts.dll` as a downloadable artifact.

The game-level workflow responds to changes anywhere in `enter-the-gungeon/`. Additional mods can be added as sibling directories with their own build jobs under the same workflow.
