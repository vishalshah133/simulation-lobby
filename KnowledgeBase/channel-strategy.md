# Channel Strategy — Simulation Lobby

## Positioning

A "lobby" of simulations: viewers drop in, pick a contender, watch physics decide. The channel
identity is the *variety of formats under one consistent visual language*, not any single format.

What makes this category work on YouTube:

- **Zero-context entry.** Nothing to learn. Rules are visible in the first three seconds.
- **Built-in stakes.** Someone wins, someone loses, and it isn't authored — viewers trust physics.
- **Parasocial pick.** Viewers adopt a color/name and root for it. This is the comment engine.
- **Infinite variation from one build.** New seed, new config, new video.

## Audience and the pick mechanic

The single highest-leverage habit: **make it trivially easy to pick a side in the first 3 seconds.**
Named, high-contrast, few enough to track (8–16 for Shorts, up to 100 for long-form spectacle where
the *mass* is the appeal rather than individual tracking).

Ask for the pick explicitly on screen and in the pinned comment. "Comment your color before it ends."

## Shorts vs long form

| | Shorts | Long form |
|---|---|---|
| Length | 25–55s | 8–20 min |
| Aspect | 9:16 1080x1920 | 16:9 1920x1080 |
| Job | Discovery + subscribe | Watch time + session depth |
| Best formats | `rom`, short `race`, `surv` | `wars`, `elim`, tournament `race` |
| Structure | Hook → rules → run → finish | Cold open of the climax → rules → rounds → finale |

Shorts are the top of the funnel; long form is where the channel monetizes and where a subscriber
becomes a regular. Aim for roughly 3–4 Shorts per long-form upload, and have the Short seed the
long-form idea rather than being a separate effort.

## Cadence

Daily Shorts is the realistic target once the pipeline is deterministic and batched — the build is
reusable, so the marginal cost of a Short is a config plus a render. Weekly long form.

Batch production: build/refine a format one week, render many variants from seeds, then publish over
following weeks. Never build and publish the same day.

## Two kinds of tension — carry both

Studying the wider genre (see `reference-channels.md`) surfaced a distinction worth building around:

- **Competitive tension** — *who wins?* Multiple contenders, viewer picks a side, comments follow.
  Covered by `race`, `wars`, `elim`, `surv`.
- **Threshold tension** — *will it happen?* One object, one binary outcome, escalating toward it.
  Covered by `esc` and `rom`.

These retain for different reasons and fail for different reasons. A channel running only one kind
is fragile to that kind going stale. Keep both in rotation.

**Two more mechanisms surfaced studying adjacent channels (2026-09-29, see `reference-channels.md`
"Adjacent categories" and the `CodeCraftedPhysics` section) — not yet built, tracked as ideas only:**

- **Single-event spectacle** — *what happens when X meets Y?* No escalation, no threshold, one
  dramatic collision resolved fast (15–40s). The single best-performing shape across the three
  Blender channels that run it (`Kawaken_3DCG`, `RenderZen`, `Oyen_3D`) — worth knowing this exists
  as a category even before deciding whether to build it, since "cheapest single collision" is a
  different production bet than anything currently in the format table. **Prototyped 2026-09-29 as
  `impact`:** rigid scatter settles in 3-4s, so the cheap version isn't a video, and the build
  turned into survival instead (`formats/impact.md`).
- **Generative curiosity** — *what does this become?* No win condition, no threshold; a system
  evolves under its own rule and the hook is purely watching what it turns into
  (`CodeCraftedPhysics`, 467K subs, current-performing). The highest creative cost of the four —
  can't be tuned by a single config knob the way `esc` can.

See `ideas-backlog.md`'s "New categories" section for working names and build-cost notes on both.

## Hooks that reliably work

- Countdown/odds shown before the start ("Red 12% · Blue 31%").
- A visible imbalance the viewer expects to resolve ("the tiny one against 50").
- Near-elimination saves — design arenas that produce them rather than editing them in.
- Numbers going up on screen (counts, multipliers, survivors remaining).
- **Ask for a guess** ("How many hits to destroy 300 blocks?"). The viewer commits to a number
  up front, then checks it against a live tally. Pure comment bait. *Untested:* video #1 used it at
  numbers too small to read, and `surv-001` is the first real test. Never show the total on screen
  (it's the answer), and never count something the viewer wouldn't (a miss isn't a hit).
- **Collision-triggered audio.** Bounces play successive notes of a melody that becomes recognizable
  as the action accelerates. This is a top-tier retention device in this genre and it is generated
  by the simulation, not added in the edit. Applies well beyond `esc` — test it on `race` and `surv`.

## Things to avoid

- Slow starts. The run begins within 3 seconds or the Short is dead.
- Too many contenders to track in 9:16 — beyond ~16 the viewer stops having a horse in the race
  unless the format is explicitly about the swarm.
- Visually identical contenders. Color alone is not enough; vary shape or trail too.
- Fake outcomes. If runs are scripted and viewers sense it, the trust that carries the category is gone.

## Title / thumbnail patterns

Titles state the premise as a question or a number: "100 Marbles, One Survives", "Release or Multiply?".
Thumbnails: one readable subject, huge number, high-saturation contrast, no small text.

## Open questions

- Persistent contender identities across videos (recurring "characters") — worth testing for retention?
- Live/premiere runs where chat picks sides?
- Leaderboards carried across a season?
