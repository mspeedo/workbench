// Level Checkpoint for Nuclear Throne Together v100 — v1.3.4 (production; Palace 7-1 Retry verified)
// Standalone, legacy NTGML global mod. No Session dependency.
// Single-player built-in characters (including Cuz, Big Dog, Skeleton, Frog).
// External custom race mods and co-op still unsupported.
// Stores TWO rotating checkpoints in this mod's private data directory.
// Load with /loadmod levelcheckpoint before starting a run.
// Single latest-run checkpoint: automatically resume ONLY after native Retry.

#define init
    global.lcp_path_a = "levelcheckpoint-a.json";
    global.lcp_path_b = "levelcheckpoint-b.json";
    global.lcp_ready = false;
    global.lcp_error = false;
    global.lcp_read_frames = 0;
    global.lcp_auto_save = true;
    global.lcp_auto_resume = true;
    global.lcp_saved = undefined;
    global.lcp_sequence = 0;
    global.lcp_phase = "idle";
    global.lcp_stable_frames = 0;
    global.lcp_last_stage = "";
    global.lcp_manual_save = false;
    global.lcp_restore = undefined;
    global.lcp_menu_seen_since_start = false;
    global.lcp_menu_visible = false;
    global.lcp_had_started = false;
    global.lcp_resume_requested = false;
    // A fresh run's first completed level overwrites BOTH old backup copies.
    // No race equality condition: these files track the latest run, not a race.
    global.lcp_fresh_pending = false;
    // Per-stage failure latch: prevents one bad field / disk write from spamming
    // 30 times per second, but allows next level and /cpsave to retry.
    global.lcp_failed_stage = "";
    chat_comp_add("cpsave", "", "Save current level start state");
    chat_comp_add("cpauto", "(on|off)", "Toggle autosave");
    chat_comp_add("cpautoload", "(on|off)", "Toggle automatic resume");
    chat_comp_add("cpclear", "confirm", "Erase both checkpoint files");
    try {
        file_load(global.lcp_path_a);
        file_load(global.lcp_path_b);
    } catch (_error) {
        global.lcp_error = true;
        trace("[LCP] Could not read checkpoint files. Autosave disabled.");
    }


#define cleanup
    chat_comp_remove("cpsave");
    chat_comp_remove("cpauto");
    chat_comp_remove("cpautoload");
    chat_comp_remove("cpclear");
    if (file_loaded(global.lcp_path_a)) file_unload(global.lcp_path_a);
    if (file_loaded(global.lcp_path_b)) file_unload(global.lcp_path_b);

#define game_start
    // A run started from character selection is always fresh. Only a native
    // run restart WITHOUT revisiting character selection counts as Retry.
    var _from_menu = global.lcp_menu_seen_since_start
        || instance_exists(CharSelect) || instance_exists(Menu);
    var _retry = global.lcp_had_started && !_from_menu;
    global.lcp_resume_requested = _retry && global.lcp_auto_resume
        && global.lcp_ready && global.lcp_saved != undefined;
    global.lcp_had_started = true;
    global.lcp_menu_seen_since_start = false;
    global.lcp_menu_visible = false;
    // First checkpoint in a fresh run replaces BOTH rotating backup slots.
    global.lcp_fresh_pending = !global.lcp_resume_requested;
    global.lcp_phase = "await-start";
    global.lcp_stable_frames = 0;
    global.lcp_last_stage = "";
    global.lcp_manual_save = false;
    global.lcp_restore = undefined;
    global.lcp_failed_stage = "";

