using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoRLargerText
{
    // Intentionally simple: all supported text is 1.35x its ORIGINAL size.
    // No size thresholds, adaptive reference, name filters or settings migration.
    // Rescans only after scene loads, not every 0.5 seconds.
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "2.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static readonly BindingFlags Props =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Plugin instance;
        private static bool internalWrite;

        private readonly Dictionary<int, Entry> known = new Dictionary<int, Entry>();
        private readonly List<Type> renderers = new List<Type>();
        private ConfigEntry<bool> enabled;
        private ConfigEntry<float> multiplier;
        private Harmony harmony;
        private Type uiTextType;
        private bool hooksInstalled, installing;

        private sealed class Entry
        {
            public Component Component;
            public float OriginalFont;
            public float AppliedFont;
            public Vector3 OriginalScale;
            public Vector3 AppliedScale;
            public bool Tk2d;
        }

        private void Awake()
        {
            instance = this;
            enabled = Config.Bind("General", "Enabled", true, "Enable font enlargement.");
            multiplier = Config.Bind("General", "Multiplier", 1.35f,
                "Multiply the original size of ALL detected text (1.35 = 35 percent larger).");
            // No settings migrations or writes. Existing Multiplier value is honored.
            harmony = new Harmony("mspeedo.sor.larger-text");

            SceneManager.sceneLoaded += OnSceneLoaded;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            DiscoverRenderersAndHooks();
            StartCoroutine(ScanAfterSceneLoad());
            Logger.LogInfo("Larger Text v2.0 loaded: all text at " + multiplier.Value.ToString("F2") +
                           "x; single scan per scene; no periodic polling.");
        }

        private void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly == null || installing) return;
            DiscoverRenderersAndHooks();
        }

        private void DiscoverRenderersAndHooks()
        {
            if (installing) return;
            installing = true;
            try
            {
                string[] names = {
                    "UnityEngine.UI.Text",
                    "UnityEngine.TextMesh",
                    "TMPro.TMP_Text",
                    "tk2dTextMesh"
                };
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (string name in names)
                    {
                        Type type = assembly.GetType(name, false);
                        if (type == null || !typeof(Component).IsAssignableFrom(type) ||
                            renderers.Contains(type)) continue;
                        renderers.Add(type);
                        Logger.LogInfo("Larger Text renderer: " + type.FullName);
                        if (name == "UnityEngine.UI.Text") uiTextType = type;
                    }
                }
                if (uiTextType != null && !hooksInstalled)
                    InstallTextHooks();
            }
            catch (Exception ex) { Logger.LogWarning("Renderer discovery: " + ex.Message); }
            finally { installing = false; }
        }

        private void InstallTextHooks()
        {
            hooksInstalled = true;
            int count = 0;
            // Hook the two common UI text updates. When a prefab spawns after
            // the one-time scan, changing its text usually invokes set_text.
            count += TryPatchSetter(uiTextType, "text");
            count += TryPatchSetter(uiTextType, "fontSize");

            // Graphic.OnEnable is inherited. It receives all UI Graphics,
            // so the callback cheaply rejects anything that is not UI Text.
            MethodInfo enable = null;
            for (Type t = uiTextType; t != null && enable == null; t = t.BaseType)
                enable = t.GetMethod("OnEnable", Props | BindingFlags.DeclaredOnly);
            if (enable != null)
            {
                try
                {
                    harmony.Patch(enable, postfix:
                        new HarmonyMethod(typeof(Plugin).GetMethod("OnTextEvent", BindingFlags.NonPublic | BindingFlags.Static)));
                    count++;
                }
                catch (Exception ex) { Logger.LogWarning("OnEnable hook: " + ex.Message); }
            }
            Logger.LogInfo("Larger Text UI lifecycle hooks: " + count);
        }

        private int TryPatchSetter(Type type, string property)
        {
            try
            {
                PropertyInfo p = type.GetProperty(property, Props);
                MethodInfo m = p == null ? null : p.GetSetMethod(true);
                if (m == null) return 0;
                harmony.Patch(m, postfix:
                    new HarmonyMethod(typeof(Plugin).GetMethod("OnTextEvent", BindingFlags.NonPublic | BindingFlags.Static)));
                return 1;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(property + " hook: " + ex.Message);
                return 0;
            }
        }

        private static void OnTextEvent(object __instance)
        {
            if (internalWrite || instance == null || instance.uiTextType == null ||
                !instance.uiTextType.IsInstanceOfType(__instance)) return;
            Component text = __instance as Component;
            if (text != null) instance.Adjust(text);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Keep original-size records for DontDestroyOnLoad UI. Clearing
            // them would cause persistent labels to grow again every level.
            var destroyed = new List<int>();
            foreach (var pair in known)
                if (pair.Value.Component == null)
                    destroyed.Add(pair.Key);
            foreach (int id in destroyed)
                known.Remove(id);
            StartCoroutine(ScanAfterSceneLoad());
        }

        private IEnumerator ScanAfterSceneLoad()
        {
            // Allow prefab and UI setup to finish; scans occur during loading,
            // never periodically mid-game.
            yield return null;
            yield return null;
            DiscoverRenderersAndHooks();
            int found = 0, modified = 0;
            foreach (Type type in renderers)
            {
                UnityEngine.Object[] objects;
                try { objects = Resources.FindObjectsOfTypeAll(type); }
                catch (Exception ex) { Logger.LogWarning("Scan " + type.Name + ": " + ex.Message); continue; }
                foreach (UnityEngine.Object item in objects)
                {
                    Component c = item as Component;
                    if (c == null || c.gameObject == null || !c.gameObject.scene.IsValid())
                        continue;
                    found++;
                    if (Adjust(c)) modified++;
                }
            }
            Logger.LogInfo("Larger Text scene scan: seen=" + found + " enlarged=" + modified);
        }

        private bool Adjust(Component component)
        {
            if (component == null || !enabled.Value) return false;
            Type type = component.GetType();
            bool tk2d = type.Name == "tk2dTextMesh";
            PropertyInfo property = type.GetProperty(tk2d ? "scale" : "fontSize", Props);
            if (property == null || !property.CanRead || !property.CanWrite) return false;
            int id = component.GetInstanceID();
            Entry entry;
            if (!known.TryGetValue(id, out entry) || entry.Component != component || entry.Tk2d != tk2d)
            {
                entry = new Entry { Component = component, Tk2d = tk2d };
                known[id] = entry;
                try
                {
                    if (tk2d)
                    {
                        entry.OriginalScale = (Vector3)property.GetValue(component, null);
                        entry.AppliedScale = entry.OriginalScale;
                    }
                    else
                    {
                        entry.OriginalFont = Convert.ToSingle(property.GetValue(component, null));
                        entry.AppliedFont = entry.OriginalFont;
                    }
                }
                catch { known.Remove(id); return false; }
            }
            float zoom = Mathf.Clamp(multiplier.Value, 1f, 3f);
            try
            {
                if (tk2d)
                {
                    Vector3 current = (Vector3)property.GetValue(component, null);
                    if ((current - entry.AppliedScale).sqrMagnitude > 0.000001f)
                        entry.OriginalScale = current; // Game changed its value.
                    Vector3 original = entry.OriginalScale;
                    Vector3 wanted = new Vector3(original.x * zoom, original.y * zoom, original.z);
                    if ((current - wanted).sqrMagnitude < 0.000001f) return false;
                    internalWrite = true;
                    try { property.SetValue(component, wanted, null); }
                    finally { internalWrite = false; }
                    entry.AppliedScale = wanted;
                    return true;
                }
                if (property.PropertyType != typeof(int) && property.PropertyType != typeof(float))
                    return false;
                float currentFont = Convert.ToSingle(property.GetValue(component, null));
                if (currentFont <= 0f || float.IsNaN(currentFont) || float.IsInfinity(currentFont)) return false;
                if (Math.Abs(currentFont - entry.AppliedFont) > 0.01f)
                    entry.OriginalFont = currentFont; // Game changed its value.
                float wantedFont = entry.OriginalFont * zoom;
                if (property.PropertyType == typeof(int))
                    wantedFont = (float)Math.Round(wantedFont);
                if (Math.Abs(currentFont - wantedFont) < 0.01f) return false;
                internalWrite = true;
                try
                {
                    property.SetValue(component,
                        property.PropertyType == typeof(int) ? (object)(int)wantedFont : wantedFont, null);
                }
                finally { internalWrite = false; }
                entry.AppliedFont = wantedFont;
                // Unity's Best Fit can immediately shrink text back to the
                // original visual size even after fontSize increases.
                // For a "scale every text" mod, disable that auto-shrink.
                if (uiTextType != null && uiTextType.IsInstanceOfType(component))
                {
                    PropertyInfo bestFit = type.GetProperty("resizeTextForBestFit", Props);
                    if (bestFit != null && bestFit.CanRead && bestFit.CanWrite &&
                        bestFit.PropertyType == typeof(bool))
                    {
                        try
                        {
                            if ((bool)bestFit.GetValue(component, null))
                                bestFit.SetValue(component, false, null);
                        }
                        catch { }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Text adjust " + component.name + ": " + ex.Message);
                return false;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            if (harmony != null) harmony.UnpatchSelf();
            known.Clear();
            if (instance == this) instance = null;
        }
    }
}
