using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    /// <summary>The crew roles you can hire into (spec §19).</summary>
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
        public Func<GameConfig, int> BaseWage;
    }

    /// <summary>A trait on a candidate — always a clear upside and a clear downside (spec §19).</summary>
    public sealed class CrewTrait
    {
        public string Id;
        public string Name;
        public string Text;
        public float SkillMod;       // added to generated skill
        public float WageMult = 1f;  // multiplies generated wage
    }

    /// <summary>One person you could hire, or have hired, into a role.</summary>
    public sealed class CrewCandidate
    {
        public Crew Role;
        public string Name;
        public float Skill;                 // 0.3 - 1.0, scales the role's effect
        public int Wage;                    // monthly
        public readonly List<string> Traits = new List<string>();
        public int ContractWeeksLeft;        // while > 0, firing costs a buyout on top of severance

        public string TraitText => Traits.Count == 0 ? "" : string.Join(", ", Traits.Select(t => CrewCatalog.Trait(t)?.Name ?? t));
    }

    public static class CrewCatalog
    {
        public static readonly IReadOnlyList<CrewRole> All = new List<CrewRole>
        {
            new CrewRole { Id = Crew.Producer,   Name = "Producer",       Effect = "+prep points a week, and every topic takes less effort to prep properly.", BaseWage = c => c.ProducerWage },
            new CrewRole { Id = Crew.Researcher, Name = "Researcher",     Effect = "The Research lever works harder, and analysis builds more reputation.", BaseWage = c => c.ResearcherWage },
            new CrewRole { Id = Crew.Clips,      Name = "Clips manager",  Effect = "Episodes get talked about more online, and a trickle of new followers every week.", BaseWage = c => c.ClipsWage },
            new CrewRole { Id = Crew.Booker,     Name = "Booker",         Effect = "Unlocks the Big interview topic and lines up guest events.", BaseWage = c => c.BookerWage }
        };

        public static CrewRole Get(Crew id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        public static readonly IReadOnlyList<CrewTrait> Traits = new List<CrewTrait>
        {
            new CrewTrait { Id = "cheap", Name = "Cheap and keen", WageMult = 0.70f, SkillMod = -0.20f,
                Text = "Wage well under market — and it shows in the work." },
            new CrewTrait { Id = "famous", Name = "Podcast-famous", WageMult = 1.50f, SkillMod = 0.15f,
                Text = "Brings real pull — and knows exactly what they're worth." },
            new CrewTrait { Id = "diva", Name = "Diva", WageMult = 1.30f, SkillMod = 0.10f,
                Text = "Genuinely good at the job. Also genuinely hard work." },
            new CrewTrait { Id = "grafter", Name = "Grafter", WageMult = 1f, SkillMod = 0f,
                Text = "Steady and reliable — good for morale around the place." },
            new CrewTrait { Id = "loose_cannon", Name = "Loose cannon", WageMult = 1f, SkillMod = 0f,
                Text = "Unpredictable on the mic — more happens when they're in the room." },
            new CrewTrait { Id = "perfectionist", Name = "Perfectionist", WageMult = 1.10f, SkillMod = 0.05f,
                Text = "Won't let anything average out the door — but it's slower going." },
        };

        public static CrewTrait Trait(string id)
        {
            foreach (var t in Traits) if (t.Id == id) return t;
            return null;
        }

        // ------------------------------------------------------------------
        static readonly string[] FirstNames =
            { "Priya", "Callum", "Ines", "Deshawn", "Maja", "Theo", "Noor", "Finn", "Zara", "Kai", "Elin", "Bram" };
        static readonly string[] LastNames =
            { "Okafor", "Bryndle", "Sowa", "Vance", "Okonkwo", "Riis", "Halden", "Marchetti", "Osei", "Winward" };

        /// <summary>Roll a new candidate for a role (spec §19).</summary>
        public static CrewCandidate Generate(Crew role, GameConfig cfg, IRng rng)
        {
            var c = new CrewCandidate
            {
                Role = role,
                Name = $"{FirstNames[rng.Range(0, FirstNames.Length)]} {LastNames[rng.Range(0, LastNames.Length)]}",
                Skill = 0.5f + (float)rng.NextDouble() * 0.5f,
                Wage = Get(role).BaseWage(cfg),
                ContractWeeksLeft = 12
            };

            int traitRoll = rng.Range(0, 10);
            int traitCount = traitRoll < 3 ? 0 : traitRoll < 8 ? 1 : 2;
            var pool = new List<CrewTrait>(Traits);
            for (int i = 0; i < traitCount && pool.Count > 0; i++)
            {
                var t = pool[rng.Range(0, pool.Count)];
                pool.Remove(t);
                c.Traits.Add(t.Id);
                c.Skill = MathX.Clamp(c.Skill + t.SkillMod, 0.3f, 1f);
                c.Wage = MathX.RoundToInt(c.Wage * t.WageMult);
            }
            return c;
        }

        public static bool HasTrait(GameState st, Crew role, string traitId) =>
            st.Employed.TryGetValue(role, out var c) && c.Traits.Contains(traitId);

        public static bool AnyEmployeeHasTrait(GameState st, string traitId) =>
            st.Employed.Values.Any(c => c.Traits.Contains(traitId));

        // --- skill-scaled effect accessors, read by Resolution (spec §19: skill scales the effect) ---
        public static float SkillOf(GameState st, Crew role) => st.Employed.TryGetValue(role, out var c) ? c.Skill : 0f;

        public static int PrepBonus(GameState st) => MathX.RoundToInt(2f * SkillOf(st, Crew.Producer));
        public static int EffortRelief(GameState st) => MathX.RoundToInt(1f * SkillOf(st, Crew.Producer));
        public static float ResearchMultiplier(GameState st) => 1f + 1f * SkillOf(st, Crew.Researcher);
        public static float AnalysisRepBonus(GameState st) => 0.35f * SkillOf(st, Crew.Researcher);
        public static float SocialMultiplier(GameState st) => 1f + 0.30f * SkillOf(st, Crew.Clips);
        public static float PassiveReachPerWeek(GameState st) => 0.01f * SkillOf(st, Crew.Clips);

        public static int MonthlyWageBill(GameState st)
        {
            int sum = 0;
            foreach (var c in st.Employed.Values) sum += c.Wage;
            return sum;
        }
    }
}