#define chat_command(_cmd, _args, _player)
    if (_cmd == "cpauto") {
        if (_args == "on") global.lcp_auto_save = true;
        else if (_args == "off") global.lcp_auto_save = false;
        trace("[LCP] Autosave: " + string(global.lcp_auto_save));
        return true;
    }
    if (_cmd == "cpautoload") {
        if (_args == "on") global.lcp_auto_resume = true;
        else if (_args == "off") global.lcp_auto_resume = false;
        trace("[LCP] Autoload: " + string(global.lcp_auto_resume));
        return true;
    }
    if (_cmd == "cpclear") {
        if (_args != "confirm") {
            trace("[LCP] Enter /cpclear confirm to erase both checkpoints.");
        } else if (!global.lcp_ready) {
            trace("[LCP] Wait for files to finish loading.");
        } else {
            file_delete(global.lcp_path_a);
            file_delete(global.lcp_path_b);
            global.lcp_saved = undefined;
            global.lcp_sequence = 0;
            global.lcp_fresh_pending = true;
            global.lcp_failed_stage = "";
            trace("[LCP] Both checkpoint files deleted.");
        }
        return true;
    }
    if (_cmd == "cpsave") {
        if (!global.lcp_ready || global.lcp_error) {
            trace("[LCP] File system not ready; save blocked.");
        } else if (global.lcp_phase != "live" || global.lcp_stable_frames < 8) {
            trace("[LCP] Save only during a stable playable level.");
        } else if (instance_exists(GameCont) && lcp_initial_stage(GameCont.area, GameCont.subarea, GameCont.loops)) {
            trace("[LCP] 1-1 is not checkpointed. Reach 1-2 first.");
        } else {
            global.lcp_manual_save = true;
            global.lcp_failed_stage = "";
            trace("[LCP] Save queued for this level.");
        }
        return true;
    }
    return false;

