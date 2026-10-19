# Level Checkpoint v1.3.4 Production — exhaustive function API audit

Reference: [YAL official NTT API (`GMEdit/api.gml`)](https://github.com/YAL-Game-Tools/bits-of-nuclear-throne/blob/main/GMEdit/api.gml), generated 2025-04-27 for NTT game_version **100022**. **This is an official v100 API snapshot, not an exact generated dump of your v100.034 installation.**

## All 48 external calls in the mod

| Function | Exported signature | Call site line(s) |
|---|---|---|
| `array_clone` | `array_clone(arr:array):` | 519, 546, 636, 684 |
| `array_length` | `array_length(val):` | 491, 503, 515, 529, 534, 535, 542, 569, 663, 667 |
| `array_push` | `array_push(array, ...values)` | 590, 612 |
| `chat_comp_add` | `chat_comp_add(command:string, ...args_n_help:string):` | 34, 35, 36, 37 |
| `chat_comp_remove` | `chat_comp_remove(command:string):` | 48, 49, 50, 51 |
| `event_perform` | `::event_perform(etype:int, enumb:int)` | 259 |
| `file_delete` | `file_delete(path:string):` | 94, 95, 357 |
| `file_exists` | `file_exists(path:string):` | 372 |
| `file_load` | `file_load(...paths):` | 39, 40 |
| `file_loaded` | `file_loaded(path:string):` | 52, 53, 124, 124 |
| `file_unload` | `file_unload(...paths)` | 52, 53, 141, 142 |
| `instance_create` | `instance_create(x, y, obj, ?var_struct):` | 250 |
| `instance_exists` | `instance_exists(obj:index):` | 59, 59, 109, 150, 151, 151, 166, 171, 171, 171, 175 |
| `instance_find` | `instance_find(obj:index, n:index);` | 200, 201, 408, 431, 575, 576, 646, 647 |
| `instance_number` | `instance_number(obj:index):` | 558 |
| `is_array` | `is_array(val):##` | 381, 382, 383, 395, 519, 533, 545, 582 |
| `is_bool` | `is_bool(val):##` | 495, 507 |
| `is_object` | `is_object(q)` | 374, 389, 393, 398, 400, 502, 526, 540 |
| `is_real` | `is_real(val):##` | 305, 376, 377, 378, 384, 385, 386, 441, 459, 459, 495, 507, 581 |
| `is_string` | `is_string(val_or_template, ...):##` | 441, 495, 507 |
| `json_decode` | `json_decode(json:string):` | 373 |
| `json_encode` | `json_encode(value, ?indent:string):` | 320, 345 |
| `lq_get` | `lq_get(struct, field)` | 375, 376, 377, 378, 379, 379, 381, 382, 383, 384, 385, 386, 388, 392, 394, 397, 399, 450, 450, 506, 532, 544, 654, 659, 676, 687, 688, 689 |
| `lq_set` | `lq_set(ctx, varname:string, value)` | 496, 519 |
| `player_get_race` | `player_get_race(player:int):` | 593, 630 |
| `player_get_race_id` | `player_get_race_id(player:int):` | 592 |
| `player_get_skin` | `player_get_skin(player:int):` | 639 |
| `player_is_active` | `player_is_active(player:int):` | 556, 558 |
| `player_set_race` | `player_set_race(player:int, race)` | 424 |
| `player_set_race_pick` | `player_set_race_pick(player:int, race)` | 423 |
| `player_set_skin` | `player_set_skin(player:int, skin)` | 660 |
| `skill_get` | `skill_get(skill):` | 589 |
| `skill_get_at` | `skill_get_at(index:int):` | 587 |
| `skill_set` | `skill_set(skill, value:number):` | 665 |
| `string` | `string(val_or_template, ...rest):#` | 79, 85, 263, 294, 325, 325, 349, 349, 392, 414, 414, 433, 433, 561, 561, 561, 564, 564, 564, 597, 597, 653, 695 |
| `string_copy` | `string_copy(str,index,count):#` | 697, 697 |
| `string_length` | `string_length(str):#` | 696, 697 |
| `string_load` | `string_load(path:string):` | 373 |
| `string_lower` | `string_lower(str):#` | 325, 325, 349, 349, 392, 414, 414, 433, 433, 442, 597, 597, 603, 653 |
| `string_save` | `string_save(str:string, path:string):` | 320, 345 |
| `string_upper` | `string_upper(str):#` | 697 |
| `trace` | `trace(...values):` | 43, 79, 85, 90, 92, 100, 106, 108, 110, 114, 139, 145, 157, 160, 204, 209, 218, 253, 263, 271, 279, 284, 291, 294, 313, 328, 336, 351, 359, 368 |
| `ultra_count` | `ultra_count(race):` | 610 |
| `ultra_get` | `ultra_get(race, index:int)` | 611 |
| `ultra_set` | `ultra_set(race, index:int, active:bool)` | 669 |
| `variable_instance_exists` | `variable_instance_exists(ctx, varname:string):` | 226, 409, 432, 493, 505, 517, 531, 545, 570, 596, 676, 690 |
| `variable_instance_get` | `variable_instance_get(inst, varname:string, ?defvalue):` | 494, 518 |
| `variable_instance_set` | `variable_instance_set(ctx, varname:string, value)` | 508, 546 |

## Exported constants

- `maxp`: `maxp = 4`
- `char_fish`: `char_fish = 1`
- `char_rogue`: `char_rogue = 12`
- `ev_other`: `ev_other = 7`
- `ev_animation_end`: `ev_animation_end = 7`
- `game_version`: `game_version = 100022`
- `json_error`: `json_error*`

## Engine symbols

`GameCont`, `GenCont`, `LevCont`, `Menu`, `CharSelect`, `Player`, `Portal`, `sprPortalDisappear` — confirmed in official `GMEdit/raw-assets.gml`. `noone`, `undefined`, and language control keywords are standard GameMaker/NTGML expressions rather than exported functions.

## Fixed issues

- `dp_player_count_active()` — **not exported**; replaced by `player_is_active(slot)` across `maxp`.
- `char_custom` — **not exported**; replaced by built-in character range and whitelisted race names.
- `room_restart()` — **not exported** and was removed previously; transition still uses exposed `instance_create()` and `event_perform()` calls.

## Important scope

This verifies **names and API availability**, not the correctness of checkpoint serialization, room transition, or in-game behavior. NTT v100.034 is described by the author as an incompletely tested build; runtime testing remains required. For an exact version-matched function audit, run `/gmlapi` on Legion Go S and compare its generated `api.gml`.
