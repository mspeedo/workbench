using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SoRLargerText
{
    [BepInPlugin("mspeedo.sor.larger-text", "SoR Larger Text", "0.4.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> enabled, showOverlay, forceAll, includeTk2d, disableBestFit, logExamples;
        private ConfigEntry<float> multiplier, smallFraction, referencePercentile, scanSeconds;
        private readonly List<Type> textTypes = new List<Type>();
        private readonly Dictionary<int, Record> textRecords = new Dictionary<int, Record>();
        private readonly Dictionary<int, Record> tkRecords = new Dictionary<int, Record>();
        private float sinceScan, sinceLog, sincePrune;
        private GUIStyle overlayStyle;
        private int uiFound, uiChanged, tkFound, tkChanged;
        private float uiReference, tkReference;
        private string sample = "No text samples yet";

        private sealed class Record
        {
            public Component Component;
            public float OriginalFontSize;
            public float AppliedFontSize;
            public Vector3 OriginalScale;
            public Vector3 AppliedScale;
            public bool HasBestFit, OriginalBestFit;
        }

        private sealed class Candidate
        {
            public Component Component;
            public PropertyInfo SizeProperty;
            public Record Record;
            public float VisualSize;
        }

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true, "Enable selective font enlargement.");
            multiplier = Config.Bind("General", "Multiplier", 1.70f,
                "Font enlargement multiplier, 1.7 is intentionally noticeable for testing.");
            smallFraction = Config.Bind("General", "SmallTextFraction", 0.82f,
                "Only fonts whose original visual size is smaller than this fraction of the reference are enlarged. Lower = fewer affected.");
            referencePercentile = Config.Bind("General", "ReferencePercentile", 0.85f,
                "The percentile of visible text sizes used as the normal-text reference. Usually 0.8-0.9.");
            includeTk2d = Config.Bind("General", "IncludeTk2dText", true,
                "Include legacy 2D Toolkit labels.");
            disableBestFit = Config.Bind("General", "OverrideBestFitForSmallText", true,
                "Temporarily disable Unity UI Best Fit on enlarged text, otherwise its automatic sizing can hide the change.");
            showOverlay = Config.Bind("Diagnostics", "ShowOverlay", true,
                "Shows a diagnostic box in the upper-left corner. Turn off once tested.");
            forceAll = Config.Bind("Diagnostics", "ForceAllText", false,
                "TEST ONLY: enlarge every detected text component, including menu text. Useful to prove the plugin works. Switch off after testing.");
            logExamples = Config.Bind("Diagnostics", "LogSamples", true,
                "Print renderer counts, reference sizes, and examples to BepInEx/LogOutput.log every 10 seconds.");
            scanSeconds = Config.Bind("Performance", "ScanSeconds", 0.50f, "Text rescan interval (minimum 0.2 seconds).");
            Logger.LogWarning("Larger Text v0.4.0 LOADED: diagnostic overlay enabled; see configuration in mspeedo.sor.larger-text.cfg");
        }

        private void Update()
        {
            sinceScan += Time.unscaledDeltaTime;
            sinceLog += Time.unscaledDeltaTime;
            sincePrune += Time.unscaledDeltaTime;
            if (sinceScan < Mathf.Max(0.2f, scanSeconds.Value)) return;
            sinceScan = 0f;
            DiscoverNewTypes();

            var ui = new List<Candidate>();
            var tk = new List<Candidate>();
            foreach (Type t in textTypes)
            {
                try
                {
                    foreach (UnityEngine.Object item in Resources.FindObjectsOfTypeAll(t))
                    {
                        Component c = item as Component;
                        if (c == null || c.gameObject == null || !c.gameObject.activeInHierarchy)
                            continue;
                        Behaviour behaviour = c as Behaviour;
                        if (behaviour != null && !behaviour.enabled) continue;
                        if (!c.gameObject.scene.IsValid()) continue;
                        if (t.Name == "tk2dTextMesh")
                        {
                            if (includeTk2d.Value) CollectTk(c, tk);
                        }
                        else
                            CollectUi(c, ui);
                    }
                }
                catch (Exception e) { Logger.LogWarning("Could not scan " + t.FullName + ": " + e.Message); }
            }

            uiFound = ui.Count;
            tkFound = tk.Count;
            uiChanged = ApplyUi(ui);
            tkChanged = ApplyTk(tk);

            if (logExamples.Value && sinceLog >= 10f)
            {
                sinceLog = 0f;
                Logger.LogInfo("LargerText v0.4: UI " + uiChanged + "/" + uiFound +
                               " changed, reference size=" + uiReference.ToString("F2") +
                               " | tk2d " + tkChanged + "/" + tkFound +
                               " changed, reference size=" + tkReference.ToString("F3") +
                               " | ForceAllText=" + forceAll.Value + " | sample: " + sample);
            }

            if (sincePrune >= 60f)
            {
                sincePrune = 0f;
                Prune(textRecords);
                Prune(tkRecords);
            }
        }

        private void DiscoverNewTypes()
        {
            string[] names = { "UnityEngine.UI.Text", "UnityEngine.TextMesh", "TMPro.TMP_Text", "tk2dTextMesh" };
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            foreach (string n in names)
            {
                Type t = a.GetType(n, false);
                if (t != null && typeof(Component).IsAssignableFrom(t) && !textTypes.Contains(t))
                {
                    textTypes.Add(t);
                    Logger.LogInfo("LargerText renderer: " + t.FullName + ", assembly " + a.GetName().Name);
                }
            }
        }

        private void CollectUi(Component c, List<Candidate> items)
        {
            PropertyInfo prop = c.GetType().GetProperty("fontSize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null || !prop.CanRead || !prop.CanWrite ||
                (prop.PropertyType != typeof(int) && prop.PropertyType != typeof(float))) return;
            float current;
            try { current = Convert.ToSingle(prop.GetValue(c, null)); }
            catch { return; }
            if (current <= 0 || float.IsNaN(current) || float.IsInfinity(current)) return;

            int id = c.GetInstanceID();
            Record r;
            if (!textRecords.TryGetValue(id, out r) || r.Component != c)
            {
                r = new Record { Component = c, OriginalFontSize = current, AppliedFontSize = current };
                textRecords[id] = r;
                PropertyInfo fit = GetBestFitProperty(c);
                if (fit != null)
                {
                    try { r.HasBestFit = true; r.OriginalBestFit = (bool)fit.GetValue(c, null); }
                    catch { r.HasBestFit = false; }
                }
            }
            else if (Math.Abs(current - r.AppliedFontSize) > 0.05f)
            {
                r.OriginalFontSize = current;
                r.AppliedFontSize = current;
            }

            // The previous mod used a fixed fontSize <= 18 threshold. This
            // game's actual Unity Text fonts can be much larger but rendered
            // small because of parent Canvas/RectTransform scaling.
            float visual = Math.Abs(r.OriginalFontSize * c.transform.lossyScale.y);
            if (visual < 0.001f || float.IsNaN(visual) || float.IsInfinity(visual))
                visual = r.OriginalFontSize;
            items.Add(new Candidate { Component = c, SizeProperty = prop, Record = r, VisualSize = visual });
        }

        private void CollectTk(Component c, List<Candidate> items)
        {
            PropertyInfo prop = c.GetType().GetProperty("scale",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null || !prop.CanRead || !prop.CanWrite || prop.PropertyType != typeof(Vector3)) return;
            Vector3 current;
            try { current = (Vector3)prop.GetValue(c, null); }
            catch { return; }
            if (Math.Abs(current.y) < 0.00001f) return;
            int id = c.GetInstanceID();
            Record r;
            if (!tkRecords.TryGetValue(id, out r) || r.Component != c)
            {
                r = new Record { Component = c, OriginalScale = current, AppliedScale = current };
                tkRecords[id] = r;
            }
            else if ((current - r.AppliedScale).sqrMagnitude > 0.000001f)
                r.OriginalScale = current;

            float visual = Math.Abs(r.OriginalScale.y * c.transform.lossyScale.y);
            if (visual < 0.000001f || float.IsNaN(visual) || float.IsInfinity(visual)) return;
            items.Add(new Candidate { Component = c, SizeProperty = prop, Record = r, VisualSize = visual });
        }

        private static PropertyInfo GetBestFitProperty(Component c)
        {
            PropertyInfo p = c.GetType().GetProperty("resizeTextForBestFit", BindingFlags.Public | BindingFlags.Instance);
            return p != null && p.CanRead && p.CanWrite && p.PropertyType == typeof(bool) ? p : null;
        }

        private float Reference(List<Candidate> items)
        {
            if (items.Count == 0) return 0f;
            var sorted = new List<float>(items.Count);
            foreach (Candidate item in items) sorted.Add(item.VisualSize);
            sorted.Sort();
            int index = (int)Math.Floor((sorted.Count - 1) * Mathf.Clamp(referencePercentile.Value, 0.50f, 0.98f));
            return sorted[index];
        }

        private float ScaleFor(float size, float reference)
        {
            if (!enabled.Value) return 1f;
            float factor = Mathf.Clamp(multiplier.Value, 1.0f, 3.0f);
            if (forceAll.Value) return factor;
            if (reference <= 0f || size >= reference * Mathf.Clamp(smallFraction.Value, 0.10f, 0.99f))
                return 1f;
            // Never make small labels larger than their normal-sized peers.
            return Mathf.Min(factor, reference / size);
        }

        private int ApplyUi(List<Candidate> items)
        {
            float reference = uiReference = Reference(items);
            int changed = 0;
            Candidate example = null;
            foreach (Candidate item in items)
            {
                float factor = ScaleFor(item.VisualSize, reference);
                if (factor > 1.001f) { changed++; if (example == null) example = item; }
                float desired = item.Record.OriginalFontSize * factor;
                if (item.SizeProperty.PropertyType == typeof(int))
                    desired = (float)Math.Round(desired);

                try
                {
                    // Best Fit clamps the displayed size even when fontSize changes.
                    // For small labels, disable Best Fit to make scaling observable.
                    PropertyInfo bestFit = GetBestFitProperty(item.Component);
                    if (bestFit != null && item.Record.HasBestFit)
                    {
                        bool desiredBestFit = item.Record.OriginalBestFit &&
                            !(factor > 1.001f && disableBestFit.Value);
                        if ((bool)bestFit.GetValue(item.Component, null) != desiredBestFit)
                            bestFit.SetValue(item.Component, desiredBestFit, null);
                    }
                    float current = Convert.ToSingle(item.SizeProperty.GetValue(item.Component, null));
                    if (Math.Abs(current - desired) > 0.05f)
                        item.SizeProperty.SetValue(item.Component,
                            item.SizeProperty.PropertyType == typeof(int) ? (object)(int)desired : desired, null);
                    item.Record.AppliedFontSize = desired;
                }
                catch (Exception e) { Logger.LogDebug("Could not resize UI text: " + e.Message); }
            }
            if (example != null)
                sample = "UI name=" + example.Component.gameObject.name +
                         " baseFont=" + example.Record.OriginalFontSize.ToString("F1") +
                         " scaled=" + ScaleFor(example.VisualSize, reference).ToString("F2") + "x";
            else if (items.Count > 0)
                sample = "No small UI fonts under reference ratio; first font=" + items[0].Record.OriginalFontSize;
            return changed;
        }

        private int ApplyTk(List<Candidate> items)
        {
            float reference = tkReference = Reference(items);
            int changed = 0;
            foreach (Candidate item in items)
            {
                float factor = ScaleFor(item.VisualSize, reference);
                if (factor > 1.001f) changed++;
                Vector3 s = item.Record.OriginalScale;
                Vector3 desired = new Vector3(s.x * factor, s.y * factor, s.z);
                try
                {
                    Vector3 current = (Vector3)item.SizeProperty.GetValue(item.Component, null);
                    if ((current - desired).sqrMagnitude > 0.000001f)
                        item.SizeProperty.SetValue(item.Component, desired, null);
                    item.Record.AppliedScale = desired;
                }
                catch (Exception e) { Logger.LogDebug("Could not resize tk2d: " + e.Message); }
            }
            return changed;
        }

        private static void Prune(Dictionary<int, Record> dict)
        {
            var dead = new List<int>();
            foreach (var kv in dict) if (kv.Value.Component == null) dead.Add(kv.Key);
            foreach (int id in dead) dict.Remove(id);
        }

        private void OnGUI()
        {
            if (showOverlay == null || !showOverlay.Value) return;
            if (overlayStyle == null)
            {
                overlayStyle = new GUIStyle(GUI.skin.label);
                overlayStyle.fontSize = 19;
                overlayStyle.normal.textColor = Color.white;
                overlayStyle.wordWrap = true;
            }
            GUI.Box(new Rect(10, 10, 690, 111), "");
            string display = "Larger Text v0.4 LOADED | " +
                (enabled.Value ? "ON" : "OFF") + (forceAll.Value ? " | FORCE ALL" : "") +
                "\nUI text: " + uiChanged + "/" + uiFound + " enlarged   tk2d: " +
                tkChanged + "/" + tkFound + " enlarged" +
                "\n" + sample +
                "\nConfig: BepInEx/config/mspeedo.sor.larger-text.cfg";
            GUI.Label(new Rect(24, 13, 660, 106), display, overlayStyle);
        }
    }
}
