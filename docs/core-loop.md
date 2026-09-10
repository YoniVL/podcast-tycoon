# Podcast Tycoon — core loop spec (v0.1)

Readable version with a live tuning bench:
https://claude.ai/code/artifact/18bdfff0-e182-4e36-a5f1-cbb9834d6348

This file is the canonical numbers for implementation. All values are first-pass
and expected to move in playtesting. Keep this file and the artifact in sync.

---

## The turn

1 week = 1 episode = 1 turn. Phases, in order:

1. **Offer** — 3 topics drawn from the pool, weighted by reputation tier.
   Redraw once for €15 or 1 Buzz.
2. **Produce** — spend prep points (base 10) across topic prep + Research +
   Audio + Promo.
3. **Play cards** — optional, up to 2 from hand.
4. **Publish** — resolution runs, results screen.

## Resources

| Resource     | Start   | Notes |
|--------------|---------|-------|
| Money €      | 500     | can go negative; bankruptcy fail state |
| Listeners    | 40      | the score; compounds reach |
| Reputation   | 10      | 0–100 slow multiplier; gates topics/sponsors/pack rarity |
| Buzz         | 0       | spiky soft currency; buys card packs |
| Prep points  | 10/week | refills weekly; unspent are lost |

## Topics

| id        | appeal | effort | swing | rep | buzz | available            | slice |
|-----------|--------|--------|-------|-----|------|----------------------|-------|
| recap     | 1.00   | 3      | 0.15  | +1  | 0    | always               | 1 |
| transfers | 1.40   | 4      | 0.45  |  0  | +3   | always               | 1 |
| tierlist  | 1.20   | 4      | 0.25  | +1  | +2   | always               | 1 |
| mailbag   | 0.70   | 2      | 0.10  | +2  | 0    | always               | 1 |
| hottake   | 1.55   | 3      | 0.60  | −1  | +5   | reputation ≥ 15      | 2 |
| tactics   | 0.90   | 6      | 0.12  | +3  | 0    | reputation ≥ 25      | 2 |
| interview | 1.90   | 7      | 0.14  | +4  | +4   | Guest budget upgrade | 3 |

## Resolution formulas

```
# inputs: ppTopic, ppResearch, ppAudio, ppPromo, listeners, reputation
# gear:   micQuality (0.20 base / 0.60 mic), editingSkill (0.15 base / 0.45 sw)
# panelsBonus: 0 / 0.08 ;  cohostAppeal: 0 / 0.05 ;  timeliness: 1.0 (slice 3: 0.85–1.30)

prepRatio = clamp(ppTopic / topic.effort, 0, 1)
overshoot = max(0, ppTopic - topic.effort) * 0.025
gear      = 0.15*micQuality + 0.15*editingSkill
quality   = 0.35 + 0.65*prepRatio + overshoot + gear + 0.035*ppAudio + panelsBonus
quality   = clamp(quality, 0.20, 1.60)

spread    = max(0, topic.swing * (1 - 0.18*ppResearch))
roll      = 1 + uniform(-spread, +spread)          # Damage Control card floors at 1.0

appeal    = topic.appeal + cohostAppeal
reach     = listeners * appeal * (1 + 0.08*ppPromo) * timeliness * cardReachMult

gross     = reach * (quality - 0.70) * roll * 0.42
wom       = quality > 1 ? listeners * 0.03 * (quality - 1) : 0
churn     = listeners * 0.045 * clamp(1.40 - quality, 0, 1.40)
listeners += round(gross + wom - churn)            # clamp at 0

repGain   = topic.rep * quality - (quality < 0.55 ? 4 : 0)
reputation = clamp(reputation + repGain, 0, 100)

perf      = quality * roll
buzz     += perf > 1.15 ? round((perf - 1.15) * 35 * sqrt(appeal)) : 0
buzz     += topic.buzz

adRevenue    = listeners * 0.0045 * (0.80 + 0.40*reputation/100)
hosting      = 5 + listeners * 0.0009
costs        = 20 + hosting + wages          # wages: co-host 45, 2nd co-host 70, retainer 60, studio 30
money       += adRevenue + sponsorWeekly - costs
```

### Worked example (week 1, no upgrades)
topic recap, prep 3 / research 2 / audio 3 / promo 2, listeners 40, rep 10
→ quality 1.12, spread ±0.10, reach 46.4
→ listeners +8 (→48), reputation +1.1, buzz 0
→ ad €0.15 − costs €25 = −€24.85 (→ €475)

