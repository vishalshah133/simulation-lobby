# comp-001: 10 Most Satisfying Simulations (16:9 long form)

**Format:** `comp` (compilation of re-done segments) · **Length:** ~7:50 (10 x ~45s + intro + outro; hard floor 6:00) · **Aspect:** 16:9 1920x1080 · **Status:** `planned`
**Brief:** none yet. Reference: `reference-channels.md` → *@Satisfying2DSims: its compilations* (its 8-10 min "N Most Satisfying Simulations" videos did 40K-1M each).

## Premise

Ten one-rule simulations, each a fresh variation of something that already worked as a Short. You guess before each one ends.

## Why this is allowed (read before building)

`learnings.md` says compilations on young channels get nothing (Bouncy Physics, 7 views). The 10-27 reference note qualifies that: a
channel with a library of proven Shorts did 5-30x its Shorts median with these. We have ~20 Shorts and 60 subs, so this is a **test,
not a bet**. Segments are *new renders*, not re-uploaded Shorts, so the video stands alone. Don't count on it before the first read.

## Storyboard (write before any code)

Spine: one fixed template for all ten so it reads as a series: segment card `n / 10` (1.5s), then the hook line top-left, the live counter
right, the result bottom (`wide-frame-hud`). A 1.5s guess hold before each payoff. Order alternates shape, palette and sound so no two
neighbours look alike; circles are spaced out.

| # | Seg | Hook (visible frame 0) | The variation | Counter | Ending image |
|---|---|---|---|---|---|
| 0 | Intro (~8s) | "10 simulations. Guess each one." | Fast cuts of the 10 ending images | none | Title card |
| 1 | `clack` | HOW MANY CLACKS? IT JUMPS 10,000x HEAVIER EACH TIME | **Digit Jump**: masses 1 / 10K / 100M / 1T, answers 3, 314, 31415, 3141592; neon on black; side digit ladder | CLACKS n | The four answers lined up as 3.141592 |
| 2 | `esc` | CAN THE BALL ESCAPE THE SPINNING GAP? | 5 rounds, gap shrinks 36 to 14 degrees; new palette and melody | bounces / ~36, round n/5 | Ball squeezing out of a 14-degree gap |
| 3 | `rom` | 1 BALL. HOW MANY SURVIVE THE GATES? | Vertical gate funnel, x n / +n / half / x0 gates, 5 rows, gold marbles | marble count | Final number huge over a gold pile |
| 4 | `conv` | 600 BALLS LEAVE RED. HOW MANY HIT BLUE? | **Needle Ellipse**: thin ellipse (0.95), 600 balls, rainbow trails; miss round, then restore | HIT BLUE n / 600 | Caustic of trails drawn on black |
| 5 | `press` | HOW MANY BALLS CAN A 128-TON PRESS CRUSH? | Wide steel chamber, 3 rounds 8 / 32 / 128 t, molten pile, deeper thud | BALLS n | Press stalls on a glowing pile |
| 6 | `royale` | WHICH OF 6 COLOURS SURVIVES THE RING? | 6 neon colours x 3, narrow x2 arc, sudden death at 15s, slow zoom on last two | per-colour share bar (right) | Last colour fills the ring |
| 7 | `brach` | WHICH SLIDE WINS? | **Racetrack**: 8 tracks on a light blueprint backdrop (the only light frame), sabotage bump round, pitch = speed | ROUND n/6 + finish ladder | Winning curve glows, `BRACHISTOCHRONE` |
| 8 | `brk` | CAN IT BREAK 3 RINGS IN 45 SECONDS? | 3 fat rings (4 hits each), 45s countdown, gold on navy, cyan ball, fast public-domain tune | RINGS n/3 + clock bar | Last ring shatters, seconds to spare |
| 9 | `bell` | WHAT SHAPE DO 1,500 MARBLES MAKE ON A BOARD THAT LEANS LEFT? | 18 rows; 500 fair, then 1,500 rigged left (p 0.25); warm bins; rising pitch per bin | n marbles | Still a bell, shifted. One-line why |
| 10 | `mve` | FILL 300, OR ESCAPE? | One long round: every bounce clones, every escape widens the wall | IN n / 300 vs OUT n | A 290/300 near-fill drains out of the open wall |
| 11 | Outro (~10s) | "Which one was most satisfying?" | Ten thumbnails in a grid | none | Comment prompt |

