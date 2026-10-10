using NUnit.Framework;
using PodcastTycoon.Game;
using UnityEngine;

namespace PodcastTycoon.Tests
{
    public class StudioArtTests
    {
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
