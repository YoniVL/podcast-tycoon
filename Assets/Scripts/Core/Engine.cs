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
        public RunModifiers Modifiers = new RunModifiers();
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
        public SponsorManager Sponsors { get; }
        public RivalTracker Rivals { get; }
        public AccessManager Access { get; }
        public ScoopManager Scoops { get; }
        public CardManager Cards { get; }

        public WeekContext CurrentWeek { get; private set; }
        public IReadOnlyList<Topic> Offer { get; private set; } = Array.Empty<Topic>();
        public bool HasRedrawnThisWeek { get; private set; }

        int _seasonStartListeners;
        float _seasonStartRep;

        public event Action<MilestoneEvent> MilestoneReached;
        public event Action<string> GameOver;
        public event Action GoalReached;
        public event Action<int> SeasonRolledOver;
        public event Action<SeasonSummary> SeasonReviewReady;
        public event Action<ThreadEvent> ThreadOpened;
        public event Action<ThreadEvent> ThreadResolved;
        public event Action<SponsorNews> SponsorResolved;
        public event Action BuyoutOffered;

        public Engine(RunSetup setup, GameConfig config, IRng rng)
        {
            Config = config ?? new GameConfig();
            _rng = rng ?? new SystemRng();
            _match = new MatchSimulator(Config);
            _resolution = new Resolution(Config);

            var profile = DifficultyProfile.For(setup.Difficulty);
            var mods = setup.Modifiers ?? new RunModifiers();
            State = new GameState
            {
                PodcastName = setup.PodcastName,
                ClubName = setup.ClubName,
                ColourPrimary = setup.ColourPrimary,
                ColourSecondary = setup.ColourSecondary,
                Difficulty = setup.Difficulty,
                Money = Config.StartMoney + (mods.NestEgg ? 300 : 0),
                Listeners = Config.StartListeners,
                Reputation = Config.StartReputation,
                Credibility = Config.StartCredibility,
                SocialReach = Config.StartSocialReach,
                TeamStrength = profile.TeamStrength,
                PeakListeners = Config.StartListeners,
                Modifiers = mods,
                IsCustomRun = mods.Any
            };

            if (mods.NoInternationalBreaks) Config.InternationalBreakTurns = Array.Empty<int>();

            Roster = Squad.Generate(State.TeamStrength, _rng);

            Threads = new ThreadManager(Config, _rng);
            Threads.ThreadOpened += e => ThreadOpened?.Invoke(e);
            Threads.ThreadResolved += e => ThreadResolved?.Invoke(e);
            Events = new EventManager(_rng);
            Sponsors = new SponsorManager(Config, _rng);
            Sponsors.Resolved += n => SponsorResolved?.Invoke(n);
            Rivals = new RivalTracker(_rng);
            Access = new AccessManager(Config, _rng);
            Scoops = new ScoopManager(Config, _rng);
            Cards = new CardManager(Config, _rng);

            Calendar = new SeasonCalendar(Config);
            Calendar.BuildSeason(State.ClubName, State.TeamStrength, 1, _rng);
            Rivals.OnSeasonStart(Calendar);
            State.InEuropeThisSeason = Calendar.InEurope;

            // A card in hand from the start, so the mechanic is visible in week one.
            State.Hand.Add("all_nighter");

            _seasonStartListeners = State.Listeners;
            _seasonStartRep = State.Reputation;
        }

        int GraceWeeks => State.Modifiers.LongRunway ? 5 : Config.BankruptcyGraceWeeks;

        // ------------------------------------------------------------------
        public WeekContext BeginWeek()
        {
            HasRedrawnThisWeek = false;
            State.CardsPlayedThisWeek = 0;
            State.CardReachMultThisWeek = 1f;
            State.CardPrepBonusThisWeek = 0;
            State.CardQualityBonusThisWeek = 0f;
            State.CardSocialBonusThisWeek = 0f;
            State.CardGuaranteeGoodRoll = false;

            // Social reach fades if you're not being talked about (spec §5).
            State.SocialReach = Math.Max(0f, State.SocialReach - Config.SocialDecayPerWeek);

            var fixture = Calendar.FixtureForTurn(State.SeasonTurn);
            Rivals.MarkFixture(fixture);

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
                MatchMoments.Emit(ctx, fixture, ctx.Match, Roster, _rng);
                if (fixture.Competition != Competition.League)
                    ApplyCompetitionResult(ctx, fixture);
            }

            int pos = Calendar.PlayerPosition();
            ctx.LeaguePosition = pos;
            ctx.LeaguePositionLabel = SeasonCalendar.Ordinal(pos);

            Access.Recompute(this, ctx);
            ContextResolver.Fill(ctx);
            Rivals.Tick(this, ctx);
            Threads.Tick(this, ctx);

            CurrentWeek = ctx;
            Events.MaybeFire(this, ctx);
            Scoops.Tick(this, ctx);
            Sponsors.Tick(this, ctx);
            Cards.BeginWeek(this, ctx);
            Offer = BuildOffer(ctx);
            return ctx;
        }

        void ApplyCompetitionResult(WeekContext ctx, Fixture fx)
        {
            var m = ctx.Match;
            if (fx.Competition == Competition.Cup)
            {
                if (m.Outcome == MatchOutcome.Loss)
                    ctx.CompetitionNote = $"Knocked out of the cup by {fx.Opponent}. That one stings.";
                else if (Calendar.WonCup)
                {
                    State.SocialReach = MathX.Clamp(State.SocialReach + Config.CupWinSocial, 0f, 100f);
                    State.Reputation = MathX.Clamp(State.Reputation + Config.CupWinReputation, 0f, 100f);
                    State.Listeners = (int)Math.Min(Config.MaxListeners, State.Listeners + (long)Math.Round(State.Listeners * 0.06));
                    State.CupsWon++;
                    ctx.CompetitionNote = "YOU'VE WON THE CUP. The city is bouncing — everyone wants the reaction episode.";
                }
                else
                    ctx.CompetitionNote = $"Through to the next round — {fx.Opponent} beaten.";
            }
            else if (fx.Competition == Competition.European)
            {
                string r = m.Outcome == MatchOutcome.Win ? "A European win" :
                           m.Outcome == MatchOutcome.Draw ? "A European draw" : "Beaten in Europe";
                ctx.CompetitionNote = $"{r} against {fx.Opponent} " +
                                      $"({Calendar.EuropeWins}W-{Calendar.EuropeDraws}D-{Calendar.EuropeLosses}L in the phase).";
                if (Calendar.EuropePhaseDone && Calendar.WonEurope)
                {
                    State.SocialReach = MathX.Clamp(State.SocialReach + Config.CupWinSocial, 0f, 100f);
                    State.Reputation = MathX.Clamp(State.Reputation + Config.CupWinReputation, 0f, 100f);
                    ctx.CompetitionNote += " You topped the group — a serious European run.";
                }
            }
        }

        IReadOnlyList<Topic> BuildOffer(WeekContext ctx)
        {
            var offer = new List<Topic>();

            var broken = Scoops.BrokenTopic;
            if (broken != null) offer.Add(broken);

            foreach (var tt in ctx.ThreadTopics.Take(2))
                offer.Add(tt);

            var catalog = TopicCatalog.All.Where(t => t.IsAvailable(ctx, State)).ToList();
            Shuffle(catalog);

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
            if (State.Money < Config.RedrawCost) return false;

            State.Money -= Config.RedrawCost;
            HasRedrawnThisWeek = true;
            Offer = BuildOffer(CurrentWeek);
            return true;
        }

        public void ResolveEvent(int optionIndex) => Events.Resolve(this, optionIndex);
        public void SignSponsor(int inboxIndex) => Sponsors.Sign(this, inboxIndex);

        public void ResolveScoop(ScoopChoice choice)
        {
            Scoops.Resolve(this, choice);
            if (Scoops.BrokenTopic != null) Offer = BuildOffer(CurrentWeek);
        }

        // ------------------------------------------------------------------
        public EpisodeResult Preview(ProductionPlan plan) => _resolution.Project(State, CurrentWeek, plan);

        public EpisodeResult Publish(ProductionPlan plan)
        {
            var result = _resolution.Resolve(State, CurrentWeek, plan, _rng);

            if (plan.ThreadTopic?.SourceThread != null)
                Threads.MarkCovered(plan.ThreadTopic.SourceThread, result.Quality, plan.Stance);

            Scoops.ConsumeBrokenTopic();

            long listeners = (long)State.Listeners + result.ListenerDeltaActual;

            if (State.WeeklyListenerDriftWeeks > 0)
            {
                listeners += (long)Math.Round(listeners * State.WeeklyListenerDrift);
                State.WeeklyListenerDriftWeeks--;
                if (State.WeeklyListenerDriftWeeks == 0) State.WeeklyListenerDrift = 0f;
            }

            listeners = Math.Max(0L, Math.Min(Config.MaxListeners, listeners));
            State.Listeners = (int)listeners;
            State.Reputation = MathX.Clamp(State.Reputation + result.ReputationDelta, 0f, 100f);
            State.Credibility = MathX.Clamp(State.Credibility + result.CredibilityDelta, 0f, 100f);
            State.SocialReach = MathX.Clamp(State.SocialReach + result.SocialGained, 0f, 100f);
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
                SocialGained = result.SocialGained
            });

            CheckFailStates();
            CheckMilestones();
            CheckGoal();

            AdvanceWeek();
            return result;
        }

        void CheckFailStates()
        {
            if (State.IsGameOver || State.Modifiers.Sandbox) return;

            if (State.Money < Config.BankruptcyFloor) State.ConsecutiveWeeksInDebt++;
            else State.ConsecutiveWeeksInDebt = 0;

            if (State.Listeners == 0) State.ConsecutiveWeeksNoAudience++;
            else State.ConsecutiveWeeksNoAudience = 0;

            if (State.ConsecutiveWeeksInDebt >= GraceWeeks)
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

                Cards.GrantMilestoneReward(this, value, CurrentWeek);

                if (value == Config.BuyoutListeners && !State.BuyoutResolved)
                {
                    State.BuyoutPending = true;
                    BuyoutOffered?.Invoke();
                }
            }
        }

        string MilestoneMessage(int value)
        {
            if (value >= 10_000_000) return "Ten million listeners. This is one of the biggest shows on the planet.";
            if (value >= 5_000_000) return "Five million. Broadcast networks are calling.";
            if (value >= 2_500_000) return "2.5 million listeners.";
            if (value >= 1_000_000) return "One million listeners. Someone wants to buy the whole thing.";
            if (value >= 500_000) return "Half a million listeners.";
            if (value >= 250_000) return "250,000 listeners — a genuine media outlet.";
            if (value >= 100_000) return "100,000 listeners.";
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

        // ------------------------------------------------------------------
        public bool AcceptBuyout()
        {
            if (!State.BuyoutPending) return false;
            State.Money += 250_000f;
            State.Reputation = MathX.Clamp(State.Reputation + 4f, 0f, 100f);
            State.BuyoutPending = false;
            State.BuyoutResolved = true;
            State.BuyoutAccepted = true;
            return true;
        }

        public void DeclineBuyout()
        {
            State.SocialReach = MathX.Clamp(State.SocialReach + 15f, 0f, 100f);
            State.Reputation = MathX.Clamp(State.Reputation + 3f, 0f, 100f);
            State.BuyoutPending = false;
            State.BuyoutResolved = true;
        }

        // ------------------------------------------------------------------
        void AdvanceWeek()
        {
            State.PrepPenaltyThisWeek = 0;
            State.GlobalWeek++;
            State.SeasonTurn++;

            if (State.SeasonTurn > Calendar.TurnsPerSeason)
                RollOverSeason();
        }

        void RollOverSeason()
        {
            int finalPos = Calendar.PlayerPosition();
            State.BestLeagueFinish = Math.Min(State.BestLeagueFinish, finalPos);
            bool euroNext = Calendar.QualifiesForEurope(finalPos);
            if (Calendar.WonEurope) State.EuropeanTrophies++;

            BuildSeasonSummary(finalPos);

            State.Season++;
            State.SeasonTurn = 1;

            float churn = Config.OffseasonChurn * (State.Modifiers.GentleChurn ? 0.8f : 1f);
            State.Listeners = Math.Max(0, MathX.RoundToInt(State.Listeners * (1f - churn)));

            float drift = Config.TeamStrengthSeasonDrift;
            if (State.TeamStrength > 0.50f) State.TeamStrength -= drift;
            else if (State.TeamStrength < 0.50f) State.TeamStrength += drift;

            Roster.Offseason(_rng);
            Threads.OnSeasonRollover();
            Sponsors.OnSeasonRollover();
            Scoops.OnSeasonRollover();
            Access.OnSeasonRollover();

            Calendar.BuildSeason(State.ClubName, State.TeamStrength, State.Season, _rng, inEurope: euroNext);
            Rivals.OnSeasonStart(Calendar);
            State.InEuropeThisSeason = euroNext;

            _seasonStartListeners = State.Listeners;
            _seasonStartRep = State.Reputation;

            SeasonRolledOver?.Invoke(State.Season);
            if (State.LastSeason != null) SeasonReviewReady?.Invoke(State.LastSeason);
        }

        void BuildSeasonSummary(int finalPos)
        {
            var s = new SeasonSummary
            {
                Season = State.Season,
                LeaguePosition = finalPos,
                LeaguePositionLabel = SeasonCalendar.Ordinal(finalPos),
                WonCup = Calendar.WonCup,
                WonEurope = Calendar.WonEurope,
                PlayedInEurope = Calendar.InEurope,
                ListenersStart = _seasonStartListeners,
                ListenersEnd = State.Listeners,
                ReputationStart = _seasonStartRep,
                ReputationEnd = State.Reputation,
                MoneyEnd = State.Money
            };

            int bestGain = int.MinValue;
            foreach (var ep in State.Episodes.Where(e => e.Season == State.Season))
            {
                s.EpisodesThisSeason++;
                if (ep.ListenerDelta > bestGain) { bestGain = ep.ListenerDelta; s.BestEpisodeTopic = ep.TopicName; }
            }
            s.BestEpisodeListenerGain = bestGain == int.MinValue ? 0 : bestGain;

            if (Calendar.WonCup) s.Headlines.Add("Won the cup.");
            if (Calendar.WonEurope) s.Headlines.Add("Topped the European group.");
            if (finalPos <= Config.EuropeQualifyPosition) s.Headlines.Add($"Finished {s.LeaguePositionLabel} — European football next year.");
            else if (finalPos >= 18) s.Headlines.Add($"Finished {s.LeaguePositionLabel} — a relegation fight to the end.");

            State.LastSeason = s;
        }

        // ------------------------------------------------------------------
        // Studio / crew / gear
        // ------------------------------------------------------------------
        public bool CanBuy(Gear gear) => !State.HasGear(gear) && State.Money >= GearCost(gear);

        public bool BuyGear(Gear gear)
        {
            if (!CanBuy(gear)) return false;
            State.Money -= GearCost(gear);
            State.Gear |= gear;
            return true;
        }

        public int GearCost(Gear gear) => gear switch
        {
            Gear.XlrMic => Config.MicCost,
            Gear.AcousticPanels => Config.PanelsCost,
            Gear.EditingSoftware => Config.EditingCost,
            _ => int.MaxValue
        };

        public bool CanHireCoHost() => !State.HasCoHost && State.Money >= Config.CoHostHireCost;

        public bool HireCoHost()
        {
            if (!CanHireCoHost()) return false;
            State.Money -= Config.CoHostHireCost;
            State.HasCoHost = true;
            return true;
        }

        public int CrewWage(Crew role) => CrewCatalog.Get(role).Wage(Config);

        public bool CanHireCrew(Crew role) => !State.HasCrew(role) && State.Money >= CrewWage(role);

        public bool HireCrew(Crew role)
        {
            if (!CanHireCrew(role)) return false;
            State.Money -= CrewWage(role);   // first month up front
            State.Crew |= role;
            return true;
        }

        public bool CanBuySecondSponsorSlot() => !State.HasSecondSponsorSlot && State.Money >= Config.SecondSponsorSlotCost;
        public bool BuySecondSponsorSlot()
        {
            if (!CanBuySecondSponsorSlot()) return false;
            State.Money -= Config.SecondSponsorSlotCost;
            State.HasSecondSponsorSlot = true;
            return true;
        }

        public bool CanBuyStudioSpace() => !State.HasStudioSpace && State.Money >= Config.StudioSpaceCost;
        public bool BuyStudioSpace()
        {
            if (!CanBuyStudioSpace()) return false;
            State.Money -= Config.StudioSpaceCost;
            State.HasStudioSpace = true;
            return true;
        }

        public bool AcceptPartnership() => Access.AcceptPartnership(this);
        public void DeclinePartnership() => Access.DeclinePartnership();

        public bool PlayCard(string cardId) => Cards.Play(this, cardId);
        public bool BuyPack() => Cards.BuyPack(this);

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
