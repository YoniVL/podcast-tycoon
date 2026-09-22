using System;
using System.Collections.Generic;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>
    /// Card/contact illustrations — a straight C# port of the Claude Design "cardart.js"
    /// prototype. Same locked palette and dilated-outline approach as <see cref="PixelPortraits"/>,
    /// but on arbitrary grid sizes (cards are 32x32, contacts are 40x28) since PixelPortraits'
    /// primitives are hardcoded to its fixed 22x22 <c>Grid</c>.
    /// </summary>
    public static class CardArt
    {
        public const int CardW = 32, CardH = 32;
        public const int ContactW = 40, ContactH = 28;

        // Same locked palette as PixelPortraits (hex-for-hex identical), plus the UI's own
        // amber accent, which the prototype also draws with.
        public static readonly Color Outline = PixelPortraits.Outline;
        public static readonly Color Accent = PixelPortraits.Accent;
        public static readonly Color[] Skins = PixelPortraits.Skins;
        public static readonly Color[] Hairs = PixelPortraits.Hairs;
        public static readonly Color[] Outfits = PixelPortraits.Outfits;
        public static readonly Color UiAccent = Hex("#e7a544");

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

        // ---- grid primitives, generalized to an arbitrary W x H canvas ----
        static void Px(Color?[,] g, int w, int h, float xf, float yf, Color c)
        {
            int x = Mathf.RoundToInt(xf), y = Mathf.RoundToInt(yf);
            if (x >= 0 && x < w && y >= 0 && y < h) g[y, x] = c;
        }

        static void Rect(Color?[,] g, int w, int h, int x0, int y0, int rw, int rh, Color c)
        {
            for (int y = y0; y < y0 + rh; y++)
                for (int x = x0; x < x0 + rw; x++)
                    Px(g, w, h, x, y, c);
        }

        static void Circ(Color?[,] g, int w, int h, float cx, float cy, float r, Color c, float? yMax = null, float? yMin = null)
        {
            for (int y = 0; y < h; y++)
            {
                if (yMax.HasValue && y > yMax.Value) continue;
                if (yMin.HasValue && y < yMin.Value) continue;
                for (int x = 0; x < w; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Px(g, w, h, x, y, c);
            }
        }

        static void Trap(Color?[,] g, int w, int h, int yTop, int yBot, float wTop, float wBot, float cx, Color c)
        {
            for (int y = yTop; y <= yBot; y++)
            {
                float t = yBot == yTop ? 0 : (float)(y - yTop) / (yBot - yTop);
                float ww = wTop + (wBot - wTop) * t;
                int x0 = Mathf.RoundToInt(cx - ww / 2f), x1 = Mathf.RoundToInt(cx + ww / 2f);
                for (int x = x0; x <= x1; x++) Px(g, w, h, x, y, c);
            }
        }

        static void Line(Color?[,] g, int w, int h, float x0f, float y0f, float x1f, float y1f, Color c, int thickness = 1)
        {
            int x0 = Mathf.RoundToInt(x0f), y0 = Mathf.RoundToInt(y0f), x1 = Mathf.RoundToInt(x1f), y1 = Mathf.RoundToInt(y1f);
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x1 >= x0 ? 1 : -1, sy = y1 >= y0 ? 1 : -1;
            int x = x0, y = y0, err = dx - dy;
            float half = (thickness - 1) / 2f;
            while (true)
            {
                for (float ox = -half; ox <= half; ox++)
                    for (float oy = -half; oy <= half; oy++)
                        Px(g, w, h, x + ox, y + oy, c);
                if (x == x1 && y == y1) break;
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }

        static void Starburst(Color?[,] g, int w, int h, float cx, float cy, float rInner, float rOuter, int spikes, Color c, int thickness = 1)
        {
            for (int i = 0; i < spikes; i++)
            {
                float a = (float)i / spikes * Mathf.PI * 2f;
                Line(g, w, h, cx + Mathf.Cos(a) * rInner, cy + Mathf.Sin(a) * rInner,
                              cx + Mathf.Cos(a) * rOuter, cy + Mathf.Sin(a) * rOuter, c, thickness);
            }
        }

        static void MicCapsule(Color?[,] g, int w, int h, float mx, float topY, Color headColor, Color standColor)
        {
            Rect(g, w, h, Mathf.RoundToInt(mx - 3), Mathf.RoundToInt(topY), 7, 8, headColor);
            Circ(g, w, h, mx, topY, 3.5f, headColor, yMax: topY + 1);
            Rect(g, w, h, Mathf.RoundToInt(mx - 3), Mathf.RoundToInt(topY + 3), 7, 1, Shade(headColor, -35));
            Rect(g, w, h, Mathf.RoundToInt(mx - 3), Mathf.RoundToInt(topY + 5), 7, 1, Shade(headColor, -35));
            Rect(g, w, h, Mathf.RoundToInt(mx - 1), Mathf.RoundToInt(topY + 8), 2, 9, standColor);
            Rect(g, w, h, Mathf.RoundToInt(mx - 4), Mathf.RoundToInt(topY + 16), 8, 2, standColor);
        }

        static void DrawZ(Color?[,] g, int w, int h, float x, float y, float s, Color c)
        {
            Line(g, w, h, x, y, x + s, y, c);
            Line(g, w, h, x + s, y, x, y + s, c);
            Line(g, w, h, x, y + s, x + s, y + s, c);
        }

        // ---- outline dilate + texture build (generalized; PixelPortraits.BuildTexture is
        // fixed to its own 22x22 Grid, so this is a parallel general-purpose version) ----
        static bool[,] Dilate(bool[,] mask, int w, int h)
        {
            var outM = (bool[,])mask.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (mask[y, x]) continue;
                    if ((y > 0 && mask[y - 1, x]) || (y < h - 1 && mask[y + 1, x]) ||
                        (x > 0 && mask[y, x - 1]) || (x < w - 1 && mask[y, x + 1]))
                        outM[y, x] = true;
                }
            return outM;
        }

        public static Texture2D BuildTexture(Color?[,] grid, int w, int h, Color outlineColor, int thickness = 1)
        {
            var filled = new bool[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    filled[y, x] = grid[y, x].HasValue;

            var mask = filled;
            for (int t = 0; t < thickness; t++) mask = Dilate(mask, w, h);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = filled[y, x] ? grid[y, x].Value : mask[y, x] ? outlineColor : new Color(0, 0, 0, 0);
                    // Texture2D row 0 is the bottom row; our grid row 0 is the top of the art.
                    pixels[(h - 1 - y) * w + x] = c;
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        // ================= CARD ART (32x32) =================

        public static Color?[,] CardViralMoment()
        {
            var g = new Color?[CardH, CardW];
            Color screen = Shade(Outfits[3], -50), body = Shade(Outfits[0], -8);
            Starburst(g, CardW, CardH, 16, 14, 6, 15, 8, UiAccent, 2);
            Starburst(g, CardW, CardH, 16, 14, 4, 10, 8, Accent, 1);
            Rect(g, CardW, CardH, 11, 7, 10, 20, body);
            Rect(g, CardW, CardH, 12, 9, 8, 15, screen);
            Rect(g, CardW, CardH, 14, 24, 4, 1, Shade(body, 30));
            Starburst(g, CardW, CardH, 16, 16, 2, 5, 6, UiAccent, 1);
            Circ(g, CardW, CardH, 16, 16, 1.4f, Accent);
            return g;
        }

        public static Color?[,] CardExclusiveScoop()
        {
            var g = new Color?[CardH, CardW];
            Color paper = Shade(Skins[0], 34), lineC = Shade(paper, -45);
            Rect(g, CardW, CardH, 8, 6, 16, 21, paper);
            for (int i = 0; i < 5; i++) Rect(g, CardW, CardH, 11, 10 + i * 3, 10, 1, lineC);
            // The prototype "fills" this circle with a transparent color rather than skipping
            // it — that still marks the cells filled (so the outline dilation draws a ring
            // around the rim), but paints nothing, leaving a see-through lens. The solid disc
            // drawn next overwrites the center, leaving just the rim ring visible.
            Circ(g, CardW, CardH, 21, 12, 6, new Color(0, 0, 0, 0));
            Circ(g, CardW, CardH, 21, 12, 4.4f, Shade(Outfits[5], 55));
            Line(g, CardW, CardH, 25, 16, 29, 20, Outfits[5], 2);
            Starburst(g, CardW, CardH, 12, 22, 2, 5, 5, Accent, 1);
            return g;
        }

        public static Color?[,] CardGhostwriter()
        {
            var g = new Color?[CardH, CardW];
            Color paper = Shade(Skins[0], 34), ghost = Shade(Outfits[3], 60);
            Trap(g, CardW, CardH, 8, 22, 12, 16, 22, ghost);
            Circ(g, CardW, CardH, 22, 9, 6, ghost, 12, 4);
            for (int i = 0; i < 4; i++) Circ(g, CardW, CardH, 16 + i * 2.6f, 22, 1.6f, ghost);
            Circ(g, CardW, CardH, 20, 9, 1.1f, Outline); Circ(g, CardW, CardH, 24, 9, 1.1f, Outline);
            Rect(g, CardW, CardH, 6, 18, 12, 10, paper);
            for (int i = 0; i < 3; i++) Rect(g, CardW, CardH, 8, 21 + i * 2, 8, 1, Shade(paper, -40));
            Line(g, CardW, CardH, 9, 20, 16, 8, Hairs[3], 2);
            Trap(g, CardW, CardH, 5, 9, 3, 6, 16.5f, Hairs[3]);
            Circ(g, CardW, CardH, 16.5f, 5, 1.6f, Accent);
            return g;
        }

        public static Color?[,] CardAllNighter()
        {
            var g = new Color?[CardH, CardW];
            Circ(g, CardW, CardH, 22, 8, 6, Shade(UiAccent, 35));
            foreach (var (sx, sy) in new[] { (8, 5), (13, 10), (27, 15) }) Rect(g, CardW, CardH, sx, sy, 2, 2, UiAccent);
            Rect(g, CardW, CardH, 10, 16, 12, 12, Shade(Outfits[0], 20));
            Rect(g, CardW, CardH, 10, 16, 12, 2, Shade(Outfits[0], -10));
            Rect(g, CardW, CardH, 12, 18, 8, 8, Shade(Accent, 10));
            Rect(g, CardW, CardH, 22, 19, 4, 1, Shade(Outfits[0], -10));
            Rect(g, CardW, CardH, 25, 19, 2, 5, Shade(Outfits[0], -10));
            Rect(g, CardW, CardH, 22, 23, 4, 1, Shade(Outfits[0], -10));
            Line(g, CardW, CardH, 13, 15, 12, 11, Shade(UiAccent, 50), 1);
            Line(g, CardW, CardH, 17, 15, 18, 11, Shade(UiAccent, 50), 1);
            return g;
        }

        public static Color?[,] CardDamageControl()
        {
            var g = new Color?[CardH, CardW];
            Trap(g, CardW, CardH, 4, 14, 3, 10, 16, Shade(Accent, -10));
            Trap(g, CardW, CardH, 7, 14, 2, 6, 16, Shade(UiAccent, 20));
            Trap(g, CardW, CardH, 10, 20, 18, 18, 16, Outfits[5]);
            Trap(g, CardW, CardH, 20, 27, 18, 2, 16, Outfits[5]);
            Trap(g, CardW, CardH, 11, 19, 14, 14, 16, Shade(Outfits[5], 25));
            Line(g, CardW, CardH, 11, 18, 15, 22, UiAccent, 2);
            Line(g, CardW, CardH, 15, 22, 21, 13, UiAccent, 2);
            return g;
        }

        public static Color?[,] CardClipFarm()
        {
            var g = new Color?[CardH, CardW];
            Color filmC = Shade(Outfits[3], -10);
            for (int i = 0; i < 3; i++)
            {
                Rect(g, CardW, CardH, 5 + i * 3, 7 + i * 2, 16, 14, filmC);
                for (int h = 0; h < 4; h++)
                {
                    Rect(g, CardW, CardH, 6 + i * 3, 9 + i * 2 + h * 3, 2, 2, Outline);
                    Rect(g, CardW, CardH, 17 + i * 3, 9 + i * 2 + h * 3, 2, 2, Outline);
                }
            }
            Line(g, CardW, CardH, 24, 8, 14, 24, Outfits[5], 2);
            Line(g, CardW, CardH, 28, 10, 16, 22, Outfits[5], 2);
            Circ(g, CardW, CardH, 24, 8, 2, Accent); Circ(g, CardW, CardH, 28, 10, 2, Accent);
            return g;
        }

        public static Color?[,] CardSureThing()
        {
            var g = new Color?[CardH, CardW];
            Circ(g, CardW, CardH, 16, 16, 12, Accent);
            Circ(g, CardW, CardH, 16, 16, 9, Shade(Outfits[0], 55));
            Circ(g, CardW, CardH, 16, 16, 6, Accent);
            Circ(g, CardW, CardH, 16, 16, 3, Shade(Outfits[0], 55));
            Circ(g, CardW, CardH, 16, 16, 1.4f, Outline);
            Line(g, CardW, CardH, 3, 29, 15, 17, Hairs[3], 2);
            Circ(g, CardW, CardH, 15, 17, 1.6f, Outline);
            return g;
        }

        public static Color?[,] CardTopProspect()
        {
            var g = new Color?[CardH, CardW];
            Color skin = Skins[3], hair = Hairs[0], kit = Outfits[2];
            Starburst(g, CardW, CardH, 16, 6, 2, 6, 6, UiAccent, 1);
            Circ(g, CardW, CardH, 16, 6, 1.6f, Accent);
            Trap(g, CardW, CardH, 18, 27, 12, 16, 16, kit);
            Circ(g, CardW, CardH, 16, 13, 6, skin);
            Rect(g, CardW, CardH, 12, 10, 8, 3, hair);
            Circ(g, CardW, CardH, 24, 27, 3, Shade(Outfits[3], 30));
            return g;
        }

        public static Color?[,] CardGeniusAppointment()
        {
            var g = new Color?[CardH, CardW];
            Color skin = Skins[1], hair = Hairs[3], suit = Outfits[0];
            Starburst(g, CardW, CardH, 24, 7, 1, 4, 5, UiAccent, 1);
            Circ(g, CardW, CardH, 24, 8, 4, Shade(UiAccent, 20));
            Rect(g, CardW, CardH, 22, 11, 4, 2, Shade(Outfits[3], -10));
            Trap(g, CardW, CardH, 20, 28, 14, 18, 14, suit);
            Circ(g, CardW, CardH, 14, 15, 6, skin);
            Rect(g, CardW, CardH, 9, 11, 10, 3, hair);
            Rect(g, CardW, CardH, 10, 15, 4, 3, Outline); Rect(g, CardW, CardH, 15, 15, 4, 3, Outline);
            Rect(g, CardW, CardH, 14, 16, 1, 1, Outline);
            return g;
        }

        public static Color?[,] CardTakeoverTalks()
        {
            var g = new Color?[CardH, CardW];
            Color caseC = Outfits[5];
            Rect(g, CardW, CardH, 7, 15, 18, 12, caseC);
            Rect(g, CardW, CardH, 12, 12, 8, 4, Shade(caseC, -15));
            Rect(g, CardW, CardH, 7, 15, 18, 2, Shade(caseC, 20));
            Rect(g, CardW, CardH, 12, 19, 8, 6, Shade(UiAccent, 10));
            Circ(g, CardW, CardH, 16, 22, 2, Accent);
            Line(g, CardW, CardH, 4, 26, 12, 20, Skins[0], 2);
            Line(g, CardW, CardH, 28, 26, 20, 20, Skins[2], 2);
            return g;
        }

        public static Color?[,] CardEvergreenSegment()
        {
            var g = new Color?[CardH, CardW];
            Color leaf = Outfits[2], leafD = Shade(leaf, -20), pot = Outfits[1];
            Trap(g, CardW, CardH, 6, 14, 4, 16, 16, leaf);
            Trap(g, CardW, CardH, 12, 20, 4, 20, 16, leafD);
            Rect(g, CardW, CardH, 14, 20, 4, 4, Shade(pot, -20));
            Trap(g, CardW, CardH, 22, 28, 10, 16, 16, pot);
            for (int a = 0; a < 300; a += 8)
            {
                float rad = a * Mathf.Deg2Rad;
                Px(g, CardW, CardH, 16 + Mathf.Cos(rad) * 13, 14 + Mathf.Sin(rad) * 13, UiAccent);
            }
            return g;
        }

        public static Color?[,] CardAllIn()
        {
            var g = new Color?[CardH, CardW];
            foreach (var (x, y, c) in new[] { (16, 24, Outfits[4]), (16, 20, UiAccent), (16, 16, Accent) })
            {
                Circ(g, CardW, CardH, x, y, 7, c);
                Circ(g, CardW, CardH, x, y, 5, Shade(c, 20));
            }
            Rect(g, CardW, CardH, 6, 6, 9, 9, Shade(Skins[0], 40));
            foreach (var (dx, dy) in new[] { (8f, 8f), (11f, 8f), (8f, 11f), (11f, 11f), (9.5f, 9.5f) })
                Circ(g, CardW, CardH, dx, dy, 0.8f, Outline);
            return g;
        }

        public static Color?[,] CardRestTeam()
        {
            var g = new Color?[CardH, CardW];
            Color skin = Skins[0], blanket = Outfits[4];
            Trap(g, CardW, CardH, 20, 26, 26, 22, 16, blanket);
            Circ(g, CardW, CardH, 8, 20, 5, skin);
            Rect(g, CardW, CardH, 4, 17, 8, 3, Hairs[1]);
            DrawZ(g, CardW, CardH, 21, 5, 6, UiAccent);
            DrawZ(g, CardW, CardH, 24, 12, 4, UiAccent);
            return g;
        }

        public static Color?[,] CardTrailer()
        {
            var g = new Color?[CardH, CardW];
            Color board = Outfits[3];
            Rect(g, CardW, CardH, 6, 14, 20, 14, board);
            Rect(g, CardW, CardH, 6, 8, 20, 5, Shade(board, 20));
            for (int i = 0; i < 5; i++) Rect(g, CardW, CardH, 8 + i * 4, 9, 3, 3, i % 2 == 1 ? Outline : Shade(UiAccent, 10));
            Trap(g, CardW, CardH, 17, 25, 0, 10, 17, Accent);
            return g;
        }

        public static Color?[,] CardHotMic()
        {
            var g = new Color?[CardH, CardW];
            Trap(g, CardW, CardH, 16, 28, 10, 4, 16, Accent);
            Trap(g, CardW, CardH, 19, 28, 5, 2, 16, Shade(UiAccent, 20));
            foreach (int side in new[] { -1, 1 })
                foreach (int r in new[] { 9, 12 })
                    for (int a = -50; a <= 50; a += 12)
                    {
                        float rad = a * Mathf.Deg2Rad;
                        Px(g, CardW, CardH, 16 + side * (r * Mathf.Cos(rad)), 6 + r * Mathf.Sin(rad), UiAccent);
                    }
            MicCapsule(g, CardW, CardH, 16, 4, new Color32(0xc9, 0xc9, 0xc9, 255), new Color32(0x2B, 0x2B, 0x2E, 255));
            return g;
        }

        // ================= CONTACT BUST ART (40x28) =================

        static (float cx, Color dark) BustBase(Color?[,] g, Color skin, Color hairColor, Color outfit, Action<Color?[,], float, Color, Color> hairStyleFn)
        {
            Color dark = Shade(skin, -55);
            const float cx = 20;
            Trap(g, ContactW, ContactH, 20, 27, 22, 34, cx, outfit);
            Circ(g, ContactW, ContactH, cx, 11, 11, skin);
            hairStyleFn(g, cx, skin, hairColor);
            Rect(g, ContactW, ContactH, Mathf.RoundToInt(cx - 6), 10, 3, 2, dark);
            Rect(g, ContactW, ContactH, Mathf.RoundToInt(cx + 3), 10, 3, 2, dark);
            return (cx, dark);
        }

        public static Color?[,] ContactClubInsider()
        {
            var g = new Color?[ContactH, ContactW];
            Color skin = Skins[1], outfit = Outfits[5];
            BustBase(g, skin, Hairs[1], outfit, (gr, cx, sk, hair) =>
            {
                Circ(gr, ContactW, ContactH, cx, 8, 11.2f, hair, 6);
                Circ(gr, ContactW, ContactH, cx - 12, 12, 3.2f, hair);
            });
            Rect(g, ContactW, ContactH, 9, 22, 22, 3, Accent);
            Rect(g, ContactW, ContactH, 18, 20, 2, 10, Shade(Outfits[1], -20));
            Rect(g, ContactW, ContactH, 15, 26, 8, 6, Shade(Skins[0], 40));
            Rect(g, ContactW, ContactH, 17, 28, 4, 3, UiAccent);
            Circ(g, ContactW, ContactH, 30, 9, 1.3f, Outline);
            return g;
        }

        public static Color?[,] ContactVeteranPundit()
        {
            var g = new Color?[ContactH, ContactW];
            Color skin = Skins[2], outfit = Outfits[0];
            BustBase(g, skin, Hairs[4], outfit, (gr, cx, sk, hair) =>
            {
                Circ(gr, ContactW, ContactH, cx, 7, 10.6f, hair, 5);
                Rect(gr, ContactW, ContactH, Mathf.RoundToInt(cx - 10), 9, 4, 6, hair);
                Rect(gr, ContactW, ContactH, Mathf.RoundToInt(cx + 6), 9, 4, 6, hair);
            });
            Rect(g, ContactW, ContactH, 11, 14, 3, 1, Shade(skin, -30)); Rect(g, ContactW, ContactH, 26, 14, 3, 1, Shade(skin, -30));
            Rect(g, ContactW, ContactH, 8, 3, 24, 2, Outfits[3]); Circ(g, ContactW, ContactH, 8, 11, 3, Outfits[3]); Circ(g, ContactW, ContactH, 32, 11, 3, Outfits[3]);
            Rect(g, ContactW, ContactH, 17, 22, 6, 8, Shade(Outfits[3], -15));
            Circ(g, ContactW, ContactH, 20, 22, 4, Shade(Outfits[3], 10));
            Circ(g, ContactW, ContactH, 20, 22, 2.2f, Outline);
            return g;
        }

        public static Color?[,] ContactViralEditor()
        {
            var g = new Color?[ContactH, ContactW];
            Color skin = Skins[4], outfit = Outfits[4];
            BustBase(g, skin, Hairs[5], outfit, (gr, cx, sk, hair) =>
            {
                Rect(gr, ContactW, ContactH, Mathf.RoundToInt(cx - 9), 2, 18, 8, hair);
                Rect(gr, ContactW, ContactH, Mathf.RoundToInt(cx - 10), 7, 3, 5, hair);
                Rect(gr, ContactW, ContactH, Mathf.RoundToInt(cx + 7), 7, 3, 5, hair);
            });
            Rect(g, ContactW, ContactH, 8, 3, 24, 2, Outfits[3]); Circ(g, ContactW, ContactH, 8, 11, 3, Outfits[3]); Circ(g, ContactW, ContactH, 32, 11, 3, Outfits[3]);
            Rect(g, ContactW, ContactH, 27, 19, 8, 9, Shade(Outfits[3], -15));
            Rect(g, ContactW, ContactH, 28, 20, 6, 6, Shade(UiAccent, 20));
            Starburst(g, ContactW, ContactH, 31, 23, 1, 3, 5, Accent, 1);
            return g;
        }

        public static Color?[,] ContactTabloidContact()
        {
            var g = new Color?[ContactH, ContactW];
            Color skin = Skins[2], outfit = Outfits[1];
            BustBase(g, skin, Hairs[2], outfit, (gr, cx, sk, hair) => Trap(gr, ContactW, ContactH, 3, 8, 20, 26, cx, hair));
            Trap(g, ContactW, ContactH, 1, 3, 30, 34, 20, Hairs[0]);
            Rect(g, ContactW, ContactH, 12, 3, 16, 3, Shade(Hairs[0], -20));
            Rect(g, ContactW, ContactH, 6, 19, 10, 8, Outfits[3]);
            Circ(g, ContactW, ContactH, 11, 23, 3, Shade(Outfits[3], -20));
            Starburst(g, ContactW, ContactH, 6, 16, 1, 4, 6, UiAccent, 1);
            return g;
        }

        public static Color?[,] ContactFanGroupLiaison()
        {
            var g = new Color?[ContactH, ContactW];
            Color skin = Skins[0], outfit = Outfits[2];
            BustBase(g, skin, Hairs[1], outfit, (gr, cx, sk, hair) => Circ(gr, ContactW, ContactH, cx, 8, 11, hair, 5));
            Trap(g, ContactW, ContactH, 0, 6, 24, 30, 20, Accent);
            Rect(g, ContactW, ContactH, 5, 5, 30, 2, Shade(Accent, -20));
            Circ(g, ContactW, ContactH, 20, 1, 2, UiAccent);
            Rect(g, ContactW, ContactH, 30, 6, 1, 18, Hairs[0]);
            Rect(g, ContactW, ContactH, 31, 6, 9, 6, UiAccent);
            Rect(g, ContactW, ContactH, 31, 6, 9, 1, Shade(UiAccent, -20));
            return g;
        }
    }

    /// <summary>Card id / contact id -> generated illustration, cached by identity like the
    /// portrait/icon caches.</summary>
    public static class CardArtCache
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static readonly Dictionary<string, Func<Color?[,]>> CardGenerators = new Dictionary<string, Func<Color?[,]>>
        {
            ["exclusive_scoop"] = CardArt.CardExclusiveScoop,
            ["viral_moment"] = CardArt.CardViralMoment,
            ["all_nighter"] = CardArt.CardAllNighter,
            ["ghostwriter"] = CardArt.CardGhostwriter,
            ["damage_control"] = CardArt.CardDamageControl,
            ["clip_farm"] = CardArt.CardClipFarm,
            ["sure_thing"] = CardArt.CardSureThing,
            ["top_prospect"] = CardArt.CardTopProspect,
            ["genius_appointment"] = CardArt.CardGeniusAppointment,
            ["takeover_talks"] = CardArt.CardTakeoverTalks,
            ["evergreen_segment"] = CardArt.CardEvergreenSegment,
            ["all_in"] = CardArt.CardAllIn,
            ["rest_the_team"] = CardArt.CardRestTeam,
            ["trailer"] = CardArt.CardTrailer,
            ["hot_mic"] = CardArt.CardHotMic,
        };

        static readonly Dictionary<string, Func<Color?[,]>> ContactGenerators = new Dictionary<string, Func<Color?[,]>>
        {
            ["club_insider"] = CardArt.ContactClubInsider,
            ["veteran_pundit"] = CardArt.ContactVeteranPundit,
            ["viral_editor"] = CardArt.ContactViralEditor,
            ["tabloid_contact"] = CardArt.ContactTabloidContact,
            ["fan_liaison"] = CardArt.ContactFanGroupLiaison,
        };

        public static Texture2D GetCard(string cardId)
        {
            string key = "card:" + cardId;
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            if (!CardGenerators.TryGetValue(cardId, out var gen)) return null;
            tex = CardArt.BuildTexture(gen(), CardArt.CardW, CardArt.CardH, CardArt.Outline);
            Cache[key] = tex;
            return tex;
        }

        public static Texture2D GetContact(string contactId)
        {
            string key = "contact:" + contactId;
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            if (!ContactGenerators.TryGetValue(contactId, out var gen)) return null;
            tex = CardArt.BuildTexture(gen(), CardArt.ContactW, CardArt.ContactH, CardArt.Outline);
            Cache[key] = tex;
            return tex;
        }
    }
}
