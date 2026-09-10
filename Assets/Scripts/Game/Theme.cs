using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    /// <summary>Holds the two club colours and paints them onto specific elements at runtime.</summary>
    public sealed class Theme
    {
        public Color Primary { get; private set; } = new Color(0.184f, 0.427f, 0.71f);
        public Color Secondary { get; private set; } = new Color(0.949f, 0.757f, 0.306f);

        public void Set(string primaryHex, string secondaryHex)
        {
            Primary = Ui.ParseColor(primaryHex, Primary);
            Secondary = Ui.ParseColor(secondaryHex, Secondary);
        }

        public Color Ink(Color background)
        {
            float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luma > 0.55f ? new Color(0.09f, 0.09f, 0.11f) : Color.white;
        }

        /// <summary>A bar in the club's primary colour.</summary>
        public void PaintBar(VisualElement el)
        {
            el.style.backgroundColor = Primary;
        }

        public void PaintPrimaryButton(Button b)
        {
            b.style.backgroundColor = Secondary;
            b.style.color = Ink(Secondary);
        }

        public void PaintScoreboard(VisualElement el)
        {
            el.style.borderTopColor = Primary;
            el.style.borderBottomColor = Primary;
            el.style.borderLeftColor = Primary;
            el.style.borderRightColor = Primary;
        }
    }
}
