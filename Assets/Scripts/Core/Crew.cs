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

        /// <summary>Trait names with what they actually do, e.g. "Diva (+10 skill, +30% wage)"
        /// — so it's visible why one candidate costs more than another, not just that they do.</summary>
        public string TraitText => Traits.Count == 0 ? "" : string.Join(", ", Traits.Select(t => CrewCatalog.TraitDisplayText(t)));
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

        /// <summary>What a trait actually does, spelled out — the skill/wage modifiers every
        /// trait carries, plus the few traits with an extra effect beyond that (hardcoded
        /// where they're actually applied: <see cref="Engine"/>'s BeginWeek for grafter,
        /// <see cref="RecordingBeats"/>.Select for loose_cannon, <see cref="Resolution"/> for
        /// perfectionist's quality-floor bonus).</summary>
        public static string TraitEffectText(string traitId)
        {
            var trait = Trait(traitId);
            if (trait == null) return "";
            var parts = new List<string>();
            if (Math.Abs(trait.SkillMod) > 0.001f)
                parts.Add($"{(trait.SkillMod > 0 ? "+" : "")}{MathX.RoundToInt(trait.SkillMod * 100f)} skill");
            if (Math.Abs(trait.WageMult - 1f) > 0.001f)
                parts.Add($"{(trait.WageMult > 1f ? "+" : "")}{MathX.RoundToInt((trait.WageMult - 1f) * 100f)}% wage");
            switch (traitId)
            {
                case "grafter": parts.Add("+1 morale/week while employed"); break;
                case "loose_cannon": parts.Add("more happens during recording"); break;
                case "perfectionist": parts.Add("+quality floor"); break;
            }
            return string.Join(", ", parts);
        }

        public static string TraitDisplayText(string traitId)
        {
            var trait = Trait(traitId);
            if (trait == null) return traitId;
            string effect = TraitEffectText(traitId);
            return effect.Length > 0 ? $"{trait.Name} ({effect})" : trait.Name;
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
        // Split into a skill->number primitive (so the UI can preview a candidate's impact
        // before hiring them, not just the current employee's) and a GameState-reading
        // wrapper for the sim to call — same formula either way, single source of truth.
        public static float SkillOf(GameState st, Crew role) => st.Employed.TryGetValue(role, out var c) ? c.Skill : 0f;

        public static int PrepBonusForSkill(float skill) => MathX.RoundToInt(2f * skill);
        public static int EffortReliefForSkill(float skill) => MathX.RoundToInt(1f * skill);
        public static float ResearchMultiplierForSkill(float skill) => 1f + 1f * skill;
        public static float AnalysisRepBonusForSkill(float skill) => 0.35f * skill;
        public static float SocialMultiplierForSkill(float skill) => 1f + 0.30f * skill;
        public static float PassiveReachPerWeekForSkill(float skill) => 0.01f * skill;

        public static int PrepBonus(GameState st) => PrepBonusForSkill(SkillOf(st, Crew.Producer));
        public static int EffortRelief(GameState st) => EffortReliefForSkill(SkillOf(st, Crew.Producer));
        public static float ResearchMultiplier(GameState st) => ResearchMultiplierForSkill(SkillOf(st, Crew.Researcher));
        public static float AnalysisRepBonus(GameState st) => AnalysisRepBonusForSkill(SkillOf(st, Crew.Researcher));
        public static float SocialMultiplier(GameState st) => SocialMultiplierForSkill(SkillOf(st, Crew.Clips));
        public static float PassiveReachPerWeek(GameState st) => PassiveReachPerWeekForSkill(SkillOf(st, Crew.Clips));

        /// <summary>A concrete, skill-scaled readout of what a person in this role actually
        /// does at their current skill — shown for both the employee and every candidate, so
        /// "impact" is a real number instead of the role's generic flavour text.</summary>
        public static string ImpactSummary(Crew role, float skill)
        {
            switch (role)
            {
                case Crew.Producer:
                    return $"+{PrepBonusForSkill(skill)} prep point(s)/week, −{EffortReliefForSkill(skill)} effort needed per topic";
                case Crew.Researcher:
                    // Spelled out mechanically rather than naming "the Research lever" — that
                    // only means something once you've already read the Production panel's
                    // own tooltip, which isn't guaranteed by the time you're staffing crew.
                    int researchPct = MathX.RoundToInt((ResearchMultiplierForSkill(skill) - 1f) * 100f);
                    // The raw per-segment bonus is well under 1 at every skill level, so a
                    // straight round would always show "0" and read as no effect — scale it
                    // up to a whole number over 10 segments instead, same info, no decimals.
                    int repPerTenSegments = MathX.RoundToInt(AnalysisRepBonusForSkill(skill) * 10f);
                    return $"Research prep cuts {researchPct}% more risk per point spent, +{repPerTenSegments} reputation per 10 Analysis segments";
                case Crew.Clips:
                    int socialPct = MathX.RoundToInt((SocialMultiplierForSkill(skill) - 1f) * 100f);
                    return $"+{socialPct}% social reach gains, plus a trickle of new followers every week";
                case Crew.Booker:
                    return "Unlocks the Big interview topic and guest bookings — a presence perk, doesn't scale with skill";
                default:
                    return "";
            }
        }

        public static int MonthlyWageBill(GameState st)
        {
            int sum = 0;
            foreach (var c in st.Employed.Values) sum += c.Wage;
            return sum;
        }
    }
}
