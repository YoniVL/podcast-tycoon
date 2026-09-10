using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    /// <summary>
    /// One slot in the episode rundown (spec §7): a topic covered from an angle, with its own
    /// share of the topic-prep points. Main / Second / Recurring; Second and Recurring can be
    /// left empty on a lighter week.
    /// </summary>
    public sealed class Segment
    {
        public TopicId Topic;
        public Topic ThreadTopic;         // set instead of Topic for a story-thread topic
        public int Stance;                // -1 / 0 / +1 on a thread topic
        public Angle Angle = Angle.Analysis;
        public int Prep;                  // topic-prep points spent on this segment
        public bool Guest;
        public bool IsEmpty = true;

        public Topic Resolved => ThreadTopic ?? TopicCatalog.Get(Topic);

        public void Set(Topic topic, int stance = 0)
        {
            if (topic == null) { Clear(); return; }
            if (topic.SourceThread != null) { ThreadTopic = topic; Stance = stance == 0 ? 1 : stance; }
            else { Topic = topic.Id; ThreadTopic = null; Stance = 0; }
            IsEmpty = false;
        }

        public void Clear()
        {
            ThreadTopic = null; Stance = 0; Prep = 0; Guest = false; IsEmpty = true;
        }

        public Segment Clone() => new Segment
        {
            Topic = Topic, ThreadTopic = ThreadTopic, Stance = Stance, Angle = Angle,
            Prep = Prep, Guest = Guest, IsEmpty = IsEmpty
        };
    }

    /// <summary>The episode the player builds each week: a three-slot rundown plus the shared
    /// production levers (spec §7, §9).</summary>
    public sealed class ProductionPlan
    {
        public readonly Segment Main = new Segment();
        public readonly Segment Second = new Segment();
        public readonly Segment Recurring = new Segment();

        public int PrepResearch;
        public int PrepAudio;
        public int PrepPromo;

        public IEnumerable<Segment> Slots
        {
            get { yield return Main; yield return Second; yield return Recurring; }
        }

        public IEnumerable<Segment> FilledSlots => Slots.Where(s => !s.IsEmpty);

        public int TopicPrep => Main.Prep + Second.Prep + Recurring.Prep;
        public int TotalPrep => TopicPrep + PrepResearch + PrepAudio + PrepPromo;

        // --- back-compat shims: older callers and tests treat the plan as a single topic ---
        public TopicId Topic
        {
            get => Main.Topic;
            set { Main.Topic = value; Main.ThreadTopic = null; Main.Stance = 0; Main.IsEmpty = false; }
        }
        public Topic ThreadTopic
        {
            get => Main.ThreadTopic;
            set { Main.ThreadTopic = value; if (value != null) Main.IsEmpty = false; }
        }
        public int Stance { get => Main.Stance; set => Main.Stance = value; }
        public int PrepTopic { get => Main.Prep; set => Main.Prep = value; }

        /// <summary>The headline topic — Main's, used for the episode log and the results screen.</summary>
        public Topic Resolved => Main.Resolved;

        public static ProductionPlan Cover(Topic topic, int stance = 1)
        {
            var p = new ProductionPlan();
            p.Main.Set(topic, stance);
            return p;
        }

        public ProductionPlan Clone()
        {
            var p = new ProductionPlan
            {
                PrepResearch = PrepResearch, PrepAudio = PrepAudio, PrepPromo = PrepPromo
            };
            CopySlot(Main, p.Main); CopySlot(Second, p.Second); CopySlot(Recurring, p.Recurring);
            return p;
        }

        static void CopySlot(Segment from, Segment to)
        {
            to.Topic = from.Topic; to.ThreadTopic = from.ThreadTopic; to.Stance = from.Stance;
            to.Angle = from.Angle; to.Prep = from.Prep; to.Guest = from.Guest; to.IsEmpty = from.IsEmpty;
        }
    }

    /// <summary>The full outcome of publishing one episode (spec §17).</summary>
    public sealed class EpisodeResult
    {
        public Topic Topic;                // the Main segment's topic (the headline)

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

        // --- rundown interactions & risk state (spec §8, §12) ---
        public readonly System.Collections.Generic.List<string> Notes = new System.Collections.Generic.List<string>();
        public bool Whiplash;
        public bool AnyClash;
        public float OverreachNextWeek;     // fraction of listeners to churn next week
        public int SegmentCount;
        public float FreshnessMult = 1f;

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

        float SlotWeight(ProductionPlan plan, Segment s)
        {
            if (ReferenceEquals(s, plan.Main)) return _cfg.SlotWeightMain;
            if (ReferenceEquals(s, plan.Second)) return _cfg.SlotWeightSecond;
            return _cfg.SlotWeightRecurring;
        }

        public EpisodeResult Resolve(GameState st, WeekContext ctx, ProductionPlan plan, IRng rng)
        {
            var cfg = _cfg;
            var d = DifficultyProfile.For(st.Difficulty);
            var crew = st.Crew;

            float micQ = st.HasGear(Gear.XlrMic) ? cfg.MicQualityUpgraded : cfg.MicQualityBase;
            float editSkill = st.HasGear(Gear.EditingSoftware) ? cfg.EditSkillUpgraded : cfg.EditSkillBase;
            float qualityFloorBonus = st.HasGear(Gear.AcousticPanels) ? cfg.PanelsQualityFloorBonus : 0f;
            float crewAppeal = st.HasCoHost ? cfg.CoHostAppealBonus : 0f;
            float gear = cfg.QualityGearWeight * micQ + cfg.QualityGearWeight * editSkill;
            int effortRelief = CrewCatalog.EffortRelief(crew);
            float researchMult = CrewCatalog.ResearchMultiplier(crew);
            float studioReach = st.HasStudioSpace ? 1.08f : 1f;

            var filled = plan.FilledSlots.ToList();
            if (filled.Count == 0) filled.Add(plan.Main);   // guard: never a zero-slot episode
            float totalWeight = filled.Sum(s => SlotWeight(plan, s));

            // Freshness (spec §12): repetition drags appeal, variety and lighter weeks restore it.
            float freshnessMult = MathX.Clamp(0.80f + (st.Freshness / 100f) * 0.25f, 0.80f, 1.05f);
            int bigSurprise = Math.Abs((int)ctx.Surprise - (int)Surprise.Par);

            var notes = new List<string>();
            bool sawCrisisEarlier = false;
            bool whiplash = false;

            float reach = 0f, qualityW = 0f, spreadW = 0f, appealW = 0f;
            float repDelta = 0f, credDelta = 0f, socialFlat = 0f;
            bool anyCrisis = false;
            int hotTakes = 0, distinctAngles;
            var anglesSeen = new HashSet<Angle>();
            var families = new List<TopicFamily>();
            bool mainLightRecurringHeavy = false;

            for (int i = 0; i < filled.Count; i++)
            {
                var seg = filled[i];
                var topic = seg.Resolved;
                var ang = AngleCatalog.Get(seg.Angle);
                bool isMain = ReferenceEquals(seg, plan.Main);
                bool isRecurring = ReferenceEquals(seg, plan.Recurring);
                float w = SlotWeight(plan, seg) / totalWeight;

                anglesSeen.Add(seg.Angle);
                families.Add(topic.Family);
                if (seg.Angle == Angle.HotTake) hotTakes++;
                if (seg.Angle == Angle.Comedy && sawCrisisEarlier) whiplash = true;

                int effort = Math.Max(1, topic.Effort - effortRelief);
                bool thin = seg.Prep < effort * 0.4f;
                float prepRatio = MathX.Clamp01((float)seg.Prep / effort);
                float overshoot = Math.Max(0, seg.Prep - effort) * cfg.QualityOvershootPerPoint;
                float q = cfg.QualityFloor
                          + cfg.QualityPrepWeight * prepRatio
                          + overshoot
                          + gear
                          + cfg.QualityAudioPerPoint * plan.PrepAudio
                          + qualityFloorBonus
                          + st.CardQualityBonusThisWeek;
                q = MathX.Clamp(q, cfg.QualityMin, cfg.QualityMax);
                if (thin) q = Math.Min(q, 0.5f);

                float spread = Math.Max(0f, topic.Swing * ang.SwingMult
                    * (1f - cfg.ResearchSpreadReductionPerPoint * plan.PrepResearch * researchMult));
                if (st.Modifiers.ChaosCycle) spread *= 1.5f;

                float contextMult = ContextResolver.AppealMultiplier(topic.Response, ctx) * ctx.ImportanceAppealMult;
                float momentBonus = ctx.HasDramaticMoment
                    && (topic.Response == TopicResponse.Reaction || topic.Response == TopicResponse.Crisis) ? 0.12f : 0f;
                float angleMult = ang.AppealMult;
                if (seg.Angle == Angle.Emotional) angleMult *= 1f + 0.20f * bigSurprise;      // lands on a big result
                if (seg.Angle == Angle.Comedy && sawCrisisEarlier) angleMult *= 0.5f;          // bombs after a crisis
                if (isRecurring && st.RecurringStreak >= 3) angleMult *= 0.6f;                 // overexposed bit
                float appeal = Math.Max(0.05f,
                    topic.BaseAppeal * contextMult * angleMult + crewAppeal + st.SponsorAppealPenalty + momentBonus);
                if (seg.Guest) appeal += cfg.GuestAppealBonus;
                appeal *= freshnessMult;

                float segReach = st.Listeners * appeal * w
                    * (1f + cfg.PromoReachPerPoint * plan.PrepPromo)
                    * ctx.ReachMult * st.CardReachMultThisWeek * studioReach;

                reach += segReach;
                qualityW += q * w;
                spreadW += spread * w;
                appealW += appeal * w;

                float analysisRep = topic.Response == TopicResponse.Evergreen && topic.RepEarn > 0f
                    ? CrewCatalog.AnalysisRepBonus(crew) : 0f;
                repDelta += (topic.RepEarn * q + analysisRep + ang.RepDelta) * w;
                credDelta += (topic.CredHook * MathX.Clamp(q, 0.4f, 1.5f) + ang.CredDelta) * w;
                socialFlat += topic.SocialHook * ctx.SocialMultiplier + ang.SocialAdd + (seg.Guest ? cfg.GuestSocialBonus : 0);

                if (topic.Response == TopicResponse.Crisis) { anyCrisis = true; sawCrisisEarlier = true; }
                if (isMain && topic.BaseAppeal >= 1.2f) mainLightRecurringHeavy = true;
                if (isRecurring && topic.BaseAppeal <= 1.0f && mainLightRecurringHeavy) notes.Add("palate cleanser");
            }

            distinctAngles = anglesSeen.Count;

            float quality = qualityW;
            float spreadC = spreadW;
            float appealC = appealW;

            // ---- interaction pass (spec §8) ----
            float reachMod = 1f, qualityMod = 0f;
            bool anyClash = false;

            if (hotTakes >= 2)
            {
                repDelta -= 3f; credDelta -= 2f; socialFlat += 3f;
                notes.Add("just ranting"); anyClash = true;
            }
            if (whiplash)
            {
                notes.Add("tonal whiplash"); anyClash = true;
            }
            if (filled.Count >= 2 && families[0] == families[1] && families[0] != TopicFamily.Meta)
            {
                reachMod *= 0.85f;
                notes.Add("one-note"); anyClash = true;
                bool deepDive = (plan.Main.Angle == Angle.Analysis || plan.Main.Angle == Angle.Investigation)
                                && filled[1].Angle == Angle.Analysis;
                if (deepDive) { credDelta += 3f; notes.Add("deep dive"); }
            }
            if (st.RecurringStreak >= 3 && !plan.Recurring.IsEmpty)
            {
                // Overexposed: handled as a freshness hit in Engine; note it here.
                notes.Add("same bit again");
            }
            if (distinctAngles == filled.Count && filled.Count == 3 && !anyClash)
            {
                qualityMod += 0.08f; notes.Add("well-produced");
            }
            bool seriousMain = plan.Main.Angle == Angle.Analysis || plan.Main.Angle == Angle.Investigation;
            if (!plan.Recurring.IsEmpty && plan.Recurring.Angle == Angle.Comedy && seriousMain && !whiplash)
            {
                socialFlat += 4f; notes.Add("range");
            }

            reach *= reachMod;
            quality = MathX.Clamp(quality + qualityMod, cfg.QualityMin, cfg.QualityMax);

            // Market saturation: growth tails off as the audience nears the addressable market.
            float market = (cfg.MarketBase + cfg.MarketSeasonBonus * (st.Season - 1))
                           * (cfg.MarketRepFloor + cfg.MarketRepPerPoint * st.Reputation)
                           * d.MarketFactor;
            float growthRoom = MathX.Clamp01(1f - st.Listeners / Math.Max(1f, market));

            float passiveGain = st.Listeners * ctx.PassiveGainRate * growthRoom;
            float clipsPassive = st.Listeners * CrewCatalog.PassiveReachPerWeek(crew) * growthRoom;
            float churnRate = cfg.ChurnRate * (st.Modifiers.GentleChurn ? 0.8f : 1f);
            if (st.SlumpWeeks > 0) churnRate *= cfg.SlumpChurnMult;
            float moodChurnMult = whiplash ? 1.5f : 1f;

            int Delta(float roll)
            {
                float gross = reach * (quality - cfg.QualityBreakeven) * roll * cfg.DeltaScale * growthRoom;
                float wom = quality > 1f ? st.Listeners * cfg.WordOfMouthRate * (quality - 1f) * growthRoom : 0f;
                float churn = st.Listeners * churnRate * MathX.Clamp(1.40f - quality, 0f, 1.40f);
                float moodChurn = st.Listeners * ctx.MoodChurnRate * Math.Max(0f, 1.15f - quality) * moodChurnMult;
                return MathX.RoundToInt(gross + wom - churn + passiveGain + clipsPassive - moodChurn);
            }

            float actualRoll = st.CardGuaranteeGoodRoll
                ? 1f + spreadC
                : (float)(1.0 + (rng.NextDouble() * 2.0 - 1.0) * spreadC);

            var result = new EpisodeResult
            {
                Topic = plan.Resolved,
                Quality = quality,
                Spread = spreadC,
                EffectiveAppeal = appealC,
                Reach = reach,
                Roll = actualRoll,
                Whiplash = whiplash,
                AnyClash = anyClash,
                SegmentCount = filled.Count,
                FreshnessMult = freshnessMult,
                ListenerDeltaExpected = Delta(1f),
                ListenerDeltaLow = Delta(1f - spreadC),
                ListenerDeltaHigh = Delta(1f + spreadC),
                ListenerDeltaActual = Delta(actualRoll)
            };
            result.Notes.AddRange(notes);

            // --- overreach (spec §9, §12): heavy promo on a weak episode churns back next week ---
            if (plan.PrepPromo >= 3 && quality < 0.85f)
            {
                float bump = cfg.PromoReachPerPoint * plan.PrepPromo;
                result.OverreachNextWeek = bump * cfg.OverreachChurnFraction;
                credDelta -= 1.5f;
                result.Notes.Add("overhyped");
            }

            // --- reputation ---
            result.ReputationDelta = repDelta
                                     - (quality < cfg.SloppyQualityThreshold ? cfg.SloppyReputationPenalty : 0f)
                                     - (st.HasPartnership && anyCrisis ? 1.5f : 0f);

            // --- credibility ---
            result.CredibilityDelta = credDelta;

            // --- social reach (a hot episode gets talked about) ---
            float perf = quality * actualRoll;
            float hotEpisode = perf > cfg.SocialHotThreshold
                ? (perf - cfg.SocialHotThreshold) * cfg.SocialHotScale * (float)Math.Sqrt(Math.Max(0.1f, appealC))
                : 0f;
            float social = socialFlat + hotEpisode + st.CardSocialBonusThisWeek;
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
