using System.Collections.Generic;
using UnityEngine;

namespace PodcastTycoon.Game
{
    public enum StatIcon { Money, Listeners, Loyalty, Followers, Reputation, Credibility, SocialReach, Freshness, Morale }

    /// <summary>
    /// Small symbolic glyphs for the resource strip and stat displays — same 22x22 pixel unit
    /// and outline treatment as <see cref="PixelPortraits"/>, so it reads as one art system.
    /// </summary>
    public static class PixelIcons
    {
        const int G = PixelPortraits.Grid;

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        internal static void Circ(Color?[,] g, float cx, float cy, float r, Color color, int? yMax = null)
        {
            for (int y = 0; y < G; y++)
            {
                if (yMax.HasValue && y > yMax.Value) continue;
                for (int x = 0; x < G; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) g[y, x] = color;
            }
        }

        internal static void Ring(Color?[,] g, float cx, float cy, float rOuter, float rInner, Color color, int? yMax = null)
        {
            for (int y = 0; y < G; y++)
            {
                if (yMax.HasValue && y > yMax.Value) continue;
                for (int x = 0; x < G; x++)
                {
                    float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    if (d2 <= rOuter * rOuter && d2 >= rInner * rInner) g[y, x] = color;
                }
            }
        }

        /// <summary>The overlap of two circles — a pointed leaf/eye shape.</summary>
        internal static void Lens(Color?[,] g, float cx, float cy, float spread, float r, Color color)
        {
            for (int y = 0; y < G; y++)
                for (int x = 0; x < G; x++)
                {
                    float d1 = (x - (cx - spread)) * (x - (cx - spread)) + (y - cy) * (y - cy);
                    float d2 = (x - (cx + spread)) * (x - (cx + spread)) + (y - cy) * (y - cy);
                    if (d1 <= r * r && d2 <= r * r) g[y, x] = color;
                }
        }

        internal static void Rect(Color?[,] g, int x0, int y0, int w, int h, Color color)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (y >= 0 && y < G && x >= 0 && x < G) g[y, x] = color;
        }

        internal static void Trap(Color?[,] g, int yTop, int yBot, float wTop, float wBot, float cx, Color color)
        {
            for (int y = yTop; y <= yBot; y++)
            {
                float t = yBot == yTop ? 0 : (float)(y - yTop) / (yBot - yTop);
                float w = wTop + (wBot - wTop) * t;
                int x0 = Mathf.RoundToInt(cx - w / 2f), x1 = Mathf.RoundToInt(cx + w / 2f);
                for (int x = x0; x <= x1; x++)
                    if (y >= 0 && y < G && x >= 0 && x < G) g[y, x] = color;
            }
        }

        static void Dot(Color?[,] g, int x, int y, Color color)
        {
            if (y >= 0 && y < G && x >= 0 && x < G) g[y, x] = color;
        }

        /// <summary>A 4-point sparkle/star: the union of a tall diamond and a wide diamond.</summary>
        static void Sparkle(Color?[,] g, float cx, float cy, float r, Color color)
        {
            for (int y = 0; y < G; y++)
                for (int x = 0; x < G; x++)
                {
                    float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                    bool tall = dx * 2.2f + dy <= r;
                    bool wide = dx + dy * 2.2f <= r;
                    if (tall || wide) g[y, x] = color;
                }
        }

        public static Color?[,] Generate(StatIcon icon)
        {
            var g = new Color?[G, G];
            const float cx = 11, cy = 11;

            switch (icon)
            {
                case StatIcon.Money:
                    Circ(g, cx, cy, 7.5f, Hex("#e8c34a"));
                    Ring(g, cx, cy, 7.5f, 6f, Hex("#c99a2e"));
                    Circ(g, cx, cy, 2.6f, Hex("#f2d98a"));
                    break;

                case StatIcon.Listeners:
                    Ring(g, cx, 9, 9, 6.8f, Hex("#5bb6a8"), yMax: 9);
                    Circ(g, 3, 13, 3.2f, Hex("#5bb6a8"));
                    Circ(g, 19, 13, 3.2f, Hex("#5bb6a8"));
                    Circ(g, 3, 13, 1.3f, Hex("#3d8377"));
                    Circ(g, 19, 13, 1.3f, Hex("#3d8377"));
                    break;

                case StatIcon.Loyalty:
                    Circ(g, cx - 3.2f, 8.5f, 4f, Hex("#e0687a"));
                    Circ(g, cx + 3.2f, 8.5f, 4f, Hex("#e0687a"));
                    Trap(g, 9, 18, 16, 0, cx, Hex("#e0687a"));
                    break;

                case StatIcon.Followers:
                    Circ(g, cx, 7, 3.2f, Hex("#5b8fd6"));
                    Trap(g, 10, 18, 6, 15, cx, Hex("#5b8fd6"));
                    Circ(g, 4.5f, 9, 2.2f, Hex("#3d6bb0"));
                    Trap(g, 11, 17, 4, 9, 4.5f, Hex("#3d6bb0"));
                    break;

                case StatIcon.Reputation:
                    Sparkle(g, cx, cy, 10.5f, Hex("#c9a227"));
                    Circ(g, cx, cy, 2.6f, Hex("#f2d98a"));
                    break;

                case StatIcon.Credibility:
                    Rect(g, 5, 3, 12, 8, Hex("#5c9e6b"));
                    Trap(g, 11, 18, 12, 0, cx, Hex("#5c9e6b"));
                    Rect(g, 8, 6, 6, 4, Hex("#8fc99c"));
                    break;

                case StatIcon.SocialReach:
                    Rect(g, 3, 4, 15, 11, Hex("#e0764f"));
                    Trap(g, 15, 19, 7, 1, 7, Hex("#e0764f"));
                    Circ(g, 18, 2, 1.4f, Hex("#e0764f"));
                    Circ(g, 20, 5, 1f, Hex("#e0764f"));
                    break;

                case StatIcon.Freshness:
                    // A fatter leaf than before — the original thin lens (spread 3.2, r 6) had
                    // far less fill than every other icon and read as a bare sliver at display
                    // size, not a shape. Tighter spread + bigger radius fills the same silhouette
                    // solidly.
                    Lens(g, cx, cy - 0.5f, 2f, 7.5f, Hex("#7bbf6a"));
                    Rect(g, 10, 17, 2, 4, Hex("#5c9448"));
                    break;

                case StatIcon.Morale:
                    // Bigger, higher-contrast facial features — the original 1-2px eyes/mouth
                    // vanished at chip size, leaving what read as a blank circle.
                    Circ(g, cx, cy, 8, Hex("#8fd6e0"));
                    Rect(g, 6, 8, 3, 3, Hex("#1c3138"));
                    Rect(g, 13, 8, 3, 3, Hex("#1c3138"));
                    Rect(g, 7, 14, 8, 2, Hex("#1c3138"));
                    break;
            }
            return g;
        }
    }

    public static class IconCache
    {
        static readonly Dictionary<StatIcon, Texture2D> Cache = new Dictionary<StatIcon, Texture2D>();

        public static Texture2D Get(StatIcon icon)
        {
            if (Cache.TryGetValue(icon, out var tex) && tex != null) return tex;
            tex = PixelPortraits.BuildTexture(PixelIcons.Generate(icon), PixelPortraits.Outline);
            Cache[icon] = tex;
            return tex;
        }
    }
}
