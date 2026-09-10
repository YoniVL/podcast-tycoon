# Podcast Tycoon — core loop spec (v0.4)

Readable version with a live tuning bench:
https://claude.ai/code/artifact/18bdfff0-e182-4e36-a5f1-cbb9834d6348

Canonical numbers for implementation. **All v0.4 values are first-pass and
un-playtested** — expect them to move. Keep this file and the artifact in sync.

- **v0.2** made your club's simulated season the spine of the loop.
- **v0.3** added sponsors as milestone-target contracts, audience as leverage
  (access tiers + scoops), rival storylines, match moments, custom-run modifiers.
- **v0.4** is the **depth pass**. The week was two clicks — pick one topic, nudge
  three sliders, publish — and it always grew you. v0.4 makes *building the
  episode* the game:
  - the episode is a **rundown of three segments**, each with a chosen **angle**;
  - a short **interactive recording phase** where choices arise mid-show;
  - a **push dial** — the weekly gamble, with visible backfire odds;
  - the audience splits into **Core / Casual / Clip-only**, each with its own
    growth, churn and money;
  - three new stats — **Credibility**, **Social Reach**, **Morale** — that turn
    "spicy vs. serious" and "loud vs. trusted" into the central long-game choices;
  - production risk reworked so the **safe play grows you slowly** and real
    growth needs bets that can miss (**freshness**, **slumps**, **overreach**);
  - **tiered upgrade tracks** instead of six flat purchases;
  - crew becomes a **roster** — candidates with traits and wages, hire *and* fire.

---

## 1. Setup — your club & podcast

Chosen once at the start of a run:

- **Podcast name**, **club name** — free text.
- **Club colours** — primary + secondary. Drive the UI accent, the matchday
  scoreboard, the episode-cover template.
- **League flavour** — generic or a named set. Default generic.
- **Difficulty** — sets club strength (§16). The biggest choice: title race vs.
  relegation scrap changes *what content exists*, not just the math.
- *(stretch)* **Supporter type** — lifelong / glory-hunter / long-suffering.

On start: generate a ~20-club league with strengths, your squad of ~6 notable
players (§14), a full fixture list, 1–2 tracked rivals (§15). Begin with
€500 / 40 Core listeners / 0 Casual / 0 Clip-only / rep 10 / credibility 40 /
social reach 5 / morale 70 / 0 Buzz, no crew, no sponsor, a bare-corner set.

## 2. The season = the game's year

One turn = one week = one episode. A season ≈ 46 turns. **Endless** — seasons
repeat indefinitely; club strength, league quality, and milestones scale with
you.

| Block | Turns | Notes |
|-------|-------|-------|
| League | 38 | one match most weeks; busy weeks bundle midweek + weekend into one episode |
| Cup | up to 5 | knockout; late rounds are event weeks |
| European | 0 / 6 | league phase, only if qualified |
| International breaks | ~4 | no club match; topic pool shifts; weekly reach ×0.8 |
| Offseason | 3 | transfer window = peak Buzz, Casual dip; sagas resolve; club tier re-evaluated. **You only react — no transfer decisions.** |

Audience carries between seasons minus `offseasonChurn` (Core −8%, Casual −22%,
Clip-only −30%). Credibility and Reputation carry fully. Social Reach decays
toward a floor set by your size.

## 3. The week

1. **The brief** (sim) — calendar advances; match simulated (§4); squad news
   rolls (§14); threads tick (§13); an event or scoop may fire (§13, §18). A
   one-screen summary: result, moments, what's moving.
2. **The offer** (§6) — 6 topics from a pool shaped by result, moments, threads,
   squad news, rivals, calendar slot. Redraw once for €15 / 1 Buzz.
3. **The rundown** (§7) — fill three segment slots (Main / Second / Recurring),
   each with a topic and an **angle**. Optional: book a **guest** into a slot;
   set the **push dial** (§11).
4. **Prep** (§9) — spend prep points (base 12) across the three segments +
   Research / Audio / Promo. You cannot fully prep everything.
5. **Record** (§10) — 2–3 interactive beats arising from the rundown, crew,
   push, guest, context. Each is a quick choice that modifies the result and
   sometimes seeds a thread or event.
6. **Publish** — resolution (§17).
7. **How it landed** — a short beat, not a wall of numbers: headline audience
   move, best clip, a standout listener reaction, whether the push paid off,
   thread + rival + sponsor-target movement. Numbers available on tap.

## 4. Match simulation & context

### Sim

```
teamStrength   # 0..1 from difficulty (§16), drifts + improves over a run
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

### Match moments

Sim emits **1–2 moments** tagged: `screamer`, `howler`, `red_card`,
`penalty_drama`, `wonderkid_goal`, `keeper_error`, `last_minute`, `masterclass`,
`capitulation`. Moments sharpen topic hooks, seed threads, and are the most
common trigger for recording beats (§10). A strong moment can shift context one
step.

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

# listener mood — fans react regardless of your episode (applied to Casual + Core)
+2 : passiveGain = audience * 0.020
+1 : passiveGain = audience * 0.010
-1 : moodChurn   = audience * 0.010 * max(0, 1.10 - quality)
-2 : moodChurn   = audience * 0.025 * max(0, 1.20 - quality)
```

Intent: handle a crisis well → you grow from it; fumble or ignore it → fans leave.

## 5. Resources & stats

Nine things to manage. Money and Buzz are spendable currencies; the audience is
the score; the four 0–100 indices (Reputation, Credibility, Social Reach, Morale)
are slow levers that gate content and shape every formula.

### Currencies & capacity

