# Level Checkpoint v1.3.2 — exhaustive function API audit

Reference: [YAL official NTT API (`GMEdit/api.gml`)](https://github.com/YAL-Game-Tools/bits-of-nuclear-throne/blob/main/GMEdit/api.gml), generated 2025-04-27 for NTT game_version **100022**. **This is an official v100 API snapshot, not an exact generated dump of your v100.034 installation.**

## All 48 external calls in the mod

| Function | Exported signature | Call site line(s) |
|---|---|---|
| `array_clone` | `array_clone(arr:array):` | 523, 550, 640, 688 |
| `array_length` | `array_length(val):` | 495, 507, 519, 533, 538, 539, 546, 573, 667, 671 |
| `array_push` | `array_push(array, ...values)` | 594, 616 |
| `chat_comp_add` | `chat_comp_add(command:string, ...args_n_help:string):` | 34, 35, 36, 37 |
| `chat_comp_remove` | `chat_comp_remove(command:string):` | 48, 49, 50, 51 |
| `event_perform` | `::event_perform(etype:int, enumb:int)` | 260 |
| `file_delete` | `file_delete(path:string):` | 94, 95, 358 |
| `file_exists` | `file_exists(path:string):` | 373 |
| `file_load` | `file_load(...paths):` | 39, 40 |
| `file_loaded` | `file_loaded(path:string):` | 52, 53, 124, 124 |
| `file_unload` | `file_unload(...paths)` | 52, 53, 141, 142 |
| `instance_create` | `instance_create(x, y, obj, ?var_struct):` | 251 |
| `instance_exists` | `instance_exists(obj:index):` | 59, 59, 109, 150, 151, 151, 166, 171, 171, 171, 175 |
| `instance_find` | `instance_find(obj:index, n:index);` | 200, 201, 409, 432, 579, 580, 650, 651 |
| `instance_number` | `instance_number(obj:index):` | 562 |
| `is_array` | `is_array(val):##` | 382, 383, 384, 396, 523, 537, 549, 586 |
| `is_bool` | `is_bool(val):##` | 499, 511 |
| `is_object` | `is_object(q)` | 375, 390, 394, 399, 401, 506, 530, 544 |
| `is_real` | `is_real(val):##` | 306, 377, 378, 379, 385, 386, 387, 442, 460, 460, 499, 511, 585 |
| `is_string` | `is_string(val_or_template, ...):##` | 442, 499, 511 |
| `json_decode` | `json_decode(json:string):` | 374 |
| `json_encode` | `json_encode(value, ?indent:string):` | 321, 346 |
| `lq_get` | `lq_get(struct, field)` | 376, 377, 378, 379, 380, 380, 382, 383, 384, 385, 386, 387, 389, 393, 395, 398, 400, 451, 451, 510, 536, 548, 658, 663, 680, 691, 692, 693 |
| `lq_set` | `lq_set(ctx, varname:string, value)` | 500, 523 |
| `player_get_race` | `player_get_race(player:int):` | 597, 634 |
| `player_get_race_id` | `player_get_race_id(player:int):` | 596 |
| `player_get_skin` | `player_get_skin(player:int):` | 643 |
| `player_is_active` | `player_is_active(player:int):` | 560, 562 |
| `player_set_race` | `player_set_race(player:int, race)` | 425 |
| `player_set_race_pick` | `player_set_race_pick(player:int, race)` | 424 |
| `player_set_skin` | `player_set_skin(player:int, skin)` | 664 |
| `skill_get` | `skill_get(skill):` | 593 |
| `skill_get_at` | `skill_get_at(index:int):` | 591 |
| `skill_set` | `skill_set(skill, value:number):` | 669 |
| `string` | `string(val_or_template, ...rest):#` | 79, 85, 264, 295, 326, 326, 350, 350, 393, 415, 415, 434, 434, 565, 565, 565, 568, 568, 568, 601, 601, 657, 699 |
| `string_copy` | `string_copy(str,index,count):#` | 701, 701 |
| `string_length` | `string_length(str):#` | 700, 701 |
| `string_load` | `string_load(path:string):` | 374 |
| `string_lower` | `string_lower(str):#` | 326, 326, 350, 350, 393, 415, 415, 434, 434, 443, 601, 601, 607, 657 |
| `string_save` | `string_save(str:string, path:string):` | 321, 346 |
| `string_upper` | `string_upper(str):#` | 701 |
| `trace` | `trace(...values):` | 43, 79, 85, 90, 92, 100, 106, 108, 110, 114, 139, 145, 157, 160, 204, 209, 214, 219, 254, 264, 272, 280, 285, 292, 295, 314, 329, 337, 352, 360, 369 |
| `ultra_count` | `ultra_count(race):` | 614 |
| `ultra_get` | `ultra_get(race, index:int)` | 615 |
| `ultra_set` | `ultra_set(race, index:int, active:bool)` | 673 |
| `variable_instance_exists` | `variable_instance_exists(ctx, varname:string):` | 212, 227, 410, 433, 497, 509, 521, 535, 549, 574, 600, 680, 694 |
| `variable_instance_get` | `variable_instance_get(inst, varname:string, ?defvalue):` | 498, 522 |
| `variable_instance_set` | `variable_instance_set(ctx, varname:string, value)` | 512, 550 |

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
