using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;

namespace PodcastTycoon.Game
{
    public enum StatIcon { Money, Listeners, Loyalty, Followers, Reputation, Credibility, SocialReach, Freshness, Morale }

    /// <summary>Fixed (non-procedural) UI icons, loaded from Assets/Resources/Icons/:
    /// <c>stat_{name}.png</c> for the resource strip and <c>crew_{role}.png</c> for crew roles
    /// (names are the lower-cased enum names — stat_socialreach, crew_producer ...).
    /// A missing file returns null and the caller shows text instead — nothing is generated.</summary>
    public static class IconArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D Load(string name)
        {
            if (Cache.TryGetValue(name, out var tex)) return tex;
            tex = Resources.Load<Texture2D>($"Icons/{name}");
            Cache[name] = tex;
            return tex;
        }

        public static string StatFile(StatIcon icon) => "stat_" + icon.ToString().ToLowerInvariant();
        public static string CrewFile(Crew role) => "crew_" + role.ToString().ToLowerInvariant();

        public static Texture2D Stat(StatIcon icon) => Load(StatFile(icon));
        public static Texture2D CrewRole(Crew role) => Load(CrewFile(role));

        /// <summary>Text shown in place of a stat icon that has no image yet.</summary>
        public static string StatLabel(StatIcon icon)
        {
            switch (icon)
            {
                case StatIcon.SocialReach: return "Social";
                case StatIcon.Credibility: return "Cred";
                default: return icon.ToString();
            }
        }
    }
}
