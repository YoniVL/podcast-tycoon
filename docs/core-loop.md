# Podcast Tycoon — core loop spec (v0.3)

Readable version with a live tuning bench:
https://claude.ai/code/artifact/18bdfff0-e182-4e36-a5f1-cbb9834d6348

Canonical numbers for implementation. All values first-pass, expected to move in
playtesting. Keep this file and the artifact in sync.

- **v0.2** made your club's simulated season the spine of the loop.
- **v0.3** adds: sponsors as milestone-target contracts you choose; audience as
  *leverage* over the club (access tiers + scoops); rival storylines; match
  scorelines + moments; unlockable custom-run modifiers.

---

## 1. Setup — your club & podcast

Chosen once at the start of a run:

- **Podcast name**, **club name** — free text.
- **Club colours** — primary + secondary. Drive the UI accent, the matchday
  scoreboard, the episode-cover template.
- **League flavour** — generic or a named set. Default generic.
- **Difficulty** — sets club strength (§12). The biggest choice: title race vs.
  relegation scrap changes *what content exists*, not just the math.
- *(stretch)* **Supporter type** — lifelong / glory-hunter / long-suffering.

On start: generate a ~20-club league with strengths, your squad of ~6 notable
players (§10), a full fixture list, 1–2 tracked rivals (§11). Begin with
€500 / 40 listeners / rep 10 / 0 Buzz, no crew, no sponsor.

## 2. The season = the game's year

One turn = one week = one episode. A season ≈ 46 turns. **Endless** — seasons
repeat indefinitely; club strength, league quality, and milestones scale with
you.

| Block | Turns | Notes |
|-------|-------|-------|
| League | 38 | one match most weeks; busy weeks bundle midweek + weekend into one episode |
| Cup | up to 6 | knockout; late rounds are event weeks |
| European | 0 / 6–13 | only if qualified |
| International breaks | ~4 | no club match; topic pool shifts; weekly reach ×0.8 |
| Offseason | 3–4 | transfer window = peak Buzz, listener dip; sagas resolve; club tier re-evaluated. **You only react — no transfer decisions.** |

Audience carries between seasons minus `offseasonChurn` (~15%).

## 3. The week

1. **The week happens** (sim) — calendar advances; match simulated (§4); squad
   news rolls (§10); threads tick (§9); an event or a scoop may fire (§8, §14).
2. **Offer** — 3 topics from a pool shaped by result, moments, threads, squad
   news, rivals, calendar slot. Redraw once for €15 / 1 Buzz.
3. **Produce** — spend prep points (base 10) on topic + Research / Audio / Promo.
4. **Cards & events** — up to 2 cards; resolve pending event/scoop choice.
5. **Publish** — resolution (§13). Results screen: league position, next
   fixture, thread + rival movement, sponsor-target progress.

## 4. Match simulation & context

### Sim

```
teamStrength   # 0..1 from difficulty (§12), drifts + improves over a run
oppStrength    # per fixture, ~0.50 mean, from the generated table
keyOut         # sum of strength penalties from injured/suspended key players

d      = (teamStrength - keyOut) - oppStrength + (home ? 0.06 : -0.06)
pWin   = clamp(0.45 + 0.85*d, 0.05, 0.88)
pLoss  = clamp(0.45 - 0.85*d, 0.05, 0.88)
pDraw  = max(0.10, 1 - pWin - pLoss)        # renormalize the three
outcome, scoreline  = weighted pick + a light goals model

expectation = pWin > 0.55 ? expWin : pLoss > 0.55 ? expLoss : tossup
surprise:  +2 won when expLoss | +1 won a tossup / drew when expLoss
            0 as expected      | -1 lost a tossup / drew when expWin
           -2 lost when expWin
```

### Match moments (a little depth)

Sim emits **1–2 moments** tagged: `screamer`, `howler`, `red_card`,
`penalty_drama`, `wonderkid_goal`, `keeper_error`, `last_minute`, `masterclass`,
`capitulation`. Moments sharpen topic hooks and seed threads (repeated `howler`
by a defender → "Is he finished?"; `wonderkid_goal` → feeds the kid thread). A
strong moment can shift context one step (a `howler` in a win still hands you a
hot-take angle).

