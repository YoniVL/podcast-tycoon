using System;
using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    /// <summary>The first thing you see: the fully-built studio, the logo and "tap to start".</summary>
    public sealed class TitleScreen
    {
        readonly Bootstrap _host;
        public TitleScreen(Bootstrap host) { _host = host; }

        public VisualElement Build()
        {
            var screen = Ui.Box("title-screen");

            // The dream studio — every upgrade bought — as the cover picture.
            var art = Ui.StudioBackdrop(4, 4, 4, 4, 4);
            art.style.marginBottom = 0;
            screen.Add(art);

            var middle = Ui.Box("title-middle");
            var logoTex = CharacterArt.Logo();
            if (logoTex != null)
                middle.Add(Ui.Art(logoTex, 280, 280 * logoTex.height / (float)logoTex.width, "title-logo"));
            else
                middle.Add(Ui.Text("Podcast Tycoon", "h1"));
            middle.Add(Ui.Text("A football podcast tycoon", "title-tagline"));
            var tap = Ui.Text("TAP TO START", "title-tap");
            middle.Add(tap);
            screen.Add(middle);
            tap.schedule.Execute(() => tap.style.opacity = tap.style.opacity.value < 0.5f ? 1f : 0.25f).Every(650);

            screen.Add(Ui.Text("v" + Application.version, "title-version"));

            screen.RegisterCallback<ClickEvent>(_ => _host.ShowMenu());
            return screen;
        }
    }

    /// <summary>New game / load game.</summary>
    public sealed class MenuScreen
    {
        readonly Bootstrap _host;
        readonly string _notice;

        public MenuScreen(Bootstrap host, string notice = null)
        {
            _host = host;
            _notice = notice;
        }

        public static string Describe(SaveHeader h)
        {
            string when = "";
            if (DateTime.TryParse(h.SavedAtUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var utc))
                when = utc.ToLocalTime().ToString("d MMM, HH:mm");
            return $"Season {h.Season} · Week {h.Week} · {h.Listeners:N0} listeners\n" +
                   $"{h.EpisodesPublished} episode{(h.EpisodesPublished == 1 ? "" : "s")} published" +
                   (when.Length > 0 ? $" · saved {when}" : "");
        }

        public VisualElement Build()
        {
            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            screen.Add(col);

            var logoTex = CharacterArt.Logo();
            if (logoTex != null)
                col.Add(Ui.Art(logoTex, 220, 220 * logoTex.height / (float)logoTex.width, "menu-logo"));
            else
                col.Add(Ui.Text("Podcast Tycoon", "h1"));

            if (!string.IsNullOrEmpty(_notice))
            {
                var toast = Ui.Box("toast", "toast-bad");
                toast.Add(Ui.Wrapping(_notice, "body"));
                col.Add(toast);
            }

            var header = SaveStore.ReadHeader();

            var load = Ui.Box("panel");
            load.Add(Ui.Text("LOAD GAME", "eyebrow"));
            if (header != null)
            {
                load.Add(Ui.Text(header.PodcastName, "h2"));
                load.Add(Ui.Text(header.ClubName, "club-name-left"));
                load.Add(Ui.Wrapping(Describe(header), "body", "dim"));
                var btn = Ui.Btn("Load game", _host.LoadGame, "btn-primary");
                _host.Theme.PaintPrimaryButton(btn);
                btn.style.marginTop = 8;
                load.Add(btn);
                load.Add(Ui.Wrapping("Your run saves itself after every episode.", "body", "dim"));
            }
            else
            {
                load.Add(Ui.Wrapping("No saved game yet. Your run saves itself after every episode.", "body", "dim"));
                var btn = Ui.Btn("Load game", () => { }, "btn-ghost");
                btn.SetEnabled(false);
                btn.style.marginTop = 8;
                load.Add(btn);
            }
            col.Add(load);

            var fresh = Ui.Box("panel");
            fresh.Add(Ui.Text("NEW GAME", "eyebrow"));
            fresh.Add(Ui.Wrapping("Name your show, pick your club and start from the bedroom.", "body", "dim"));
            if (header != null)
                fresh.Add(Ui.Wrapping("Your saved game stays until your new run publishes its first episode.", "body", "dim"));
            var start = Ui.Btn("New game", _host.ShowSetup, header == null ? "btn-primary" : "btn-ghost");
            if (header == null) _host.Theme.PaintPrimaryButton(start);
            start.style.marginTop = 8;
            fresh.Add(start);
            col.Add(fresh);

            return screen;
        }
    }
}
