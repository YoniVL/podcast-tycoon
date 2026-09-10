using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    /// <summary>Tiny builders so the screen code stays readable.</summary>
    public static class Ui
    {
        public static VisualElement Box(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static VisualElement Row(params string[] classes)
        {
            var e = Box("row");
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) l.AddToClassList(c);
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
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }

        public static VisualElement Stat(string label, string value, string valueClass = null)
        {
            var box = Box("stat");
            box.Add(Text(label.ToUpperInvariant(), "stat-label"));
            var v = Text(value, "stat-value");
            if (valueClass != null) v.AddToClassList(valueClass);
            box.Add(v);
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