#define step
    // Load previous save before allowing any write. Fail closed on I/O failure.
    if (!global.lcp_ready && !global.lcp_error) {
        global.lcp_read_frames += 1;
        if (file_loaded(global.lcp_path_a) && file_loaded(global.lcp_path_b)) {
            try {
                var _a = lcp_read_slot(global.lcp_path_a);
                var _b = lcp_read_slot(global.lcp_path_b);
                if (_a != undefined && (_b == undefined || _a.sequence >= _b.sequence)) {
                    global.lcp_saved = _a;
                } else {
                    global.lcp_saved = _b;
                }
                if (global.lcp_saved != undefined) {
                    global.lcp_sequence = global.lcp_saved.sequence;
                }
                global.lcp_ready = true;
            } catch (_error) {
                global.lcp_error = true;
                trace("[LCP] Checkpoint read failed. Autosave disabled.");
            }
            file_unload(global.lcp_path_a);
            file_unload(global.lcp_path_b);
        } else if (global.lcp_read_frames > 600) {
            global.lcp_error = true;
            trace("[LCP] File load timeout: no writes allowed.");
        }
    }
    if (!global.lcp_ready || global.lcp_error) exit;
    // Menu visits distinguish new runs from native Retry.
    var _on_menu = !instance_exists(Player)
        && (instance_exists(CharSelect) || instance_exists(Menu));
    if (_on_menu) {
        global.lcp_menu_seen_since_start = true;
        if (!global.lcp_menu_visible) {
            global.lcp_menu_visible = true;
            if (global.lcp_saved != undefined) {
                trace("[LCP] Checkpoint: " + lcp_display_race(global.lcp_saved.race)
                    + " " + lcp_stage(global.lcp_saved));
            } else {
                trace("[LCP] No saved checkpoint. First save at 1-2.");
            }
        }
    } else {
        global.lcp_menu_visible = false;
    }
    if (!instance_exists(GameCont) || !lcp_is_solo()) {
        global.lcp_stable_frames = 0;
        exit;
    }
    // Original Session's post-generation gate: wait for the level to exist.
    if (instance_exists(GenCont) || instance_exists(LevCont) || instance_exists(Menu)) {
        global.lcp_stable_frames = 0;
        exit;
    }
    if (!instance_exists(Player)) {
        global.lcp_stable_frames = 0;
        exit;
    }
    global.lcp_stable_frames += 1;
    if (global.lcp_stable_frames < 8) exit;

    if (global.lcp_phase == "await-start") {
        // Only native Retry requests automatic checkpoint restoration.
        if (global.lcp_resume_requested && global.lcp_saved != undefined) {
            var _save = global.lcp_saved;
            global.lcp_restore = _save;
            global.lcp_phase = (GameCont.area == _save.area
                && GameCont.subarea == _save.subarea
                && GameCont.loops == _save.loops) ? "apply" : "warp";
        } else {
            global.lcp_phase = "live";
        }
    }

    if (global.lcp_phase == "warp") {
        // room_restart() is NOT exposed to NTGML mods in NTT v100.
        // The game's /goto implementation advances levels through Portal.
        // Leave the actual room transition to native game code.
        var _s = global.lcp_restore;
        var _g = instance_find(GameCont, 0);
        var _p = instance_find(Player, 0);
        if (_g == noone || _p == noone) {
            global.lcp_phase = "blocked";
            trace("[LCP] Unable to resume checkpoint; previous save preserved.");
            exit;
        }
        if (!lcp_supported_stage(_s.area, _s.subarea)) {
            global.lcp_phase = "blocked";
            trace("[LCP] Invalid stage: " + lcp_stage(_s));
            exit;
        }
        // NTT v100.034 does not expose `can_advance_stage`. Its normal
        // portal transition can still advance into Palace or secret stages
        // from a staged predecessor (subarea - 1). Verify the result in
        // the apply phase; never accept or overwrite a mismatched checkpoint.
        if (!lcp_require_fields(_g, "GameCont", ["area", "subarea", "loops", "hard", "crown", "lastarea", "lastsubarea", "levseed"])) {
            global.lcp_phase = "blocked";
            trace("[LCP] Warp blocked: required game-state fields missing.");
            exit;
        }
        _g.loops = _s.loops;
        _g.crown = _s.crown;
        _g.lastarea = _s.lastarea;
        _g.lastsubarea = _s.lastsubarea;
        _g.levseed = _s.levseed;
        if (variable_instance_exists(_g, "can_advance_stage")) {
            // Some builds expose a native "do not advance" flag.
            _g.area = _s.area;
            _g.subarea = _s.subarea;
            _g.hard = _s.hard;
            _g.can_advance_stage = false;
        } else {
            // Vanilla NTT /goto approach: set the stage just BEFORE the target.
            // Transition code advances the stage when the portal closes.
            _g.area = _s.area;
            _g.subarea = _s.subarea - 1;
            _g.hard = _s.hard - 1;
            // 2-1 / 4-1 / 6-1 require advancing from the previous area.
            if (_s.subarea == 1 && _s.area <= 6 && (_s.area mod 2) == 0) {
                _g.area = _s.area - 1;
                _g.subarea = 3;
            }
        }
        // Prepare the saved race for the upcoming native portal transition.
        // Actual Player initialization happens only after the transition.
        lcp_force_saved_character(_s, false);
        global.lcp_phase = "apply";
        global.lcp_stable_frames = 0;
        try {
            var _portal = instance_create(_p.x, _p.y, Portal);
            if (_portal == noone) {
                global.lcp_phase = "blocked";
                trace("[LCP] Could not create transition portal.");
                exit;
            }
            // Like the NTT debug /goto command, trigger portal end animation.
            with (_portal) {
                sprite_index = sprPortalDisappear;
                event_perform(ev_other, ev_animation_end);
            }
        } catch (_error) {
            global.lcp_phase = "blocked";
            trace("[LCP] Portal transition failed: " + string(_error));
        }
        exit;
    }
    if (global.lcp_phase == "apply") {
        var _target = global.lcp_restore;
        if (GameCont.area != _target.area || GameCont.subarea != _target.subarea || GameCont.loops != _target.loops) {
            global.lcp_phase = "blocked";
            trace("[LCP] Checkpoint level mismatch. Resume cancelled.");
            exit;
        }
        try {
            // Reinitialize the actual Player actor after the portal. Changing
            // the slot selection alone does not rebuild its character state.
            if (!lcp_force_saved_character(_target, true)) {
                global.lcp_phase = "blocked";
                trace("[LCP] Saved character could not be initialized; checkpoint preserved.");
                exit;
            }
            if (!lcp_apply(_target)) {
                global.lcp_phase = "blocked";
                trace("[LCP] Checkpoint restore failed; previous save preserved.");
                exit;
            }
            global.lcp_last_stage = lcp_stage(_target);
            global.lcp_phase = "live";
            global.lcp_restore = undefined;
            global.lcp_fresh_pending = false;
            trace("[LCP] Resumed " + lcp_display_race(_target.race) + " " + global.lcp_last_stage);
        } catch (_error) {
            global.lcp_phase = "blocked";
            trace("[LCP] Restore error: " + string(_error));
        }
        exit;
    }
    if (global.lcp_phase != "live") exit;
    var _stage = lcp_current_stage();
    if (_stage == global.lcp_last_stage && !global.lcp_manual_save) exit;
    // Do not keep retrying on every frame after a failed capture/write.
    if (_stage == global.lcp_failed_stage && !global.lcp_manual_save) exit;
    if (!global.lcp_auto_save && !global.lcp_manual_save) exit;
    if (!lcp_supported_stage(GameCont.area, GameCont.subarea)) exit;
    if (!is_real(GameCont.area) || GameCont.area < 1 || GameCont.subarea < 1) exit;
    // A new run begins without a checkpoint. The first is 1-2, not 1-1.
    // Later loops may revisit 1-1; do not skip those advanced runs.
    if (lcp_initial_stage(GameCont.area, GameCont.subarea, GameCont.loops)) exit;
    try {
        var _snapshot = lcp_capture();
        if (_snapshot == undefined) {
            global.lcp_failed_stage = _stage;
            trace("[LCP] Could not save this level; previous checkpoint preserved.");
            global.lcp_manual_save = false;
            exit;
        }
        var _nextseq = global.lcp_sequence + 1;
        _snapshot.sequence = _nextseq;
        var _slot = (_nextseq mod 2 == 0) ? global.lcp_path_a : global.lcp_path_b;
        string_save(json_encode(_snapshot), _slot);
        // Verify the newly written JSON through NTT's own file I/O API
        // before advertising success or selecting this as the active save.
        var _readback = lcp_read_slot(_slot);
        if (_readback == undefined || _readback.sequence != _nextseq
            || string_lower(string(_readback.race)) != string_lower(string(_snapshot.race))) {
            global.lcp_failed_stage = _stage;
            global.lcp_manual_save = false;
            trace("[LCP] Checkpoint save failed; previous save preserved.");
            exit;
        }
        global.lcp_sequence = _nextseq;
        global.lcp_saved = _snapshot;
        global.lcp_last_stage = _stage;
        global.lcp_failed_stage = "";
            global.lcp_manual_save = false;
        trace("[LCP] Saved " + lcp_display_race(_snapshot.race) + " " + _stage);
        if (global.lcp_fresh_pending) {
            // First checkpoint for ANY fresh run, regardless of race.
            // Make both slots current so an older run is never used as backup.
            // Only do this after primary write + readback succeeded.
            global.lcp_fresh_pending = false;
            var _other_slot = (_nextseq mod 2 == 0) ? global.lcp_path_b : global.lcp_path_a;
            var _other_ok = false;
            try {
                string_save(json_encode(_snapshot), _other_slot);
                var _other_readback = lcp_read_slot(_other_slot);
                _other_ok = (_other_readback != undefined
                    && _other_readback.sequence == _nextseq
                    && string_lower(string(_other_readback.race)) == string_lower(string(_snapshot.race)));
            } catch (_backup_error) {
                trace("[LCP] Backup checkpoint write failed.");
            }
            if (!_other_ok) {
                // Clearing the stale second slot is safer than accidentally
                // falling back to the previous character if one save corrupts.
                try {
                    file_delete(_other_slot);
                } catch (_delete_error) {
                    trace("[LCP] Could not remove stale backup.");
                }
            }
        }
    } catch (_error) {
        // A capture/write failure is recoverable. Only file LOAD errors should
        // disable all writes, as unread files may contain a valid checkpoint.
        global.lcp_failed_stage = _stage;
        global.lcp_manual_save = false;
        trace("[LCP] Save failed; previous checkpoint preserved. Use /cpsave to retry.");
    }

