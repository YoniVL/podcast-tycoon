using System.Collections.Generic;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>Fixed character art — baked PNGs from the "cozy_chibi_booth" pack, loaded from
    /// Assets/Resources/Characters/: the host, the co-host and the logo. Crew are shown with
    /// role icons instead (see <see cref="IconArt"/>), not portraits.</summary>
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

    }
}
