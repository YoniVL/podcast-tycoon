namespace PodcastTycoon.Core
{
    public enum MatchOutcome { Loss = 0, Draw = 1, Win = 2 }

    /// <summary>Result versus expectation — the number that reshapes the episode (spec §5).</summary>
    public enum Surprise
    {
        Disaster = -2,
        Poor = -1,
        Par = 0,
        Good = 1,
        Heroic = 2
    }

    public enum FixtureImportance { Normal, BigMatch, Derby, Final }

    public enum Competition { League, Cup, European }

    public sealed class Fixture
    {
        public int Turn;                 // season turn this fixture belongs to (1-based)
        public bool IsInternationalBreak;
        public bool IsOffseason;
        public string Opponent;
        public int OpponentIndex = -1;   // index into SeasonCalendar.Clubs, -1 when not league play
        public bool Home;
        public float OpponentStrength;
        public FixtureImportance Importance = FixtureImportance.Normal;

        // --- competition (slice 3) ---
        public Competition Competition = Competition.League;
        public int LeagueRound = -1;     // 0-based index into the round-robin, -1 when not league
        public int CupRound;             // 1-based; 0 when not a cup tie
        public int EuropeGame;           // 1-based; 0 when not a European night
        public bool IsCupByeWeek;        // a cup weekend you're watching from home (knocked out)

        public bool IsMatchless => IsInternationalBreak || IsOffseason || IsCupByeWeek;
        public bool IsRivalFixture;      // set by RivalTracker — always derby-importance
    }

    public sealed class MatchResult
    {
        public MatchOutcome Outcome;
        public int GoalsFor;
        public int GoalsAgainst;
        public Surprise Surprise;
        public float WinProbability;
        public float LossProbability;

        public string ScoreLine => $"{GoalsFor}–{GoalsAgainst}";
    }
}
