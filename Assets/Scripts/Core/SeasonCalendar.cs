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
    /// calendar. Slice 3 splices in a cup run, a European league phase (when qualified),
    /// international breaks and a short playable offseason (spec §2).
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

        // --- competition state for the current season ---
        public bool InEurope { get; private set; }
        public bool CupAlive { get; private set; }
        public int CupRoundReached { get; private set; }     // rounds won
        public bool WonCup { get; private set; }
        public int EuropeWins { get; private set; }
        public int EuropeDraws { get; private set; }
        public int EuropeLosses { get; private set; }
        public bool EuropePhaseDone { get; private set; }
        public bool WonEurope { get; private set; }

        float[] _cupOpp;
        string[] _cupOppName;
        float[] _euroOpp;
        string[] _euroOppName;
        int _cupRounds;

        static readonly string[] NamePool =
        {
            "Ashford", "Brenham", "Calder", "Dunmoor", "Elmswick", "Fenwick", "Garrow",
            "Holt", "Ilford", "Jarrow", "Kesgrave", "Lynton", "Marsden", "Northwold",
            "Oakley", "Pendle", "Quarrend", "Rushmere", "Selby", "Tarn", "Ulverston",
            "Verwood", "Whitby", "Yarm"
        };
        static readonly string[] SuffixPool = { "Town", "City", "United", "Rovers", "Athletic", "County", "Albion", "Wanderers" };
        static readonly string[] EuroPool =
        {
            "FK Rijnaas", "Athletic Bornova", "SC Malbork", "Real Candido", "Valeren IF",
            "Dynamo Krosin", "Sporting Aveiras", "US Chambre", "Ashkoy SK", "Gornik Walbra",
            "Hellas Verano", "FC Trnava", "Olympiakos Nera", "Rode Ster Brss"
        };
        static readonly string[] CupRoundNames = { "Third round", "Fourth round", "Quarter-final", "Semi-final", "Final" };

        public SeasonCalendar(GameConfig cfg) { _cfg = cfg; }

        public LeagueClub PlayerClub => Clubs[PlayerIndex];
        public IReadOnlyList<Fixture> Turns => _turns;

        public int TurnsPerSeason => _turns.Count;

        public Fixture FixtureForTurn(int seasonTurn)
        {
            int i = seasonTurn - 1;
            if (i < 0 || i >= _turns.Count) return null;
            var stored = _turns[i];

            // Cup ties are resolved live: a knocked-out club just has a quiet weekend.
            if (stored.Competition == Competition.Cup && stored.CupRound > 0)
            {
                if (!CupAlive || stored.CupRound <= CupRoundReached)
                    return new Fixture { Turn = stored.Turn, Competition = Competition.Cup, IsCupByeWeek = true, Opponent = "" };

                int round = CupRoundReached + 1;
                bool home = (round % 2) == 1;
                return new Fixture
                {
                    Turn = stored.Turn,
                    Competition = Competition.Cup,
                    CupRound = round,
                    Opponent = _cupOppName[round - 1],
                    Home = home,
                    OpponentStrength = _cupOpp[round - 1],
                    Importance = round == _cupRounds ? FixtureImportance.Final
                               : round >= _cupRounds - 1 ? FixtureImportance.BigMatch
                               : FixtureImportance.Normal
                };
            }

            return stored;
        }

        // ------------------------------------------------------------------
        public void BuildSeason(string playerClubName, float playerStrength, int season, IRng rng, bool inEurope = false)
        {
            Season = season;
            Clubs.Clear();
            _rounds.Clear();
            _turns.Clear();

            InEurope = inEurope;
            CupAlive = true;
            CupRoundReached = 0;
            WonCup = false;
            EuropeWins = EuropeDraws = EuropeLosses = 0;
            EuropePhaseDone = false;
            WonEurope = false;
            _cupRounds = _cfg.CupAfterMatchday.Length;

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

                float t = (i + 0.5f) / ClubCount;
                float strength = MathX.Clamp(0.28f + 0.5f * t + (float)(rng.NextDouble() - 0.5) * 0.12f, 0.1f, 0.92f);
                Clubs.Add(new LeagueClub { Name = name, Strength = strength });
            }

            // --- cup & european opponents ---
            _cupOpp = new float[_cupRounds];
            _cupOppName = new string[_cupRounds];
            for (int r = 0; r < _cupRounds; r++)
            {
                _cupOpp[r] = MathX.Clamp(_cfg.CupOpponentBase + r * _cfg.CupOpponentStep
                                         + (float)(rng.NextDouble() - 0.5) * 0.10f, 0.15f, 0.95f);
                _cupOppName[r] = NamePool[rng.Range(0, NamePool.Length)] + " " + SuffixPool[rng.Range(0, SuffixPool.Length)];
            }

            int euroGames = _cfg.EuropeAfterMatchday.Length;
            _euroOpp = new float[euroGames];
            _euroOppName = new string[euroGames];
            var euroUsed = new HashSet<string>();
            for (int g = 0; g < euroGames; g++)
            {
                _euroOpp[g] = MathX.Clamp(_cfg.EuropeOpponentBase + (float)(rng.NextDouble() - 0.5) * _cfg.EuropeOpponentSpread, 0.2f, 0.95f);
                string n;
                do { n = EuroPool[rng.Range(0, EuroPool.Length)]; } while (!euroUsed.Add(n) && euroUsed.Count < EuroPool.Length);
                _euroOppName[g] = n;
            }

            BuildRoundRobin(rng);
            BuildTurnCalendar();
        }

        void BuildRoundRobin(IRng rng)
        {
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
                    pairs[i] = (round % 2 == 0) ? (a, b) : (b, a);
                }
                single.Add(pairs);

                int last = order[n - 1];
                for (int i = n - 1; i > 1; i--) order[i] = order[i - 1];
                order[1] = last;
            }

            _rounds.AddRange(single);
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
            var cup = new HashSet<int>(_cfg.CupAfterMatchday);
            var euro = new HashSet<int>(_cfg.EuropeAfterMatchday);

            int turn = 1;
            int cupSlot = 0;
            int euroSlot = 0;

            for (int md = 1; md <= _cfg.LeagueMatchdays; md++)
            {
                var fx = FixtureForPlayer(md - 1);
                fx.Turn = turn++;
                fx.LeagueRound = md - 1;
                fx.Competition = Competition.League;
                _turns.Add(fx);

                if (InEurope && euro.Contains(md))
                {
                    euroSlot++;
                    bool home = (euroSlot % 2) == 1;
                    _turns.Add(new Fixture
                    {
                        Turn = turn++,
                        Competition = Competition.European,
                        EuropeGame = euroSlot,
                        Opponent = _euroOppName[euroSlot - 1],
                        Home = home,
                        OpponentStrength = _euroOpp[euroSlot - 1],
                        Importance = FixtureImportance.BigMatch
                    });
                }

                if (cup.Contains(md))
                {
                    cupSlot++;
                    _turns.Add(new Fixture
                    {
                        Turn = turn++,
                        Competition = Competition.Cup,
                        CupRound = cupSlot   // "the Nth cup weekend" — resolved live in FixtureForTurn
                    });
                }

                if (breaks.Contains(md))
                    _turns.Add(new Fixture { Turn = turn++, IsInternationalBreak = true, Opponent = "" });
            }

            for (int i = 0; i < _cfg.OffseasonTurns; i++)
                _turns.Add(new Fixture { Turn = turn++, IsOffseason = true, Opponent = "" });
        }

        Fixture FixtureForPlayer(int round)
        {
            var r = _rounds[round % _rounds.Count];
            foreach (var (home, away) in r)
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
            return new Fixture { Opponent = "TBC", Home = true, OpponentStrength = 0.5f };
        }

        FixtureImportance DecideImportance(int oppIndex)
        {
            var opp = Clubs[oppIndex];
            float gap = Math.Abs(opp.Strength - PlayerClub.Strength);
            if (gap < 0.05f) return FixtureImportance.Derby;
            if (opp.Strength > 0.78f) return FixtureImportance.BigMatch;
            return FixtureImportance.Normal;
        }

        // ------------------------------------------------------------------
        /// <summary>Advance the rest of the division for the given season turn (league weeks only).</summary>
        public void SimulateOtherFixtures(int seasonTurn, IRng rng, MatchSimulator sim)
        {
            var fx = FixtureForTurn(seasonTurn);
            if (fx == null || fx.Competition != Competition.League || fx.LeagueRound < 0) return;

            var round = _rounds[fx.LeagueRound % _rounds.Count];
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
            if (fixture == null) return;

            switch (fixture.Competition)
            {
                case Competition.League:
                    PlayerClub.Record(result.GoalsFor, result.GoalsAgainst);
                    if (fixture.OpponentIndex >= 0)
                        Clubs[fixture.OpponentIndex].Record(result.GoalsAgainst, result.GoalsFor);
                    break;

                case Competition.Cup:
                    if (result.Outcome == MatchOutcome.Loss)
                    {
                        CupAlive = false;
                    }
                    else
                    {
                        CupRoundReached = fixture.CupRound;
                        if (fixture.CupRound >= _cupRounds) { WonCup = true; CupAlive = false; }
                    }
                    break;

                case Competition.European:
                    if (result.Outcome == MatchOutcome.Win) EuropeWins++;
                    else if (result.Outcome == MatchOutcome.Draw) EuropeDraws++;
                    else EuropeLosses++;
                    if (fixture.EuropeGame >= _euroOpp.Length)
                    {
                        EuropePhaseDone = true;
                        WonEurope = EuropeWins * 3 + EuropeDraws >= 14;
                    }
                    break;
            }
        }

        /// <summary>Did the player earn a European place for next season?</summary>
        public bool QualifiesForEurope(int position)
            => WonCup || WonEurope || position <= _cfg.EuropeQualifyPosition + 2;

        public string RoundName(Fixture fx)
        {
            if (fx == null) return "";
            if (fx.Competition == Competition.Cup && fx.CupRound >= 1 && fx.CupRound <= CupRoundNames.Length)
                return "Cup · " + CupRoundNames[fx.CupRound - 1];
            if (fx.Competition == Competition.European)
                return $"Europe · night {fx.EuropeGame}";
            return "";
        }

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

        public int PlayerPosition() => PositionOf(PlayerIndex);

        public int PositionOf(int clubIndex)
        {
            if (clubIndex < 0 || clubIndex >= Clubs.Count) return Clubs.Count;
            int pos = 1;
            var me = Clubs[clubIndex];
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
