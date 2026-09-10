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
        public float SignRepCost;
        public float ActiveAppealPenalty;
        public float MissRepCost;
        public bool MissClawback;
        public string Demand;
        public int Tier;
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
    /// Sponsors as milestone-target contracts you choose (spec §16). You hold one deal, or two
    /// once the second sponsor slot is bought. National / category tiers arrive as the show grows.
    /// </summary>
    public sealed class SponsorManager
    {
        readonly IRng _rng;
        readonly GameConfig _cfg;

        public readonly List<ActiveSponsor> ActiveDeals = new List<ActiveSponsor>();
        public ActiveSponsor Active => ActiveDeals.Count > 0 ? ActiveDeals[0] : null;

        public readonly List<SponsorOffer> Inbox = new List<SponsorOffer>();

        int _offerCooldown;
        int _offerLife;
        int _maxSlots = 1;

        public event Action<SponsorNews> Resolved;

        public SponsorManager(GameConfig cfg, IRng rng) { _cfg = cfg; _rng = rng; }

        public int MaxSlots => _maxSlots;

        public void Tick(Engine engine, WeekContext ctx)
        {
            var st = engine.State;
            _maxSlots = st.HasSecondSponsorSlot ? 2 : 1;

            if (st.Modifiers.SponsorFree)
            {
                ActiveDeals.Clear();
                Inbox.Clear();
                st.SponsorWeekly = 0;
                st.SponsorAppealPenalty = 0f;
                ctx.SponsorInbox = Inbox;
                ctx.ActiveSponsor = null;
                return;
            }

            for (int i = ActiveDeals.Count - 1; i >= 0; i--)
            {
                var a = ActiveDeals[i];
                a.WeeksElapsed++;
                if (!a.TargetMet && MetricValue(engine, a.Offer.Target) >= a.Offer.Target.Value)
                    a.TargetMet = true;

                if (a.WeeksElapsed >= a.Offer.Target.Weeks)
                {
                    if (a.TargetMet) Succeed(engine, ctx, a);
                    else Fail(engine, ctx, a);
                }
            }

            int weekly = 0;
            float appealPenalty = 0f;
            foreach (var a in ActiveDeals)
            {
                weekly += a.Offer.Weekly;
                appealPenalty += a.Offer.ActiveAppealPenalty;
            }
            st.SponsorWeekly = weekly;
            st.SponsorAppealPenalty = appealPenalty;

            if (_offerCooldown > 0) _offerCooldown--;

            if (ActiveDeals.Count < _maxSlots && _offerCooldown == 0)
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
            else if (ActiveDeals.Count >= _maxSlots)
            {
                Inbox.Clear();
            }

            ctx.SponsorInbox = Inbox;
            ctx.ActiveSponsor = Active;
        }

        public void OnSeasonRollover()
        {
            if (ActiveDeals.Count == 0) { Inbox.Clear(); _offerCooldown = 1; }
        }

        int MetricValue(Engine engine, SponsorTarget t)
            => t.Metric == "reputation"
                ? (int)engine.State.Reputation
                : engine.State.AverageListeners(_cfg.AvgListenerWindow);

        public bool CanSign(int index) => ActiveDeals.Count < _maxSlots && index >= 0 && index < Inbox.Count;

        public void Sign(Engine engine, int index)
        {
            if (!CanSign(index)) return;
            var o = Inbox[index];
            engine.State.Money += o.SigningBonus;
            if (o.SignRepCost > 0f)
                engine.State.Reputation = MathX.Clamp(engine.State.Reputation - o.SignRepCost, 0f, 100f);

            ActiveDeals.Add(new ActiveSponsor { Offer = o, StartListeners = engine.State.Listeners });
            Inbox.Clear();
            _offerLife = 0;
            _offerCooldown = ActiveDeals.Count < _maxSlots ? 2 : 0;
        }

        void Succeed(Engine engine, WeekContext ctx, ActiveSponsor a)
        {
            var o = a.Offer;
            engine.State.Money += o.HitBonus;
            engine.State.Reputation = MathX.Clamp(engine.State.Reputation + 2f, 0f, 100f);
            engine.State.SocialReach = MathX.Clamp(engine.State.SocialReach + 2f, 0f, 100f);

            var renewal = Renewal(o, engine);
            ActiveDeals.Remove(a);
            Inbox.Clear();
            Inbox.Add(renewal);
            _offerLife = 6;
            _offerCooldown = 0;

            var news = new SponsorNews
            {
                Good = true,
                Headline = $"{o.Name}: target hit",
                Body = $"You delivered. €{o.HitBonus:N0} bonus, and a renewal is on the table at €{renewal.Weekly:N0}/week."
            };
            ctx.SponsorNews = news;
            Resolved?.Invoke(news);
        }

        void Fail(Engine engine, WeekContext ctx, ActiveSponsor a)
        {
            var o = a.Offer;
            engine.State.Reputation = MathX.Clamp(engine.State.Reputation - o.MissRepCost, 0f, 100f);
            if (o.MissClawback) engine.State.Money -= o.SigningBonus;

            ActiveDeals.Remove(a);
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

            float weeklyMult = variant == 0 ? 0.7f : variant == 1 ? 1.0f : 1.7f;
            float growthReq = variant == 0 ? 0.10f : variant == 1 ? 0.24f : 0.48f;
            int weeks = variant == 0 ? 14 : variant == 1 ? 10 : 7;
            float missRep = variant == 0 ? 1f : variant == 1 ? 3f : 7f;
            bool clawback = variant == 2;
            int signing = (int)Math.Round(b.bonus * (variant == 2 ? 1.8f : variant == 0 ? 0.5f : 1f));

            // Growth slows near the market ceiling, so a big show can't promise a big % rise.
            float sizeFactor = MathX.Clamp(1f - avg / 90000f, 0.16f, 1f);
            growthReq *= sizeFactor;

            bool betting = name.Contains("betting");
            var target = new SponsorTarget
            {
                Metric = "avgListeners",
                Value = (int)Math.Round(avg * (1f + growthReq)),
                Weeks = weeks
            };

            var offer = new SponsorOffer
            {
                Name = name,
                Tier = tier,
                Weekly = (int)Math.Round(b.weekly * weeklyMult),
                SigningBonus = signing,
                HitBonus = b.hit,
                RenewWeekly = (int)Math.Round(b.weekly * weeklyMult * 1.35f),
                MissRepCost = missRep,
                MissClawback = clawback,
                SignRepCost = betting ? 4f : 0f,
                ActiveAppealPenalty = variant == 2 && !betting ? -0.04f : 0f,
                Target = target
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
            var target = new SponsorTarget
            {
                Metric = "avgListeners",
                Value = (int)Math.Round(avg * (1f + 0.16f * MathX.Clamp(1f - avg / 90000f, 0.3f, 1f))),
                Weeks = 11
            };
            return new SponsorOffer
            {
                Name = o.Name,
                Tier = o.Tier,
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
                Target = target
            };
        }
    }
}
