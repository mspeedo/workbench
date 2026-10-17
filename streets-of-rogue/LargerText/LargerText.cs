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
    // v2.5 production: preserve v2.4's confirmed-working zoom and fitting
    // behavior. Remove all per-text diagnostic processing and logging.
    // Determine large-size tiers once at scene load (not periodically).
    // No startup settings migrations, overlay or repeated scans.
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "2.5.0")]
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
        private ConfigEntry<bool> protectLargeText;
        private ConfigEntry<bool> safeFit;
        // Separate thresholds for each renderer: tk2d scale cannot be
        // meaningfully compared to a Unity UI fontSize.
        private readonly Dictionary<string, float> largeSizeCutoffs =
            new Dictionary<string, float>();
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
            public bool OriginalBestFit;
            public bool HasBestFit;
            public bool HasBestFitMaximum;
            public int OriginalBestFitMaximum;
            public int AppliedBestFitMaximum;
        }

        private void Awake()
        {
            instance = this;
            enabled = Config.Bind("General", "Enabled", true, "Enable font enlargement.");
            multiplier = Config.Bind("General", "Multiplier", 1.20f,
                "Enlarge eligible text by 20 percent (1.2x).");
            protectLargeText = Config.Bind("General", "ProtectLargeText", true,
                "Keep the biggest text and obvious titles/headings at their original sizes.");
            safeFit = Config.Bind("General", "PreventTextClipping", true,
                "Only prevent enlargement when Unity is configured to truncate text and the enlarged text would exceed its height. Best Fit and Overflow text can enlarge.");
            // No settings migrations or writes. Existing Multiplier value is honored.
            harmony = new Harmony("mspeedo.sor.larger-text");

            SceneManager.sceneLoaded += OnSceneLoaded;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            DiscoverRenderersAndHooks();
            StartCoroutine(ScanAfterSceneLoad());
            Logger.LogInfo("Larger Text v2.5 loaded: zoom " + multiplier.Value.ToString("F2") +
                           "x; large-text protection " + protectLargeText.Value +
                           "; one scene scan, no periodic polling.");
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
            // Hook the two common UI text updates. When a prefab spawns after
            // the one-time scan, changing its text usually invokes set_text.
            TryPatchSetter(uiTextType, "text");
            TryPatchSetter(uiTextType, "fontSize");

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
                 }
                catch (Exception ex) { Logger.LogWarning("OnEnable hook: " + ex.Message); }
            }
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
            largeSizeCutoffs.Clear();
            StartCoroutine(ScanAfterSceneLoad());
        }

        private IEnumerator ScanAfterSceneLoad()
        {
            // Allow prefab and UI setup to finish; scans occur during loading,
            // never periodically mid-game.
            yield return null;
            yield return null;
            DiscoverRenderersAndHooks();
            // Gather first and classify original sizes BEFORE changing them.
            // This adds one sort per scene, never a recurring gameplay pass.
            var candidates = new List<Component>();
            var byRenderer = new Dictionary<string, List<float>>();
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
                    candidates.Add(c);
                    float originalSize;
                    if (TryGetOriginalVisualSize(c, out originalSize))
                    {
                        string key = RendererKey(c);
                        List<float> sizes;
                        if (!byRenderer.TryGetValue(key, out sizes))
                            byRenderer[key] = sizes = new List<float>();
                        sizes.Add(originalSize);
                    }
                }
            }

            largeSizeCutoffs.Clear();
            if (protectLargeText.Value)
            {
                foreach (var pair in byRenderer)
                {
                    List<float> sizes = pair.Value;
                    if (sizes.Count < 4) continue;
                    sizes.Sort();
                    float median = sizes[sizes.Count / 2];
                    float maximum = sizes[sizes.Count - 1];
                    // Only designate a "large tier" when it is clearly
                    // separated from ordinary text sizes. Never reject all
                    // labels just because a screen uses one uniform font.
                    if (median > 0 && maximum >= median * 1.35f)
                    {
                        largeSizeCutoffs[pair.Key] = (median + maximum) / 2f;
                    }
                }
            }
            foreach (Component c in candidates)
                Adjust(c);
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
                        if (uiTextType != null && uiTextType.IsInstanceOfType(component))
                        {
                            PropertyInfo bestFit = type.GetProperty("resizeTextForBestFit", Props);
                            if (bestFit != null && bestFit.CanRead && bestFit.CanWrite &&
                                bestFit.PropertyType == typeof(bool))
                            {
                                entry.OriginalBestFit = (bool)bestFit.GetValue(component, null);
                                entry.HasBestFit = true;
                            }
                            PropertyInfo maxProp = type.GetProperty("resizeTextMaxSize", Props);
                            if (maxProp != null && maxProp.CanRead && maxProp.CanWrite &&
                                maxProp.PropertyType == typeof(int))
                            {
                                entry.OriginalBestFitMaximum = (int)maxProp.GetValue(component, null);
                                entry.AppliedBestFitMaximum = entry.OriginalBestFitMaximum;
                                entry.HasBestFitMaximum = true;
                            }
                        }
                    }
                }
                catch { known.Remove(id); return false; }
            }
            float zoom = IsLargeText(component) ? 1f :
                Mathf.Clamp(multiplier.Value, 1f, 3f);
            try
            {
                if (tk2d)
                {
                    Vector3 current = (Vector3)property.GetValue(component, null);
                    if ((current - entry.AppliedScale).sqrMagnitude > 0.000001f)
                        entry.OriginalScale = current; // Game changed its value.
                    Vector3 original = entry.OriginalScale;
                    Vector3 wanted = new Vector3(original.x * zoom, original.y * zoom, original.z);
                    if ((current - wanted).sqrMagnitude < 0.000001f)
                        return false;
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
                bool changed = Math.Abs(currentFont - wantedFont) >= 0.01f;
                if (changed)
                {
                    internalWrite = true;
                    try
                    {
                        property.SetValue(component,
                            property.PropertyType == typeof(int) ? (object)(int)wantedFont : wantedFont, null);
                    }
                    finally { internalWrite = false; }
                }

                // Best Fit has an INDEPENDENT max font size. Changing fontSize
                // alone often makes no visual difference because that maximum
                // still clamps the generated text. Let Best Fit pick the actual
                // size within its existing rectangle, but allow a higher ceiling.
                if (entry.HasBestFit && entry.OriginalBestFit)
                {
                    PropertyInfo bestFit = type.GetProperty("resizeTextForBestFit", Props);
                    if (bestFit != null && bestFit.CanRead && bestFit.CanWrite)
                    {
                        try
                        {
                            if (!(bool)bestFit.GetValue(component, null))
                                bestFit.SetValue(component, true, null);
                        }
                        catch { }
                    }
                    if (entry.HasBestFitMaximum)
                    {
                        PropertyInfo maxProp = type.GetProperty("resizeTextMaxSize", Props);
                        if (maxProp != null)
                        {
                            try
                            {
                                int currentMax = (int)maxProp.GetValue(component, null);
                                if (currentMax != entry.AppliedBestFitMaximum)
                                    entry.OriginalBestFitMaximum = currentMax;
                                int desiredMax = Math.Max(1, (int)Math.Round(
                                    entry.OriginalBestFitMaximum * zoom));
                                if (currentMax != desiredMax)
                                {
                                    internalWrite = true;
                                    try { maxProp.SetValue(component, desiredMax, null); }
                                    finally { internalWrite = false; }
                                }
                                entry.AppliedBestFitMaximum = desiredMax;
                            }
                            catch (Exception ex)
                            {
                                Logger.LogDebug("Best Fit maximum: " + ex.Message);
                            }
                        }
                    }
                }

                // Run the guard for all UI text types, including dynamic
                // menu labels, tooltips and dialogues. It only vetoes actual
                // truncate-mode overflow; no category-specific exclusions.
                float rectHeight = 0f, preferredHeight = 0f;
                if (zoom > 1f && wantedFont > entry.OriginalFont &&
                    safeFit.Value && uiTextType != null &&
                    uiTextType.IsInstanceOfType(component))
                {
                    float originalHeight;
                    if (WouldClipText(component, type, entry,
                            out rectHeight, out preferredHeight, out originalHeight))
                    {
                        // Try all intermediate integer font sizes. Revert only
                        // if not even 1 px of enlargement fits the real label
                        // rectangle. This replaces v2.3's all-or-nothing veto.
                        int lower = (int)Math.Round(entry.OriginalFont);
                        int upper = (int)Math.Round(wantedFont);
                        int fitting = lower;
                        PropertyInfo h = type.GetProperty("preferredHeight", Props);
                        for (int candidate = upper - 1; candidate > lower; --candidate)
                        {
                            internalWrite = true;
                            try { property.SetValue(component, candidate, null); }
                            finally { internalWrite = false; }
                            float test = Convert.ToSingle(h.GetValue(component, null));
                            if (!float.IsNaN(test) && !float.IsInfinity(test) &&
                                test <= rectHeight + 0.5f)
                            {
                                fitting = candidate;
                                break;
                            }
                        }
                        // Apply the highest safe fit (possibly vanilla).
                        internalWrite = true;
                        try { property.SetValue(component, fitting, null); }
                        finally { internalWrite = false; }
                        wantedFont = fitting;
                        changed = fitting > lower;
                    }
                }

                entry.AppliedFont = wantedFont;
                return changed;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Text adjust " + component.name + ": " + ex.Message);
                return false;
            }
        }

        private bool WouldClipText(Component component, Type type, Entry entry,
            out float available, out float enlargedHeight, out float originalHeight)
        {
            available = 0f;
            enlargedHeight = 0f;
            originalHeight = 0f;
            try
            {
                // The previous guard used preferredHeight > rect.height for
                // every text component. That is incorrect for Overflow mode
                // and Best Fit and excluded many perfectly readable labels.
                // Let Unity handle both cases using its own text pipeline.
                PropertyInfo bestFit = type.GetProperty("resizeTextForBestFit", Props);
                if (bestFit != null && bestFit.CanRead &&
                    (bool)bestFit.GetValue(component, null))
                    return false;

                PropertyInfo verticalOverflow = type.GetProperty("verticalOverflow", Props);
                if (verticalOverflow == null || !verticalOverflow.CanRead ||
                    verticalOverflow.GetValue(component, null).ToString() != "Truncate")
                    return false;

                RectTransform rect = component.transform as RectTransform;
                if (rect == null) return false;
                available = rect.rect.height;
                if (available <= 1f) return false; // Layout has not settled.

                PropertyInfo textProperty = type.GetProperty("text", Props);
                string content = textProperty != null
                    ? textProperty.GetValue(component, null) as string : null;
                if (string.IsNullOrEmpty(content)) return false;

                PropertyInfo heightProperty = type.GetProperty("preferredHeight", Props);
                if (heightProperty == null || !heightProperty.CanRead) return false;

                enlargedHeight = Convert.ToSingle(heightProperty.GetValue(component, null));
                if (float.IsNaN(enlargedHeight) || float.IsInfinity(enlargedHeight))
                    return false;
                if (enlargedHeight <= available + 0.5f)
                    return false; // The enlarged label still fits. Keep it.

                // When Unity reports an overflow for the enlarged candidate,
                // check whether it was already reporting the same overflow
                // for the original font. This avoids classifying an existing
                // unconstrained layout as a new clipping regression.
                PropertyInfo font = type.GetProperty("fontSize", Props);
                if (font == null || !font.CanRead || !font.CanWrite) return false;
                object enlarged = font.GetValue(component, null);
                internalWrite = true;
                try
                {
                    font.SetValue(component, (int)Math.Round(entry.OriginalFont), null);
                    originalHeight = Convert.ToSingle(heightProperty.GetValue(component, null));
                }
                finally
                {
                    try { font.SetValue(component, enlarged, null); }
                    finally { internalWrite = false; }
                }

                if (float.IsNaN(originalHeight) || float.IsInfinity(originalHeight))
                    return false;

                // If even vanilla is reported as overflowing, preferredHeight
                // cannot establish that our increase introduced clipping;
                // leave the label enlarged. Truncation introduced by zoom is
                // what we can safely detect and prevent.
                return originalHeight <= available + 0.5f &&
                       enlargedHeight > available + 0.5f;
            }
            catch (Exception e)
            {
                Logger.LogDebug("Fit check: " + e.Message);
                return false;
            }
        }

        private static string RendererKey(Component c)
        {
            return c.GetType().FullName;
        }

        private bool TryGetOriginalVisualSize(Component component, out float size)
        {
            size = 0f;
            if (component == null) return false;
            try
            {
                bool tk2d = component.GetType().Name == "tk2dTextMesh";
                Entry existing;
                bool tracked = known.TryGetValue(component.GetInstanceID(), out existing) &&
                    existing.Component == component && existing.Tk2d == tk2d;
                if (tk2d)
                {
                    var p = component.GetType().GetProperty("scale", Props);
                    Vector3 v = tracked ? existing.OriginalScale :
                        (Vector3)p.GetValue(component, null);
                    size = Math.Abs(v.y * component.transform.lossyScale.y);
                }
                else
                {
                    var p = component.GetType().GetProperty("fontSize", Props);
                    float v = tracked ? existing.OriginalFont :
                        Convert.ToSingle(p.GetValue(component, null));
                    size = Math.Abs(v * component.transform.lossyScale.y);
                }
                return size > 0.00001f && !float.IsNaN(size) && !float.IsInfinity(size);
            }
            catch { return false; }
        }

        // Reuse the original visual-size cutoffs from the scene-load scan.
        // Avoid constructing diagnostic reason strings on every text update.
        private bool IsLargeText(Component component)
        {
            if (!protectLargeText.Value) return false;
            Transform t = component.transform;
            for (int i = 0; i < 3 && t != null; i++, t = t.parent)
            {
                string n = t.gameObject.name.ToLowerInvariant();
                if (n.Contains("title") || n.Contains("header") ||
                    n.Contains("heading") || n.Contains("logo") ||
                    n.Contains("bigtext") || n.Contains("largetext") ||
                    n.Contains("banner"))
                    return true;
            }
            float cutoff;
            float size;
            return largeSizeCutoffs.TryGetValue(RendererKey(component), out cutoff) &&
                   TryGetOriginalVisualSize(component, out size) &&
                   size >= cutoff;
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
