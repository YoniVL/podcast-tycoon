using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;

namespace PodcastTycoon.Game
{
    public enum PortraitHairStyle { Short, Pigtails, Pony, Afro }
    public enum PortraitAccessory { None, Headphones, Cap, Scarf }

    public struct PortraitSpec
    {
        public Color Skin, Hair, Outfit, Accent;
        public PortraitHairStyle HairStyle;
        public PortraitAccessory Accessory;
    }

    /// <summary>
    /// Chibi/Cozy pixel portraits — a straight C# port of the Claude Design prototype
    /// (22x22 grid, auto-dilated 1px outline). Locked production palette from that handoff.
    /// </summary>
    public static class PixelPortraits
    {
        public const int Grid = 22;

        public static readonly Color Outline = Hex("#2b2118");
        public static readonly Color Accent = Hex("#e8763c");
        public static readonly Color[] Skins = { Hex("#E8B98A"), Hex("#C68A5B"), Hex("#8B5A3C"), Hex("#4A2E1F"), Hex("#D9A06B"), Hex("#6E4630") };
        public static readonly Color[] Hairs = { Hex("#3B2A20"), Hex("#C9A227"), Hex("#7A2E2E"), Hex("#2B2B2E"), Hex("#8A8A8A"), Hex("#B5602E") };
        public static readonly Color[] Outfits = { Hex("#4A4E5C"), Hex("#5C4A3A"), Hex("#3F4A3D"), Hex("#6B6F76"), Hex("#5C4A6B"), Hex("#3A4A5C") };
        static readonly Color Blush = new Color(0.910f, 0.541f, 0.541f);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Shade(Color c, float amt)
        {
            float f = amt / 100f;
            float Mix(float ch) => amt >= 0 ? ch + (1f - ch) * f : ch + ch * f;
            return new Color(Mix(c.r), Mix(c.g), Mix(c.b), c.a);
        }

        static void Circ(Color?[,] g, float cx, float cy, float r, Color color, int? yMax = null)
        {
            for (int y = 0; y < Grid; y++)
            {
                if (yMax.HasValue && y > yMax.Value) continue;
                for (int x = 0; x < Grid; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) g[y, x] = color;
            }
        }

        static void Rect(Color?[,] g, int x0, int y0, int w, int h, Color color)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (y >= 0 && y < Grid && x >= 0 && x < Grid) g[y, x] = color;
        }

        static void Trap(Color?[,] g, int yTop, int yBot, float wTop, float wBot, float cx, Color color)
        {
            for (int y = yTop; y <= yBot; y++)
            {
                float t = yBot == yTop ? 0 : (float)(y - yTop) / (yBot - yTop);
                float w = wTop + (wBot - wTop) * t;
                int x0 = Mathf.RoundToInt(cx - w / 2f), x1 = Mathf.RoundToInt(cx + w / 2f);
                for (int x = x0; x <= x1; x++)
                    if (y >= 0 && y < Grid && x >= 0 && x < Grid) g[y, x] = color;
            }
        }

        static void Dot(Color?[,] g, int x, int y, Color color)
        {
            if (y >= 0 && y < Grid && x >= 0 && x < Grid) g[y, x] = color;
        }

        public static Color?[,] Generate(PortraitSpec p)
        {
            var g = new Color?[Grid, Grid];
            var dark = Shade(p.Skin, -55);
            const float cx = 11;

            Trap(g, 18, 21, 8, 12, cx, p.Outfit);
            Rect(g, 9, 17, 4, 1, Shade(p.Outfit, -18));
            Circ(g, cx, 10, 8, p.Skin);

            switch (p.HairStyle)
            {
                case PortraitHairStyle.Pigtails:
                    Circ(g, cx, 9, 8.4f, p.Hair, 7); Circ(g, 3, 12, 2.6f, p.Hair); Circ(g, 19, 12, 2.6f, p.Hair);
                    break;
                case PortraitHairStyle.Pony:
                    Circ(g, cx, 9, 8.4f, p.Hair, 7); Circ(g, 18, 14, 2.3f, p.Hair);
                    break;
                case PortraitHairStyle.Afro:
                    Circ(g, cx, 8, 9, p.Hair); Circ(g, cx, 11, 6.4f, p.Skin);
                    break;
                default:
                    Circ(g, cx, 9, 8.4f, p.Hair, 7);
                    break;
            }

            Rect(g, 8, 9, 2, 1, Shade(p.Skin, -35)); Rect(g, 13, 9, 2, 1, Shade(p.Skin, -35));
            Rect(g, 8, 10, 2, 2, dark); Rect(g, 13, 10, 2, 2, dark);
            Dot(g, 8, 10, Color.white); Dot(g, 13, 10, Color.white);
            Dot(g, 6, 13, Blush); Dot(g, 16, 13, Blush);
            Dot(g, 11, 12, Shade(p.Skin, -18));
            Rect(g, 10, 14, 2, 1, dark);

            switch (p.Accessory)
            {
                case PortraitAccessory.Headphones:
                    Rect(g, 4, 4, 14, 1, p.Accent); Circ(g, 3, 10, 2, p.Accent); Circ(g, 19, 10, 2, p.Accent);
                    break;
                case PortraitAccessory.Cap:
                    Circ(g, cx, 7, 8.6f, p.Accent, 5); Rect(g, 3, 6, 7, 1, p.Accent);
                    break;
                case PortraitAccessory.Scarf:
                    Rect(g, 8, 16, 6, 2, p.Accent);
                    break;
            }
            return g;
        }