### Context → the episode

```
# topic appeal multiplier by surprise (on top of base appeal, §6)
-2 : recap 0.7 · hottake/crisis 2.0 · ratings 1.9
-1 : recap 0.9 · hottake 1.5
 0 : ~1.0  (evergreen / mailbag / tactics weeks)
+1 : recap 1.4 · optimism 1.4
+2 : recap 1.8 · optimism 1.8 · Buzz bonus

fixture importance (derby/big-match/six-pointer/cup-KO/euro/final): appeal ×1.3, Buzz ×2
international break: club topics ×0.55 · international/prospect/transfer ×1.35 · reach ×0.8

# listener mood — fans react regardless of your episode
+2 : passiveGain = listeners * 0.020
+1 : passiveGain = listeners * 0.010
-1 : moodChurn   = listeners * 0.010 * max(0, 1.10 - quality)
-2 : moodChurn   = listeners * 0.025 * max(0, 1.20 - quality)
```

Intent: handle a crisis well → you grow from it; fumble or ignore it → fans leave.

## 5. Resources

| Resource | Start | Notes |
|----------|-------|-------|
| Money €      | 500     | can go negative; bankruptcy fail state (§14) |
| Listeners    | 40      | the score; compounds reach; drives access (§14) |
| Reputation   | 10      | 0–100 slow multiplier; gates topics/sponsors/pack rarity; club trust |
| Buzz         | 0       | spiky soft currency; card packs |
| Prep points  | 10/week | refills weekly; crew raises cap; unspent lost |

## 6. Topics

Base `appeal` × match context (§4). Contextual topics appear only when triggered.
`slice 1` = first six.

| id | appeal | effort | swing | rep | buzz | appears when | slice |
|----|--------|--------|-------|-----|------|--------------|-------|
| recap     | 1.00 | 3 | 0.15 | +1 | 0  | any match week | 1 |
| preview   | 0.85 | 3 | 0.15 | +1 | 0  | any match week | 1 |
| tierlist  | 1.20 | 4 | 0.25 | +1 | +2 | always | 1 |
| mailbag   | 0.70 | 2 | 0.10 | +2 | 0  | always | 1 |
| optimism  | 1.10 | 3 | 0.20 |  0 | +2 | surprise ≥ +1 | 1 |
| hottake   | 1.55 | 3 | 0.60 | −1 | +5 | surprise ≤ −1, or rep ≥ 15 | 2 |
| ratings   | 1.15 | 3 | 0.30 |  0 | +1 | surprise ≤ −1 | 2 |
| transfers | 1.40 | 4 | 0.45 |  0 | +3 | window open / active saga | 2 |
| tactics   | 0.90 | 6 | 0.12 | +3 | 0  | rep ≥ 25 | 2 |
| prospect  | 1.05 | 4 | 0.30 | +1 | +2 | wonderkid thread / int'l break | 2 |
| schadenfreude | 1.35 | 3 | 0.40 | −1 | +3 | a rival stumbles (§11) | 3 |
| worried   | 1.05 | 4 | 0.25 |  0 | +1 | a rival surges (§11) | 3 |
| scoop     | 2.20 | 4 | 0.30 |  0 | +8 | you hold a scoop, "break it" (§14) | 3 |
| interview | 1.90 | 7 | 0.14 | +4 | +4 | Booker crew, or access tier ≥ 2 | 3 |

## 7. Prep points & production levers

Base 10/wk. Topic prep + Research (narrows swing, +rep; Researcher ×2) + Audio
(+quality, −churn) + Promo (one-week reach, no compounding).

## 8. Story threads & events

**Threads** — multi-week arcs, seeded by game state not RNG. Each stage offers a
high-appeal topic; ignoring a hot thread is a miss, riding drama carries rep
risk. Resolve into a payoff and sometimes a permanent shift.

| Thread | Seeded by | Resolves to |
|--------|-----------|-------------|
| Manager under pressure | 3 losses in 5 | sacked (new boss, strength ±0.04) or backed |
| Star wants out | key player, contract ≤ 1yr, losing | sold (−0.05) or stays (+buzz) |
| Is the kid the real deal? | prospect, 3 good games / wonderkid_goal | breakout (+0.03) or fades |
| Are we actually good? | beat two top-6 clubs | belief (rep +, listeners +) or bubble bursts |
| Is he finished? | repeated howler/keeper_error by a starter | benched/sold or bounces back |
| Transfer saga | RNG during windows | signs / collapses / rival hijacks |
| Takeover talks | RNG, weighted by league position | takeover (+0.08 over a season) or nothing |

