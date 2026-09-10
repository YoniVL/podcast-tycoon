using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public sealed class RunSetup
    {
        public string PodcastName = "The Untitled Pod";
        public string ClubName = "Rovers";
        public string ColourPrimary = "#2F6DB5";
        public string ColourSecondary = "#F2C14E";
        public Difficulty Difficulty = Difficulty.Regular;
    }

    public sealed class MilestoneEvent
    {
        public int Listeners;
        public bool IsPrimaryGoal;
        public string Message;
    }

    /// <summary>
    /// Orchestrates the weekly loop (spec §3): begin week (sim the match, build context and
    /// the topic offer) → the player builds a <see cref="ProductionPlan"/> → publish.
    /// The engine owns all state mutation; the presentation layer only reads and calls in.
    /// </summary>
    public sealed class Engine
    {
        public GameConfig Config { get; }
        public GameState State { get; }
        public SeasonCalendar Calendar { get; }

        readonly IRng _rng;
        readonly MatchSimulator _match;
        readonly Resolution _resolution;

        public Squad Roster { get; }
        public ThreadManager Threads { get; }
        public EventManager Events { get; }

        public WeekContext CurrentWeek { get; private set; }
        public IReadOnlyList<Topic> Offer { get; private set; } = Array.Empty<Topic>();
        public bool HasRedrawnThisWeek { get; private set; }

        public event Action<MilestoneEvent> MilestoneReached;
        public event Action<string> GameOver;      // reason
        public event Action GoalReached;
        public event Action<int> SeasonRolledOver; // new season number
        public event Action<ThreadEvent> ThreadOpened;
        public event Action<ThreadEvent> ThreadResolved;

        public Engine(RunSetup setup, GameConfig config, IRng rng)
        {
            Config = config ?? new GameConfig();
            _rng = rng ?? new SystemRng();
            _match = new MatchSimulator(Config);
            _resolution = new Resolution(Config);

            var profile = DifficultyProfile.For(setup.Difficulty);
            State = new GameState
            {
                PodcastName = setup.PodcastName,
                ClubName = setup.ClubName,
                ColourPrimary = setup.ColourPrimary,
                ColourSecondary = setup.ColourSecondary,
                Difficulty = setup.Difficulty,
                Money = Config.StartMoney,
                Listeners = Config.StartListeners,
                Reputation = Config.StartReputation,
                TeamStrength = profile.TeamStrength,
                PeakListeners = Config.StartListeners
            };

            Roster = Squad.Generate(State.TeamStrength, _rng);

            Threads = new ThreadManager(Config, _rng);
            Threads.ThreadOpened += e => ThreadOpened?.Invoke(e);
            Threads.ThreadResolved += e => ThreadResolved?.Invoke(e);
            Events = new EventManager(_rng);

            Calendar = new SeasonCalendar(Config);
            Calendar.BuildSeason(State.ClubName, State.TeamStrength, 1, _rng);
        }

        // ------------------------------------------------------------------
        // Begin week: "the week happens"
        // ------------------------------------------------------------------
        public WeekContext BeginWeek()
        {
            HasRedrawnThisWeek = false;

            var fixture = Calendar.FixtureForTurn(State.SeasonTurn);
            var ctx = new WeekContext
            {
                Turn = State.SeasonTurn,
                GlobalWeek = State.GlobalWeek,
                Season = State.Season,
                Fixture = fixture
            };

            bool isBreak = fixture == null || fixture.IsMatchless;
            ctx.SquadNews = Roster.AdvanceWeek(isBreak, fixtureCongestion: !isBreak, _rng);
            ctx.KeyPlayersOut = Roster.KeyPlayersOut();

            if (fixture != null && !fixture.IsMatchless)
            {
                Calendar.SimulateOtherFixtures(State.SeasonTurn, _rng, _match);
                ctx.Match = _match.Simulate(State.TeamStrength, Roster.KeyOut(), fixture, _rng);
                Calendar.RecordPlayerResult(fixture, ctx.Match);
            }

            int pos = Calendar.PlayerPosition();
            ctx.LeaguePosition = pos;
            ctx.LeaguePositionLabel = SeasonCalendar.Ordinal(pos);

            ContextResolver.Fill(ctx);
            Threads.Tick(this, ctx);

            CurrentWeek = ctx;
            Events.MaybeFire(this, ctx);
            Offer = BuildOffer(ctx);
            return ctx;
        }

        IReadOnlyList<Topic> BuildOffer(WeekContext ctx)
        {
            var offer = new List<Topic>();

            // Running story threads always get a slot — that's the story you're covering.
            foreach (var tt in ctx.ThreadTopics.Take(2))
                offer.Add(tt);

            var catalog = TopicCatalog.All.Where(t => t.IsAvailable(ctx, State)).ToList();
            Shuffle(catalog);

            // If there's room and no thread topic, guarantee the contextually strongest catalog topic.
            if (offer.Count == 0 && catalog.Count > 0)
            {
                Topic anchor = catalog
                    .OrderByDescending(t => t.BaseAppeal * ContextResolver.AppealMultiplier(t.Response, ctx))
                    .First();
                offer.Add(anchor);
                catalog.Remove(anchor);
            }

            foreach (var t in catalog)
            {
                if (offer.Count >= 3) break;
                offer.Add(t);
            }

            Shuffle(offer);
            return offer;
        }

        public bool TryRedraw()
        {
            if (HasRedrawnThisWeek) return false;
            if (State.Money < Config.RedrawCost && State.Buzz < 1) return false;

            if (State.Money >= Config.RedrawCost) State.Money -= Config.RedrawCost;
            else State.Buzz -= 1;

            HasRedrawnThisWeek = true;
            Offer = BuildOffer(CurrentWeek);
            return true;
        }

        public void ResolveEvent(int optionIndex) => Events.Resolve(this, optionIndex);

        // ------------------------------------------------------------------
        // Preview (no state change)
        // ------------------------------------------------------------------
        public EpisodeResult Preview(ProductionPlan plan) => _resolution.Project(State, CurrentWeek, plan);

        // ------------------------------------------------------------------
        // Publish: resolve, apply, advance
        // ------------------------------------------------------------------
        public EpisodeResult Publish(ProductionPlan plan)
        {
            var result = _resolution.Resolve(State, CurrentWeek, plan, _rng);

            if (plan.ThreadTopic?.SourceThread != null)
                Threads.MarkCovered(plan.ThreadTopic.SourceThread, result.Quality, plan.Stance);

            long listeners = (long)State.Listeners + result.ListenerDeltaActual;

            // Weekly drift from an unresolved event (e.g. ignoring a rival).
            if (State.WeeklyListenerDriftWeeks > 0)
            {
                listeners += (long)Math.Round(listeners * State.WeeklyListenerDrift);
                State.WeeklyListenerDriftWeeks--;
                if (State.WeeklyListenerDriftWeeks == 0) State.WeeklyListenerDrift = 0f;
            }

            listeners = Math.Max(0L, Math.Min(Config.MaxListeners, listeners));
            State.Listeners = (int)listeners;
            State.Reputation = MathX.Clamp(State.Reputation + result.ReputationDelta, 0f, 100f);
            State.Buzz += result.BuzzGained;
            State.Money += result.MoneyDelta;
            State.EpisodesPublished++;
            State.PeakListeners = Math.Max(State.PeakListeners, State.Listeners);
            State.ListenerHistory.Add(State.Listeners);
            State.Episodes.Add(new EpisodeRecord
            {
                GlobalWeek = State.GlobalWeek,
                Season = State.Season,
                TopicName = result.Topic.Name,
                QualityLabel = result.QualityLabel,
                Surprise = CurrentWeek.Surprise,
                Matchless = CurrentWeek.IsMatchless,
                ListenerDelta = result.ListenerDeltaActual,
                ReputationDelta = result.ReputationDelta,
                BuzzGained = result.BuzzGained
            });

            CheckFailStates();
            CheckMilestones();
            CheckGoal();

            AdvanceWeek();
            return result;
        }

        void CheckFailStates()
        {
            if (State.IsGameOver) return;

            if (State.Money < Config.BankruptcyFloor)
                State.ConsecutiveWeeksInDebt++;
            else
                State.ConsecutiveWeeksInDebt = 0;

            if (State.Listeners == 0)
                State.ConsecutiveWeeksNoAudience++;
            else
                State.ConsecutiveWeeksNoAudience = 0;

            if (State.ConsecutiveWeeksInDebt >= Config.BankruptcyGraceWeeks)
                EndRun($"The podcast ran out of money. {State.ClubName} will have to find another show.");
            else if (State.ConsecutiveWeeksNoAudience >= Config.ZeroAudienceGraceWeeks)
                EndRun("Nobody is listening any more. Time to call it.");
        }

        void EndRun(string reason)
        {
            State.IsGameOver = true;
            State.GameOverReason = reason;
            GameOver?.Invoke(reason);
        }

        void CheckMilestones()
        {
            while (State.NextMilestoneIndex < Config.Milestones.Length
                   && State.Listeners >= Config.Milestones[State.NextMilestoneIndex])
            {
                int value = Config.Milestones[State.NextMilestoneIndex];
                State.NextMilestoneIndex++;
                MilestoneReached?.Invoke(new MilestoneEvent
                {
                    Listeners = value,
                    IsPrimaryGoal = value >= Config.GoalListeners,
                    Message = MilestoneMessage(value)
                });
            }
        }

        string MilestoneMessage(int value)
        {
            if (value >= Config.GoalListeners) return "You made it. 50,000 listeners.";
            if (value >= 25000) return "25,000 listeners — the national media is quoting you.";
            if (value >= 10000) return "10,000 listeners — you're a fixture of the fanbase now.";
            if (value >= 2500) return "2,500 listeners — the numbers are getting serious.";
            if (value >= 1000) return "1,000 listeners — the club has started to notice.";
            if (value >= 500) return "500 listeners — real sponsors would talk to you now.";
            return "100 listeners — you have an actual audience.";
        }

        void CheckGoal()
        {
            if (!State.GoalReached && State.Listeners >= Config.GoalListeners)
            {
                State.GoalReached = true;
                GoalReached?.Invoke();
            }
        }

        void AdvanceWeek()
        {
            State.PrepPenaltyThisWeek = 0;
            State.GlobalWeek++;
            State.SeasonTurn++;

            if (State.SeasonTurn > Calendar.TurnsPerSeason)
            {
                RollOverSeason();
            }
        }

        void RollOverSeason()
        {
            State.Season++;
            State.SeasonTurn = 1;

            // Offseason audience churn.
            State.Listeners = Math.Max(0, MathX.RoundToInt(State.Listeners * (1f - Config.OffseasonChurn)));

            // Club drifts gently toward the middle of the division.
            float drift = Config.TeamStrengthSeasonDrift;
            if (State.TeamStrength > 0.50f) State.TeamStrength -= drift;
            else if (State.TeamStrength < 0.50f) State.TeamStrength += drift;

            Roster.Offseason(_rng);
            Threads.OnSeasonRollover();
            Calendar.BuildSeason(State.ClubName, State.TeamStrength, State.Season, _rng);
            SeasonRolledOver?.Invoke(State.Season);
        }

        // ------------------------------------------------------------------
        // Studio actions
        // ------------------------------------------------------------------
        public bool CanBuy(Gear gear) => !State.HasGear(gear) && State.Money >= GearCost(gear);

        public bool BuyGear(Gear gear)
        {
            if (!CanBuy(gear)) return false;
            State.Money -= GearCost(gear);
            State.Gear |= gear;
            return true;
        }

        public int GearCost(Gear gear)
        {
            switch (gear)
            {
                case Gear.XlrMic: return Config.MicCost;
                case Gear.AcousticPanels: return Config.PanelsCost;
                case Gear.EditingSoftware: return Config.EditingCost;
                default: return int.MaxValue;
            }
        }

        public bool CanHireCoHost() => !State.HasCoHost && State.Money >= Config.CoHostHireCost;

        public bool HireCoHost()
        {
            if (!CanHireCoHost()) return false;
            State.Money -= Config.CoHostHireCost;
            State.HasCoHost = true;
            return true;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
