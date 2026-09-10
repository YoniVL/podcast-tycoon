using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public sealed class LeagueClub
    {
        public string Name;
        public float Strength;
        public bool IsPlayer;

        public int Played;
        public int Won;
        public int Drawn;
        public int Lost;
        public int GoalsFor;
        public int GoalsAgainst;

        public int Points => Won * 3 + Drawn;
        public int GoalDifference => GoalsFor - GoalsAgainst;

        public void Record(int gf, int ga)
        {
            Played++;
            GoalsFor += gf;
            GoalsAgainst += ga;
            if (gf > ga) Won++;
            else if (gf == ga) Drawn++;
            else Lost++;
        }

        public void Reset()
        {
            Played = Won = Drawn = Lost = GoalsFor = GoalsAgainst = 0;
        }
    }

    /// <summary>
    /// A 20-club league with a double round-robin schedule, plus the player's per-turn
    /// calendar (matchdays interleaved with international breaks). Slice 1 (spec §3, §25).
    /// </summary>
    public sealed class SeasonCalendar
    {
        public const int ClubCount = 20;

        public readonly List<LeagueClub> Clubs = new List<LeagueClub>();
        public int PlayerIndex { get; private set; }
        public int Season { get; private set; }

        readonly GameConfig _cfg;
        readonly List<(int home, int away)[]> _rounds = new List<(int, int)[]>();
        readonly List<Fixture> _turns = new List<Fixture>();  // index 0 => turn 1

        static readonly string[] NamePool =
        {
            "Ashford", "Brenham", "Calder", "Dunmoor", "Elmswick", "Fenwick", "Garrow",
            "Holt", "Ilford", "Jarrow", "Kesgrave", "Lynton", "Marsden", "Northwold",
            "Oakley", "Pendle", "Quarrend", "Rushmere", "Selby", "Tarn", "Ulverston",
            "Verwood", "Whitby", "Yarm"
        };
        static readonly string[] SuffixPool = { "Town", "City", "United", "Rovers", "Athletic", "County", "Albion", "Wanderers" };

        public SeasonCalendar(GameConfig cfg) { _cfg = cfg; }

        public LeagueClub PlayerClub => Clubs[PlayerIndex];
        public IReadOnlyList<Fixture> Turns => _turns;

        /// <summary>Total turns in a season = matchdays + international breaks.</summary>
        public int TurnsPerSeason => _turns.Count;

        public Fixture FixtureForTurn(int seasonTurn)
        {
            int i = seasonTurn - 1;
            if (i < 0 || i >= _turns.Count) return null;
            return _turns[i];
        }

        public void BuildSeason(string playerClubName, float playerStrength, int season, IRng rng)
        {
            Season = season;
            Clubs.Clear();
            _rounds.Clear();
            _turns.Clear();

            // --- clubs ---
            PlayerIndex = rng.Range(0, ClubCount);
            var usedNames = new HashSet<string>();
            for (int i = 0; i < ClubCount; i++)
            {
                if (i == PlayerIndex)
                {
                    Clubs.Add(new LeagueClub { Name = playerClubName, Strength = MathX.Clamp(playerStrength, 0.05f, 0.95f), IsPlayer = true });
                    continue;
                }

                string name;
                do
                {
                    name = NamePool[rng.Range(0, NamePool.Length)] + " " + SuffixPool[rng.Range(0, SuffixPool.Length)];
                } while (!usedNames.Add(name));

                // Spread opponent strength across a believable division.
                float t = (i + 0.5f) / ClubCount;
                float strength = MathX.Clamp(0.28f + 0.5f * t + (float)(rng.NextDouble() - 0.5) * 0.12f, 0.1f, 0.92f);
                Clubs.Add(new LeagueClub { Name = name, Strength = strength });
            }

            BuildRoundRobin(rng);
            BuildTurnCalendar();
        }

        void BuildRoundRobin(IRng rng)
        {
            // Circle method for a single round-robin (ClubCount even => ClubCount-1 rounds).
            int n = ClubCount;
            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;

            var single = new List<(int, int)[]>();
            for (int round = 0; round < n - 1; round++)
            {
                var pairs = new (int, int)[n / 2];
                for (int i = 0; i < n / 2; i++)
                {
                    int a = order[i];
                    int b = order[n - 1 - i];
                    // Alternate home/away by round so it isn't lopsided.
                    pairs[i] = (round % 2 == 0) ? (a, b) : (b, a);
                }
                single.Add(pairs);

                // rotate all but the first
                int last = order[n - 1];
                for (int i = n - 1; i > 1; i--) order[i] = order[i - 1];
                order[1] = last;
            }

            _rounds.AddRange(single);
            // Reverse fixtures for the second half of the season.
            foreach (var round in single)
            {
                var mirrored = new (int, int)[round.Length];
                for (int i = 0; i < round.Length; i++)
                    mirrored[i] = (round[i].Item2, round[i].Item1);
                _rounds.Add(mirrored);
            }
        }

        void BuildTurnCalendar()
        {
            var breaks = new HashSet<int>(_cfg.InternationalBreakTurns);
            int matchday = 0;
            int turn = 1;
            int totalTurns = _cfg.LeagueMatchdays + _cfg.InternationalBreakTurns.Length;

            while (turn <= totalTurns)
            {
                if (breaks.Contains(turn))
                {
                    _turns.Add(new Fixture { Turn = turn, IsInternationalBreak = true, Opponent = "" });
                }
                else
                {
                    var fx = FixtureForPlayer(matchday);
                    fx.Turn = turn;
                    _turns.Add(fx);
                    matchday++;
                }
                turn++;
            }
        }

        Fixture FixtureForPlayer(int matchday)
        {
            var round = _rounds[matchday % _rounds.Count];
            foreach (var (home, away) in round)
            {
                if (home == PlayerIndex || away == PlayerIndex)
                {
                    bool playerHome = home == PlayerIndex;
                    int oppIndex = playerHome ? away : home;
                    return new Fixture
                    {
                        Opponent = Clubs[oppIndex].Name,
                        OpponentIndex = oppIndex,
                        Home = playerHome,
                        OpponentStrength = Clubs[oppIndex].Strength,
                        Importance = DecideImportance(oppIndex)
                    };
                }
            }
            // Should never happen with a valid schedule.
            return new Fixture { Opponent = "TBC", Home = true, OpponentStrength = 0.5f };
        }

        FixtureImportance DecideImportance(int oppIndex)
        {
            // Slice 1: the two strongest opponents are "big matches"; the single
            // closest-strength club is treated as the local "derby".
            var opp = Clubs[oppIndex];
            float gap = Math.Abs(opp.Strength - PlayerClub.Strength);
            if (gap < 0.05f) return FixtureImportance.Derby;
            if (opp.Strength > 0.78f) return FixtureImportance.BigMatch;
            return FixtureImportance.Normal;
        }

        /// <summary>Advance the rest of the division for the given season turn (no-op on a break).</summary>
        public void SimulateOtherFixtures(int seasonTurn, IRng rng, MatchSimulator sim)
        {
            var fx = FixtureForTurn(seasonTurn);
            if (fx == null || fx.IsMatchless) return;

            // Which round is this? (count non-break turns up to here)
            int matchday = 0;
            for (int t = 1; t < seasonTurn; t++)
            {
                var f = FixtureForTurn(t);
                if (f != null && !f.IsMatchless) matchday++;
            }

            var round = _rounds[matchday % _rounds.Count];
            foreach (var (home, away) in round)
            {
                if (home == PlayerIndex || away == PlayerIndex) continue;
                var h = Clubs[home];
                var a = Clubs[away];
                var r = sim.Simulate(h.Strength, 0f, new Fixture { Home = true, OpponentStrength = a.Strength }, rng);
                h.Record(r.GoalsFor, r.GoalsAgainst);
                a.Record(r.GoalsAgainst, r.GoalsFor);
            }
        }

        public void RecordPlayerResult(Fixture fixture, MatchResult result)
        {
            PlayerClub.Record(result.GoalsFor, result.GoalsAgainst);
            if (fixture != null && fixture.OpponentIndex >= 0)
                Clubs[fixture.OpponentIndex].Record(result.GoalsAgainst, result.GoalsFor);
        }

        /// <summary>League table, best first.</summary>
        public List<LeagueClub> Standings()
        {
            var list = new List<LeagueClub>(Clubs);
            list.Sort((a, b) =>
            {
                int c = b.Points.CompareTo(a.Points);
                if (c != 0) return c;
                c = b.GoalDifference.CompareTo(a.GoalDifference);
                if (c != 0) return c;
                return b.GoalsFor.CompareTo(a.GoalsFor);
            });
            return list;
        }

        /// <summary>The player's next <paramref name="count"/> fixtures from (and including) a turn.</summary>
        public List<Fixture> UpcomingFixtures(int fromTurn, int count)
        {
            var list = new List<Fixture>();
            for (int t = fromTurn; t <= _turns.Count && list.Count < count; t++)
            {
                var fx = FixtureForTurn(t);
                if (fx != null) list.Add(fx);
            }
            return list;
        }

        public int PlayerPosition()
        {
            int pos = 1;
            var me = PlayerClub;
            foreach (var c in Clubs)
            {
                if (ReferenceEquals(c, me)) continue;
                if (c.Points > me.Points
                    || (c.Points == me.Points && c.GoalDifference > me.GoalDifference)
                    || (c.Points == me.Points && c.GoalDifference == me.GoalDifference && c.GoalsFor > me.GoalsFor))
                    pos++;
            }
            return pos;
        }

        public static string Ordinal(int n)
        {
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }
    }
}
