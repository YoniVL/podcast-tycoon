using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class RecordingTests
    {
        static (GameState, GameConfig) Fresh()
        {
            var cfg = new GameConfig();
            var st = new GameState
            {
                Money = cfg.StartMoney, Listeners = 5000, Core = 3000f, Casual = 2000f,
                Reputation = cfg.StartReputation, Credibility = 50f, Difficulty = Difficulty.Regular,
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
        public void HigherPushRaisesAppealAndBackfireChance()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);

            var plan = new ProductionPlan();
            plan.Main.Set(TopicCatalog.Get(TopicId.HotTake));
            plan.Main.Angle = Angle.HotTake; plan.Main.Prep = 3;
            plan.Push = 2;
            var low = res.Project(st, ParWeek(), plan);

            plan.Push = 5;
            var high = res.Project(st, ParWeek(), plan);

            Assert.That(high.EffectiveAppeal, Is.GreaterThan(low.EffectiveAppeal));
            Assert.That(high.BackfireChance, Is.GreaterThan(low.BackfireChance));
        }

        [Test]
        public void LowMoraleCapsQuality()
        {
            var (st, cfg) = Fresh();
            st.Morale = 20f;
            var res = new Resolution(cfg);

            var plan = new ProductionPlan();
            plan.Main.Set(TopicCatalog.Get(TopicId.Recap));
            plan.Main.Angle = Angle.Analysis; plan.Main.Prep = 8;
            plan.PrepAudio = 6;

            var r = res.Project(st, ParWeek(), plan);
            Assert.That(r.Quality, Is.LessThanOrEqualTo(1.05f));
        }

        [Test]
        public void BackfireEventuallyOpensACorrectionOpportunity()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(1234));
            e.State.Credibility = 5f;   // stack the odds — low cred, always hot takes, max push

            bool sawPending = false;
            for (int i = 0; i < 150 && !sawPending; i++)
            {
                e.BeginWeek();
                var plan = new ProductionPlan();
                plan.Main.Set(e.Offer[0]);
                plan.Main.Angle = Angle.HotTake; plan.Main.Prep = 3;
                plan.Push = 5;
                e.Publish(plan);
                if (e.State.PendingCorrection) sawPending = true;
            }

            Assert.That(sawPending, Is.True, "a low-credibility, always-loud show should eventually backfire");

            e.BeginWeek();
            Assert.That(e.Offer.Any(t => t.IsCorrectionOpportunity), Is.True);
        }

        [Test]
        public void RecordingBeatsResolveThenPublish()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(5));
            e.State.HasCoHost = true;
            e.BeginWeek();

            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]);
            plan.Main.Angle = Angle.HotTake; plan.Main.Prep = 3;
            plan.PrepAudio = 2;

            e.StartRecording(plan);
            Assert.That(e.IsRecording, Is.True);

            int guard = 0;
            while (e.CurrentBeat != null && guard++ < 20)
                Assert.That(e.ResolveBeat(0), Is.True);

            Assert.That(e.RecordingReady, Is.True);
            var result = e.Publish(e.PendingPlan);
            Assert.That(result, Is.Not.Null);
            Assert.That(e.State.EpisodesPublished, Is.EqualTo(1));
        }
    }
}