#define lcp_read_slot(_path)
    if (!file_exists(_path)) return undefined;
    var _obj = json_decode(string_load(_path));
    if (_obj == json_error || !is_object(_obj)) return undefined;
    if (lq_get(_obj, "schema") != 1) return undefined;
    if (!is_real(lq_get(_obj, "sequence"))) return undefined;
    if (!is_real(lq_get(_obj, "area"))) return undefined;
    if (!is_real(lq_get(_obj, "subarea"))) return undefined;
    if (lq_get(_obj, "area") < 1 || lq_get(_obj, "subarea") < 1) return undefined;
    if (!lcp_supported_saved_race(_obj)) return undefined;
    if (!is_array(lq_get(_obj, "ammo"))) return undefined;
    if (!is_array(lq_get(_obj, "mutations"))) return undefined;
    if (!is_array(lq_get(_obj, "ultras"))) return undefined;
    if (!is_real(lq_get(_obj, "level"))) return undefined;
    if (!is_real(lq_get(_obj, "health"))) return undefined;
    if (!is_real(lq_get(_obj, "maxhealth"))) return undefined;
    // Optional v0.6 fields must have compatible shapes if present.
    var _extra = lq_get(_obj, "extra_weapons");
    if (_extra != undefined && !is_object(_extra)) return undefined;
    // Old 0.4/0.5 saves were not created for Cuz. New Cuz saves must
    // include the complete third-weapon data, else fall back to older slot.
    if (string_lower(string(lq_get(_obj, "race"))) == "cuz") {
        if (!is_object(_extra)) return undefined;
        var _cur = lq_get(_extra, "extra_weps");
        if (!is_array(_cur)) return undefined;
    }
    var _ps = lq_get(_obj, "player_scalars");
    if (_ps != undefined && !is_object(_ps)) return undefined;
    var _gs = lq_get(_obj, "game_scalars");
    if (_gs != undefined && !is_object(_gs)) return undefined;
    return _obj;

