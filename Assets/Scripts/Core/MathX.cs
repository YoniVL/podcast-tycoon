using System;

namespace PodcastTycoon.Core
{
    /// <summary>Small math helpers (Core has no reference to UnityEngine.Mathf).</summary>
    public static class MathX
    {
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int ClampInt(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
