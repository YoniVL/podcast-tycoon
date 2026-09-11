using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class Slice3Tests
    {
        static Engine NewEngine(int seed, Difficulty diff = Difficulty.Regular, RunModifiers mods = null) =>
            new Engine(new RunSetup { ClubName = "Testford", Difficulty = diff, Modifiers = mods ?? new RunModifiers() },
                new GameConfig(), new SystemRng(seed));

        static int _wk;

        // A sensible player won't take on more monthly burden than their cash reserves can
        // carry for a while — mirrors the same runway check the headless AI uses.
        static float CurrentMonthlyTotal(Engine e)
        {
            var st = e.State;
            float total = st.HasCoHost ? e.Config.CoHostMonthlyWage + st.CoHostWageBump : 0f;
            foreach (var c in st.Employed.Values) total += c.Wage;
            total += UpgradeCatalog.TotalMonthlyUpkeep(st);
            return total;
        }
        static bool RoomFor(Engine e, float extraMonthly) => e.State.Money > (CurrentMonthlyTotal(e) + extraMonthly) * 6f;

        // A sensible player also doesn't spend down to the wire on a one-time purchase —
        // keep a cash cushion on top of the monthly-runway check above.
        const float CashReserve = 300f;
        static bool CanAfford(Engine e, int cost, float extraMonthly) =>
            e.State.Money - cost > CashReserve && RoomFor(e, extraMonthly);

        static void PlayWeek(Engine e)
        {
            e.BeginWeek();
            if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
            if (e.Scoops.Pending != null) e.ResolveScoop(ScoopChoice.VerifyHold);
            if (e.State.BuyoutPending) e.DeclineBuyout();

            // The basics any player does: buy gear, hire, take a sponsor when it's affordable
            // — and keeps a cash cushion plus enough runway to cover the new recurring cost.
            if (e.CanBuyUpgrade(UpgradeTrack.Audio) && CanAfford(e, e.NextUpgrade(UpgradeTrack.Audio).Cost, e.NextUpgrade(UpgradeTrack.Audio).Monthly))
                e.BuyUpgrade(UpgradeTrack.Audio);
            if (e.CanBuyUpgrade(UpgradeTrack.Post) && CanAfford(e, e.NextUpgrade(UpgradeTrack.Post).Cost, e.NextUpgrade(UpgradeTrack.Post).Monthly))
                e.BuyUpgrade(UpgradeTrack.Post);
            if (e.CanHireCoHost() && CanAfford(e, 0, e.Config.CoHostMonthlyWage))
                e.HireCoHost();
            if (e.CandidatesFor(Crew.Producer).Count > 0 && e.CanHireCandidate(Crew.Producer, 0)
                && CanAfford(e, 0, e.CandidatesFor(Crew.Producer)[0].Wage))
                e.HireCandidate(Crew.Producer, 0);
            if (e.CandidatesFor(Crew.Researcher).Count > 0 && e.CanHireCandidate(Crew.Researcher, 0)
                && CanAfford(e, 0, e.CandidatesFor(Crew.Researcher)[0].Wage))
                e.HireCandidate(Crew.Researcher, 0);
            if (e.Sponsors.Active == null && e.Sponsors.Inbox.Count > 0) e.SignSponsor(0);

            // A varied two/three-segment rundown — rotate angles so freshness doesn't crater.
            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]);
            plan.Main.Angle = (_wk % 3) switch { 0 => Angle.Analysis, 1 => Angle.Emotional, _ => Angle.Analysis };
            plan.Main.Prep = 5;
            if (e.Offer.Count > 1)
            {
                plan.Second.Set(e.Offer[1]);
                plan.Second.Angle = Angle.Analysis;
                plan.Second.Prep = 3;
            }
            var rec = e.Offer.FirstOrDefault(t => t.Id == TopicId.Mailbag || t.Id == TopicId.TierList || t.Id == TopicId.Explainer);
            if (rec != null && !ReferenceEquals(rec, plan.Main.Resolved))
            {
                plan.Recurring.Set(rec);
                plan.Recurring.Angle = _wk % 2 == 0 ? Angle.Analysis : Angle.Comedy;
                plan.Recurring.Prep = 2;
            }
            plan.PrepAudio = 2;
            _wk++;
            e.Publish(plan);
        }

        [Test]
        public void CalendarHasCupWeekendsAndAShortOffseason()
        {
            var cfg = new GameConfig();
            var cal = new SeasonCalendar(cfg);
            cal.BuildSeason("Testford", 0.5f, 1, new SystemRng(1));

            int cup = 0, offseason = 0;
            for (int t = 1; t <= cal.TurnsPerSeason; t++)
            {
                var fx = cal.FixtureForTurn(t);
                if (fx.Competition == Competition.Cup) cup++;
                if (fx.IsOffseason) offseason++;
            }
            Assert.That(cup, Is.EqualTo(cfg.CupAfterMatchday.Length));
            Assert.That(offseason, Is.EqualTo(cfg.OffseasonTurns));
        }

        [Test]
        public void EuropeanNightsOnlyExistWhenQualified()
        {
            var cfg = new GameConfig();
            var plain = new SeasonCalendar(cfg);
            plain.BuildSeason("Testford", 0.5f, 1, new SystemRng(2), inEurope: false);
            var euro = new SeasonCalendar(cfg);
            euro.BuildSeason("Testford", 0.5f, 1, new SystemRng(2), inEurope: true);

            int Count(SeasonCalendar c)
            {
                int n = 0;
                for (int t = 1; t <= c.TurnsPerSeason; t++)
                    if (c.FixtureForTurn(t).Competition == Competition.European) n++;
                return n;
            }

            Assert.That(Count(plain), Is.EqualTo(0));
            Assert.That(Count(euro), Is.EqualTo(cfg.EuropeAfterMatchday.Length));
        }

        [Test]
        public void CupRunResolvesOverASeason()
        {
            var e = NewEngine(2026, Difficulty.Casual);
            bool cupPlayed = false;
            for (int i = 0; i < 120 && e.State.Season == 1; i++)
            {
                PlayWeek(e);
                if (e.Calendar.CupRoundReached > 0 || !e.Calendar.CupAlive) cupPlayed = true;
            }
            Assert.That(cupPlayed, Is.True, "the cup should have been played during the season");
        }

        [Test]
        public void RivalsAreTrackedAndTheirFixturesAreDerbies()
        {
            var e = NewEngine(7);
            Assert.That(e.Rivals.Rivals.Count, Is.GreaterThan(0), "a rival should be picked each season");

            bool sawRivalDerby = false;
            for (int i = 0; i < 80 && e.State.Season == 1; i++)
            {
                var ctx = e.BeginWeek();
                if (ctx.Fixture != null && ctx.Fixture.IsRivalFixture)
                {
                    sawRivalDerby = true;
                    Assert.That(ctx.Importance, Is.Not.EqualTo(FixtureImportance.Normal));
                }
                if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
                if (e.Scoops.Pending != null) e.ResolveScoop(ScoopChoice.VerifyHold);
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 5;
                e.Publish(plan);
            }
            Assert.That(sawRivalDerby, Is.True, "you play each rival twice a season");
        }

        [Test]
        public void AccessTierRisesWithTheRollingAudience()
        {
            var e = NewEngine(3);
            e.BeginWeek();
            Assert.That(e.State.AccessTier, Is.EqualTo(0));

            e.State.Listeners = 20000;
            e.State.Credibility = 60f;   // tier 2 also needs the club's trust (spec §18)
            for (int i = 0; i < 8; i++) e.State.ListenerHistory.Add(20000);
            e.BeginWeek();
            Assert.That(e.State.AccessTier, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ScoopsArriveAtInsiderAccessAndCanBeResolved()
        {
            var e = NewEngine(11);
            e.State.Listeners = 30000;
            e.State.Credibility = 65f;   // insider access needs the club's trust (spec §18)
            for (int i = 0; i < 8; i++) e.State.ListenerHistory.Add(30000);

            bool sawScoop = false;
            for (int i = 0; i < 60 && !e.State.IsGameOver; i++)
            {
                var ctx = e.BeginWeek();
                if (e.Scoops.Pending != null)
                {
                    sawScoop = true;
                    e.ResolveScoop(ScoopChoice.BreakNow);
                    Assert.That(e.Scoops.Pending, Is.Null);
                }
                if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 5; plan.PrepAudio = 3;
                e.State.Listeners = System.Math.Max(e.State.Listeners, 25000);
                e.Publish(plan);
            }
            Assert.That(sawScoop, Is.True, "a scoop should fire at insider access over 60 weeks");
        }

        [Test]
        public void CrewCanBeHiredFiredAndChangesPrepCapacity()
        {
            var e = NewEngine(5);
            e.BeginWeek();
            e.State.Money = 5000;
            int before = e.State.PrepCapacity(e.Config);

            Assert.That(e.CandidatesFor(Crew.Producer).Count, Is.GreaterThan(0), "a fresh run should have candidates to pick from");
            float skill = e.CandidatesFor(Crew.Producer)[0].Skill;
            Assert.That(e.HireCandidate(Crew.Producer, 0), Is.True);
            Assert.That(e.State.PrepCapacity(e.Config), Is.EqualTo(before + MathX.RoundToInt(2f * skill)));
            Assert.That(e.HireCandidate(Crew.Producer, 0), Is.False, "can't hire into a filled role");

            float moneyBeforeFiring = e.State.Money;
            Assert.That(e.FireCrew(Crew.Producer), Is.True);
            Assert.That(e.State.Money, Is.LessThan(moneyBeforeFiring), "firing costs severance");
            Assert.That(e.State.PrepCapacity(e.Config), Is.EqualTo(before));
            Assert.That(e.State.HasCrew(Crew.Producer), Is.False);
        }

        [Test]
        public void PlayingAPermanentCardSticks()
        {
            var e = NewEngine(9);
            e.BeginWeek();
            e.State.Hand.Clear();
            e.State.Hand.Add("genius_appointment");
            float before = e.State.TeamStrength;
            Assert.That(e.PlayCard("genius_appointment"), Is.True);
            Assert.That(e.State.TeamStrength, Is.GreaterThan(before));
            Assert.That(e.State.Hand, Is.Empty);
        }

        [Test]
        public void SandboxModifierDisablesBankruptcy()
        {
            var e = NewEngine(99, Difficulty.Nightmare, new RunModifiers { Sandbox = true });
            for (int i = 0; i < 120 && !e.State.IsGameOver; i++)
            {
                e.BeginWeek();
                if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
                if (e.Scoops.Pending != null) e.ResolveScoop(ScoopChoice.VerifyHold);
                var plan = ProductionPlan.Cover(e.Offer[0]); // no prep at all
                e.Publish(plan);
            }
            Assert.That(e.State.IsGameOver, Is.False, "sandbox runs never end from bankruptcy");
            Assert.That(e.State.Money, Is.LessThan(0f), "and this one really should be broke");
        }

        [Test]
        public void EndlessMilestonesFireBeyondFiftyThousand()
        {
            var e = NewEngine(4);
            int highest = 0;
            e.MilestoneReached += m => highest = m.Listeners;

            e.State.Listeners = 260000;
            e.BeginWeek();
            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 4; plan.PrepAudio = 3;
            e.Publish(plan);

            Assert.That(highest, Is.GreaterThanOrEqualTo(250000));
        }

        [Test]
        public void FullSeasonWithEverythingRunsClean()
        {
            var e = NewEngine(2027, Difficulty.Casual);
            for (int i = 0; i < 260 && !e.State.IsGameOver; i++)
                PlayWeek(e);
            Assert.That(e.State.Season, Is.GreaterThanOrEqualTo(4));
            Assert.That(e.State.EpisodesPublished, Is.GreaterThan(150));
        }
    }
}
