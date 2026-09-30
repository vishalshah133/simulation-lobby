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
