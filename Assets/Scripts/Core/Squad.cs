using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public enum PlayerArchetype
    {
        Talisman,
        Keeper,
        Captain,
        Wonderkid,
        Crock,             // injury-prone
        DeadlineDaySigning
    }

    public enum PlayerForm { Cold = -2, Poor = -1, Steady = 0, Sharp = 1, OnFire = 2 }

    public sealed class Player
    {
        public string Name;
        public string Position;
        public PlayerArchetype Archetype;
        public float Rating;          // 0.40 .. 0.92
        public int Age;
        public bool InjuryProne;
        public int ContractYears;
        public bool International;

        // dynamic
        public int WeeksOut;          // 0 = fit
        public PlayerForm Form = PlayerForm.Steady;
        public bool OnInternationalDuty;

        public bool IsFit => WeeksOut == 0;

        public bool IsKey =>
            Archetype == PlayerArchetype.Talisman ||
            Archetype == PlayerArchetype.Captain ||
            Rating >= 0.74f;

        public string StatusLine
        {
            get
            {
                if (WeeksOut > 0) return $"Out {WeeksOut} wk" + (WeeksOut == 1 ? "" : "s");
                if (OnInternationalDuty) return "On int'l duty";
                switch (Form)
                {
                    case PlayerForm.OnFire: return "On fire";
                    case PlayerForm.Sharp: return "In form";
                    case PlayerForm.Poor: return "Out of form";
                    case PlayerForm.Cold: return "Cold";
                    default: return "Fit";
                }
            }
        }
    }

    /// <summary>The ~6 notable players the podcast talks about (spec §11).</summary>
    public sealed class Squad
    {
        public readonly List<Player> Players = new List<Player>();

        // Deliberately invented — not meant to resemble real players.
        static readonly string[] First =
        {
            "Casper", "Milo", "Dane", "Ferran", "Otis", "Vince", "Rory", "Nias", "Elian",
            "Brant", "Kofi", "Teo", "Lars", "Shay", "Marek", "Devon", "Amari", "Jools"
        };
        static readonly string[] Last =
        {
            "Vance", "Okoro", "Priddy", "Halden", "Marsh", "Kallio", "Bomani", "Restrup",
            "Crowe", "Fenn", "Sekou", "Thorn", "Wray", "Bakke", "Mercer", "Voss"
        };
        static readonly string[] Positions = { "GK", "CB", "LB", "RB", "CDM", "CM", "CAM", "LW", "RW", "ST" };

        public static Squad Generate(float teamStrength, IRng rng)
        {
            var squad = new Squad();
            var usedNames = new HashSet<string>();
            var usedPos = new HashSet<string>();

            foreach (PlayerArchetype a in Enum.GetValues(typeof(PlayerArchetype)))
            {
                string name;
                do { name = First[rng.Range(0, First.Length)] + " " + Last[rng.Range(0, Last.Length)]; }
                while (!usedNames.Add(name));

                string pos = PositionFor(a, usedPos, rng);
                usedPos.Add(pos);

                float baseRating = MathX.Clamp(teamStrength + 0.10f + (float)(rng.NextDouble() - 0.5) * 0.14f, 0.42f, 0.92f);
                float rating = a switch
                {
                    PlayerArchetype.Talisman => MathX.Clamp(baseRating + 0.08f, 0.5f, 0.93f),
                    PlayerArchetype.Wonderkid => MathX.Clamp(baseRating - 0.06f, 0.42f, 0.82f),
                    PlayerArchetype.DeadlineDaySigning => MathX.Clamp(baseRating - 0.02f, 0.42f, 0.9f),
                    _ => baseRating
                };

                squad.Players.Add(new Player
                {
                    Name = name,
                    Position = pos,
                    Archetype = a,
                    Rating = rating,
                    Age = AgeFor(a, rng),
                    InjuryProne = a == PlayerArchetype.Crock || rng.NextDouble() < 0.15,
                    ContractYears = a == PlayerArchetype.Talisman && rng.NextDouble() < 0.5 ? 1 : rng.Range(2, 5),
                    International = a == PlayerArchetype.Talisman || a == PlayerArchetype.Captain || rng.NextDouble() < 0.4
                });
            }
            return squad;
        }

        static string PositionFor(PlayerArchetype a, HashSet<string> used, IRng rng)
        {
            if (a == PlayerArchetype.Keeper) return "GK";
            string[] pool = a switch
            {
                PlayerArchetype.Talisman => new[] { "ST", "CAM", "LW", "RW" },
                PlayerArchetype.Captain => new[] { "CB", "CDM", "CM" },
                PlayerArchetype.Wonderkid => new[] { "CAM", "LW", "RW", "ST", "CM" },
                _ => Positions
            };
            for (int tries = 0; tries < 12; tries++)
            {
                string p = pool[rng.Range(0, pool.Length)];
                if (p != "GK" && !used.Contains(p)) return p;
            }
            return pool[rng.Range(0, pool.Length)];
        }

        static int AgeFor(PlayerArchetype a, IRng rng) => a switch
        {
            PlayerArchetype.Wonderkid => rng.Range(17, 21),
            PlayerArchetype.Captain => rng.Range(28, 34),
            PlayerArchetype.Talisman => rng.Range(23, 30),
            _ => rng.Range(21, 32)
        };

        public Player Get(PlayerArchetype a) => Players.Find(p => p.Archetype == a);

        /// <summary>Strength lost this week from unavailable key players (feeds the match sim).</summary>
        public float KeyOut()
        {
            float sum = 0f;
            foreach (var p in Players)
                if (!p.IsFit && p.IsKey)
                    sum += 0.035f + Math.Max(0f, p.Rating - 0.60f) * 0.14f;
            return MathX.Clamp(sum, 0f, 0.20f);
        }

        public int KeyPlayersOut()
        {
            int n = 0;
            foreach (var p in Players)
                if (!p.IsFit && p.IsKey) n++;
            return n;
        }

        /// <summary>Roll injuries, returns, call-ups and form swings. Returns headline-worthy news.</summary>
        public List<string> AdvanceWeek(bool internationalBreak, bool fixtureCongestion, IRng rng)
        {
            var news = new List<string>();

            foreach (var p in Players)
            {
                p.OnInternationalDuty = false;

                if (p.WeeksOut > 0)
                {
                    p.WeeksOut--;
                    if (p.WeeksOut == 0)
                        news.Add($"{p.Name} is back from injury.");
                    continue;
                }

                float injuryChance = 0.028f
                                     + (p.InjuryProne ? 0.045f : 0f)
                                     + (fixtureCongestion ? 0.02f : 0f);
                if (rng.NextDouble() < injuryChance)
                {
                    p.WeeksOut = rng.Range(1, 6) + (p.InjuryProne ? rng.Range(0, 3) : 0);
                    p.Form = PlayerForm.Steady;
                    string sev = p.WeeksOut >= 6 ? "a serious injury" : p.WeeksOut >= 3 ? "an injury" : "a knock";
                    news.Add($"{p.Name} ({p.Position}) picks up {sev} — out {p.WeeksOut} week{(p.WeeksOut == 1 ? "" : "s")}.");
                    continue;
                }

                if (internationalBreak && p.International)
                {
                    p.OnInternationalDuty = true;
                    if (p.Archetype == PlayerArchetype.Wonderkid)
                        news.Add($"{p.Name} earns a first senior international call-up.");
                }

                if (rng.NextDouble() < 0.28)
                {
                    var was = p.Form;
                    int dir = rng.NextDouble() < 0.5 ? -1 : 1;
                    p.Form = (PlayerForm)MathX.ClampInt((int)p.Form + dir, -2, 2);
                    if (p.Form == PlayerForm.OnFire && was != PlayerForm.OnFire)
                        news.Add($"{p.Name} is in superb form.");
                    else if (p.Form == PlayerForm.Cold && was != PlayerForm.Cold)
                        news.Add($"{p.Name}'s form has fallen off a cliff.");
                }
            }

            return news;
        }

        /// <summary>Between seasons: heal everyone, age on, small rating drift.</summary>
        public void Offseason(IRng rng)
        {
            foreach (var p in Players)
            {
                p.WeeksOut = 0;
                p.Form = PlayerForm.Steady;
                p.Age++;
                float drift = p.Age > 31 ? -0.02f : p.Age < 23 ? 0.02f : 0f;
                p.Rating = MathX.Clamp(p.Rating + drift + (float)(rng.NextDouble() - 0.5) * 0.02f, 0.40f, 0.94f);
                if (p.ContractYears > 0) p.ContractYears--;
            }
        }
    }
}
