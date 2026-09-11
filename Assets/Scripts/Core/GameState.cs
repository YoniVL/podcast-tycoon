using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public sealed class EpisodeRecord
    {
        public int GlobalWeek;
        public int Season;
        public string TopicName;
        public string QualityLabel;
        public float Quality;
        public Surprise Surprise;
        public bool Matchless;
        public int ListenerDelta;
        public float ReputationDelta;
        public float SocialGained;
    }

    /// <summary>Mutable state of a single run.</summary>
    public sealed class GameState
    {
        // --- identity (chosen at setup) ---
        public string PodcastName = "The Untitled Pod";
        public string ClubName = "Rovers";
        public string ColourPrimary = "#2F6DB5";
        public string ColourSecondary = "#F2C14E";
        public Difficulty Difficulty = Difficulty.Regular;

        // --- clock ---
        public int GlobalWeek = 1;   // lifetime, 1-based
        public int Season = 1;       // 1-based
        public int SeasonTurn = 1;   // turn within the current season, 1-based

        // --- resources ---
        public float Money = 500f;
        public int Listeners = 40;          // headline number = Core + Casual (spec §5)
        public float Reputation = 10f;

        // --- audience pools, simulated behind the one Listeners number (spec §5, v0.4) ---
        public float Core = 40f;            // loyal, low churn, pays best
        public float Casual;                // chases the buzz, high churn
        public float Followers;             // clip-only; barely monetises, amplifies virality

        public string LoyaltyLabel
        {
            get
            {
                float aud = Core + Casual;
                if (aud < 1f) return "New";
                float share = Core / aud;
                return share >= 0.70f ? "Devoted" : share >= 0.45f ? "Solid" : share >= 0.25f ? "Fickle" : "Fragile";
            }
        }

        // --- slow indices (spec §5, v0.4) ---
        public float Credibility = 50f;   // how much people trust what you say
        public float SocialReach = 5f;    // how loud your megaphone is (absorbs old "Buzz")

        // --- club ---
        public float TeamStrength = 0.52f;

        // --- competitions (slice 3) ---
        public bool InEuropeThisSeason;
        public int CupsWon;
        public int EuropeanTrophies;
        public int BestLeagueFinish = 20;

        // --- assets ---
        public bool HasCoHost = false;
        public int CoHostWageBump;               // added to the co-host's monthly wage (from events)
        public Crew Crew = Crew.None;             // which roles are currently filled
        public int CrewWageBump;
        public bool HasSecondSponsorSlot;

        // --- crew roster (slice 6, spec §19) ---
        public readonly Dictionary<Crew, CrewCandidate> Employed = new Dictionary<Crew, CrewCandidate>();
        public readonly Dictionary<Crew, List<CrewCandidate>> CandidatePool = new Dictionary<Crew, List<CrewCandidate>>();
        public int CandidatePoolRefreshWeeks = 1;

        // --- upgrade tracks (slice 6, spec §20): 0 = not bought, 1-4 = tier owned ---
        public int SetTier;
        public int AudioTier;
        public int PostTier;
        public int StudioTier;
        public int DistributionTier;

        // --- audience-as-leverage (slice 3, spec §14) ---
        public int AccessTier;                   // 0..3, recomputed each week from the rolling average
        public int TrustedStanding;              // better future scoops; grows on "verify & hold"
        public bool HasPartnership;
        public int ScoopsBroken;
        public int YouthChampioned;
        public int AccessProtectedWeeks;         // a burned scoop suppresses your tier for a while

        // --- run modifiers (slice 3D) ---
        public RunModifiers Modifiers = new RunModifiers();
        public bool IsCustomRun;

        // --- season review (slice 3D) ---
        public SeasonSummary LastSeason;

        // --- morale, backfire correction (slice 5, spec §5, §11) ---
        public float Morale = 70f;
        public bool PendingCorrection;
        public int PendingCorrectionWeeks;
        public bool LastEpisodeBackfired;        // read by sponsor CONDUCT terms the following week

        // --- freshness / slumps / overreach (slice 4B, spec §12) ---
        public float Freshness = 80f;
        public int SlumpWeeks;                    // > 0 while the show is in a slump
        public int RecurringStreak;              // consecutive weeks with the same recurring bit
        public bool HadEpisodeLastWeek;
        public Angle LastMainAngle;
        public TopicFamily LastMainFamily;
        public TopicId LastRecurringTopic;
        public bool LastHadRecurring;
        public float OverreachChurnNextWeek;     // fraction of listeners that churn next week

        // --- cards (slice 3E) ---
        public readonly List<string> Hand = new List<string>();
        public int CardsPlayedThisWeek;
        public float CardReachMultThisWeek = 1f;
        public int CardPrepBonusThisWeek;
        public int CardPermanentPrepBonus;
        public float CardQualityBonusThisWeek;
        public float CardSocialBonusThisWeek;
        public bool CardGuaranteeGoodRoll;

        // --- temporary effects (from events) ---
        public int PrepPenaltyThisWeek;          // subtracted from prep capacity, this week only
        public float WeeklyListenerDrift;        // fractional listener change applied each week
        public int WeeklyListenerDriftWeeks;

        // --- sponsor (reflected here each week for the resolution maths) ---
        public int SponsorWeekly;
        public float SponsorAppealPenalty;       // <= 0, applied while a demanding sponsor is active

        // --- progress / fail ---
        public int ConsecutiveWeeksInDebt = 0;
        public int ConsecutiveWeeksNoAudience = 0;
        public bool IsGameOver = false;
        public string GameOverReason = "";
        public bool GoalReached = false;
        public int NextMilestoneIndex = 0;

        // --- 1M buyout decision (slice 3D) ---
        public bool BuyoutPending;
        public bool BuyoutResolved;
        public bool BuyoutAccepted;

        // --- history ---
        public readonly List<int> ListenerHistory = new List<int>();
        public readonly List<EpisodeRecord> Episodes = new List<EpisodeRecord>();
        public int PeakListeners = 40;
        public int EpisodesPublished = 0;

        public bool HasCrew(Crew c) => (Crew & c) == c;

        public int PrepCapacity(GameConfig cfg)
            => Math.Max(4, cfg.PrepBase
                           + (HasCoHost ? cfg.CoHostPrepBonus : 0)
                           + CrewCatalog.PrepBonus(this)
                           + UpgradeCatalog.Current(this, UpgradeTrack.Studio).PrepBonus
                           + (Modifiers.ExtraPrep ? 1 : 0)
                           + CardPrepBonusThisWeek
                           + CardPermanentPrepBonus
                           - PrepPenaltyThisWeek
                           - (CrewCatalog.AnyEmployeeHasTrait(this, "perfectionist") ? 1 : 0));

        /// <summary>Rolling average of recent episodes (used for milestones / later, access tiers).</summary>
        public int AverageListeners(int window)
        {
            if (ListenerHistory.Count == 0) return Listeners;
            int n = Math.Min(window, ListenerHistory.Count);
            long sum = 0;
            for (int i = ListenerHistory.Count - n; i < ListenerHistory.Count; i++)
                sum += ListenerHistory[i];
            return (int)(sum / n);
        }
    }
}
