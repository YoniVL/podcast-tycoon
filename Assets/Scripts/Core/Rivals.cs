using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public sealed class Rival
    {
        public int ClubIndex;
        public string Name;
        public string Arc;          // "the title race" / "the drop" / "your transfer target"
        public bool StumbledThisWeek;
        public bool SurgedThisWeek;
    }

    /// <summary>
    /// Tracks 1–2 rival clubs a season (spec §11). Their results are content: a rival stumble
    /// opens the schadenfreude topic, a rival surge opens the worried topic, and fixtures
    /// against a rival are always derby-importance.
    /// </summary>
    public sealed class RivalTracker
    {
        readonly IRng _rng;
        public readonly List<Rival> Rivals = new List<Rival>();
        int _cooldown;

        static readonly string[] Arcs = { "the title race", "a European place", "the drop", "your transfer target" };

        public RivalTracker(IRng rng) { _rng = rng; }

        public void OnSeasonStart(SeasonCalendar cal)
        {
            Rivals.Clear();
            _cooldown = 3;

            var me = cal.PlayerClub;
            var candidates = new List<int>();
            for (int i = 0; i < cal.Clubs.Count; i++)
            {
                if (i == cal.PlayerIndex) continue;
                if (Math.Abs(cal.Clubs[i].Strength - me.Strength) < 0.17f) candidates.Add(i);
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = _rng.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int n = candidates.Count >= 2 && _rng.NextDouble() < 0.6 ? 2 : Math.Min(1, candidates.Count);
            for (int k = 0; k < n; k++)
            {
                int idx = candidates[k];
                Rivals.Add(new Rival
                {
                    ClubIndex = idx,
                    Name = cal.Clubs[idx].Name,
                    Arc = me.Strength > 0.6f ? Arcs[_rng.Range(0, 2)]
                        : me.Strength < 0.4f ? "the drop"
                        : Arcs[_rng.Range(0, Arcs.Length)]
                });
            }
        }

        /// <summary>Bump a fixture to derby-importance if the opponent is a tracked rival.</summary>
        public void MarkFixture(Fixture fx)
        {
            if (fx == null || fx.OpponentIndex < 0) return;
            foreach (var r in Rivals)
                if (r.ClubIndex == fx.OpponentIndex)
                {
                    fx.IsRivalFixture = true;
                    if (fx.Importance == FixtureImportance.Normal) fx.Importance = FixtureImportance.Derby;
                }
        }

        public void Tick(Engine engine, WeekContext ctx)
        {
            if (Rivals.Count == 0) return;
            var cal = engine.Calendar;

            foreach (var r in Rivals) { r.StumbledThisWeek = false; r.SurgedThisWeek = false; }
            if (_cooldown > 0) { _cooldown--; return; }
            if (ctx.Match == null) return;
            if (_rng.NextDouble() > 0.35) return;

            var rv = Rivals[_rng.Range(0, Rivals.Count)];
            int rivalPos = cal.PositionOf(rv.ClubIndex);
            int myPos = ctx.LeaguePosition;
            var club = cal.Clubs[rv.ClubIndex];
            bool rivalLostGround = club.Played >= 4 && rivalPos > myPos + 2;
            bool rivalGainedGround = club.Played >= 4 && rivalPos < myPos - 2;

            if (rivalLostGround && _rng.NextDouble() < 0.65)
            {
                rv.StumbledThisWeek = true;
                ctx.RivalNews.Add($"{rv.Name} are falling apart — {SeasonCalendar.Ordinal(rivalPos)} and no sign of a fix. Good week to enjoy it.");
                _cooldown = 4;
            }
            else if (rivalGainedGround && _rng.NextDouble() < 0.6)
            {
                rv.SurgedThisWeek = true;
                ctx.RivalNews.Add($"{rv.Name} keep winning — {SeasonCalendar.Ordinal(rivalPos)}, clear of you now. The fanbase is rattled.");
                _cooldown = 4;
            }

            ctx.AnyRivalStumbled = Rivals.Exists(x => x.StumbledThisWeek);
            ctx.AnyRivalSurged = Rivals.Exists(x => x.SurgedThisWeek);
        }
    }
}
