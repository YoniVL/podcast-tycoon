using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public sealed class BeatOption
    {
        public string Label;
        public string Outcome;
        public Action<Engine, ProductionPlan> Apply;
    }

    /// <summary>A quick in-the-room situation during the recording phase (spec §10).</summary>
    public sealed class RecordingBeat
    {
        public string Id;
        public string Prompt;
        public string Detail;
        public Func<Engine, ProductionPlan, WeekContext, bool> CanFire;
        public readonly List<BeatOption> Options = new List<BeatOption>();
    }

    /// <summary>
    /// The recording phase (spec §10): after the rundown is built, 2–3 quick beats can arise
    /// from what you planned — the angles, the push, a live thread, the match context. Each is
    /// an immediate choice that nudges this episode via the same per-week modifiers cards use.
    /// A smooth week (nothing fires) gets a small "clean show" quality bump instead.
    /// </summary>
    public static class RecordingBeats
    {
        public static readonly IReadOnlyList<RecordingBeat> Pool = Build();

        static List<RecordingBeat> Build()
        {
            void Soc(Engine e, float d) => e.State.SocialReach = MathX.Clamp(e.State.SocialReach + d, 0f, 100f);
            void Cred(Engine e, float d) => e.State.Credibility = MathX.Clamp(e.State.Credibility + d, 0f, 100f);
            void Rep(Engine e, float d) => e.State.Reputation = MathX.Clamp(e.State.Reputation + d, 0f, 100f);

            return new List<RecordingBeat>
            {
                new RecordingBeat
                {
                    Id = "cohost_tangent",
                    Prompt = "Your co-host goes off on a tangent",
                    Detail = "Halfway through, they're ranting about something that has nothing to do with the topic.",
                    CanFire = (e, p, c) => e.State.HasCoHost
                        && p.FilledSlots.Any(s => s.Angle == Angle.HotTake || s.Angle == Angle.Comedy),
                    Options =
                    {
                        new BeatOption { Label = "Rein it in", Outcome = "You pulled it back on topic. Safe, on-message.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 0.97f },
                        new BeatOption { Label = "Let it ride", Outcome = "You let them go. Chaotic, but it's a moment people will talk about.",
                            Apply = (e, p) => { Soc(e, 4f); Rep(e, -1f); } }
                    }
                },
                new RecordingBeat
                {
                    Id = "thin_on_facts",
                    Prompt = "You realise you're thin on the facts",
                    Detail = "Mid hot take, and you're not actually sure you've got this one right.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Angle == Angle.HotTake) && p.PrepResearch < 2,
                    Options =
                    {
                        new BeatOption { Label = "Walk it back on air", Outcome = "You caught yourself and softened it — sounded a little less sure of itself.",
                            Apply = (e, p) => { Rep(e, 1f); e.State.CardReachMultThisWeek *= 0.95f; } },
                        new BeatOption { Label = "Commit anyway", Outcome = "You said it like you meant it.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "audio_glitch",
                    Prompt = "The audio glitches on the best ten minutes",
                    Detail = "The recording hiccupped right through your strongest stretch.",
                    CanFire = (e, p, c) => !UpgradeCatalog.Current(e.State, UpgradeTrack.Audio).AudioGlitchProof,
                    Options =
                    {
                        new BeatOption { Label = "Re-record it", Outcome = "You did it again. Took the edge off, but it's clean.",
                            Apply = (e, p) => e.State.CardQualityBonusThisWeek -= 0.03f },
                        new BeatOption { Label = "Ship it rough", Outcome = "You put it out warts and all.",
                            Apply = (e, p) => e.State.CardQualityBonusThisWeek -= 0.08f }
                    }
                },
                new RecordingBeat
                {
                    Id = "great_bit",
                    Prompt = "A genuinely great bit happens",
                    Detail = "One of you says something properly funny. This is clip material.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Angle == Angle.Comedy),
                    Options =
                    {
                        new BeatOption { Label = "Clip it", Outcome = "Cut, posted, doing numbers already.",
                            Apply = (e, p) => Soc(e, 5f) },
                        new BeatOption { Label = "Move on", Outcome = "You kept going — might come back to it.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "callout",
                    Prompt = "The take basically requires naming someone",
                    Detail = "You can make the point in general terms, or say exactly who you mean.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Resolved.Family == TopicFamily.Drama),
                    Options =
                    {
                        new BeatOption { Label = "Name them", Outcome = "You said exactly who you meant. It travels.",
                            Apply = (e, p) => { Soc(e, 4f); Cred(e, -1.5f); } },
                        new BeatOption { Label = "Keep it vague", Outcome = "You made the point without the name.",
                            Apply = (e, p) => Cred(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "emotional_moment",
                    Prompt = "It's still sinking in",
                    Detail = "Whatever happened this week, it's clearly still raw for both of you.",
                    CanFire = (e, p, c) => c.Match != null && (c.Surprise == Surprise.Heroic || c.Surprise == Surprise.Disaster),
                    Options =
                    {
                        new BeatOption { Label = "Lean into it", Outcome = "You let the emotion carry the episode.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 1.05f },
                        new BeatOption { Label = "Stay analytical", Outcome = "You kept a level head through it.",
                            Apply = (e, p) => Cred(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "correction_owed",
                    Prompt = "Last week's take hasn't aged well",
                    Detail = "People are still bringing it up in the replies.",
                    CanFire = (e, p, c) => e.State.PendingCorrection,
                    Options =
                    {
                        new BeatOption { Label = "Own it, on air", Outcome = "You addressed it head-on. That lands well.",
                            Apply = (e, p) => Cred(e, 3f) },
                        new BeatOption { Label = "Brush past it", Outcome = "You moved on without dwelling on it.",
                            Apply = (e, p) => Cred(e, -1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "sponsor_read",
                    Prompt = "Time for the sponsor read",
                    Detail = "You can sell it hard, or keep it light and move on.",
                    CanFire = (e, p, c) => e.Sponsors.ActiveDeals.Count > 0,
                    Options =
                    {
                        new BeatOption { Label = "Read it hard", Outcome = "You gave it the full pitch. The sponsor will love it.",
                            Apply = (e, p) => e.State.SponsorAppealPenalty -= 0.03f },
                        new BeatOption { Label = "Keep it light", Outcome = "A quick, breezy mention and back to the show.",
                            Apply = (e, p) => { } }
                    }
                }
            };
        }

        /// <summary>Pick which beats fire this week. Usually 0-3; push and chaos raise the odds.</summary>
        public static List<RecordingBeat> Select(Engine e, ProductionPlan plan, WeekContext ctx, IRng rng)
        {
            var eligible = Pool.Where(b => b.CanFire(e, plan, ctx)).ToList();
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (eligible[i], eligible[j]) = (eligible[j], eligible[i]);
            }

            int target = rng.Range(0, 4);   // 0..3, leaning toward a couple of beats most weeks
            if (e.State.Modifiers.ChaosCycle) target = Math.Min(target + 2, eligible.Count);
            if (plan.Push >= 4) target += 1;
            if (CrewCatalog.AnyEmployeeHasTrait(e.State, "loose_cannon")) target += 1;   // more happens when they're in the room
            target = Math.Min(target, eligible.Count);

            return eligible.Take(target).ToList();
        }
    }
}
