using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class EventTests
    {
        static Engine NewEngine(int seed) =>
            new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(seed));

        static void PlayWeek(Engine e, int resolveOption = 0)
        {
            e.BeginWeek();
            if (e.Events.Pending != null) e.ResolveEvent(resolveOption);
            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 4; plan.PrepAudio = 3;
            e.Publish(plan);
        }

        [Test]
        public void EventsFireOverTime()
        {
            var e = NewEngine(11);
            int fired = 0;
            for (int i = 0; i < 60; i++)
            {
                e.BeginWeek();
                if (e.Events.Pending != null) { fired++; e.ResolveEvent(0); }
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 4; plan.PrepAudio = 3;
                e.Publish(plan);
            }
            Assert.That(fired, Is.GreaterThan(2), "events should fire a handful of times over 60 weeks");
        }

        [Test]
        public void ResolvingAnEventAppliesItsEffectAndClearsPending()
        {
            var e = NewEngine(3);
            for (int i = 0; i < 200; i++)
            {
                e.BeginWeek();
                if (e.Events.Pending != null)
                {
                    float moneyBefore = e.State.Money;
                    float repBefore = e.State.Reputation;
                    float socialBefore = e.State.SocialReach;
                    int listenersBefore = e.State.Listeners;

                    e.ResolveEvent(0);

                    Assert.That(e.Events.Pending, Is.Null);
                    Assert.That(e.Events.LastOutcome, Is.Not.Null.And.Not.Empty);
                    bool somethingChanged =
                        e.State.Money != moneyBefore || e.State.Reputation != repBefore ||
                        e.State.SocialReach != socialBefore || e.State.Listeners != listenersBefore ||
                        e.State.WeeklyListenerDriftWeeks > 0 || e.State.PrepPenaltyThisWeek > 0 ||
                        e.State.CoHostWageBump > 0 || !e.State.HasCoHost;
                    Assert.That(somethingChanged, Is.True, "an event choice should do something");
                    return;
                }
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 4;
                e.Publish(plan);
            }
            Assert.Ignore("no event fired in 200 weeks with this seed");
        }

        [Test]
        public void PrepPenaltyIsSpentInOneWeek()
        {
            var e = NewEngine(1);
            e.BeginWeek();
            e.State.PrepPenaltyThisWeek = 3;
            Assert.That(e.State.PrepCapacity(e.Config), Is.EqualTo(7));

            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 4;
            e.Publish(plan);

            e.BeginWeek();
            Assert.That(e.State.PrepCapacity(e.Config), Is.EqualTo(10), "the penalty should not carry over");
        }

        [Test]
        public void EveryEventOptionResolvesWithoutError()
        {
            // Cycle through option indices across a long run so each branch of each event runs.
            var e = NewEngine(202);
            int resolved = 0;
            for (int i = 0; i < 300; i++)
            {
                e.State.Money = System.Math.Max(e.State.Money, 300f); // testing events, not the economy
                e.BeginWeek();
                if (e.Events.Pending != null)
                {
                    e.ResolveEvent(resolved % 3);
                    resolved++;
                }
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 5; plan.PrepAudio = 3;
                e.Publish(plan);
            }
            Assert.That(resolved, Is.GreaterThan(5));
            Assert.That(e.State.EpisodesPublished, Is.GreaterThan(250));
        }
    }
}
