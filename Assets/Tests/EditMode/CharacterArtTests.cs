using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;

namespace PodcastTycoon.Tests
{
    public class CharacterArtTests
    {
        [Test]
        public void HostCoHostLogoAndCoveredRolesLoadFromResources()
        {
            Assert.That(CharacterArt.Host(), Is.Not.Null);
            Assert.That(CharacterArt.CoHost(), Is.Not.Null);
            Assert.That(CharacterArt.Logo(), Is.Not.Null);
            Assert.That(CharacterArt.ForCrewRole(Crew.Producer), Is.Not.Null);
            Assert.That(CharacterArt.ForCrewRole(Crew.Clips), Is.Not.Null);
        }

        [Test]
        public void RolesWithoutCommissionedArtReturnNull()
        {
            // Documents the known gap (see CharacterArt's own header comment) rather than
            // letting it silently resolve to the wrong thing — if this starts failing because
            // someone added art for these roles, update PortraitCache's expectations too.
            Assert.That(CharacterArt.ForCrewRole(Crew.Researcher), Is.Null);
            Assert.That(CharacterArt.ForCrewRole(Crew.Booker), Is.Null);
        }

        [Test]
        public void PortraitCacheFallsBackToProceduralForUncoveredRoles()
        {
            // Must still resolve to *something* renderable even without fixed art.
            var tex = PortraitCache.Crew(Crew.Researcher, "Test Person");
            Assert.That(tex, Is.Not.Null);
            Assert.That(tex.width, Is.EqualTo(PixelPortraits.Grid), "should be the procedural generator's own size, not baked art");
        }

        [Test]
        public void PortraitCacheUsesFixedArtForCoveredRoles()
        {
            var tex = PortraitCache.Crew(Crew.Producer, "Test Person");
            Assert.That(tex, Is.SameAs(CharacterArt.ForCrewRole(Crew.Producer)));
        }
    }
}
