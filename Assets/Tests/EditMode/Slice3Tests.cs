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

        static void PlayWeek(Engine e)
        {
            e.BeginWeek();
            if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
            if (e.Scoops.Pending != null) e.ResolveScoop(ScoopChoice.VerifyHold);
            if (e.State.BuyoutPending) e.DeclineBuyout();
            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 5; plan.PrepAudio = 3;
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
            for (int i = 0; i < 8; i++) e.State.ListenerHistory.Add(20000);
            e.BeginWeek();
            Assert.That(e.State.AccessTier, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ScoopsArriveAtInsiderAccessAndCanBeResolved()
        {
            var e = NewEngine(11);
            e.State.Listeners = 30000;
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
        public void CrewCanBeHiredAndChangesPrepCapacity()
        {
            var e = NewEngine(5);
            e.BeginWeek();
            e.State.Money = 5000;
            int before = e.State.PrepCapacity(e.Config);
            Assert.That(e.HireCrew(Crew.Producer), Is.True);
            Assert.That(e.State.PrepCapacity(e.Config), Is.EqualTo(before + 2));
            Assert.That(e.HireCrew(Crew.Producer), Is.False, "can't hire the same role twice");
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
