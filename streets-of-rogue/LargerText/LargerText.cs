using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SoRLargerText
{
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> enabled, onlyUi;
        private ConfigEntry<float> multiplier, interval;
        private ConfigEntry<int> maximumSize;
        private float clock;
        private bool checkedTypes;
        private readonly List<Type> types = new List<Type>();
        private readonly Dictionary<int, Record> records = new Dictionary<int, Record>();

        private sealed class Record
        {
            public UnityEngine.Object Target;
            public float Original;
            public float LastApplied;
        }

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true, "Enable enlarged text.");
            multiplier = Config.Bind("General", "Multiplier", 1.35f,
                "Small-text size multiplier, 1.0 is vanilla; 1.2 to 1.5 is recommended.");
            maximumSize = Config.Bind("General", "MaxOriginalFontSize", 22,
                "Only enlarge fonts originally at or below this size.");
            onlyUi = Config.Bind("General", "OnlyUiNames", false,
                "When true, restrict scaling to text whose ancestor name suggests a game UI element.");
            interval = Config.Bind("Performance", "ScanSeconds", 1f,
                "Interval between text component scans, minimum 0.25 seconds.");
            Logger.LogInfo("Larger Text: started, waiting to locate text renderers");
        }

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock < Math.Max(0.25f, interval.Value)) return;
            clock = 0f;
            if (!checkedTypes) DiscoverTypes();
            foreach (var t in types)
            {
                try
                {
                    foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(t))
                    {
                        Component c = o as Component;
                        if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy) continue;
                        if (onlyUi.Value && !LooksLikeUi(c.transform)) continue;
                        Apply(c);
                    }
                }
                catch (Exception ex) { Logger.LogWarning("Scan " + t.Name + ": " + ex.Message); }
            }
        }

        private void DiscoverTypes()
        {
            checkedTypes = true;
            var names = new[] {"tk2dTextMesh", "UnityEngine.UI.Text", "TMPro.TMP_Text", "UnityEngine.TextMesh"};
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            foreach (string name in names)
            {
                var t = a.GetType(name, false);
                if (t != null && typeof(Component).IsAssignableFrom(t) && !types.Contains(t))
                {
                    types.Add(t);
                    Logger.LogInfo("Font renderer found: " + t.FullName + " (" + a.GetName().Name + ")");
                }
            }
            if (types.Count == 0) Logger.LogWarning("No supported text renderers loaded");
        }

        private void Apply(Component c)
        {
            var prop = c.GetType().GetProperty("fontSize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null || !prop.CanRead || !prop.CanWrite ||
                (prop.PropertyType != typeof(int) && prop.PropertyType != typeof(float))) return;
            float current;
            try { current = Convert.ToSingle(prop.GetValue(c, null)); }
            catch { return; }
            if (current <= 0 || float.IsNaN(current) || float.IsInfinity(current)) return;
            int id = c.GetInstanceID();
            Record r;
            if (!records.TryGetValue(id, out r) || r.Target != c)
            {
                r = new Record { Target = c, Original = current, LastApplied = current };
                records[id] = r;
            }
            else if (Math.Abs(current - r.LastApplied) > 0.01f) r.Original = current;
            float factor = enabled.Value ? Math.Max(1f, Math.Min(2.5f, multiplier.Value)) : 1f;
            if (r.Original > maximumSize.Value) factor = 1f;
            float wanted = r.Original * factor;
            if (prop.PropertyType == typeof(int)) wanted = (float)Math.Round(wanted);
            if (Math.Abs(current - wanted) > 0.01f)
            {
                try
                {
                    prop.SetValue(c, prop.PropertyType == typeof(int) ? (object)(int)wanted : wanted, null);
                }
                catch { return; }
            }
            r.LastApplied = wanted;
        }

        private static bool LooksLikeUi(Transform node)
        {
            for (int n = 0; node != null && n < 8; n++, node = node.parent)
            {
                var x = node.gameObject.name.ToLowerInvariant();
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
