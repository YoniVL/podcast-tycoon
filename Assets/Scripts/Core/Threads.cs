using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public enum ThreadKind
    {
        ManagerPressure,
        StarWantsOut,
        WonderkidWatch,
        AreWeGood
    }

    /// <summary>
    /// A multi-week story arc, seeded by game state (spec §8). It advances on results and on how
    /// well the player covers its weekly topic, then resolves with a narrative payoff and an effect.
    /// </summary>
    public sealed class StoryThread
    {
        public ThreadKind Kind;
        public string Subject;          // manager or player name
        public int Stage;               // 0-based
        public int WeeksInStage;
        public int WeeksSinceCovered = 99;
        public int TimesCovered;
        public float Momentum;          // -1..+1, toward the "good" outcome
        public bool Resolved;

        public int TotalWeeks;          // since the thread opened

        public string Title;            // current stage's topic title
        public string Note;             // one-line status shown in the match panel
        public Topic Topic;             // this week's topic (null between stages)

        // The fork: the two positions you can take, and how you've been calling it.
        public string StancePlusLabel = "";   // the +1 position
        public string StanceMinusLabel = "";  // the -1 position
        public string StanceQuestion = "";
        public float StanceScore;             // running sum of your positions; sign = your overall call
        public int LastStance;                // -1 / 0 / +1
        public string CallResult;             // "right" / "wrong" / "hedged" / "absent" — set on resolution

        public string OutcomeText;      // set on resolution

        public string Label => Kind switch
        {
            ThreadKind.ManagerPressure => "Manager under pressure",
            ThreadKind.StarWantsOut => $"{Subject}'s future",
            ThreadKind.WonderkidWatch => $"The {Subject} question",
            ThreadKind.AreWeGood => "Are we actually any good?",
            _ => "Story"
        };
    }

    public sealed class ThreadEvent
    {
        public ThreadKind Kind;
        public string Headline;
        public string Body;
        public bool IsResolution;
    }

    public sealed class ThreadManager
    {
        readonly GameConfig _cfg;
        readonly IRng _rng;

        public readonly List<StoryThread> Active = new List<StoryThread>();
        readonly List<MatchOutcome> _recent = new List<MatchOutcome>();
        readonly Dictionary<ThreadKind, int> _kindCooldown = new Dictionary<ThreadKind, int>();
        int _strongWinsThisSeason;
        int _cooldown;
        int _managerChanges;
        bool _wonderkidPeaked;

        public event Action<ThreadEvent> ThreadOpened;
        public event Action<ThreadEvent> ThreadResolved;

        static readonly string[] ManagerNames =
        {
            "Ray Datchett", "Enzo Ferioli", "Coen Bakhuis", "Miguel Arroyo", "Frank Sowerby",
            "Nikola Petrić", "Dougie Ferns", "Ola Halvard", "Terry Windass", "Sami El Amrani"
        };
        string _managerName;

        public ThreadManager(GameConfig cfg, IRng rng)
        {
            _cfg = cfg;
            _rng = rng;
            _managerName = ManagerNames[rng.Range(0, ManagerNames.Length)];
        }

        public string ManagerName => _managerName;

        public void OnSeasonRollover()
        {
            _recent.Clear();
            _strongWinsThisSeason = 0;
            Active.Clear();
            _cooldown = 2;
            _kindCooldown.Clear();
        }

        int CooldownFor(ThreadKind k) => _kindCooldown.TryGetValue(k, out var v) ? v : 0;

        // ------------------------------------------------------------------
        WeekContext _ctx;

        public void Tick(Engine engine, WeekContext ctx)
        {
            _ctx = ctx;

            if (ctx.Match != null)
            {
                _recent.Add(ctx.Match.Outcome);
                if (_recent.Count > 6) _recent.RemoveAt(0);
                if (ctx.Match.Outcome == MatchOutcome.Win && ctx.Fixture != null && ctx.Fixture.OpponentStrength >= 0.68f)
                    _strongWinsThisSeason++;
            }

            foreach (var t in Active.ToList())
                Advance(engine, ctx, t);
            Active.RemoveAll(t => t.Resolved);

            foreach (var k in _kindCooldown.Keys.ToList())
                if (_kindCooldown[k] > 0) _kindCooldown[k]--;

            if (_cooldown > 0) _cooldown--;
            else if (Active.Count < 2 && !ctx.IsMatchless)
                TrySpawn(engine, ctx);

            foreach (var t in Active)
            {
                if (t.Topic != null) ctx.ThreadTopics.Add(t.Topic);
                if (!string.IsNullOrEmpty(t.Note)) ctx.ThreadNotes.Add(t.Note);
                ctx.ActiveThreads.Add(t);
            }
        }

        /// <summary>
        /// Called from Engine.Publish when the player covered a thread topic with a stance.
        /// The football drives the outcome; your position nudges it, and — more importantly —
        /// it's on record when the story pays off.
        /// </summary>
        public void MarkCovered(StoryThread t, float quality, int stance)
        {
            t.WeeksSinceCovered = 0;
            t.TimesCovered++;
            t.LastStance = stance;

            float conviction = MathX.Clamp(quality, 0.4f, 1.6f);
            float push = stance * (0.055f + MathX.Clamp((quality - 0.95f) * 0.14f, -0.03f, 0.09f));
            t.Momentum = MathX.Clamp(t.Momentum + push, -1f, 1f);
            t.StanceScore += stance * conviction;
        }

        /// <summary>Score the player's call once the story has resolved. outcomeSign: +1 or -1.</summary>
        static void ApplyCall(StoryThread t, int outcomeSign, GameState st)
        {
            int call = Math.Sign(t.StanceScore);
            if (call != 0)
            {
                if (call == outcomeSign)
                {
                    st.Reputation = MathX.Clamp(st.Reputation + 4f, 0f, 100f);
                    st.Buzz += 5;
                    t.CallResult = "right";
                }
                else
                {
                    st.Reputation = MathX.Clamp(st.Reputation - 4f, 0f, 100f);
                    t.CallResult = "wrong";
                }
            }
            else if (t.TimesCovered == 0)
            {
                st.Reputation = MathX.Clamp(st.Reputation - 1f, 0f, 100f);
                t.CallResult = "absent";
            }
            else
            {
                t.CallResult = "hedged";
            }
        }

        static string CallLine(StoryThread t)
        {
            switch (t.CallResult)
            {
                case "right": return "You'd been calling it that way. Good shout.";
                case "wrong": return "You'd been arguing the opposite. Awkward.";
                case "absent": return "You sat this one out entirely.";
                default: return "";
            }
        }

        int Losses(int lastN)
        {
            int n = 0, seen = 0;
            for (int i = _recent.Count - 1; i >= 0 && seen < lastN; i--, seen++)
                if (_recent[i] == MatchOutcome.Loss) n++;
            return n;
        }

        int Wins(int lastN)
        {
            int n = 0, seen = 0;
            for (int i = _recent.Count - 1; i >= 0 && seen < lastN; i--, seen++)
                if (_recent[i] == MatchOutcome.Win) n++;
            return n;
        }

        bool HasThread(ThreadKind k) => Active.Any(t => t.Kind == k);

        // ------------------------------------------------------------------
        void TrySpawn(Engine engine, WeekContext ctx)
        {
            var st = engine.State;
            int position = ctx.LeaguePosition;
            int bump = Math.Min(_managerChanges, 2); // gets a bit harder to trigger after each change
            int lossGate = 3 + bump;
            int lossWindow = 5 + bump;

            // Manager under pressure — a bad run of results.
            if (!HasThread(ThreadKind.ManagerPressure) && CooldownFor(ThreadKind.ManagerPressure) == 0
                && _recent.Count >= lossWindow && Losses(lossWindow) >= lossGate)
            {
                Open(new StoryThread { Kind = ThreadKind.ManagerPressure, Subject = _managerName, Momentum = -0.15f },
                    $"{_managerName} under pressure",
                    $"A run of {Losses(lossWindow)} defeats has {st.ClubName}'s support turning on the manager.");
                return;
            }

            // Star wants out — the talisman, short contract, club struggling.
            var star = engine.Roster.Get(PlayerArchetype.Talisman);
            if (!HasThread(ThreadKind.StarWantsOut) && CooldownFor(ThreadKind.StarWantsOut) == 0
                && star != null && star.ContractYears <= 1
                && (position >= 11 || Losses(3) >= 2) && _rng.NextDouble() < 0.55)
            {
                Open(new StoryThread { Kind = ThreadKind.StarWantsOut, Subject = star.Name, Momentum = -0.10f },
                    $"{star.Name} linked with a move",
                    $"{star.Name} has a year left and the speculation has started.");
                return;
            }

            // Wonderkid watch — the kid is in form, and hasn't already "arrived".
            var kid = engine.Roster.Get(PlayerArchetype.Wonderkid);
            if (!HasThread(ThreadKind.WonderkidWatch) && CooldownFor(ThreadKind.WonderkidWatch) == 0
                && !_wonderkidPeaked && kid != null && kid.IsFit
                && (int)kid.Form >= (int)PlayerForm.Sharp && _rng.NextDouble() < 0.45)
            {
                Open(new StoryThread { Kind = ThreadKind.WonderkidWatch, Subject = kid.Name, Momentum = 0.0f },
                    $"Is {kid.Name} the real deal?",
                    $"{kid.Name} ({kid.Position}) has caught fire. The fanbase wants to believe.");
                return;
            }

            // Are we actually good — top six, two wins over strong sides.
            if (!HasThread(ThreadKind.AreWeGood) && CooldownFor(ThreadKind.AreWeGood) == 0
                && position <= 6 && _strongWinsThisSeason >= 2 && _rng.NextDouble() < 0.5)
            {
                Open(new StoryThread { Kind = ThreadKind.AreWeGood, Subject = st.ClubName, Momentum = 0.15f },
                    $"{st.ClubName} — flat-track bullies, or the real thing?",
                    "Two big scalps and a top-six place. Is this a genuine push or a false dawn?");
            }
        }

        void Open(StoryThread t, string headline, string body)
        {
            Active.Add(t);
            var e = new ThreadEvent { Kind = t.Kind, Headline = headline, Body = body };
            _ctx?.ThreadEvents.Add(e);
            ThreadOpened?.Invoke(e);
        }

        // ------------------------------------------------------------------
        void Advance(Engine engine, WeekContext ctx, StoryThread t)
        {
            t.WeeksInStage++;
            t.WeeksSinceCovered++;
            t.TotalWeeks++;
            t.Topic = null;

            if (ctx.Match != null)
                t.Momentum = MathX.Clamp(t.Momentum + ResultMomentum(t.Kind, ctx.Match.Outcome), -1f, 1f);

            switch (t.Kind)
            {
                case ThreadKind.ManagerPressure: AdvanceManager(engine, ctx, t); break;
                case ThreadKind.StarWantsOut: AdvanceStar(engine, ctx, t); break;
                case ThreadKind.WonderkidWatch: AdvanceKid(engine, ctx, t); break;
                case ThreadKind.AreWeGood: AdvanceGood(engine, ctx, t); break;
            }
        }

        static float ResultMomentum(ThreadKind k, MatchOutcome o)
        {
            switch (k)
            {
                case ThreadKind.ManagerPressure:
                    return o == MatchOutcome.Win ? 0.22f : o == MatchOutcome.Draw ? -0.02f : -0.16f;
                case ThreadKind.StarWantsOut:
                    return o == MatchOutcome.Win ? 0.14f : o == MatchOutcome.Draw ? 0f : -0.10f;
                case ThreadKind.WonderkidWatch:
                    return o == MatchOutcome.Win ? 0.16f : o == MatchOutcome.Draw ? 0.02f : -0.12f;
                case ThreadKind.AreWeGood:
                    return o == MatchOutcome.Win ? 0.18f : o == MatchOutcome.Draw ? -0.04f : -0.20f;
                default:
                    return 0f;
            }
        }

        Topic MakeTopic(StoryThread t, string name, string blurb, float appeal, int effort, float swing,
            float rep, int buzz, TopicResponse response)
        {
            return new Topic
            {
                Id = TopicId.Recap, // unused for thread topics
                Name = name,
                Blurb = blurb,
                BaseAppeal = appeal,
                Effort = effort,
                Swing = swing,
                RepEarn = rep,
                BuzzBonus = buzz,
                Response = response,
                IsAvailable = (c, s) => true,
                SourceThread = t
            };
        }

        void Resolve(StoryThread t, string headline, string body)
        {
            t.Resolved = true;
            t.OutcomeText = body;
            _cooldown = 3;
            _kindCooldown[t.Kind] = t.Kind == ThreadKind.ManagerPressure ? 13 : 8;
            var e = new ThreadEvent { Kind = t.Kind, Headline = headline, Body = body, IsResolution = true };
            _ctx?.ThreadEvents.Add(e);
            ThreadResolved?.Invoke(e);
        }

        // ---- Manager under pressure ----   (+1 = he stays, -1 = he goes)
        void AdvanceManager(Engine engine, WeekContext ctx, StoryThread t)
        {
            var st = engine.State;
            bool timeout = t.TotalWeeks >= 8;

            if (t.Momentum > 0.42f || (timeout && t.Momentum > -0.05f))
            {
                ApplyCall(t, +1, st);
                st.Buzz += 5;
                Resolve(t, $"{t.Subject} keeps his job",
                    $"The results turned and the board has backed {t.Subject}. {CallLine(t)}");
                return;
            }

            if (t.Momentum < -0.55f || timeout)
            {
                ApplyCall(t, -1, st);
                float swing = (float)(_rng.NextDouble() * 0.09 - 0.03); // -0.03 .. +0.06
                engine.State.TeamStrength = MathX.Clamp(engine.State.TeamStrength + swing, 0.1f, 0.95f);
                st.Buzz += 12;
                st.Listeners = (int)Math.Min(_cfg.MaxListeners, st.Listeners + (long)Math.Round(st.Listeners * 0.03));

                string old = _managerName;
                do { _managerName = ManagerNames[_rng.Range(0, ManagerNames.Length)]; }
                while (_managerName == old && ManagerNames.Length > 1);
                _managerChanges++;

                Resolve(t, $"{old} is sacked",
                    $"{engine.State.ClubName} have dismissed {old}. {_managerName} takes over — " +
                    (swing > 0.02f ? "an upgrade, on paper. " : swing < -0.01f ? "a gamble that could backfire. " : "a steady pair of hands. ")
                    + CallLine(t));
                return;
            }

            if (t.Stage == 0 && t.WeeksInStage >= 2) { t.Stage = 1; t.WeeksInStage = 0; }

            t.Note = t.Stage == 0
                ? $"{t.Subject} is one bad result from the sack."
                : $"The board is deciding {t.Subject}'s future this week.";
            t.Title = "The manager's future";
            SetFork(t, "Back the manager", "Time for a change", "How are you calling it?");
            t.Topic = MakeTopic(t, t.Title, "Take a side on the manager.",
                appeal: 1.55f, effort: 3, swing: 0.35f, rep: 1f, buzz: 3, response: TopicResponse.Crisis);
        }

        static void SetFork(StoryThread t, string plus, string minus, string question)
        {
            t.StancePlusLabel = plus;
            t.StanceMinusLabel = minus;
            t.StanceQuestion = question;
        }

        // ---- Star wants out ----   (+1 = he stays, -1 = cash in)
        void AdvanceStar(Engine engine, WeekContext ctx, StoryThread t)
        {
            var st = engine.State;
            var star = engine.Roster.Get(PlayerArchetype.Talisman);
            bool timeout = t.TotalWeeks >= 8;

            if (t.Momentum > 0.4f || (timeout && t.Momentum > 0.05f))
            {
                ApplyCall(t, +1, st);
                if (star != null) { star.ContractYears = 4; star.Rating = MathX.Clamp(star.Rating + 0.01f, 0.4f, 0.95f); }
                engine.State.TeamStrength = MathX.Clamp(engine.State.TeamStrength + 0.02f, 0.1f, 0.95f);
                st.Buzz += 8;
                Resolve(t, $"{t.Subject} signs a new deal",
                    $"{t.Subject} has committed his future to {st.ClubName}. The mood is transformed. {CallLine(t)}");
                return;
            }

            if (t.Momentum < -0.45f || timeout)
            {
                ApplyCall(t, -1, st);
                if (star != null)
                {
                    star.Rating = MathX.Clamp(star.Rating - 0.10f, 0.42f, 0.9f);
                    star.ContractYears = 4;
                    star.Name = RandomPlayerName(_rng);
                }
                engine.State.TeamStrength = MathX.Clamp(engine.State.TeamStrength - 0.05f, 0.1f, 0.95f);
                st.Buzz += 14;
                st.Listeners = (int)Math.Min(_cfg.MaxListeners, st.Listeners + (long)Math.Round(st.Listeners * 0.02));
                Resolve(t, $"{t.Subject} is sold",
                    $"{t.Subject} has left {st.ClubName}. A big cheque, a big hole in the team, and a lot for you to talk about. {CallLine(t)}");
                return;
            }

            if (t.Stage == 0 && t.WeeksInStage >= 3) { t.Stage = 1; t.WeeksInStage = 0; }

            t.Note = $"{t.Subject}'s future is unresolved.";
            t.Title = $"{t.Subject}'s future";
            SetFork(t, "We can't lose him", "Cash in while we can", "What should the club do?");
            t.Topic = MakeTopic(t, t.Title, "Take a side on the star.",
                appeal: 1.5f, effort: 4, swing: 0.4f, rep: 0f, buzz: 4, response: TopicResponse.Reaction);
        }

        // ---- Wonderkid watch ----
        void AdvanceKid(Engine engine, WeekContext ctx, StoryThread t)
        {
            var st = engine.State;
            var kid = engine.Roster.Get(PlayerArchetype.Wonderkid);
            bool timeout = t.TotalWeeks >= 8;

            if ((t.Momentum > 0.55f && t.WeeksInStage >= 3) || (timeout && t.Momentum > 0.2f))
            {
                ApplyCall(t, +1, st);
                if (kid != null)
                {
                    kid.Rating = MathX.Clamp(kid.Rating + 0.08f, 0.42f, 0.82f);
                    kid.Form = PlayerForm.Sharp;
                }
                _wonderkidPeaked = true;
                engine.State.TeamStrength = MathX.Clamp(engine.State.TeamStrength + 0.02f, 0.1f, 0.95f);
                st.Buzz += 8;
                st.Listeners = (int)Math.Min(_cfg.MaxListeners, st.Listeners + (long)Math.Round(st.Listeners * 0.02));
                Resolve(t, $"{t.Subject} has arrived",
                    $"{t.Subject} has kicked on and looks the real thing. {CallLine(t)}");
                return;
            }

            if (t.Momentum < -0.4f || timeout)
            {
                ApplyCall(t, -1, st);
                if (kid != null) kid.Form = PlayerForm.Poor;
                st.Buzz = Math.Max(0, st.Buzz - 3);
                Resolve(t, $"The {t.Subject} hype cools",
                    $"{t.Subject}'s form has dipped and the excitement has faded. {CallLine(t)}");
                return;
            }

            t.Note = $"The fanbase is watching {t.Subject} closely.";
            t.Title = $"The {t.Subject} question";
            SetFork(t, "He's the real deal", "Don't get carried away", "Where do you stand?");
            if (t.Stage == 0 && t.WeeksInStage >= 3) { t.Stage = 1; t.WeeksInStage = 0; }
            t.Topic = MakeTopic(t, t.Title, "Make the case, or pump the brakes.",
                appeal: 1.25f, effort: 4, swing: 0.3f, rep: 1f, buzz: 3, response: TopicResponse.Positive);
        }

        // ---- Are we actually good ----   (+1 = it's real, -1 = flat-track bullies)
        void AdvanceGood(Engine engine, WeekContext ctx, StoryThread t)
        {
            var st = engine.State;
            bool timeout = t.TotalWeeks >= 8;

            if ((t.Momentum > 0.5f && t.WeeksInStage >= 2) || (timeout && t.Momentum > 0.1f))
            {
                ApplyCall(t, +1, st);
                engine.State.TeamStrength = MathX.Clamp(engine.State.TeamStrength + 0.02f, 0.1f, 0.95f);
                st.Buzz += 6;
                st.Listeners = (int)Math.Min(_cfg.MaxListeners, st.Listeners + (long)Math.Round(st.Listeners * 0.03));
                Resolve(t, $"{st.ClubName} are the real thing",
                    $"The results kept coming. This is a genuine season. {CallLine(t)}");
                return;
            }

            if (t.Momentum < -0.3f || timeout)
            {
                ApplyCall(t, -1, st);
                st.Listeners = Math.Max(0, (int)(st.Listeners - Math.Round(st.Listeners * 0.015)));
                Resolve(t, "The bubble bursts",
                    $"A couple of bad weeks and the doubts are back. {st.ClubName} were flattering to deceive. {CallLine(t)}");
                return;
            }

            t.Note = "The \"are we good?\" question is still open.";
            t.Title = "Are we actually any good?";
            SetFork(t, "This is real", "Flat-track bullies", "Which is it?");
            t.Topic = MakeTopic(t, t.Title, "Take a side on whether this is real.",
                appeal: 1.2f, effort: 4, swing: 0.25f, rep: 1f, buzz: 2, response: TopicResponse.Reaction);
        }

        static readonly string[] Names =
        {
            "Casper Vance", "Milo Okoro", "Dane Priddy", "Ferran Halden", "Otis Marsh", "Vince Kallio",
            "Rory Bomani", "Nias Restrup", "Elian Crowe", "Brant Fenn", "Teo Thorn", "Lars Bakke"
        };
        static string RandomPlayerName(IRng rng) => Names[rng.Range(0, Names.Length)];
    }
}
