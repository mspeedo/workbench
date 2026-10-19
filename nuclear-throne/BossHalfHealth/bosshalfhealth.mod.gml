// Boss Half Health v1.0.0-beta for Nuclear Throne Together (NTT v100)
// Standalone global mod; safe to use beside Level Checkpoint.
// Reduces current and maximum health to 50% once per boss instance.
// Bosses are selected by exact object type; ordinary enemies are untouched.

#macro BHH_HEALTH_FACTOR 0.5

#define bhh_apply_once
// Called from inside a boss instance (with ...).
// An instance marker prevents the 50% multiplier being applied repeatedly.
if (variable_instance_exists(id, "bhh_health_adjusted")) return;
if (!variable_instance_exists(id, "maxhealth")) return;
if (!variable_instance_exists(id, "my_health")) return;

var _old_max = maxhealth;
var _old_hp = my_health;
// Some bosses create before their health is initialized. Try next frame.
if (_old_max <= 0 || _old_hp <= 0) return;

var _new_max = max(1, ceil(_old_max * BHH_HEALTH_FACTOR));
var _new_hp = max(1, min(_new_max, ceil(_old_hp * BHH_HEALTH_FACTOR)));
maxhealth = _new_max;
my_health = _new_hp;
bhh_health_adjusted = true;

#define step
// Core bosses (including secret/loop bosses). Each 'with' visits only
// instances of that boss type, rather than scanning every ordinary enemy.
with (BanditBoss)   bhh_apply_once(); // Big Bandit
with (ScrapBoss)    bhh_apply_once(); // Big Dog (awake)
with (LilHunter)    bhh_apply_once(); // Lil' Hunter
with (Nothing)      bhh_apply_once(); // Nuclear Throne
with (Nothing2)     bhh_apply_once(); // Throne II
with (FrogQueen)    bhh_apply_once(); // Mom
with (HyperCrystal) bhh_apply_once(); // Hyper Crystal
with (TechnoMancer) bhh_apply_once(); // Technomancer
with (Last)         bhh_apply_once(); // I.D.P.D. Captain
// Special/secret boss encounters
with (OasisBoss)    bhh_apply_once(); // Oasis boss
with (CrownGuardian) bhh_apply_once(); // Crown Guardian
