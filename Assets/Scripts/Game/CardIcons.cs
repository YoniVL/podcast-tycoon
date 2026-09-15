using System.Collections.Generic;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>Small thematic glyphs for cards, contacts, and interrupt events — not tied to
    /// one specific stat like <see cref="StatIcon"/>, but a broad category (money, risk, the
    /// team, breaking news...) so every card/contact/event reads as something at a glance
    /// instead of a plain box of text. Same 22x22 pixel-art system as the rest of the art.</summary>
    public enum CardIcon { Money, Social, Reputation, Credibility, Team, Risk, Prep, Story }

    public static class CardIcons
    {
        const int G = PixelPortraits.Grid;

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color?[,] Generate(CardIcon icon)
        {
            const float cx = 11, cy = 11;

            // The four resource-driven categories are literally that stat, so reuse the exact
            // same glyph rather than inventing a near-duplicate — one consistent icon per idea.
            switch (icon)
            {
                case CardIcon.Money: return PixelIcons.Generate(StatIcon.Money);
                case CardIcon.Social: return PixelIcons.Generate(StatIcon.SocialReach);
                case CardIcon.Reputation: return PixelIcons.Generate(StatIcon.Reputation);
                case CardIcon.Credibility: return PixelIcons.Generate(StatIcon.Credibility);
            }

            var g = new Color?[G, G];
            switch (icon)
            {
                case CardIcon.Team:
                    // A little shield/kit — squad-strength cards and morale-of-the-room cards.
                    PixelIcons.Rect(g, 4, 3, 14, 9, Hex("#5b8fd6"));
                    PixelIcons.Trap(g, 12, 19, 14, 0, cx, Hex("#5b8fd6"));
                    PixelIcons.Rect(g, 10, 7, 2, 4, Hex("#3d6bb0"));
                    break;

                case CardIcon.Risk:
                    // A die showing five — the gambles and guarantees.
                    PixelIcons.Rect(g, 3, 3, 16, 16, Hex("#c97a4a"));
                    PixelIcons.Rect(g, 6, 6, 2, 2, Hex("#2b2118"));
                    PixelIcons.Rect(g, 14, 6, 2, 2, Hex("#2b2118"));
                    PixelIcons.Rect(g, 10, 10, 2, 2, Hex("#2b2118"));
                    PixelIcons.Rect(g, 6, 14, 2, 2, Hex("#2b2118"));
                    PixelIcons.Rect(g, 14, 14, 2, 2, Hex("#2b2118"));
                    break;

                case CardIcon.Prep:
                    // A clock — extra prep points, permanent time savers.
                    PixelIcons.Circ(g, cx, cy, 8, Hex("#e0a45b"));
                    PixelIcons.Ring(g, cx, cy, 8, 6.5f, Hex("#a8763a"));
                    PixelIcons.Rect(g, 10, 5, 2, 6, Hex("#5c3d1f"));
                    PixelIcons.Rect(g, 11, 10, 5, 2, Hex("#5c3d1f"));
                    break;

                case CardIcon.Story:
                default:
                    // A breaking-news badge — scoops, buyout offers, "something's come up".
                    PixelIcons.Circ(g, cx, cy, 8, Hex("#d6785b"));
                    PixelIcons.Rect(g, 10, 5, 2, 9, Hex("#3a1f16"));
                    PixelIcons.Rect(g, 10, 16, 2, 2, Hex("#3a1f16"));
                    break;
            }
            return g;
        }
    }

    public static class CardIconCache
    {
        static readonly Dictionary<CardIcon, Texture2D> Cache = new Dictionary<CardIcon, Texture2D>();

        public static Texture2D Get(CardIcon icon)
        {
            if (Cache.TryGetValue(icon, out var tex) && tex != null) return tex;
            tex = PixelPortraits.BuildTexture(CardIcons.Generate(icon), PixelPortraits.Outline);
            Cache[icon] = tex;
            return tex;
        }

        /// <summary>Core-layer cards/contacts carry their icon as a plain string (Core can't
        /// reference the Game-layer enum) — parse it here, falling back to Story if unset.</summary>
        public static Texture2D Get(string category) =>
            System.Enum.TryParse<CardIcon>(category, out var icon) ? Get(icon) : Get(CardIcon.Story);
    }
}
