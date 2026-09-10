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
        public Surprise Surprise;
        public bool Matchless;
        public int ListenerDelta;
        public float ReputationDelta;
        public float SocialGained;
    }

    [Flags]
    public enum Gear
    {
        None = 0,
        XlrMic = 1 << 0,
        AcousticPanels = 1 << 1,
        EditingSoftware = 1 << 2
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
        public int Listeners = 40;
        public float Reputation = 10f;

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
        public Gear Gear = Gear.None;
        public bool HasCoHost = false;
        public int CoHostWageBump;               // added to the co-host's monthly wage (from events)
        public Crew Crew = Crew.None;
        public int CrewWageBump;
        public bool HasSecondSponsorSlot;
        public bool HasStudioSpace;

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

        public bool HasGear(Gear g) => (Gear & g) == g;
        public bool HasCrew(Crew c) => (Crew & c) == c;

        public int PrepCapacity(GameConfig cfg)
            => Math.Max(4, cfg.PrepBase
                           + (HasCoHost ? cfg.CoHostPrepBonus : 0)
                           + CrewCatalog.PrepBonus(Crew)
                           + (HasStudioSpace ? 2 : 0)
                           + (Modifiers.ExtraPrep ? 1 : 0)
                           + CardPrepBonusThisWeek
                           + CardPermanentPrepBonus
                           - PrepPenaltyThisWeek);

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
