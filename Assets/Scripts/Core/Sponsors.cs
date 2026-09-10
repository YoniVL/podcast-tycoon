using System;
using System.Collections.Generic;

namespace PodcastTycoon.Core
{
    public sealed class SponsorTarget
    {
        public string Metric;   // "avgListeners" | "reputation"
        public int Value;
        public int Weeks;

        public string Describe() => Metric == "reputation"
            ? $"reach reputation {Value} within {Weeks} weeks"
            : $"reach {Value:N0} average listeners within {Weeks} weeks";
    }

    public sealed class SponsorOffer
    {
        public string Name;
        public string Blurb;
        public int Weekly;
        public int SigningBonus;
        public SponsorTarget Target;
        public int HitBonus;
        public int RenewWeekly;
        public float SignRepCost;           // >= 0, deducted on signing
        public float ActiveAppealPenalty;   // <= 0, while active
        public float MissRepCost;           // >= 0, deducted on a missed target
        public bool MissClawback;           // repay the signing bonus if you miss
        public string Demand;               // one-line flavour, nullable
    }

    public sealed class ActiveSponsor
    {
        public SponsorOffer Offer;
        public int WeeksElapsed;
        public bool TargetMet;
        public int StartListeners;
        public int WeeksLeft => Math.Max(0, Offer.Target.Weeks - WeeksElapsed);
    }

    public sealed class SponsorNews
    {
        public string Headline;
        public string Body;
        public bool Good;
    }

    /// <summary>
    /// Sponsors as milestone-target contracts you choose (spec §16). Offers arrive when the show is
    /// big enough; each deal pays weekly but must hit a growth target by a deadline or it ends badly.
    /// </summary>
    public sealed class SponsorManager
    {
        readonly IRng _rng;
        readonly GameConfig _cfg;

        public ActiveSponsor Active { get; private set; }
        public readonly List<SponsorOffer> Inbox = new List<SponsorOffer>();

        int _offerCooldown;
        int _offerLife;

        public event Action<SponsorNews> Resolved;

        public SponsorManager(GameConfig cfg, IRng rng) { _cfg = cfg; _rng = rng; }

        public void Tick(Engine engine, WeekContext ctx)
        {
            var st = engine.State;

            if (Active != null)
            {
                Active.WeeksElapsed++;
                if (!Active.TargetMet && MetricValue(engine, Active.Offer.Target) >= Active.Offer.Target.Value)
                    Active.TargetMet = true;

                if (Active.WeeksElapsed >= Active.Offer.Target.Weeks)
                {
                    if (Active.TargetMet) Succeed(engine, ctx);
                    else Fail(engine, ctx);
                }
            }

            st.SponsorWeekly = Active?.Offer.Weekly ?? 0;
            st.SponsorAppealPenalty = Active?.Offer.ActiveAppealPenalty ?? 0f;

            // Offer inbox
            if (_offerCooldown > 0) _offerCooldown--;

            if (Active == null && _offerCooldown == 0)
            {
                if (Inbox.Count == 0)
                {
                    GenerateOffers(engine);
                    _offerLife = 6;
                }
                else if (--_offerLife <= 0)
                {
                    Inbox.Clear();
                    _offerCooldown = 2;
                }
            }

            ctx.SponsorInbox = Inbox;
            ctx.ActiveSponsor = Active;
        }

        public void OnSeasonRollover()
        {
            // A running deal continues across the offseason; just clear stale offers.
            if (Active == null) { Inbox.Clear(); _offerCooldown = 1; }
        }

        int MetricValue(Engine engine, SponsorTarget t)
            => t.Metric == "reputation"
                ? (int)engine.State.Reputation
                : engine.State.AverageListeners(_cfg.AvgListenerWindow);

        // ------------------------------------------------------------------
        public bool CanSign(int index) => Active == null && index >= 0 && index < Inbox.Count;

        public void Sign(Engine engine, int index)
        {
            if (!CanSign(index)) return;
            var o = Inbox[index];
            engine.State.Money += o.SigningBonus;
            if (o.SignRepCost > 0f)
                engine.State.Reputation = MathX.Clamp(engine.State.Reputation - o.SignRepCost, 0f, 100f);

            Active = new ActiveSponsor { Offer = o, StartListeners = engine.State.Listeners };
            Inbox.Clear();
            _offerLife = 0;
        }

        void Succeed(Engine engine, WeekContext ctx)
        {
            var o = Active.Offer;
            engine.State.Money += o.HitBonus;
            engine.State.Reputation = MathX.Clamp(engine.State.Reputation + 2f, 0f, 100f);
            engine.State.Buzz += 5;

            var renewal = Renewal(o, engine);
            Active = null;
            Inbox.Clear();
            Inbox.Add(renewal);
            _offerLife = 6;
            _offerCooldown = 0;

            var news = new SponsorNews
            {
                Good = true,
                Headline = $"{o.Name}: target hit",
                Body = $"You delivered. €{o.HitBonus:N0} bonus, and they've put a renewal on the table at €{renewal.Weekly:N0}/week."
            };
            ctx.SponsorNews = news;
            Resolved?.Invoke(news);
        }

        void Fail(Engine engine, WeekContext ctx)
        {
            var o = Active.Offer;
            engine.State.Reputation = MathX.Clamp(engine.State.Reputation - o.MissRepCost, 0f, 100f);
            if (o.MissClawback) engine.State.Money -= o.SigningBonus;

            Active = null;
            Inbox.Clear();
            _offerCooldown = 4;

            var news = new SponsorNews
            {
                Good = false,
                Headline = $"{o.Name}: deal's off",
                Body = $"You didn't hit the target in time. The deal ends"
                       + (o.MissClawback ? $", and the €{o.SigningBonus:N0} signing fee has to go back." : ".")
            };
            ctx.SponsorNews = news;
            Resolved?.Invoke(news);
        }

