using NUnit.Framework;
using PodcastTycoon.Core;
using PodcastTycoon.Game;
using UnityEngine;

namespace PodcastTycoon.Tests
{
    public class CardIconsTests
    {
        [Test]
        public void EveryCardIconGeneratesA22x22Texture()
        {
            foreach (CardIcon icon in System.Enum.GetValues(typeof(CardIcon)))
            {
                var tex = CardIconCache.Get(icon);
                Assert.That(tex.width, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.height, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }

        [Test]
        public void UnknownCategoryFallsBackInsteadOfThrowing()
        {
            var tex = CardIconCache.Get((string)null);
            Assert.That(tex, Is.Not.Null);
            Assert.That(CardIconCache.Get("not_a_real_category"), Is.SameAs(tex));
        }

        [Test]
        public void EveryCardAndContactHasAResolvableIconCategory()
        {
            foreach (var card in CardManager.Catalog.Values)
                Assert.That(CardIconCache.Get(card.IconCategory), Is.Not.Null, card.Id + " has no resolvable icon category");
            foreach (var contact in CardManager.Contacts)
                Assert.That(CardIconCache.Get(contact.IconCategory), Is.Not.Null, contact.Id + " has no resolvable icon category");
        }
    }
}
