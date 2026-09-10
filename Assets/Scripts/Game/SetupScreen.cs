using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    public sealed class SetupScreen
    {
        readonly Bootstrap _host;

        string _podcast = "The Back Post";
        string _club = "Fenwick Rovers";
        string _primary = "#2F6DB5";
        string _secondary = "#F2C14E";
        Difficulty _difficulty = Difficulty.Regular;

        static readonly (string name, string hex)[] Palette =
        {
            ("Blue", "#2F6DB5"), ("Red", "#C0433F"), ("Green", "#2F9E63"),
            ("Claret", "#7A263B"), ("Sky", "#5FB4E5"), ("Amber", "#E7A544"),
            ("Black", "#20222A"), ("White", "#E9E7EE")
        };

        public SetupScreen(Bootstrap host) { _host = host; }

        public VisualElement Build()
        {
            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            col.Add(scroll);
            screen.Add(col);

            scroll.Add(Ui.Text("NEW PODCAST", "eyebrow"));
            scroll.Add(Ui.Text("Podcast Tycoon", "h1"));
            scroll.Add(Ui.Wrapping(
                "Name your show and the club you support, pick your colours, and choose how hard you want it. " +
                "Difficulty sets how good your club is — a weaker club means more crisis weeks.", "body"));
            scroll.Add(Ui.Box("divider"));

            var panel = Ui.Box("panel");

            panel.Add(Field("Podcast name", _podcast, v => _podcast = v));
            panel.Add(Field("Club you support", _club, v => _club = v));

            panel.Add(SwatchField("Primary colour", () => _primary, v => _primary = v));
            panel.Add(SwatchField("Secondary colour", () => _secondary, v => _secondary = v));

            scroll.Add(panel);

            scroll.Add(Ui.Text("DIFFICULTY", "eyebrow"));
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
                scroll.Add(DifficultyCard(d));

            var start = Ui.Btn("Start the podcast", () =>
            {
                _host.StartRun(new RunSetup
                {
                    PodcastName = string.IsNullOrWhiteSpace(_podcast) ? "The Untitled Pod" : _podcast.Trim(),
                    ClubName = string.IsNullOrWhiteSpace(_club) ? "Rovers" : _club.Trim(),
                    ColourPrimary = _primary,
                    ColourSecondary = _secondary,
                    Difficulty = _difficulty
                });
            }, "btn-primary");
            _host.Theme.Set(_primary, _secondary);
            _host.Theme.PaintPrimaryButton(start);
            start.style.marginTop = 10;
            scroll.Add(start);

            return screen;
        }

        VisualElement Field(string label, string value, System.Action<string> onChange)
        {
            var wrap = Ui.Box("field");
            wrap.Add(Ui.Text(label.ToUpperInvariant(), "field-label"));
            var tf = new TextField { value = value };
            tf.AddToClassList("text-input");
            tf.RegisterValueChangedCallback(e => onChange(e.newValue));
            wrap.Add(tf);
            return wrap;
        }

        VisualElement SwatchField(string label, System.Func<string> get, System.Action<string> set)
        {
            var wrap = Ui.Box("field");
            wrap.Add(Ui.Text(label.ToUpperInvariant(), "field-label"));
            var row = Ui.Box("swatch-row");

            void Refresh()
            {
                row.Clear();
                foreach (var (name, hex) in Palette)
                {
                    var sw = Ui.Box("swatch");
                    sw.tooltip = name;
                    sw.style.backgroundColor = Ui.ParseColor(hex, Color.gray);
                    if (get() == hex) sw.AddToClassList("selected");
                    sw.RegisterCallback<ClickEvent>(_ => { set(hex); Refresh(); });
                    row.Add(sw);
                }
            }
            Refresh();
            wrap.Add(row);
            return wrap;
        }

        VisualElement DifficultyCard(Difficulty d)
        {
            var profile = DifficultyProfile.For(d);
            var card = Ui.Box("diff-card");
            if (_difficulty == d) card.AddToClassList("selected");

            card.Add(Ui.Text(profile.Label, "diff-name"));
            card.Add(Ui.Wrapping(profile.Outlook, "body", "dim"));

            card.RegisterCallback<ClickEvent>(_ =>
            {
                _difficulty = d;
                var parent = card.parent;
                foreach (var child in parent.Children())
                    child.RemoveFromClassList("selected");
                card.AddToClassList("selected");
            });
            return card;
        }
    }
}
