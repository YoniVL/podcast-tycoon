using System.Text;
using PodcastTycoon.Core;
using UnityEditor;
using UnityEngine;

namespace PodcastTycoon.EditorTools
{
    /// <summary>
    /// Drives the Core engine with a simple scripted strategy for a few seasons and logs the
    /// weekly state. Proves the loop end-to-end without any UI.
    /// Headless: -executeMethod PodcastTycoon.EditorTools.Headless.SimulateSeasons
    /// </summary>
    public static class Headless
    {
        [MenuItem("Podcast Tycoon/Smoke test — simulate 2 seasons")]
        public static void SimulateSeasons()
        {
            var engine = new Engine(
                new RunSetup { PodcastName = "Smoke FM", ClubName = "Testford", Difficulty = Difficulty.Regular },
                new GameConfig(),
                new SystemRng(20260910));

            engine.MilestoneReached += m => Debug.Log($"  ★ milestone: {m.Message}");
            engine.GameOver += r => Debug.Log($"  ✗ GAME OVER: {r}");
            engine.SeasonRolledOver += s => Debug.Log($"  — season {s} begins —");

            var sb = new StringBuilder();
            sb.AppendLine("turn | wk | fixture / result           | surprise | topic       | qual | Δlist | listeners | money | rep");

            int safety = 0;
            while (engine.State.Season <= 2 && !engine.State.IsGameOver && safety++ < 200)
            {
                var ctx = engine.BeginWeek();

                // Scripted "decent host": pick the highest context-appeal topic, meet its effort,
                // spend the rest on research/audio, a little on promo. Buy gear when flush.
                if (engine.State.Money > 500 && engine.CanBuy(Gear.XlrMic)) engine.BuyGear(Gear.XlrMic);
                if (engine.State.Money > 500 && engine.CanBuy(Gear.EditingSoftware)) engine.BuyGear(Gear.EditingSoftware);
                if (engine.State.Money > 700 && engine.CanHireCoHost()) engine.HireCoHost();

                Topic best = null;
                float bestScore = float.MinValue;
                foreach (var t in engine.Offer)
                {
                    float score = t.BaseAppeal * ContextResolver.AppealMultiplier(t.Response, ctx);
                    if (score > bestScore) { bestScore = score; best = t; }
                }

                int cap = engine.State.PrepCapacity(engine.Config);
                var plan = new ProductionPlan { Topic = best.Id };
                plan.PrepTopic = Mathf.Min(best.Effort, cap);
                int left = cap - plan.PrepTopic;
                plan.PrepResearch = Mathf.Clamp(left / 3, 0, 4);
                left -= plan.PrepResearch;
                plan.PrepAudio = Mathf.Clamp(left / 2, 0, 4);
                left -= plan.PrepAudio;
                plan.PrepPromo = Mathf.Max(0, left);

                string fixtureText;
                string surprise;
                if (ctx.IsMatchless)
                {
                    fixtureText = ctx.IsInternationalBreak ? "int'l break" : "offseason";
                    surprise = "-";
                }
                else
                {
                    var m = ctx.Match;
                    fixtureText = $"{(ctx.Fixture.Home ? "H" : "A")} v {ctx.Fixture.Opponent,-16} {m.ScoreLine}";
                    surprise = ctx.Surprise.ToString();
                }

                var preview = engine.Preview(plan);
                var result = engine.Publish(plan);

                sb.AppendLine(
                    $"{ctx.Turn,4} | {ctx.GlobalWeek,2} | {fixtureText,-26} | {surprise,-8} | {best.Name,-11} | " +
                    $"{result.Quality,4:0.00} | {result.ListenerDeltaActual,5} | {engine.State.Listeners,9:N0} | " +
                    $"{engine.State.Money,6:0} | {engine.State.Reputation,3:0}");
            }

            Debug.Log("[Headless] Simulation:\n" + sb);
            Debug.Log($"[Headless] Finished: season {engine.State.Season}, week {engine.State.GlobalWeek}, " +
                      $"{engine.State.Listeners:N0} listeners, €{engine.State.Money:0}, gameOver={engine.State.IsGameOver}");

            if (Application.isBatchMode)
                EditorApplication.Exit(engine.State.IsGameOver ? 1 : 0);
        }
    }
}
