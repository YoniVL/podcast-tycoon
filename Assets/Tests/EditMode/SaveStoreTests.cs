using System.IO;
using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;

namespace PodcastTycoon.Tests
{
    public class SaveStoreTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pt_save_test_" + System.Guid.NewGuid().ToString("N"));
            SaveStore.DirectoryOverride = _dir;
        }

        [TearDown]
        public void TearDown()
        {
            SaveStore.DirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static Engine PlayedEngine()
        {
            var e = new Engine(new RunSetup { PodcastName = "Disk FM", ClubName = "Testford" }, new GameConfig(), new SystemRng(4));
            e.BeginWeek();
            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]); plan.Main.Angle = Angle.Analysis; plan.Main.Prep = 2;
            e.Publish(plan);
            return e;
        }

        [Test]
        public void NothingIsSavedUntilTheFirstSave()
        {
            Assert.That(SaveStore.Exists(), Is.False);
            Assert.That(SaveStore.ReadHeader(), Is.Null);
            var engine = SaveStore.Load(new SystemRng(1), out string error);
            Assert.That(engine, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void SaveThenLoadRoundTripsThroughTheFile()
        {
            var e = PlayedEngine();
            Assert.That(SaveStore.Save(e), Is.True);
            Assert.That(SaveStore.Exists(), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "save.json.tmp")), Is.False, "the temp file must not be left behind");

            var header = SaveStore.ReadHeader();
            Assert.That(header.PodcastName, Is.EqualTo("Disk FM"));
            Assert.That(header.EpisodesPublished, Is.EqualTo(1));

            var loaded = SaveStore.Load(new SystemRng(2), out string error);
            Assert.That(error, Is.Null);
            Assert.That(loaded.State.Listeners, Is.EqualTo(e.State.Listeners));
        }

        [Test]
        public void SavingAgainReplacesTheOldSave()
        {
            var e = PlayedEngine();
            SaveStore.Save(e);
            e.BeginWeek();
            var plan = new ProductionPlan();
            plan.Main.Set(e.Offer[0]); plan.Main.Angle = Angle.Analysis; plan.Main.Prep = 2;
            e.Publish(plan);
            SaveStore.Save(e);
            Assert.That(SaveStore.ReadHeader().EpisodesPublished, Is.EqualTo(2));
        }

        [Test]
        public void ADamagedSaveGivesAnErrorAndIsKeptAside()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(SaveStore.SavePath, "{ this is not a save");
            Assert.That(SaveStore.ReadHeader(), Is.Null);

            var engine = SaveStore.Load(new SystemRng(1), out string error);
            Assert.That(engine, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(SaveStore.Exists(), Is.False, "the broken file is moved out of the way");
            Assert.That(File.Exists(Path.Combine(_dir, "save.damaged.json")), Is.True, "but not thrown away");
        }

        [Test]
        public void DeleteRemovesTheSave()
        {
            SaveStore.Save(PlayedEngine());
            SaveStore.Delete();
            Assert.That(SaveStore.Exists(), Is.False);
            SaveStore.Delete(); // deleting nothing is fine
        }
    }
}
