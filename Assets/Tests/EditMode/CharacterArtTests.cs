using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;

namespace PodcastTycoon.Tests
{
    public class CharacterArtTests
    {
        [Test]
        public void HostCoHostAndLogoLoadFromResources()
        {
            Assert.That(CharacterArt.Host(), Is.Not.Null);
            Assert.That(CharacterArt.CoHost(), Is.Not.Null);
            Assert.That(CharacterArt.Logo(), Is.Not.Null);
        }

        [Test]
        public void EveryStatAndCrewIconHasADistinctLowercaseFileName()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (StatIcon i in System.Enum.GetValues(typeof(StatIcon)))
                Assert.That(seen.Add(IconArt.StatFile(i)), Is.True, i + " collides with another icon file");
            foreach (Crew r in System.Enum.GetValues(typeof(Crew)))
                if (r != Crew.None)
                Assert.That(seen.Add(IconArt.CrewFile(r)), Is.True, r + " collides with another icon file");
            foreach (var name in seen) Assert.That(name, Is.EqualTo(name.ToLowerInvariant()));
        }

        [Test]
        public void EveryStatAndCrewIconLoadsAtTheSameSize()
        {
            int? size = null;
            void Check(UnityEngine.Texture2D tex, string what)
            {
                Assert.That(tex, Is.Not.Null, what + " is missing from Resources/Icons");
                size ??= tex.width;
                Assert.That(tex.width, Is.EqualTo(size), what + " width differs from the other icons");
                Assert.That(tex.height, Is.EqualTo(size), what + " is not square");
            }
            foreach (StatIcon i in System.Enum.GetValues(typeof(StatIcon))) Check(IconArt.Stat(i), IconArt.StatFile(i));
            foreach (Crew r in System.Enum.GetValues(typeof(Crew)))
                if (r != Crew.None) Check(IconArt.CrewRole(r), IconArt.CrewFile(r));
        }

        [Test]
        public void EveryStatHasAFallbackLabelForWhenAnIconFileIsMissing()
        {
            foreach (StatIcon i in System.Enum.GetValues(typeof(StatIcon)))
                Assert.That(IconArt.StatLabel(i), Is.Not.Empty);
        }
    }
}
