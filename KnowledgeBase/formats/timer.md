# timer — Marble Run Timer

**Status:** `rework v2` (2026-11-12) · 3D · long form (16:9, 5:00 default; 10:00 and 15:00 are config)
**Plan:** `plans/videos/timer-001-marble-run-timer.md` · **Block:** `plans/blocks/countdown-clock.md`

## Idea (v2, 2026-11-12)
A pure marble-run timer: an exact countdown to 0:00 and a fast stream of 400 marbles (6-9 m/s) down a cartoon run (banked bends,
funnelled peg fields) into a glass collector that is nearly full at 0:00. No contest. The user chose this over a lap race; it
deliberately breaks the long-form story rules and is judged as a utility experiment.

**v1 lessons (race the gold clock marble, dropped):** a scripted "clock marble" read as an obstacle, not a clock; tying the clock to a
marble capped its speed at course length / duration (0.5-3 m/s felt dead); marbles slid past peg fields along the wall (now a CLAUDE.md
rule: funnel before every peg field); a stale config asset kept old damping (the builder now rewrites configs behind its defaults version).

## Why
Reference pull 2026-11-11 (`reference-channels.md` → *@onlinevisualtimer*): search-term countdown timers reached 1.0M views in 9 weeks. We add a contest inside the timer
so it satisfies the long-form rules (stake, acts, twist, recurring characters). Experiment; judge on day-7 views and Studio's average view duration.

## Diversity rule (user, 2026-11-11)
One timer mechanism per video, not a re-skin: next is a fuse maze. Themes (Neon, Christmas, Halloween, Winter) are palette configs inside one mechanism. Max one timer a week.
The channel was demonetisation-checked against the inauthentic-content rule (`reference-channels.md` → *Demonetisation risk*).

## Build
- Scene: **Simulation Lobby ▸ 3D Formats ▸ Marble Run Timer (timer) ▸ Race the Clock (timer-001)** → `Assets/Scenes/Timer_RaceTheClock.unity`.
- Tools: *Reset Config*, *Duration Test* (20 seeds, offline), *Reset Course*; `Simulation Lobby ▸ Tools ▸ Timer Clock Self-Check`.
- Tune `racerLinearDamping` first (model: racer finishes ~291 s vs clock 300 s at damping 1.2; real pegs/bends will add drag).

## Known risks
- Nothing compiled or run yet. A true fork is impossible on a single-path `TrackLayout`; the twist is a divider + bump lane in a steep chute into a pause pool.
- Peg-garden jams (a marble stalling on a peg) are unverified; the sim respawns off-track racers but does not nudge stalled ones.
- The scene builder duplicates Race's dark-studio setup; promote to `Shared`/`Presentation` when a third format needs it.
