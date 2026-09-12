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
        readonly RunModifiers _mods = new RunModifiers();

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
            var col = Ui.Box("column-wide");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            col.Add(scroll);
            screen.Add(col);

            scroll.Add(Ui.Text("NEW PODCAST", "eyebrow"));
            var h1 = Ui.Text("Podcast Tycoon", "h1");
            h1.tooltip = "Name your show and the club you support, pick your colours, and choose how hard you want it.";
            scroll.Add(h1);

            var columns = Ui.Box("week-columns");
            var left = Ui.Box("week-col-left");
            var right = Ui.Box("week-col-right");
            columns.Add(left);
            columns.Add(right);
            scroll.Add(columns);

            var panel = Ui.Box("panel");
            panel.Add(Field("Podcast name", _podcast, v => _podcast = v));
            panel.Add(Field("Club you support", _club, v => _club = v));
            panel.Add(SwatchField("Primary colour", () => _primary, v => _primary = v));
            panel.Add(SwatchField("Secondary colour", () => _secondary, v => _secondary = v));
            left.Add(panel);

            var diffPanel = Ui.Box("panel");
            var diffTitle = Ui.Text("Difficulty", "h2");
            diffTitle.tooltip = "Difficulty sets how good your club is. A stronger club wins more — more feel-good " +
                "weeks and easier money. A weaker club loses more — tighter budget, but better drama.";
            diffPanel.Add(diffTitle);
            var diffGrid = Ui.Box("row-wrap");
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
                diffGrid.Add(DifficultyCard(d));
            diffPanel.Add(diffGrid);
            left.Add(diffPanel);

            var modsPanel = Ui.Box("panel");
            var modsTitle = Ui.Text("Custom modifiers", "h2");
            modsTitle.tooltip = "Optional tweaks to the run. Turning any on flags the run as Custom — the endless " +
                "chase still works, it just sits on its own board.";
            modsPanel.Add(modsTitle);
            modsPanel.Add(ModToggle("+1 prep point every week", () => _mods.ExtraPrep, v => _mods.ExtraPrep = v));
            modsPanel.Add(ModToggle("Churn −20% (listeners leave more slowly)", () => _mods.GentleChurn, v => _mods.GentleChurn = v));
            modsPanel.Add(ModToggle("Start with +€300", () => _mods.NestEgg, v => _mods.NestEgg = v));
            modsPanel.Add(ModToggle("Longer runway (5 weeks in the red before folding)", () => _mods.LongRunway, v => _mods.LongRunway = v));
            modsPanel.Add(ModToggle("No international breaks", () => _mods.NoInternationalBreaks, v => _mods.NoInternationalBreaks = v));
            modsPanel.Add(ModToggle("Sponsor-free (no deals, higher ad rate)", () => _mods.SponsorFree, v => _mods.SponsorFree = v));
            modsPanel.Add(ModToggle("Chaos cycle (more events, wilder results)", () => _mods.ChaosCycle, v => _mods.ChaosCycle = v));
            modsPanel.Add(ModToggle("Sandbox (no bankruptcy)", () => _mods.Sandbox, v => _mods.Sandbox = v));
            right.Add(modsPanel);

            var start = Ui.Btn("Start the podcast", () =>
            {
                _host.StartRun(new RunSetup
                {
                    PodcastName = string.IsNullOrWhiteSpace(_podcast) ? "The Untitled Pod" : _podcast.Trim(),
                    ClubName = string.IsNullOrWhiteSpace(_club) ? "Rovers" : _club.Trim(),
                    ColourPrimary = _primary,
                    ColourSecondary = _secondary,
                    Difficulty = _difficulty,
                    Modifiers = _mods.Clone()
                });
            }, "btn-primary");
            _host.Theme.Set(_primary, _secondary);
            _host.Theme.PaintPrimaryButton(start);
            start.style.marginTop = 10;
            scroll.Add(start);

            return screen;
        }

        VisualElement ModToggle(string label, System.Func<bool> get, System.Action<bool> set)
        {
            var t = new Toggle(label) { value = get() };
            t.RegisterValueChangedCallback(e => set(e.newValue));
            t.style.marginBottom = 4;
            return t;
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
                    var col = Ui.ParseColor(hex, Color.gray);
                    var sw = Ui.Box("swatch");
                    sw.tooltip = name;
                    sw.style.backgroundColor = col;
                    if (get() == hex)
                    {
                        sw.AddToClassList("selected");
                        var check = Ui.Text("✓", "swatch-check");
                        check.style.color = _host.Theme.Ink(col);
                        sw.Add(check);
                    }
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
