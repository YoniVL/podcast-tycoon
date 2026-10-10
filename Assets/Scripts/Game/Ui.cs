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

        /// <summary>A click-to-expand section — a one-line header (with an optional always-visible
        /// summary) that shows/hides the real content below it. Used to keep secondary or optional
        /// content out of the way without hiding it, so a screen can fit without scrolling.</summary>
        public static VisualElement Collapsible(string title, string summary, VisualElement content,
            bool startExpanded = false, Action<bool> onToggle = null)
        {
            var wrap = Box("collapsible");
            var header = Row("collapsible-header");
            var chevron = Text(startExpanded ? "▾" : "▸", "collapsible-chevron");
            var titleLabel = Text(title, "collapsible-title");
            titleLabel.style.flexGrow = 1;
            header.Add(chevron);
            header.Add(titleLabel);
            if (!string.IsNullOrEmpty(summary))
                header.Add(Text(summary, "collapsible-summary"));
            wrap.Add(header);

            content.style.display = startExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            wrap.Add(content);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = content.style.display == DisplayStyle.Flex;
                content.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                chevron.text = expanded ? "▸" : "▾";
                onToggle?.Invoke(!expanded);
            });
            return wrap;
        }

        /// <summary>A compact icon + value chip for the persistent resource strip — the full
        /// name and explanation live in the tooltip instead of always-on text, so the strip
        /// stays a single thin row (the glossary in Help still covers all of this in full).</summary>
        public static VisualElement StatChip(StatIcon icon, string value, string tooltip, string valueClass = null)
        {
            var chip = Row("statchip");
            chip.tooltip = tooltip;
            var tex = IconArt.Stat(icon);
            if (tex != null) chip.Add(Portrait(tex, 22));
            else chip.Add(Text(IconArt.StatLabel(icon), "statchip-label"));
            var v = Text(value, "statchip-value");
            if (valueClass != null) v.AddToClassList(valueClass);
            chip.Add(v);
            return chip;
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

        /// <summary>A fixed-size square image.</summary>
        public static VisualElement Portrait(Texture2D tex, float size, params string[] classes)
        {
            var e = Box("portrait");
            AddClasses(e, classes);
            e.style.width = size;
            e.style.height = size;
            e.style.flexShrink = 0;
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
        }

        /// <summary>Like <see cref="Portrait"/> but for non-square art where width and height
        /// aren't the same.</summary>
        public static VisualElement Art(Texture2D tex, float width, float height, params string[] classes)
        {
            var e = Box("portrait");
            AddClasses(e, classes);
            e.style.width = width;
            e.style.height = height;
            e.style.flexShrink = 0;
            e.style.backgroundImage = new StyleBackground(tex);
            return e;
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
