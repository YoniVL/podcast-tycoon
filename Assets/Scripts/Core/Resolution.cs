using System;

namespace PodcastTycoon.Core
{
    public sealed class ProductionPlan
    {
        /// <summary>A catalog topic. Ignored when <see cref="ThreadTopic"/> is set.</summary>
        public TopicId Topic;

        /// <summary>Set instead of <see cref="Topic"/> when covering a story-thread topic.</summary>
        public Topic ThreadTopic;

        /// <summary>The position taken on a thread topic: -1, 0 (none), or +1.</summary>
        public int Stance;

        public int PrepTopic;
        public int PrepResearch;
        public int PrepAudio;
        public int PrepPromo;

        public int TotalPrep => PrepTopic + PrepResearch + PrepAudio + PrepPromo;

        /// <summary>The actual topic being covered.</summary>
        public Topic Resolved => ThreadTopic ?? TopicCatalog.Get(Topic);

        /// <summary>Build a plan that covers the given topic (catalog or thread).</summary>
        public static ProductionPlan Cover(Topic topic, int stance = 1)
        {
            var p = new ProductionPlan();
            if (topic.SourceThread != null) { p.ThreadTopic = topic; p.Stance = stance; }
            else p.Topic = topic.Id;
            return p;
        }

        public ProductionPlan Clone() => new ProductionPlan
        {
            Topic = Topic, ThreadTopic = ThreadTopic, Stance = Stance,
            PrepTopic = PrepTopic, PrepResearch = PrepResearch,
            PrepAudio = PrepAudio, PrepPromo = PrepPromo
        };
    }

    /// <summary>The full outcome of publishing one episode (spec §13).</summary>
    public sealed class EpisodeResult
    {
        public Topic Topic;

        public float Quality;
        public float Spread;
        public float EffectiveAppeal;
        public float Reach;
        public float Roll;                 // the roll actually used (1.0 for a projection)

        public int ListenerDeltaExpected;  // at roll = 1.0
        public int ListenerDeltaLow;       // worst roll
        public int ListenerDeltaHigh;      // best roll
        public int ListenerDeltaActual;

        public float ReputationDelta;
        public float CredibilityDelta;
        public float SocialGained;

        public float AdRevenue;
        public float SponsorRevenue;
        public float WeeklyCosts;
        public float MonthlyWagesCharged;
        public float MoneyDelta;

        public string QualityLabel =>
            Quality >= 1.35f ? "Outstanding" :
            Quality >= 1.10f ? "Strong" :
            Quality >= 0.90f ? "Solid" :
            Quality >= 0.65f ? "Patchy" : "Weak";
    }

    public sealed class Resolution
    {
        readonly GameConfig _cfg;

        public Resolution(GameConfig cfg) { _cfg = cfg; }

        /// <summary>Deterministic projection at roll = 1.0 (for the live UI preview).</summary>
        public EpisodeResult Project(GameState st, WeekContext ctx, ProductionPlan plan)
            => Resolve(st, ctx, plan, new FixedRng(0.5));

