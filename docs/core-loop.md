# Podcast Tycoon — core loop spec (v0.2)

Readable version with a live tuning bench:
https://claude.ai/code/artifact/18bdfff0-e182-4e36-a5f1-cbb9834d6348

Canonical numbers for implementation. All values first-pass, expected to move in
playtesting. Keep this file and the artifact in sync.

**v0.2 adds the club:** your team plays a simulated season, and its results,
fixtures, squad news, and story threads are what each episode is *about*.

---

## 1. Setup — your team & podcast

Chosen once at the start of a run:

- **Podcast name** (text)
- **Club name** (text)
- **Club colours** — primary + secondary. Drive the UI accent, the matchday
  scoreboard, and the episode-cover template.
- **League flavour** — generic ("the top division") or a named set. Default generic.
- **Difficulty** — sets your club's strength tier (see §12). This is the single
  biggest choice: it decides whether you're covering a title race or a
  relegation scrap, which changes *what content exists*, not just the numbers.
- *(stretch)* **Supporter type** — lifelong / glory-hunter / long-suffering.
  Small starting modifier + flavour.

On start, generate: a ~20-club league table with strengths, your squad of ~6
notable players (§10), a full season fixture list (§2), €500 / 40 listeners /
rep 10 / 0 Buzz, no crew, no sponsors.

## 2. The season = the game's year

One turn = one week = one episode. A season is ~46 turns:

| Block | Turns | Notes |
|-------|-------|-------|
| League | 38 | one match most weeks; busy weeks bundle a midweek + weekend result into one episode |
| Cup | up to 6 | knockout; early rounds low-stakes, later rounds are event weeks |
| European | 0 / 6–13 | only if qualified (Casual/Regular likely, Hard rarely); group + knockout |
| International breaks | ~4 | no club match; topic pool shifts, weekly reach ×0.8 |
| Offseason | 3–4 | transfer window = peak Buzz, listener dip; sagas resolve; club tier re-evaluated |

Audience carries between seasons minus an offseason churn (~15%).

## 3. The week

1. **The week happens** (sim, before you decide anything)
   - calendar advances; match(es) simulated (§4)
   - squad news rolls (§10); story threads tick (§8); an event may fire (§9)
2. **Offer** — 3 topics drawn from a pool now *shaped by* this week's result,
   active threads, squad news, and calendar slot. Redraw once for €15 / 1 Buzz.
3. **Produce** — spend prep points (base 10) on topic + Research / Audio / Promo.
4. **Cards & events** — play up to 2 cards; resolve any pending event choice.
5. **Publish** — resolution (§11). Results screen also shows league position,
   next fixture, thread movement.

## 4. Match simulation & context

### Sim (light)

```
teamStrength   # 0..1, from difficulty tier, drifts + improves over a run (§12)
oppStrength    # per fixture, from the generated league table, ~0.50 mean
home           # bool
keyOut         # sum of strength penalties from injured/suspended key players (§10)

d      = (teamStrength - keyOut) - oppStrength + (home ? 0.06 : -0.06)
pWin   = clamp(0.45 + 0.85*d, 0.05, 0.88)
pLoss  = clamp(0.45 - 0.85*d, 0.05, 0.88)
pDraw  = max(0.10, 1 - pWin - pLoss)          # renormalize the three
outcome = weighted pick → { W/D/L, goalsFor, goalsAgainst }

expectation = pWin > 0.55 ? "expWin" : pLoss > 0.55 ? "expLoss" : "tossup"
surprise:
  +2 heroic    won when expLoss
  +1 good      won when tossup  |  drew when expLoss
   0 par       result matches expectation
  -1 poor      lost when tossup |  drew when expWin
  -2 disaster  lost when expWin
```

### Fixture importance

Drawn from the calendar: `normal`, `big-match`, `derby`, `six-pointer`,
`cup-knockout`, `european-night`, `final`. Everything except `normal` applies
`importanceMult 1.3` to topic appeal and doubles Buzz for the week.

