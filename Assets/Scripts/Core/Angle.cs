namespace PodcastTycoon.Core
{
    /// <summary>
    /// How a segment covers its topic (spec §7). The angle sits on top of the topic: it
    /// reshapes appeal and variance and moves reputation / credibility / social reach in its
    /// own direction. Analysis is the safe Core-builder; Hot take is the loud Credibility
    /// drain; Investigation is the deep one that needs a Researcher or insider access.
    /// </summary>
    public enum Angle
    {
        Analysis,
        HotTake,
        Emotional,
        Comedy,
        Investigation
    }

    public sealed class AngleProfile
    {
        public Angle Id;
        public string Name;
        public string Blurb;
        public float AppealMult;
        public float SwingMult;
        public float RepDelta;
        public float CredDelta;
        public int SocialAdd;
        public bool NeedsResearchAccess;   // Investigation: Researcher crew or access tier >= 2
    }

    public static class AngleCatalog
    {
        public static readonly AngleProfile[] All =
        {
            new AngleProfile
            {
                Id = Angle.Analysis, Name = "Analysis",
                Blurb = "Measured, well-argued. Slow but it builds trust and a loyal core.",
                AppealMult = 0.95f, SwingMult = 0.6f, RepDelta = 1.5f, CredDelta = 2.0f, SocialAdd = 0
            },
            new AngleProfile
            {
                Id = Angle.HotTake, Name = "Hot take",
                Blurb = "Say the thing, loudly. Huge reach and chatter — and it eats your credibility.",
                AppealMult = 1.45f, SwingMult = 1.8f, RepDelta = -1.5f, CredDelta = -2.5f, SocialAdd = 6
            },
            new AngleProfile
            {
                Id = Angle.Emotional, Name = "Emotional",
                Blurb = "Lean into how it feels to be a fan. Lands hard on a big result, flat otherwise.",
                AppealMult = 1.20f, SwingMult = 1.1f, RepDelta = 0f, CredDelta = 0f, SocialAdd = 2
            },
            new AngleProfile
            {
                Id = Angle.Comedy, Name = "Comedy bit",
                Blurb = "Play it for laughs. A big driver of clips — awkward straight after a crisis.",
                AppealMult = 1.10f, SwingMult = 0.9f, RepDelta = 0f, CredDelta = -0.5f, SocialAdd = 5
            },
            new AngleProfile
            {
                Id = Angle.Investigation, Name = "Investigation",
                Blurb = "Dig in properly. Big credibility, forces a story on. Needs a Researcher or insider access.",
                AppealMult = 1.35f, SwingMult = 1.3f, RepDelta = 2f, CredDelta = 3.5f, SocialAdd = 2,
                NeedsResearchAccess = true
            }
        };

        public static AngleProfile Get(Angle a)
        {
            foreach (var p in All) if (p.Id == a) return p;
            return All[0];
        }

        public static bool Allowed(Angle a, GameState st)
        {
            var p = Get(a);
            if (!p.NeedsResearchAccess) return true;
            return st.HasCrew(Crew.Researcher) || st.AccessTier >= 2;
        }
    }
}
