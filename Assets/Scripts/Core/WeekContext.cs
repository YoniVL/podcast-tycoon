namespace PodcastTycoon.Core
{
    /// <summary>
    /// Everything "the week happens" produces, before the player picks a topic.
    /// The appeal multipliers here are consumed by <see cref="Resolution"/>.
    /// </summary>
    public sealed class WeekContext
    {
        public int Turn;                    // season turn (1-based)
        public int GlobalWeek;              // lifetime week (1-based)
        public int Season;                  // 1-based

        public Fixture Fixture;
        public MatchResult Match;           // null on a matchless week
        public int LeaguePosition;
        public string LeaguePositionLabel;  // "7th"

        public bool IsInternationalBreak => Fixture != null && Fixture.IsInternationalBreak;
        public bool IsOffseason => Fixture != null && Fixture.IsOffseason;
        public bool IsMatchless => Fixture == null || Fixture.IsMatchless;

        public Surprise Surprise => Match?.Surprise ?? Surprise.Par;
        public FixtureImportance Importance => Fixture?.Importance ?? FixtureImportance.Normal;

        // --- resolved context modifiers (filled by ContextResolver) ---
        public float ReachMult = 1f;
        public float PassiveGainRate;       // fraction of listeners gained regardless of episode
        public float MoodChurnRate;         // fraction lost if the episode is weak
        public float ImportanceAppealMult = 1f;
        public int BuzzMultiplier = 1;

        /// <summary>A short line describing what kind of week this is, for the UI.</summary>
        public string Headline;
    }

    public static class ContextResolver
    {
        public static void Fill(WeekContext c)
        {
            c.ReachMult = 1f;
            c.PassiveGainRate = 0f;
            c.MoodChurnRate = 0f;
            c.ImportanceAppealMult = 1f;
            c.BuzzMultiplier = 1;

            if (c.IsOffseason)
            {
                c.ReachMult = 0.85f;
                c.Headline = "Offseason — no football, but the fans are still listening.";
                return;
            }

            if (c.IsInternationalBreak)
            {
                c.ReachMult = 0.80f;
                c.Headline = "International break — no club game this week.";
                return;
            }

            switch (c.Surprise)
            {
                case Surprise.Heroic:
                    c.PassiveGainRate = 0.020f;
                    c.Headline = "A famous result. Everyone wants to hear about this one.";
                    break;
                case Surprise.Good:
                    c.PassiveGainRate = 0.010f;
                    c.Headline = "A good day. The mood is up.";
                    break;
                case Surprise.Par:
                    c.Headline = "Roughly what everyone expected.";
                    break;
                case Surprise.Poor:
                    c.MoodChurnRate = 0.010f;
                    c.Headline = "A frustrating one. The fanbase is grumbling.";
                    break;
                case Surprise.Disaster:
                    c.MoodChurnRate = 0.025f;
                    c.Headline = "A disaster. Handle this badly and people will switch off.";
                    break;
            }

            if (c.Importance != FixtureImportance.Normal)
            {
                c.ImportanceAppealMult = 1.30f;
                c.BuzzMultiplier = 2;
                if (c.Importance == FixtureImportance.Derby)
                    c.Headline = "Derby week. " + c.Headline;
            }
        }

        /// <summary>Context appeal multiplier for a topic, by how it responds to the result (spec §5).</summary>
        public static float AppealMultiplier(TopicResponse response, WeekContext c)
        {
            if (c.IsOffseason)
            {
                switch (response)
                {
                    case TopicResponse.Reaction: return 0.60f;
                    case TopicResponse.Positive: return 0.70f;
                    case TopicResponse.Crisis: return 0.60f;
                    default: return 1.00f;
                }
            }

            if (c.IsInternationalBreak)
            {
                switch (response)
                {
                    case TopicResponse.Reaction: return 0.55f;
                    case TopicResponse.Positive: return 0.60f;
                    case TopicResponse.Crisis: return 0.50f;
                    default: return 0.90f;
                }
            }

            switch (response)
            {
                case TopicResponse.Reaction:
                    switch (c.Surprise)
                    {
                        case Surprise.Heroic: return 1.80f;
                        case Surprise.Good: return 1.40f;
                        case Surprise.Par: return 1.00f;
                        case Surprise.Poor: return 0.90f;
                        default: return 0.70f; // Disaster
                    }
                case TopicResponse.Positive:
                    switch (c.Surprise)
                    {
                        case Surprise.Heroic: return 1.70f;
                        case Surprise.Good: return 1.35f;
                        default: return 0.60f;
                    }
                case TopicResponse.Crisis:
                    switch (c.Surprise)
                    {
                        case Surprise.Disaster: return 2.00f;
                        case Surprise.Poor: return 1.50f;
                        case Surprise.Par: return 0.85f;
                        case Surprise.Good: return 0.55f;
                        default: return 0.45f; // Heroic
                    }
                case TopicResponse.Evergreen:
                default:
                    return 1.00f;
            }
        }
    }
}
