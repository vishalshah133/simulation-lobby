# timer-001 — 5 Minute Marble Run Timer

**Format:** `timer` · **Length:** 5:00 (config `durationSeconds`; 10:00 and 15:00 are config assets) · **Aspect:** 16:9 · **Status:** `rework v2` (3D, long form)
**Brief:** `KnowledgeBase/formats/timer.md`

## Premise (v2, decided by the user 2026-11-12)

A pure marble-run timer, reference-channel style: a countdown that ends at exactly 0:00, and a fast, continuous stream of 300 marbles
down a cartoon marble run into a glass collector that is nearly full at 0:00. **One question (user, 2026-11-12): "How many of 300 marbles make it in 5 minutes?"** The bucket count is the answer; all 300 in triggers a celebration. This deliberately breaks
our long-form story rules (stake, acts, recurring characters); it is a utility video and an experiment.

v1 ("can any marble beat the gold clock marble?") was dropped: the gold ball read as an obstacle, and the clock's speed was capped at
course length / duration, so marbles could never go fast.

## Storyboard

| Time | On screen | HUD |
|---|---|---|
| 0:00-0:05 setup | Whole run, hopper full, collector empty | Question card "HOW MANY OF 300 MARBLES MAKE IT IN 5 MINUTES?", then a small persistent subtitle |
| 0:05 | Hopper opens, first marbles plunge | Countdown runs |
| Running | Continuous stream at 6-9 m/s: plunge, banked switchbacks, funnelled peg fields, a helix, a jump-free long run, into the collector | Countdown + a thin progress bar; collector fill is the visual clock |
| Last 10 s | Ticks speed up; countdown pulses | Countdown turns red |
| All in (any time) | Big confetti, fanfare | "ALL 300 MADE IT!" |
| 0:00 | Finale: bell, confetti at the collector | "247 OF 300 MADE IT" (or the all-in card) |

## Rules for this build

- Speed: 6-9 m/s on the runs (2-3x v1). Banked bends (radius 6-8 m) so fast marbles stay on.
- **Every peg field has a funnel in front of it** (CLAUDE.md rule, 2026-11-12).
- Cartoon look: toon (cel) shading with outlines, saturated toy colours, rounded rail edges, sky and grass.
- HUD: fully on screen at 16:9 and any Game-view aspect (anchored to edges, scaled by height), cartoon styling.
- Camera: smooth tour along the stream (CLAUDE.md camera rules), wide establishing at start and at 0:00.
- Diversity: one mechanism per timer video; themes are palettes inside one mechanism; max one timer a week.

## Acceptance criteria

- [ ] Finale fires at exactly Duration (tick-exact)
- [ ] Mean marble speed on the runs 6-9 m/s (Stream Test); no jams (no marble stalled > 3 s); none lost off the track
- [ ] Collector 80-100% full at 0:00
- [ ] HUD fully visible at 1920x1080 and readable at 480 px wide
- [ ] Toon look reads as cartoon at 480 px; nothing is a bare default primitive

## Render + publish

1920x1080 @ 60 fps. Title is the search term ("5 Minute Marble Run Timer ..."). Record the logged seed here.
