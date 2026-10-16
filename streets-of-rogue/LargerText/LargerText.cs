using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoRLargerText
{
    // Unlike v1.1, this version does not reject text based on a guessed
    // absolute font-size cutoff. It observes actual UI text creation/rebuild
    // events and compares originally-rendered sizes within the same Canvas.
    //
    // No periodic scans, Update(), OnGUI(), settings migrations or scene-wide
    // Resources.FindObjectsOfTypeAll() calls.
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "1.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const BindingFlags Props = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Plugin instance;
        private static bool internalWrite;
        private readonly Dictionary<int, TextEntry> entries = new Dictionary<int, TextEntry>();
        private readonly List<TextEntry> work = new List<TextEntry>();
        private readonly List<float> sizes = new List<float>();
        private readonly List<int> expired = new List<int>();
        private Type uiType;
        private PropertyInfo fontSizeProp, bestFitProp;
        private Harmony harmony;
        private bool dirty;
        private bool handling;
        private bool hooksInstalled;
        private bool installing;
        private bool listenersInstalled;
        private int revision;
        private ConfigEntry<bool> enabled, protectHeadings, logging;
        private ConfigEntry<float> multiplier, smallRatio;

        private sealed class TextEntry
        {
            internal Component Component;
            internal Canvas ParentCanvas;
            internal float Original;
            internal float Applied;
            internal float OriginalVisual;
            internal bool HasBestFit;
            internal bool OriginalBestFit;
            internal int LastRevision;
        }

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true, "Enlarge eligible smaller fonts.");
            multiplier = Config.Bind("General", "Multiplier", 1.35f,
                "Maximum enlargement of qualifying small text (1.35 = 35% larger).");
            // Deliberately new setting name: prior v0.4/v1.0 ratios cannot
            // silently override the new conservative selection.
            smallRatio = Config.Bind("General", "SmallToNormalRatio", 0.78f,
                "Only resize text originally smaller than this fraction of ordinary UI text in its Canvas.");
            protectHeadings = Config.Bind("General", "ProtectHeadingsAndButtons", true,
                "Leave headers, titles, buttons, logos and big messages at their original size.");
            logging = Config.Bind("Diagnostics", "LogResizes", false,
                "Log enlarged text names/sizes. Off by default.");
            instance = this;

            // Some Unity UI assemblies load after the BepInEx plugin Awake().
            // AssemblyLoad is event driven: no scene scans or periodic polling.
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            TryInstallHooks();
        }

        private void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            if (hooksInstalled || args.LoadedAssembly == null) return;
            string name = args.LoadedAssembly.GetName().Name;
            if (name == "UnityEngine.UI" || name == "UnityEngine.UIElementsModule")
                TryInstallHooks();
        }

        private void TryInstallHooks()
        {
            if (hooksInstalled || installing) return;
            installing = true;
            try
            {
                uiType = Type.GetType("UnityEngine.UI.Text, UnityEngine.UI", false);
                if (uiType == null)
                {
                    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        uiType = a.GetType("UnityEngine.UI.Text", false);
                        if (uiType != null) break;
                    }
                }
                if (uiType == null)
                {
                    Logger.LogInfo("Larger Text: waiting for UnityEngine.UI to load.");
                    return;
                }
                fontSizeProp = uiType.GetProperty("fontSize", Props);
                bestFitProp = uiType.GetProperty("resizeTextForBestFit", Props);
                if (fontSizeProp == null || !fontSizeProp.CanRead || !fontSizeProp.CanWrite)
                    throw new MissingMemberException("Unity UI Text.fontSize property missing");

                // Guaranteed to run when Unity generates the actual text mesh.
                // A text element is registered on its first render, even if it
                // never calls fontSize.set at runtime.
                MethodInfo populate = uiType.GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (populate == null)
                    throw new MissingMethodException("Unity UI Text.OnPopulateMesh missing");

                harmony = new Harmony("mspeedo.sor.larger-text");
                harmony.Patch(populate, postfix: new HarmonyMethod(typeof(Plugin)
                    .GetMethod("AfterPopulateMesh", BindingFlags.Static | BindingFlags.NonPublic)));

                // Some UI labels change their font size after initialization.
                // Record their new baseline without polling.
                MethodInfo setFontSize = fontSizeProp.GetSetMethod(true);
                if (setFontSize != null)
                    harmony.Patch(setFontSize, postfix: new HarmonyMethod(typeof(Plugin)
                        .GetMethod("AfterFontSizeChange", BindingFlags.Static | BindingFlags.NonPublic)));

                if (!listenersInstalled)
                {
                    Canvas.willRenderCanvases += ProcessDirtyUi;
                    SceneManager.sceneLoaded += SceneLoaded;
                    listenersInstalled = true;
                }
                hooksInstalled = true;
                Logger.LogInfo("Larger Text v1.2.0 active: text-mesh hooks installed. " +
                               "Zoom=" + multiplier.Value.ToString("F2") +
                               "x, small ratio=" + smallRatio.Value.ToString("F2") +
                               ". No polling, overlay or migration.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Larger Text: unable to install text hooks: " + ex);
            }
            finally { installing = false; }
        }

        private static void AfterPopulateMesh(object __instance)
        {
            if (internalWrite || instance == null) return;
            Component c = __instance as Component;
            if (c != null) instance.Capture(c);
        }

        private static void AfterFontSizeChange(object __instance)
        {
            if (internalWrite || instance == null) return;
            Component c = __instance as Component;
            if (c != null) instance.Capture(c);
        }

        private void Capture(Component c)
        {
            if (c == null || !c.gameObject.activeInHierarchy || !c.gameObject.scene.IsValid()) return;
            try
            {
                float current = Convert.ToSingle(fontSizeProp.GetValue(c, null));
                if (current <= 0f || float.IsInfinity(current) || float.IsNaN(current)) return;
                int id = c.GetInstanceID();
                TextEntry e;
                if (!entries.TryGetValue(id, out e) || e.Component != c)
                {
                    e = new TextEntry {
                        Component = c, Original = current, Applied = current,
                        ParentCanvas = c.GetComponentInParent<Canvas>()
                    };
                    if (bestFitProp != null && bestFitProp.CanRead && bestFitProp.CanWrite)
                    {
                        try {
                            e.OriginalBestFit = (bool)bestFitProp.GetValue(c, null);
                            e.HasBestFit = true;
                        } catch { }
                    }
                    entries[id] = e;
                }
                else if (Math.Abs(current - e.Applied) > 0.01f)
                {
                    // The GAME changed this label's font, not us.
                    e.Original = current;
                    e.Applied = current;
                }

                float visualScale = Math.Abs(c.transform.lossyScale.y);
                if (visualScale < 0.0001f || float.IsNaN(visualScale))
                    visualScale = 1f;
                float originalVisual = e.Original * visualScale;
                if (Math.Abs(e.OriginalVisual - originalVisual) > 0.01f ||
                    e.LastRevision == 0)
                {
                    e.OriginalVisual = originalVisual;
                    dirty = true;
                }
                // Newly visible UI may need reclassification against its peers.
                e.LastRevision = ++revision;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Text capture: " + ex.Message);
            }
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            entries.Clear();
            dirty = false;
        }

        private void ProcessDirtyUi()
        {
            if (!dirty || handling || instance != this) return;
            handling = true;
            dirty = false;
            try
            {
                // Iterate only text meshes we've actually observed rendering.
                // Never search Unity's entire scene/object registry.
                expired.Clear();
                foreach (var pair in entries)
                    if (pair.Value.Component == null)
                        expired.Add(pair.Key);
                foreach (int id in expired) entries.Remove(id);

                work.Clear();
                foreach (var e in entries.Values)
                {
                    Component c = e.Component;
                    if (c == null || !c.gameObject.activeInHierarchy ||
                        !c.gameObject.scene.IsValid()) continue;
                    var behavior = c as Behaviour;
                    if (behavior != null && !behavior.enabled) continue;
                    work.Add(e);
                }
                if (work.Count < 3) return; // A single title must not classify itself as small.

                // Each Canvas is compared only against its own text. This keeps
                // popups and HUD groups from changing the menu's size reference.
                var groups = new Dictionary<int, List<TextEntry>>();
                foreach (TextEntry e in work)
                {
                    int key = e.ParentCanvas == null ? 0 : e.ParentCanvas.GetInstanceID();
                    List<TextEntry> group;
                    if (!groups.TryGetValue(key, out group))
                        groups[key] = group = new List<TextEntry>();
                    group.Add(e);
                }
                foreach (List<TextEntry> group in groups.Values)
                {
                    if (group.Count < 3) continue;
                    sizes.Clear();
                    foreach (TextEntry e in group) sizes.Add(e.OriginalVisual);
                    sizes.Sort();
                    float reference = sizes[(int)Math.Floor((sizes.Count - 1) * 0.85f)];
                    float cutoff = reference * Mathf.Clamp(smallRatio.Value, 0.35f, 0.90f);
                    foreach (TextEntry e in group)
                    {
                        Component c = e.Component;
                        if (c == null) continue;
                        bool eligible = enabled.Value &&
                                        e.OriginalVisual < cutoff &&
                                        !IsProtected(c);
                        float factor = eligible
                            ? Mathf.Min(Mathf.Clamp(multiplier.Value, 1f, 2f),
                                reference / Math.Max(e.OriginalVisual, 0.001f))
                            : 1f;
                        float target = (float)Math.Round(e.Original * factor);
                        try
                        {
                            if (e.HasBestFit && bestFitProp != null)
                            {
                                bool shouldFit = e.OriginalBestFit && factor <= 1.001f;
                                if ((bool)bestFitProp.GetValue(c, null) != shouldFit)
                                    bestFitProp.SetValue(c, shouldFit, null);
                            }
                            float current = Convert.ToSingle(fontSizeProp.GetValue(c, null));
                            if (Math.Abs(current - target) > 0.01f)
                            {
                                internalWrite = true;
                                try { fontSizeProp.SetValue(c, (int)target, null); }
                                finally { internalWrite = false; }
                                if (logging.Value && eligible)
                                    Logger.LogInfo("Small text: " + c.name + " " +
                                                   e.Original + " -> " + target);
                            }
                            e.Applied = target;
                        }
                        catch (Exception ex) { Logger.LogDebug("Text resize: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Logger.LogWarning("Text processing: " + ex.Message); }
            finally { handling = false; }
        }

        private bool IsProtected(Component c)
        {
            if (!protectHeadings.Value) return false;
            Transform t = c.transform;
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

        private void OnDestroy()
        {
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            if (listenersInstalled)
            {
                Canvas.willRenderCanvases -= ProcessDirtyUi;
                SceneManager.sceneLoaded -= SceneLoaded;
            }
            if (harmony != null) harmony.UnpatchSelf();
            entries.Clear();
            if (instance == this) instance = null;
        }
    }
}
