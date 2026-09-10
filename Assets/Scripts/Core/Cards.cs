using System;
using System.Collections.Generic;
using System.Linq;

namespace PodcastTycoon.Core
{
    public enum CardKind { OneShot, Permanent }

    public sealed class Card
    {
        public string Id;
        public string Name;
        public string Text;
        public CardKind Kind;
        public int Rarity;             // 0 common, 1 uncommon, 2 rare
        public Action<Engine> Apply;
    }

    /// <summary>
    /// Cards & packs (spec §17). A hand of up to five; milestone rewards and cash-bought packs.
    /// Most are one-shot buffs on the current episode or an immediate resource bump; a few
    /// permanently move the club or the show.
    /// </summary>
    public sealed class CardManager
    {
        readonly GameConfig _cfg;
        readonly IRng _rng;

        public CardManager(GameConfig cfg, IRng rng) { _cfg = cfg; _rng = rng; }

        public static readonly Dictionary<string, Card> Catalog = BuildCatalog();

        static Dictionary<string, Card> BuildCatalog()
        {
            void Rep(Engine e, float d) => e.State.Reputation = MathX.Clamp(e.State.Reputation + d, 0f, 100f);
            void Soc(Engine e, float d) => e.State.SocialReach = MathX.Clamp(e.State.SocialReach + d, 0f, 100f);
            void Str(Engine e, float d) => e.State.TeamStrength = MathX.Clamp(e.State.TeamStrength + d, 0.1f, 0.95f);
            void Lst(Engine e, float pct) => e.State.Listeners = (int)Math.Min(e.Config.MaxListeners,
                e.State.Listeners + (long)Math.Round(e.State.Listeners * pct));

            var list = new List<Card>
            {
                new Card { Id = "exclusive_scoop", Name = "Exclusive scoop", Rarity = 2, Kind = CardKind.OneShot,
                    Text = "Break a story nobody else has. Everyone's talking about you, and a jump in listeners right now.",
                    Apply = e => { Soc(e, 15f); Lst(e, 0.03f); } },

                new Card { Id = "viral_moment", Name = "Viral moment", Rarity = 1, Kind = CardKind.OneShot,
                    Text = "A clip takes off. This week's episode reaches far further than normal.",
                    Apply = e => e.State.CardReachMultThisWeek *= 1.6f },

                new Card { Id = "all_nighter", Name = "All-nighter", Rarity = 0, Kind = CardKind.OneShot,
                    Text = "Pull a long one. +4 prep points this week.",
                    Apply = e => e.State.CardPrepBonusThisWeek += 4 },

                new Card { Id = "ghostwriter", Name = "Ghostwriter", Rarity = 1, Kind = CardKind.OneShot,
                    Text = "A friend scripts it for you. This episode comes out noticeably sharper.",
                    Apply = e => e.State.CardQualityBonusThisWeek += 0.25f },

                new Card { Id = "damage_control", Name = "Damage control", Rarity = 1, Kind = CardKind.OneShot,
                    Text = "Get ahead of a bad week. +6 reputation now.",
                    Apply = e => Rep(e, 6f) },

                new Card { Id = "clip_farm", Name = "Clip farm", Rarity = 0, Kind = CardKind.OneShot,
                    Text = "Chop the back catalogue into shorts. A big spike in how much you're talked about online.",
                    Apply = e => Soc(e, 18f) },

                new Card { Id = "sure_thing", Name = "Sure thing", Rarity = 1, Kind = CardKind.OneShot,
                    Text = "You know this one lands. This week's episode gets the best possible reception.",
                    Apply = e => e.State.CardGuaranteeGoodRoll = true },

                new Card { Id = "top_prospect", Name = "Top prospect emerges", Rarity = 2, Kind = CardKind.Permanent,
                    Text = "An academy kid forces his way in. The team is permanently a little better.",
                    Apply = e => Str(e, 0.03f) },

                new Card { Id = "genius_appointment", Name = "Genius appointment", Rarity = 2, Kind = CardKind.Permanent,
                    Text = "The club hires brilliantly. A real, lasting lift to the team.",
                    Apply = e => Str(e, 0.05f) },

                new Card { Id = "takeover_talks", Name = "Takeover talks", Rarity = 2, Kind = CardKind.Permanent,
                    Text = "New money arrives. The squad strengthens and there's cash in the pot.",
                    Apply = e => { Str(e, 0.05f); e.State.Money += 300f; } },

                new Card { Id = "evergreen_segment", Name = "Evergreen segment", Rarity = 1, Kind = CardKind.Permanent,
                    Text = "A format that always works. +1 prep point every week, for good.",
                    Apply = e => e.State.CardPermanentPrepBonus += 1 },
            };
            return list.ToDictionary(c => c.Id);
        }

        public static Card Get(string id) => Catalog.TryGetValue(id, out var c) ? c : null;

        string Draw(float reputation)
        {
            // Reputation improves rarity odds.
            double roll = _rng.NextDouble() + reputation / 400.0;
            int wantRarity = roll > 0.88 ? 2 : roll > 0.55 ? 1 : 0;
            var pool = Catalog.Values.Where(c => c.Rarity == wantRarity).ToList();
            if (pool.Count == 0) pool = Catalog.Values.ToList();
            return pool[_rng.Range(0, pool.Count)].Id;
        }

        void AddCard(Engine engine, WeekContext ctx, string id)
        {
            if (engine.State.Hand.Count >= _cfg.CardHandLimit)
            {
                // hand full: a small cash consolation instead
                engine.State.Money += 15f;
                return;
            }
            engine.State.Hand.Add(id);
            ctx?.CardsGained.Add(Catalog[id].Name);
        }

        public void GrantMilestoneReward(Engine engine, int milestoneValue, WeekContext ctx)
        {
            int n = milestoneValue >= engine.Config.GoalListeners ? 3 : milestoneValue >= 2500 ? 2 : 1;
            for (int i = 0; i < n; i++) AddCard(engine, ctx, Draw(engine.State.Reputation));
        }

        public bool CanBuyPack(GameState st) => st.Money >= _cfg.PackCostMoney && st.Hand.Count < _cfg.CardHandLimit;

        public bool BuyPack(Engine engine)
        {
            if (!CanBuyPack(engine.State)) return false;
            engine.State.Money -= _cfg.PackCostMoney;
            for (int i = 0; i < 3; i++) AddCard(engine, engine.CurrentWeek, Draw(engine.State.Reputation));
            return true;
        }

        public bool CanPlay(GameState st) => st.CardsPlayedThisWeek < _cfg.CardPlaysPerWeek;

        public bool Play(Engine engine, string cardId)
        {
            if (!engine.State.Hand.Contains(cardId)) return false;
            if (!CanPlay(engine.State)) return false;
            var card = Get(cardId);
            if (card == null) return false;
            card.Apply(engine);
            engine.State.Hand.Remove(cardId);
            engine.State.CardsPlayedThisWeek++;
            return true;
        }

        public void BeginWeek(Engine engine, WeekContext ctx) { /* per-week card bonuses are reset by the engine */ }
    }
}
