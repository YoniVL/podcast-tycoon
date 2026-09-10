using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class MatchSimulatorTests
    {
        [Test]
        public void StrongTeamBeatsWeakTeamMostOfTheTime()
        {
            var sim = new MatchSimulator(new GameConfig());
            var rng = new SystemRng(2024);
            var fx = new Fixture { Home = true, OpponentStrength = 0.25f };

            int wins = 0;
            for (int i = 0; i < 400; i++)
                if (sim.Simulate(0.78f, 0f, fx, rng).Outcome == MatchOutcome.Win) wins++;

            Assert.That(wins, Is.GreaterThan(220));
        }

        [Test]
        public void BeatingAStrongerTeamAwayIsHeroic()
        {
            var sim = new MatchSimulator(new GameConfig());
            var rng = new SystemRng(1);
            var fx = new Fixture { Home = false, OpponentStrength = 0.85f };

            // Find a win and check how it's classified.
            for (int i = 0; i < 500; i++)
            {
                var r = sim.Simulate(0.30f, 0f, fx, rng);
                if (r.Outcome == MatchOutcome.Win)
                {
                    Assert.That(r.Surprise, Is.EqualTo(Surprise.Heroic));
                    Assert.That(r.GoalsFor, Is.GreaterThan(r.GoalsAgainst));
                    return;
                }
            }
            Assert.Fail("expected at least one upset win in 500 tries");
        }

        [Test]
        public void ExpectedWinThatHappensIsPar()
        {
            var sim = new MatchSimulator(new GameConfig());
            var rng = new SystemRng(5);
            var fx = new Fixture { Home = true, OpponentStrength = 0.2f };

            for (int i = 0; i < 200; i++)
            {
                var r = sim.Simulate(0.85f, 0f, fx, rng);
                if (r.Outcome == MatchOutcome.Win)
                {
                    Assert.That(r.Surprise, Is.EqualTo(Surprise.Par));
                    return;
                }
            }
            Assert.Fail("expected a routine win");
        }

        [Test]
        public void ScorelineAlwaysAgreesWithOutcome()
        {
            var sim = new MatchSimulator(new GameConfig());
            var rng = new SystemRng(77);
            var fx = new Fixture { Home = true, OpponentStrength = 0.5f };

            for (int i = 0; i < 300; i++)
            {
                var r = sim.Simulate(0.5f, 0f, fx, rng);
                switch (r.Outcome)
                {
                    case MatchOutcome.Win: Assert.That(r.GoalsFor, Is.GreaterThan(r.GoalsAgainst)); break;
                    case MatchOutcome.Draw: Assert.That(r.GoalsFor, Is.EqualTo(r.GoalsAgainst)); break;
                    case MatchOutcome.Loss: Assert.That(r.GoalsFor, Is.LessThan(r.GoalsAgainst)); break;
                }
            }
        }

        [Test]
        public void LeagueTableFillsOutOverASeason()
        {
            var cfg = new GameConfig();
            var rng = new SystemRng(3);
            var cal = new SeasonCalendar(cfg);
            cal.BuildSeason("Testford", 0.5f, 1, rng);
            var sim = new MatchSimulator(cfg);

            Assert.That(cal.Clubs.Count, Is.EqualTo(20));
            Assert.That(cal.TurnsPerSeason, Is.EqualTo(38 + cfg.InternationalBreakTurns.Length));

            for (int turn = 1; turn <= cal.TurnsPerSeason; turn++)
            {
                var fx = cal.FixtureForTurn(turn);
                if (fx.IsMatchless) continue;
                cal.SimulateOtherFixtures(turn, rng, sim);
                var r = sim.Simulate(0.5f, 0f, fx, rng);
                cal.RecordPlayerResult(fx, r);
            }

            foreach (var c in cal.Clubs)
                Assert.That(c.Played, Is.EqualTo(38), $"{c.Name} should have played every matchday");

            int pos = cal.PlayerPosition();
            Assert.That(pos, Is.InRange(1, 20));
        }
    }
}
