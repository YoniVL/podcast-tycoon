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

        static ProductionPlan Plan(Engine e, int topicPrep = 4, int research = 2, int audio = 3, int promo = 1)
        {
            var p = ProductionPlan.Cover(e.Offer[0]);
            p.PrepTopic = topicPrep;
            p.PrepResearch = research;
            p.PrepAudio = audio;
            p.PrepPromo = promo;
            return p;
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
        public void BeginWeek_ProducesAnOfferOfUpToSix()
        {
            var e = NewEngine();
            e.BeginWeek();
            Assert.That(e.Offer.Count, Is.InRange(1, 6));
            Assert.That(e.CurrentWeek, Is.Not.Null);
        }

        [Test]
        public void PublishAdvancesTheWeekAndRecordsHistory()
        {
            var e = NewEngine();
            e.BeginWeek();
            e.Publish(Plan(e, topicPrep: 3, research: 2, audio: 3, promo: 2));

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
                e.Publish(Plan(e));
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
                e.Publish(Plan(e, topicPrep: 10, research: 0, audio: 0, promo: 0));
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
                e.Publish(Plan(e, topicPrep: 0, research: 0, audio: 0, promo: 0));
            }

            Assert.That(over, Is.True, "a show that never invests should eventually go broke");
            Assert.That(e.State.IsGameOver, Is.True);
        }

        [Test]
        public void BuyingAnUpgradeDeductsMoneyAndSticks()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            int cost = e.NextUpgrade(UpgradeTrack.Audio).Cost;
            Assert.That(e.BuyUpgrade(UpgradeTrack.Audio), Is.True);
            Assert.That(e.State.Money, Is.EqualTo(1000f - cost));
            Assert.That(e.State.AudioTier, Is.EqualTo(1));

            int cost2 = e.NextUpgrade(UpgradeTrack.Audio).Cost;
            Assert.That(e.BuyUpgrade(UpgradeTrack.Audio), Is.True, "should be able to buy the next tier up");
            Assert.That(e.State.AudioTier, Is.EqualTo(2));
            Assert.That(e.State.Money, Is.EqualTo(1000f - cost - cost2));
        }

        [Test]
        public void MilestoneFiresWhenListenersCrossThreshold()
        {
            var e = NewEngine();
            int fired = -1;
            e.MilestoneReached += m => fired = m.Listeners;

            e.State.Listeners = 260;   // already past the 100 milestone
            e.BeginWeek();
            e.Publish(Plan(e, topicPrep: 4, research: 0, audio: 3, promo: 0));

            Assert.That(fired, Is.EqualTo(100));
            Assert.That(e.State.NextMilestoneIndex, Is.EqualTo(1));
        }
    }
}
