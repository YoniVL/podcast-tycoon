namespace PodcastTycoon.Core
{
    /// <summary>
    /// Every tuning knob for the slice-1 loop (spec §24). Difficulty-dependent values
    /// (ad rate, fixed overhead, team strength) live on <see cref="DifficultyProfile"/>.
    /// These are deliberately mutable fields so a designer can tweak a live instance.
    /// </summary>
    public sealed class GameConfig
    {
        // --- starting state ---
        public int StartMoney = 500;
        public int StartListeners = 40;
        public float StartReputation = 10f;
        public float StartCredibility = 50f;
        public float StartSocialReach = 5f;
        public int PrepBase = 12;

        // --- the rundown (spec §7): three segment slots, reach split by weight ---
        public float SlotWeightMain = 0.60f;
        public float SlotWeightSecond = 0.30f;
        public float SlotWeightRecurring = 0.10f;
        public float GuestAppealBonus = 0.15f;
        public int GuestSocialBonus = 3;

        // --- listener model ---
        public float ChurnRate = 0.045f;
        public float QualityBreakeven = 0.70f;
        public float DeltaScale = 0.22f;   // v0.4: the safe episode only holds serve (spec §12, §17)
        public float WordOfMouthRate = 0.03f;

        // --- audience pools (spec §5, §17) ---
        public float CoreChurn = 0.015f;
        public float CasualChurn = 0.070f;
        public float FollowerChurn = 0.120f;
        public float CasualToCoreRate = 0.030f;      // conversion when you're consistent + credible
        public float FollowerToCasualRate = 0.010f;
        public float FollowerAdFraction = 0.05f;     // of the Core ad rate — they barely pay
        public float CasualAdFraction = 0.55f;
        public float ViralAmplifyK = 1.4f;           // breakouts and duds scale with social reach

        // --- freshness / slumps / overreach (spec §12) ---
        public float FreshnessStart = 80f;
        public float FreshnessDriftTarget = 60f;
        public float SlumpChurnMult = 1.5f;
        public float SlumpQualityCeiling = 0.75f;   // episodes below this count as "bad"
        public float SlumpRecoverQuality = 0.95f;   // two of these in a row exits a slump
        public float OverreachChurnFraction = 0.35f; // fraction of the promo bump that churns back

        // Market saturation — growth slows to zero as the audience approaches the
        // addressable market, which itself grows with reputation and over seasons.
        public float MarketBase = 60000f;
        public float MarketSeasonBonus = 6000f;      // per season after the first
        public float MarketRepFloor = 0.45f;         // market multiplier at 0 reputation
        public float MarketRepPerPoint = 0.012f;     // extra multiplier per reputation point
        public int MaxListeners = 20_000_000;        // hard ceiling, overflow guard
        public int ZeroAudienceGraceWeeks = 4;       // weeks at 0 listeners before the run ends

        // --- quality model ---
        public float QualityFloor = 0.35f;
        public float QualityPrepWeight = 0.65f;
        public float QualityOvershootPerPoint = 0.025f;
        public float QualityGearWeight = 0.15f;      // applied to both mic and editing skill
        public float QualityAudioPerPoint = 0.035f;
        public float QualityMin = 0.20f;
        public float QualityMax = 1.60f;

        // --- volatility ---
        public float ResearchSpreadReductionPerPoint = 0.18f;

        // --- reach / promo ---
        public float PromoReachPerPoint = 0.08f;

        // --- reputation ---
        public float SloppyQualityThreshold = 0.55f;
        public float SloppyReputationPenalty = 4f;

        // --- social reach (absorbs old "Buzz"; spec §5, §17) ---
        public float SocialDecayPerWeek = 1.5f;   // decays toward a size floor each week
        public float SocialHotThreshold = 1.15f;  // quality*roll above this "gets talked about"
        public float SocialHotScale = 12f;
        public float CredFloorTier2 = 55f;   // access tier 2/3 need this much credibility (spec §18)
        public float CredFloorTier3 = 70f;

        // --- economy ---
        public float HostingBase = 5f;
        public float HostingSlope = 0.0009f;         // € per listener per week
        public float AdReputationFloor = 0.80f;      // ad multiplier at 0 reputation
        public float AdReputationRange = 0.40f;      // extra multiplier at 100 reputation

        // --- fail state ---
        public float BankruptcyFloor = -200f;
        public int BankruptcyGraceWeeks = 3;

        // --- goal ---
        public int GoalListeners = 50000;
        public int[] Milestones =
        {
            100, 500, 1000, 2500, 10000, 25000, 50000,
            100_000, 250_000, 500_000, 1_000_000, 2_500_000, 5_000_000, 10_000_000
        };
        public int AvgListenerWindow = 6;   // episodes averaged for access / sponsor targets

        // --- match sim ---
        public float HomeAdvantage = 0.06f;
        public float MatchProbBase = 0.45f;
        public float MatchProbSlope = 0.85f;
        public float MatchProbMin = 0.05f;
        public float MatchProbMax = 0.88f;
        public float ExpectationThreshold = 0.55f;   // pWin/pLoss above this => "expected"
        public float OffseasonChurn = 0.15f;
        public float TeamStrengthSeasonDrift = 0.01f; // per season, toward 0.50

        // --- gear upgrades (one-time) ---
        public int MicCost = 150;
        public float MicQualityBase = 0.20f;
        public float MicQualityUpgraded = 0.60f;

        public int PanelsCost = 120;
        public float PanelsQualityFloorBonus = 0.08f;

        public int EditingCost = 200;
        public float EditSkillBase = 0.15f;
        public float EditSkillUpgraded = 0.45f;

        // --- co-host (monthly) ---
        public int CoHostHireCost = 0;                // no signing fee in slice 1
        public int CoHostMonthlyWage = 180;
        public int CoHostPrepBonus = 3;
        public float CoHostAppealBonus = 0.05f;
        public int MonthlyIntervalWeeks = 4;

        // --- actions ---
        public int RedrawCost = 15;

        // --- calendar ---
        public int LeagueMatchdays = 38;
        public int[] InternationalBreakTurns = { 6, 15, 24, 33 };

        // --- competitions (slice 3) ---
        // Cup / European rounds are spliced in after these league matchdays.
        public int[] CupAfterMatchday = { 6, 12, 18, 24, 29 };        // 5 knockout rounds
        public int[] EuropeAfterMatchday = { 3, 9, 14, 20, 26, 32 };  // 6-game league phase, only if qualified
        public int OffseasonTurns = 3;                                // playable quiet weeks before rollover
        public int EuropeQualifyPosition = 5;                         // finish here or better → Europe next season
        public float CupOpponentBase = 0.44f;                         // round-1 opponent strength
        public float CupOpponentStep = 0.055f;                        // added per round
        public float EuropeOpponentBase = 0.60f;
        public float EuropeOpponentSpread = 0.22f;
        public float CupWinSocial = 14f;
        public float CupWinReputation = 6f;

        // --- access tiers (slice 3, spec §14) ---
        public int[] AccessTierListeners = { 1000, 12000, 80000 };    // tier 1 / 2 / 3 thresholds on the rolling average

        // --- scoops (slice 3) ---
        public float ScoopBaseChance = 0.14f;                         // per eligible week at tier 2+
        public float ScoopDisruptionBase = 0.15f;
        public float ScoopDisruptionPerTier = 0.10f;

        // --- crew (slice 3, monthly) ---
        public int ProducerWage = 220;
        public int ResearcherWage = 200;
        public int ClipsWage = 240;
        public int BookerWage = 260;

        // --- extra gear (slice 3) ---
        public int SecondSponsorSlotCost = 600;
        public int StudioSpaceCost = 900;
        public int StudioSpaceMonthly = 30;

        // --- endless milestones / buyout ---
        public int BuyoutListeners = 1_000_000;

        // --- cards & packs (slice 3E; bought with cash since v0.4) ---
        public int PackCostMoney = 150;
        public int CardHandLimit = 5;
        public int CardPlaysPerWeek = 2;
    }
}
