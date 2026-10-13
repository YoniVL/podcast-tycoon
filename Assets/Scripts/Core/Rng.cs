using System;

namespace PodcastTycoon.Core
{
    /// <summary>Injectable randomness so the simulation can be unit-tested deterministically.</summary>
    public interface IRng
    {
        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();

        /// <summary>Integer in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>An RNG that can be put back on a known sequence — the engine does this at the start
    /// of every week, so a saved game replays that week exactly instead of re-rolling it.</summary>
    public interface IReseedable
    {
        void Reseed(int seed);
    }

    public sealed class SystemRng : IRng, IReseedable
    {
        Random _r;

        public SystemRng() : this(Environment.TickCount) { }
        public SystemRng(int seed) { _r = new Random(seed); }

        public void Reseed(int seed) { _r = new Random(seed); }

        public double NextDouble() => _r.NextDouble();
        public int Range(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
    }

    /// <summary>Always returns the same value — used to project an "expected" (roll = 1.0) outcome.</summary>
    public sealed class FixedRng : IRng
    {
        readonly double _value;
        public FixedRng(double value) { _value = value; }

        public double NextDouble() => _value;
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(_value * (maxExclusive - minInclusive));
        }
    }
}
