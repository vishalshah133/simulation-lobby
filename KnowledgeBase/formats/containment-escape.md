# Format: Containment / Escape (`esc`)

A single ball (or a few) bounces inside a boundary. Each bounce changes something — the ball grows,
speeds up, the boundary shrinks, or a wall layer breaks. The only question is **"will it escape?"**

Source of inspiration: the "Will the Ball Escape? / Ball Bouncing Brainrot" genre that went viral on
YouTube Shorts from November 2023 onward — channels `satisfying.ba11s`, FuncFlow, Jack's Physics,
CodeCraftedPhysics. Individual videos in this genre reached 38M–67M views within months.

## Why this is different from every other format here

`race`, `wars`, `rom`, `elim`, `surv` are all **competitive** — contenders against each other, and
the hook is *who wins*. This format has no competition at all. One object versus a boundary, and the
hook is *whether a threshold gets crossed*.

That means it needs none of the leaderboard/identity machinery, and it retains for a completely
different reason: **monotonic escalation toward a binary outcome.** Worth having in the mix precisely
because it fails differently from the others.

## The audio mechanic — treat this as the primary feature

Each bounce triggers the next note of a recognizable melody. As the ball accelerates, notes come
faster and the tune becomes identifiable, resolving right around the climax.

This is not post-production polish. It is the retention device, and it must be **driven by the
simulation** — the collision event fires the note. Implications:

- Note sequence advances on collision, not on a timeline. The music *is* the physics readout.
- Escalation tuning is therefore musical tuning: the bounce rate curve should make the melody land
  coherently, not just accelerate arbitrarily.
- This belongs in `Shared` as a `BounceAudioSequencer` component — `race`, `rom`, and `surv` can all
  use collision-triggered audio too. It's the most reusable thing in this whole brief.

Use public-domain or original melodies. Recognizable-but-licensed music is a copyright-claim risk on
a monetized channel.

## Escalation variants (each is its own video series)

| Variant | Each bounce... |
|---|---|
| Growth | Ball gets bigger |
| Speed | Ball gets faster |
| Shrink | Boundary gets smaller |
| Layers | Breaks one wall layer of many; regenerating layers extend the run |
| Multiply | Ball splits into two (overlaps with `rom` — the count is the story) |

Combining two escalations at once (grow + speed) shortens runs sharply. Start with one.

**Series ramps (built 2026-10-11, esc-005..007):** a second kind of variation that leaves the
per-bounce escalation alone and changes the *arena between rounds*: the gap narrows
(`finalGapDegrees`) or spins faster (`finalGapAngularSpeed`) each round. These make the series harder
as the score builds without stacking two per-bounce escalations, so they're not the trap above.
Untested on viewers.

## Build notes

- **Trivially cheap to build** compared to the other formats — one rigidbody, one boundary, one
  escalation rule. This is the fastest path to a daily Shorts cadence while the bigger formats are
  still being built.
- Perfectly elastic collisions, zero damping — energy must never bleed off or the run dies quietly.
- **Tunneling is the main technical risk.** A small fast ball will pass through a thin wall. Use
  continuous collision detection, a low fixed timestep (0.01 or lower), and thick colliders. The
  ball escaping through a wall it should have hit invalidates the entire premise.
- Color-cycling on the ball and a trail are genre convention and do real work — they make speed
  legible.
- Cap the run: if it hasn't resolved by ~50s, the video is too long for the format.

## What makes a good run

A near-escape that gets pulled back, then resolution. A ball that escapes in 8 seconds is no video;
one that never escapes is a cheat unless the title asked a question the answer to which is "no."

Seed-scan for runs where the ball comes close to the threshold and survives at least twice.

## Title / hook patterns

The title is the entire premise and should be a question: "Will the Ball Escape?", "Every Bounce It
Gets Bigger", "999 Walls". Put a countdown, layer count, or size meter on screen so the viewer can
see how close it is.

## Failure modes

- Tunneling (see above) — the credibility killer.
- Energy loss making the run peter out — check restitution is exactly 1.
- Escalation too slow; nothing has visibly changed by 10s.
- Melody unrecognizable because the bounce rate is erratic.

## Open questions

- Does the genre still perform in 2026, or is it saturated? Test 3 Shorts before committing a series.
- Our differentiator vs. the dozens of existing ball channels: the *lobby* framing — the same
  polished visual language and named contenders shared across all our formats, rather than a
  one-trick channel. Worth testing whether crossover branding actually transfers viewers.
- Does collision-triggered audio lift retention on `race` and `surv` too? Cheap to test once
  `BounceAudioSequencer` exists.

## Wide variant (comp-001)

Segment 2 of `comp-001`, "Shrinking Gap" (16:9, 1920x1080). Built 2026-10-27, **not yet run in Unity**; all numbers below are estimates.

- **Menu:** Simulation Lobby ▸ 2D Formats ▸ Escape (esc) ▸ Wide: Shrinking Gap (comp-001). Scene `Assets/Scenes/Comp001_02_ShrinkingGap_Wide.unity`.
- **Files:** `Escape/EscapeWideSegment.cs` (delayed start + counter/caption feed into `SegmentCardPresenter`), `Escape/Editor/EscapeWideSceneBuilder.cs`. The 9:16 builder is untouched.
- **Config `EscapeConfig_008`** (copy of 006): 5 rounds, gap 36 to 14 deg, spin 20 to 36 deg/s, speed 11-13, growth x1.045, `maxAttemptSeconds` 18, `interAttemptSeconds` 2. Plan had ~9s per round; that would cut rounds before the ball is "too big" and make the on-screen `/ ~N` ceiling wrong, so it is 18 (a stall guard, rounds normally end earlier). Expected ~45-55s with the ~4.5s card + explainer lead-in.
- **Look/sound:** Frost backdrop pinned (no per-seed theme, no jitter), Ode to Joy carried across rounds (the Shorts used none, Baa Baa Black Sheep, or Fur Elise).
- **Frame:** arena ~80% of frame height (outer ring), camera shifted right so the ring centres left of the 22% right margin. Counter plate: `BOUNCES`, `n / ~N`, `ROUND k/5`. N is computed live from the ball's size (the bounce at which it becomes too big), so it is exact per round (about 41/36/30/23/14 for 1.035-style growth, lower at x1.045).
- **Run starts late:** `autoStart` is off and `EscapeWideSegment` calls `Begin` after the card + 3s of explainer, so round 1 is not hidden behind the card. The card keeps its own clock. Seed is random and logged (`[esc-wide] Random seed ...`).
- **Risks:** (1) A ball aimed straight at the gap on its first approach escapes in ~1s with 0 bounces (~10% in round 1, ~4% in round 5, before the spin). `EscapeConfig` has no minimum-round field; the only hold is the 2s result beat. Re-roll the take or add a min-hold field to the config if it shows. (2) The ending image (last round squeezing out of 14 deg) only happens if the ball escapes in round 5; otherwise the closing caption sits over a trapped ball. (3) The run stops right after the last result; extend the recording tail by ~2s in the edit.