| Resource | Start | Notes |
|----------|-------|-------|
| Money €      | 500     | can go negative; bankruptcy fail state (§18) |
| Buzz         | 0       | spiky soft currency; card packs, redraws |
| Prep points  | 12/week | refills weekly; crew + upgrades raise the cap; unspent lost |

### The audience — three segments (§17 for the maths)

| Segment | Base churn/wk | Grows from | Monetises | Cares about |
|---------|---------------|------------|-----------|-------------|
| **Core** | 1.5% | Casual converting when you're consistent, credible, on-format | best (high ad value; merch/subs later) | quality, consistency, Credibility, format identity |
| **Casual** | 7% | reach, appeal, buzz, virality, breakouts | modest | being entertained, appeal, buzz |
| **Clip-only** | 12% | Social Reach, viral moments, comedy/hot-take clips | poorly (tiny ad value) | nothing — they never hear the show; they feed Casual and amplify virality |

- **Listeners = Core + Casual.** This is the headline number: milestones (§22),
  the goal, access tiers (§18), and most sponsor targets read Listeners (or its
  rolling average).
- **Reach = Listeners + Clip-only.** Engagement-focused sponsors and Social-Reach
  effects read Reach.
- Weekly conversions: Casual→Core `casual * coreConvert` (1–3%, up with
  consistency + quality + Credibility); Clip→Casual `clip * clipConvert`
  (0.5–1.5%, up when the show is listenable, i.e. format identity toward
  Newsy/Analysis). Betray your format or tank Credibility and Core leaks back to
  Casual, then churns.
- Strategic identity falls out of the mix: a Core-heavy show is smaller but
  financially stable and slump-resistant; a Clip-heavy show is huge, broke, and
  one bad take from a collapse.

### The four indices

| Stat | Start | What it *is* | Built by | Burned by | Gates / effects |
|------|-------|--------------|----------|-----------|-----------------|
| **Reputation** | 10 | how established / known you are in football media | rep-earning topics, analysis, tactics, cup runs, milestones | sloppy episodes (q < 0.55), relegation | rep-gated topics & sponsors, pack rarity, ad-rate multiplier, market ceiling (§17) |
| **Credibility** | 40 | how much people **trust** what you say | analysis & investigation angles, verified scoops, takes that age well, graceful corrections | missed scoops broken anyway, hot takes that age badly, platforming a guest who lies, self-contradiction / whiplash | Core growth & retention; scoop potency `×clamp(cred/60, 0.2, 1.6)`; access tier 2–3 has a Credibility floor; national/serious sponsors gated; partnership needs cred ≥ 65; interview quality |
| **Social Reach** | 5 | how loud your megaphone is — clips, posts, the discourse | Clips crew, comedy/hot-take angles, viral moments, the Distribution track, spicy recording-beat wins | decay 1.5/wk toward a size floor if not fed | Clip-only growth; **viral amplification** — breakouts *and* backfires scale `×(1 + socialReach/100 * 1.4)`; engagement sponsors; feeds the discourse event stream (opportunities *and* pile-ons) |
| **Morale** | 70 | the team's energy and goodwill (studio-wide; per-employee in §19) | wins (club, milestone, sponsor hit, viral W), raises/bonuses, a **light week**, nice set/space, resolving conflict | overwork (bonus episodes, back-to-back high push, crisis crunch), underpay vs. your size, public failures, firing someone, diva conflict | < 40: −quality, −appeal, "phoned-in" episodes, employees may quit. > 80: small q/appeal bonus, employees decline outside offers. Below a floor you **cannot** run a high push or a bonus episode without tanking it further |

**The central axis of v0.4:** the fast way to grow **Social Reach** (spice, hot
takes, picking fights) corrodes **Credibility**; the fast way to grow
**Credibility** (measured analysis, holding scoops) is slow for Social Reach.
Casual/Clip growth leans on the first, Core growth on the second. Reputation sits
above both as "are you a known quantity." You are always choosing which of the
three you're feeding this week.

## 6. The offer

**Six** topics each week (was 3). Sources, in priority order:

1. a **broken scoop** topic, if you chose "break it now" (§18)
2. **thread** stage topics (§13), up to 2
3. **context** topics unlocked this week (schadenfreude if a rival stumbled,
   transfers during a window, etc.)
