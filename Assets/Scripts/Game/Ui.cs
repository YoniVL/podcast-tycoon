using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    /// <summary>Tiny builders so the screen code stays readable.</summary>
    public static class Ui
    {
        static void AddClasses(VisualElement e, string[] classes)
        {
            if (classes == null) return;
            foreach (var c in classes)
                if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
        }

        public static VisualElement Box(params string[] classes)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            return e;
        }

        public static VisualElement Row(params string[] classes)
        {
            var e = Box("row");
            AddClasses(e, classes);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            AddClasses(l, classes);
            return l;
        }

        public static Label Wrapping(string text, params string[] classes)
        {
            var l = Text(text, classes);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.RemoveFromClassList("unity-button");
            b.AddToClassList("btn");
            AddClasses(b, classes);
            return b;
        }

        public static VisualElement Stat(string label, string value, string valueClass = null, string note = null)
        {
            var box = Box("stat");
            box.Add(Text(label.ToUpperInvariant(), "stat-label"));
            var v = Text(value, "stat-value");
            if (valueClass != null) v.AddToClassList(valueClass);
            box.Add(v);
            if (!string.IsNullOrEmpty(note))
            {
                var n = Text(note, "stat-note");
                n.style.whiteSpace = WhiteSpace.Normal;
                box.Add(n);
            }
            box.userData = v; // so callers can update it
            return box;
        }

        public static void SetStat(VisualElement stat, string value)
        {
            if (stat?.userData is Label l) l.text = value;
        }

        public static VisualElement Chip(string text)
        {
            var c = Text(text, "chip");
            return c;
        }

        public static VisualElement Divider()
        {
            return Box("divider");
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }

        public static string Money(float v)
        {
            string sign = v < 0 ? "-€" : "€";
            return sign + Mathf.Abs(v).ToString("N0");
        }

        public static string Signed(int v) => (v > 0 ? "+" : "") + v.ToString("N0");
        public static string Signed(float v, string fmt) => (v > 0 ? "+" : "") + v.ToString(fmt);
    }
}
