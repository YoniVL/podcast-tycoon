using System.Collections.Generic;
using PodcastTycoon.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace PodcastTycoon.Game
{
    public sealed class ResultsScreen
    {
        readonly Bootstrap _host;
        readonly EpisodeResult _result;
        readonly List<MilestoneEvent> _milestones;
        readonly bool _goalReached;

        public ResultsScreen(Bootstrap host, EpisodeResult result, List<MilestoneEvent> milestones, bool goalReached)
        {
            _host = host;
            _result = result;
            _milestones = milestones;
            _goalReached = goalReached;
        }

        public VisualElement Build()
        {
            var st = _host.Engine.State;
            var screen = Ui.Box("screen");
            var col = Ui.Box("column");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("scroll");
            col.Add(scroll);
            screen.Add(col);

            scroll.Add(Ui.Text("EPISODE PUBLISHED", "eyebrow"));
            scroll.Add(Ui.Text($"“{_result.Topic.Name}”", "h1"));
            scroll.Add(Ui.Wrapping(
                $"{_result.QualityLabel} episode — quality {_result.Quality:0.00}" +
                (_result.SegmentCount > 1 ? $", {_result.SegmentCount} segments." : "."), "body"));

            foreach (var note in _result.Notes)
            {
                bool bad = note == "just ranting" || note == "tonal whiplash" || note == "one-note"
                           || note == "same bit again" || note == "overhyped";
                var toast = Ui.Box("toast");
                if (bad) toast.AddToClassList("toast-bad");
                toast.Add(Ui.Wrapping((bad ? "That didn't sit right — " : "That worked — ") + note + ".", "body"));
                scroll.Add(toast);
            }
            if (st.SlumpWeeks == 1 && _result.Whiplash)
            {
                var toast = Ui.Box("toast", "toast-bad");
                toast.Add(Ui.Wrapping("The show's in a slump now. Two strong episodes back to back gets you out of it.", "body"));
                scroll.Add(toast);
            }

            foreach (var m in _milestones)
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping(m.Message, "body"));
                scroll.Add(toast);
            }
            if (_goalReached)
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping("Primary goal reached — 50,000 listeners. You can keep going as long as you like.", "body", "good"));
                scroll.Add(toast);
            }
            if (st.BuyoutPending)
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping("A media group wants to buy the podcast — the offer is waiting on This Week.", "body", "good"));
                scroll.Add(toast);
            }
            var gained = _host.Engine.CurrentWeek?.CardsGained;
            if (gained != null && gained.Count > 0)
            {
                var toast = Ui.Box("toast");
                toast.Add(Ui.Wrapping("New cards: " + string.Join(", ", gained) + ".", "body"));
                scroll.Add(toast);
            }
            if (st.SeasonTurn == 1 && st.LastSeason != null)
            {
                var toast = Ui.Box("toast", "toast-story");
                toast.Add(Ui.Wrapping($"Season {st.LastSeason.Season} wrapped up — finished {st.LastSeason.LeaguePositionLabel}. Full review in the Logbook.", "body"));
                scroll.Add(toast);
            }

            // --- how it landed ---
            var landed = Ui.Box("panel");
            landed.Add(Ui.Text("How it landed", "h2"));
            int dCore = Mathf.RoundToInt(_result.CoreDelta);
            int dCasual = Mathf.RoundToInt(_result.CasualDelta);
            int dFoll = Mathf.RoundToInt(_result.FollowersDelta);
            string landLine;
            if (_result.ListenerDeltaActual > 30 && dCasual > dCore * 2)
                landLine = $"{Ui.Signed(_result.ListenerDeltaActual)} listeners, but mostly casual — chasing the buzz, and they won't all stick around.";
            else if (_result.ListenerDeltaActual > 30 && dCore >= dCasual)
                landLine = $"{Ui.Signed(_result.ListenerDeltaActual)} listeners, and a good share of them converted to your core. That's the audience that stays.";
            else if (_result.ListenerDeltaActual > 5)
                landLine = $"{Ui.Signed(_result.ListenerDeltaActual)} listeners — steady growth.";
            else if (_result.ListenerDeltaActual >= -5)
                landLine = "Roughly flat this week — you held serve.";
            else
                landLine = $"{Ui.Signed(_result.ListenerDeltaActual)} listeners — the casual audience is drifting.";
            landed.Add(Ui.Wrapping(landLine, "body"));
            landed.Add(Ui.Wrapping($"Audience: {_result.LoyaltyAfter}." +
                (dFoll >= 50 ? $"  {Ui.Signed(dFoll)} clip followers." : ""), "body", "dim"));
            scroll.Add(landed);

            // --- what changed ---
            var deltas = Ui.Box("panel");
            deltas.Add(Ui.Text("What changed", "h2"));
            var grid = Ui.Box("statstrip");
            grid.Add(Ui.Stat("Listeners", Ui.Signed(_result.ListenerDeltaActual),
                _result.ListenerDeltaActual >= 0 ? "good" : "bad"));
            grid.Add(Ui.Stat("Reputation", Ui.Signed(_result.ReputationDelta, "0.0")));
            grid.Add(Ui.Stat("Credibility", Ui.Signed(_result.CredibilityDelta, "0.0")));
            grid.Add(Ui.Stat("Social reach", _result.SocialGained >= 0.1f ? "+" + _result.SocialGained.ToString("0.0") : "0"));
            grid.Add(Ui.Stat("Ad revenue", Ui.Money(_result.AdRevenue), "good"));
            if (_result.SponsorRevenue > 0)
                grid.Add(Ui.Stat("Sponsor", Ui.Money(_result.SponsorRevenue), "good"));
            grid.Add(Ui.Stat("Costs", Ui.Money(-_result.WeeklyCosts), "bad"));
            grid.Add(Ui.Stat("Net cash", Ui.Money(_result.MoneyDelta), _result.MoneyDelta >= 0 ? "good" : "bad"));
            deltas.Add(grid);
            if (_result.MonthlyWagesCharged > 0)
                deltas.Add(Ui.Wrapping($"Monthly wages of {Ui.Money(_result.MonthlyWagesCharged)} came out this week.", "body", "dim"));
            if (_result.Topic.SourceThread != null)
            {
                string steer = _result.Quality >= 1.05f
                    ? "A strong episode — you've nudged the story your way."
                    : _result.Quality >= 0.8f
                        ? "You covered it, which keeps you in the conversation."
                        : "A thin episode on a story people care about — that won't have helped.";
                deltas.Add(Ui.Wrapping($"Story: \"{_result.Topic.Name}\". {steer}", "body", "dim"));
            }
            scroll.Add(deltas);

            // --- new totals ---
            var totals = Ui.Box("panel");
            totals.Add(Ui.Text("Where things stand", "h2"));
            var tgrid = Ui.Box("statstrip");
            tgrid.Add(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null));
            tgrid.Add(Ui.Stat("Listeners", st.Listeners.ToString("N0")));
            tgrid.Add(Ui.Stat("Loyalty", st.LoyaltyLabel));
            tgrid.Add(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString()));
            tgrid.Add(Ui.Stat("Credibility", Mathf.RoundToInt(st.Credibility).ToString()));
            tgrid.Add(Ui.Stat("Social reach", Mathf.RoundToInt(st.SocialReach).ToString()));
            totals.Add(tgrid);

            var next = _host.Engine.Calendar.FixtureForTurn(st.SeasonTurn);
            string nextLine = next == null ? "Season complete."
                : next.IsInternationalBreak ? "Next week: international break."
                : next.IsOffseason ? "Next: the offseason."
                : $"Next: {(next.Home ? "home" : "away")} to {next.Opponent}.";
            totals.Add(Ui.Wrapping(nextLine, "body", "dim"));

            if (st.Money < _host.Engine.Config.BankruptcyFloor && !st.IsGameOver)
                totals.Add(Ui.Wrapping(
                    $"You're in the red. {_host.Engine.Config.BankruptcyGraceWeeks - st.ConsecutiveWeeksInDebt} more week(s) below €{_host.Engine.Config.BankruptcyFloor:0} ends the run.",
                    "body", "bad"));
            scroll.Add(totals);

            var cont = Ui.Btn(st.IsGameOver ? "See how it ended" : "On to next week", _host.ContinueFromResults, "btn-primary");
            _host.Theme.PaintPrimaryButton(cont);
            cont.style.marginTop = 6;
            scroll.Add(cont);

            return screen;
        }
    }
}
