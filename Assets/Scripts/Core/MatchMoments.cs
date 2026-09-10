using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    /// <summary>
    /// The match sim emits 1–2 "moments" that sharpen the week's topic hooks (spec §4).
    /// First pass: colour for the match panel plus a small appeal nudge when something dramatic happened.
    /// </summary>
    public static class MatchMoments
    {
        static readonly string[] Screamer =
        {
            "{p} lashed one in from 30 yards — goal of the season contender.",
            "{p} with an absolute screamer into the top corner."
        };
        static readonly string[] Howler =
        {
            "{p} played a suicidal backpass and gifted them a goal.",
            "A horror mix-up at the back, {p} at the heart of it."
        };
        static readonly string[] RedCard =
        {
            "{p} saw red after a wild lunge — down to ten for half an hour.",
            "A straight red for {p}. Reckless, and it changed the game."
        };
        static readonly string[] KeeperError =
        {
            "The keeper let a tame one squirm under him.",
            "A flap at a corner and it's in off the keeper."
        };
        static readonly string[] LastMinute =
        {
            "A 94th-minute winner. Scenes.",
            "Heartbreak — they nicked it in stoppage time."
        };
        static readonly string[] Masterclass =
        {
            "{p} ran the game from midfield. A complete performance.",
            "{p} was untouchable today."
        };
        static readonly string[] WonderkidGoal =
        {
            "{p} with the winner — 18 years old and ice cold.",
            "{p} announced himself with a brilliant solo goal."
        };

        public static void Emit(WeekContext ctx, Fixture fx, MatchResult m, Squad squad, IRng rng)
        {
            if (m == null) return;

            var talisman = squad.Get(PlayerArchetype.Talisman);
            var kid = squad.Get(PlayerArchetype.Wonderkid);
            var captain = squad.Get(PlayerArchetype.Captain);

            void Add(string[] pool, Player p)
            {
                string line = pool[rng.Range(0, pool.Length)];
                line = line.Replace("{p}", p != null ? p.Name : "someone");
                ctx.Moments.Add(line);
            }

            int margin = m.GoalsFor - m.GoalsAgainst;

            // Positive-leaning moments
            if (m.Outcome == MatchOutcome.Win && rng.NextDouble() < 0.45)
            {
                if (kid != null && kid.IsFit && rng.NextDouble() < 0.35) Add(WonderkidGoal, kid);
                else if (rng.NextDouble() < 0.5) Add(Screamer, talisman);
                else Add(Masterclass, captain ?? talisman);
            }

            // Negative-leaning moments
            if ((m.Outcome == MatchOutcome.Loss || m.Surprise <= Surprise.Poor) && rng.NextDouble() < 0.5)
            {
                double r = rng.NextDouble();
                if (r < 0.4) Add(Howler, captain);
                else if (r < 0.7) Add(KeeperError, squad.Get(PlayerArchetype.Keeper));
                else Add(RedCard, talisman);
            }

            // Drama regardless of result
            if (System.Math.Abs(margin) <= 1 && rng.NextDouble() < 0.3)
                ctx.Moments.Add(LastMinute[rng.Range(0, LastMinute.Length)]);

            ctx.HasDramaticMoment = ctx.Moments.Count > 0
                && (m.Surprise <= Surprise.Poor || m.Surprise >= Surprise.Heroic || System.Math.Abs(margin) >= 3);
        }
    }
}