**Playing order (changed 10-28 at the user's request; the `#` column above is now just an id):** 1 `mve`, 2 `brk`, 3 `bell`, 4 `conv`, then
5 `press`, 6 `esc`, 7 `brach`, 8 `clack`, 9 `rom`, 10 `royale`. Opens on the one brand-new format; `clack` no longer leads. Midpoint twist is still
segment 7 (`brach`, the only light frame and the only race). Closing act is 9-10 (`rom` payoff, then `royale` last-colour finish). The `mve` near-miss
now lands in the first minute, which is the hook.

### On-screen explanations ("HOW IT WORKS", shown ~6s after each segment card)

Added at the user's request: every segment explains itself in plain words, 140 characters or fewer, no jargon. Drafts below; each
builder checks its text against the real rules and corrects it if the sim does something different. Held in a `SegmentCardConfig` asset
per segment (`Assets/Settings/SegmentCards/`), so the sim configs are untouched.

| # | Explainer |
|---|---|
| 1 clack | A heavy block pushes a light one into a wall. Every bounce is a clack. Heavier block, more clacks, and the count spells π. |
| 2 esc | The ball bounces inside a ring with a moving gap. The gap shrinks every round. Can it still get out? |
| 3 rom | Each gate changes the count: x3 multiplies, half halves, x0 wipes it out. How many marbles reach the bottom? |
| 4 conv | Every ball starts at the red dot and bounces off the walls. In an ellipse, they all pass through the blue dot. |
| 5 press | Each crushed ball splits in two. The press gets stronger every round. How many balls before it stops? |
| 6 royale | Marbles spin in a ring. The EXIT drops them out, the gold arc clones them. The last colour left wins. |
| 7 brach | Same start, same finish, different slides. Which one is fastest? The steep-then-flat curve beats the straight line. |
| 8 brk | The ball must break through the rings one at a time. Hit a ring enough times and it shatters. Beat the clock. |
| 9 bell | Every peg is a coin flip: left or right. Drop enough marbles and they pile into a bell curve. |
| 10 mve | Every wall bounce clones the ball. Every escape widens the exit. Does the swarm fill the ring, or empty out? |

Cold-viewer checklist (read as a stranger, sound off):
- [ ] Every hook names things visible from frame 0 and fits one line at 480px
- [ ] Every cause happens on screen (before/after visible), including the clack digit ladder and the `bell` rig
- [ ] Every segment changes the answer or raises stakes at least once; cut any that sit still
- [ ] Only the answer number is shown (no secondary tallies except the two real sides in 6 and 10)
- [ ] Segments with a curiosity (`clack`, `bell`, `brach`, `conv`) end on a one-line why
- [ ] No segment's first 3s looks like the previous one's last 3s (palette, shape or camera changes at every cut)
- [ ] Quiet stretches carry a caption (clack round k=2 has a ~7.9s silent tail: caption `WAIT...`)

## Blocks used

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md) (extend for 1920x1080; check it has no 9:16 assumption)
- [ ] **REUSE** [onscreen-counter](../blocks/onscreen-counter.md), [bounce-audio-sequencer](../blocks/bounce-audio-sequencer.md), [escalation-rule](../blocks/escalation-rule.md)
- [ ] **BUILD** [wide-frame-hud](../blocks/wide-frame-hud.md) (this video pays the cost; ten segments share it)
- [ ] **REUSE, sparingly** [seed-scan-harness](../blocks/seed-scan-harness.md): only to tune lengths (see risks), not to hunt a best seed

## Video-specific work

### Architecture decision: one master scene plays all ten, recorded in one take

**Updated:** the earlier note said "no in-Unity orchestrator; render ten clips and assemble in an editor". The user now wants one
Play session, one recording, so there is an orchestrator, kept in `Capture` so the formats stay isolated (`CLAUDE.md` rule 2).
`CompilationPlayer` (Capture) sequences scenes **by name only**, never referencing a format. It finishes a segment when Core's
`SimulationRunner` reports a `Result` (all runners in the scene), plus `tailSeconds`; fallback is the hard `maxSeconds` cap (with a
console warning). `elim` stays the only bracket orchestrator; this one is a playlist, not a competition.

- **Assets:** `Assets/Scenes/Comp001_Master.unity` (a camera + `CompilationPlayer`), `Assets/Settings/Compilation/Comp001Playlist.asset`
  (entries: scene, label, maxSeconds 70, tailSeconds 2.5, transitionIn, transitionSeconds 0.6 per half-cut; intro/outro text and
  seconds; startDelaySeconds 3).
- **Build:** `Simulation Lobby ▸ Tools ▸ Compilation (comp-001) ▸ Build All (10 segments + master scene)` runs the ten builders by
  menu-path string, registers the 11 scenes in Build Settings, makes the playlist and the master scene, and opens the master.
  `Rebuild Master Scene Only` refreshes the master/Build Settings/playlist and keeps hand-edited playlist values.
- **Run:** Game view 1920x1080 -> start the Recorder (`Print Recording Checklist` lists the settings) -> Play. 3 s of black lead-in,
  intro card (5 s), ten segments, outro (8 s), then Play mode exits. The Console logs chapters (`m:ss`, t=0 at the intro card),
  total length and each segment's seed.
