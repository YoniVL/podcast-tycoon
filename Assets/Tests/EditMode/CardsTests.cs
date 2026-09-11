using System.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class CardsTests
    {
        static Engine NewEngine(int seed = 1) =>
            new Engine(new RunSetup { ClubName = "Testford" }, new GameConfig(), new SystemRng(seed));

        [Test]
        public void DrawingARandomCardCostsMoneyAndAddsOneToHand()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            e.State.Hand.Clear();
            int before = e.State.Hand.Count;
            float moneyBefore = e.State.Money;

            Assert.That(e.DrawRandomCard(), Is.True);

            Assert.That(e.State.Hand.Count, Is.EqualTo(before + 1));
            Assert.That(e.State.Money, Is.EqualTo(moneyBefore - e.Config.CardDrawCost));
        }

        [Test]
        public void BuyingASpecificCardAddsExactlyThatCard()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            e.State.Hand.Clear();

            Assert.That(e.BuySpecificCard("hot_mic"), Is.True);
            Assert.That(e.State.Hand, Contains.Item("hot_mic"));
            Assert.That(e.BuySpecificCard("hot_mic"), Is.False, "already in hand");
        }

        [Test]
        public void SlottingAContactAppliesItsWeeklyTick()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            e.State.Contacts.Clear();

            Assert.That(e.SlotContact("veteran_pundit"), Is.True);
            float repBefore = e.State.Reputation;
            float moneyBefore = e.State.Money;

            e.BeginWeek();   // CardManager.BeginWeek ticks contacts

            Assert.That(e.State.Reputation, Is.GreaterThan(repBefore), "veteran pundit should build reputation weekly");
            Assert.That(e.State.Money, Is.LessThan(moneyBefore), "veteran pundit costs a weekly retainer");
        }

        [Test]
        public void UnslottingAContactStopsTheEffect()
        {
            var e = NewEngine();
            e.State.Money = 1000;
            e.State.Contacts.Clear();
            e.SlotContact("fan_liaison");
            Assert.That(e.UnslotContact("fan_liaison"), Is.True);

            float moraleBefore = e.State.Morale;
            e.BeginWeek();
            Assert.That(e.State.Morale, Is.EqualTo(moraleBefore));
        }

        [Test]
        public void CannotSlotMoreContactsThanTheLimit()
        {
            var e = NewEngine();
            e.State.Money = 10000;
            e.State.Contacts.Clear();
            var ids = CardManager.Contacts.Select(c => c.Id).ToList();
            for (int i = 0; i < e.Config.ContactSlots; i++)
                Assert.That(e.SlotContact(ids[i]), Is.True);

            Assert.That(e.State.Contacts.Count, Is.EqualTo(e.Config.ContactSlots));
            Assert.That(e.SlotContact(ids[e.Config.ContactSlots]), Is.False, "no room for a fourth contact");
        }
    }
}