#define lcp_force_saved_character(_save, _rebuild)
    // Retry only. NTT keeps the player-slot identity and the actual Player
    // instance as separate pieces of state. In particular, changing the slot
    // with player_set_race() does not always reconstruct a living actor.
    if (_rebuild) {
        var _p = instance_find(Player, 0);
        if (_p == noone || !variable_instance_exists(_p, "race")) return false;
        // The official NTT custom-race documentation says assigning Player.race
        // invokes race initialization. Do this BEFORE restoring equipment and
        // HP, as race-specific setup can reset default stats and sprites.
        try {
            if (string_lower(string(_p.race)) != string_lower(string(_save.race))) {
                _p.race = _save.race;
            }
        } catch (_actor_error) {
            return false;
        }
    }
    // Keep the next-run selection and current slot synchronized as well.
    try {
        player_set_race_pick(0, _save.race);
        player_set_race(0, _save.race);
    } catch (_slot_error) {
        // Some builds don't allow selection changes while a level is active.
        // The Player actor is authoritative for the immediate restore.
    }
    if (_rebuild) {
        // Abort rather than falsely claiming success for a different actor.
        var _check = instance_find(Player, 0);
        if (_check == noone || !variable_instance_exists(_check, "race")) return false;
        if (string_lower(string(_check.race)) != string_lower(string(_save.race))) return false;
    }
    return true;

#define lcp_builtin_race(_id, _name)
    // The old published NTT v100.022 API does not define char_cuz;
    // the U100 roster puts Cuz at ID 16. Check BOTH name and numeric ID,
    // so a custom race cannot sneak in through an untrusted save.
    if (!is_real(_id) || _id < char_fish || _id > 16 || !is_string(_name)) return false;
    var _n = string_lower(_name);
    return _n == "fish" || _n == "crystal" || _n == "eyes"
        || _n == "melting" || _n == "plant" || _n == "venuz" || _n == "yv"
        || _n == "steroids" || _n == "robot" || _n == "chicken"
        || _n == "rebel" || _n == "horror" || _n == "rogue"
        || _n == "bigdog" || _n == "skeleton" || _n == "frog" || _n == "cuz";