### Context → the episode

```
# topic appeal multiplier by surprise (applied on top of base appeal, §6)
surprise -2 : recap 0.7 · crisis/hottake 2.0 · player-ratings 1.9
surprise -1 : recap 0.9 · crisis/hottake 1.5
surprise  0 : ~1.0 across the board  (evergreen/mailbag/tactics weeks)
surprise +1 : recap 1.4 · optimism 1.4
surprise +2 : recap 1.8 · optimism 1.8 · Buzz bonus

international break : club topics ×0.55 · international/prospect/transfer ×1.35 · weekly reach ×0.8

# listener mood — fans react to the result regardless of your episode
surprise +2 : passiveGain = listeners * 0.020
surprise +1 : passiveGain = listeners * 0.010
surprise -1 : moodChurn   = listeners * 0.010 * clamp(1.10 - quality, 0, 1.10)
surprise -2 : moodChurn   = listeners * 0.025 * clamp(1.20 - quality, 0, 1.20)
```

The design intent: **handle a crisis well and you grow from it; ignore it or
fumble it and fans leave.** A great win floats all boats.

## 5. Resources

| Resource     | Start   | Notes |
|--------------|---------|-------|
| Money €      | 500     | can go negative; bankruptcy fail state (§13) |
| Listeners    | 40      | the score; compounds reach |
| Reputation   | 10      | 0–100 slow multiplier; gates topics/sponsors/pack rarity |
| Buzz         | 0       | spiky soft currency; buys card packs |
| Prep points  | 10/week | refills weekly; unspent are lost; crew raises the cap |

## 6. Topics

Base `appeal` is multiplied by the match context (§4). Base `effort`, `swing`
(narrowed by Research), `rep`, `buzz` as before. Contextual topics only appear
when their trigger is active.

| id         | appeal | effort | swing | rep | buzz | appears when                         | slice |
|------------|--------|--------|-------|-----|------|--------------------------------------|-------|
| recap      | 1.00   | 3      | 0.15  | +1  | 0    | any week with a match                | 1 |
| preview    | 0.85   | 3      | 0.15  | +1  | 0    | any week with a match                | 1 |
| tierlist   | 1.20   | 4      | 0.25  | +1  | +2   | always                               | 1 |
| mailbag    | 0.70   | 2      | 0.10  | +2  | 0    | always                               | 1 |
| optimism   | 1.10   | 3      | 0.20  | 0   | +2   | surprise ≥ +1                         | 1 |
| hottake    | 1.55   | 3      | 0.60  | −1  | +5   | surprise ≤ −1, or reputation ≥ 15     | 2 |
| ratings    | 1.15   | 3      | 0.30  | 0   | +1   | surprise ≤ −1                         | 2 |
| transfers  | 1.40   | 4      | 0.45  | 0   | +3   | transfer window, or active saga      | 2 |
| tactics    | 0.90   | 6      | 0.12  | +3  | 0    | reputation ≥ 25                       | 2 |
| prospect   | 1.05   | 4      | 0.30  | +1  | +2   | wonderkid thread active / int'l break | 2 |
| interview  | 1.90   | 7      | 0.14  | +4  | +4   | Booker crew member (§14)              | 3 |

## 7. Prep points & production levers

Base 10/week. Topic prep + three levers:

| Where points go | Effect | Formula hook |
|-----------------|--------|--------------|
| Topic prep | Meeting Effort = "solid"; overshoot adds a little, sharp diminishing returns | prepRatio, overshoot |
| Research | Narrows the swing, small +rep. Researcher crew doubles effectiveness | spread ×(1 − 0.18·n) |
| Audio | +quality directly, softens churn | +0.035·n to quality |
| Promo | One-week reach boost only, no compounding | reach ×(1 + 0.08·n) |

## 8. Story threads — the light story system