**Events** — interrupts with a choice, ~1 every 2–3 weeks: the leak, poaching of
your co-host, going viral for the wrong reason, a rival podcast launch, a live
show offer, a sponsor overstepping. Each trades something for something.

## 9. (folded into §8)

## 10. Squad & player news

~6 generated players `{ name, position, rating 0.40–0.90, age, injuryProne,
contractYears, international }`. Archetypes: talisman, keeper, captain,
wonderkid, crock, deadline-day signing.

Weekly rolls: **injury** (weighted by injuryProne + congestion; key player out →
`keyOut += 0.04–0.08` while out), **international call-up** (during breaks),
**form swings** (nudge sim + flavour), **transfer interest** (during windows).

## 11. Rivals

Each season the game tracks **1–2 rival clubs** with their own mini-arcs: a rival
in your title/relegation race, a rival manager under fire, a rival chasing your
transfer target. Rival events are content:

- rival stumbles → `schadenfreude` topic (high appeal, −rep, +Buzz)
- rival surges → `worried` topic
- fixtures vs. a rival are always derby-importance

## 12. Difficulty = club strength

| Difficulty | strength | outlook | texture | economy |
|------------|----------|---------|---------|---------|
| Casual | 0.72 | title / Europe / cup | glory content, high floors | overhead 16, adRate 0.0050 |
| Regular | 0.52 | mid-table, cup run | default mix | overhead 20, adRate 0.0045 |
| Hard | 0.36 | relegation scrap | crisis dominant, rep swings | overhead 22, adRate 0.0042 |
| Nightmare | 0.26 | survival | damage-control content | overhead 24, adRate 0.0040 |

Club improvement over a run: organic drift toward 0.50 (±0.01/season) + cards
(Top prospect +0.03 / Genius appointment +0.05 / Takeover +0.08) + offseason
transfers. Nightmare → Regular over two seasons is a core arc.

## 13. Resolution formulas

```
# inputs: ppTopic, ppResearch, ppAudio, ppPromo, listeners, reputation
# gear: micQuality (0.20/0.60), editingSkill (0.15/0.45), panelsBonus 0/0.08
# crewAppeal (co-host 0.05), contextMult (§4), passiveGain/moodChurn (§4)

prepRatio = clamp(ppTopic / topic.effort, 0, 1)
overshoot = max(0, ppTopic - topic.effort) * 0.025
gear      = 0.15*micQuality + 0.15*editingSkill
quality   = 0.35 + 0.65*prepRatio + overshoot + gear + 0.035*ppAudio + panelsBonus
quality   = clamp(quality, 0.20, 1.60)

spread    = max(0, topic.swing * (1 - 0.18*ppResearch*researcherMult))
roll      = 1 + uniform(-spread, +spread)

appeal    = topic.appeal * contextMult + crewAppeal
reach     = listeners * appeal * (1 + 0.08*ppPromo) * intlBreakMult * cardReachMult

gross     = reach * (quality - 0.70) * roll * 0.42
wom       = quality > 1 ? listeners * 0.03 * (quality - 1) : 0
churn     = listeners * 0.045 * clamp(1.40 - quality, 0, 1.40)
listeners += round(gross + wom - churn + passiveGain - moodChurn)   # clamp 0

repGain   = topic.rep * quality - (quality < 0.55 ? 4 : 0)
reputation = clamp(reputation + repGain, 0, 100)

perf      = quality * roll
buzz     += perf > 1.15 ? round((perf - 1.15) * 35 * sqrt(appeal)) : 0
buzz     += topic.buzz * (importance != normal ? 2 : 1)

adRevenue = listeners * adRate * (0.80 + 0.40*reputation/100)
hosting   = 5 + listeners * 0.0009
costs     = fixedOverhead + hosting        # + crew monthly every 4 weeks (§15)
money    += adRevenue + sponsorWeekly - costs
```

