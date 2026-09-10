using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class ResolutionTests
    {
        static (GameState, GameConfig) Fresh()
        {
            var cfg = new GameConfig();
            var st = new GameState
            {
                Money = cfg.StartMoney,
                Listeners = cfg.StartListeners,
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
        public void MeetingEffortWithNoGear_GivesSolidQuality()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);
            var plan = new ProductionPlan { Topic = TopicId.Recap, PrepTopic = 3, PrepResearch = 0, PrepAudio = 0, PrepPromo = 0 };

            var r = res.Project(st, ParWeek(), plan);

            // 0.35 + 0.65*1 + gear(0.15*0.20 + 0.15*0.15 = 0.0525) = ~1.0025
            Assert.That(r.Quality, Is.EqualTo(1.0f).Within(0.03f));
        }

        [Test]
        public void ResearchNarrowsTheSwing()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);

            var noResearch = res.Project(st, ParWeek(),
                new ProductionPlan { Topic = TopicId.HotTake, PrepTopic = 3 });
            var withResearch = res.Project(st, ParWeek(),
                new ProductionPlan { Topic = TopicId.HotTake, PrepTopic = 3, PrepResearch = 3 });

            Assert.That(withResearch.Spread, Is.LessThan(noResearch.Spread));
        }

        [Test]
        public void ShockWin_InflatesRecapReachVersusPar()
        {
            var (st, cfg) = Fresh();
            var res = new Resolution(cfg);
            var plan = new ProductionPlan { Topic = TopicId.Recap, PrepTopic = 3, PrepAudio = 3 };

            var par = ParWeek();
            var shock = new WeekContext
            {
                Fixture = new Fixture { Opponent = "Test", Home = false, OpponentStrength = 0.8f },
                Match = new MatchResult { Outcome = MatchOutcome.Win, Surprise = Surprise.Heroic }
            };
            ContextResolver.Fill(shock);

            var rPar = res.Project(st, par, plan);
            var rShock = res.Project(st, shock, plan);

            Assert.That(rShock.Reach, Is.GreaterThan(rPar.Reach * 1.5f));
            Assert.That(rShock.ListenerDeltaExpected, Is.GreaterThan(rPar.ListenerDeltaExpected));
        }

        [Test]
        public void WeakEpisodeOnADisasterWeek_LosesListeners()
        {
            var (st, cfg) = Fresh();
            st.Listeners = 4000;
            var res = new Resolution(cfg);

            var disaster = new WeekContext
            {
                Fixture = new Fixture { Opponent = "Test", Home = true, OpponentStrength = 0.2f },
                Match = new MatchResult { Outcome = MatchOutcome.Loss, Surprise = Surprise.Disaster }
            };
            ContextResolver.Fill(disaster);

            // Barely any prep on a mismatched evergreen topic.
            var plan = new ProductionPlan { Topic = TopicId.Mailbag, PrepTopic = 0 };
            var r = res.Project(st, disaster, plan);

            Assert.That(r.ListenerDeltaExpected, Is.LessThan(0));
        }

        [Test]
        public void CoHostRaisesPrepCapacityAndAppeal()
        {
            var (st, cfg) = Fresh();
            Assert.That(st.PrepCapacity(cfg), Is.EqualTo(10));
            st.HasCoHost = true;
            Assert.That(st.PrepCapacity(cfg), Is.EqualTo(13));

            var res = new Resolution(cfg);
            var plan = new ProductionPlan { Topic = TopicId.Recap, PrepTopic = 3 };
            var without = new Resolution(cfg).Project(new GameState { Listeners = 40, Reputation = 10, Difficulty = Difficulty.Regular }, ParWeek(), plan);
            var with = res.Project(st, ParWeek(), plan);

            Assert.That(with.EffectiveAppeal, Is.GreaterThan(without.EffectiveAppeal));
        }
    }
}