        // ------------------------------------------------------------------
        static readonly string[] LocalNames =
        {
            "Priddy's Hardware", "The Anchor (your local)", "Marsh & Sons Plumbing",
            "Cranbrook Car Wash", "Halden Cycles", "The Corner Cafe"
        };
        static readonly string[] RegionalNames =
        {
            "Northgate Kitchens", "a regional betting site", "Fenn Insurance",
            "Vale Sportswear", "the county brewery", "Okoro Motors"
        };
        static readonly string[] NationalNames =
        {
            "a national betting firm", "a food delivery app", "a mobile network",
            "a fantasy football platform", "an energy drink brand"
        };
        static readonly string[] PartnerNames =
        {
            "a global sportswear brand", "a streaming service", "a bank's football arm"
        };

        void GenerateOffers(Engine engine)
        {
            Inbox.Clear();
            int avg = Math.Max(engine.State.Listeners, engine.State.AverageListeners(_cfg.AvgListenerWindow));
            if (avg < 300) return;

            int tier = avg >= 70000 ? 3 : avg >= 14000 ? 2 : avg >= 2500 ? 1 : 0;
            int count = 2 + (_rng.NextDouble() < 0.4 ? 1 : 0);

            for (int variant = 0; variant < count; variant++)
                Inbox.Add(MakeOffer(tier, avg, variant, engine));
        }

        SponsorOffer MakeOffer(int tier, int avg, int variant, Engine engine)
        {
            // base economy per tier
            (int weekly, int bonus, int hit)[] baseByTier =
            {
                (30, 150, 220),
                (95, 320, 550),
                (430, 1300, 2600),
                (1900, 4200, 9000)
            };
            var b = baseByTier[Math.Min(tier, 3)];
            string name = tier switch
            {
                0 => LocalNames[_rng.Range(0, LocalNames.Length)],
                1 => RegionalNames[_rng.Range(0, RegionalNames.Length)],
                2 => NationalNames[_rng.Range(0, NationalNames.Length)],
                _ => PartnerNames[_rng.Range(0, PartnerNames.Length)]
            };

            // variant: 0 = cautious (should be hittable), 1 = standard, 2 = aggressive (a gamble)
            float weeklyMult = variant == 0 ? 0.7f : variant == 1 ? 1.0f : 1.7f;
            float growthReq = variant == 0 ? 0.10f : variant == 1 ? 0.24f : 0.48f;
            int weeks = variant == 0 ? 14 : variant == 1 ? 10 : 7;
            float missRep = variant == 0 ? 1f : variant == 1 ? 3f : 7f;
            bool clawback = variant == 2;
            int signing = (int)Math.Round(b.bonus * (variant == 2 ? 1.8f : variant == 0 ? 0.5f : 1f));

            // Growth slows near the market ceiling, so a big show can't promise a big % rise.
            float sizeFactor = MathX.Clamp(1f - avg / 90000f, 0.28f, 1f);
            growthReq *= sizeFactor;

            bool betting = name.Contains("betting");
            var offer = new SponsorOffer
            {
                Name = name,
                Weekly = (int)Math.Round(b.weekly * weeklyMult),
                SigningBonus = signing,
                HitBonus = b.hit,
                RenewWeekly = (int)Math.Round(b.weekly * weeklyMult * 1.35f),
                MissRepCost = missRep,
                MissClawback = clawback,
                SignRepCost = betting ? 4f : 0f,
                ActiveAppealPenalty = variant == 2 && !betting ? -0.04f : 0f,
                Target = new SponsorTarget
                {
                    Metric = "avgListeners",
                    Value = (int)Math.Round(avg * (1f + growthReq)),
                    Weeks = weeks
                }
            };

            offer.Demand =
                betting ? "Costs you 4 reputation to sign." :
                offer.ActiveAppealPenalty < 0f ? "You have to read their ad copy every episode (small appeal hit)." :
                clawback ? "Miss the target and the signing fee has to be repaid." :
                null;

            offer.Blurb = variant == 0
                ? "A cautious deal — modest money, a gentle target."
                : variant == 2
                    ? "Big money up front, but a steep target and a hard landing if you miss."
                    : "A straightforward deal at market rate.";

            return offer;
        }

        SponsorOffer Renewal(SponsorOffer o, Engine engine)
        {
            int avg = Math.Max(engine.State.Listeners, engine.State.AverageListeners(_cfg.AvgListenerWindow));
            return new SponsorOffer
            {
                Name = o.Name,
                Blurb = "A renewal on better terms after you delivered.",
                Weekly = o.RenewWeekly,
                SigningBonus = 0,
                HitBonus = (int)Math.Round(o.HitBonus * 1.2f),
                RenewWeekly = (int)Math.Round(o.RenewWeekly * 1.3f),
                MissRepCost = o.MissRepCost,
                MissClawback = false,
                SignRepCost = 0f,
                ActiveAppealPenalty = o.ActiveAppealPenalty,
                Demand = o.ActiveAppealPenalty < 0f ? "Keep reading their ad copy (small appeal hit)." : null,
                Target = new SponsorTarget
                {
                    Metric = "avgListeners",
                    Value = (int)Math.Round(avg * (1f + 0.18f * MathX.Clamp(1f - avg / 90000f, 0.35f, 1f))),
                    Weeks = 10
                }
            };
        }
    }
}