4. **squad-news** topics (a returning talisman, a wonderkid's third good game)
5. shuffled **evergreen catalog** topics to fill to six

Redraw the whole offer once for €15 / 1 Buzz. Each topic carries a `family` tag
(match / club / transfer / player / meta / drama) used by the freshness rule
(§12) and the one-note clash (§8).

Base `appeal` × match context (§4). `slice 1` = first six.

| id | appeal | effort | swing | rep | cred | buzz | appears when | slice |
|----|--------|--------|-------|-----|------|------|--------------|-------|
| recap     | 1.00 | 3 | 0.15 | +1 | +0.3 | 0  | any match week | 1 |
| preview   | 0.85 | 3 | 0.15 | +1 | +0.3 | 0  | any match week | 1 |
| tierlist  | 1.20 | 4 | 0.25 | +1 |  0   | +2 | always | 1 |
| mailbag   | 0.70 | 2 | 0.10 | +2 | +0.5 | 0  | always | 1 |
| optimism  | 1.10 | 3 | 0.20 |  0 |  0   | +2 | surprise ≥ +1 | 1 |
| hottake   | 1.55 | 3 | 0.60 | −1 | −1.5 | +5 | surprise ≤ −1, or rep ≥ 15 | 2 |
| ratings   | 1.15 | 3 | 0.30 |  0 |  0   | +1 | surprise ≤ −1 | 2 |
| transfers | 1.40 | 4 | 0.45 |  0 | −0.5 | +3 | window open / active saga | 2 |
| tactics   | 0.90 | 6 | 0.12 | +3 | +1.5 | 0  | rep ≥ 25 | 2 |
| prospect  | 1.05 | 4 | 0.30 | +1 | +0.5 | +2 | wonderkid thread / int'l break | 2 |
| schadenfreude | 1.35 | 3 | 0.40 | −1 | −1.0 | +3 | a rival stumbles (§15) | 3 |
| worried   | 1.05 | 4 | 0.25 |  0 |  0   | +1 | a rival surges (§15) | 3 |
| scoop     | 2.20 | 4 | 0.30 |  0 | +0/−  | +8 | you hold a scoop, "break it" (§18) | 3 |
| interview | 1.90 | 7 | 0.14 | +4 | +2.0 | +4 | Booker crew, or access tier ≥ 2 | 3 |
| investigation | 1.60 | 6 | 0.35 | +2 | +3.0 | +3 | Researcher crew + a live thread | 4 |
| explainer | 0.95 | 4 | 0.10 | +2 | +1.5 | 0  | always | 4 |

## 7. The rundown — segments & angles

The episode is **three slots**. A fourth (Cold Open) unlocks via the Post track
(§20) or a card.

| Slot | Role | Reach weight | Typical prep |
|------|------|--------------|--------------|
| **Main** | the week's big story | 0.60 | 5–8 |
| **Second** | a B-story on a *different* subject | 0.30 | 3–5 |
| **Recurring bit** | mailbag / tier list / "state of the club" — cheap, and it's what builds your **format identity** over a run | 0.10 | 1–3 |
| *(Cold Open)* | 60-second hook; sets the episode's tone for clash rules | 0.05 | 1–2 |

For each slot: pick a topic from the offer, then pick an **angle**. Reach and
quality are computed per segment and combined weighted (§17); rep / cred / social
/ buzz deltas sum across segments then apply the interaction modifiers (§8).

### Angles

| Angle | appeal | swing | rep | cred | social | buzz | notes |
|-------|--------|-------|-----|------|--------|------|-------|
| **Analysis** | ×0.95 | ×0.6 | +1.5 | +2.0 | +0 | ×0.8 | safe; the Core-builder. Researcher boosts it. |
| **Hot take** | ×1.45 | ×1.8 | −1.5 | −2.5 | +4 | ×1.6 | the Casual/Clip engine and the Credibility drain. Feeds backfire odds (§11). |
| **Emotional / fan** | ×1.20 | ×1.1 | +0 | +0 | +2 | ×1.1 | lands hard when `|surprise| ≥ 1`, flat otherwise. Whiplash risk after a bit. |
| **Comedy bit** | ×1.10 | ×0.9 | +0 | −0.5 | +3 | ×1.4 | big Clip driver. **Bombs** (appeal ×0.5, −morale) if it directly follows a Crisis-response segment. |
| **Investigation** | ×1.35 | ×1.3 | +2 | +3.5 | +2 | ×1.2 | needs Researcher or access ≥ 2. Can force a thread to its next stage. High prep. |

Guest in a slot (§19 Booker / access ≥ 2): +appeal, +buzz, a chance of a strong
"quote" moment (clip + social) or a bad one (you platformed it — cred hit). Guest
types: pundit (analysis boost, rep+), insider (scoop chance, access risk), fan
favourite (buzz+, chaotic — more recording beats), rival podcaster (spicy, huge
buzz, beef-thread risk).

## 8. Angle interactions — combos & clashes

Applied after per-segment resolution, before finalising deltas.

**Clashes (penalties):**

| Rule | Trigger | Effect |
|------|---------|--------|
| Just ranting | 2+ Hot take segments | rep −3, cred −2, Casual churn +30% this week, Social +3 |
| Tonal whiplash | Comedy immediately after a Crisis-response segment | mood churn ×1.5, morale −4 |
| One-note | Main + Second share a topic `family` | combined reach ×0.85 **but** the shared thread advances a full extra stage |
| Overexposed | Same Recurring bit 4 weeks running | Recurring appeal ×0.6 until you rotate it (freshness, §12) |
| Thin week | Any slot under-prepped (prep < effort×0.4) | that segment's quality floored at 0.5; "felt rushed" reaction |

**Combos (bonuses):**

| Rule | Trigger | Effect |
|------|---------|--------|
| Well-produced | three distinct angles, no clash | quality +0.08 all segments, morale +2 |
| Palate cleanser | light Recurring bit (appeal ≤ 1.0) after a heavy Main | mood churn ×0.7 |
| Deep dive | Analysis or Investigation Main + Analysis Second, same family | cred +3, Core conversion ×1.5 this week |
| Newsroom | Investigation Main + a broken scoop in any slot | Social +6, cred +2, a "who fed them this" rival beat |
| Range | a serious Main and a Comedy Recurring, no whiplash | Clip-only +8%, "something for everyone" |

## 9. Prep points & levers

Base **12/wk**. Spent across: the three segment topics, plus the shared levers:

- **Research** — narrows swing (all segments), +rep, +cred; Researcher ×2.
- **Audio** — +quality (all segments), −churn; diminishing past 4.
- **Promo** — one-week Reach multiplier, no compounding. **Overreach (§12):**
  Promo on a low-quality episode churns the extra reach straight back out next
  week + an "overhyped" cred ding.

Crew and upgrades raise the cap (Co-host +3, Producer +2, Faster PC +1, Studio
+2, …). Unspent points are lost — but a deliberately low-prep week is a **light
week** (morale +6, freshness +10; §12).

## 10. The recording — live choices

After **Record**, play **2–3 beats** before resolution. Each: a sentence of
situation, 2–3 options, an immediate outcome (a modifier on this episode's
resolution, sometimes a stat delta or a thread/event seed). A smooth week
(0 beats fired) gets a small "clean show" bonus (quality +0.04).

Beat count and pool are seeded by: angles chosen (Hot take → more "did you go too
far"), crew traits (§19), the push dial (§11), a guest present, live threads,
match context, and **Social Reach** (high → more discourse beats). Chaos-cycle
modifier roughly doubles beat frequency.

Example beats:

| Beat | Fires when | Options (sketch) |
|------|-----------|------------------|
| Co-host tangent | loose/loose-cannon co-host | rein it in (on-message, −buzz) / let it ride (+buzz, +social, rep risk) |
| Thin on facts | Hot take Main, low Research | walk it back on air (rep +1, appeal ×0.9) / commit (roll the backfire now) |
| Guest overshares | insider/rival guest | cut it (safe) / keep it (+social +buzz, cred −3, libel-thread risk) |
| News breaks mid-record | a scoop or transfer pending | bin the rundown to chase it (fresh, +appeal, prep wasted) / hold course |
| Audio corrupted | no Audio-chain upgrade | re-record (−3 prep equiv, morale −3) / ship rough (quality ×0.9) |
| Callout | schadenfreude/hottake vs a named person | name them (+appeal +social, beef thread, cred −2) / keep it vague (+cred) |
| Sponsor line | demanding sponsor active | read the ad hard (sponsor happy, appeal −0.05) / soft-pedal (sponsor −trust) |
| Emotional moment | `|surprise| = 2` | lean in (Emotional angle bonus even if not chosen) / stay analytical (+cred) |
| Great bit | Comedy angle + high morale | clip it (Social +5, Clip-only +) / move on |
| Correction | last week's take aged badly | own it cleanly (cred +4, appeal −0.1) / brush past (cred −3, "never admits it" reputation) |

## 11. The push dial & backfire

One dial per episode, **1–5** (default 2). Higher = more aggressive editorial
line across all segments.

```
pushAppealBonus  = 0.06 * (push - 2)          # -0.06 .. +0.18 on combined appeal
pushBuzzBonus    = 0.15 * (push - 2)
pushSocialGain   = 2 * (push - 2)
pushMoraleCost   = push >= 4 ? (push - 3) * 4 : 0

backfireChance = clamp(
    0.04
  + 0.05 * hotTakeSegments
  + 0.04 * (push - 2)
  + 0.06 * (1 - clamp(credibility/60, 0, 1))     # low cred → takes land worse
  + 0.03 * (socialReach/100),                     # bigger stage, more scrutiny
  0, 0.75)
```

Odds shown before you commit. On a backfire: `rep −4`, `cred −5`, Casual churn
+40% next week, a **correction thread** opens (its stage-1 topic is the only way
to recover the cred cleanly), and if `socialReach ≥ 50` a **"main character of
the day"** event fires — pile-on, Clip-only spikes then craters, a sponsor may
invoke a morality clause.

Risk management: you can run **push 1** ("keep your head down") to shrink
backfire odds and let Social Reach decay during a rough patch — a club crisis,
a thin squad, a shaky sponsor target — then ramp back up when you're on solid
ground.

## 12. Freshness, slumps & burnout — the risk state

The v0.3 loop always grew you. v0.4 rebalances so a **competent, un-risky episode
nets roughly flat Casual and a trickle of Core** (see §17 `DeltaScale` drop and
the widened safe-play band). Growth comes from appeal, promo, virality and
breakouts — all of which carry downside.

### Freshness (0–100, starts 80)

```
freshness -= 6   same Main angle as last week
freshness -= 5   same Recurring bit as last week
freshness -= 4   same topic family as last week's Main
freshness -= 3   push >= 4
freshness += 10  a light week (§9)
freshness += 8   a format change (rotate the Recurring bit, first time in 4wk)
freshness += 6   an international break or offseason week
freshness  = clamp(0..100); drifts +2/wk toward 60 otherwise

appealMult from freshness:  0.70 at 0  →  1.00 at 60  →  1.05 at 100
```

### Slump (state)

Entered when **any** of: 2 of the last 3 episodes below quality 0.75; a backfire
(§11); a whiplash clash (§8); a missed sponsor target (§18).

While slumped: churn ×1.5 (all segments), Casual→Core conversion paused,
Social Reach decay ×2. **Exit:** two consecutive episodes at quality ≥ 0.95 with
no clash.

### Burnout ↔ Morale

High-push weeks and bonus episodes cost Morale (§5). Morale < 40 caps quality at
1.05 and adds a "phoned-in" roll (10% chance of quality ×0.8). The intended
rhythm: push hard for a stretch, then a light week to recover freshness *and*
morale before the next run.

## 13. Story threads & events

**Threads** — multi-week arcs, seeded by game state not RNG. Each stage offers a
high-appeal topic; ignoring a hot thread is a miss, riding drama carries rep and
**credibility** risk. Resolve into a payoff and sometimes a permanent shift.

| Thread | Seeded by | Resolves to |
|--------|-----------|-------------|
| Manager under pressure | 3 losses in 5 | sacked (new boss, strength ±0.04) or backed |
| Star wants out | key player, contract ≤ 1yr, losing | sold (−0.05) or stays (+buzz) |
| Is the kid the real deal? | prospect, 3 good games / wonderkid_goal | breakout (+0.03) or fades |
| Are we actually good? | beat two top-6 clubs | belief (rep +, listeners +) or bubble bursts |
| Is he finished? | repeated howler/keeper_error by a starter | benched/sold or bounces back |
| Transfer saga | RNG during windows | signs / collapses / rival hijacks |
| Takeover talks | RNG, weighted by league position | takeover (+0.08 over a season) or nothing |
| **Correction** | a backfired push (§11) | recover cred by covering it, or let it fester (−cred/wk for 4wk) |
| **Beef** | a callout recording beat, or a rival-podcaster guest | truce (buzz spike) / escalate (huge social, cred −, a bad-faith-audience influx that churns hard) |
| **Libel scare** | keeping a guest's unverified claim | retract (cred −2, done) / stand by it (lawyer's-letter event; money + cred at stake) |

**Events** — interrupts with a choice, ~1 every 2–3 weeks: the leak, poaching of
a crew member (§19), going viral for the wrong reason, a rival podcast launch, a
live-show offer, a sponsor overstepping, a **discourse pile-on** (Social-Reach
driven), a candidate walking in the door (§19). Each trades something for
something.

## 14. Squad & player news

~6 generated players `{ name, position, rating 0.40–0.90, age, injuryProne,
contractYears, international }`. Archetypes: talisman, keeper, captain,
wonderkid, crock, deadline-day signing.

Weekly rolls: **injury** (weighted by injuryProne + congestion; key player out →
`keyOut += 0.04–0.08` while out), **international call-up** (during breaks),
**form swings** (nudge sim + flavour), **transfer interest** (during windows).
Squad news is a primary source for the §6 offer.

## 15. Rivals

Each season the game tracks **1–2 rival clubs** with their own mini-arcs: a rival
in your title/relegation race, a rival manager under fire, a rival chasing your
transfer target. Rival events are content:

- rival stumbles → `schadenfreude` topic (high appeal, −rep, −cred, +Buzz)
- rival surges → `worried` topic
- fixtures vs. a rival are always derby-importance
- a **rival podcast** may launch (event) — an ongoing Social-Reach competitor
  that siphons Clip-only unless you out-produce or out-spice it

## 16. Difficulty = club strength

| Difficulty | strength | outlook | texture | economy |
|------------|----------|---------|---------|---------|
| Casual | 0.72 | title / Europe / cup | glory content, high floors | overhead 16, adRate 0.0050 |
| Regular | 0.52 | mid-table, cup run | default mix | overhead 20, adRate 0.0045 |
| Hard | 0.36 | relegation scrap | crisis dominant, rep swings | overhead 22, adRate 0.0042 |
| Nightmare | 0.26 | survival | damage-control content | overhead 24, adRate 0.0040 |

Club improvement over a run: organic drift toward 0.50 (±0.01/season) + cards
(Top prospect +0.03 / Genius appointment +0.05 / Takeover +0.08) + offseason
transfers. Nightmare → Regular over two seasons is a core arc.

## 17. Resolution formulas

Per-segment quality and reach; combine; then economy and stat deltas once.

```
# ---- per segment s in {main, second, recurring(, coldopen)} ----
# inputs: prep_s (topic points), angle_s, topic_s
# shared: ppResearch, ppAudio, ppPromo, push, freshnessMult, morale

effort_s   = topic_s.effort - crewEffortRelief
prepRatio  = clamp(prep_s / effort_s, 0, 1)
overshoot  = max(0, prep_s - effort_s) * 0.025
gear       = 0.15*micQuality + 0.15*editingSkill + setFloorBonus       # §20
quality_s  = 0.35 + 0.60*prepRatio + overshoot + gear
           + 0.035*ppAudio + comboQualityBonus + cardQualityBonus
quality_s  = clamp(quality_s, 0.20, 1.60)
if morale < 40: quality_s = min(quality_s, 1.05)   # + 10% chance ×0.8

spread_s   = max(0, topic_s.swing * angle_s.swingMult
                    * (1 - 0.18*ppResearch*researcherMult))
if chaosCycle: spread_s *= 1.5

appeal_s   = max(0.05, topic_s.appeal * contextMult * angle_s.appealMult
                    + crewAppeal + sponsorAppealPenalty + momentBonus)
appeal_s  *= (1 + pushAppealBonus) * freshnessMult

reach_s    = audienceListeners * appeal_s * slotWeight_s
           * (1 + 0.08*ppPromo) * intlBreakMult * cardReachMult * studioReach

# ---- combine ----
reach      = Σ reach_s
quality    = Σ (quality_s * slotWeight_s) / Σ slotWeight_s
spread     = Σ (spread_s * slotWeight_s) / Σ slotWeight_s
apply §8 combo/clash modifiers to reach, quality, and the stat-delta sums

# ---- market saturation (per segment target market; unchanged shape) ----
market      = (60000 + 6000*(season-1)) * (0.45 + 0.012*reputation) * difficultyMarketFactor
growthRoom  = clamp01(1 - listeners / market)

# ---- the roll & the segmented deltas ----
roll        = 1 + uniform(-spread, +spread)          # cardGuarantee → 1+spread
viralMult   = 1 + (socialReach/100) * 1.4            # amplifies both tails

grossReach  = reach * (quality - 0.70) * roll * 0.19 * growthRoom
if roll > 1.15 or roll < 0.9: grossReach *= viralMult      # breakout / dud both scale

# split incoming audience by what the episode leaned on
appealShare = clamp((avgAppealMult - 1.0), 0, 1)     # how "spicy" the week was
newCasual   = grossReach * (0.55 + 0.35*appealShare)
newClip     = grossReach * (0.10 + 0.30*appealShare) + socialClipGain
womCore     = quality > 1 ? core * 0.03 * (quality-1) * growthRoom : 0

# churn per segment pool
coreChurn   = core   * 0.015 * slumpMult * clamp(1.30 - quality, 0, 1) * (2 - credFactor)
casualChurn = casual * 0.070 * slumpMult * clamp(1.45 - quality, 0, 1.45)
clipChurn   = clip   * 0.120 * slumpMult
moodChurn   = (core+casual) * moodChurnRate * max(0, 1.15 - quality)   # §4

# conversions
toCore      = casual * 0.02 * consistencyFactor * clamp(credFactor,0,1.5) * (slump ? 0 : 1)
toCasual    = clip   * 0.01 * listenableFactor
backfireOut = backfired ? casual * 0.40 : 0

core   += round(womCore + toCore - coreChurn)
casual += round(newCasual + toCasual - casualChurn - moodChurn - toCore - backfireOut)
clip   += round(newClip - clipChurn - toCasual)
# clamp each ≥ 0

# ---- reputation & credibility ----
repGain  = Σ topic_s.rep * quality_s + analysisRepBonus
         - (quality < 0.55 ? 4 : 0) - (push>=4 ? (push-3) : 0)
credGain = Σ topic_s.cred * angle_s.credMult + investigationBonus + correctionBonus
         - (backfired ? 5 : 0) - (whiplash ? 2 : 0) - platformedLiePenalty
reputation = clamp(reputation + repGain, 0, 100)
credibility = clamp(credibility + credGain, 0, 100)

# ---- social reach ----
socialGain = Σ angle_s.social + pushSocialGain + viralMomentSocial + comboSocial
socialReach = clamp(socialReach - 1.5 + socialGain, sizeFloor(reach), 100)

# ---- morale ----  (studio average of §19 per-employee morale, nudged)
moraleDelta = wins*+ - pushMoraleCost - overworkCost + lightWeekBonus + comboMorale
              - (backfired ? 5 : 0)

# ---- buzz ----
perf   = quality * roll
buzz  += perf > 1.15 ? round((perf-1.15) * 35 * sqrt(avgAppeal)) : 0
buzz  += Σ topic_s.buzz * angle_s.buzzMult * (importance != normal ? 2 : 1)
buzz   = round(buzz * clipsBuzzMult) + cardBuzz

# ---- economy (segmented ad revenue) ----
adRevenue = (core*0.0045 + casual*0.0018 + clip*0.0003)
          * (0.80 + 0.40*reputation/100) * difficultyAdRateFactor
hosting   = 5 + reach * 0.0007
costs     = fixedOverhead + hosting + wages(§19) + upkeep(§20)
money    += adRevenue + sponsorWeekly + partnershipRevenue - costs
```

`DeltaScale` drops **0.26 → 0.19** and Core churn is low but credibility-gated:
the safe episode barely moves the needle; the spicy/promoted/high-push episode
has a real negative tail (`viralMult` on a dud, `backfireOut`, slump entry).

### Worked sketches (Regular, week 8, ~1,200 Core / 3,000 Casual / 900 Clip)
- **Safe week** — Analysis Main, mailbag Recurring, push 2, no promo → q 1.12,
  Casual +40, Core +25, Clip −60, cash −€10. Net roughly flat; you held serve.
- **Swing week** — Hot take Main + callout beat + push 4, promo 3 → appeal way
  up, `backfireChance ≈ 0.28`. Hits: Casual +520, Clip +340, Social +12, cred −4.
  Misses: Casual −380 next week, correction thread, slump. This is the decision.

## 18. Economy — sponsors, access, scoops, bankruptcy

### Sponsors — you pick the deal *and* the target

Offers arrive every few weeks, 1–3 at a time, scaled to your size. Hold **one**
deal (two with the §20 upgrade). Every deal is a **contract with a milestone**.

```
SponsorOffer {
  weekly, signingBonus,
  target:  { metric: listeners | avgListeners | reach | reputation | credibility, value, byWeek },
  onHit:   { bonus, renewAt, repDelta },
  onMiss:  { endDeal, repDelta (neg), clawback? },
  demands: [ "-rep to sign", "-0.05 appeal while active", "morality clause: end on a backfire", ... ]
}
```

| Example | Weekly | Target | Hit | Miss | Cares about |
|---------|--------|--------|-----|------|-------------|
| Local café | €30 | 400 listeners in 8wk | +€150, renew €45 | ends | listeners |
| Kit retailer | €55 | reputation 25 in 10wk | renew €80 | −2 rep | rep |
| Betting site | €90 +€100 | 600 listeners in 6wk | +€250, renew €140 | −4 rep, repay bonus | listeners; no morality clause |
| Energy drink | €140 | 40,000 **reach** in 12wk | +€500 | ends | reach — loves Clip-only |
| Broadsheet partner | €260 | **credibility** 65 in 16wk | partnership track | −3 cred, −6 rep | credibility; morality clause |
| National brand | €600 | 20,000 listeners in 14wk | +€2,000, category-partner offer | −8 rep, 6wk cooldown | listeners + credibility floor |

Tension: aggressive listener/reach deals fix cash flow now but bet on growth you
might miss — and chasing them pushes you toward appeal over credibility, which
then locks you out of the rep/cred-gated deals and access.

### Access tiers — audience is leverage

Rolling average **Listeners** (last 6) sets access — **but tiers 2 and 3 also
require a credibility floor** (55 / 70). Burn a scoop and you lose the tier even
if the audience qualifies.

| Tier | Avg listeners | + cred | You get | Catch |
|------|---------------|--------|---------|-------|
| 0 Outsider | < 1,000 | — | react to public news only | — |
| 1 Press pass | 1,000+ | — | post-match quotes, pressers, small scoops | — |
| 2 Insider | 12,000+ | 55 | scoops arrive *before* news breaks; players & staff talk; interview without a Booker | the club notices your tone |
| 3 Power broker | 80,000+ | 70 | takes move fan sentiment + board pressure; nudge a youth player's minutes; transfer scoops carry weight | partnership offer with editorial strings; scorched-earth costs access |

### Scoops (tier 2+)

Advance word on a transfer / manager decision / contract. Choose:

```
BREAK IT NOW:
  scoop topic (appeal 2.20), big Buzz, Casual + Clip spike
  disruptionChance = 0.15 + 0.10*accessTier + clamp(reach/500000, 0, 0.30)
                     - clamp((credibility-50)/100, 0, 0.20)
  if disrupted: rep -6, cred -8, accessTier -1 for ~8wk, fan-backlash thread, churn roll
  impact on sentiment scales ×clamp(credibility/60, 0.2, 1.6)

VERIFY & HOLD:
  small bump now; guaranteed strong recap when it breaks officially
  +3 rep, +4 cred, access protected, "trusted" standing ++ (better future scoops)

TRADE IT:
  give it to a national outlet for money / a favour / future access
```

### Positive influence (tier 2–3)

Backing the manager on-air during a crisis reduces board pressure (if your
audience matters). Championing a youth player nudges his minutes. Measured + big
+ trusted → the club offers a **partnership**: official access + a small revenue
share (§17 `partnershipRevenue = adRevenue * 0.35`), but you can't go too
negative without losing it.

### Bankruptcy

Money < −€200 warns; 3 consecutive weeks below → run ends. A real threat in year
one and again if a Clip-heavy show never converts to paying Core.

## 19. Crew — the roster

Five role **slots**: Co-host, Producer, Researcher, Clips manager, Booker. Each
is filled from a **rotating pool of 2–3 candidates** (refreshes every ~6 weeks;
a walk-in candidate event can surface a standout).

```
Candidate {
  role, name,
  skill:  0.3 .. 1.0,            # scales that role's effect
  wage:   monthly, varies ±40% around the role baseline
  traits: 1-2 from the table below
  morale: 70 at hire
  contract: minimum 12 weeks, or pay a buyout to fire early
}
```

Role baselines (monthly, lump every 4 weeks): Co-host 180 · Producer 220 ·
Researcher 200 · Clips 240 · Booker 260. Effect scales with `skill`.

| role | effect at skill 1.0 |
|------|---------------------|
| Co-host | +3 prep/wk, +0.05 appeal |
| Producer | +2 prep/wk, −0.5 effort on every segment |
| Researcher | Research lever ×2, +rep & +cred on Analysis/Investigation |
| Clips manager | +30% Buzz, +1%/wk passive Clip-only reach, +social on comedy/hot-take |
| Booker | unlocks guests in any slot, more guest events, better guest quality |

### Traits

| Trait | Effect |
|-------|--------|
| Ex-journalist | Investigation angle ×1.3; **caps push dial at 3** |
| Podcast-famous | +buzz, +appeal, +social; wage +50%; walks for a bigger show if you go 6wk without a milestone |
| Cheap and keen | wage −30%, skill −0.2, **skill +0.03/season** (a project) |
| Loose cannon | buzz variance both ways; +1 recording beat/wk |
| Diva | output +0.1 skill-equivalent; periodic conflict event; wage creep |
| Grafter | morale decays slower; +2 team morale passively |
| Homer | appeal +0.05 on optimism/recap; cred −1/wk (fans clock the bias) |
| Contrarian | Hot take swing narrower (better); Analysis appeal −0.05 |
| Burned out | wage −40%, skill −0.15/season, morale −3/wk — a gamble on a turnaround |
| Connected | +1 scoop chance; occasionally brings a free guest |
| Perfectionist | quality +0.05; −1 effective prep point (slow) |
| Audience favourite | firing them triggers a **backlash event** (Casual −, social −) |

### Hiring & firing

- **Hire:** accept their wage ask, or lowball (−10 morale at hire, small chance
  they decline and leave the pool).
- **Raise / bonus:** +morale, costs money; expected as you grow (underpaying vs.
  your size bleeds −2 morale/wk).
- **Fire:** severance = 4 weeks' wage + team morale −8. Within the minimum
  contract, add a buyout (8 weeks' wage). `Audience favourite` → backlash event.
  A fired co-host may start a **rival podcast** (§15).
- **Poaching event:** a rival outlet bids for your best crew — match the offer
  (wage up), let them go (slot opens, morale −5), or counter with equity
  (partnership-like string).

## 20. Upgrade tracks

Five tracks, each 3–4 tiers. Buy in order; each tier gates on money (and
sometimes listeners / rep). Upkeep is monthly.

| Track | T1 | T2 | T3 | T4 |
|-------|----|----|----|----|
| **Set** | Plants & posters — €120, floor +0.03 | Branded backdrop — €300 +€10/mo, floor +0.06, +social on clips | Green screen — €700 +€20/mo, unlocks visual Comedy bits, clip reach ×1.15 | Custom built set — €1,800 +€40/mo, floor +0.10, appeal +0.04, "looks like a real show" |
| **Audio chain** | XLR mic — €150, micQuality 0.20→0.55 | Interface + treatment — €350 +€8/mo, quality +0.05, −churn | Treated room — €800 +€15/mo, "audio corrupted" beat disabled, quality +0.05 | Broadcast booth — €2,000 +€35/mo, quality +0.08, guest audio, −churn |
| **Post / editing** | Editing software — €200, editSkill 0.15→0.45 | Editing suite — €500 +€12/mo, −1 effort all segments | Editor workstation — €1,200 +€25/mo, +Buzz from clips, unlocks **Cold Open** slot | Auto clip pipeline — €2,800 +€50/mo, +2%/wk passive Clip-only, Social decay halved |
| **Studio space** | Spare room — free (start) | Rented unit — €900 +€30/mo, +2 prep, reach ×1.08 | Proper studio — €2,200 +€55/mo, +1 prep, live-audience event weeks | Guest suite — €4,500 +€90/mo, two guests/episode, guest-heavy formats |
| **Distribution** | Website — €100, ad rate ×1.05 | App — €600 +€15/mo, +passive Core reach, push notifications (promo ×1.1) | Media network deal — €1,500 +€30/mo, unlocks national sponsors, +social floor | Own the feed — €3,500 +€60/mo, ad rate ×1.3, a second show (idle income + a management event) |

Second sponsor slot (§18) becomes a standalone €600 unlock, unchanged.

## 21. Cards & packs

Hand of 5. Milestone rewards or 3-card packs for 60 Buzz. Reputation improves
rarity. Up to 2 played per week, in the rundown step. Mostly one-shot; a few
permanent shifts. v0.4 adds a handful keyed to the new systems (a "viral clip"
one-shot: Social +15, Clip-only spike; a "retraction" one-shot: undo the last
backfire's cred loss; a permanent "trusted voice": cred decay floor +10).

## 22. Goal & milestones — endless

Primary goal **50,000 average Listeners** → "you made it" beat → continue prompt
→ endless logarithmic ladder.

100 · 500 · 2,500 · 10,000 · **50,000 ★** · 100k · 250k · 500k · 1M (buyout
decision) · 2.5M · 5M · 10M · … Each rung past 2,500 pays a card pack + Buzz + a
flavour unlock. A parallel **Reach** ladder (counting Clip-only) pays cosmetic
unlocks only.

## 23. Between runs

- **Cosmetics** — always free, no gameplay effect: cover art, studio skins,
  intro stingers, UI themes, mic models, on-air signs.
- **Custom modifiers** — toggles set before a run: +1 base prep · churn −20% ·
  start +€300 · bankruptcy grace 5 · no international breaks · sponsor-free
  (higher ad rate) · chaos cycle (more events + recording beats, wider rolls) ·
  sandbox (no bankruptcy) · **thick skin** (backfire odds −halved) · **viral
  start** (social 40, Clip-heavy opening). Enabling any flags the run **Custom**.

## 24. Tuning knobs

startMoney 500 · startCore 40 · prepBase 12 · adRate core/casual/clip
0.0045/0.0018/0.0003 (× difficulty) · churn core/casual/clip 0.015/0.070/0.120 ·
qualityBreakeven 0.70 · deltaScale 0.19 · womRate 0.03 · fixedOverhead 20 (by
difficulty) · hostingSlope 0.0007 · buzzThreshold 1.15 · buzzScale 35 · packCost
60 · bankruptcyFloor/grace −200 / 3 · goalListeners 50000 · homeAdvantage 0.06 ·
offseasonChurn core/casual/clip 0.08/0.22/0.30 · crewMonthlyInterval 4wk ·
accessTierListeners [1000, 12000, 80000] · accessCredFloor [–, 55, 70] ·
scoopDisruptionBase 0.15 · avgListenerWindow 6 · pushRange 1–5 · backfireBase
0.04 · freshnessStart 80 · slumpChurnMult 1.5 · viralMultK 1.4 · socialDecay 1.5
· moraleQualityCap 40 · candidateRefreshWeeks 6 · severanceWeeks 4 · buyoutWeeks 8

## 25. Build order

Slices 1–3 are shipped. v0.4 is the next four:

- **Slice 4 — the episode has weight.**
  - **4A** rundown (3 slots) + angles + guest hook. Offer grows to 6.
  - **4B** angle interactions (§8) + freshness + slump + the safe-play rebalance
    (`deltaScale` drop, widened band) + overreach.
  - **4C** audience segments (Core / Casual / Clip) + Credibility + Social Reach +
    the "how it landed" beat.
  - **Playtest gate:** is the week now a real decision, and does the safe play
    feel like holding serve rather than winning?
- **Slice 5 — the recording.** Interactive beats (§10) + push dial + backfire +
  Morale + the new threads (correction / beef / libel) + discourse events.
- **Slice 6 — the business grows.** Upgrade tracks (§20) + crew roster (§19:
  candidates, traits, hire/fire, poaching) + the rival-podcast competitor.
- **Slice 7 — economy & balance pass.** Retune every formula against playtest
  data; sponsor variety; make each of the four indices clearly worth chasing.
- **Slice 8 — visuals.** The deferred pixel-art identity pass: episode-cover
  template, matchday scoreboard, the studio scene filling out with your
  upgrades, the logbook as a shelf of covers, a UI Toolkit styling pass.

## 26. Still open

- Three segments the right number, or does Main + Recurring (2) keep it tighter?
- Is Social Reach distinct enough from Buzz, or should Buzz fold into it?
- Do Core/Casual/Clip need to be visible to the player as three numbers, or is
  one Listeners figure + a "loyalty" read cleaner?
- Recording beats per week — 2–3, or scale with push / chaos only?
- Backfire punishment — is a slump + correction thread + event too much on one
  bad roll? Playtest.
- Candidate pool size and refresh rate — does firing ever feel worth it?
