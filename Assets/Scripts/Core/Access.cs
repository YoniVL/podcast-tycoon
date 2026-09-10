using System;

namespace PodcastTycoon.Core
{
    /// <summary>
    /// Audience is leverage (spec §14). The rolling-average listener count sets an access tier
    /// 0–3 that gates scoops, the interview topic, positive influence and — at the top — a
    /// partnership offer with editorial strings.
    /// </summary>
    public sealed class AccessManager
    {
        readonly GameConfig _cfg;
        readonly IRng _rng;
        int _partnershipCooldown = 6;

        public bool PartnershipOffered { get; private set; }

        public AccessManager(GameConfig cfg, IRng rng) { _cfg = cfg; _rng = rng; }

        public static string TierLabel(int tier) => tier switch
        {
            3 => "Power broker",
            2 => "Insider",
            1 => "Press pass",
            _ => "Outsider"
        };

        public void Recompute(Engine engine, WeekContext ctx)
        {
            var st = engine.State;
            if (st.AccessProtectedWeeks > 0) st.AccessProtectedWeeks--;

            int avg = st.AverageListeners(_cfg.AvgListenerWindow);
            var th = _cfg.AccessTierListeners;
            int raw = avg >= th[2] ? 3 : avg >= th[1] ? 2 : avg >= th[0] ? 1 : 0;
            if (st.AccessProtectedWeeks > 0) raw = Math.Max(0, raw - 1);

            if (raw != st.AccessTier)
            {
                bool up = raw > st.AccessTier;
                st.AccessTier = raw;
                ctx.AccessNote = up
                    ? $"Access upgraded — {TierLabel(raw)}. " + UpPerk(raw)
                    : $"Your access to the club has slipped back to {TierLabel(raw)}.";
            }
            ctx.AccessTier = st.AccessTier;

            // Partnership offer — rare, top tier, well-regarded, trusted.
            if (_partnershipCooldown > 0) _partnershipCooldown--;
            if (!st.HasPartnership && !PartnershipOffered
                && st.AccessTier >= 3 && st.Reputation >= 55f && st.TrustedStanding >= 2
                && _partnershipCooldown == 0 && _rng.NextDouble() < 0.25)
            {
                PartnershipOffered = true;
                ctx.AccessNote = "The club wants to make it official — a partnership offer is on the table (see Business).";
            }
        }

        static string UpPerk(int tier) => tier switch
        {
            1 => "Post-match quotes and pressers are open to you now.",
            2 => "Scoops start reaching you before the news breaks — and the club is listening to your tone.",
            3 => "Your takes move the needle now: fan sentiment, board pressure, even a youngster's minutes.",
            _ => ""
        };

        public bool AcceptPartnership(Engine engine)
        {
            if (!PartnershipOffered || engine.State.HasPartnership) return false;
            engine.State.HasPartnership = true;
            engine.State.Reputation = MathX.Clamp(engine.State.Reputation + 3f, 0f, 100f);
            PartnershipOffered = false;
            return true;
        }

        public void DeclinePartnership()
        {
            PartnershipOffered = false;
            _partnershipCooldown = 20;
        }

        public void OnSeasonRollover()
        {
            // access carries; just re-arm the partnership pitch if it lapsed
            if (_partnershipCooldown > 4) _partnershipCooldown = 4;
        }
    }
}