A thread is a multi-week arc with a topic hook that escalates. **Seeded by game
state, not pure RNG** — that's the "sense". Each stage lasts 1–3 weeks and
offers a high-appeal contextual topic; ignoring a hot thread is a missed
opportunity, covering drama threads carries rep risk. Threads resolve into a
payoff episode and sometimes a permanent shift.

| Thread | Seeded by | Stages | Resolution |
|--------|-----------|--------|------------|
| Manager under pressure | 3 losses in 5 | rumours → board silence/backing → decision | sacked (new manager, strength ±0.04) or backed |
| Star wants out | key player, contract ≤ 1yr, club losing | links → agent quotes → deadline | sold (strength −0.05, €topic) or stays (+buzz) |
| Is the kid the real deal? | prospect, 3 good games | hype → first start → big test | breakout (strength +0.03) or fades |
| Are we actually good? | beat 2 top-6 clubs | "flat-track bullies?" → top clash → verdict | belief (rep +, listener bump) or bubble bursts |
| Transfer saga | RNG during windows | linked → talks → hijack rumour → done | signs / collapses / rival signs him |
| Boardroom / takeover | RNG, weighted by league position | speculation → due diligence → announcement | takeover (strength +0.08 over a season, money topics) or nothing |

## 9. Events — interrupts with choices

Fire between phases, ~1 every 2–3 weeks. Each is a choice with tradeoffs.

- **The leak** — an agent offers an exclusive. Publish (+buzz, +appeal now / −rep, backlash roll) · sit on it (+rep with club, future access) · pass it to a rival (they owe you).
- **Poaching** — local radio wants your co-host. Match the offer (monthly cost up) · let them go (−prep, lose the appeal bonus) · counter with a title bump (rep +).
- **Going viral for the wrong reason** — a clip of you is out of context. Apologise (−buzz, +rep) · double down (+buzz, −rep, churn roll) · ignore (thread may spawn).
- **Rival launch** — a new podcast on your beat. Diss episode (+buzz, −rep, fun) · collab (both grow, split buzz) · ignore (they chip listeners for a few weeks).
- **Fan meetup / live show offer** — spend € + a week's prep for a one-off listener + buzz spike, or decline.
- **Sponsor oversteps** — your sponsor wants editorial control. Refuse (lose the deal, +rep) · comply (keep €, −rep, −appeal for 3 weeks).

## 10. Squad & player news

~6 generated notable players: `{ name, position, rating 0.40–0.90, age,
injuryProne, contractYears, international }`. Archetypes: talisman, keeper,
captain, wonderkid, crock (injury-prone), deadline-day signing.

Weekly rolls:
- **Injury** — weighted by `injuryProne` + fixture congestion. Key player out
  → `keyOut += 0.04–0.08` for the recovery weeks; spawns anxiety topics.
- **International call-up** — during international breaks; prospect call-up feeds
  the wonderkid thread.
- **Form swing** — hot/cold streaks nudge match sim and topic flavour.
- **Transfer interest** — during windows; feeds "Star wants out".

## 11. Resolution formulas

```
# inputs: ppTopic, ppResearch, ppAudio, ppPromo, listeners, reputation
# gear:   micQuality (0.20 base / 0.60 mic), editingSkill (0.15 base / 0.45 sw)
# panelsBonus 0/0.08 · crewAppeal (co-host 0.05) · contextMult from §4
# surprise-derived passiveGain / moodChurn from §4

prepRatio = clamp(ppTopic / topic.effort, 0, 1)
overshoot = max(0, ppTopic - topic.effort) * 0.025
gear      = 0.15*micQuality + 0.15*editingSkill
quality   = 0.35 + 0.65*prepRatio + overshoot + gear + 0.035*ppAudio + panelsBonus
quality   = clamp(quality, 0.20, 1.60)

spread    = max(0, topic.swing * (1 - 0.18*ppResearch * researcherMult))   # researcherMult 1 or 2
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
buzz     += topic.buzz + (importance != normal ? topic.buzz : 0)   # importance doubles topic buzz

adRevenue    = listeners * 0.0045 * (0.80 + 0.40*reputation/100)
hosting      = 5 + listeners * 0.0009
costs        = 20 + hosting                              # + crew monthly, deducted every 4 weeks (§14)
money       += adRevenue + sponsorWeekly - costs
```

