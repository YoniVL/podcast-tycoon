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
            scroll.Add(Ui.Wrapping($"{_result.QualityLabel} episode — quality {_result.Quality:0.00}.", "body"));

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

            // --- what changed ---
            var deltas = Ui.Box("panel");
            deltas.Add(Ui.Text("What changed", "h2"));
            var grid = Ui.Box("statstrip");
            grid.Add(Ui.Stat("Listeners", Ui.Signed(_result.ListenerDeltaActual),
                _result.ListenerDeltaActual >= 0 ? "good" : "bad"));
            grid.Add(Ui.Stat("Reputation", Ui.Signed(_result.ReputationDelta, "0.0")));
            grid.Add(Ui.Stat("Buzz", _result.BuzzGained > 0 ? "+" + _result.BuzzGained : "0"));
            grid.Add(Ui.Stat("Ad revenue", Ui.Money(_result.AdRevenue), "good"));
            grid.Add(Ui.Stat("Costs", Ui.Money(-_result.WeeklyCosts), "bad"));
            grid.Add(Ui.Stat("Net cash", Ui.Money(_result.MoneyDelta), _result.MoneyDelta >= 0 ? "good" : "bad"));
            deltas.Add(grid);
            if (_result.MonthlyWagesCharged > 0)
                deltas.Add(Ui.Wrapping($"Monthly wages of {Ui.Money(_result.MonthlyWagesCharged)} came out this week.", "body", "dim"));
            scroll.Add(deltas);

            // --- new totals ---
            var totals = Ui.Box("panel");
            totals.Add(Ui.Text("Where things stand", "h2"));
            var tgrid = Ui.Box("statstrip");
            tgrid.Add(Ui.Stat("Money", Ui.Money(st.Money), st.Money < 0 ? "bad" : null));
            tgrid.Add(Ui.Stat("Listeners", st.Listeners.ToString("N0")));
            tgrid.Add(Ui.Stat("Reputation", Mathf.RoundToInt(st.Reputation).ToString()));
            tgrid.Add(Ui.Stat("Buzz", st.Buzz.ToString()));
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