- **Transitions** (uGUI overlay, sorting order 1000, unscaled time, audio ducked across each cut, `n / 10` banner while covered):
  FadeBlack, WipeLeft, WipeRight, FlashWhite, IrisClose. Default `transitionIn` per segment 1-10: WipeRight (from the intro card),
  FadeBlack, WipeLeft, IrisClose, FlashWhite, WipeRight, FadeBlack, IrisClose, WipeLeft, FlashWhite (no two neighbours match).
- **Expected length:** 3 s lead-in + 5 s intro + 10 x ~45 s (+ 2.5 s tail each) + 10 cuts x ~1.5 s + 8 s outro, roughly 8:30 to 9:30
  until real segment lengths are measured. Cut `tailSeconds`/`maxSeconds` to trim.

### Per-segment build cost (from the format reads; none run yet, all lengths unmeasured)

| # | Cost | Needs | Biggest risk |
|---|---|---|---|
| 1 clack | small code | 16:9 branch (`frameWidth` ~20), side ladder, new round list | silent round 3; ~38s natural, pad to 45 |
| 2 esc | config-only | `EscapeConfig_006` copy, theme, melody | gap ramp untested on viewers, instant-escape rounds |
| 3 rom | config + 16:9 layout | 6 lanes wide, `startMarbles` 1 | never watched in Unity; length a guess |
| 4 conv | config + small code | new `EllipseFocusConfig`, 16:9 builder | 600 balls + trails clutter / frame time; second pass is code |
| 5 press | config + small builder | wide chamber, palette | 128t run unmeasured (maybe 25s+), pile hits maxBalls 5000 |
| 6 royale | config + small presenter | 6 colours, side share bar, zoom | six hues unreadable at 480px, 1-marble colour |
| 7 brach | low-medium | new config, bump segment, pitch audio | 6 rounds in 45s may break the 1.2s caption hold |
| 8 brk | config-only (+ small HUD clock) | `BreakerConfig_001` copy | the pacing model was wrong before (measured 123s vs 46s), scan required |
| 9 bell | config + small builder | 18 rows, rig | CLT: p 0.25 is still a bell, caption it honestly |
| 10 mve | config-only | single round | **never run**; balance and 480px readability unknown. Fallback: Wall vs Ball (3D, built, but v5 got 246 views) |

All ten are circles or boards in 2D except the optional Wall vs Ball. Non-circular containers need new code and are out of scope.

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|
| Segment length | 45s each (band 38-55) | Total must land 6:30-8:00. Floor 6:00 means no segment below ~35s |
| Guess hold | 1.5s | before each payoff; keep at least 1.2s |
| Card | 1.5s | `n / 10` + title, no sim running |
| Aspect | 1920x1080 | check at 480px wide |

### Look

Light frame only in segment 7. Elsewhere dark backgrounds, saturated colour, one dominant hue per segment (neon, steel/orange, gold/navy,
warm bins, rainbow). Procedural audio throughout (`ProceduralToneBank`); a distinct timbre per segment. No licensed music.

## Acceptance criteria

- [ ] All block acceptance criteria met, `wide-frame-hud` included
- [ ] Each segment: same seed twice, identical outcome
- [ ] Each segment readable at 480px wide
- [ ] Final cut 6:30-8:00 with the intro and outro
- [ ] No two adjacent segments share palette or container shape

## Seed selection

Seeds stay random (`CLAUDE.md`), logged per segment and written into `PROJECTS.md`. Exception, per the risks above: length tuning may use
the scan harness to find config values (not seeds) that land ~45s, for `brk`, `press` and `royale`.

## Build order

1. `wide-frame-hud` block + one pilot segment (**`esc`**, config-only) to prove the frame at 16:9.
2. In parallel (Sonnet builders, Opus reviews): config-only first (`brk`, `mve`, `rom`), then builder-tweak (`press`, `royale`, `bell`, `conv`), then `clack`, `brach`.
3. User runs `Build All`, plays the master scene, and records lengths from the console; adjust to 45s.
4. Record the master scene in one take, publish.

Do not enter play mode or run menu items from here; hand each scene over to run. Assembly is the master scene above, not a video editor.

## Render + publish

One 1920x1080 take of the master scene (cards and transitions are built in). Title pattern for later: `10 Most Satisfying Simulations (<one thing visible in the video>)`,
standalone, no hashtags (`CLAUDE.md`). Chapters from the segment times. Pinned comment: "Which one was the most satisfying? 👇".
Give the title, description, tags and pinned comment as code blocks when the cut is ready, and remind to log the seeds.

## Retro

`KnowledgeBase/retros/YYYY-MM-DD-comp-001-ten-sims.md`; update `PROJECTS.md`.
The open question this video answers: **does a compilation of freshly re-done segments work on a 60-sub channel, or does it need S2DS's audience?** Read it against `elim` (573) and `mach` (406), not against Shorts.
