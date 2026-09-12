using System;
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

        // tabs
        static readonly string[] TabNames = { "This Week", "The Club", "Business", "Logbook", "Help" };
        int _activeTab;
        readonly List<Button> _tabButtons = new List<Button>();
        ScrollView _scroll;
        VisualElement _tabContent;

        // this-week widgets (rebuilt whenever the This Week tab is shown)
        VisualElement _leversBlock;
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
            if (E.Events.Pending != null || E.Scoops.Pending != null || E.State.BuyoutPending || E.IsRecording) _activeTab = 0;

            var screen = Ui.Box("screen");
            var col = Ui.Box("column-wide");
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
            else if (E.IsRecording)
            {
                var banner = Ui.Box("toast", "toast-story");
                banner.Add(Ui.Wrapping(
                    E.CurrentBeat != null ? "You're recording — something's come up." : "Recording's done — ready to release.",
                    "body"));
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
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            _host.Theme.PaintBar(bar);
            var ink = _host.Theme.Ink(_host.Theme.Primary);

            var portraits = Ui.Row();
            portraits.style.marginRight = 10;
            portraits.Add(Ui.Portrait(PortraitCache.Host(), 40));
            if (st.HasCoHost)
            {
                var co = Ui.Portrait(PortraitCache.CoHost(), 40);
                co.style.marginLeft = -14;
                portraits.Add(co);
            }
            bar.Add(portraits);

            var text = Ui.Box();
            var t1 = Ui.Text(st.PodcastName, "h2");
            t1.style.color = ink;
            t1.style.marginTop = 0;
            t1.style.marginBottom = 0;
            var t2 = Ui.Text(
                $"Season {st.Season} · Week {ctx.Turn} · {st.ClubName} sit {ctx.LeaguePositionLabel}", "body");
            t2.style.color = ink;
            text.Add(t1);
            text.Add(t2);
            bar.Add(text);
            return bar;
        }

        VisualElement BuildResourceStrip(GameState st)
        {
            var strip = Ui.Box("statstrip");
            strip.Add(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null,
                "Cash. Overhead bleeds it every week."));
            strip.Add(Ui.Stat("Listeners", st.Listeners.ToString("N0"), null,
                "Your audience and your score — the loyal core plus the casual listeners who come and go."));
            strip.Add(Ui.Stat("Loyalty", st.LoyaltyLabel,
                st.LoyaltyLabel == "Fragile" ? "bad" : st.LoyaltyLabel == "Devoted" ? "good" : null,
                "How much of your audience is loyal core vs. casual. A fragile audience swings hard and can collapse; a devoted one shrugs off a bad week."));
            if (Mathf.RoundToInt(st.Followers) >= 100)
                strip.Add(Ui.Stat("Followers", Mathf.RoundToInt(st.Followers).ToString("N0"), null,
                    "Clip-only followers. They never hear the show and barely pay, but they spread it and feed casual listeners."));
            strip.Add(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString(), null,
                "How respected the show is. Raises your growth ceiling."));
            strip.Add(Ui.Stat("Credibility", Mathf.RoundToInt(st.Credibility).ToString(), null,
                "How much people trust what you say. Built by analysis and verified scoops."));
            strip.Add(Ui.Stat("Social reach", Mathf.RoundToInt(st.SocialReach).ToString(), null,
                "How much the show is talked about online. Fades if you go quiet."));
            strip.Add(Ui.Stat("Freshness", Mathf.RoundToInt(st.Freshness).ToString(),
                st.Freshness < 45f ? "bad" : null,
                "Falls when you repeat yourself — same angle, same bit. Vary the show or take a lighter week to recover."));
            strip.Add(Ui.Stat("Morale", Mathf.RoundToInt(st.Morale).ToString(),
                st.Morale < 40f ? "bad" : null,
                "The team's energy. Pushing hard costs it; a light week or a win restores it. Low morale caps episode quality."));
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

            // The recording phase (spec §10): once you hit Record, the rundown is locked in and
            // a few quick beats can arise before the episode actually goes out.
            if (E.IsRecording)
            {
                root.Add(BuildRecordingPanel());
                return;
            }

            // Landscape's extra width goes here: the decisions on the left, their
            // running consequence (levers + live preview) alongside on the right,
            // instead of everything stacked in one long scroll.
            var columns = Ui.Box("week-columns");
            var left = Ui.Box("week-col-left");
            var right = Ui.Box("week-col-right");
            columns.Add(left);
            columns.Add(right);
            root.Add(columns);

            // the rundown — three segment slots
            left.Add(BuildRundownPanel());

            // the weekly gamble (spec §11)
            left.Add(BuildPushPanel());

            // cards — a step in the weekly loop: play up to a couple to shape this episode
            left.Add(BuildWeeklyCardsPanel());

            // production levers + the live preview of the episode you're building
            right.Add(BuildLeversPanel());

            _publish = Ui.Btn("Record & release", StartRecording, "btn-primary");
            _host.Theme.PaintPrimaryButton(_publish);
            _publish.style.marginTop = 6;
            _publish.SetEnabled(false);
            right.Add(_publish);

            SyncSliders();
            RefreshPreview();
        }

        VisualElement BuildRecordingPanel()
        {
            var box = Ui.Box();
            if (!string.IsNullOrEmpty(E.LastBeatOutcome))
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping(E.LastBeatOutcome, "body"));
                box.Add(toast);
            }

            var panel = Ui.Box("panel", "event-card");
            var beat = E.CurrentBeat;
            if (beat != null)
            {
                panel.Add(Ui.Text("RECORDING", "eyebrow"));
                panel.Add(Ui.Text(beat.Prompt, "h2"));
                panel.Add(Ui.Wrapping(beat.Detail, "body"));
                for (int i = 0; i < beat.Options.Count; i++)
                {
                    int idx = i;
                    var b = Ui.Btn(beat.Options[i].Label, () => { E.ResolveBeat(idx); _host.RerenderWeek(); }, "btn-ghost");
                    b.style.marginTop = 4;
                    panel.Add(b);
                }
            }
            else
            {
                panel.Add(Ui.Text("RECORDING", "eyebrow"));
                panel.Add(Ui.Text("That's a wrap", "h2"));
                panel.Add(Ui.Wrapping(
                    string.IsNullOrEmpty(E.LastBeatOutcome)
                        ? "A clean recording — nothing went sideways."
                        : "Ready to release.", "body", "dim"));
                var release = Ui.Btn("Release it", () => _host.Publish(E.PendingPlan), "btn-primary");
                _host.Theme.PaintPrimaryButton(release);
                release.style.marginTop = 6;
                panel.Add(release);
            }
            box.Add(panel);
            return box;
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
            if (E.Cards.CanDrawRandomCard(st))
            {
                var draw = Ui.Btn($"Chase a lead  ({Ui.Money(E.Config.CardDrawCost)})", () => { E.DrawRandomCard(); _host.RerenderWeek(); }, "btn-ghost");
                head.Add(draw);
            }
            panel.Add(head);

            if (st.Hand.Count == 0)
            {
                panel.Add(Ui.Wrapping(
                    "No cards right now. You get one at every listener milestone, resolving a thread, or from Business.", "body", "dim"));
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

        // Business-tab view: hand + shop (spec §21 — deterministic pick, or a cheap random draw)
        // plus the three contact slots.
        VisualElement BuildCardsPanel()
        {
            var st = E.State;
            var panel = Ui.Box("panel");
            var head = Ui.Row();
            head.Add(Ui.Text("Cards", "h2"));
            var draw = Ui.Btn($"Chase a lead  ({Ui.Money(E.Config.CardDrawCost)})", () => { E.DrawRandomCard(); _host.RerenderWeek(); }, "btn-ghost");
            draw.SetEnabled(E.Cards.CanDrawRandomCard(st));
            head.Add(draw);
            panel.Add(head);
            panel.Add(Ui.Wrapping(
                $"Hand: {st.Hand.Count}/{E.Config.CardHandLimit}. Play up to {E.Config.CardPlaysPerWeek} a week, on This Week. " +
                "Most come from resolving threads and hitting milestones; chase a lead for a random one, or book something specific below.",
                "body", "dim"));

            if (st.Hand.Count == 0)
                panel.Add(Ui.Wrapping("No cards in hand.", "body", "dim"));
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

            panel.Add(Ui.Divider());
            panel.Add(Ui.Text("BOOK SOMETHING", "eyebrow"));
            panel.Add(Ui.Wrapping($"Pick exactly the card you want for {Ui.Money(E.Config.CardPickCost)}.", "body", "dim"));
            var shopRow = Ui.Box("row-wrap");
            foreach (var card in CardManager.Catalog.Values)
            {
                string cid = card.Id;
                var b = Ui.Btn(card.Name, () => { E.BuySpecificCard(cid); _host.RerenderWeek(); }, "btn-ghost");
                b.SetEnabled(E.Cards.CanBuySpecificCard(st, cid));
                shopRow.Add(b);
            }
            panel.Add(shopRow);

            panel.Add(Ui.Divider());
            panel.Add(Ui.Text("CONTACTS", "eyebrow"));
            panel.Add(Ui.Wrapping(
                $"{st.Contacts.Count}/{E.Config.ContactSlots} slots. Each is a passive weekly effect — a real upside, a real downside. " +
                "Swap freely as the run changes.", "body", "dim"));
            foreach (var contact in CardManager.Contacts)
            {
                bool slotted = st.Contacts.Contains(contact.Id);
                var box = Ui.Box("topiccard");
                if (slotted) box.AddToClassList("selected");
                box.Add(Ui.Text(contact.Name, "topiccard-title"));
                box.Add(Ui.Wrapping("+ " + contact.Upside, "body", "good"));
                box.Add(Ui.Wrapping("− " + contact.Downside, "body", "bad"));
                if (slotted)
                {
                    var unslot = Ui.Btn("Drop", () => { E.UnslotContact(contact.Id); _host.RerenderWeek(); }, "btn-ghost");
                    unslot.style.marginTop = 4;
                    box.Add(unslot);
                }
                else
                {
                    var slot = Ui.Btn($"Slot in  ({Ui.Money(E.Config.ContactSlotCost)})", () => { E.SlotContact(contact.Id); _host.RerenderWeek(); }, "btn-ghost");
                    slot.style.marginTop = 4;
                    slot.SetEnabled(E.Cards.CanSlotContact(st, contact.Id));
                    box.Add(slot);
                }
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
                "Every week your club plays. You see the result and how surprising it was, then you build that week's episode.\n" +
                "1. Deal with anything waiting — an interrupt event, a scoop, a big decision.\n" +
                "2. Build the rundown: a main story, a second segment and a small recurring bit. Second and recurring can be left empty for a lighter week.\n" +
                "3. For each segment, pick a topic and an angle. The angle — analysis, hot take, emotional, comedy, investigation — decides how it lands: reach, risk, and what it does to your reputation and credibility.\n" +
                "4. Set the push dial — how hard you're going this week. Louder means more reach and chatter, and a real chance it backfires.\n" +
                "5. Optionally play a card or two.\n" +
                "6. Split your prep points across the segments and the three levers — research, audio, promo.\n" +
                "7. Hit Record. A few quick things can come up during recording — your call on each. Then release it. The result is a surprise until it's out.\n" +
                "Spend what you earn on gear, crew and a co-host. Keep money above water. Grow the audience.", "body"));
            root.Add(week);

            var numbers = Ui.Box("panel");
            numbers.Add(Ui.Text("THE NUMBERS", "eyebrow"));
            numbers.Add(Ui.Wrapping(
                "Listeners — your audience and your score. Some are loyal core who barely leave and pay the bills; " +
                "the rest are casual, chasing whatever's loud that week, and they churn fast. The Loyalty read — " +
                "Devoted / Solid / Fickle / Fragile — tells you the mix. A fragile audience is big but one bad week from a collapse; " +
                "a devoted one shrugs things off. Consistency and credibility turn casual listeners into core.\n\n" +
                "Followers — clip-only. They never hear the show and barely pay, but they spread it and slowly feed the casual audience.\n\n" +
                "Reputation (0–100) — how seriously the show is taken. Thoughtful, well-made episodes and measured takes build it; " +
                "lazy episodes and cheap drama burn it. It matters because a well-regarded show can grow much bigger — high reputation " +
                "lifts the ceiling on your audience. Some topics also need a minimum reputation before you can cover them.\n\n" +
                "Money — ad income barely covers the weekly overhead on its own, so the budget is tight until the audience is large. " +
                "Three straight weeks more than €200 in the red and the show folds. It also buys card packs and a topic redraw.\n\n" +
                "Credibility (0–100) — how much people trust what you say. Analysis and verified scoops build it; hot takes that miss burn it. " +
                "Trust is what turns casual listeners into a loyal core, and the club won't grant real access without it.\n\n" +
                "Social reach (0–100) — how loudly the show is talked about online. A breakout episode, a clip, a spicy take all raise it; " +
                "go quiet and it fades. It brings new listeners in fast — and makes a bad week travel further too.\n\n" +
                "Morale (0–100) — the team's energy. Pushing hard, back to back, wears it down; a light week or a win brings it back. " +
                "Below 40 the episode is capped, and there's a real chance it comes out sounding phoned in.", "body"));
            root.Add(numbers);

            var gloss = Ui.Box("panel");
            gloss.Add(Ui.Text("GLOSSARY", "eyebrow"));
            void Term(string t, string d)
            {
                gloss.Add(Ui.Text(t, "topiccard-title"));
                gloss.Add(Ui.Wrapping(d, "body", "dim"));
                gloss.Add(Ui.Divider());
            }
            Term("Loyalty", "How much of your audience is loyal core vs. casual drop-ins. Devoted / Solid / Fickle / Fragile. Analysis, consistency and credibility build a devoted core; a hot-take show grows fast but stays fragile.");
            Term("Credibility", "How much people trust what you say (0–100). Analysis and verified scoops build it; hot takes and missed scoops burn it. Low credibility loses you insider access and bleeds your core audience.");
            Term("Freshness", "Falls when you repeat yourself — same angle, same recurring bit, same subject week after week. Drags down how far every segment reaches. Vary the show, or take a lighter week, to recover.");
            Term("Slump", "Two weak episodes in a row, or a tonal-whiplash clash, and listeners start leaving faster than usual. Two strong episodes back to back pulls you out.");
            Term("Push", "How hard you're going this week, 1-5. Higher push means more appeal and more social reach — and raises the chance the week's take backfires.");
            Term("Backfire", "The gamble not paying off. Costs reputation and credibility, and the casual audience thins out for the week. A correction opportunity appears on next week's offer — cover it well and the credibility comes back.");
            Term("Recording", "After you hit Record, the plan is locked in and a few quick things can come up — your co-host goes off script, the audio glitches, a great bit happens. Your call on each shapes the episode.");
            Term("Morale", "The team's energy (0-100). Pushing hard costs it; a light week or a win restores it. Low morale caps how good the episode can be.");
            Term("Rundown", "The three segments that make up the episode — main story, second segment, recurring bit. The main carries most of the reach; the recurring bit is small but it builds the show's identity over time.");
            Term("Angle", "How a segment covers its topic. Analysis is safe and builds credibility. Hot take is loud — big reach, but it burns credibility. Emotional lands on a big result. Comedy drives clips. Investigation digs in (needs a Researcher or insider access).");
            Term("Draw", "How many listeners a topic pulls in this week, before quality. Shifts with the result, the fixture, the angle and story context.");
            Term("Prep needed", "The prep points it takes to cover a topic properly. Under it and the episode sounds thin; a point or two over gives a small edge.");
            Term("Risk", "How much the outcome can swing. Research prep narrows it — a gamble becomes a safer bet.");
            Term("Surprise", "How far the match result landed from what was expected. Drives the mood you're reacting to and which topics land.");
            Term("Topic prep / Research / Audio / Promo", "Topic prep = the homework. Research = less variance. Audio = a better episode and less churn. Promo = a one-week reach bump only.");
            Term("Running stories", "Ongoing club storylines. Pick the STORY topic to take a side; match the eventual outcome and you gain reputation, call it wrong and it costs you.");
            Term("Sponsors", "A deal pays weekly for 1-3 terms — a growth target, an ongoing constraint (an appeal hit, a credibility floor), a one-off ask, or a conduct clause. Meet every term by the deadline for a bonus and a better renewal; miss one, or break a standing/conduct term along the way, and the deal ends badly.");
            Term("Cup & Europe", "Extra midweek fixtures alongside the league. Win the cup or finish high enough and you play in Europe next season — bigger nights, more reach.");
            Term("Access tier", "Set by your rolling-average listeners. Tier 2 unlocks scoops and the Big interview; tier 3 lets your takes move the club, and may bring a partnership offer.");
            Term("Scoops", "Advance word on a club decision. Break it now for a huge episode and a real risk it's wrong (which costs access); verify & hold to build trust; or trade it for cash.");
            Term("Crew", "Producer, Researcher, Clips manager, Booker. Each role is filled from 2-3 candidates who refresh every few weeks — skill scales the role's effect, and traits give a real upside and a real downside. Firing someone costs severance, more if they're still under contract.");
            Term("Upgrade tracks", "Five ladders — Set, Audio chain, Post/editing, Studio space, Distribution — each 3-4 tiers. Buy in order; each tier replaces the one below it and usually adds a bit to the monthly bill.");
            Term("Cards", "Plays — one-shot buffs or permanent lifts, hand of five, up to two a week. Mostly earned from milestones and threads; chase a lead for a random one or book an exact one for more.");
            Term("Contacts", "Up to three passive slots, each a fixed weekly upside and downside. Cheap to swap — worth re-evaluating as the show changes size.");
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
                deal.Add(Ui.Wrapping($"€{o.Weekly:N0} a week. {active.WeeksLeft} week(s) left on the deal.", "body"));
                foreach (var term in o.Terms)
                    deal.Add(Ui.Wrapping("• " + TermStatusLine(term), "body", TermGood(term) ? "good" : "dim"));
                deal.Add(Ui.Wrapping(
                    active.AllMet
                        ? "Every growth term is met — the bonus lands when the deal ends."
                        : "Miss any growth term by the deadline, or break a standing/conduct term, and the deal ends badly.",
                    "body", active.AllMet ? "good" : "dim"));
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
                    foreach (var term in o.Terms)
                        card.Add(Ui.Wrapping("• " + term.Text, "body", term.Kind == SponsorTermKind.Growth ? null : "bad"));
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

        static string TermStatusLine(SponsorTerm term) => term.Kind switch
        {
            SponsorTermKind.Growth => term.Met ? $"{term.Text} — hit" : term.Text,
            SponsorTermKind.Ask => term.Met ? $"{term.Text} — done" : term.Text,
            SponsorTermKind.Standing => term.Text + " — holding",
            SponsorTermKind.Conduct => term.Text + " — clean so far",
            _ => term.Text
        };

        static bool TermGood(SponsorTerm term) =>
            (term.Kind == SponsorTermKind.Growth || term.Kind == SponsorTermKind.Ask) && term.Met;

        static readonly string[] TrackLabels =
        {
            "Set", "Audio chain", "Post / editing", "Studio space", "Distribution"
        };

        VisualElement BuildStudioPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("Studio — upgrade tracks, gear & crew", "h2"));
            panel.Add(Ui.Wrapping(
                "Five tracks, each a ladder of tiers. Buy in order — each tier replaces the one below it.",
                "body", "dim"));
            var st = E.State;

            void TrackRow(UpgradeTrack track)
            {
                var current = E.CurrentUpgrade(track);
                var next = E.NextUpgrade(track);
                var row = Ui.Row();
                var left = Ui.Box();
                left.style.flexGrow = 1;
                left.Add(Ui.Text($"{TrackLabels[(int)track]} — {current.Name}", "topiccard-title"));
                left.Add(Ui.Wrapping(current.Level == 0 ? "Not upgraded yet." : current.Effect, "body", "dim"));
                row.Add(left);
                if (next != null)
                {
                    string cost = next.Monthly > 0 ? $"{Ui.Money(next.Cost)} + €{next.Monthly}/mo" : Ui.Money(next.Cost);
                    var buy = Ui.Btn($"{next.Name}  ({cost})", () => { E.BuyUpgrade(track); _host.RerenderWeek(); }, "btn-ghost");
                    buy.SetEnabled(E.CanBuyUpgrade(track));
                    row.Add(buy);
                }
                panel.Add(row);
                if (next != null) panel.Add(Ui.Wrapping(next.Effect, "body", "dim"));
                panel.Add(Ui.Divider());
            }

            panel.Add(Ui.Text("UPGRADE TRACKS", "eyebrow"));
            foreach (UpgradeTrack track in Enum.GetValues(typeof(UpgradeTrack)))
                TrackRow(track);

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

            panel.Add(Ui.Text("CREW", "eyebrow"));
            foreach (var role in CrewCatalog.All)
                panel.Add(BuildCrewRoleBlock(role));

            panel.Add(Ui.Text("OTHER", "eyebrow"));
            HireRow("Second sponsor slot", st.HasSecondSponsorSlot,
                "Hold two sponsor deals at once.", Ui.Money(E.Config.SecondSponsorSlotCost),
                E.CanBuySecondSponsorSlot(), () => E.BuySecondSponsorSlot());

            return panel;
        }

        VisualElement BuildCrewRoleBlock(CrewRole role)
        {
            var st = E.State;
            var box = Ui.Box();
            box.style.marginBottom = 6;
            box.Add(Ui.Text($"{role.Name}  ({role.Effect})", "topiccard-title"));

            if (st.Employed.TryGetValue(role.Id, out var emp))
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.Add(Ui.Portrait(PortraitCache.Crew(role.Id, emp.Name), 32, "portrait-inline"));
                var left = Ui.Box();
                left.style.flexGrow = 1;
                string traits = emp.TraitText;
                left.Add(Ui.Wrapping(
                    $"{emp.Name} — skill {emp.Skill:0.0}, €{emp.Wage:N0}/mo" + (traits.Length > 0 ? $"  ·  {traits}" : ""),
                    "body"));
                if (emp.ContractWeeksLeft > 0)
                    left.Add(Ui.Wrapping($"Under contract for {emp.ContractWeeksLeft} more week(s) — firing early costs a buyout.", "body", "dim"));
                row.Add(left);
                var fire = Ui.Btn($"Fire  ({Ui.Money(E.FireCost(role.Id))})", () => { E.FireCrew(role.Id); _host.RerenderWeek(); }, "btn-ghost");
                row.Add(fire);
                box.Add(row);
            }
            else
            {
                var candidates = E.CandidatesFor(role.Id);
                if (candidates.Count == 0)
                {
                    box.Add(Ui.Wrapping("No candidates on the market right now — check back in a few weeks.", "body", "dim"));
                }
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    int idx = i;
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.Add(Ui.Portrait(PortraitCache.Crew(role.Id, c.Name), 32, "portrait-inline"));
                    var left = Ui.Box();
                    left.style.flexGrow = 1;
                    string traits = c.TraitText;
                    left.Add(Ui.Wrapping(
                        $"{c.Name} — skill {c.Skill:0.0}, €{c.Wage:N0}/mo" + (traits.Length > 0 ? $"  ·  {traits}" : ""),
                        "body", "dim"));
                    row.Add(left);
                    var hire = Ui.Btn("Hire", () => { E.HireCandidate(role.Id, idx); _host.RerenderWeek(); }, "btn-ghost");
                    hire.SetEnabled(E.CanHireCandidate(role.Id, idx));
                    row.Add(hire);
                    box.Add(row);
                }
            }
            box.Add(Ui.Divider());
            return box;
        }

        // ================================================================
        // The rundown — three segment slots, each a topic + an angle (spec §7)
        // ================================================================
        VisualElement BuildRundownPanel()
        {
            var panel = Ui.Box("panel");
            var head = Ui.Row();
            head.Add(Ui.Text("This week's rundown", "h2"));
            _redraw = Ui.Btn(RedrawLabel(), Redraw, "btn-ghost");
            _redraw.SetEnabled(!E.HasRedrawnThisWeek && E.State.Money >= E.Config.RedrawCost);
            head.Add(_redraw);
            panel.Add(head);
            panel.Add(Ui.Wrapping(
                "Build the show from three segments. The main story carries most of the reach; the recurring bit is small " +
                "but it's what gives the show its identity. Pick a topic and an angle for each — the angle decides how it lands.",
                "body", "dim"));

            panel.Add(BuildSlotCard(_plan.Main, "MAIN STORY", canBeEmpty: false));
            panel.Add(BuildSlotCard(_plan.Second, "SECOND SEGMENT", canBeEmpty: true));
            panel.Add(BuildSlotCard(_plan.Recurring, "RECURRING BIT", canBeEmpty: true));
            return panel;
        }

        static readonly string[] PushLabels = { "1 · Head down", "2 · Normal", "3 · Bold", "4 · Loud", "5 · Full send" };

        VisualElement BuildPushPanel()
        {
            var panel = Ui.Box("panel");
            panel.Add(Ui.Text("How hard are you pushing it?", "h2"));
            panel.Add(Ui.Wrapping(
                "The editorial line for the whole episode. Louder means more reach and more chatter — and a real chance " +
                "a take lands badly. Keep your head down to lie low for a week.", "body", "dim"));
            var row = Ui.Box("row-wrap");
            for (int i = 1; i <= 5; i++)
            {
                int val = i;
                var b = Ui.Btn(PushLabels[i - 1], () => { _plan.Push = val; _host.RerenderWeek(); }, "btn-ghost");
                if (_plan.Push == val) b.AddToClassList("btn-primary");
                row.Add(b);
            }
            panel.Add(row);
            return panel;
        }

        VisualElement BuildSlotCard(Segment seg, string label, bool canBeEmpty)
        {
            var st = E.State;
            var ctx = E.CurrentWeek;
            var card = Ui.Box("panel", "panel-tight");
            card.Add(Ui.Text(label, "eyebrow"));

            // --- topic picker ---
            var topicRow = Ui.Box("row-wrap");
            if (canBeEmpty)
            {
                var none = Ui.Btn("— none —", () => { seg.Clear(); _host.RerenderWeek(); }, "btn-ghost");
                if (seg.IsEmpty) none.AddToClassList("btn-primary");
                topicRow.Add(none);
            }
            foreach (var topic in E.Offer)
            {
                var t = topic;
                bool selected = !seg.IsEmpty && ReferenceEquals(seg.Resolved, t);
                var b = Ui.Btn(t.Name + (t.SourceThread != null ? "  (story)" : ""), () => AssignTopic(seg, t), "btn-ghost");
                if (selected) b.AddToClassList("btn-primary");
                topicRow.Add(b);
            }
            card.Add(topicRow);

            if (seg.IsEmpty)
            {
                card.Add(Ui.Wrapping("Nothing in this slot — a lighter week.", "body", "dim"));
                return card;
            }

            var chosen = seg.Resolved;
            card.Add(Ui.Wrapping(chosen.Blurb, "body", "dim"));

            // --- thread stance ---
            if (chosen.SourceThread != null)
            {
                var thr = chosen.SourceThread;
                card.Add(Ui.Wrapping(thr.StanceQuestion, "body", "dim"));
                var stanceRow = Ui.Box("row-wrap");
                var plus = Ui.Btn(thr.StancePlusLabel, () => { seg.Stance = +1; _host.RerenderWeek(); }, "btn-ghost");
                var minus = Ui.Btn(thr.StanceMinusLabel, () => { seg.Stance = -1; _host.RerenderWeek(); }, "btn-ghost");
                if (seg.Stance == +1) plus.AddToClassList("btn-primary");
                if (seg.Stance == -1) minus.AddToClassList("btn-primary");
                stanceRow.Add(plus); stanceRow.Add(minus);
                card.Add(stanceRow);
            }

            // --- angle picker ---
            card.Add(Ui.Text("Angle", "prep-name"));
            var angleRow = Ui.Box("row-wrap");
            foreach (var ap in AngleCatalog.All)
            {
                var a = ap;
                var b = Ui.Btn(a.Name, () => { seg.Angle = a.Id; _host.RerenderWeek(); }, "btn-ghost");
                if (seg.Angle == a.Id) b.AddToClassList("btn-primary");
                b.SetEnabled(AngleCatalog.Allowed(a.Id, st));
                angleRow.Add(b);
            }
            card.Add(angleRow);
            card.Add(Ui.Wrapping(AngleCatalog.Get(seg.Angle).Blurb, "body", "dim"));

            // --- guest ---
            if (st.HasCrew(Crew.Booker) || st.AccessTier >= 2)
            {
                var g = new Toggle("Book a guest for this segment") { value = seg.Guest };
                g.RegisterValueChangedCallback(e => { seg.Guest = e.newValue; RefreshPreview(); });
                card.Add(g);
            }

            // --- per-segment topic prep ---
            card.Add(SegPrepSlider(seg));

            // --- read ---
            var ang = AngleCatalog.Get(seg.Angle);
            float ctxMult = ContextResolver.AppealMultiplier(chosen.Response, ctx) * ctx.ImportanceAppealMult;
            float draw = chosen.BaseAppeal * ctxMult * ang.AppealMult;
            int effort = Mathf.Max(1, chosen.Effort - CrewCatalog.EffortRelief(st));
            var chips = Ui.Box("row-wrap");
            chips.Add(Ui.Chip("Draw: " + DrawLabel(draw)));
            chips.Add(Ui.Chip($"Prep needed: {effort}"));
            chips.Add(Ui.Chip(RiskLabel(chosen.Swing * ang.SwingMult)));
            if (ang.CredDelta >= 1.5f) chips.Add(Ui.Chip("Builds credibility"));
            else if (ang.CredDelta <= -1f) chips.Add(Ui.Chip("Costs credibility"));
            if (ang.SocialAdd >= 4 || chosen.SocialHook >= 3) chips.Add(Ui.Chip("Gets shared"));
            card.Add(chips);

            return card;
        }

        VisualElement SegPrepSlider(Segment seg)
        {
            var wrap = Ui.Box();
            wrap.style.marginTop = 4;
            var row = Ui.Box("prep-row");
            row.Add(Ui.Text("Prep on this segment", "prep-name"));
            var s = new SliderInt(0, 10) { value = seg.Prep };
            s.AddToClassList("prep-slider");
            var val = Ui.Text(seg.Prep.ToString(), "prep-value");
            s.RegisterValueChangedCallback(e => { seg.Prep = e.newValue; val.text = e.newValue.ToString(); RefreshPreview(); });
            row.Add(s); row.Add(val);
            wrap.Add(row);
            return wrap;
        }

        void AssignTopic(Segment seg, Topic topic)
        {
            bool changed = seg.IsEmpty || !ReferenceEquals(seg.Resolved, topic);
            seg.Set(topic, topic.SourceThread != null ? (topic.SourceThread.Momentum >= 0f ? 1 : -1) : 0);
            if (changed && seg.Prep == 0)
            {
                int effort = Mathf.Max(1, topic.Effort - CrewCatalog.EffortRelief(E.State));
                int room = Mathf.Max(0, E.State.PrepCapacity(E.Config) - _plan.TotalPrep);
                seg.Prep = Mathf.Min(effort, room);
            }
            _host.RerenderWeek();
        }

        VisualElement BuildLeversPanel()
        {
            var st = E.State;
            _prep.Clear();
            _leversBlock = Ui.Box("panel");
            _leversBlock.Add(Ui.Text("Production", "h2"));
            _leversBlock.Add(Ui.Wrapping(
                $"You have {st.PrepCapacity(E.Config)} prep points this week, shared across the segments above and the three levers below. " +
                "Unspent points are wasted.", "body", "dim"));
            _prepMeter = Ui.Text("", "prep-meter");
            _leversBlock.Add(_prepMeter);
            _leversBlock.Add(Lever("Research", "Fact-checking and depth. Narrows the swing on every segment — a gamble becomes a safer bet.",
                () => _plan.PrepResearch, v => _plan.PrepResearch = v));
            _leversBlock.Add(Lever("Audio", "Editing and sound. Lifts every segment and keeps listeners from drifting away.",
                () => _plan.PrepAudio, v => _plan.PrepAudio = v));
            _leversBlock.Add(Lever("Promo", "Pushing this one episode — clips, posts, plugs. A one-week bump in reach, nothing lasting.",
                () => _plan.PrepPromo, v => _plan.PrepPromo = v));
            _leversBlock.Add(Ui.Divider());
            _previewBlock = Ui.Box();
            _leversBlock.Add(_previewBlock);
            return _leversBlock;
        }

        VisualElement Lever(string name, string help, System.Func<int> get, System.Action<int> set)
        {
            var wrap = Ui.Box();
            wrap.style.marginBottom = 8;
            var row = Ui.Box("prep-row");
            row.Add(Ui.Text(name, "prep-name"));
            var s = new SliderInt(0, 8) { value = get() };
            s.AddToClassList("prep-slider");
            var val = Ui.Text(get().ToString(), "prep-value");
            s.RegisterValueChangedCallback(e => { set(e.newValue); val.text = e.newValue.ToString(); RefreshPreview(); });
            row.Add(s); row.Add(val);
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
            if (_prepMeter == null || _publish == null) return;
            int cap = E.State.PrepCapacity(E.Config);
            int used = _plan.TotalPrep;
            _prepMeter.text = $"Prep points spent   {used} / {cap}";
            _prepMeter.EnableInClassList("over", used > cap);

            _previewBlock?.Clear();
            bool ok = !_plan.Main.IsEmpty && used <= cap && used > 0
                      && E.Events.Pending == null && E.Scoops.Pending == null && !E.State.BuyoutPending;
            _publish.SetEnabled(ok);

            if (_previewBlock == null) return;

            if (_plan.Main.IsEmpty)
            {
                _previewBlock.Add(Ui.Wrapping("Pick a main story to build the episode around.", "body", "dim"));
                return;
            }

            int filled = 0;
            foreach (var s in _plan.FilledSlots) filled++;
            _previewBlock.Add(Ui.Wrapping(
                filled == 1 ? "A single-segment episode — lean, but it leaves reach on the table."
                : filled == 2 ? "Two segments. A solid show."
                : "A full three-segment rundown.", "body", "dim"));

            // Interaction read — how the segments play together (spec §8).
            if (used <= cap && used > 0)
            {
                var preview = E.Preview(_plan);
                foreach (var note in preview.Notes)
                {
                    bool bad = note == "just ranting" || note == "tonal whiplash" || note == "one-note"
                               || note == "same bit again" || note == "overhyped" || note == "phoned-in"
                               || note == "backfired" || note == "main character of the day";
                    _previewBlock.Add(Ui.Wrapping((bad ? "⚠ " : "✓ ") + NoteLabel(note), "body", bad ? "bad" : "good"));
                }

                if (preview.BackfireChance >= 0.01f)
                {
                    string odds = preview.BackfireChance < 0.08f ? "low" : preview.BackfireChance < 0.20f ? "moderate"
                        : preview.BackfireChance < 0.40f ? "high" : "very high";
                    _previewBlock.Add(Ui.Wrapping(
                        $"Backfire risk this week: {odds} ({preview.BackfireChance * 100f:0}%).",
                        "body", preview.BackfireChance >= 0.25f ? "bad" : "dim"));
                }
            }

            var stt = E.State;
            if (stt.Freshness < 45f)
                _previewBlock.Add(Ui.Wrapping("The show's feeling stale — you've been repeating yourself. Vary the angle, rotate the bit, or take a lighter week.", "body", "bad"));
            if (stt.SlumpWeeks > 0)
                _previewBlock.Add(Ui.Wrapping("You're in a slump — listeners are leaving faster than usual. Two strong episodes in a row pulls you out.", "body", "bad"));

            if (used > cap)
                _previewBlock.Add(Ui.Wrapping("You've allocated more prep points than you have this week.", "body", "bad"));
            else if (used < cap)
                _previewBlock.Add(Ui.Wrapping($"{cap - used} point(s) still unspent.", "body", "dim"));
        }

        static string NoteLabel(string note) => note switch
        {
            "just ranting" => "Two hot takes in one episode reads as ranting — costs reputation and credibility.",
            "tonal whiplash" => "A comedy bit straight after a crisis segment jars — listeners drift.",
            "one-note" => "Main and second are on the same subject — less overall reach (but the story moves faster).",
            "same bit again" => "Same recurring bit too many weeks running — it's wearing thin.",
            "overhyped" => "Heavy promo on a weak episode — the new listeners won't stick.",
            "well-produced" => "Three distinct angles, no clashes — a well-rounded show.",
            "palate cleanser" => "A light bit after a heavy main gives listeners a breather.",
            "deep dive" => "Two analytical segments on one subject — a real deep dive. Credibility up.",
            "range" => "Something serious and something funny — range. More clips.",
            "phoned-in" => "Morale's low and it showed — the episode sounded phoned in.",
            "backfired" => "The push backfired — a take landed badly. Reputation and credibility took a hit.",
            "main character of the day" => "It really backfired — you're the main character today. A pile-on, and a real dent in trust.",
            _ => note
        };

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
                _plan.Main.Clear();
                _plan.Second.Clear();
                _plan.Recurring.Clear();
                _plan.PrepResearch = _plan.PrepAudio = _plan.PrepPromo = 0;
                _host.RerenderWeek();
            }
            else
            {
                _redraw.SetEnabled(false);
            }
        }

        string RedrawLabel() => $"Redraw  ({Ui.Money(E.Config.RedrawCost)})";

        void StartRecording()
        {
            if (_plan.Main.IsEmpty) return;
            E.StartRecording(_plan.Clone());
            _host.RerenderWeek();
        }

        // --- headless capture hooks ---
        public void DebugPickFirst()
        {
            if (_activeTab != 0) SwitchTab(0);
            if (E.Offer.Count > 0) { _plan.Main.Set(E.Offer[0], 1); _plan.Main.Prep = 5; }
        }

        public void DebugPublish()
        {
            if (E.IsRecording)
            {
                while (E.CurrentBeat != null) E.ResolveBeat(0);
                _host.Publish(E.PendingPlan);
            }
            else if (!_plan.Main.IsEmpty)
            {
                E.StartRecording(_plan.Clone());
                while (E.CurrentBeat != null) E.ResolveBeat(0);
                _host.Publish(E.PendingPlan);
            }
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
