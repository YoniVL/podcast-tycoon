using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PodcastTycoon.Core;

namespace PodcastTycoon.Tests
{
    public class SaveGameTests
    {
        static Engine NewEngine(int seed = 11, Difficulty diff = Difficulty.Regular) =>
            new Engine(new RunSetup { PodcastName = "Save FM", ClubName = "Testford", Difficulty = diff },
                       new GameConfig(), new SystemRng(seed));

        /// <summary>Plays one simple week: settle anything pending, then publish the first offered topic.</summary>
        static void PlayWeek(Engine e)
        {
            e.BeginWeek();
            if (e.Events.Pending != null) e.ResolveEvent(0);
            if (e.Scoops.Pending != null) e.ResolveScoop(ScoopChoice.VerifyHold);
            if (e.State.BuyoutPending) e.DeclineBuyout();

            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]);
            plan.Main.Angle = Angle.Analysis;
            plan.Main.Prep = 2;
            plan.Push = 2;
            e.Publish(plan);
        }

        static string WithoutTimestamp(string json)
        {
            var o = JObject.Parse(json);
            ((JObject)o["header"]).Remove("SavedAtUtc");
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }

        [Test]
        public void ARunSurvivesASaveAndLoadUnchanged()
        {
            var e = NewEngine();
            for (int i = 0; i < 30; i++) PlayWeek(e);

            string saved = SaveGame.Serialize(e);
            var loaded = SaveGame.Deserialize(saved, new SystemRng(5));

            Assert.That(WithoutTimestamp(SaveGame.Serialize(loaded)), Is.EqualTo(WithoutTimestamp(saved)),
                "saving a freshly loaded run must reproduce the same save");
            Assert.That(loaded.State.Listeners, Is.EqualTo(e.State.Listeners));
            Assert.That(loaded.State.Money, Is.EqualTo(e.State.Money));
            Assert.That(loaded.State.EpisodesPublished, Is.EqualTo(e.State.EpisodesPublished));
            Assert.That(loaded.State.Episodes.Count, Is.EqualTo(e.State.Episodes.Count));
            Assert.That(loaded.Roster.Players.Select(p => p.Name), Is.EqualTo(e.Roster.Players.Select(p => p.Name)));
            Assert.That(loaded.Calendar.Clubs.Select(c => c.Points), Is.EqualTo(e.Calendar.Clubs.Select(c => c.Points)));
        }

        [Test]
        public void ALoadedRunKeepsPlayingAcrossSeasons()
        {
            var e = NewEngine(seed: 3);
            for (int i = 0; i < 12; i++) PlayWeek(e);
            var loaded = SaveGame.Deserialize(SaveGame.Serialize(e), new SystemRng(99));

            int guard = 0;
            int startSeason = loaded.State.Season;
            while (loaded.State.Season < startSeason + 2 && !loaded.State.IsGameOver && guard++ < 150)
                PlayWeek(loaded);

            Assert.That(guard, Is.LessThan(150));
            Assert.That(loaded.State.EpisodesPublished, Is.GreaterThan(e.State.EpisodesPublished));
        }

        [Test]
        public void CrewUpgradesCardsAndContactsAreSaved()
        {
            var e = NewEngine(seed: 8);
            e.State.Money = 50000f;
            e.BeginWeek();
            foreach (var role in new[] { Crew.Producer, Crew.Researcher })
                if (e.CandidatesFor(role).Count > 0 && e.CanHireCandidate(role, 0)) e.HireCandidate(role, 0);
            if (e.CanBuyUpgrade(UpgradeTrack.Audio)) e.BuyUpgrade(UpgradeTrack.Audio);
            if (e.CanHireCoHost()) e.HireCoHost();
            e.State.Hand.Add("hot_mic");
            var contact = CardManager.Contacts.First();
            if (e.Cards.CanSlotContact(e.State, contact.Id)) e.SlotContact(contact.Id);
            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]); plan.Main.Angle = Angle.Analysis; plan.Main.Prep = 2;
            e.Publish(plan);

            var loaded = SaveGame.Deserialize(SaveGame.Serialize(e), new SystemRng(1));
            Assert.That(loaded.State.Crew, Is.EqualTo(e.State.Crew));
            Assert.That(loaded.State.Employed.Keys, Is.EquivalentTo(e.State.Employed.Keys));
            foreach (var kv in e.State.Employed)
            {
                Assert.That(loaded.State.Employed[kv.Key].Name, Is.EqualTo(kv.Value.Name));
                Assert.That(loaded.State.Employed[kv.Key].Traits, Is.EqualTo(kv.Value.Traits));
            }
            Assert.That(loaded.State.AudioTier, Is.EqualTo(e.State.AudioTier));
            Assert.That(loaded.State.HasCoHost, Is.EqualTo(e.State.HasCoHost));
            Assert.That(loaded.State.Hand, Is.EqualTo(e.State.Hand));
            Assert.That(loaded.State.Contacts, Is.EqualTo(e.State.Contacts));
            Assert.That(loaded.State.CandidatePool.Keys, Is.EquivalentTo(e.State.CandidatePool.Keys));
        }

        [Test]
        public void HeaderDescribesTheSaveWithoutLoadingIt()
        {
            var e = NewEngine();
            PlayWeek(e);
            var h = SaveGame.ReadHeader(SaveGame.Serialize(e));
            Assert.That(h.PodcastName, Is.EqualTo("Save FM"));
            Assert.That(h.EpisodesPublished, Is.EqualTo(1));
            Assert.That(h.Version, Is.EqualTo(SaveGame.CurrentVersion));
        }

        [Test]
        public void GarbageAndWrongVersionsAreRejectedWithAClearError()
        {
            Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize("not json at all", new SystemRng(1)));
            Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize("{}", new SystemRng(1)));
            Assert.Throws<SaveFormatException>(() => SaveGame.ReadHeader("[1,2"));

            var e = NewEngine();
            var o = JObject.Parse(SaveGame.Serialize(e));
            o["header"]["Version"] = 999;
            Assert.Throws<SaveFormatException>(() => SaveGame.Deserialize(o.ToString(), new SystemRng(1)));
        }
    }
}