#define lcp_supported_saved_race(_save)
    return lcp_builtin_race(lq_get(_save, "race_id"), lq_get(_save, "race"));

#define lcp_initial_stage(_area, _subarea, _loops)
    return _area == 1 && _subarea == 1 && _loops == 0;

#define lcp_supported_stage(_area, _subarea)
    // Regular areas + U100 secret areas; only simple scalar numeric stages.
    // Secret and Palace transitions are attempted through the native portal;
    // the apply phase always validates the resulting target stage.
    return is_real(_area) && is_real(_subarea)
        && ((_area >= 1 && _area <= 7) || (_area >= 100 && _area <= 107))
        && _subarea >= 1 && _subarea <= 5;

// Allowlisted primitive per-character and persistent per-run attributes.
// Missing fields are ignored: U100 builds expose slightly different names.
#define lcp_player_scalar_keys()
    return ["curse", "bcurse", "reload", "breload", "can_shoot", "bcan_shoot",
        "rogueammo", "rogue_ammo", "rogue_ammo_max", "cuz_ammo", "cuz_ammo_max",
        "frogcharge", "froggas", "horrorcharge", "horrornorad", "horrorbeam",
        "hammerhead", "hammering", "headloses", "chickendeaths", "spirit",
        "canspirit", "can_spirit", "spirit_index", "dogammo", "infammo",
        "bleed", "patient", "veinsthreshold", "bskin", "max_extra_weps"];

#define lcp_game_scalar_keys()
    return ["crownpoints", "crownvisits", "junglevisits", "horror", "guitar",
        "droppedguitar", "droppedsword", "blackswords", "nochest", "noradch", "norads",
        "seenhq", "triedhq", "popolevel", "novans", "proto", "destiny",
        "patiencepick", "patienceskill", "patient", "skeleloops", "wasskeleton",
        "ultrapoints", "endpoints", "endcount",
        "banditbosses", "kills", "wepdrops", "wepmuts", "wepmuted", "wepseed",
        "mutseed", "patseed", "codseed", "junseed", "areanum", "maxsubarea"];

#define lcp_extra_weapon_keys()
    // Cuz has at least one third weapon in extra_weps. Keep companion state
    // arrays aligned with it (weapon curses, readiness and animation).
    return ["extra_weps", "extra_weps_curse", "extra_weps_reload",
        "extra_weps_wkick", "extra_weps_wepflip", "extra_weps_wepangle",
        "extra_weps_can_shoot"];

#define lcp_snapshot_scalars(_inst, _keys)
    var _data = {};
    for (var _i = 0; _i < array_length(_keys); _i += 1) {
        var _key = _keys[_i];
        if (!variable_instance_exists(_inst, _key)) continue;
        var _value = variable_instance_get(_inst, _key);
        if (is_real(_value) || is_bool(_value) || is_string(_value)) {
            lq_set(_data, _key, _value);
        }
    }
    return _data;

#define lcp_restore_scalars(_inst, _data, _keys)
    if (!is_object(_data)) return;
    for (var _i = 0; _i < array_length(_keys); _i += 1) {
        var _key = _keys[_i];
        if (!variable_instance_exists(_inst, _key)) continue;
        var _value = lq_get(_data, _key);
        if (is_real(_value) || is_bool(_value) || is_string(_value)) {
            variable_instance_set(_inst, _key, _value);
        }
    }

#define lcp_capture_extra_weps(_p)
    var _fields = lcp_extra_weapon_keys();
    var _data = {};
    for (var _i = 0; _i < array_length(_fields); _i += 1) {
        var _name = _fields[_i];
        if (!variable_instance_exists(_p, _name)) continue;
        var _value = variable_instance_get(_p, _name);
        if (is_array(_value)) lq_set(_data, _name, array_clone(_value));
    }
    return _data;

