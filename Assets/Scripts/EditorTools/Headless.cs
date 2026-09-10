using System;
using System.Text;
using PodcastTycoon.Core;
using UnityEditor;
using UnityEngine;

namespace PodcastTycoon.EditorTools
{
    /// <summary>
    /// Drives the Core engine with a couple of scripted strategies for several seasons and
    /// logs the weekly state. Proves the loop end-to-end and gives a read on balance.
    /// Headless: -executeMethod PodcastTycoon.EditorTools.Headless.SimulateSeasons
    /// </summary>
    public static class Headless
    {
        [MenuItem("Podcast Tycoon/Smoke test — simulate strategies")]
        public static void SimulateSeasons()
        {
            RunStrategy("GREEDY  (always the biggest-appeal topic, hot takes and all)", greedy: true);
            RunStrategy("BALANCED (leans on reputation topics, only rants in a real crisis)", greedy: false);

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static void RunStrategy(string label, bool greedy)
        {
            var engine = new Engine(
                new RunSetup { PodcastName = "Smoke FM", ClubName = "Testford", Difficulty = Difficulty.Regular },
                new GameConfig(),
                new SystemRng(20260910));

            string tag = greedy ? "greedy" : "balanced";
            engine.MilestoneReached += m => Debug.Log($"  [{tag}] ★ {m.Message}");
            int events = 0;
            engine.GameOver += r => Debug.Log($"  [{tag}] ✗ GAME OVER: {r}");
            engine.ThreadOpened += ev => Debug.Log($"  [{tag}] 📰 opened: {ev.Headline}");
            engine.ThreadResolved += ev => Debug.Log($"  [{tag}] 📰 resolved: {ev.Headline} — {ev.Body}");

            var sb = new StringBuilder();
            sb.AppendLine($"=== {label} ===");
            sb.AppendLine("wk  | fixture / result            | surprise | topic          | qual | Δlist  | listeners | money  | rep");

            int safety = 0;
            int peakSeason = 1;
            while (engine.State.Season <= 5 && !engine.State.IsGameOver && safety++ < 400)
            {
                var ctx = engine.BeginWeek();
                peakSeason = engine.State.Season;

                if (engine.Events.Pending != null)
                {
                    events++;
                    int n = engine.Events.Pending.Options.Count;
                    // greedy leans toward the middle option, balanced toward the last (usually the safe one)
                    engine.ResolveEvent(greedy ? Math.Min(1, n - 1) : n - 1);
                }

                // Buy gear when comfortably in the black.
                if (engine.State.Money > 450 && engine.CanBuy(Gear.XlrMic)) engine.BuyGear(Gear.XlrMic);
                if (engine.State.Money > 450 && engine.CanBuy(Gear.EditingSoftware)) engine.BuyGear(Gear.EditingSoftware);
                if (engine.State.Money > 350 && engine.CanBuy(Gear.AcousticPanels)) engine.BuyGear(Gear.AcousticPanels);
                if (engine.State.Money > 650 && engine.CanHireCoHost()) engine.HireCoHost();

                Topic pick = ChooseTopic(engine, ctx, greedy);
                var plan = Plan(engine, pick);

                string fixtureText;
                string surprise;
                if (ctx.IsMatchless)
                {
                    fixtureText = ctx.IsInternationalBreak ? "international break" : "offseason";
                    surprise = "-";
                }
                else
                {
                    var m = ctx.Match;
                    fixtureText = $"{(ctx.Fixture.Home ? "H" : "A")} v {ctx.Fixture.Opponent,-18} {m.ScoreLine}";
                    surprise = ctx.Surprise.ToString();
                }

                foreach (var n in ctx.SquadNews)
                    sb.AppendLine($"      · {n}");

                var result = engine.Publish(plan);

                if (engine.State.GlobalWeek % 3 == 0 || Math.Abs(result.ListenerDeltaActual) > 1500 || ctx.KeyPlayersOut > 0)
                    sb.AppendLine(
                        $"{ctx.GlobalWeek,3} | {fixtureText,-27} | {surprise,-8} | {pick.Name,-14} | " +
                        $"{result.Quality,4:0.00} | {result.ListenerDeltaActual,6} | {engine.State.Listeners,9:N0} | " +
                        $"{engine.State.Money,6:0} | {engine.State.Reputation,3:0}" +
                        (ctx.KeyPlayersOut > 0 ? $"  [{ctx.KeyPlayersOut} key out]" : ""));
            }

            var st = engine.State;
            sb.AppendLine($"--> after {st.EpisodesPublished} episodes / {peakSeason} seasons: " +
                          $"{st.Listeners:N0} listeners (peak {st.PeakListeners:N0}), €{st.Money:0}, rep {st.Reputation:0}, " +
                          $"{events} events, goal {(st.GoalReached ? "REACHED" : "not reached")}, gameOver={st.IsGameOver}");

            Debug.Log("[Headless]\n" + sb);
        }

        static Topic ChooseTopic(Engine engine, WeekContext ctx, bool greedy)
        {
            Topic best = null;
            float bestScore = float.MinValue;
            foreach (var t in engine.Offer)
            {
                float appeal = t.BaseAppeal * ContextResolver.AppealMultiplier(t.Response, ctx);
                float score = appeal;
                if (t.SourceThread != null) score += 0.5f; // a running story is worth covering
                if (!greedy)
                {
                    // Value reputation; only pick a hot take when the week is genuinely bad.
                    score += t.RepEarn * 0.25f;
                    if (t.Response == TopicResponse.Crisis && ctx.Surprise > Surprise.Poor)
                        score -= 1.5f;
                }
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        static ProductionPlan Plan(Engine engine, Topic pick)
        {
            int cap = engine.State.PrepCapacity(engine.Config);
            // On a story, back whichever way it's already trending.
            int stance = pick.SourceThread != null ? (pick.SourceThread.Momentum >= 0f ? 1 : -1) : 0;
            var plan = ProductionPlan.Cover(pick, stance);
            plan.PrepTopic = Mathf.Min(pick.Effort + 1, cap);
            int left = cap - plan.PrepTopic;
            plan.PrepResearch = Mathf.Clamp(left / 3, 0, 4);
            left -= plan.PrepResearch;
            plan.PrepAudio = Mathf.Clamp(left / 2, 0, 4);
            left -= plan.PrepAudio;
            plan.PrepPromo = Mathf.Max(0, left);
            return plan;
        }
    }
}
