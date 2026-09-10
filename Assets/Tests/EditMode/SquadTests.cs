using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class SquadTests
    {
        [Test]
        public void GeneratesOnePlayerPerArchetype()
        {
            var squad = Squad.Generate(0.52f, new SystemRng(1));
            Assert.That(squad.Players.Count, Is.EqualTo(6));
            foreach (PlayerArchetype a in System.Enum.GetValues(typeof(PlayerArchetype)))
                Assert.That(squad.Players.Count(p => p.Archetype == a), Is.EqualTo(1));
        }

        [Test]
        public void StrongerClubHasBetterPlayersOnAverage()
        {
            float weak = Squad.Generate(0.30f, new SystemRng(4)).Players.Average(p => p.Rating);
            float strong = Squad.Generate(0.75f, new SystemRng(4)).Players.Average(p => p.Rating);
            Assert.That(strong, Is.GreaterThan(weak));
        }

        [Test]
        public void InjuriesAccrueAndHealOverTime()
        {
            var squad = Squad.Generate(0.52f, new SystemRng(9));
            var rng = new SystemRng(9);

            int everInjured = 0;
            for (int wk = 0; wk < 40; wk++)
            {
                squad.AdvanceWeek(false, true, rng);
                if (squad.Players.Any(p => !p.IsFit)) everInjured++;
            }
            Assert.That(everInjured, Is.GreaterThan(0), "someone should get injured across 40 congested weeks");

            // Everyone eventually heals.
            for (int wk = 0; wk < 12; wk++) squad.AdvanceWeek(true, false, rng);
            squad.Offseason(rng);
            Assert.That(squad.Players.All(p => p.IsFit), Is.True);
        }

        [Test]
        public void KeyOutIsZeroWhenEveryoneIsFit_PositiveWhenAKeyPlayerIsDown()
        {
            var squad = Squad.Generate(0.6f, new SystemRng(2));
            Assert.That(squad.KeyOut(), Is.EqualTo(0f));

            var key = squad.Players.First(p => p.IsKey);
            key.WeeksOut = 3;
            Assert.That(squad.KeyOut(), Is.GreaterThan(0f));
            Assert.That(squad.KeyPlayersOut(), Is.EqualTo(1));
        }

        [Test]
        public void EngineSurfacesSquadNewsAndRecordsEpisodes()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(123));

            bool sawNews = false;
            for (int i = 0; i < 30; i++)
            {
                var ctx = e.BeginWeek();
                if (ctx.SquadNews.Count > 0) sawNews = true;
                e.Publish(new ProductionPlan { Topic = e.Offer[0].Id, PrepTopic = 4, PrepAudio = 3 });
            }

            Assert.That(sawNews, Is.True, "squad news should show up over 30 weeks");
            Assert.That(e.State.Episodes.Count, Is.EqualTo(30));
            Assert.That(e.State.Episodes[0].TopicName, Is.Not.Null.And.Not.Empty);
        }
    }
}