#define lcp_cuz_extra_fields_ok(_inst, _data)
    // A Cuz checkpoint is only complete if all of its third-weapon arrays
    // have been captured and still exist in the active game build.
    if (!is_object(_data)) return false;
    var _keys = lcp_extra_weapon_keys();
    var _size = -1;
    for (var _i = 0; _i < array_length(_keys); _i += 1) {
        var _name = _keys[_i];
        if (!variable_instance_exists(_inst, _name)) return false;
        var _value = lq_get(_data, _name);
        if (!is_array(_value)) return false;
        if (_size == -1) _size = array_length(_value);
        else if (array_length(_value) != _size) return false;
    }
    return true;

#define lcp_restore_extra_weps(_p, _data)
    if (!is_object(_data)) return;
    var _fields = lcp_extra_weapon_keys();
    for (var _i = 0; _i < array_length(_fields); _i += 1) {
        var _name = _fields[_i];
        var _value = lq_get(_data, _name);
        if (is_array(_value) && variable_instance_exists(_p, _name)) {
            variable_instance_set(_p, _name, array_clone(_value));
        }
    }

#define lcp_is_solo()
    // dp_player_count_active() appears in docs but isn't callable in NTGML.
    // player_is_active(slot) and maxp are in the exposed NTT API.
    // Count every slot: active players need not occupy consecutive slots.
    var _count = 0;
    for (var _slot = 0; _slot < maxp; _slot += 1) {
        if (player_is_active(_slot)) _count += 1;
    }
    return (_count == 1 && player_is_active(0) && instance_number(Player) == 1);

#define lcp_stage(_s)
    return string(_s.area) + "-" + string(_s.subarea) + " L" + string(_s.loops);

#define lcp_current_stage()
    return string(GameCont.area) + "-" + string(GameCont.subarea) + " L" + string(GameCont.loops);

// Validate EVERY game-state field before reading it. The public NTT API
// lists functions but cannot guarantee instance variables on a particular build.
#define lcp_require_fields(_obj, _label, _fields)
    for (var _j = 0; _j < array_length(_fields); _j += 1) {
        if (!variable_instance_exists(_obj, _fields[_j])) return false;
    }
    return true;

#define lcp_capture()
    var _p = instance_find(Player, 0);
    var _g = instance_find(GameCont, 0);
    if (_p == noone || _g == noone) return undefined;
    // Check first, do not access any required variables until confirmed.
    if (!lcp_require_fields(_g, "GameCont", ["area", "subarea", "loops", "hard", "crown", "lastarea", "lastsubarea", "levseed", "level", "rad", "skillpoints", "endskill"])) return undefined;
    if (!lcp_require_fields(_p, "Player", ["my_health", "maxhealth", "wep", "bwep", "ammo"])) return undefined;
    if (!is_real(_g.area) || _g.area < 1 || _g.subarea < 1) return undefined;
    if (!is_array(_p.ammo)) {
        return undefined;
    }
    var _mutations = [];
    for (var _i = 0; _i < 128; _i += 1) {
        var _id = skill_get_at(_i);
        if (_id == undefined) break;
        var _value = skill_get(_id);
        if (_value != 0) array_push(_mutations, {"id":_id, "value":_value});
    }
    var _raceid = player_get_race_id(0);
    var _racename = player_get_race(0);
    // This compares the two CURRENT representations only (not the old
    // checkpoint). Do not serialize a mislabeled run after a failed override.
    if (variable_instance_exists(_p, "race")
        && string_lower(string(_p.race)) != string_lower(string(_racename))) {
        return undefined;
    }
    if (!lcp_builtin_race(_raceid, _racename)) {
        return undefined;
    }
    if (string_lower(_racename) == "cuz") {
        var _extra_data = lcp_capture_extra_weps(_p);
        if (!lcp_cuz_extra_fields_ok(_p, _extra_data)) {
            return undefined;
        }
    }
    var _ultras = [];
    for (var _u = 1; _u <= ultra_count(_raceid); _u += 1) {
        var _v = ultra_get(_raceid, _u);
        if (_v != 0) array_push(_ultras, {"index":_u, "value":_v});
    }
    return {
        "schema": 1,
        "sequence": 0,
        "game_version": game_version,
        "area": _g.area,
        "subarea": _g.subarea,
        "loops": _g.loops,
        "hard": _g.hard,
        "crown": _g.crown,
        "lastarea": _g.lastarea,
        "lastsubarea": _g.lastsubarea,
        "levseed": _g.levseed,
        "level": _g.level,
        "rad": _g.rad,
        "skillpoints": _g.skillpoints,
        "endskill": _g.endskill,
        "race": player_get_race(0),
        "race_id": _raceid,
        "health": _p.my_health,
        "maxhealth": _p.maxhealth,
        "wep": _p.wep,
        "bwep": _p.bwep,
        "ammo": array_clone(_p.ammo),
        "mutations": _mutations,
        "ultras": _ultras,
        "skin": player_get_skin(0),
        "player_scalars": lcp_snapshot_scalars(_p, lcp_player_scalar_keys()),
        "game_scalars": lcp_snapshot_scalars(_g, lcp_game_scalar_keys()),
        "extra_weapons": lcp_capture_extra_weps(_p)
    };

