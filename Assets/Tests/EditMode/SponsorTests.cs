using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class SponsorTests
    {
        static Engine Grown(int seed, int startListeners)
        {
            var e = new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(seed));
            e.State.Listeners = startListeners;
            for (int i = 0; i < e.Config.AvgListenerWindow; i++) e.State.ListenerHistory.Add(startListeners);
            return e;
        }

        static void Week(Engine e, bool sign = false, int listenerGain = 0)
        {
            e.BeginWeek();
            if (e.Events.Pending != null) e.ResolveEvent(e.Events.Pending.Options.Count - 1);
            if (sign && e.Sponsors.Active == null && e.Sponsors.Inbox.Count > 0)
                e.SignSponsor(0);

            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 5; plan.PrepAudio = 3;
            e.Publish(plan);
            if (listenerGain != 0)
                e.State.Listeners = System.Math.Max(0, e.State.Listeners + listenerGain);
        }

        [Test]
        public void NoOffersWhileTheShowIsTiny()
        {
            var e = Grown(1, 40);
            for (int i = 0; i < 20; i++)
            {
                Week(e);
                e.State.Listeners = 40; // keep it tiny
                e.State.ListenerHistory.Add(40);
            }
            Assert.That(e.Sponsors.Inbox, Is.Empty);
        }

        [Test]
        public void OffersArriveOnceBigEnough()
        {
            var e = Grown(2, 3000);
            bool sawOffers = false;
            for (int i = 0; i < 20; i++)
            {
                Week(e);
                if (e.Sponsors.Inbox.Count > 0) sawOffers = true;
            }
            Assert.That(sawOffers, Is.True);
        }

        [Test]
        public void SigningPaysWeeklyIntoTheEconomy()
        {
            var e = Grown(3, 3000);
            for (int i = 0; i < 25 && e.Sponsors.Active == null; i++) Week(e, sign: true);
            Assert.That(e.Sponsors.Active, Is.Not.Null);

            int weekly = e.Sponsors.Active.Offer.Weekly;
            e.BeginWeek();
            var plan = ProductionPlan.Cover(e.Offer[0]);
            plan.PrepTopic = 5;
            var r = e.Publish(plan);
            Assert.That(r.SponsorRevenue, Is.EqualTo(weekly).Within(0.01f));
        }

        [Test]
        public void HittingTheTargetPaysABonusAndOffersARenewal()
        {
            var e = Grown(4, 3000);
            for (int i = 0; i < 25 && e.Sponsors.Active == null; i++) Week(e, sign: true);
            Assert.That(e.Sponsors.Active, Is.Not.Null);

            // Jump listeners well past the target and run the deal out.
            e.State.Listeners = e.Sponsors.Active.Offer.PrimaryGrowth.Value * 3;
            for (int i = 0; i < e.Config.AvgListenerWindow; i++) e.State.ListenerHistory.Add(e.State.Listeners);

            float moneyBefore = e.State.Money;
            bool good = false;
            e.SponsorResolved += n => good = n.Good;

            for (int i = 0; i < 15 && e.Sponsors.Active != null; i++)
            {
                e.BeginWeek();
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 5;
                e.Publish(plan);
            }

            Assert.That(good, Is.True);
            Assert.That(e.State.Money, Is.GreaterThan(moneyBefore));
            Assert.That(e.Sponsors.Inbox.Any(), Is.True, "a renewal should be on the table");
        }

        [Test]
        public void MissingTheTargetEndsTheDealBadly()
        {
            var e = Grown(5, 3000);
            for (int i = 0; i < 25 && e.Sponsors.Active == null; i++) Week(e, sign: true);
            Assert.That(e.Sponsors.Active, Is.Not.Null);

            float repBefore = e.State.Reputation;
            bool bad = false;
            e.SponsorResolved += n => bad = !n.Good;

            // Keep listeners flat — never hit the target — and run past the deadline.
            for (int i = 0; i < 20 && e.Sponsors.Active != null; i++)
            {
                e.BeginWeek();
                e.State.Listeners = 3000; // pin it
                var plan = ProductionPlan.Cover(e.Offer[0]);
                plan.PrepTopic = 0; // weak episode, no growth
                e.Publish(plan);
            }

            Assert.That(bad, Is.True);
            Assert.That(e.State.Reputation, Is.LessThan(repBefore));
        }

        [Test]
        public void EveryDealCarriesAtLeastOneGrowthTerm()
        {
            var e = Grown(6, 90000);   // tier 3 — should carry standing/conduct terms too
            for (int i = 0; i < 20 && e.Sponsors.Inbox.Count == 0; i++) Week(e);
            Assert.That(e.Sponsors.Inbox, Is.Not.Empty);
            foreach (var o in e.Sponsors.Inbox)
                Assert.That(o.PrimaryGrowth, Is.Not.Null, $"{o.Name} has no GROWTH term");
        }

        [Test]
        public void AConductTermBreaksTheDealOnABackfire()
        {
            var e = Grown(7, 90000);
            for (int i = 0; i < 30 && e.Sponsors.Active == null; i++) Week(e, sign: true);
            Assert.That(e.Sponsors.Active, Is.Not.Null);

            var conduct = e.Sponsors.Active.Offer.Terms.FirstOrDefault(t => t.Kind == SponsorTermKind.Conduct);
            if (conduct == null) { Assert.Ignore("this deal didn't carry a conduct term"); return; }

            bool bad = false;
            e.SponsorResolved += n => bad = !n.Good;

            e.State.LastEpisodeBackfired = true;
            e.BeginWeek();

            Assert.That(bad, Is.True);
            Assert.That(e.Sponsors.Active, Is.Null);
        }
    }
}