        public EpisodeResult Resolve(GameState st, WeekContext ctx, ProductionPlan plan, IRng rng)
        {
            var cfg = _cfg;
            var d = DifficultyProfile.For(st.Difficulty);
            var topic = plan.Resolved;

            float micQ = st.HasGear(Gear.XlrMic) ? cfg.MicQualityUpgraded : cfg.MicQualityBase;
            float editSkill = st.HasGear(Gear.EditingSoftware) ? cfg.EditSkillUpgraded : cfg.EditSkillBase;
            float qualityFloorBonus = st.HasGear(Gear.AcousticPanels) ? cfg.PanelsQualityFloorBonus : 0f;
            float crewAppeal = st.HasCoHost ? cfg.CoHostAppealBonus : 0f;

            var crew = st.Crew;
            int effectiveEffort = Math.Max(1, topic.Effort - CrewCatalog.EffortRelief(crew));
            float researchMult = CrewCatalog.ResearchMultiplier(crew);

            // --- quality ---
            float prepRatio = MathX.Clamp01((float)plan.PrepTopic / effectiveEffort);
            float overshoot = Math.Max(0, plan.PrepTopic - effectiveEffort) * cfg.QualityOvershootPerPoint;
            float gear = cfg.QualityGearWeight * micQ + cfg.QualityGearWeight * editSkill;
            float quality = cfg.QualityFloor
                            + cfg.QualityPrepWeight * prepRatio
                            + overshoot
                            + gear
                            + cfg.QualityAudioPerPoint * plan.PrepAudio
                            + qualityFloorBonus
                            + st.CardQualityBonusThisWeek;
            quality = MathX.Clamp(quality, cfg.QualityMin, cfg.QualityMax);

            // --- volatility ---
            float spread = Math.Max(0f, topic.Swing * (1f - cfg.ResearchSpreadReductionPerPoint * plan.PrepResearch * researchMult));
            if (st.Modifiers.ChaosCycle) spread *= 1.5f;

            // --- appeal & reach ---
            float contextMult = ContextResolver.AppealMultiplier(topic.Response, ctx) * ctx.ImportanceAppealMult;
            float momentBonus = ctx.HasDramaticMoment
                && (topic.Response == TopicResponse.Reaction || topic.Response == TopicResponse.Crisis) ? 0.12f : 0f;
            float appeal = Math.Max(0.05f, topic.BaseAppeal * contextMult + crewAppeal + st.SponsorAppealPenalty + momentBonus);
            float studioReach = st.HasStudioSpace ? 1.08f : 1f;
            float reach = st.Listeners * appeal * (1f + cfg.PromoReachPerPoint * plan.PrepPromo)
                          * ctx.ReachMult * st.CardReachMultThisWeek * studioReach;

            // Market saturation: growth tails off as the audience nears the addressable
            // market, which grows with reputation and across seasons.
            float market = (cfg.MarketBase + cfg.MarketSeasonBonus * (st.Season - 1))
                           * (cfg.MarketRepFloor + cfg.MarketRepPerPoint * st.Reputation)
                           * d.MarketFactor;
            float growthRoom = MathX.Clamp01(1f - st.Listeners / Math.Max(1f, market));

            float passiveGain = st.Listeners * ctx.PassiveGainRate * growthRoom;

            float clipsPassive = st.Listeners * CrewCatalog.PassiveReachPerWeek(crew) * growthRoom;
            float churnRate = cfg.ChurnRate * (st.Modifiers.GentleChurn ? 0.8f : 1f);

            int Delta(float roll)
            {
                float gross = reach * (quality - cfg.QualityBreakeven) * roll * cfg.DeltaScale * growthRoom;
                float wom = quality > 1f ? st.Listeners * cfg.WordOfMouthRate * (quality - 1f) * growthRoom : 0f;
                float churn = st.Listeners * churnRate * MathX.Clamp(1.40f - quality, 0f, 1.40f);
                float moodChurn = st.Listeners * ctx.MoodChurnRate * Math.Max(0f, 1.15f - quality);
                return MathX.RoundToInt(gross + wom - churn + passiveGain + clipsPassive - moodChurn);
            }

            float actualRoll = st.CardGuaranteeGoodRoll
                ? 1f + spread
                : (float)(1.0 + (rng.NextDouble() * 2.0 - 1.0) * spread);

            var result = new EpisodeResult
            {
                Topic = topic,
                Quality = quality,
                Spread = spread,
                EffectiveAppeal = appeal,
                Reach = reach,
                Roll = actualRoll,
                ListenerDeltaExpected = Delta(1f),
                ListenerDeltaLow = Delta(1f - spread),
                ListenerDeltaHigh = Delta(1f + spread),
                ListenerDeltaActual = Delta(actualRoll)
            };

            // --- reputation ---
            float analysisRep = topic.Response == TopicResponse.Evergreen && topic.RepEarn > 0f
                ? CrewCatalog.AnalysisRepBonus(crew) : 0f;
            result.ReputationDelta = topic.RepEarn * quality + analysisRep
                                     - (quality < cfg.SloppyQualityThreshold ? cfg.SloppyReputationPenalty : 0f)
                                     - (st.HasPartnership && topic.Response == TopicResponse.Crisis ? 1.5f : 0f);

            // --- credibility (spec §17) ---
            result.CredibilityDelta = topic.CredHook * MathX.Clamp(quality, 0.4f, 1.5f);

            // --- social reach (absorbs old "Buzz"; a hot episode gets talked about) ---
            float perf = quality * actualRoll;
            float hotEpisode = perf > cfg.SocialHotThreshold
                ? (perf - cfg.SocialHotThreshold) * cfg.SocialHotScale * (float)Math.Sqrt(Math.Max(0.1f, appeal))
                : 0f;
            float social = topic.SocialHook * ctx.SocialMultiplier + hotEpisode + st.CardSocialBonusThisWeek;
            social *= CrewCatalog.SocialMultiplier(crew);
            result.SocialGained = Math.Max(0f, social);

            // --- economy ---
            float adRate = d.AdRate * (st.Modifiers.SponsorFree ? 1.7f : 1f);
            result.AdRevenue = st.Listeners * adRate * (cfg.AdReputationFloor + cfg.AdReputationRange * st.Reputation / 100f);
            float hosting = cfg.HostingBase + st.Listeners * cfg.HostingSlope;

            float wages = 0f;
            bool payday = st.GlobalWeek % cfg.MonthlyIntervalWeeks == 0;
            if (payday)
            {
                if (st.HasCoHost) wages += cfg.CoHostMonthlyWage + st.CoHostWageBump;
                wages += CrewCatalog.MonthlyWageBill(st.Crew, cfg);
                if (st.HasStudioSpace) wages += cfg.StudioSpaceMonthly;
            }
            result.MonthlyWagesCharged = wages;

            result.WeeklyCosts = d.FixedOverhead + hosting + wages;
            result.SponsorRevenue = st.Modifiers.SponsorFree ? 0f : st.SponsorWeekly;
            float partnershipRevenue = st.HasPartnership ? result.AdRevenue * 0.35f : 0f;
            result.MoneyDelta = result.AdRevenue + result.SponsorRevenue + partnershipRevenue - result.WeeklyCosts;

            return result;
        }
    }
}
