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
        Topic _picked;

        // tabs
        static readonly string[] TabNames = { "This Week", "The Club", "Business", "Logbook", "Help" };
        int _activeTab;
        readonly List<Button> _tabButtons = new List<Button>();
        ScrollView _scroll;
        VisualElement _tabContent;

        // this-week widgets (rebuilt whenever the This Week tab is shown)
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

            // An unresolved interrupt has to be dealt with on This Week before anything else.
            if (E.Events.Pending != null || E.Scoops.Pending != null || E.State.BuyoutPending) _activeTab = 0;

            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            screen.Add(col);

            // --- persistent chrome ---
            col.Add(BuildHeaderBar(ctx, st));
            col.Add(BuildResourceStrip(st));

            if (E.Events.Pending != null || E.Scoops.Pending != null || E.State.BuyoutPending)
            {
                var banner = Ui.Box("toast", "toast-bad");
                banner.Add(Ui.Wrapping(
                    "There's a decision waiting on This Week — sort it before you can release.", "body"));
                col.Add(banner);
            }

            col.Add(BuildTabBar());

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("scroll");
            _tabContent = Ui.Box();
            _scroll.Add(_tabContent);
            col.Add(_scroll);

            RenderActiveTab();
            return screen;
        }

        // ------------------------------------------------------------------
        VisualElement BuildHeaderBar(WeekContext ctx, GameState st)
        {
            var bar = Ui.Box("panel", "panel-tight");
            _host.Theme.PaintBar(bar);
            var ink = _host.Theme.Ink(_host.Theme.Primary);

            var t1 = Ui.Text(st.PodcastName, "h2");
            t1.style.color = ink;
            t1.style.marginTop = 0;
            t1.style.marginBottom = 0;
            var t2 = Ui.Text(
                $"Season {st.Season} · Week {ctx.Turn} · {st.ClubName} sit {ctx.LeaguePositionLabel}", "body");
            t2.style.color = ink;
            bar.Add(t1);
            bar.Add(t2);
            return bar;
        }

        VisualElement BuildResourceStrip(GameState st)
        {
            var strip = Ui.Box("statstrip");
            strip.Add(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null,
                "Cash. Overhead bleeds it every week."));
            strip.Add(Ui.Stat("Listeners", st.Listeners.ToString("N0"), null,
                "Your audience — and your score."));
            strip.Add(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString(), null,
                "How respected the show is. Raises your growth ceiling."));
            strip.Add(Ui.Stat("Buzz", st.Buzz.ToString(), null,
                "Hype from breakout episodes. Spends on redraws."));
            return strip;
        }

        VisualElement BuildTabBar()
        {
            _tabButtons.Clear();
            var bar = Ui.Box("tabbar");
            for (int i = 0; i < TabNames.Length; i++)
            {
                int idx = i;
                var b = Ui.Btn(TabNames[i], () => SwitchTab(idx), "tab");
                _tabButtons.Add(b);
                bar.Add(b);
            }
            PaintTabs();
            return bar;
        }

        void PaintTabs()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool active = i == _activeTab;
                _tabButtons[i].EnableInClassList("active", active);
                _tabButtons[i].style.borderBottomColor = active
                    ? new StyleColor(_host.Theme.Secondary)
                    : new StyleColor(new Color(0, 0, 0, 0));
            }
        }

        void SwitchTab(int i)
        {
            if (_activeTab == i && _tabContent.childCount > 0) return;
            _activeTab = i;
            PaintTabs();
            RenderActiveTab();
            if (_scroll != null) _scroll.scrollOffset = Vector2.zero;
        }

        void RenderActiveTab()
        {
            _tabContent.Clear();
            _prep.Clear();
            var ctx = E.CurrentWeek;
            switch (_activeTab)
            {
                case 0: BuildThisWeekTab(_tabContent, ctx); break;
                case 1: BuildClubTab(_tabContent, ctx); break;
                case 2: BuildBusinessTab(_tabContent, ctx); break;
                case 3: BuildLogbookTab(_tabContent); break;
                default: BuildHelpTab(_tabContent); break;
            }
        }

        // ================================================================
        // TAB 0 — This Week
        // ================================================================
        void BuildThisWeekTab(VisualElement root, WeekContext ctx)
        {
            var st = E.State;

            // story headlines from the start of this week
            foreach (var te in ctx.ThreadEvents)
            {
                var toast = Ui.Box("toast");
                if (te.IsResolution) toast.AddToClassList("toast-story");
                toast.Add(Ui.Text(te.Headline.ToUpperInvariant(), "eyebrow"));
                toast.Add(Ui.Wrapping(te.Body, "body"));
                root.Add(toast);
            }

            if (!string.IsNullOrEmpty(ctx.CompetitionNote))
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Text("CUP & EUROPE", "eyebrow"));
                toast.Add(Ui.Wrapping(ctx.CompetitionNote, "body"));
                root.Add(toast);
            }
            foreach (var rn in ctx.RivalNews)
            {
                var toast = Ui.Box("toast", "toast-story");
                toast.Add(Ui.Text("RIVAL WATCH", "eyebrow"));
                toast.Add(Ui.Wrapping(rn, "body"));
                root.Add(toast);
            }
            if (!string.IsNullOrEmpty(ctx.AccessNote))
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Text("CLUB ACCESS", "eyebrow"));
                toast.Add(Ui.Wrapping(ctx.AccessNote, "body"));
                root.Add(toast);
            }
            if (ctx.CardsGained.Count > 0)
            {
                var toast = Ui.Box("toast", "toast-story");
                toast.Add(Ui.Text("NEW CARDS", "eyebrow"));
                toast.Add(Ui.Wrapping("Added to your hand: " + string.Join(", ", ctx.CardsGained) + ". Play them from Business.", "body"));
                root.Add(toast);
            }

            // a scoop awaiting a decision — blocks the week like an event
            if (E.Scoops.Pending != null)
            {
                var sc = E.Scoops.Pending;
                var card = Ui.Box("panel", "event-card");
                card.Add(Ui.Text("YOU'VE GOT A SCOOP", "eyebrow"));
                card.Add(Ui.Text(sc.Headline, "h2"));
                card.Add(Ui.Wrapping(sc.Detail, "body"));
                card.Add(Ui.Wrapping("Break it now for big numbers and a real risk it's wrong; verify and hold to build trust; or trade it to a national outlet for cash.", "body", "dim"));
                void Choice(string label, ScoopChoice c)
                {
                    var b = Ui.Btn(label, () => { E.ResolveScoop(c); _host.RerenderWeek(); }, "btn-ghost");
                    b.style.marginTop = 4;
                    card.Add(b);
                }
                Choice("Break it now", ScoopChoice.BreakNow);
                Choice("Verify & hold", ScoopChoice.VerifyHold);
                Choice("Trade it", ScoopChoice.Trade);
                root.Add(card);
            }
            else if (!string.IsNullOrEmpty(E.Scoops.LastOutcome))
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping(E.Scoops.LastOutcome, "body"));
                root.Add(toast);
            }

            // the 1M buyout decision
            if (st.BuyoutPending)
            {
                var card = Ui.Box("panel", "event-card");
                card.Add(Ui.Text("SOMEONE WANTS TO BUY THE SHOW", "eyebrow"));
                card.Add(Ui.Text("A media group has made an offer for the whole podcast.", "h2"));
                card.Add(Ui.Wrapping("Take the money and keep making it under their banner, or stay independent. Either way the show goes on.", "body", "dim"));
                var yes = Ui.Btn("Sell — take the €250,000", () => { E.AcceptBuyout(); _host.RerenderWeek(); }, "btn-ghost");
                var no = Ui.Btn("Stay independent", () => { E.DeclineBuyout(); _host.RerenderWeek(); }, "btn-ghost");
                yes.style.marginTop = 4; no.style.marginTop = 4;
                card.Add(yes); card.Add(no);
                root.Add(card);
            }

            // an interrupt event, if one fired
            if (E.Events.Pending != null)
            {
                var ev = E.Events.Pending;
                var card = Ui.Box("panel", "event-card");
                card.Add(Ui.Text("SOMETHING'S COME UP", "eyebrow"));
                card.Add(Ui.Text(ev.Prompt, "h2"));
                card.Add(Ui.Wrapping(ev.Detail, "body"));
                for (int i = 0; i < ev.Options.Count; i++)
                {
                    int idx = i;
                    var b = Ui.Btn(ev.Options[i].Label, () => { E.ResolveEvent(idx); _host.RerenderWeek(); }, "btn-ghost");
                    b.style.marginTop = 4;
                    card.Add(b);
                }
                root.Add(card);
            }
            else if (!string.IsNullOrEmpty(E.Events.LastOutcome))
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping(E.Events.LastOutcome, "body"));
                root.Add(toast);
            }

            if (ctx.SponsorNews != null)
            {
                var toast = Ui.Box("toast");
                if (!ctx.SponsorNews.Good) toast.AddToClassList("toast-bad");
                toast.Add(Ui.Text(ctx.SponsorNews.Headline.ToUpperInvariant(), "eyebrow"));
                toast.Add(Ui.Wrapping(ctx.SponsorNews.Body, "body"));
                root.Add(toast);
            }

            root.Add(BuildMatchPanel(ctx));

            // running-story reminder (the full panel lives on The Club)
            if (ctx.ActiveThreads != null && ctx.ActiveThreads.Count > 0)
            {
                var note = Ui.Box("toast", "toast-story");
                note.Add(Ui.Text("RUNNING STORIES", "eyebrow"));
                foreach (var t in ctx.ActiveThreads)
                    note.Add(Ui.Wrapping("• " + t.Label + " — " + LeanLabel(t.Momentum), "body"));
                note.Add(Ui.Wrapping("Pick the STORY topic below to weigh in. Full detail on The Club.", "body", "dim"));
                root.Add(note);
            }

            // topic offer
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
            root.Add(offerPanel);

            // cards — a step in the weekly loop: play up to a couple to shape this episode
            root.Add(BuildWeeklyCardsPanel());

            // production
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
            root.Add(_prepBlock);

            _publish = Ui.Btn("Record & release", Publish, "btn-primary");
            _host.Theme.PaintPrimaryButton(_publish);
            _publish.style.marginTop = 6;
            _publish.SetEnabled(false);
            root.Add(_publish);

            // restore selection on a re-render (e.g. after buying gear on Business)
            if (_picked != null)
            {
                _prepBlock.style.display = DisplayStyle.Flex;
                SyncSliders();
                RefreshPreview();
            }
        }

        // ================================================================
        // TAB 1 — The Club
        // ================================================================
        void BuildClubTab(VisualElement root, WeekContext ctx)
        {
            root.Add(BuildCompetitionsPanel());
            root.Add(BuildRivalsPanel());

            var threads = BuildThreadsPanel(ctx);
            if (threads != null) root.Add(threads);
            else
            {
                var quiet = Ui.Box("panel");
                quiet.Add(Ui.Text("Running stories", "h2"));
                quiet.Add(Ui.Wrapping("Nothing brewing around the club right now.", "body", "dim"));
                root.Add(quiet);
            }
            root.Add(BuildSquadPanel());
            root.Add(BuildTablePanel());
        }

        VisualElement BuildCompetitionsPanel()
        {
            var cal = E.Calendar;
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Cup & Europe", "h2"));

            string cup = cal.WonCup ? "Cup: WON. 🏆"
                : !cal.CupAlive && cal.CupRoundReached > 0 ? $"Cup: knocked out after {cal.CupRoundReached} round(s)."
                : !cal.CupAlive ? "Cup: out."
                : cal.CupRoundReached == 0 ? "Cup: first round to come."
                : $"Cup: through {cal.CupRoundReached} round(s) and counting.";
            panel.Add(Ui.Wrapping(cup, "body", cal.WonCup ? "good" : "dim"));

            string eur = !cal.InEurope ? "Europe: not qualified this season."
                : cal.EuropePhaseDone ? $"Europe: phase done — {cal.EuropeWins}W {cal.EuropeDraws}D {cal.EuropeLosses}L." + (cal.WonEurope ? " Topped the group." : "")
                : $"Europe: {cal.EuropeWins}W {cal.EuropeDraws}D {cal.EuropeLosses}L so far.";
            panel.Add(Ui.Wrapping(eur, "body", cal.WonEurope ? "good" : "dim"));

            if (E.State.CupsWon > 0 || E.State.EuropeanTrophies > 0)
                panel.Add(Ui.Wrapping($"Trophy cabinet: {E.State.CupsWon} cup(s), {E.State.EuropeanTrophies} European.", "body", "dim"));
            return panel;
        }

        VisualElement BuildRivalsPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Rivals", "h2"));
            if (E.Rivals.Rivals.Count == 0)
            {
                panel.Add(Ui.Wrapping("No standout rival this season.", "body", "dim"));
                return panel;
            }
            foreach (var r in E.Rivals.Rivals)
            {
                int pos = E.Calendar.PositionOf(r.ClubIndex);
                int mine = E.Calendar.PlayerPosition();
                string where = pos < mine ? "above you" : pos > mine ? "below you" : "level with you";
                var row = Ui.Box();
                row.style.marginBottom = 4;
                row.Add(Ui.Text(r.Name, "topiccard-title"));
                row.Add(Ui.Wrapping($"Your rival for {r.Arc}. Currently {SeasonCalendar.Ordinal(pos)} — {where}.", "body", "dim"));
                panel.Add(row);
            }
            panel.Add(Ui.Wrapping("Matches against a rival are always a derby. When they slip up or surge, you'll get a topic for it.", "body", "dim"));
            return panel;
        }

        // ================================================================
        // TAB 2 — Business
        // ================================================================
        void BuildBusinessTab(VisualElement root, WeekContext ctx)
        {
            if (E.Access.PartnershipOffered)
            {
                var card = Ui.Box("panel", "event-card");
                card.Add(Ui.Text("PARTNERSHIP OFFER", "eyebrow"));
                card.Add(Ui.Text("The club wants to make it official", "h2"));
                card.Add(Ui.Wrapping("Official access and a 35% cut of your ad revenue — but you can't go heavily negative on the club without risking it.", "body", "dim"));
                var yes = Ui.Btn("Accept the partnership", () => { E.AcceptPartnership(); _host.RerenderWeek(); }, "btn-ghost");
                var no = Ui.Btn("Stay independent", () => { E.DeclinePartnership(); _host.RerenderWeek(); }, "btn-ghost");
                yes.style.marginTop = 4; no.style.marginTop = 4;
                card.Add(yes); card.Add(no);
                root.Add(card);
            }

            root.Add(BuildAccessPanel());
            root.Add(BuildSponsorPanel(ctx));
            root.Add(BuildCardsPanel());
            root.Add(BuildStudioPanel());
        }

        VisualElement BuildAccessPanel()
        {
            var st = E.State;
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Club access", "h2"));
            panel.Add(Ui.Wrapping($"Tier {st.AccessTier} — {AccessManager.TierLabel(st.AccessTier)}.", "body",
                st.AccessTier >= 2 ? "good" : null));
            string perk = st.AccessTier switch
            {
                0 => "You react to public news like everyone else. Grow the rolling audience to get closer to the club.",
                1 => "Post-match quotes and pressers. Small \"we asked the club\" angles.",
                2 => "Scoops reach you before the news breaks. The Big interview is open to you. The club is watching your tone.",
                _ => "Your takes move fan sentiment and board pressure. A partnership may be offered."
            };
            panel.Add(Ui.Wrapping(perk, "body", "dim"));
            if (st.AccessProtectedWeeks > 0)
                panel.Add(Ui.Wrapping($"Access suspended for {st.AccessProtectedWeeks} more week(s) after a burned scoop.", "body", "bad"));
            if (st.HasPartnership)
                panel.Add(Ui.Wrapping("Partnered with the club — a cut of ad revenue, and crisis takes cost you a little extra reputation.", "body", "dim"));
            if (st.TrustedStanding > 0)
                panel.Add(Ui.Wrapping($"Trusted standing: {st.TrustedStanding} — better scoops, lower chance of getting one wrong.", "body", "dim"));
            return panel;
        }

        // Shown on This Week as a step in the loop.
        VisualElement BuildWeeklyCardsPanel()
        {
            var st = E.State;
            int left = Mathf.Max(0, E.Config.CardPlaysPerWeek - st.CardsPlayedThisWeek);

            var panel = Ui.Box("panel");
            var head = Ui.Row();
            head.Add(Ui.Text("Cards", "h2"));
            if (E.Cards.CanBuyPack(st))
            {
                var buy = Ui.Btn($"Buy pack  ({E.Config.PackCostBuzz} Buzz)", () => { E.BuyPack(); _host.RerenderWeek(); }, "btn-ghost");
                head.Add(buy);
            }
            panel.Add(head);

            if (st.Hand.Count == 0)
            {
                panel.Add(Ui.Wrapping(
                    "No cards right now. You get one at every listener milestone, or buy a 3-card pack with Buzz.", "body", "dim"));
                return panel;
            }

            panel.Add(Ui.Wrapping(
                left > 0
                    ? $"Play up to {left} this week to shape the episode you're about to record. One-shots affect this week only; permanent cards stick."
                    : "You've played your cards for this week.", "body", "dim"));

            foreach (var id in st.Hand.ToArray())
            {
                var card = CardManager.Get(id);
                if (card == null) continue;
                string cid = id;
                var box = Ui.Box("topiccard");
                box.Add(Ui.Text(card.Name + (card.Kind == CardKind.Permanent ? "   · permanent" : ""), "topiccard-title"));
                box.Add(Ui.Wrapping(card.Text, "body", "dim"));
                var play = Ui.Btn("Play", () => { E.PlayCard(cid); _host.RerenderWeek(); }, "btn-ghost");
                play.style.marginTop = 4;
                play.SetEnabled(left > 0 && E.Events.Pending == null && E.Scoops.Pending == null);
                box.Add(play);
                panel.Add(box);
            }
            return panel;
        }

        // Business-tab view: the pack shop plus a read-only look at the hand.
        VisualElement BuildCardsPanel()
        {
            var st = E.State;
            var panel = Ui.Box("panel");
            var head = Ui.Row();
            head.Add(Ui.Text("Cards & packs", "h2"));
            var buy = Ui.Btn($"Buy pack  ({E.Config.PackCostBuzz} Buzz)", () => { E.BuyPack(); _host.RerenderWeek(); }, "btn-ghost");
            buy.SetEnabled(E.Cards.CanBuyPack(st));
            head.Add(buy);
            panel.Add(head);

            panel.Add(Ui.Wrapping(
                $"Hand: {st.Hand.Count}/{E.Config.CardHandLimit}. You play cards on This Week, up to {E.Config.CardPlaysPerWeek} a week. " +
                "Cards come from milestones or a 3-card pack for Buzz.", "body", "dim"));

            if (st.Hand.Count == 0)
            {
                panel.Add(Ui.Wrapping("No cards in hand.", "body", "dim"));
                return panel;
            }
            foreach (var id in st.Hand.ToArray())
            {
                var card = CardManager.Get(id);
                if (card == null) continue;
                string cid = id;
                var box = Ui.Box("topiccard");
                box.Add(Ui.Text(card.Name + (card.Kind == CardKind.Permanent ? "   · permanent" : ""), "topiccard-title"));
                box.Add(Ui.Wrapping(card.Text, "body", "dim"));
                var play = Ui.Btn("Play now", () => { E.PlayCard(cid); _host.RerenderWeek(); }, "btn-ghost");
                play.style.marginTop = 4;
                play.SetEnabled(E.Cards.CanPlay(st) && E.Events.Pending == null && E.Scoops.Pending == null);
                box.Add(play);
                panel.Add(box);
            }
            return panel;
        }

        // ================================================================
        // TAB 3 — Logbook
        // ================================================================
        void BuildLogbookTab(VisualElement root)
        {
            var st = E.State;
            if (st.LastSeason != null) root.Add(BuildSeasonReviewPanel(st.LastSeason));

            var prog = Ui.Box("panel");
            prog.Add(Ui.Text("Progress", "h2"));
            int avg = st.AverageListeners(E.Config.AvgListenerWindow);
            prog.Add(Ui.Wrapping($"Average listeners (last {E.Config.AvgListenerWindow} weeks): {avg:N0}", "body"));
            prog.Add(Ui.Wrapping(
                $"Peak: {st.PeakListeners:N0}   ·   Episodes released: {st.EpisodesPublished}", "body", "dim"));

            if (!st.GoalReached)
            {
                int next = st.NextMilestoneIndex < E.Config.Milestones.Length
                    ? E.Config.Milestones[st.NextMilestoneIndex]
                    : E.Config.GoalListeners;
                prog.Add(Ui.Divider());
                prog.Add(Ui.Wrapping($"Next milestone: {next:N0} listeners.", "body", "dim"));
                prog.Add(Ui.Wrapping($"Goal: {E.Config.GoalListeners:N0} average listeners, then endless milestones.", "body", "dim"));
            }
            else
            {
                prog.Add(Ui.Divider());
                prog.Add(Ui.Wrapping("Goal reached — you're chasing milestones now, for as long as you like.", "body", "good"));
            }
            if (st.CupsWon > 0 || st.EuropeanTrophies > 0 || st.BestLeagueFinish < 20)
            {
                var trophies = Ui.Box("panel");
                trophies.Add(Ui.Text("Honours", "h2"));
                trophies.Add(Ui.Wrapping($"Best league finish: {SeasonCalendar.Ordinal(st.BestLeagueFinish)}.", "body", "dim"));
                if (st.CupsWon > 0) trophies.Add(Ui.Wrapping($"Cups won: {st.CupsWon}.", "body", "good"));
                if (st.EuropeanTrophies > 0) trophies.Add(Ui.Wrapping($"European trophies: {st.EuropeanTrophies}.", "body", "good"));
                root.Add(trophies);
            }

            root.Add(prog);
            root.Add(BuildEpisodeLog());
        }

        VisualElement BuildSeasonReviewPanel(SeasonSummary s)
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text($"Season {s.Season} review", "h2"));
            panel.Add(Ui.Wrapping($"Finished {s.LeaguePositionLabel}" +
                (s.WonCup ? ", won the cup" : "") + (s.WonEurope ? ", topped the European group" : "") + ".", "body"));
            var grid = Ui.Box("statstrip");
            grid.Add(Ui.Stat("Listeners", $"{s.ListenersStart:N0} → {s.ListenersEnd:N0}"));
            grid.Add(Ui.Stat("Reputation", $"{Mathf.RoundToInt(s.ReputationStart)} → {Mathf.RoundToInt(s.ReputationEnd)}"));
            grid.Add(Ui.Stat("Episodes", s.EpisodesThisSeason.ToString()));
            panel.Add(grid);
            if (!string.IsNullOrEmpty(s.BestEpisodeTopic))
                panel.Add(Ui.Wrapping($"Best episode: \"{s.BestEpisodeTopic}\" ({Ui.Signed(s.BestEpisodeListenerGain)} listeners).", "body", "dim"));
            foreach (var h in s.Headlines)
                panel.Add(Ui.Wrapping("• " + h, "body", "dim"));
            return panel;
        }

        // ================================================================
        // TAB 4 — Help
        // ================================================================
        void BuildHelpTab(VisualElement root)
        {
            var week = Ui.Box("panel");
            week.Add(Ui.Text("THE WEEK", "eyebrow"));
            week.Add(Ui.Wrapping(
                "Every week your club plays. You see the result and how surprising it was, then you make one episode about it.\n" +
                "1. Deal with anything waiting — an interrupt event, a scoop, a big decision.\n" +
                "2. Pick a topic. Each has a \"draw\" (how many people it pulls in this week) and a \"prep needed\".\n" +
                "3. Optionally play a card or two to shape the episode.\n" +
                "4. Split your prep points between the topic and three levers — research, audio, promo.\n" +
                "5. Release it. The result is a surprise until it's out.\n" +
                "Spend what you earn on gear, crew and a co-host. Keep money above water. Grow the audience.", "body"));
            root.Add(week);

            var numbers = Ui.Box("panel");
            numbers.Add(Ui.Text("THE NUMBERS", "eyebrow"));
            numbers.Add(Ui.Wrapping(
                "Listeners — your audience and your score. A bigger audience means every episode reaches further, " +
                "so growth compounds — but it slows down as you approach the size the fanbase can realistically support.\n\n" +
                "Reputation (0–100) — how seriously the show is taken. Thoughtful, well-made episodes and measured takes build it; " +
                "lazy episodes and cheap drama burn it. It matters because a well-regarded show can grow much bigger — high reputation " +
                "lifts the ceiling on your audience. Some topics also need a minimum reputation before you can cover them.\n\n" +
                "Money — ad income barely covers the weekly overhead on its own, so the budget is tight until the audience is large. " +
                "Three straight weeks more than €200 in the red and the show folds.\n\n" +
                "Buzz — earned when an episode punches above its weight. Right now it only pays to redraw the topic offer; " +
                "it unlocks more later.", "body"));
            root.Add(numbers);

            var gloss = Ui.Box("panel");
            gloss.Add(Ui.Text("GLOSSARY", "eyebrow"));
            void Term(string t, string d)
            {
                gloss.Add(Ui.Text(t, "topiccard-title"));
                gloss.Add(Ui.Wrapping(d, "body", "dim"));
                gloss.Add(Ui.Divider());
            }
            Term("Draw", "How many listeners a topic pulls in this week, before quality. Shifts with the result, the fixture and story context.");
            Term("Prep needed", "The prep points it takes to cover a topic properly. Under it and the episode sounds thin; a point or two over gives a small edge.");
            Term("Risk", "How much the outcome can swing. Research prep narrows it — a gamble becomes a safer bet.");
            Term("Surprise", "How far the match result landed from what was expected. Drives the mood you're reacting to and which topics land.");
            Term("Topic prep / Research / Audio / Promo", "Topic prep = the homework. Research = less variance. Audio = a better episode and less churn. Promo = a one-week reach bump only.");
            Term("Running stories", "Ongoing club storylines. Pick the STORY topic to take a side; match the eventual outcome and you gain reputation and buzz, call it wrong and it costs you.");
            Term("Sponsors", "A deal pays weekly but sets a growth target and a deadline. Hit it for a bonus and a better renewal; miss it and the deal ends badly.");
            Term("Cup & Europe", "Extra midweek fixtures alongside the league. Win the cup or finish high enough and you play in Europe next season — bigger nights, more reach and buzz.");
            Term("Access tier", "Set by your rolling-average listeners. Tier 2 unlocks scoops and the Big interview; tier 3 lets your takes move the club, and may bring a partnership offer.");
            Term("Scoops", "Advance word on a club decision. Break it now for a huge episode and a real risk it's wrong (which costs access); verify & hold to build trust; or trade it for cash.");
            Term("Crew", "Producer, Researcher, Clips manager, Booker — monthly wages, each changing how the week works. Hire them once ad and sponsor money can carry the bill.");
            Term("Cards", "One-shot buffs on the current episode, or permanent lifts to the club/show. From milestones, or a 3-card pack for Buzz. Hand holds five.");
            Term("Custom run", "Modifiers set at the start (extra prep, gentler churn, sandbox, chaos…). Flags the run as Custom; the endless chase still works.");
            root.Add(gloss);
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
                AppendSquadNews(panel, ctx);
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
            string compChip = fx.Competition == Competition.Cup ? E.Calendar.RoundName(fx)
                : fx.Competition == Competition.European ? E.Calendar.RoundName(fx)
                : fx.Importance == FixtureImportance.Derby ? (fx.IsRivalFixture ? "Rival" : "Derby")
                : fx.Importance == FixtureImportance.BigMatch ? "Big match"
                : fx.Importance == FixtureImportance.Final ? "Final" : null;
            if (!string.IsNullOrEmpty(compChip)) meta.Add(Ui.Text(compChip, "chip"));
            panel.Add(meta);

            panel.Add(Ui.Wrapping(ctx.Headline, "body"));
            if (!string.IsNullOrEmpty(ctx.Advice))
                panel.Add(Ui.Wrapping(ctx.Advice, "body", "dim"));

            if (ctx.Moments != null && ctx.Moments.Count > 0)
            {
                panel.Add(Ui.Divider());
                panel.Add(Ui.Text("MOMENTS", "eyebrow"));
                foreach (var mo in ctx.Moments)
                    panel.Add(Ui.Wrapping("• " + mo, "body"));
            }

            if (ctx.KeyPlayersOut > 0)
                panel.Add(Ui.Wrapping(
                    $"{ctx.KeyPlayersOut} key player{(ctx.KeyPlayersOut == 1 ? "" : "s")} missing — the team was weaker for this one.",
                    "body", "dim"));

            AppendSquadNews(panel, ctx);
            return panel;
        }

        VisualElement BuildThreadsPanel(WeekContext ctx)
        {
            if (ctx.ActiveThreads == null || ctx.ActiveThreads.Count == 0) return null;

            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Running stories", "h2"));
            panel.Add(Ui.Wrapping(
                "Ongoing storylines around the club. Cover one on this week's episode to steer where it goes — " +
                "ignore it and it drifts with the results.", "body", "dim"));

            foreach (var t in ctx.ActiveThreads)
            {
                var row = Ui.Box();
                row.style.marginTop = 8;
                row.Add(Ui.Text(t.Label, "topiccard-title"));
                if (!string.IsNullOrEmpty(t.Note))
                    row.Add(Ui.Wrapping(t.Note, "body", "dim"));
                row.Add(Ui.Wrapping("Which way it's leaning: " + LeanLabel(t.Momentum), "body",
                    t.Momentum >= 0 ? "good" : "bad"));

                int call = System.Math.Sign(t.StanceScore);
                if (call != 0 && !string.IsNullOrEmpty(t.StancePlusLabel))
                    row.Add(Ui.Wrapping("You've been calling it: “" +
                        (call > 0 ? t.StancePlusLabel : t.StanceMinusLabel) + "”", "body", "dim"));
                panel.Add(row);
            }
            return panel;
        }

        static string LeanLabel(float momentum)
        {
            if (momentum > 0.35f) return "towards a happy ending";
            if (momentum > 0.1f) return "cautiously positive";
            if (momentum > -0.1f) return "genuinely up in the air";
            if (momentum > -0.35f) return "not looking good";
            return "heading for the worst outcome";
        }

        void AppendSquadNews(VisualElement panel, WeekContext ctx)
        {
            if (ctx.SquadNews == null || ctx.SquadNews.Count == 0) return;
            panel.Add(Ui.Divider());
            panel.Add(Ui.Text("SQUAD NEWS", "eyebrow"));
            foreach (var line in ctx.SquadNews)
                panel.Add(Ui.Wrapping("• " + line, "body"));
        }

        VisualElement BuildSquadPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("The squad", "h2"));
            foreach (var p in E.Roster.Players)
            {
                var row = Ui.Row();
                row.style.marginBottom = 3;
                var left = Ui.Text($"{p.Name}  ({p.Position})" + (p.IsKey ? "  ★" : ""), "body");
                left.style.flexGrow = 1;
                var status = Ui.Text(p.StatusLine, "body", p.IsFit ? "dim" : "bad");
                row.Add(left);
                row.Add(status);
                panel.Add(row);
            }
            panel.Add(Ui.Wrapping("★ marks the players whose absence actually weakens the team.", "body", "dim"));
            return panel;
        }

        VisualElement BuildTablePanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("League table & fixtures", "h2"));

            var standings = E.Calendar.Standings();
            int i = 1;
            foreach (var c in standings)
            {
                var row = Ui.Row();
                row.style.marginBottom = 2;
                var name = Ui.Text($"{i,2}. {c.Name}", "body", c.IsPlayer ? null : "dim");
                if (c.IsPlayer) name.style.color = _host.Theme.Secondary;
                name.style.flexGrow = 1;
                var pts = Ui.Text($"P{c.Played}  {c.Points}pts  ({(c.GoalDifference >= 0 ? "+" : "")}{c.GoalDifference})", "body", "dim");
                row.Add(name);
                row.Add(pts);
                panel.Add(row);
                i++;
            }

            panel.Add(Ui.Divider());
            panel.Add(Ui.Text("NEXT UP", "eyebrow"));
            foreach (var fx in E.Calendar.UpcomingFixtures(E.State.SeasonTurn + 1, 5))
            {
                string line = fx.IsInternationalBreak ? "International break"
                    : fx.IsOffseason ? "Transfer window"
                    : fx.IsCupByeWeek ? "Cup weekend (not involved)"
                    : fx.Competition == Competition.Cup ? $"CUP  {fx.Opponent}"
                    : fx.Competition == Competition.European ? $"EUROPE  {(fx.Home ? "H" : "A")} {fx.Opponent}"
                    : $"{(fx.Home ? "H" : "A")}  {fx.Opponent}"
                      + (fx.IsRivalFixture ? "  · rival" : fx.Importance == FixtureImportance.Derby ? "  · derby"
                         : fx.Importance == FixtureImportance.BigMatch ? "  · big match" : "");
                panel.Add(Ui.Text(line, "body", "dim"));
            }
            return panel;
        }

        VisualElement BuildEpisodeLog()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Recent episodes", "h2"));

            var eps = E.State.Episodes;
            if (eps.Count == 0)
            {
                panel.Add(Ui.Wrapping("Nothing published yet.", "body", "dim"));
                return panel;
            }

            int from = Mathf.Max(0, eps.Count - 16);
            for (int i = eps.Count - 1; i >= from; i--)
            {
                var e = eps[i];
                var row = Ui.Row();
                row.style.marginBottom = 2;
                var l = Ui.Text($"Wk {e.GlobalWeek}: {e.TopicName}", "body");
                l.style.flexGrow = 1;
                var r = Ui.Text($"{e.QualityLabel} · {Ui.Signed(e.ListenerDelta)} listeners", "body",
                    e.ListenerDelta >= 0 ? "dim" : "bad");
                row.Add(l);
                row.Add(r);
                panel.Add(row);
            }
            return panel;
        }

        VisualElement BuildSponsorPanel(WeekContext ctx)
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Sponsors", "h2"));

            foreach (var active in E.Sponsors.ActiveDeals)
            {
                var o = active.Offer;
                var deal = Ui.Box();
                deal.style.marginBottom = 6;
                deal.Add(Ui.Text(o.Name, "topiccard-title"));
                deal.Add(Ui.Wrapping($"€{o.Weekly:N0} a week. Target: {o.Target.Describe()}.", "body"));
                int have = o.Target.Metric == "reputation"
                    ? Mathf.RoundToInt(E.State.Reputation)
                    : E.State.AverageListeners(E.Config.AvgListenerWindow);
                deal.Add(Ui.Wrapping(
                    active.TargetMet
                        ? $"Target hit — the bonus lands when the term ends in {active.WeeksLeft} week(s)."
                        : $"Currently at {have:N0}. {active.WeeksLeft} week(s) left to reach {o.Target.Value:N0}.",
                    "body", active.TargetMet ? "good" : "dim"));
                panel.Add(deal);
            }

            bool slotFree = E.Sponsors.ActiveDeals.Count < E.Sponsors.MaxSlots;
            if (slotFree && ctx.SponsorInbox != null && ctx.SponsorInbox.Count > 0)
            {
                panel.Add(Ui.Wrapping(
                    E.Sponsors.MaxSlots > 1
                        ? $"Offers on the table. You can hold {E.Sponsors.MaxSlots} deals at once."
                        : "Offers on the table. You can hold one deal at a time.", "body", "dim"));
                for (int i = 0; i < ctx.SponsorInbox.Count; i++)
                {
                    var o = ctx.SponsorInbox[i];
                    var card = Ui.Box("topiccard");
                    card.Add(Ui.Text(o.Name, "topiccard-title"));
                    card.Add(Ui.Wrapping(o.Blurb, "body", "dim"));
                    var chips = Ui.Box("row-wrap");
                    chips.Add(Ui.Chip($"€{o.Weekly:N0}/wk"));
                    if (o.SigningBonus > 0) chips.Add(Ui.Chip($"€{o.SigningBonus:N0} to sign"));
                    chips.Add(Ui.Chip($"Bonus €{o.HitBonus:N0} if hit"));
                    card.Add(chips);
                    card.Add(Ui.Wrapping($"Target: {o.Target.Describe()}.", "body"));
                    if (!string.IsNullOrEmpty(o.Demand))
                        card.Add(Ui.Wrapping("Catch: " + o.Demand, "body", "bad"));
                    int idx = i;
                    var sign = Ui.Btn("Sign", () => { E.SignSponsor(idx); _host.RerenderWeek(); }, "btn-ghost");
                    sign.style.marginTop = 4;
                    card.Add(sign);
                    panel.Add(card);
                }
                return panel;
            }

            if (E.Sponsors.ActiveDeals.Count == 0)
                panel.Add(Ui.Wrapping(
                    E.State.AverageListeners(E.Config.AvgListenerWindow) < 300
                        ? "No offers yet — the show's too small for a sponsor to be interested. Keep growing."
                        : "No offers right now. Check back in a few weeks.", "body", "dim"));
            else if (!slotFree)
                panel.Add(Ui.Wrapping("All your sponsor slots are full.", "body", "dim"));
            return panel;
        }

        VisualElement BuildStudioPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Studio — gear, crew & upgrades", "h2"));
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

            void HireRow(string name, bool have, string effect, string cost, bool canAfford, System.Action doHire)
            {
                var row = Ui.Row();
                var left = Ui.Box();
                left.style.flexGrow = 1;
                left.Add(Ui.Text(name + (have ? "  ✓" : ""), "topiccard-title"));
                left.Add(Ui.Wrapping(effect + (have ? "" : $"  ({cost})"), "body", "dim"));
                row.Add(left);
                if (!have)
                {
                    var b = Ui.Btn("Hire", () => { doHire(); _host.RerenderWeek(); }, "btn-ghost");
                    b.SetEnabled(canAfford);
                    row.Add(b);
                }
                panel.Add(row);
                panel.Add(Ui.Divider());
            }

            HireRow("Co-host", st.HasCoHost,
                $"+{E.Config.CoHostPrepBonus} prep a week and a little appeal.",
                $"€{E.Config.CoHostMonthlyWage}/mo", E.CanHireCoHost(), () => E.HireCoHost());

            foreach (var role in CrewCatalog.All)
            {
                var r = role;
                HireRow(r.Name, st.HasCrew(r.Id), r.Effect, $"€{E.CrewWage(r.Id)}/mo",
                    E.CanHireCrew(r.Id), () => E.HireCrew(r.Id));
            }

            panel.Add(Ui.Text("UPGRADES", "eyebrow"));
            HireRow("Faster PC / studio space", st.HasStudioSpace,
                "+2 prep points a week and a little more reach.",
                $"{Ui.Money(E.Config.StudioSpaceCost)} + €{E.Config.StudioSpaceMonthly}/mo",
                E.CanBuyStudioSpace(), () => E.BuyStudioSpace());
            HireRow("Second sponsor slot", st.HasSecondSponsorSlot,
                "Hold two sponsor deals at once.", Ui.Money(E.Config.SecondSponsorSlotCost),
                E.CanBuySecondSponsorSlot(), () => E.BuySecondSponsorSlot());

            return panel;
        }

        // ------------------------------------------------------------------
        void RenderTopics()
        {
            _topicList.Clear();
            foreach (var topic in E.Offer)
            {
                var card = Ui.Box("topiccard");
                bool isThread = topic.SourceThread != null;
                if (ReferenceEquals(_picked, topic)) card.AddToClassList("selected");

                var titleRow = Ui.Row();
                titleRow.Add(Ui.Text(topic.Name, "topiccard-title"));
                if (isThread)
                {
                    var tag = Ui.Text("STORY", "chip");
                    tag.AddToClassList("chip-story");
                    titleRow.Add(tag);
                }
                card.Add(titleRow);
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

                if (isThread)
                {
                    var thread = topic.SourceThread;
                    card.Add(Ui.Wrapping(thread.StanceQuestion, "body", "dim"));
                    var btns = Ui.Box("row-wrap");
                    var plus = Ui.Btn(thread.StancePlusLabel, () => PickThread(topic, +1), "btn-ghost");
                    var minus = Ui.Btn(thread.StanceMinusLabel, () => PickThread(topic, -1), "btn-ghost");
                    if (ReferenceEquals(_picked, topic) && _plan.Stance == +1) plus.AddToClassList("btn-primary");
                    if (ReferenceEquals(_picked, topic) && _plan.Stance == -1) minus.AddToClassList("btn-primary");
                    btns.Add(plus);
                    btns.Add(minus);
                    card.Add(btns);
                }
                else
                {
                    var picked = topic;
                    card.RegisterCallback<ClickEvent>(_ => Pick(picked));
                }
                _topicList.Add(card);
            }
        }

        void PickThread(Topic topic, int stance)
        {
            _plan.Stance = stance;
            _plan.ThreadTopic = topic;
            _plan.Topic = default;
            SelectTopic(topic);
        }

        void Pick(Topic topic)
        {
            _plan.Topic = topic.Id;
            _plan.ThreadTopic = null;
            _plan.Stance = 0;
            SelectTopic(topic);
        }

        void SelectTopic(Topic topic)
        {
            bool changed = !ReferenceEquals(_picked, topic);
            _picked = topic;

            if (changed)
            {
                // Sensible default allocation: meet the effort, spread the rest.
                int cap = E.State.PrepCapacity(E.Config);
                _plan.PrepTopic = Mathf.Min(topic.Effort, cap);
                int left = cap - _plan.PrepTopic;
                _plan.PrepResearch = Mathf.Clamp(left / 3, 0, 6);
                left -= _plan.PrepResearch;
                _plan.PrepAudio = Mathf.Clamp(left / 2, 0, 6);
                left -= _plan.PrepAudio;
                _plan.PrepPromo = Mathf.Clamp(left, 0, 6);
            }

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
            bool ok = _picked != null && used <= cap && used > 0
                      && E.Events.Pending == null && E.Scoops.Pending == null && !E.State.BuyoutPending;
            _publish.SetEnabled(ok);

            if (_picked == null) return;
            var topic = _picked;

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
                _plan.ThreadTopic = null;
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
            if (_picked == null) return;
            _host.Publish(_plan.Clone());
        }

        // --- headless capture hooks ---
        public void DebugPickFirst()
        {
            if (_activeTab != 0) SwitchTab(0);
            if (E.Offer.Count > 0) Pick(E.Offer[0]);
        }

        public void DebugPublish()
        {
            if (_picked != null) _host.Publish(_plan.Clone());
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
