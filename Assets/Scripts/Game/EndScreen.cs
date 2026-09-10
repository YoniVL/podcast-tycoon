using PodcastTycoon.Core;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    public sealed class EndScreen
    {
        readonly Bootstrap _host;
        readonly string _eyebrow;
        readonly string _title;
        readonly string _body;

        EndScreen(Bootstrap host, string eyebrow, string title, string body)
        {
            _host = host;
            _eyebrow = eyebrow;
            _title = title;
            _body = body;
        }

        public static EndScreen GameOver(Bootstrap host)
        {
            var st = host.Engine.State;
            string body =
                $"{st.GameOverReason}\n\n" +
                $"You lasted {st.EpisodesPublished} episodes across {st.Season} season(s), " +
                $"and peaked at {st.PeakListeners:N0} listeners.";
            return new EndScreen(host, "THE END", "Off the air", body);
        }

        public VisualElement Build()
        {
            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            screen.Add(col);

            col.Add(Ui.Text(_eyebrow, "eyebrow"));
            col.Add(Ui.Text(_title, "h1"));
            col.Add(Ui.Wrapping(_body, "body"));

            var again = Ui.Btn("Start a new podcast", _host.RestartToSetup, "btn-primary");
            _host.Theme.PaintPrimaryButton(again);
            again.style.marginTop = 14;
            col.Add(again);

            return screen;
        }
    }
}
