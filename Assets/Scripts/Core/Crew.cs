using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    /// <summary>The crew roles you can hire into once you can carry the monthly cost (spec §15).</summary>
    [Flags]
    public enum Crew
    {
        None = 0,
        Producer = 1 << 0,
        Researcher = 1 << 1,
        Clips = 1 << 2,
        Booker = 1 << 3
    }

    public sealed class CrewRole
    {
        public Crew Id;
        public string Name;
        public string Effect;
        public Func<GameConfig, int> Wage;
    }

    public static class CrewCatalog
    {
        public static readonly IReadOnlyList<CrewRole> All = new List<CrewRole>
        {
            new CrewRole { Id = Crew.Producer,   Name = "Producer",       Effect = "+2 prep points a week, and every topic takes one less point to prep properly.", Wage = c => c.ProducerWage },
            new CrewRole { Id = Crew.Researcher, Name = "Researcher",      Effect = "The Research lever works twice as hard, and analysis episodes build more reputation.", Wage = c => c.ResearcherWage },
            new CrewRole { Id = Crew.Clips,      Name = "Clips manager",   Effect = "Every episode gets talked about more online, and a slow trickle of new listeners each week.", Wage = c => c.ClipsWage },
            new CrewRole { Id = Crew.Booker,     Name = "Booker",          Effect = "Unlocks the Big interview topic and lines up more guest events.", Wage = c => c.BookerWage }
        };

        public static CrewRole Get(Crew id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        // --- effect accessors, read by Resolution / GameState ---
        public static int PrepBonus(Crew hired) => (hired & Crew.Producer) != 0 ? 2 : 0;
        public static int EffortRelief(Crew hired) => (hired & Crew.Producer) != 0 ? 1 : 0;
        public static float ResearchMultiplier(Crew hired) => (hired & Crew.Researcher) != 0 ? 2f : 1f;
        public static float AnalysisRepBonus(Crew hired) => (hired & Crew.Researcher) != 0 ? 0.35f : 0f;
        public static float SocialMultiplier(Crew hired) => (hired & Crew.Clips) != 0 ? 1.30f : 1f;
        public static float PassiveReachPerWeek(Crew hired) => (hired & Crew.Clips) != 0 ? 0.01f : 0f;
        public static bool HasBooker(Crew hired) => (hired & Crew.Booker) != 0;

        public static int MonthlyWageBill(Crew hired, GameConfig cfg)
        {
            int sum = 0;
            foreach (var r in All) if ((hired & r.Id) != 0) sum += r.Wage(cfg);
            return sum;
        }
    }
}
