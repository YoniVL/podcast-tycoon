using NUnit.Framework;
using PodcastTycoon.Game;
using UnityEngine;

namespace PodcastTycoon.Tests
{
    public class PixelIconsTests
    {
        [Test]
        public void EveryStatIconGeneratesA22x22Texture()
        {
            foreach (StatIcon icon in System.Enum.GetValues(typeof(StatIcon)))
            {
                var tex = IconCache.Get(icon);
                Assert.That(tex.width, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.height, Is.EqualTo(PixelPortraits.Grid));
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point));
            }
        }

        [Test]
        public void IconCacheReturnsTheSameTextureInstance()
        {
            var a = IconCache.Get(StatIcon.Money);
            var b = IconCache.Get(StatIcon.Money);
            Assert.That(a, Is.SameAs(b));
        }

        [Test]
        public void DifferentIconsAreVisuallyDistinct()
        {
            // Not every pair is guaranteed to differ, but no two should be pixel-identical —
            // that would mean a copy-paste mistake left one icon undrawn.
            var seen = new System.Collections.Generic.List<Color?[,]>();
            foreach (StatIcon icon in System.Enum.GetValues(typeof(StatIcon)))
            {
                var grid = PixelIcons.Generate(icon);
                foreach (var other in seen)
                    Assert.That(GridsEqual(grid, other), Is.False, $"{icon} looks identical to another icon");
                seen.Add(grid);
            }
        }

        static bool GridsEqual(Color?[,] a, Color?[,] b)
        {
            for (int y = 0; y < PixelPortraits.Grid; y++)
                for (int x = 0; x < PixelPortraits.Grid; x++)
                    if (a[y, x] != b[y, x]) return false;
            return true;
        }
    }
}
