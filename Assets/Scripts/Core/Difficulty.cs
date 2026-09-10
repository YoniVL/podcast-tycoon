namespace PodcastTycoon.Core
{
    public enum Difficulty
    {
        Casual,
        Regular,
        Hard,
        Nightmare
    }

    /// <summary>
    /// Difficulty is expressed as club strength plus a couple of economy tweaks (spec §20).
    /// A weaker club means more losing, which means more crisis content and a tighter economy.
    /// </summary>
    public readonly struct DifficultyProfile
    {
        public readonly Difficulty Difficulty;
        public readonly float TeamStrength;   // 0..1, start of a run
        public readonly float AdRate;         // € per listener per episode
        public readonly float FixedOverhead;  // € per week
        public readonly float MarketFactor;   // multiplies the addressable audience
        public readonly string Label;
        public readonly string Outlook;

        DifficultyProfile(Difficulty d, float strength, float adRate, float overhead, float market, string label, string outlook)
        {
            Difficulty = d;
            TeamStrength = strength;
            AdRate = adRate;
            FixedOverhead = overhead;
            MarketFactor = market;
            Label = label;
            Outlook = outlook;
        }

        public static DifficultyProfile For(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Casual:
                    return new DifficultyProfile(d, 0.72f, 0.0050f, 16f, 1.30f, "Casual",
                        "Your club fights for the title, Europe and the cup.");
                case Difficulty.Hard:
                    return new DifficultyProfile(d, 0.36f, 0.0042f, 22f, 0.82f, "Hard",
                        "Your club is in a relegation scrap. Crisis weeks will be common.");
                case Difficulty.Nightmare:
                    return new DifficultyProfile(d, 0.26f, 0.0040f, 24f, 0.68f, "Nightmare",
                        "Your club is fighting to survive. Almost every week is damage control.");
                case Difficulty.Regular:
                default:
                    return new DifficultyProfile(Difficulty.Regular, 0.52f, 0.0045f, 20f, 1.00f, "Regular",
                        "Your club is mid-table, with the odd cup run. A balanced mix of weeks.");
            }
        }
    }
}
