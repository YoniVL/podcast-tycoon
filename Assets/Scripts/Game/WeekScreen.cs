using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    public sealed class WeekScreen
    {
        readonly Bootstrap _host;
        Engine E => _host.Engine;

        readonly ProductionPlan _plan = new ProductionPlan();
        TopicId? _picked;

        VisualElement _topicList;
        VisualElement _prepBlock;
        VisualElement _previewBlock;
        Label _prepMeter;
        Button _publish;
        Button _redraw;

        sealed class PrepControl
        {
            public SliderInt Slider;
            public Label Value;
            public System.Func<int> Get;
        }

        readonly List<PrepControl> _prep = new List<PrepControl>();

        public WeekScreen(Bootstrap host) { _host = host; }

        public VisualElement Build()
        {
            var ctx = E.CurrentWeek;
            var st = E.State;

            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            col.Add(scroll);
            screen.Add(col);

            // --- header bar ---
            var bar = Ui.Box("panel", "panel-tight");
            _host.Theme.PaintBar(bar);
            var barInk = _host.Theme.Ink(_host.Theme.Primary);
            var t1 = Ui.Text(st.PodcastName, "h2");
            t1.style.color = barInk;
            t1.style.marginTop = 0;
            t1.style.marginBottom = 0;
            var t2 = Ui.Text($"Season {st.Season} · Week {ctx.Turn} · {st.ClubName} sit {ctx.LeaguePositionLabel}", "body");
            t2.style.color = barInk;
            bar.Add(t1);
            bar.Add(t2);
            scroll.Add(bar);

            // --- resource strip ---
            var strip = Ui.Box("statstrip");
            strip.Add(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null));
            strip.Add(Ui.Stat("Listeners", st.Listeners.ToString("N0")));
            strip.Add(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString()));
            strip.Add(Ui.Stat("Buzz", st.Buzz.ToString()));
            scroll.Add(strip);

            // --- the week ---
            scroll.Add(BuildMatchPanel(ctx));

            // --- topic offer ---
            var offerPanel = Ui.Box("panel");
            var offerHead = Ui.Row();
            offerHead.Add(Ui.Text("This week's episode", "h2"));
            _redraw = Ui.Btn(RedrawLabel(), Redraw, "btn-ghost");
            _redraw.SetEnabled(!E.HasRedrawnThisWeek && (E.State.Money >= E.Config.RedrawCost || E.State.Buzz >= 1));
            offerHead.Add(_redraw);
            offerPanel.Add(offerHead);
            offerPanel.Add(Ui.Wrapping("Pick your angle. Appeal shown is after this week's context.", "body", "dim"));
            _topicList = Ui.Box();
            offerPanel.Add(_topicList);
            RenderTopics();
            scroll.Add(offerPanel);

            // --- production ---
            _prepBlock = Ui.Box("panel");
            _prepBlock.style.display = DisplayStyle.None;
            _prepBlock.Add(Ui.Text("Production", "h2"));
            _prepMeter = Ui.Text("", "prep-meter");
            _prepBlock.Add(_prepMeter);
            _prepBlock.Add(Slider("Topic prep", () => _plan.PrepTopic, v => _plan.PrepTopic = v));
            _prepBlock.Add(Slider("Research", () => _plan.PrepResearch, v => _plan.PrepResearch = v));
            _prepBlock.Add(Slider("Audio", () => _plan.PrepAudio, v => _plan.PrepAudio = v));
            _prepBlock.Add(Slider("Promo", () => _plan.PrepPromo, v => _plan.PrepPromo = v));
            _prepBlock.Add(Ui.Divider());
            _previewBlock = Ui.Box();
            _prepBlock.Add(_previewBlock);
            scroll.Add(_prepBlock);

            // --- studio ---
            scroll.Add(BuildStudioPanel());

            // --- publish ---
            _publish = Ui.Btn("Record & release", Publish, "btn-primary");
            _host.Theme.PaintPrimaryButton(_publish);
            _publish.style.marginTop = 6;
            _publish.SetEnabled(false);
            scroll.Add(_publish);

            return screen;
        }

        // ------------------------------------------------------------------
        VisualElement BuildMatchPanel(WeekContext ctx)
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("The week", "h2"));

            if (ctx.IsMatchless)
            {
                panel.Add(Ui.Wrapping(ctx.Headline, "body"));
                return panel;
            }

            var fx = ctx.Fixture;
            var m = ctx.Match;

            var board = Ui.Box("scoreboard");
            _host.Theme.PaintScoreboard(board);
            board.Add(Ui.Text(fx.Home ? E.State.ClubName : fx.Opponent, "club-name"));
            board.Add(Ui.Text(fx.Home ? $"{m.GoalsFor}–{m.GoalsAgainst}" : $"{m.GoalsAgainst}–{m.GoalsFor}", "score"));
            board.Add(Ui.Text(fx.Home ? fx.Opponent : E.State.ClubName, "club-name"));
            panel.Add(board);

            var meta = Ui.Row();
            meta.style.marginTop = 8;
            var badge = Ui.Text(SurpriseLabel(m.Surprise), "badge");
            PaintBadge(badge, m.Surprise);
            meta.Add(badge);
            meta.Add(Ui.Text(fx.Home ? "Home" : "Away", "chip"));
            if (fx.Importance != FixtureImportance.Normal)
                meta.Add(Ui.Text(fx.Importance == FixtureImportance.Derby ? "Derby" : "Big match", "chip"));
            panel.Add(meta);

            panel.Add(Ui.Wrapping(ctx.Headline, "body"));
            return panel;
        }

        VisualElement BuildStudioPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Studio", "h2"));
            var st = E.State;

            void GearRow(string name, Gear g, string effect)
            {
                var row = Ui.Row();
                var left = Ui.Box();
                left.style.flexGrow = 1;
                left.Add(Ui.Text(name + (st.HasGear(g) ? "  ✓" : ""), "topiccard-title"));
                left.Add(Ui.Wrapping(effect, "body", "dim"));
                row.Add(left);
                if (!st.HasGear(g))
                {
                    var buy = Ui.Btn(Ui.Money(E.GearCost(g)), () => { E.BuyGear(g); _host.ShowWeek(); }, "btn-ghost");
                    buy.SetEnabled(E.CanBuy(g));
                    row.Add(buy);
                }
                panel.Add(row);
                panel.Add(Ui.Divider());
            }

            GearRow("XLR microphone", Gear.XlrMic, "Raises the quality ceiling.");
            GearRow("Acoustic panels", Gear.AcousticPanels, "Lifts the quality floor a little.");
            GearRow("Editing software", Gear.EditingSoftware, "Better edit, every episode.");

            var coRow = Ui.Row();
            var coLeft = Ui.Box();
            coLeft.style.flexGrow = 1;
            coLeft.Add(Ui.Text("Co-host" + (st.HasCoHost ? "  ✓" : ""), "topiccard-title"));
            coLeft.Add(Ui.Wrapping($"+{E.Config.CoHostPrepBonus} prep points a week and a little appeal. €{E.Config.CoHostMonthlyWage}/month.", "body", "dim"));
            coRow.Add(coLeft);
            if (!st.HasCoHost)
            {
                var hire = Ui.Btn("Hire", () => { E.HireCoHost(); _host.ShowWeek(); }, "btn-ghost");
                hire.SetEnabled(E.CanHireCoHost());
                coRow.Add(hire);
            }
            panel.Add(coRow);

            return panel;
        }

        // ------------------------------------------------------------------
        void RenderTopics()
        {
            _topicList.Clear();
            foreach (var topic in E.Offer)
            {
                var card = Ui.Box("topiccard");
                if (_picked == topic.Id) card.AddToClassList("selected");

                card.Add(Ui.Text(topic.Name, "topiccard-title"));
                card.Add(Ui.Wrapping(topic.Blurb, "body", "dim"));

                float ctxMult = ContextResolver.AppealMultiplier(topic.Response, E.CurrentWeek) * E.CurrentWeek.ImportanceAppealMult;
                float effAppeal = topic.BaseAppeal * ctxMult;

                var chips = Ui.Box("row-wrap");
                chips.Add(Ui.Chip($"appeal {effAppeal:0.00}"));
                chips.Add(Ui.Chip($"effort {topic.Effort}"));
                chips.Add(Ui.Chip($"swing ±{topic.Swing:0.00}"));
                if (topic.RepEarn != 0) chips.Add(Ui.Chip($"rep {(topic.RepEarn > 0 ? "+" : "")}{topic.RepEarn:0}"));
                if (topic.BuzzBonus != 0) chips.Add(Ui.Chip($"buzz +{topic.BuzzBonus}"));
                card.Add(chips);

                var id = topic.Id;
                card.RegisterCallback<ClickEvent>(_ => Pick(id));
                _topicList.Add(card);
            }
        }

        void Pick(TopicId id)
        {
            _picked = id;
            _plan.Topic = id;
            var topic = TopicCatalog.Get(id);
            // Sensible default allocation: meet the effort, spread the rest.
            int cap = E.State.PrepCapacity(E.Config);
            _plan.PrepTopic = Mathf.Min(topic.Effort, cap);
            int left = cap - _plan.PrepTopic;
            _plan.PrepResearch = Mathf.Clamp(left / 3, 0, 6);
            left -= _plan.PrepResearch;
            _plan.PrepAudio = Mathf.Clamp(left / 2, 0, 6);
            left -= _plan.PrepAudio;
            _plan.PrepPromo = Mathf.Clamp(left, 0, 6);

            RenderTopics();
            _prepBlock.style.display = DisplayStyle.Flex;
            SyncSliders();
            RefreshPreview();
        }

        VisualElement Slider(string name, System.Func<int> get, System.Action<int> set)
        {
            var row = Ui.Box("prep-row");
            row.Add(Ui.Text(name, "prep-name"));
            var s = new SliderInt(0, 10) { value = get() };
            s.AddToClassList("prep-slider");
            var val = Ui.Text(get().ToString(), "prep-value");
            s.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                val.text = e.newValue.ToString();
                RefreshPreview();
            });
            row.Add(s);
            row.Add(val);
            _prep.Add(new PrepControl { Slider = s, Value = val, Get = get });
            return row;
        }

        void SyncSliders()
        {
            foreach (var p in _prep)
            {
                p.Slider.SetValueWithoutNotify(p.Get());
                p.Value.text = p.Get().ToString();
            }
        }

        void RefreshPreview()
        {
            int cap = E.State.PrepCapacity(E.Config);
            int used = _plan.TotalPrep;
            _prepMeter.text = $"Prep points   {used} / {cap}";
            _prepMeter.EnableInClassList("over", used > cap);

            _previewBlock.Clear();
            bool ok = _picked.HasValue && used <= cap && used > 0;
            _publish.SetEnabled(ok);

            if (!_picked.HasValue) return;

            var p = E.Preview(_plan);
            var grid = Ui.Box("statstrip");
            grid.Add(Ui.Stat("Quality", p.Quality.ToString("0.00") + "  " + p.QualityLabel));
            grid.Add(Ui.Stat("Reach", Mathf.RoundToInt(p.Reach).ToString("N0")));
            grid.Add(Ui.Stat("Listeners", Ui.Signed(p.ListenerDeltaExpected),
                p.ListenerDeltaExpected >= 0 ? "good" : "bad"));
            grid.Add(Ui.Stat("Rep", Ui.Signed(p.ReputationDelta, "0.0")));
            grid.Add(Ui.Stat("Buzz", p.BuzzGained > 0 ? "+" + p.BuzzGained : "0"));
            grid.Add(Ui.Stat("Weekly cash", Ui.Money(p.MoneyDelta), p.MoneyDelta >= 0 ? "good" : "bad"));
            _previewBlock.Add(grid);

            _previewBlock.Add(Ui.Wrapping(
                $"On the roll, listeners land between {Ui.Signed(p.ListenerDeltaLow)} and {Ui.Signed(p.ListenerDeltaHigh)}.",
                "body", "dim"));

            if (used > cap)
                _previewBlock.Add(Ui.Wrapping("You've allocated more prep points than you have.", "body", "bad"));
        }

        void Redraw()
        {
            if (E.TryRedraw())
            {
                _picked = null;
                _plan.PrepTopic = _plan.PrepResearch = _plan.PrepAudio = _plan.PrepPromo = 0;
                _prepBlock.style.display = DisplayStyle.None;
                _publish.SetEnabled(false);
                RenderTopics();
            }
            _redraw.text = RedrawLabel();
            _redraw.SetEnabled(false);
        }

        string RedrawLabel()
            => E.State.Money >= E.Config.RedrawCost ? $"Redraw  ({Ui.Money(E.Config.RedrawCost)})" : "Redraw  (1 Buzz)";

        void Publish()
        {
            if (!_picked.HasValue) return;
            _host.Publish(_plan.Clone());
        }

        // ------------------------------------------------------------------
        static string SurpriseLabel(Surprise s)
        {
            switch (s)
            {
                case Surprise.Heroic: return "HEROIC";
                case Surprise.Good: return "GOOD";
                case Surprise.Poor: return "POOR";
                case Surprise.Disaster: return "DISASTER";
                default: return "AS EXPECTED";
            }
        }

        void PaintBadge(Label badge, Surprise s)
        {
            Color c;
            switch (s)
            {
                case Surprise.Heroic:
                case Surprise.Good: c = new Color(0.18f, 0.5f, 0.33f); break;
                case Surprise.Poor: c = new Color(0.55f, 0.4f, 0.2f); break;
                case Surprise.Disaster: c = new Color(0.55f, 0.24f, 0.23f); break;
                default: c = new Color(0.27f, 0.26f, 0.34f); break;
            }
            badge.style.backgroundColor = c;
        }
    }
}