### Worked example (week 1, Regular difficulty, no upgrades, par result)
topic recap, contextMult 1.0, prep 3 / research 2 / audio 3 / promo 2, listeners 40
→ quality 1.12 · swing ±0.10 · reach 46.4
→ listeners +8 (→48) · reputation +1.1 · buzz 0
→ ad €0.15 − costs €25 = −€24.85 (→ €475)

### Same week, but a shock win (surprise +2, recap contextMult 1.8)
→ reach 83.5 · passiveGain +0.8
→ listeners +21 (→61) · buzz +6

## 12. Difficulty = club strength

| Difficulty | teamStrength | Season outlook | Content texture | Economy |
|------------|--------------|----------------|-----------------|---------|
| Casual | 0.72 | title / Europe / cup runs | glory content, high floors | forgiving (overhead 16, adRate 0.0050) |
| Regular | 0.52 | mid-table, cup run | the default mix | baseline (overhead 20, adRate 0.0045) |
| Hard | 0.36 | relegation scrap | crisis/drama dominant, rep swings | tight (overhead 22, adRate 0.0042) |
| Nightmare | 0.26 | survival | mostly damage-control content | brutal (overhead 24, adRate 0.0040) |

**Club improvement over a run:** organic drift toward 0.50 (±0.01/season), plus
cards ("Top prospect emerges" +0.03 & spawns wonderkid thread · "Takeover" +0.08
over a season · "Genius appointment" +0.05 & manager thread), plus offseason
transfer outcomes. A Nightmare club that survives two seasons can become a
Regular one — that arc is a big part of the appeal.

## 13. Economy — sponsors & bankruptcy

| sponsor          | listeners | rep | €/week | note |
|------------------|-----------|-----|--------|------|
| Local brand      | 500       | 20  | 40     | 6-week deal; saves the first run |
| Betting app      | 2,500     | 30  | 120    | −5 rep to accept |
| National sponsor | 15,000    | 50  | 500    | renews on better terms |
| Category partner | 75,000    | 60  | 2,000  | exclusivity clause |

**Bankruptcy:** money < −€200 → warning. 3 consecutive weeks < −€200 → run ends.

## 14. Crew — start alone, build a team

You begin solo. Hire into role slots once you can carry the **monthly** cost
(deducted as a lump every 4 weeks, so it stings).

| role | €/month | effect |
|------|---------|--------|
| Co-host | 180 | +3 prep/wk, +0.05 appeal |
| Producer | 220 | +2 prep/wk, −0.5 effort on every topic |
| Researcher | 200 | Research lever twice as effective, +rep on analysis topics |
| Clips manager | 240 | +30% Buzz earned, +1%/wk passive reach |
| Booker | 260 | unlocks Big interview topic, more guest events |

Events can threaten crew (poaching) or raise their cost (counter-offers).

## 15. Upgrades (gear — one-time)

| upgrade | cost | effect | slice |
|---------|------|--------|-------|
| XLR mic | €150 | micQuality 0.20 → 0.60 | 1 |
| Acoustic panels | €120 | quality floor +0.08 | 1 |
| Editing software | €200 | editingSkill 0.15 → 0.45 | 1 |
| Faster PC | €400 | +1 usable prep point/wk | 2 |
| Studio space | €900 | +2 prep points, reach ×1.08 (adds €30/mo) | 3 |

## 16. Cards & packs (slice 2+)

Hand of 5. Earned at milestones or bought as 3-card packs for 60 Buzz.
Reputation improves rarity odds. Most are one-shot; a few are permanent club or
show shifts.

