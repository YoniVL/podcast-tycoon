using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public enum ScoopChoice { BreakNow, VerifyHold, Trade }

    public sealed class Scoop
    {
        public string Headline;
        public string Detail;
        public string Subject;   // for the broken-scoop topic name
    }

    /// <summary>
    /// At access tier 2+ you periodically get advance word on a transfer / manager / contract
    /// decision (spec §14). Break it now for numbers and risk, verify & hold for trust, or
    /// trade it for cash and a favour.
    /// </summary>
    public sealed class ScoopManager
    {
        readonly GameConfig _cfg;
        readonly IRng _rng;
        int _cooldown = 5;

        public Scoop Pending { get; private set; }
        public string LastOutcome { get; private set; }
        public Topic BrokenTopic { get; private set; }
        int _pendingAge;

        public ScoopManager(GameConfig cfg, IRng rng) { _cfg = cfg; _rng = rng; }

        static readonly (string head, string detail, string subject)[] Pool =
        {
            ("A manager approach you weren't supposed to know about",
             "Someone senior has been sounded out about the job. It's real, but it isn't public — and it might collapse.",
             "the manager approach"),
            ("A contract rebel in the dressing room",
             "A key player has told the club he won't sign. You have it before anyone.",
             "the contract standoff"),
            ("An incoming transfer, close to done",
             "A signing is at the medical stage. Nobody's reported it yet.",
             "the incoming signing"),
            ("Boardroom unrest",
             "Two directors are at war and one wants out. Explosive if it's true.",
             "the boardroom split"),
            ("A youth-team gem about to be tied down",
             "The club is rushing through a long contract for a 16-year-old you've barely heard of.",
             "the wonderkid contract"),
        };

        public void Tick(Engine engine, WeekContext ctx)
        {
            var st = engine.State;

            if (Pending != null)
            {
                if (++_pendingAge >= 3)
                {
                    // Sat on too long — treat it as verified and held.
                    Resolve(engine, ScoopChoice.VerifyHold);
                    ctx.ScoopOutcome = LastOutcome;
                    return;
                }
                ctx.PendingScoop = Pending;
                return;
            }
            LastOutcome = null;

            if (st.AccessTier < 2 || ctx.IsMatchless) return;
            if (_cooldown > 0) { _cooldown--; return; }

            float chance = _cfg.ScoopBaseChance + 0.025f * st.TrustedStanding;
            if (_rng.NextDouble() > chance) return;

            var p = Pool[_rng.Range(0, Pool.Length)];
            Pending = new Scoop { Headline = p.head, Detail = p.detail, Subject = p.subject };
            _pendingAge = 0;
            ctx.PendingScoop = Pending;
        }

        public void Resolve(Engine engine, ScoopChoice choice)
        {
            if (Pending == null) return;
            var st = engine.State;

            switch (choice)
            {
                case ScoopChoice.BreakNow:
                {
                    float disruption = _cfg.ScoopDisruptionBase
                                       + _cfg.ScoopDisruptionPerTier * st.AccessTier
                                       + MathX.Clamp01(st.Listeners / 500000f) * 0.30f
                                       - 0.03f * st.TrustedStanding
                                       - MathX.Clamp01((st.Credibility - 50f) / 100f) * 0.20f;
                    st.SocialReach = MathX.Clamp(st.SocialReach + 12f, 0f, 100f);
                    st.Listeners = (int)Math.Min(_cfg.MaxListeners, st.Listeners + (long)Math.Round(st.Listeners * 0.03));
                    st.ScoopsBroken++;
                    BrokenTopic = MakeBrokenTopic(Pending);

                    if (_rng.NextDouble() < MathX.Clamp01(disruption))
                    {
                        st.Reputation = MathX.Clamp(st.Reputation - 6f, 0f, 100f);
                        st.Credibility = MathX.Clamp(st.Credibility - 8f, 0f, 100f);
                        st.AccessProtectedWeeks = 8;
                        LastOutcome = "You broke it — enormous numbers — but a detail was wrong and the club has cut you off for a while.";
                    }
                    else
                    {
                        LastOutcome = "You broke it first. It stood up, and the whole fanbase came to you for it.";
                    }
                    break;
                }
                case ScoopChoice.VerifyHold:
                    st.Reputation = MathX.Clamp(st.Reputation + 3f, 0f, 100f);
                    st.Credibility = MathX.Clamp(st.Credibility + 4f, 0f, 100f);
                    st.TrustedStanding++;
                    st.SocialReach = MathX.Clamp(st.SocialReach + 2f, 0f, 100f);
                    LastOutcome = "You checked it and held it. The club noticed — and you'll have the definitive episode when it breaks.";
                    break;

                case ScoopChoice.Trade:
                    st.Money += 120f + st.Listeners * 0.01f;
                    st.TrustedStanding++;
                    LastOutcome = "You handed it to a national outlet. Cash now, and they owe you one.";
                    break;
            }

            Pending = null;
            _cooldown = 6;
        }

        public Topic ConsumeBrokenTopic()
        {
            var t = BrokenTopic;
            BrokenTopic = null;
            return t;
        }

        Topic MakeBrokenTopic(Scoop s) => new Topic
        {
            Id = TopicId.Recap,
            Name = "The scoop: " + s.Subject,
            Blurb = "You broke it. Now do it justice.",
            BaseAppeal = 2.20f,
            Effort = 4,
            Swing = 0.30f,
            RepEarn = 0f,
            SocialHook = 8,
            Response = TopicResponse.Reaction,
            Family = TopicFamily.Drama,
            IsAvailable = (c, st) => true
        };

        public void OnSeasonRollover()
        {
            Pending = null;
            BrokenTopic = null;
            _cooldown = 3;
        }
    }
}
