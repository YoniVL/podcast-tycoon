using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    /// <summary>A snapshot of a completed season, shown on the season-review screen (spec §21).</summary>
    public sealed class SeasonSummary
    {
        public int Season;
        public int LeaguePosition;
        public string LeaguePositionLabel;
        public bool WonCup;
        public bool WonEurope;
        public bool PlayedInEurope;

        public int ListenersStart;
        public int ListenersEnd;
        public float ReputationStart;
        public float ReputationEnd;
        public float MoneyEnd;
        public int EpisodesThisSeason;
        public int BestEpisodeListenerGain;
        public string BestEpisodeTopic;

        public readonly List<string> Headlines = new List<string>();   // thread resolutions, trophies, milestones
    }
}
