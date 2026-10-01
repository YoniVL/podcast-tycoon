using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;
using UnityEngine;

namespace PodcastTycoon.Tests
{
    public class CardArtTests
    {
        [Test]
        public void EveryCardHasAResolvable32x32Illustration()
        {
            foreach (var card in CardManager.Catalog.Values)
            {
                var tex = CardArtCache.GetCard(card.Id);
                Assert.That(tex, Is.Not.Null, card.Id + " has no illustration");
                Assert.That(tex.width, Is.EqualTo(CardArt.CardW));
                Assert.That(tex.height, Is.EqualTo(CardArt.CardH));
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }

        [Test]
        public void EveryContactHasAResolvable40x28Illustration()
        {
            foreach (var contact in CardManager.Contacts)
            {
                var tex = CardArtCache.GetContact(contact.Id);
                Assert.That(tex, Is.Not.Null, contact.Id + " has no illustration");
                Assert.That(tex.width, Is.EqualTo(CardArt.ContactW));
                Assert.That(tex.height, Is.EqualTo(CardArt.ContactH));
            }
        }

        [Test]
        public void UnknownIdsReturnNullInsteadOfThrowing()
        {
            Assert.That(CardArtCache.GetCard("not_a_real_card"), Is.Null);
            Assert.That(CardArtCache.GetContact("not_a_real_contact"), Is.Null);
        }

        [Test]
        public void CardCacheReturnsTheSameTextureInstance()
        {
            var a = CardArtCache.GetCard("hot_mic");
            var b = CardArtCache.GetCard("hot_mic");
            Assert.That(a, Is.SameAs(b));
        }

        [Test]
        public void EveryCardIllustrationHasVisibleContent()
        {
            // Catches a generator that silently draws nothing (e.g. an off-canvas shape).
            foreach (var card in CardManager.Catalog.Values)
            {
                var tex = CardArtCache.GetCard(card.Id);
                int opaque = 0;
                foreach (var p in tex.GetPixels32()) if (p.a > 200) opaque++;
                Assert.That(opaque, Is.GreaterThan(20), card.Id + " illustration looks blank");
            }
        }

        [Test]
        public void AllTwentyFiveStudioBackgroundPiecesLoadAtTheSameSize()
        {
            // Pinned to the current "cozy_chibi_booth" recording-layers pack (682x384) rather
            // than a specific resolution — what actually matters is that every layer shares one
            // canvas size so shell/overlays composite in alignment; re-pin the literal numbers
            // if a future art pass changes the delivered resolution.
            var tracks = new[] { "studio_shell", "set_overlay", "audio_overlay", "post_overlay", "distribution_overlay" };
            int? w = null, h = null;
            foreach (var prefix in tracks)
            {
                for (int tier = 0; tier <= 4; tier++)
                {
                    var tex = StudioArt.Get(prefix, tier);
                    Assert.That(tex, Is.Not.Null, $"{prefix}_{tier} failed to load from Resources/Studio");
                    w ??= tex.width; h ??= tex.height;
                    Assert.That(tex.width, Is.EqualTo(w), $"{prefix}_{tier} width doesn't match the other layers");
                    Assert.That(tex.height, Is.EqualTo(h), $"{prefix}_{tier} height doesn't match the other layers");
                }
            }
        }

        [Test]
        public void StudioArtClampsOutOfRangeTiers()
        {
            Assert.That(StudioArt.Get("studio_shell", -1), Is.SameAs(StudioArt.Get("studio_shell", 0)));
            Assert.That(StudioArt.Get("studio_shell", 9), Is.SameAs(StudioArt.Get("studio_shell", 4)));
        }
    }
}
