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
            _prep.Clear();

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
            strip.Add(Tip(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null),
                "Weekly overhead eats into this. Run three weeks below -€200 and the show is over."));
            strip.Add(Tip(Ui.Stat("Listeners", st.Listeners.ToString("N0")),
                "Your audience, and the score. A bigger audience reaches further next week — but growth slows as you near what the fanbase can support."));
            strip.Add(Tip(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString()),
                "0-100. Built by well-made, thoughtful episodes; spent by lazy takes and cheap drama. High reputation raises the ceiling on how big the show can get."));
            strip.Add(Tip(Ui.Stat("Buzz", st.Buzz.ToString()),
                "Earned when an episode over-performs. For now it just pays for topic redraws; it matters more later."));
            scroll.Add(strip);

            // --- how it works (open on the very first week) ---
            var help = new Foldout { text = "How a week works", value = st.GlobalWeek == 1 };
            help.AddToClassList("help-foldout");
            help.Add(Ui.Wrapping(
                "Every week your club plays. You see the result and how surprising it was, then you make one episode about it.\n\n" +
                "1. Pick a topic. Each has a \"draw\" (how many people it pulls in this week) and a \"prep needed\".\n" +
                "2. Split your prep points between the topic and three levers — research, audio, promo.\n" +
                "3. Release it. Listeners, reputation, buzz and money all move — and you won't know exactly how until it's out.\n\n" +
                "Spend what you earn on gear and a co-host. Keep money above water. Grow the audience.", "body"));
            scroll.Add(help);

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
            offerPanel.Add(Ui.Wrapping(
                "Pick your angle. \"Draw\" is how many people this topic pulls in given how the week has gone. " +
                "\"Prep needed\" is how many prep points it takes to do the topic justice.", "body", "dim"));
            _topicList = Ui.Box();
            offerPanel.Add(_topicList);
            RenderTopics();
            scroll.Add(offerPanel);

            // --- production ---
            _prepBlock = Ui.Box("panel");
            _prepBlock.style.display = DisplayStyle.None;
            _prepBlock.Add(Ui.Text("Production", "h2"));
            _prepBlock.Add(Ui.Wrapping(
                $"You get {st.PrepCapacity(E.Config)} prep points this week. Spend them across the four areas below. " +
                "Unspent points are wasted.", "body", "dim"));
            _prepMeter = Ui.Text("", "prep-meter");
            _prepBlock.Add(_prepMeter);
            _prepBlock.Add(Slider("Topic prep", "The homework for this episode. Hit the topic's \"prep needed\" to do it justice; go over for a small extra edge.",
                () => _plan.PrepTopic, v => _plan.PrepTopic = v));
            _prepBlock.Add(Slider("Research", "Fact-checking and prep depth. Makes the outcome less of a gamble — a shaky topic becomes a safer bet.",
                () => _plan.PrepResearch, v => _plan.PrepResearch = v));
            _prepBlock.Add(Slider("Audio", "Editing and sound. Lifts the episode and keeps listeners from drifting away.",
                () => _plan.PrepAudio, v => _plan.PrepAudio = v));
            _prepBlock.Add(Slider("Promo", "Pushing this one episode — clips, posts, plugs. A one-week bump in reach, nothing lasting.",
                () => _plan.PrepPromo, v => _plan.PrepPromo = v));
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

            // Restore selection if this is a re-render (e.g. after buying gear).
            if (_picked.HasValue)
            {
                _plan.Topic = _picked.Value;
                _prepBlock.style.display = DisplayStyle.Flex;
                SyncSliders();
                RefreshPreview();
            }

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
                if (!string.IsNullOrEmpty(ctx.Advice))
                    panel.Add(Ui.Wrapping(ctx.Advice, "body", "dim"));
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
            if (!string.IsNullOrEmpty(ctx.Advice))
                panel.Add(Ui.Wrapping(ctx.Advice, "body", "dim"));
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
                    var buy = Ui.Btn(Ui.Money(E.GearCost(g)), () => { E.BuyGear(g); _host.RerenderWeek(); }, "btn-ghost");
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
                var hire = Ui.Btn("Hire", () => { E.HireCoHost(); _host.RerenderWeek(); }, "btn-ghost");
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
                chips.Add(Ui.Chip("Draw this week: " + DrawLabel(effAppeal)));
                chips.Add(Ui.Chip($"Prep needed: {topic.Effort}"));
                chips.Add(Ui.Chip(RiskLabel(topic.Swing)));
                if (topic.RepEarn >= 2) chips.Add(Ui.Chip("Builds reputation"));
                else if (topic.RepEarn <= -1) chips.Add(Ui.Chip("Costs reputation"));
                if (topic.BuzzBonus >= 3) chips.Add(Ui.Chip("Good for buzz"));
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

        VisualElement Slider(string name, string help, System.Func<int> get, System.Action<int> set)
        {
            var wrap = Ui.Box();
            wrap.style.marginBottom = 8;

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
            wrap.Add(row);
            wrap.Add(Ui.Wrapping(help, "body", "dim"));

            _prep.Add(new PrepControl { Slider = s, Value = val, Get = get });
            return wrap;
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
            _prepMeter.text = $"Prep points spent   {used} / {cap}";
            _prepMeter.EnableInClassList("over", used > cap);

            _previewBlock.Clear();
            bool ok = _picked.HasValue && used <= cap && used > 0;
            _publish.SetEnabled(ok);

            if (!_picked.HasValue) return;
            var topic = TopicCatalog.Get(_picked.Value);

            // A read on your *choice* — not on the outcome. The result is still a surprise.
            string prepNote;
            if (_plan.PrepTopic <= 0) prepNote = "You've put no real prep into the topic itself — this will sound thin.";
            else if (_plan.PrepTopic < topic.Effort - 1) prepNote = "Well under-prepped on the topic.";
            else if (_plan.PrepTopic < topic.Effort) prepNote = "A touch under-prepped on the topic.";
            else if (_plan.PrepTopic == topic.Effort) prepNote = "Topic is properly prepped.";
            else prepNote = "You've gone deep on the topic.";
            _previewBlock.Add(Ui.Wrapping(prepNote, "body", "dim"));

            if (used > cap)
                _previewBlock.Add(Ui.Wrapping("You've allocated more prep points than you have this week.", "body", "bad"));
            else if (used < cap)
                _previewBlock.Add(Ui.Wrapping($"{cap - used} point(s) still unspent.", "body", "dim"));
        }

        static VisualElement Tip(VisualElement el, string tip)
        {
            el.tooltip = tip;
            return el;
        }

        static string DrawLabel(float effectiveAppeal)
        {
            if (effectiveAppeal < 0.75f) return "Low";
            if (effectiveAppeal < 1.15f) return "Moderate";
            if (effectiveAppeal < 1.80f) return "High";
            return "Huge";
        }

        static string RiskLabel(float swing)
        {
            if (swing < 0.16f) return "Predictable";
            if (swing < 0.35f) return "Some variance";
            return "A gamble";
        }

        void Redraw()
        {
            if (E.TryRedraw())
            {
                _picked = null;
                _plan.PrepTopic = _plan.PrepResearch = _plan.PrepAudio = _plan.PrepPromo = 0;
                _host.RerenderWeek();
            }
            else
            {
                _redraw.SetEnabled(false);
            }
        }

        string RedrawLabel()
            => E.State.Money >= E.Config.RedrawCost ? $"Redraw  ({Ui.Money(E.Config.RedrawCost)})" : "Redraw  (1 Buzz)";

        void Publish()
        {
            if (!_picked.HasValue) return;
            _host.Publish(_plan.Clone());
        }

        // --- headless capture hooks ---
        public void DebugPickFirst()
        {
            if (E.Offer.Count > 0) Pick(E.Offer[0].Id);
        }

        public void DebugPublish()
        {
            if (_picked.HasValue) _host.Publish(_plan.Clone());
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
