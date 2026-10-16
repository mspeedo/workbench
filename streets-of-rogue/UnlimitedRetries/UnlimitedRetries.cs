using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SoRUnlimitedRetries
{
    [BepInPlugin("mspeedo.sor.unlimited-retries", "SoR Unlimited Retries", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin Instance;
        private ConfigEntry<bool> enabled;
        private ConfigEntry<int> target;
        private ConfigEntry<bool> verbose;
        private Harmony harmony;
        private FieldInfo gcSingleton, sessionField, challengesField, continuesField;
        private bool ready, warned;
        private float elapsed;

        private void Awake()
        {
            Instance = this;
            enabled = Config.Bind("General", "Enabled", true,
                "Extend retries for the game's own Continue? mutator.");
            target = Config.Bind("General", "ContinuesPerFloor", 10000,
                "Effectively unlimited retries, safely below integer overflow.");
            verbose = Config.Bind("Diagnostics", "Verbose", false, "Log counter updates.");
            try
            {
                var asm = typeof(Plugin).Assembly;
                var game = AppDomain.CurrentDomain.GetAssemblies();
                Type gc = null, session = null, save = null;
                foreach (var a in game)
                {
                    if (a.GetName().Name != "Assembly-CSharp") continue;
                    gc = a.GetType("GameController", false);
                    session = a.GetType("SessionData", false);
                    save = a.GetType("SaveGame", false);
                    break;
                }
                if (gc == null || session == null || save == null)
                    throw new InvalidOperationException("Game types not loaded");
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                gcSingleton = gc.GetField("gameController", flags);
                sessionField = gc.GetField("sessionData", flags);
                challengesField = gc.GetField("challenges", flags);
                continuesField = session.GetField("continues", flags);
                var saveMethod = AccessTools.Method(save, "Save");
                if (gcSingleton == null || sessionField == null || challengesField == null ||
                    continuesField == null || continuesField.FieldType != typeof(int) ||
                    saveMethod == null)
                    throw new MissingMemberException("Unable to resolve game's save/retry metadata");
                harmony = new Harmony("mspeedo.sor.unlimited-retries");
                harmony.Patch(saveMethod,
                    prefix: new HarmonyMethod(typeof(Plugin).GetMethod("BeforeSave", BindingFlags.Static | BindingFlags.NonPublic)));
                ready = true;
                Logger.LogInfo("Unlimited Retries: patched SaveGame.Save; uses SessionData.continues");
            }
            catch (Exception e) { Logger.LogError("Unable to install retry patch: " + e); }
        }

        private static void BeforeSave()
        {
            if (Instance != null) Instance.RefreshCounter();
        }

        private void Update()
        {
            if (!ready || !enabled.Value) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 0.25f) return;
            elapsed = 0;
            RefreshCounter();
        }

        private void RefreshCounter()
        {
            if (!ready || !enabled.Value) return;
            try
            {
                object gc = gcSingleton.GetValue(null);
                if (gc == null || !HasContinueMutator(gc)) return;
                object session = sessionField.GetValue(gc);
                if (session == null) return;
                int current = (int)continuesField.GetValue(session);
                int desired = Math.Max(3, Math.Min(1000000, target.Value));
                if (current >= desired) return;
                continuesField.SetValue(session, desired);
                if (verbose.Value)
                    Logger.LogInfo("Refilled SessionData.continues: " + current + " -> " + desired);
            }
            catch (Exception e)
            {
                if (!warned) { Logger.LogWarning("Counter refresh failed: " + e.Message); warned = true; }
            }
        }

        private bool HasContinueMutator(object gc)
        {
            // Never touch saves or the counter unless vanilla Continue? is enabled.
            object raw = challengesField.GetValue(gc);
            var list = raw as IEnumerable;
            if (list == null) return false;
            foreach (object item in list)
            {
                if (item == null) continue;
                string name = item.ToString();
                if (name.Equals("ContinuesMode", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("D_ContinuesMode", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Continue", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Continue?", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }
}
