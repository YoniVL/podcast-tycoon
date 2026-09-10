using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class RundownTests
    {
        static (GameState, GameConfig) Fresh()
        {
            var cfg = new GameConfig();
            var st = new GameState
            {
                Money = cfg.StartMoney,
                Listeners = 5000,
                Reputation = cfg.StartReputation,
                Difficulty = Difficulty.Regular,
                TeamStrength = 0.52f
            };
            return (st, cfg);
        }

        static WeekContext ParWeek()
        {
            var ctx = new WeekContext
            {
                Fixture = new Fixture { Opponent = "Test", Home = true, OpponentStrength = 0.5f },
                Match = new MatchResult { Outcome = MatchOutcome.Win, Surprise = Surprise.Par }
            };
            ContextResolver.Fill(ctx);
            return ctx;
        }

        [Test]
        public void AFreshPlanHasOnlyAnEmptyMainSlot()
        {
            var p = new ProductionPlan();
            Assert.That(p.Main.IsEmpty, Is.True);
            Assert.That(p.FilledSlots.Count(), Is.EqualTo(0));

            p.Main.Set(TopicCatalog.Get(TopicId.Recap));
            Assert.That(p.Main.IsEmpty, Is.False);
            Assert.That(p.FilledSlots.Count(), Is.EqualTo(1));
        }

        [Test]
        public void ThreeSegmentsReachRoughlyLikeAWeightedWhole()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);

            var single = new ProductionPlan();
            single.Main.Set(TopicCatalog.Get(TopicId.Recap));
            single.Main.Prep = 3;

            var full = new ProductionPlan();
            full.Main.Set(TopicCatalog.Get(TopicId.Recap)); full.Main.Prep = 3;
            full.Second.Set(TopicCatalog.Get(TopicId.Preview)); full.Second.Prep = 3;
            full.Recurring.Set(TopicCatalog.Get(TopicId.Mailbag)); full.Recurring.Prep = 2;

            var rSingle = res.Project(st, ParWeek(), single);
            var rFull = res.Project(st, ParWeek(), full);

            // Slot weights sum to 1, so a full rundown reaches in the same ballpark as a
            // single Main-only episode — not multiples more.
            Assert.That(rFull.Reach, Is.InRange(rSingle.Reach * 0.6f, rSingle.Reach * 1.6f));
        }

        [Test]
        public void HotTakeLiftsAppealAndWidensSwingVersusAnalysis()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);

            var analysis = new ProductionPlan();
            analysis.Main.Set(TopicCatalog.Get(TopicId.HotTake));
            analysis.Main.Prep = 3; analysis.Main.Angle = Angle.Analysis;

            var hot = analysis.Clone();
            hot.Main.Angle = Angle.HotTake;

            var rA = res.Project(st, ParWeek(), analysis);
            var rH = res.Project(st, ParWeek(), hot);

            Assert.That(rH.EffectiveAppeal, Is.GreaterThan(rA.EffectiveAppeal));
            Assert.That(rH.Spread, Is.GreaterThan(rA.Spread));
            Assert.That(rH.CredibilityDelta, Is.LessThan(rA.CredibilityDelta));
        }

        [Test]
        public void InvestigationAngleNeedsAResearcherOrInsiderAccess()
        {
            var (st, _) = Fresh();
            Assert.That(AngleCatalog.Allowed(Angle.Investigation, st), Is.False);

            st.Crew |= Crew.Researcher;
            Assert.That(AngleCatalog.Allowed(Angle.Investigation, st), Is.True);

            st.Crew = Crew.None;
            st.AccessTier = 2;
            Assert.That(AngleCatalog.Allowed(Angle.Investigation, st), Is.True);
        }

        [Test]
        public void CoveringAThreadInTheSecondSlotStillCountsAsCovered()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(42));

            StoryThread thread = null;
            for (int i = 0; i < 200 && thread == null; i++)
            {
                e.BeginWeek();
                thread = e.CurrentWeek.ActiveThreads.FirstOrDefault(t => t.Topic != null);
                if (thread == null)
                {
                    var skip = ProductionPlan.Cover(e.Offer[0]);
                    skip.PrepTopic = 4;
                    e.Publish(skip);
                }
            }
            Assert.That(thread, Is.Not.Null, "a thread with a topic should appear within 200 weeks");

            var plan = new ProductionPlan();
            plan.Main.Set(TopicCatalog.Get(TopicId.Mailbag)); plan.Main.Prep = 2;
            plan.Second.Set(thread.Topic, 1); plan.Second.Prep = 4;
            plan.PrepAudio = 3;

            int coveredBefore = thread.TimesCovered;
            e.Publish(plan);

            Assert.That(thread.TimesCovered, Is.EqualTo(coveredBefore + 1));
            Assert.That(thread.WeeksSinceCovered, Is.EqualTo(0));
        }

        [Test]
        public void GuestLiftsAppealWhenBooked()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);

            var plain = new ProductionPlan();
            plain.Main.Set(TopicCatalog.Get(TopicId.Recap)); plain.Main.Prep = 3;

            var withGuest = plain.Clone();
            withGuest.Main.Guest = true;

            Assert.That(res.Project(st, ParWeek(), withGuest).EffectiveAppeal,
                Is.GreaterThan(res.Project(st, ParWeek(), plain).EffectiveAppeal));
        }
    }
}