### Worked examples (Regular, no upgrades)
- **par**, recap ×1.0, prep 3/2/3/2, 40 listeners → q 1.12, reach 46.4,
  listeners +8, rep +1.1, buzz 0, cash −€24.85
- **shock win**, recap ×1.8 → reach 83.5 + passiveGain → listeners +21, buzz +6

## 14. Economy — sponsors, access, scoops, bankruptcy

### Weekly money
See §13. Ad revenue per listener is deliberately tiny — real money is sponsors.

### Sponsors — you pick the deal *and* the target

Offers arrive in an inbox every few weeks, 1–3 at a time, scaled to your size.
You hold **one** deal (two with a later upgrade). Every deal is a **contract with
a milestone** — more pay, harder target, worse miss.

```
SponsorOffer {
  weekly, signingBonus,
  target:  { metric: listeners | avgListeners | reputation, value, byWeek },
  onHit:   { bonus, renewAt, repDelta },
  onMiss:  { endDeal, repDelta (neg), clawback? },
  demands: [ "-rep to sign", "-0.05 appeal while active", "no negativity re: our other client", ... ]
}
```

| Example | Weekly | Target | Hit | Miss |
|---------|--------|--------|-----|------|
| Local café | €30 | 400 listeners in 8 wk | +€150, renew €45 | ends |
| Kit retailer | €55 | reputation 25 in 10 wk | renew €80 | ends, −2 rep |
| Regional betting site | €90 +€100 sign | 600 listeners in 6 wk | +€250, renew €140 | −4 rep, repay signing bonus |
| National brand | €500 | 20,000 listeners in 14 wk | +€2,000, category-partner offer | −8 rep, 6-wk cooldown |

Tension: aggressive deals fix cash flow now but bet on growth you might miss —
and chasing a listener target pushes you toward appeal over reputation, which
then costs rep-gated topics and sponsors.

### Access tiers — audience is leverage

Rolling average listeners (last ~6 episodes) sets your access to the club:

| Tier | Avg listeners | You get | Catch |
|------|---------------|---------|-------|
| 0 Outsider | < 1,000 | react to public news only | — |
| 1 Press pass | 1,000+ | post-match quotes, pressers, small scoops, "we asked the club" topics | — |
| 2 Insider | 12,000+ | scoops arrive *before* news breaks; players & staff talk; occasional interview without a Booker | the club now notices your tone |
| 3 Power broker | 80,000+ | takes move fan sentiment + board pressure; can nudge a youth player's minutes; transfer scoops carry weight | a partnership offer with editorial strings; scorched-earth costs access |

### Scoops (tier 2+)

Periodically you receive advance word on a transfer / manager decision /
contract. Choose:

```
BREAK IT NOW:
  scoop topic (appeal 2.20 base), big Buzz, listener spike
  disruptionChance = 0.15 + 0.10*accessTier + clamp(audience/500000, 0, 0.30)
  if disrupted: rep -6, accessTier -1 for ~8 weeks, fan-backlash thread, churn roll

VERIFY & HOLD:
  small bump now; guaranteed strong recap when it breaks officially
  +3 rep with the club, access protected, "trusted" standing ++ (better future scoops)

TRADE IT:
  give it to a national outlet for money / a favour / future access
```

### Positive influence (tier 2–3)
- backing the manager on-air during a crisis reduces board pressure in that
  thread — if your audience is big enough to matter
- championing a youth player nudges his minutes up (mild strength, feeds the kid thread)
- measured + big + trusted → club offers a **partnership**: official access + a
  small revenue share, but you can't go too negative without losing it

### Bankruptcy
money < −€200 warns; 3 consecutive weeks below → run ends. Real threat in year
one, non-issue once a national sponsor signs.

## 15. Crew — start alone, build a team

Hire into a role slot once you can carry the **monthly** cost (lump every 4 weeks).

| role | €/month | effect |
|------|---------|--------|
| Co-host | 180 | +3 prep/wk, +0.05 appeal |
| Producer | 220 | +2 prep/wk, −0.5 effort on every topic |
| Researcher | 200 | Research lever ×2, +rep on analysis |
| Clips manager | 240 | +30% Buzz earned, +1%/wk passive reach |
| Booker | 260 | unlocks Big interview, more guest events |

