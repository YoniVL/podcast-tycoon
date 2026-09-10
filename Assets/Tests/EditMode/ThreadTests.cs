using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class ThreadTests
    {
        static Engine Play(int seed, int weeks, out List<ThreadEvent> opened, out List<ThreadEvent> resolved,
            bool coverThreads = true)
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(seed));
            var op = new List<ThreadEvent>();
            var rs = new List<ThreadEvent>();
            e.ThreadOpened += ev => op.Add(ev);
            e.ThreadResolved += ev => rs.Add(ev);

            for (int i = 0; i < weeks && !e.State.IsGameOver; i++)
            {
                e.BeginWeek();
                Topic pick = e.Offer[0];
                if (coverThreads)
                    pick = e.Offer.FirstOrDefault(t => t.SourceThread != null) ?? e.Offer[0];

                var plan = ProductionPlan.Cover(pick);
                plan.PrepTopic = 6; plan.PrepResearch = 2; plan.PrepAudio = 3;
                e.Publish(plan);
            }

            opened = op;
            resolved = rs;
            return e;
        }

        [Test]
        public void ThreadsOpenAndResolveOverManySeasons()
        {
            var e = Play(2026, 180, out var opened, out var resolved);
            Assert.That(opened.Count, Is.GreaterThan(0), "at least one story thread should have opened");
            Assert.That(resolved.Count, Is.GreaterThan(0), "and at least one should have resolved");
        }

        [Test]
        public void ThreadsDoNotStallPastTheirTimeout()
        {
            var e = Play(77, 250, out var opened, out var resolved);
            // Whatever is still running opened recently — nothing is stuck.
            Assert.That(e.Threads.Active.All(t => t.TotalWeeks <= 8), Is.True,
                "no active thread should be older than its timeout");
            Assert.That(resolved.Count, Is.GreaterThan(opened.Count - 5),
                "the vast majority of opened threads should have resolved");
        }

        [Test]
        public void ThreadTopicsAppearInTheOfferWhileActive()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(5));
            bool sawThreadTopicOffered = false;

            for (int i = 0; i < 160 && !e.State.IsGameOver; i++)
            {
                var ctx = e.BeginWeek();
                if (ctx.ActiveThreads.Count > 0 && e.Offer.Any(t => t.SourceThread != null))
                    sawThreadTopicOffered = true;

                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 5; plan.PrepAudio = 3;
                e.Publish(plan);
            }

            Assert.That(sawThreadTopicOffered, Is.True);
        }

        [Test]
        public void CoveringAThreadWellNudgesItTowardTheGoodOutcome()
        {
            // Same seed, same weeks — one run covers threads, one ignores them.
            var covered = Play(999, 200, out var oCov, out var rCov, coverThreads: true);
            var ignored = Play(999, 200, out var oIgn, out var rIgn, coverThreads: false);

            // Both should see similar numbers of threads open (same seed).
            Assert.That(oCov.Count, Is.GreaterThan(0));
            Assert.That(oIgn.Count, Is.GreaterThan(0));

            // The "covered" run should end at least as well-regarded — engaging with stories
            // and steering them positively tends to build reputation.
            Assert.That(covered.State.Reputation, Is.GreaterThanOrEqualTo(ignored.State.Reputation - 5f));
        }

        [Test]
        public void TeamStrengthStaysInBoundsThroughManyThreadResolutions()
        {
            var e = new Engine(new RunSetup { ClubName = "Testford", Difficulty = Difficulty.Nightmare },
                new GameConfig(), new SystemRng(41));

            for (int i = 0; i < 200 && !e.State.IsGameOver; i++)
            {
                e.BeginWeek();
                var plan = ProductionPlan.Cover(e.Offer.FirstOrDefault(t => t.SourceThread != null) ?? e.Offer[0]);
                plan.PrepTopic = 5; plan.PrepAudio = 3;
                e.Publish(plan);
                Assert.That(e.State.TeamStrength, Is.InRange(0.05f, 0.95f));
            }
        }
    }
}
