using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class EngineTests
    {
        static Engine NewEngine(int seed = 12345, Difficulty diff = Difficulty.Regular)
        {
            return new Engine(new RunSetup { ClubName = "Testford", Difficulty = diff },
                new GameConfig(), new SystemRng(seed));
        }

        [Test]
        public void StartsWithConfiguredResources()
        {
            var e = NewEngine();
            Assert.That(e.State.Money, Is.EqualTo(500f));
            Assert.That(e.State.Listeners, Is.EqualTo(40));
            Assert.That(e.State.Reputation, Is.EqualTo(10f));
            Assert.That(e.State.Season, Is.EqualTo(1));
        }

        [Test]
        public void BeginWeek_ProducesAnOfferOfUpToThree()
        {
            var e = NewEngine();
            e.BeginWeek();
            Assert.That(e.Offer.Count, Is.InRange(1, 3));
            Assert.That(e.CurrentWeek, Is.Not.Null);
        }

        [Test]
        public void PublishAdvancesTheWeekAndRecordsHistory()
        {
            var e = NewEngine();
            e.BeginWeek();
            var topic = e.Offer[0].Id;
            e.Publish(new ProductionPlan { Topic = topic, PrepTopic = 3, PrepAudio = 3, PrepPromo = 2, PrepResearch = 2 });

            Assert.That(e.State.GlobalWeek, Is.EqualTo(2));
            Assert.That(e.State.EpisodesPublished, Is.EqualTo(1));
            Assert.That(e.State.ListenerHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanPlayAFullSeasonWithoutError()
        {
            var e = NewEngine();
            int guard = 0;
            while (e.State.Season == 1 && guard++ < 200)
            {
                e.BeginWeek();
                var t = e.Offer[0].Id;
                e.Publish(new ProductionPlan { Topic = t, PrepTopic = 4, PrepAudio = 3, PrepResearch = 2, PrepPromo = 1 });
            }
            Assert.That(e.State.Season, Is.GreaterThanOrEqualTo(2), "season should have rolled over");
            Assert.That(e.State.GlobalWeek, Is.GreaterThan(38));
        }

        [Test]
        public void SeasonRollover_AppliesOffseasonChurn()
        {
            var e = NewEngine(seed: 7);
            bool rolled = false;
            e.SeasonRolledOver += _ => rolled = true;

            int guard = 0;
            while (!rolled && guard++ < 200)
            {
                e.BeginWeek();
                var t = e.Offer[0].Id;
                e.Publish(new ProductionPlan { Topic = t, PrepTopic = 10 });
            }

            Assert.That(rolled, Is.True);
            Assert.That(e.State.SeasonTurn, Is.EqualTo(1));
        }

        [Test]
        public void NeglectedShowGoesBankrupt()
        {
            var e = NewEngine(seed: 99, diff: Difficulty.Nightmare);
            bool over = false;
            e.GameOver += _ => over = true;

            int guard = 0;
            while (!over && guard++ < 120)
            {
                e.BeginWeek();
                // Publish the laziest possible episode every week.
                e.Publish(new ProductionPlan { Topic = e.Offer[0].Id, PrepTopic = 0 });
            }

            Assert.That(over, Is.True, "a show that never invests should eventually go broke");
            Assert.That(e.State.IsGameOver, Is.True);
        }

        [Test]
        public void BuyingGearDeductsMoneyAndSticks()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            Assert.That(e.BuyGear(Gear.XlrMic), Is.True);
            Assert.That(e.State.Money, Is.EqualTo(850f));
            Assert.That(e.State.HasGear(Gear.XlrMic), Is.True);
            Assert.That(e.BuyGear(Gear.XlrMic), Is.False, "can't buy it twice");
        }

        [Test]
        public void MilestoneFiresWhenListenersCrossThreshold()
        {
            var e = NewEngine();
            int fired = -1;
            e.MilestoneReached += m => fired = m.Listeners;

            e.State.Listeners = 260;   // already past the 100 milestone
            e.BeginWeek();
            e.Publish(new ProductionPlan { Topic = e.Offer[0].Id, PrepTopic = 4, PrepAudio = 3 });

            Assert.That(fired, Is.EqualTo(100));
            Assert.That(e.State.NextMilestoneIndex, Is.EqualTo(1));
        }
    }
}