Events can threaten crew (poaching) or raise costs (counter-offers).

## 16. Gear upgrades (one-time)

| upgrade | cost | effect | slice |
|---------|------|--------|-------|
| XLR mic | €150 | micQuality 0.20 → 0.60 | 1 |
| Acoustic panels | €120 | quality floor +0.08 | 1 |
| Editing software | €200 | editingSkill 0.15 → 0.45 | 1 |
| Faster PC | €400 | +1 usable prep point/wk | 2 |
| Second sponsor slot | €600 | hold two sponsor deals | 3 |
| Studio space | €900 +€30/mo | +2 prep points, reach ×1.08 | 3 |

## 17. Cards & packs (slice 2+)

Hand of 5. Milestone rewards or 3-card packs for 60 Buzz. Reputation improves
rarity. Mostly one-shot; a few permanent shifts. (Full list unchanged from v0.2:
Exclusive scoop, Viral moment, All-nighter, Ghostwriter, Damage control, Clip
farm, Top prospect emerges, Genius appointment, Takeover talks, Evergreen segment.)

## 18. Goal & milestones — endless

Primary goal **50,000 average listeners** → "you made it" beat → continue
prompt → endless logarithmic ladder that scales forever.

100 · 500 · 2,500 · 10,000 · **50,000 ★** · 100k · 250k · 500k · 1M (buyout
decision) · 2.5M · 5M · 10M · … Each rung past 2,500 pays a card pack + Buzz + a
flavour unlock.

## 19. Between runs

Two unlock tracks, both from achievements:

- **Cosmetics** — always free, no gameplay effect: cover art, studio skins,
  intro stingers, UI themes, mic models, on-air signs.
- **Custom modifiers** — toggles set before a run: +1 base prep · churn −20% ·
  start +€300 · bankruptcy grace 5 · no international breaks · sponsor-free
  (higher ad rate) · chaos cycle (more events, wilder results) · sandbox (no
  bankruptcy). Enabling any flags the run **Custom** — the endless chase still
  works for your own satisfaction, Custom runs sit on a separate board.

## 20. Tuning knobs

startMoney 500 · startListeners 40 · prepBase 10 · adRate 0.0045 (by difficulty)
· churnRate 0.045 · qualityBreakeven 0.70 · deltaScale 0.42 · womRate 0.03 ·
fixedOverhead 20 (by difficulty) · hostingSlope 0.0009 · buzzThreshold 1.15 ·
buzzScale 35 · packCost 60 · bankruptcyFloor/grace −200 / 3 · goalListeners 50000
· homeAdvantage 0.06 · surpriseWinReach 1.8 · offseasonChurn 0.15 ·
crewMonthlyInterval 4wk · accessTierListeners [1000, 12000, 80000] ·
scoopDisruptionBase 0.15 · avgListenerWindow 6

## 21. Build order

- **Slice 1 — react to your club.** Setup (name/colours/difficulty) · calendar +
  minimal sim (W/D/L vs expectation, surprise) · context topic modifiers · 6
  topics · prep + 4 levers · resolution + economy + bankruptcy · 3 gear + co-host
  · milestone popups. No threads/events/squad/cards/sponsors/scoops. **Question:
  is "result comes in → you make the episode" fun every week?**
- **Slice 2 — texture & risk.** Match scorelines + moments · threads · squad ·
  events · cards + packs + Buzz · hottake/ratings/transfers/tactics/prospect ·
  **sponsor contracts** · access tiers 0–1 · Researcher/Producer/Clips crew.
- **Slice 3 — the long game.** Access tiers 2–3 + scoops + positive influence +
  partnership · rivals · cup + European · offseason · full crew + Booker +
  interviews · club-improvement cards · national/category sponsors · second
  sponsor slot · endless milestones · 1M buyout · season review · cosmetics +
  custom modifiers.

## 22. Still open (small)

- Scoop frequency at tier 2 — how many per season feels right?
- Does the partnership's editorial constraint feel like a meaningful choice or
  just a nerf? Playtest.
- Rival count — fixed at 2, or scale with league size?
- Match-moment list — is 9 tags enough, and do any need dedicated topics?
