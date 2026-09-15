using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public sealed class BeatOption
    {
        public string Label;
        /// <summary>Shown on the option before you pick it — the real upside/downside, so
        /// this isn't a blind flavour-text choice.</summary>
        public string Effect;
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
            void Fresh(Engine e, float d) => e.State.Freshness = MathX.Clamp(e.State.Freshness + d, 0f, 100f);

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
                        new BeatOption { Label = "Rein it in", Effect = "Slightly smaller reach this episode.",
                            Outcome = "You pulled it back on topic. Safe, on-message.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 0.97f },
                        new BeatOption { Label = "Let it ride", Effect = "+4 social reach, −1 reputation.",
                            Outcome = "You let them go. Chaotic, but it's a moment people will talk about.",
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
                        new BeatOption { Label = "Walk it back on air", Effect = "+1 reputation, slightly smaller reach.",
                            Outcome = "You caught yourself and softened it — sounded a little less sure of itself.",
                            Apply = (e, p) => { Rep(e, 1f); e.State.CardReachMultThisWeek *= 0.95f; } },
                        new BeatOption { Label = "Commit anyway", Effect = "No effect — you got away with it (for now).",
                            Outcome = "You said it like you meant it.",
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
                        new BeatOption { Label = "Re-record it", Effect = "−€25 studio time, small quality hit.",
                            Outcome = "You did it again. Took the edge off, but it's clean.",
                            Apply = (e, p) => { e.State.Money -= 25f; e.State.CardQualityBonusThisWeek -= 0.03f; } },
                        new BeatOption { Label = "Ship it rough", Effect = "Free, but a bigger quality hit.",
                            Outcome = "You put it out warts and all.",
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
                        new BeatOption { Label = "Clip it", Effect = "+5 social reach.",
                            Outcome = "Cut, posted, doing numbers already.",
                            Apply = (e, p) => Soc(e, 5f) },
                        new BeatOption { Label = "Move on", Effect = "No effect — the moment passes.",
                            Outcome = "You kept going — might come back to it.",
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
                        new BeatOption { Label = "Name them", Effect = "+4 social reach, −1.5 credibility.",
                            Outcome = "You said exactly who you meant. It travels.",
                            Apply = (e, p) => { Soc(e, 4f); Cred(e, -1.5f); } },
                        new BeatOption { Label = "Keep it vague", Effect = "+1 credibility.",
                            Outcome = "You made the point without the name.",
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
                        new BeatOption { Label = "Lean into it", Effect = "Slightly bigger reach this episode.",
                            Outcome = "You let the emotion carry the episode.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 1.05f },
                        new BeatOption { Label = "Stay analytical", Effect = "+1 credibility.",
                            Outcome = "You kept a level head through it.",
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
                        new BeatOption { Label = "Own it, on air", Effect = "+3 credibility.",
                            Outcome = "You addressed it head-on. That lands well.",
                            Apply = (e, p) => Cred(e, 3f) },
                        new BeatOption { Label = "Brush past it", Effect = "−1 credibility.",
                            Outcome = "You moved on without dwelling on it.",
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
                        new BeatOption { Label = "Read it hard", Effect = "Small appeal hit this episode — the sponsor gets the full pitch.",
                            Outcome = "You gave it the full pitch. The sponsor will love it.",
                            Apply = (e, p) => e.State.SponsorAppealPenalty -= 0.03f },
                        new BeatOption { Label = "Keep it light", Effect = "No effect.",
                            Outcome = "A quick, breezy mention and back to the show.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "guest_runs_long",
                    Prompt = "Your guest won't stop talking",
                    Detail = "Great story, wrong length — you're well past when this was meant to wrap.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Guest),
                    Options =
                    {
                        new BeatOption { Label = "Cut them off", Effect = "+1 credibility, slightly smaller reach.",
                            Outcome = "You wrapped it politely and moved on.",
                            Apply = (e, p) => { Cred(e, 1f); e.State.CardReachMultThisWeek *= 0.97f; } },
                        new BeatOption { Label = "Let them finish", Effect = "+3 social reach, small quality hit.",
                            Outcome = "You let it run. Rambling, but genuinely good listening.",
                            Apply = (e, p) => { Soc(e, 3f); e.State.CardQualityBonusThisWeek -= 0.03f; } }
                    }
                },
                new RecordingBeat
                {
                    Id = "technical_question",
                    Prompt = "A genuinely sharp question comes up",
                    Detail = "Mid-investigation, and you realise you can actually answer this one properly.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Angle == Angle.Investigation),
                    Options =
                    {
                        new BeatOption { Label = "Answer it live", Effect = "+2 credibility.",
                            Outcome = "You went deep on it — exactly the kind of thing that builds trust.",
                            Apply = (e, p) => Cred(e, 2f) },
                        new BeatOption { Label = "Save it for next week", Effect = "No effect — safe, but a missed chance.",
                            Outcome = "You parked it for a future episode.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "trending_topic",
                    Prompt = "The story you're covering just moved",
                    Detail = "Something broke on this exact thread while you were recording.",
                    CanFire = (e, p, c) => p.FilledSlots.Any(s => s.Resolved.SourceThread != null),
                    Options =
                    {
                        new BeatOption { Label = "Address it live", Effect = "−€10 to verify it fast, +3 social reach, +1 credibility.",
                            Outcome = "You checked it out and worked it in — right on the news.",
                            Apply = (e, p) => { e.State.Money -= 10f; Soc(e, 3f); Cred(e, 1f); } },
                        new BeatOption { Label = "Stick to the plan", Effect = "No effect.",
                            Outcome = "You left it for next week's episode.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "cohost_disagrees",
                    Prompt = "You and your co-host clash on air",
                    Detail = "Turns out you don't actually agree on this one — and it's showing.",
                    CanFire = (e, p, c) => e.State.HasCoHost,
                    Options =
                    {
                        new BeatOption { Label = "Hash it out live", Effect = "+3 social reach, −2 morale.",
                            Outcome = "You argued it out properly. Great radio, tense room.",
                            Apply = (e, p) => { Soc(e, 3f); e.State.Morale = MathX.Clamp(e.State.Morale - 2f, 0f, 100f); } },
                        new BeatOption { Label = "Agree to disagree", Effect = "+1 credibility.",
                            Outcome = "You both aired your view and moved on cleanly.",
                            Apply = (e, p) => Cred(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "big_match_nerves",
                    Prompt = "There's real tension in the room",
                    Detail = "A fixture this big puts an edge on everything you say about it.",
                    CanFire = (e, p, c) => c.Fixture != null && (c.Importance == FixtureImportance.BigMatch
                        || c.Importance == FixtureImportance.Derby || c.Importance == FixtureImportance.Final),
                    Options =
                    {
                        new BeatOption { Label = "Lean into the hype", Effect = "Slightly bigger reach, −1 credibility.",
                            Outcome = "You matched the occasion. Loud, and it landed.",
                            Apply = (e, p) => { e.State.CardReachMultThisWeek *= 1.05f; Cred(e, -1f); } },
                        new BeatOption { Label = "Stay measured", Effect = "+1 credibility.",
                            Outcome = "You kept a level head while everyone else lost theirs.",
                            Apply = (e, p) => Cred(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "rival_mentioned",
                    Prompt = "Talk turns to your rival",
                    Detail = "You can't get through this one without bringing them up.",
                    CanFire = (e, p, c) => c.IsRivalMatch,
                    Options =
                    {
                        new BeatOption { Label = "Needle them", Effect = "+3 social reach, −1 credibility.",
                            Outcome = "You couldn't resist. It travels — and they'll have seen it.",
                            Apply = (e, p) => { Soc(e, 3f); Cred(e, -1f); } },
                        new BeatOption { Label = "Stay classy", Effect = "+1 reputation.",
                            Outcome = "You made the point without the cheap shot.",
                            Apply = (e, p) => Rep(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "no_sponsor_joke",
                    Prompt = "Someone jokes about the empty ad slot",
                    Detail = "No sponsor to read for this week, and it's become a bit of a running gag.",
                    CanFire = (e, p, c) => e.Sponsors.ActiveDeals.Count == 0,
                    Options =
                    {
                        new BeatOption { Label = "Make light of it", Effect = "+2 social reach.",
                            Outcome = "You leaned into the bit — people always like the self-aware ones.",
                            Apply = (e, p) => Soc(e, 2f) },
                        new BeatOption { Label = "Don't dwell on it", Effect = "No effect.",
                            Outcome = "You moved straight past it.",
                            Apply = (e, p) => { } }
                    }
                },
                new RecordingBeat
                {
                    Id = "prep_pays_off",
                    Prompt = "The research really shows",
                    Detail = "All that prep is paying off — you're speaking with real authority on this one.",
                    CanFire = (e, p, c) => p.PrepResearch >= 4,
                    Options =
                    {
                        new BeatOption { Label = "Lean into the depth", Effect = "+2 credibility.",
                            Outcome = "You went further than planned — it clearly landed.",
                            Apply = (e, p) => Cred(e, 2f) },
                        new BeatOption { Label = "Keep it snappy", Effect = "+1 social reach.",
                            Outcome = "You kept the pace up instead — a punchier listen.",
                            Apply = (e, p) => Soc(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "under_prepared",
                    Prompt = "You're clearly winging part of this",
                    Detail = "Barely any prep went into this one and it's starting to show.",
                    CanFire = (e, p, c) => p.TotalPrep < e.State.PrepCapacity(e.Config) * 0.5f,
                    Options =
                    {
                        new BeatOption { Label = "Own the improv", Effect = "+2 social reach, −1 credibility.",
                            Outcome = "You made a bit out of not knowing — it actually worked.",
                            Apply = (e, p) => { Soc(e, 2f); Cred(e, -1f); } },
                        new BeatOption { Label = "Play it safe", Effect = "Slightly smaller reach this episode.",
                            Outcome = "You kept it vague rather than risk getting it wrong.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 0.95f }
                    }
                },
                new RecordingBeat
                {
                    Id = "editing_room_floor",
                    Prompt = "There's a tangent worth cutting",
                    Detail = "With the gear you've got now, you could tighten this up in post if you wanted.",
                    CanFire = (e, p, c) => UpgradeCatalog.TierOf(e.State, UpgradeTrack.Post) >= 2,
                    Options =
                    {
                        new BeatOption { Label = "Trim it down", Effect = "Small quality lift.",
                            Outcome = "You cut it clean in the edit — a tighter show for it.",
                            Apply = (e, p) => e.State.CardQualityBonusThisWeek += 0.03f },
                        new BeatOption { Label = "Keep the tangent in", Effect = "+2 social reach.",
                            Outcome = "You left it in. Rough round the edges, but people quote it.",
                            Apply = (e, p) => Soc(e, 2f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "freshness_low_banter",
                    Prompt = "You can feel yourselves repeating old bits",
                    Detail = "This is starting to sound like every other week.",
                    CanFire = (e, p, c) => e.State.Freshness < 45f,
                    Options =
                    {
                        new BeatOption { Label = "Force a new angle", Effect = "+8 freshness, small quality hit.",
                            Outcome = "You pushed yourselves somewhere new. A bit clunky, but it's different.",
                            Apply = (e, p) => { Fresh(e, 8f); e.State.CardQualityBonusThisWeek -= 0.02f; } },
                        new BeatOption { Label = "Lean on the classic", Effect = "Slightly bigger reach, −3 freshness.",
                            Outcome = "You leaned into the bit that always works. It does, again.",
                            Apply = (e, p) => { e.State.CardReachMultThisWeek *= 1.03f; Fresh(e, -3f); } }
                    }
                },
                new RecordingBeat
                {
                    Id = "morale_lifts",
                    Prompt = "Everyone's in a great mood",
                    Detail = "The room's buzzing today, and it's coming through on the mic.",
                    CanFire = (e, p, c) => e.State.Morale >= 80f,
                    Options =
                    {
                        new BeatOption { Label = "Ride the energy", Effect = "Slightly bigger reach.",
                            Outcome = "You let the good mood carry the episode.",
                            Apply = (e, p) => e.State.CardReachMultThisWeek *= 1.05f },
                        new BeatOption { Label = "Stay focused", Effect = "+1 credibility.",
                            Outcome = "You kept the energy but stayed on message.",
                            Apply = (e, p) => Cred(e, 1f) }
                    }
                },
                new RecordingBeat
                {
                    Id = "injury_news_breaks",
                    Prompt = "News breaks about a player mid-recording",
                    Detail = "Your phone lights up with an update while you're live.",
                    CanFire = (e, p, c) => c.KeyPlayersOut > 0,
                    Options =
                    {
                        new BeatOption { Label = "Break in with the update", Effect = "+2 social reach.",
                            Outcome = "You caught it live — good instincts, good timing.",
                            Apply = (e, p) => Soc(e, 2f) },
                        new BeatOption { Label = "Save it for after", Effect = "+1 credibility.",
                            Outcome = "You didn't want to report something half-confirmed.",
                            Apply = (e, p) => Cred(e, 1f) }
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