#define lcp_apply(_s)
    var _p = instance_find(Player, 0);
    var _g = instance_find(GameCont, 0);
    if (_p == noone || _g == noone) return false;
    // Validate the target before changing ANY live state. A failed restore
    // should not half-apply mutations or equipment.
    if (!lcp_require_fields(_g, "GameCont", ["level", "rad", "skillpoints", "endskill", "crown", "hard"])) return false;
    if (!lcp_require_fields(_p, "Player", ["wep", "bwep", "ammo", "maxhealth", "my_health"])) return false;
    if (string_lower(string(_s.race)) == "cuz"
        && !lcp_cuz_extra_fields_ok(_p, lq_get(_s, "extra_weapons"))) {
        return false;
    }
    // NTT may run character setup when skin changes. Do that BEFORE restoring
    // mutations, equipment, HP, and character-specific scalar state.
    var _skin = lq_get(_s, "skin");
    if (_skin != undefined) player_set_skin(0, _skin);
    // Restore passive modifiers BEFORE adjusting HP to stored values.
    // Avoid resetting the world or touching custom assets.
    for (var _i = 0; _i < array_length(_s.mutations); _i += 1) {
        var _m = _s.mutations[_i];
        skill_set(_m.id, _m.value);
    }
    for (var _u = 0; _u < array_length(_s.ultras); _u += 1) {
        var _ultra = _s.ultras[_u];
        ultra_set(_s.race_id, _ultra.index, _ultra.value);
    }
    _g.level = _s.level;
    _g.rad = _s.rad;
    _g.skillpoints = _s.skillpoints;
    // v100 does not universally expose GameCont.ultrapoints.
    // Optional legacy slot for checkpoints made on older builds only.
    if (variable_instance_exists(_g, "ultrapoints") && lq_get(_s, "ultrapoints") != undefined) {
        _g.ultrapoints = _s.ultrapoints;
    }
    _g.endskill = _s.endskill;
    _g.crown = _s.crown;
    _g.hard = _s.hard;
    _p.wep = _s.wep;
    _p.bwep = _s.bwep;
    _p.ammo = array_clone(_s.ammo);
    _p.maxhealth = _s.maxhealth;
    _p.my_health = _s.health;
    lcp_restore_scalars(_g, lq_get(_s, "game_scalars"), lcp_game_scalar_keys());
    lcp_restore_scalars(_p, lq_get(_s, "player_scalars"), lcp_player_scalar_keys());
    lcp_restore_extra_weps(_p, lq_get(_s, "extra_weapons"));
    if (variable_instance_exists(_p, "lsthealth")) _p.lsthealth = _s.health;
    return true;

#define lcp_display_race(_race)
    // UI formatting ONLY. Never use the capitalized name for save identity.
    var _label = string(_race);
    if (string_length(_label) < 1) return _label;
    return string_upper(string_copy(_label, 1, 1)) + string_copy(_label, 2, string_length(_label) - 1);

