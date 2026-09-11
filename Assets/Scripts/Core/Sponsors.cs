using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    /// <summary>The four kinds of clause a sponsor deal can carry (spec §18).</summary>
    public enum SponsorTermKind
    {
        /// <summary>A target metric by the deal's deadline — the classic milestone.</summary>
        Growth,
        /// <summary>A constraint that applies every week the deal is active.</summary>
        Standing,
        /// <summary>A dated one-off: hit a short spike by a given week.</summary>
        Ask,
        /// <summary>A pass/fail trigger, checked weekly; tripping it ends the deal immediately.</summary>
        Conduct
    }

    public sealed class SponsorTerm
    {
        public SponsorTermKind Kind;
        public string Text;                  // human-readable, set at creation

        // GROWTH
        public string Metric;                // "avgListeners" | "reputation" | "credibility" | "reach"
        public int Value;
        public bool Met;

        // STANDING
        public float AppealPenalty;          // <= 0, applied every week while active
        public float CredFloor;              // > 0: the deal breaks if credibility ever drops below this

        // ASK — a one-week reach spike due by a given week
        public int DueWeek;

        // CONDUCT
        public bool EndsOnBackfire;
    }

    public sealed class SponsorOffer
    {
        public string Name;
        public string Blurb;
        public int Weekly;
        public int SigningBonus;
        public int LengthWeeks;
        public readonly List<SponsorTerm> Terms = new List<SponsorTerm>();
        public int HitBonus;
        public int RenewWeekly;
        public float SignRepCost;
        public float MissRepCost;
        public bool MissClawback;
        public int Tier;

        public SponsorTerm PrimaryGrowth => Terms.FirstOrDefault(t => t.Kind == SponsorTermKind.Growth);
        public float TotalAppealPenalty => Terms.Where(t => t.Kind == SponsorTermKind.Standing).Sum(t => t.AppealPenalty);
        public string Describe() => string.Join("  ·  ", Terms.Select(t => t.Text));
    }

    public sealed class ActiveSponsor
    {
        public SponsorOffer Offer;
        public int WeeksElapsed;
        public bool Broken;         // a STANDING/CONDUCT term tripped — ends the deal immediately, as a miss
        public int WeeksLeft => Math.Max(0, Offer.LengthWeeks - WeeksElapsed);

        public bool AllMet => Offer.Terms
            .Where(t => t.Kind == SponsorTermKind.Growth || t.Kind == SponsorTermKind.Ask)
            .All(t => t.Met);

        public bool TargetMet => AllMet;   // back-compat name used by the UI
    }

    public sealed class SponsorNews
    {
        public string Headline;
        public string Body;
        public bool Good;
    }

    /// <summary>
    /// Sponsors as milestone-target contracts you choose (spec §18, v0.4). You hold one deal, or
    /// two once the second sponsor slot is bought. A deal is a bundle of 1-3 terms — more terms
    /// means more money and a worse miss.
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

                foreach (var term in a.Offer.Terms)
                {
                    switch (term.Kind)
                    {
                        case SponsorTermKind.Growth:
                            if (!term.Met && MetricValue(engine, term.Metric) >= term.Value) term.Met = true;
                            break;
                        case SponsorTermKind.Ask:
                            if (!term.Met && st.Listeners >= term.Value) term.Met = true;
                            break;
                        case SponsorTermKind.Standing:
                            if (term.CredFloor > 0f && st.Credibility < term.CredFloor) a.Broken = true;
                            break;
                        case SponsorTermKind.Conduct:
                            if (term.EndsOnBackfire && st.LastEpisodeBackfired) a.Broken = true;
                            break;
                    }
                }

                if (a.Broken)
                    Fail(engine, ctx, a);
                else if (a.WeeksElapsed >= a.Offer.LengthWeeks)
                {
                    if (a.AllMet) Succeed(engine, ctx, a);
                    else Fail(engine, ctx, a);
                }
            }

            int weekly = 0;
            float appealPenalty = 0f;
            foreach (var a in ActiveDeals)
            {
                weekly += a.Offer.Weekly;
                appealPenalty += a.Offer.TotalAppealPenalty;
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

        static float MetricValue(Engine engine, string metric) => metric switch
        {
            "reputation" => engine.State.Reputation,
            "credibility" => engine.State.Credibility,
            "reach" => engine.State.Listeners + engine.State.Followers,
            _ => engine.State.AverageListeners(engine.Config.AvgListenerWindow)
        };

        public bool CanSign(int index) => ActiveDeals.Count < _maxSlots && index >= 0 && index < Inbox.Count;

        public void Sign(Engine engine, int index)
        {
            if (!CanSign(index)) return;
            var o = Inbox[index];
            engine.State.Money += o.SigningBonus;
            if (o.SignRepCost > 0f)
                engine.State.Reputation = MathX.Clamp(engine.State.Reputation - o.SignRepCost, 0f, 100f);

            ActiveDeals.Add(new ActiveSponsor { Offer = o });
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
                Headline = $"{o.Name}: terms met",
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

            string why = a.Broken ? "A term broke mid-deal." : "You didn't meet the terms in time.";
            var news = new SponsorNews
            {
                Good = false,
                Headline = $"{o.Name}: deal's off",
                Body = $"{why} The deal ends"
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
            bool betting = name.Contains("betting");

            // variant 0 = cautious (fewer terms, gentle), 1 = standard, 2 = aggressive (more terms, more money)
            float weeklyMult = variant == 0 ? 0.7f : variant == 1 ? 1.0f : 1.7f;
            float growthReq = variant == 0 ? 0.10f : variant == 1 ? 0.24f : 0.48f;
            int weeks = variant == 0 ? 14 : variant == 1 ? 10 : 7;
            float missRep = variant == 0 ? 1f : variant == 1 ? 3f : 7f;
            bool clawback = variant == 2;
            int signing = (int)Math.Round(b.bonus * (variant == 2 ? 1.8f : variant == 0 ? 0.5f : 1f));

            // Growth slows near the market ceiling, so a big show can't promise a big % rise.
            float sizeFactor = MathX.Clamp(1f - avg / 90000f, 0.16f, 1f);
            growthReq *= sizeFactor;

            var offer = new SponsorOffer
            {
                Name = name,
                Tier = tier,
                Weekly = (int)Math.Round(b.weekly * weeklyMult),
                SigningBonus = signing,
                LengthWeeks = weeks,
                HitBonus = b.hit,
                RenewWeekly = (int)Math.Round(b.weekly * weeklyMult * 1.35f),
                MissRepCost = missRep,
                MissClawback = clawback,
                SignRepCost = betting ? 4f : 0f
            };

            // Every deal carries a GROWTH term. Bigger / later variants and the top tier add strings
            // attached — variant 0 stays "no strings" at every tier.
            offer.Terms.Add(GrowthTerm(avg, growthReq, weeks));

            if (!betting && variant > 0)
            {
                if (tier == 3)
                {
                    offer.Terms.Add(StandingCredFloorTerm(engine.State.Credibility >= 55f ? 55f : 40f));
                    offer.Terms.Add(ConductBackfireTerm());
                }
                else if (variant == 1)
                    offer.Terms.Add(StandingAppealTerm(-0.05f));
                else if (tier <= 1)
                    offer.Terms.Add(StandingAppealTerm(-0.08f));
                else
                    offer.Terms.Add(AskReachTerm(avg, weeks));
            }

            offer.Blurb = variant == 0
                ? "A cautious deal — modest money, a gentle target, no strings."
                : variant == 2
                    ? "Big money up front, but real terms and a hard landing if you miss."
                    : "A straightforward deal at market rate.";

            return offer;
        }

        SponsorTerm GrowthTerm(int avg, float growthReq, int weeks) => new SponsorTerm
        {
            Kind = SponsorTermKind.Growth,
            Metric = "avgListeners",
            Value = (int)Math.Round(avg * (1f + growthReq)),
            Text = $"reach {(int)Math.Round(avg * (1f + growthReq)):N0} average listeners within {weeks} weeks"
        };

        static SponsorTerm StandingAppealTerm(float penalty) => new SponsorTerm
        {
            Kind = SponsorTermKind.Standing,
            AppealPenalty = penalty,
            Text = "read their ad copy every episode (a small appeal hit, every week)"
        };

        static SponsorTerm StandingCredFloorTerm(float floor) => new SponsorTerm
        {
            Kind = SponsorTermKind.Standing,
            CredFloor = floor,
            Text = $"keep credibility at {floor:0} or above for the life of the deal"
        };

        static SponsorTerm ConductBackfireTerm() => new SponsorTerm
        {
            Kind = SponsorTermKind.Conduct,
            EndsOnBackfire = true,
            Text = "morality clause — the deal ends the moment a push backfires on you"
        };

        SponsorTerm AskReachTerm(int avg, int weeks)
        {
            int spike = (int)Math.Round(avg * 1.15f);
            int due = Math.Max(1, weeks / 2);
            return new SponsorTerm
            {
                Kind = SponsorTermKind.Ask,
                Value = spike,
                DueWeek = due,
                Text = $"hit {spike:N0} listeners for one week during the campaign (by week {due})"
            };
        }

        SponsorOffer Renewal(SponsorOffer o, Engine engine)
        {
            int avg = Math.Max(engine.State.Listeners, engine.State.AverageListeners(_cfg.AvgListenerWindow));
            float growthReq = 0.16f * MathX.Clamp(1f - avg / 90000f, 0.3f, 1f);
            var renewal = new SponsorOffer
            {
                Name = o.Name,
                Tier = o.Tier,
                Blurb = "A renewal on better terms after you delivered.",
                Weekly = o.RenewWeekly,
                SigningBonus = 0,
                LengthWeeks = 11,
                HitBonus = (int)Math.Round(o.HitBonus * 1.2f),
                RenewWeekly = (int)Math.Round(o.RenewWeekly * 1.3f),
                MissRepCost = o.MissRepCost,
                MissClawback = false,
                SignRepCost = 0f
            };
            renewal.Terms.Add(GrowthTerm(avg, growthReq, 11));
            var standing = o.Terms.FirstOrDefault(t => t.Kind == SponsorTermKind.Standing && t.AppealPenalty < 0f);
            if (standing != null) renewal.Terms.Add(StandingAppealTerm(standing.AppealPenalty));
            return renewal;
        }
    }
}
