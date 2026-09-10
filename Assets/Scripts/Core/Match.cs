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

    public enum FixtureImportance { Normal, BigMatch, Derby }

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

        public bool IsMatchless => IsInternationalBreak || IsOffseason;
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
