using System.Collections.Generic;
using UnityEngine;

namespace PodcastTycoon.Game
{
    /// <summary>Baked (not procedural) studio-background art — imported PNGs from the Claude
    /// Design handoff, one opaque room shell per Studio-space tier plus four independent
    /// transparent overlay tracks (Set, Audio, Post, Distribution), each 0-4 tiers, all
    /// 1024x576 on the same camera framing so any combination composites cleanly. Loaded from
    /// Assets/Resources/Studio/ — Resources.Load already caches internally, this just avoids
    /// repeated path-string allocation and a failed-load retrying every rebuild.</summary>
    public static class StudioArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly HashSet<string> Missing = new HashSet<string>();

        public static Texture2D Get(string prefix, int tier)
        {
            string key = $"{prefix}_{Mathf.Clamp(tier, 0, 4)}";
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            if (Missing.Contains(key)) return null;
            tex = Resources.Load<Texture2D>($"Studio/{key}");
            if (tex != null) Cache[key] = tex;
            else Missing.Add(key);
            return tex;
        }
    }
}
