using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SoRLargerText
{
    /// <summary>
    /// Event-driven small text adjustment. No Update(), OnGUI(), or hierarchy scans.
    /// Version 1.1 deliberately uses fixed cutoffs instead of a moving scene
    /// percentile, so headings cannot become "small" just because another giant
    /// text element entered the scene.
    /// </summary>
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "1.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static Plugin instance;
        private static bool writingFont;
        private static bool writingTk;
        private static Type uiTextType;
        private static Type tkTextType;
        private static PropertyInfo uiFontSize;
        private static PropertyInfo uiBestFit;
        private static PropertyInfo tkScale;
        private static readonly ConditionalWeakTable<Component, TextState> states =
            new ConditionalWeakTable<Component, TextState>();

        private ConfigEntry<bool> enabled, enableTk2d, excludeHeadings, trace;
        private ConfigEntry<float> zoom, maxVisualPx, maxTkVisualSize;
        private ConfigEntry<int> maxOriginalSize;
        private Harmony harmony;

        private sealed class TextState
        {
            public float BaselineFont;
            public float AppliedFont;
            public Vector3 BaselineTk;
            public Vector3 AppliedTk;
            public bool HasFont;
            public bool HasTk;
            public bool HasBestFit;
            public bool OriginalBestFit;
        }

        private void Awake()
        {
            instance = this;
            enabled = Config.Bind("General", "Enabled", true, "Enable smaller-text enlargement.");
            zoom = Config.Bind("General", "Multiplier", 1.50f,
                "Enlarge eligible small text by up to 1.5x.");
            maxVisualPx = Config.Bind("General", "MaxVisualFontSize", 25f,
                "Only resize text with original size strictly below this estimated onscreen size.");
            maxOriginalSize = Config.Bind("General", "MaxOriginalFontSize", 40,
                "Additional safety bound for the original fontSize; larger headings remain unchanged.");
            excludeHeadings = Config.Bind("General", "ProtectTitlesAndButtons", true,
                "Protect title/header/large/button/logo labels from text scaling.");
            enableTk2d = Config.Bind("General", "EnableTk2d", false,
                "Experimental: also scale legacy tk2d labels; disabled by default because tk2d font dimensions differ from Unity UI.");
            maxTkVisualSize = Config.Bind("General", "MaxTk2dVisualScale", 0.60f,
                "Experimental tk2d selection threshold based on its original text scale.");
            trace = Config.Bind("Diagnostics", "LogEligibleText", false,
                "Log eligible text components as they are initialized (diagnostics only).");

            // Intentionally NO settings migration: prior config values are respected.
            // To load all new defaults, delete mspeedo.sor.larger-text.cfg with the game closed.
            harmony = new Harmony("mspeedo.sor.larger-text");
            int patched = 0;
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (uiTextType == null)
                        uiTextType = assembly.GetType("UnityEngine.UI.Text", false);
                    if (tkTextType == null)
                        tkTextType = assembly.GetType("tk2dTextMesh", false);
                }

                if (uiTextType != null)
                {
                    uiBestFit = uiTextType.GetProperty("resizeTextForBestFit", BindingFlags.Instance | BindingFlags.Public);
                    uiFontSize = uiTextType.GetProperty("fontSize",
                        BindingFlags.Instance | BindingFlags.Public);
                    MethodInfo setter = uiFontSize != null ? uiFontSize.GetSetMethod() : null;
                    if (setter != null)
                    {
                        harmony.Patch(setter,
                            prefix: new HarmonyMethod(typeof(Plugin).GetMethod("FontSizePrefix",
                                BindingFlags.Static | BindingFlags.NonPublic)),
                            postfix: new HarmonyMethod(typeof(Plugin).GetMethod("FontSizePostfix",
                                BindingFlags.Static | BindingFlags.NonPublic)));
                        patched++;
                    }

                    // Text usually inherits Graphic.OnEnable. Hooking Graphic's
                    // implementation catches activation without polling the scene.
                    Type graphic = uiTextType.BaseType;
                    MethodInfo onEnable = null;
                    for (Type t = uiTextType; t != null && onEnable == null; t = t.BaseType)
                        onEnable = t.GetMethod("OnEnable", BindingFlags.Instance |
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    if (onEnable != null)
                    {
                        harmony.Patch(onEnable,
                            postfix: new HarmonyMethod(typeof(Plugin).GetMethod("UiEnablePostfix",
                                BindingFlags.Static | BindingFlags.NonPublic)));
                        patched++;
                    }
                }

                if (enableTk2d.Value && tkTextType != null)
                {
                    tkScale = tkTextType.GetProperty("scale",
                        BindingFlags.Public | BindingFlags.Instance);
                    MethodInfo setter = tkScale != null ? tkScale.GetSetMethod() : null;
                    if (setter != null)
                    {
                        harmony.Patch(setter,
                            prefix: new HarmonyMethod(typeof(Plugin).GetMethod("TkScalePrefix",
                                BindingFlags.Static | BindingFlags.NonPublic)),
                            postfix: new HarmonyMethod(typeof(Plugin).GetMethod("TkScalePostfix",
                                BindingFlags.Static | BindingFlags.NonPublic)));
                        patched++;
                    }
                    MethodInfo enableTk = tkTextType.GetMethod("OnEnable",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly);
                    if (enableTk != null)
                    {
                        harmony.Patch(enableTk,
                            postfix: new HarmonyMethod(typeof(Plugin).GetMethod("TkEnablePostfix",
                                BindingFlags.Static | BindingFlags.NonPublic)));
                        patched++;
                    }
                }
                Logger.LogInfo("Larger Text 1.1 loaded: " + patched +
                    " lifecycle/property hooks; no scan, no overlay, no settings migration.");
                if (patched == 0)
                    Logger.LogWarning("No supported UI text hooks found; please send BepInEx/LogOutput.log.");
            }
            catch (Exception e)
            {
                Logger.LogError("Text hook setup failed: " + e);
            }
        }

        private static void UiEnablePostfix(object __instance)
        {
            if (writingFont || instance == null || uiTextType == null ||
                !uiTextType.IsInstanceOfType(__instance)) return;
            Component component = __instance as Component;
            if (component != null) instance.ResizeUi(component, null);
        }

        private static void FontSizePrefix(object __instance, int __0)
        {
            if (writingFont || instance == null || __instance == null) return;
            Component component = __instance as Component;
            if (component == null) return;
            TextState state = states.GetOrCreateValue(component);
            if (!state.HasFont || Math.Abs(__0 - state.AppliedFont) > 0.01f)
            {
                state.BaselineFont = __0;
                state.HasFont = true;
            }
        }

        private static void FontSizePostfix(object __instance)
        {
            if (writingFont || instance == null) return;
            Component component = __instance as Component;
            if (component != null) instance.ResizeUi(component, null);
        }

        private void ResizeUi(Component component, float? supplied)
        {
            if (uiFontSize == null || component == null) return;
            try
            {
                TextState state = states.GetOrCreateValue(component);
                float current = supplied ?? Convert.ToSingle(uiFontSize.GetValue(component, null));
                if (!state.HasFont)
                {
                    state.BaselineFont = current;
                    state.AppliedFont = current;
                    state.HasFont = true;
                    if (uiBestFit != null && uiBestFit.CanRead && uiBestFit.CanWrite)
                    {
                        try
                        {
                            state.OriginalBestFit = (bool)uiBestFit.GetValue(component, null);
                            state.HasBestFit = true;
                        }
                        catch { state.HasBestFit = false; }
                    }
                }
                float size = state.BaselineFont;
                bool eligible = enabled.Value &&
                    size > 0f &&
                    size < maxOriginalSize.Value &&
                    !IsTitleOrButton(component) &&
                    GetVisualSize(size, component.transform) < maxVisualPx.Value;
                float originalVisual = GetVisualSize(size, component.transform);
                float factor = eligible
                    ? Mathf.Min(Mathf.Clamp(zoom.Value, 1f, 2f), maxVisualPx.Value / originalVisual)
                    : 1f;
                float wanted = eligible ? (float)Math.Round(size * factor) : size;
                // Best Fit can silently shrink text back to its old rendered size.
                // Restore its original state whenever a label is not enlarged.
                if (state.HasBestFit && uiBestFit != null)
                {
                    try
                    {
                        bool bestFit = state.OriginalBestFit && !(eligible && factor > 1.001f);
                        if ((bool)uiBestFit.GetValue(component, null) != bestFit)
                            uiBestFit.SetValue(component, bestFit, null);
                    }
                    catch { }
                }
                if (eligible && trace.Value && Math.Abs(state.AppliedFont - wanted) > 0.01f)
                    Logger.LogInfo("Text enlarged: " + component.name +
                        " original=" + size + " new=" + wanted);
                if (Math.Abs(current - wanted) > 0.01f)
                {
                    writingFont = true;
                    try { uiFontSize.SetValue(component, (int)wanted, null); }
                    finally { writingFont = false; }
                }
                state.AppliedFont = wanted;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("UI text adjust: " + ex.Message);
            }
        }

        private float GetVisualSize(float original, Transform transform)
        {
            if (transform == null) return original;
            float scale = Math.Abs(transform.lossyScale.y);
            if (float.IsNaN(scale) || scale <= 0.00001f) return original;
            return original * scale;
        }

        private bool IsTitleOrButton(Component component)
        {
            if (!excludeHeadings.Value) return false;
            // Inspect just a few ancestors on text lifecycle events, rather
            // than repeatedly traversing every object in the scene.
            Transform t = component.transform;
            for (int i = 0; i < 3 && t != null; i++, t = t.parent)
            {
                string name = t.gameObject.name.ToLowerInvariant();
                if (name.Contains("title") || name.Contains("header") ||
                    name.Contains("heading") || name.Contains("logo") ||
                    name.Contains("bigtext") || name.Contains("largetext") ||
                    name.Contains("bigmessage") || name.Contains("button"))
                    return true;
            }
            return false;
        }

        private static void TkScalePrefix(object __instance, Vector3 __0)
        {
            if (writingTk || instance == null || __instance == null) return;
            Component c = __instance as Component;
            if (c == null) return;
            TextState s = states.GetOrCreateValue(c);
            if (!s.HasTk || (__0 - s.AppliedTk).sqrMagnitude > 0.000001f)
            {
                s.BaselineTk = __0;
                s.HasTk = true;
            }
        }

        private static void TkScalePostfix(object __instance)
        {
            if (writingTk || instance == null) return;
            Component c = __instance as Component;
            if (c != null) instance.ResizeTk(c);
        }

        private static void TkEnablePostfix(object __instance)
        {
            if (writingTk || instance == null) return;
            Component c = __instance as Component;
            if (c != null) instance.ResizeTk(c);
        }

        private void ResizeTk(Component c)
        {
            if (tkScale == null || c == null) return;
            try
            {
                TextState s = states.GetOrCreateValue(c);
                Vector3 current = (Vector3)tkScale.GetValue(c, null);
                if (!s.HasTk)
                {
                    s.BaselineTk = current;
                    s.AppliedTk = current;
                    s.HasTk = true;
                }
                Vector3 b = s.BaselineTk;
                float visual = GetVisualSize(Math.Abs(b.y), c.transform);
                bool eligible = enabled.Value && enableTk2d.Value &&
                    visual < maxTkVisualSize.Value && !IsTitleOrButton(c);
                float factor = eligible ? Mathf.Clamp(zoom.Value, 1f, 2f) : 1f;
                Vector3 wanted = new Vector3(b.x * factor, b.y * factor, b.z);
                if ((current - wanted).sqrMagnitude > 0.000001f)
                {
                    writingTk = true;
                    try { tkScale.SetValue(c, wanted, null); }
                    finally { writingTk = false; }
                }
                s.AppliedTk = wanted;
            }
            catch (Exception ex) { Logger.LogDebug("tk2d text adjust: " + ex.Message); }
        }

        private void OnDestroy()
        {
            if (harmony != null) harmony.UnpatchSelf();
            if (instance == this) instance = null;
        }
    }
}
