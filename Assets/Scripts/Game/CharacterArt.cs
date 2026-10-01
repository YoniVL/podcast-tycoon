using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>Fixed (non-procedural) character art — baked PNGs from the "cozy_chibi_booth"
    /// reference pack, loaded from Assets/Resources/Characters/. Unlike the old
    /// <see cref="PixelPortraits"/> system, every candidate/employee in a crew role shares that
    /// role's one portrait (confirmed with the user — per-candidate variety isn't needed).
    /// Only Producer and Clips have art so far; Researcher and Booker fall back to the old
    /// procedural system via <see cref="PortraitCache"/> until two more pieces are commissioned
    /// in the same style.</summary>
    public static class CharacterArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D Load(string name)
        {
            if (Cache.TryGetValue(name, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>($"Characters/{name}");
            if (tex != null) Cache[name] = tex;
            return tex;
        }

        public static Texture2D Host() => Load("host");
        public static Texture2D CoHost() => Load("cohost");
        public static Texture2D Logo() => Load("logo");

        static readonly Dictionary<Crew, string> RolePortraits = new Dictionary<Crew, string>
        {
            [Crew.Producer] = "producer",
            [Crew.Clips] = "clips",
        };

        /// <summary>Null if this role has no fixed art yet — caller falls back to the old
        /// procedural portrait system for it (currently Researcher, Booker).</summary>
        public static Texture2D ForCrewRole(Crew role) =>
            RolePortraits.TryGetValue(role, out var name) ? Load(name) : null;
    }
}
