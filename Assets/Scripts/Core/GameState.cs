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
        public int BuzzGained;
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
        public int Buzz = 0;

        // --- club ---
        public float TeamStrength = 0.52f;

        // --- assets ---
        public Gear Gear = Gear.None;
        public bool HasCoHost = false;

        // --- progress / fail ---
        public int ConsecutiveWeeksInDebt = 0;
        public int ConsecutiveWeeksNoAudience = 0;
        public bool IsGameOver = false;
        public string GameOverReason = "";
        public bool GoalReached = false;
        public int NextMilestoneIndex = 0;

        // --- history ---
        public readonly List<int> ListenerHistory = new List<int>();
        public readonly List<EpisodeRecord> Episodes = new List<EpisodeRecord>();
        public int PeakListeners = 40;
        public int EpisodesPublished = 0;

        public bool HasGear(Gear g) => (Gear & g) == g;

        public int PrepCapacity(GameConfig cfg)
            => cfg.PrepBase + (HasCoHost ? cfg.CoHostPrepBonus : 0);

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
