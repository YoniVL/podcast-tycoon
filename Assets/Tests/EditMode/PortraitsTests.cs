using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;
using UnityEngine;

namespace PodcastTycoon.Tests
{
    public class PortraitsTests
    {
        [Test]
        public void EveryHairStyleAndAccessoryGeneratesA22x22Texture()
        {
            foreach (PortraitHairStyle hair in System.Enum.GetValues(typeof(PortraitHairStyle)))
            foreach (PortraitAccessory acc in System.Enum.GetValues(typeof(PortraitAccessory)))
            {
                var spec = new PortraitSpec
                {
                    Skin = PixelPortraits.Skins[0], Hair = PixelPortraits.Hairs[0], Outfit = PixelPortraits.Outfits[0],
                    Accent = PixelPortraits.Accent, HairStyle = hair, Accessory = acc,
                };
                var tex = PixelPortraits.BuildTexture(PixelPortraits.Generate(spec), PixelPortraits.Outline);
                Assert.That(tex.width, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.height, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }

        [Test]
        public void SameNameAndRoleAlwaysCastsTheSameLook()
        {
            var a = PortraitCasting.ForCrew(Crew.Producer, "Dana Ruiz");
            var b = PortraitCasting.ForCrew(Crew.Producer, "Dana Ruiz");
            Assert.That(a.Skin, Is.EqualTo(b.Skin));
            Assert.That(a.Hair, Is.EqualTo(b.Hair));
            Assert.That(a.Outfit, Is.EqualTo(b.Outfit));
            Assert.That(a.HairStyle, Is.EqualTo(b.HairStyle));
            Assert.That(a.Accessory, Is.EqualTo(b.Accessory));
        }

        [Test]
        public void DifferentRolesCanCastDifferentLooksForTheSameName()
        {
            // Not a strict guarantee for every name, but the role is folded into the hash —
            // this specific pair is known to diverge, guarding against a role-blind regression.
            var producer = PortraitCasting.ForCrew(Crew.Producer, "Alex Kim");
            var booker = PortraitCasting.ForCrew(Crew.Booker, "Alex Kim");
            bool anyDifference = producer.Skin != booker.Skin || producer.Hair != booker.Hair ||
                                  producer.Outfit != booker.Outfit || producer.HairStyle != booker.HairStyle ||
                                  producer.Accessory != booker.Accessory;
            Assert.That(anyDifference, Is.True);
        }

        [Test]
        public void PortraitCacheReturnsTheSameTextureInstanceForTheSameIdentity()
        {
            var t1 = PortraitCache.Crew(Crew.Researcher, "Tomasz Wik");
            var t2 = PortraitCache.Crew(Crew.Researcher, "Tomasz Wik");
            Assert.That(t1, Is.SameAs(t2));
        }

        [Test]
        public void HostAndCoHostHaveDistinctFixedLooks()
        {
            var host = PortraitCasting.ForHost();
            var coHost = PortraitCasting.ForCoHost();
            Assert.That(host.Skin != coHost.Skin || host.Hair != coHost.Hair || host.HairStyle != coHost.HairStyle,
                "host and co-host should look like different people");
        }
    }
}