        static bool[,] Dilate(bool[,] mask)
        {
            var outM = (bool[,])mask.Clone();
            for (int y = 0; y < Grid; y++)
                for (int x = 0; x < Grid; x++)
                {
                    if (mask[y, x]) continue;
                    if ((y > 0 && mask[y - 1, x]) || (y < Grid - 1 && mask[y + 1, x]) ||
                        (x > 0 && mask[y, x - 1]) || (x < Grid - 1 && mask[y, x + 1]))
                        outM[y, x] = true;
                }
            return outM;
        }

        /// <summary>Rasterizes a grid to a point-filtered Texture2D, with a dilated outline under the fill.</summary>
        public static Texture2D BuildTexture(Color?[,] grid, Color outlineColor, int thickness = 1)
        {
            var filled = new bool[Grid, Grid];
            for (int y = 0; y < Grid; y++)
                for (int x = 0; x < Grid; x++)
                    filled[y, x] = grid[y, x].HasValue;

            var mask = filled;
            for (int t = 0; t < thickness; t++) mask = Dilate(mask);

            var pixels = new Color32[Grid * Grid];
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    Color c = filled[y, x] ? grid[y, x].Value : mask[y, x] ? outlineColor : new Color(0, 0, 0, 0);
                    // Texture2D row 0 is the bottom row; our grid row 0 is the top of the portrait.
                    pixels[(Grid - 1 - y) * Grid + x] = c;
                }
            }

            var tex = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }

    /// <summary>Maps game data (host / co-host / crew candidates) to a deterministic portrait look.</summary>
    public static class PortraitCasting
    {
        static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (var c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        public static PortraitSpec ForHost() => new PortraitSpec
        {
            Skin = PixelPortraits.Skins[0], Hair = PixelPortraits.Hairs[0], Outfit = PixelPortraits.Outfits[0],
            Accent = PixelPortraits.Accent, HairStyle = PortraitHairStyle.Short, Accessory = PortraitAccessory.Headphones,
        };

        public static PortraitSpec ForCoHost() => new PortraitSpec
        {
            Skin = PixelPortraits.Skins[4], Hair = PixelPortraits.Hairs[1], Outfit = PixelPortraits.Outfits[4],
            Accent = PixelPortraits.Accent, HairStyle = PortraitHairStyle.Pony, Accessory = PortraitAccessory.Headphones,
        };

        static readonly PortraitHairStyle[] HairStyles =
            { PortraitHairStyle.Short, PortraitHairStyle.Pigtails, PortraitHairStyle.Pony, PortraitHairStyle.Afro };
        static readonly PortraitAccessory[] Accessories =
            { PortraitAccessory.None, PortraitAccessory.Cap, PortraitAccessory.Scarf, PortraitAccessory.Headphones };

        /// <summary>Stable per-identity look — same name+role always casts the same face.</summary>
        public static PortraitSpec ForCrew(Crew role, string name)
        {
            int h = Hash(role + "|" + name);
            return new PortraitSpec
            {
                Skin = PixelPortraits.Skins[h % PixelPortraits.Skins.Length],
                Hair = PixelPortraits.Hairs[(h / 7) % PixelPortraits.Hairs.Length],
                Outfit = PixelPortraits.Outfits[(h / 13) % PixelPortraits.Outfits.Length],
                Accent = PixelPortraits.Accent,
                HairStyle = HairStyles[(h / 17) % HairStyles.Length],
                Accessory = Accessories[(h / 23) % Accessories.Length],
            };
        }

        public static PortraitSpec ForCrew(CrewCandidate c) => ForCrew(c.Role, c.Name);
    }

    /// <summary>Generates portrait textures once per identity and reuses them. Prefers the
    /// fixed character art from <see cref="CharacterArt"/> where it exists, falling back to
    /// the procedural chibi generator only where no baked art has been supplied yet.</summary>
    public static class PortraitCache
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string key, PortraitSpec spec)
        {
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = PixelPortraits.BuildTexture(PixelPortraits.Generate(spec), PixelPortraits.Outline);
            Cache[key] = tex;
            return tex;
        }

        public static Texture2D Host() => CharacterArt.Host() ?? Get("host", PortraitCasting.ForHost());
        public static Texture2D CoHost() => CharacterArt.CoHost() ?? Get("cohost", PortraitCasting.ForCoHost());
        public static Texture2D Crew(Crew role, string name) =>
            CharacterArt.ForCrewRole(role) ?? Get($"crew|{role}|{name}", PortraitCasting.ForCrew(role, name));
    }
}
