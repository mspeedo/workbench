using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SoRLargerText
{
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "0.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> enabled, onlyUi, diagnostic;
        private ConfigEntry<float> multiplier, interval, tk2dThreshold, tk2dReferencePercentile;
        private ConfigEntry<int> maxFontSize;

        private float clock;
        private bool foundTypes;
        private readonly List<Type> types = new List<Type>();
        private readonly Dictionary<int, FontRecord> fontRecords = new Dictionary<int, FontRecord>();
        private readonly Dictionary<int, Tk2dRecord> tkRecords = new Dictionary<int, Tk2dRecord>();

        private sealed class FontRecord
        {
            public Component Component;
            public float Original;
            public float Applied;
        }

        private sealed class Tk2dRecord
        {
            public Component Component;
            public Vector3 Original;
            public Vector3 Applied;
        }

        private sealed class Tk2dCandidate
        {
            public Component Component;
            public PropertyInfo Property;
            public Tk2dRecord Record;
            public float EffectiveSize;
        }

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true, "Enable small-font enlargement.");
            multiplier = Config.Bind("General", "Multiplier", 1.35f,
                "Enlargement factor for eligible smaller fonts (1.0 = normal).");
            maxFontSize = Config.Bind("General", "MaxOriginalFontSize", 18,
                "Unity UI/TMP/TextMesh: enlarge ONLY original fonts smaller than this size, capping the result at this size. Set below your normal menu font size.");
            tk2dThreshold = Config.Bind("General", "Tk2dSmallTextFraction", 0.80f,
                "2D Toolkit: enlarge ONLY text smaller than this fraction of the scene's reference text size (0.80 = 80%).");
            tk2dReferencePercentile = Config.Bind("General", "Tk2dReferencePercentile", 0.80f,
                "2D Toolkit: the active text-size percentile used as a normal-sized reference. Lower values protect more menu text.");
            onlyUi = Config.Bind("General", "OnlyUiNames", false,
                "Optional filter: only alter text with UI-like ancestor object names.");
            diagnostic = Config.Bind("Diagnostics", "LogFontSizes", false,
                "Log up to 20 candidate text sizes per scan (use temporarily for fine tuning).");
            interval = Config.Bind("Performance", "ScanSeconds", 1f,
                "Time between scans, at least 0.25 seconds.");
            Logger.LogInfo("Larger Text v0.3: selective small-font scaling enabled");
        }

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock < Math.Max(0.25f, interval.Value)) return;
            clock = 0f;
            if (!foundTypes) DiscoverTypes();

            var tkCandidates = new List<Tk2dCandidate>();
            foreach (Type type in types)
            {
                try
                {
                    foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(type))
                    {
                        var c = obj as Component;
                        if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
                            continue;
                        if (!c.gameObject.scene.IsValid()) continue;
                        if (onlyUi.Value && !LooksLikeUi(c.transform)) continue;

                        if (type.Name == "tk2dTextMesh")
                        {
                            CollectTk2d(c, tkCandidates);
                        }
                        else
                        {
                            AdjustFontSize(c);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Text scan (" + type.Name + "): " + ex.Message);
                }
            }

            AdjustTk2d(tkCandidates);

            // Remove references to destroyed Unity objects rather than retaining them indefinitely.
            var expiredFonts = new List<int>();
            foreach (var kv in fontRecords)
                if (kv.Value.Component == null) expiredFonts.Add(kv.Key);
            foreach (int id in expiredFonts) fontRecords.Remove(id);
            var expiredTk = new List<int>();
            foreach (var kv in tkRecords)
                if (kv.Value.Component == null) expiredTk.Add(kv.Key);
            foreach (int id in expiredTk) tkRecords.Remove(id);
        }

        private void DiscoverTypes()
        {
            foundTypes = true;
            string[] names = { "tk2dTextMesh", "UnityEngine.UI.Text", "TMPro.TMP_Text", "UnityEngine.TextMesh" };
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string name in names)
                {
                    var t = a.GetType(name, false);
                    if (t != null && typeof(Component).IsAssignableFrom(t) && !types.Contains(t))
                    {
                        types.Add(t);
                        Logger.LogInfo("Text renderer: " + t.FullName);
                    }
                }
            }
            if (types.Count == 0) Logger.LogWarning("No supported text renderers found");
        }

        private void AdjustFontSize(Component c)
        {
            var p = c.GetType().GetProperty("fontSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null || !p.CanRead || !p.CanWrite ||
                (p.PropertyType != typeof(int) && p.PropertyType != typeof(float))) return;
            float actual;
            try { actual = Convert.ToSingle(p.GetValue(c, null)); }
            catch { return; }
            if (!(actual > 0f) || float.IsInfinity(actual) || float.IsNaN(actual)) return;

            int id = c.GetInstanceID();
            FontRecord r;
            if (!fontRecords.TryGetValue(id, out r) || r.Component != c)
                fontRecords[id] = r = new FontRecord { Component = c, Original = actual, Applied = actual };
            else if (Math.Abs(actual - r.Applied) > 0.02f)
                r.Original = actual; // The game changed its own font size; learn the new baseline.

            float factor = Mathf.Clamp(multiplier.Value, 1f, 2.5f);
            int cutoff = Math.Max(1, maxFontSize.Value);
            // This strict less-than comparison protects text at the normal menu font size.
            float desired = enabled.Value && r.Original < cutoff
                ? Math.Min(r.Original * factor, cutoff)
                : r.Original;
            if (p.PropertyType == typeof(int)) desired = (float)Math.Round(desired);
            if (Math.Abs(actual - desired) > 0.02f)
            {
                try { p.SetValue(c, p.PropertyType == typeof(int) ? (object)(int)desired : desired, null); }
                catch { return; }
            }
            r.Applied = desired;
        }

        private void CollectTk2d(Component c, List<Tk2dCandidate> candidates)
        {
            // tk2dTextMesh exposes scale (Vector3), NOT fontSize. The old plugin
            // silently skipped it. Read the original text scale and the transform's
            // inherited scale to estimate its actual visual height.
            PropertyInfo p = c.GetType().GetProperty("scale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null || !p.CanRead || !p.CanWrite || p.PropertyType != typeof(Vector3)) return;
            Vector3 actual;
            try { actual = (Vector3)p.GetValue(c, null); }
            catch { return; }
            if (!(Math.Abs(actual.y) > 0.00001f)) return;

            int id = c.GetInstanceID();
            Tk2dRecord r;
            if (!tkRecords.TryGetValue(id, out r) || r.Component != c)
                tkRecords[id] = r = new Tk2dRecord { Component = c, Original = actual, Applied = actual };
            else if ((actual - r.Applied).sqrMagnitude > 0.000001f)
                r.Original = actual;

            float visualHeight = Math.Abs(r.Original.y * c.transform.lossyScale.y);
            if (visualHeight > 0.000001f && !float.IsInfinity(visualHeight) && !float.IsNaN(visualHeight))
                candidates.Add(new Tk2dCandidate { Component = c, Property = p, Record = r, EffectiveSize = visualHeight });
        }

        private void AdjustTk2d(List<Tk2dCandidate> candidates)
        {
            if (candidates.Count == 0) return;
            var heights = new List<float>(candidates.Count);
            foreach (var c in candidates) heights.Add(c.EffectiveSize);
            heights.Sort();
            int index = Math.Max(0, Math.Min(heights.Count - 1,
                (int)Math.Floor((heights.Count - 1) * Mathf.Clamp(tk2dReferencePercentile.Value, 0.5f, 0.95f))));
            float referenceSize = heights[index];
            float threshold = referenceSize * Mathf.Clamp(tk2dThreshold.Value, 0.2f, 0.99f);
            float factor = Mathf.Clamp(multiplier.Value, 1f, 2.5f);
            int logged = 0;

            foreach (Tk2dCandidate item in candidates)
            {
                // Text already at the normal-size reference is intentionally untouched.
                float appliedFactor = enabled.Value && item.EffectiveSize < threshold
                    ? Math.Min(factor, referenceSize / item.EffectiveSize)
                    : 1f;
                Vector3 original = item.Record.Original;
                Vector3 desired = new Vector3(original.x * appliedFactor,
                                               original.y * appliedFactor,
                                               original.z);
                Vector3 actual;
                try { actual = (Vector3)item.Property.GetValue(item.Component, null); }
                catch { continue; }
                if ((desired - actual).sqrMagnitude > 0.000001f)
                {
                    try { item.Property.SetValue(item.Component, desired, null); }
                    catch { continue; }
                }
                item.Record.Applied = desired;
                if (diagnostic.Value && logged++ < 20)
                    Logger.LogInfo("tk2d name=" + item.Component.name +
                        " originalEffective=" + item.EffectiveSize.ToString("F4") +
                        " reference=" + referenceSize.ToString("F4") +
                        " factor=" + appliedFactor.ToString("F2"));
            }
        }

        private static bool LooksLikeUi(Transform node)
        {
            for (int depth = 0; node != null && depth < 8; depth++, node = node.parent)
            {
                string x = node.gameObject.name.ToLowerInvariant();
                if (x.Contains("menu") || x.Contains("ui") || x.Contains("gui") ||
                    x.Contains("tool") || x.Contains("desc") || x.Contains("item") ||
                    x.Contains("inventory") || x.Contains("dialog") || x.Contains("quest") ||
                    x.Contains("hud") || x.Contains("popup") || x.Contains("trait") ||
                    x.Contains("canvas") || x.Contains("button")) return true;
            }
            return false;
        }
    }
}
