using System;
using System.Linq;
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
            engine.SponsorResolved += n => Debug.Log($"  [{tag}] 💷 {n.Headline} — {n.Body}");

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

                if (engine.Scoops.Pending != null)
                    engine.ResolveScoop(greedy ? ScoopChoice.BreakNow : ScoopChoice.VerifyHold);

                if (engine.State.BuyoutPending)
                {
                    if (greedy) engine.AcceptBuyout(); else engine.DeclineBuyout();
                }

                // A single, global brake on every recurring commitment: don't take on more monthly
                // burden than ~6 months of current cash can carry. Monthly wages/upkeep land in one
                // lump every 4 weeks, so over-committing across several tracks/crew at once is what
                // actually causes a bankruptcy, not any one purchase in isolation.
                float CurrentMonthlyTotal()
                {
                    var st2 = engine.State;
                    float total = st2.HasCoHost ? engine.Config.CoHostMonthlyWage + st2.CoHostWageBump : 0f;
                    foreach (var c in st2.Employed.Values) total += c.Wage;
                    total += UpgradeCatalog.TotalMonthlyUpkeep(st2);
                    return total;
                }
                bool RoomForMoreMonthly(float extraMonthly) => engine.State.Money > (CurrentMonthlyTotal() + extraMonthly) * 6f;

                // Hire crew (pick the most cost-effective affordable candidate) once there's room.
                void HireBest(Crew role, float minMoney)
                {
                    if (engine.State.HasCrew(role) || engine.State.Money <= minMoney) return;
                    var pool = engine.CandidatesFor(role);
                    int best = -1; float bestValue = -1f;
                    for (int k = 0; k < pool.Count; k++)
                    {
                        var c = pool[k];
                        if (!engine.CanHireCandidate(role, k) || !RoomForMoreMonthly(c.Wage)) continue;
                        float value = c.Skill / Math.Max(1, c.Wage);   // skill per euro
                        if (value > bestValue) { best = k; bestValue = value; }
                    }
                    if (best >= 0) engine.HireCandidate(role, best);
                }
                HireBest(Crew.Producer, 900f);
                HireBest(Crew.Researcher, 900f);
                HireBest(Crew.Clips, 1200f);
                HireBest(Crew.Booker, 1500f);
                {
                    var studioNext = engine.NextUpgrade(UpgradeTrack.Studio);
                    if (studioNext != null && engine.State.Money > studioNext.Cost && RoomForMoreMonthly(studioNext.Monthly)
                        && engine.CanBuyUpgrade(UpgradeTrack.Studio))
                        engine.BuyUpgrade(UpgradeTrack.Studio);
                }
                if (engine.State.Money > 2000 && engine.CanBuySecondSponsorSlot()) engine.BuySecondSponsorSlot();

                // Play any one-shot cards that clearly help; keep permanents.
                foreach (var id in engine.State.Hand.ToArray())
                {
                    var card = CardManager.Get(id);
                    if (card != null && card.Kind == CardKind.Permanent) engine.PlayCard(id);
                }
                if (engine.Cards.CanDrawRandomCard(engine.State) && engine.State.Money > 900) engine.DrawRandomCard();
                foreach (var contact in CardManager.Contacts)
                    if (engine.Cards.CanSlotContact(engine.State, contact.Id) && engine.State.Money > 1500) { engine.SlotContact(contact.Id); break; }

                if (engine.Sponsors.Active == null && engine.Sponsors.Inbox.Count > 0)
                {
                    // greedy grabs the biggest weekly; balanced takes the safest (index 0).
                    int best = 0;
                    if (greedy)
                        for (int k = 1; k < engine.Sponsors.Inbox.Count; k++)
                            if (engine.Sponsors.Inbox[k].Weekly > engine.Sponsors.Inbox[best].Weekly) best = k;
                    engine.SignSponsor(best);
                }

                // Buy the first rung of each track when comfortably affordable; only push past tier 1
                // once there's a real cash buffer, since every tier past the first adds to the bill.
                bool CanAffordUpgrade(UpgradeTrack t, float minMoney)
                {
                    var next = engine.NextUpgrade(t);
                    if (next == null) return false;
                    return engine.State.Money > minMoney && RoomForMoreMonthly(next.Monthly) && engine.CanBuyUpgrade(t);
                }
                if (CanAffordUpgrade(UpgradeTrack.Set, 350f)) engine.BuyUpgrade(UpgradeTrack.Set);
                if (CanAffordUpgrade(UpgradeTrack.Audio, 450f)) engine.BuyUpgrade(UpgradeTrack.Audio);
                if (CanAffordUpgrade(UpgradeTrack.Post, 450f)) engine.BuyUpgrade(UpgradeTrack.Post);
                if (CanAffordUpgrade(UpgradeTrack.Distribution, 800f)) engine.BuyUpgrade(UpgradeTrack.Distribution);
                if (engine.State.Money > 650 && RoomForMoreMonthly(engine.Config.CoHostMonthlyWage) && engine.CanHireCoHost())
                    engine.HireCoHost();

                var plan = BuildRundown(engine, ctx, greedy, out Topic pick);

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
                          $"{st.Listeners:N0} listeners ({st.LoyaltyLabel}, {st.Followers:N0} followers, peak {st.PeakListeners:N0}), " +
                          $"€{st.Money:0}, rep {st.Reputation:0}, cred {st.Credibility:0}, social {st.SocialReach:0}, " +
                          $"access T{st.AccessTier}, {st.CupsWon} cup(s), {st.EuropeanTrophies} euro, " +
                          $"{events} events, {st.ScoopsBroken} scoops broken, goal {(st.GoalReached ? "REACHED" : "not reached")}, gameOver={st.IsGameOver}");

            Debug.Log("[Headless]\n" + sb);
        }

        static float Score(Topic t, WeekContext ctx, bool greedy)
        {
            float score = t.BaseAppeal * ContextResolver.AppealMultiplier(t.Response, ctx);
            if (t.SourceThread != null) score += 0.5f;
            if (!greedy)
            {
                score += t.RepEarn * 0.25f + t.CredHook * 0.2f;
                if (t.Response == TopicResponse.Crisis && ctx.Surprise > Surprise.Poor) score -= 1.5f;
            }
            return score;
        }

        // Build a three-segment rundown (spec §7): a main story, a second segment, a cheap
        // recurring bit, with angles that fit the strategy.
        static ProductionPlan BuildRundown(Engine engine, WeekContext ctx, bool greedy, out Topic headline)
        {
            var st = engine.State;
            int cap = st.PrepCapacity(engine.Config);
            var offer = engine.Offer.OrderByDescending(t => Score(t, ctx, greedy)).ToList();

            var plan = new ProductionPlan();
            Topic main = offer.FirstOrDefault();
            headline = main;
            if (main == null) return plan;

            // Vary the main angle week to week so freshness doesn't crater.
            Angle[] balancedRotation = { Angle.Analysis, Angle.Emotional, Angle.Analysis, Angle.Investigation };
            plan.Main.Set(main, main.SourceThread != null ? (main.SourceThread.Momentum >= 0f ? 1 : -1) : 0);
            if (greedy)
                plan.Main.Angle = (ctx.Surprise <= Surprise.Poor || st.Reputation >= 15f) ? Angle.HotTake : Angle.Emotional;
            else
            {
                var want = balancedRotation[st.GlobalWeek % balancedRotation.Length];
                plan.Main.Angle = AngleCatalog.Allowed(want, st) ? want : Angle.Analysis;
            }

            Topic second = offer.Skip(1).FirstOrDefault(t => !ReferenceEquals(t, main) && t.Family != main.Family)
                           ?? offer.Skip(1).FirstOrDefault(t => !ReferenceEquals(t, main));
            if (second != null)
            {
                plan.Second.Set(second, second.SourceThread != null ? (second.SourceThread.Momentum >= 0f ? 1 : -1) : 0);
                plan.Second.Angle = greedy ? Angle.Emotional : Angle.Analysis;
            }

            Topic rec = offer.FirstOrDefault(t => t.Id == TopicId.Mailbag || t.Id == TopicId.TierList || t.Id == TopicId.Explainer)
                        ?? offer.FirstOrDefault(t => !ReferenceEquals(t, main) && !ReferenceEquals(t, second));
            if (rec != null && !ReferenceEquals(rec, main) && !ReferenceEquals(rec, second))
            {
                plan.Recurring.Set(rec);
                // Comedy only when the main isn't a crisis take (avoid tonal whiplash).
                bool crisisMain = plan.Main.Resolved.Response == TopicResponse.Crisis;
                plan.Recurring.Angle = (!crisisMain && st.GlobalWeek % 3 == 0) ? Angle.Comedy : Angle.Analysis;
            }

            // Meet each segment's effort, then spread whatever's left across the levers.
            int relief = CrewCatalog.EffortRelief(st);
            foreach (var seg in plan.FilledSlots)
                seg.Prep = Math.Max(1, seg.Resolved.Effort - relief);
            int left = cap - plan.TopicPrep;
            plan.PrepResearch = Mathf.Clamp(left / 3, 0, 4);
            left -= plan.PrepResearch;
            plan.PrepAudio = Mathf.Clamp(left / 2, 0, 4);
            left -= plan.PrepAudio;
            plan.PrepPromo = Mathf.Max(0, left);

            // If we overspent on topics, trim the recurring bit first.
            while (plan.TotalPrep > cap && plan.Recurring.Prep > 0) plan.Recurring.Prep--;
            while (plan.TotalPrep > cap && plan.Second.Prep > 0) plan.Second.Prep--;
            while (plan.TotalPrep > cap && plan.Main.Prep > 1) plan.Main.Prep--;
            return plan;
        }
    }
}