## Tuning knobs

| knob | default | controls |
|------|---------|----------|
| startMoney | 500 | opening runway length |
| startListeners | 40 | length of first climb |
| prepBase | 10 | tightness of the weekly decision |
| adRate | 0.0045 | passive income slope (€/listener/episode) |
| churnRate | 0.045 | how fast a bad run bleeds audience |
| qualityBreakeven | 0.70 | quality below this loses listeners |
| deltaScale | 0.42 | overall growth speed |
| womRate | 0.03 | bonus growth from standout episodes |
| fixedOverhead | 20 | baseline weekly cost pressure |
| hostingSlope | 0.0009 | how much scale costs |
| buzzThreshold | 1.15 | how good an episode must be to earn Buzz |
| buzzScale | 35 | Buzz payout size |
| packCost | 60 | Buzz per 3-card pack |
| bankruptcyFloor / grace | −200 / 3 weeks | fail-state forgiveness |
| goalListeners | 50000 | primary goal |

## Economy — sponsors

| sponsor          | listeners | rep | €/week | note |
|------------------|-----------|-----|--------|------|
| Local brand      | 500       | 20  | 40     | 6-week deal; saves the first run |
| Betting app      | 2,500     | 30  | 120    | −5 rep to accept |
| National sponsor | 15,000    | 50  | 500    | renews on better terms |
| Category partner | 75,000    | 60  | 2,000  | exclusivity clause |

**Bankruptcy:** money < −200 → warning. 3 consecutive weeks < −200 → run ends.

## Upgrades

| upgrade | cost | effect | slice |
|---------|------|--------|-------|
| XLR mic | €150 | micQuality 0.20 → 0.60 | 1 |
| Acoustic panels | €120 | quality floor +0.08 | 1 |
| Editing software | €200 | editingSkill 0.15 → 0.45 | 1 |
| Co-host | €300 +€45/wk | +3 prep points/wk, +0.05 appeal | 1 |
| Faster PC | €400 | +1 usable prep point/wk | 2 |
| Guest budget | €250 | unlocks Big interview topic | 2 |
| Marketing retainer | €500 +€60/wk | +2% passive listener growth/wk | 3 |
| Studio space | €900 +€30/wk | +2 prep points, reach ×1.08 | 3 |
| Second co-host | €600 +€70/wk | +2 prep points/wk, +0.05 appeal | 3 |

## Cards (slice 2+)

Hand of 5. Earned at milestones or bought as 3-card packs for 60 Buzz.
Reputation improves rarity odds.

| card | effect | rarity |
|------|--------|--------|
| Exclusive scoop | +0.5 appeal this episode | common |
| Viral moment | reach ×1.5 this episode | common |
| All-nighter | +4 prep now, −3 next week | common |
| Ghostwriter | topic prep counts as fully met | uncommon |
| Damage control | floor this episode's roll at 1.0 | uncommon |
| Clip farm | ×2 Buzz from over-performing this episode | uncommon |
| Sponsor hustle | +€250 now, −4 reputation | rare |
| Evergreen segment | permanent +0.08 quality floor | rare |

## Milestone ladder

100 · 500 (Local sponsor) · 2,500 (+pack) · 10,000 (+pack +40 Buzz) ·
**50,000 = primary goal** · 100k · 250k · 500k · 1M (buyout decision) · 2.5M+ endless.
Each rung past 2,500 pays a card pack + Buzz + a flavor unlock.

## Build order

- **Slice 1** — state model, resolution, 4 topics, prep + 4 levers, results
  screen, 3 upgrades + co-host, ad revenue + costs, bankruptcy check, milestone
  popups. No cards, no sponsors (stub both). Question: is the turn fun?
- **Slice 2** — cards + packs + Buzz economy, Hot take + Tactics topics,
  Local + Betting sponsors, Faster PC + Guest budget, redraw.
- **Slice 3** — news-cycle timeliness, Big interview, national + category
  sponsors, retainer/studio/2nd co-host, full + endless milestones, 1M buyout,
  season-review screen.

## Open questions

- News cycle: pure RNG vs. multi-week story arcs (a transfer saga)?
- Interrupt events (guest cancels, clip blows up, rival launches) — yes/no?
- Staff depth: co-hosts only, or a crew with role slots?
- Between-run meta-progression if a run can end?
- One show or a slate of shows late-game?
