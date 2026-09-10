namespace PodcastTycoon.Core
{
    /// <summary>
    /// Pre-run toggles unlocked between runs (spec §19). Enabling any of these flags the run
    /// as "Custom" — the endless chase still works, it just sits on its own board.
    /// </summary>
    public sealed class RunModifiers
    {
        public bool ExtraPrep;          // +1 base prep point a week
        public bool GentleChurn;        // churn -20%
        public bool NestEgg;            // start +€300
        public bool LongRunway;         // bankruptcy grace 5 instead of 3
        public bool NoInternationalBreaks;
        public bool SponsorFree;        // no sponsors, higher ad rate
        public bool ChaosCycle;         // more events, wilder match results
        public bool Sandbox;            // no bankruptcy fail state

        public bool Any =>
            ExtraPrep || GentleChurn || NestEgg || LongRunway ||
            NoInternationalBreaks || SponsorFree || ChaosCycle || Sandbox;

        public RunModifiers Clone() => new RunModifiers
        {
            ExtraPrep = ExtraPrep, GentleChurn = GentleChurn, NestEgg = NestEgg, LongRunway = LongRunway,
            NoInternationalBreaks = NoInternationalBreaks, SponsorFree = SponsorFree,
            ChaosCycle = ChaosCycle, Sandbox = Sandbox
        };
    }
}