| card | effect | rarity |
|------|--------|--------|
| Exclusive scoop | +0.5 appeal this episode | common |
| Viral moment | reach ×1.5 this episode | common |
| All-nighter | +4 prep now, −3 next week | common |
| Ghostwriter | topic prep counts as fully met | uncommon |
| Damage control | floor this episode's roll at 1.0 | uncommon |
| Clip farm | ×2 Buzz from over-performing this episode | uncommon |
| Top prospect emerges | club strength +0.03, spawns wonderkid thread | rare |
| Genius appointment | club strength +0.05, spawns manager thread | rare |
| Takeover talks | club strength +0.08 over the season, unlocks money topics | rare |
| Evergreen segment | permanent +0.08 quality floor | rare |

## 17. Goal & milestones

Primary goal **50,000 average listeners** → "you made it" beat → continue
prompt → endless logarithmic milestone ladder. Difficulty reframes the story
(Nightmare: hit 50k while the club fights relegation), the number is the same.

100 · 500 (Local sponsor) · 2,500 (+pack) · 10,000 (+pack, +40 Buzz) ·
**50,000 ★ goal** · 100k · 250k · 500k · 1M (buyout decision) · 2.5M+ endless.

## 18. Between runs — cosmetics only

Milestones and achievements unlock cosmetics that persist across runs, **no
gameplay effect**: podcast cover-art styles, studio backdrop skins, intro
stinger sounds, UI theme variants, mic models, "on air" sign styles.

## 19. Tuning knobs

| knob | default | controls |
|------|---------|----------|
| startMoney | 500 | opening runway |
| startListeners | 40 | length of first climb |
| prepBase | 10 | tightness of the weekly decision |
| adRate | 0.0045 (by difficulty) | passive income slope |
| churnRate | 0.045 | how fast a bad run bleeds audience |
| qualityBreakeven | 0.70 | quality below this loses listeners |
| deltaScale | 0.42 | overall growth speed |
| womRate | 0.03 | bonus growth from standout episodes |
| fixedOverhead | 20 (by difficulty) | baseline weekly cost pressure |
| hostingSlope | 0.0009 | how much scale costs |
| buzzThreshold | 1.15 | how good an episode must be to earn Buzz |
| packCost | 60 | Buzz per 3-card pack |
| bankruptcyFloor / grace | −200 / 3 weeks | fail-state forgiveness |
| goalListeners | 50000 | primary goal |
| homeAdvantage | 0.06 | match sim swing for home fixtures |
| surpriseWinReach | 1.8 | how much a shock result inflates the recap |
| offseasonChurn | 0.15 | audience lost between seasons |
| crewMonthlyInterval | 4 weeks | how often crew wages hit |

## 20. Build order

- **Slice 1 — react to your club.** Setup screen (name/colours/difficulty) ·
  season calendar + minimal match sim (W/D/L vs expectation, surprise rating) ·
  match-context topic modifiers · 6 topics · prep + 4 levers · resolution +
  economy + bankruptcy · 3 gear upgrades + co-host · milestone popups.
  **No threads, events, squad, cards, sponsors, crew beyond co-host.**
  Question: is "the result comes in, you make the episode" fun every week?
- **Slice 2 — texture & risk.** Story threads · squad model + player news ·
  events · cards + packs + Buzz economy · Hot take / ratings / transfers /
  tactics / prospect topics · Local + Betting sponsors · Researcher + Producer
  + Clips crew.
- **Slice 3 — the long game.** Cup + European competitions · offseason +
  transfer window · full crew + Booker + interviews · club-improvement cards ·
  national + category sponsors · full + endless milestones · 1M buyout ·
  season-review screen · cosmetic unlocks.

## 21. Still open

- Match sim depth: pure W/D/L + surprise (current plan) vs. a bit of scoreline /
  xG flavour for topic hooks.
- Do rival clubs' storylines matter (a rival's crisis = content for you)?
- Offseason length and whether the player makes any transfer *decisions* or just
  reacts to them.
- Multiple seasons: soft cap, or genuinely endless with scaling milestones?
