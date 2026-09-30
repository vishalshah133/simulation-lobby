# Learnings — video vs stats

Live pull from the YouTube Data API (`youtube` skill, `videos`/`report` commands), 2026-09-29.
Public data only — no retention/drop-off (needs OAuth + Analytics API, not set up).

| # | Date | Slug | Format | Title | Length | Views | Likes | Like% |
|---|---|---|---|---|---|---|---|---|
| 1 | 2025-05-01 | pre | — | How many balls will fit? | 48s | 11 | 4 | 36.36% |
| 2 | 2025-05-03 | pre | — | Marble Volcano 🌋 | 59s | 21 | 3 | 14.29% |
| 3 | 2026-09-27 | esc | Containment/escape | The Ball Grows Every Bounce. Can It Escape? | 2:12 | 712 | 8 | 1.12% |
| 4 | 2026-09-29 | esc | Containment/escape (rotating gap) | Will It Escape? The Exit Keeps Moving | 2:05 | 1,201 | 13 | 1.08% |

Total: 1,945 views across 4 videos.

## Observations (provisional — small numbers, short time since publish)

- **Both `esc` videos are still very fresh** (2 days and 0 days old at this pull). Views climb fast
  in the first 24–48h on Shorts regardless of quality — video 4's 1,201 vs video 3's 712 is **not**
  yet evidence the rotating-gap variant outperforms the original. Day-7/day-30 snapshots (due
  2026-10-04 and 2026-10-06, see `PROJECTS.md`) are the first point either number becomes comparable.
- **Video 4 has no seed** — `SimulationRunner.randomizeSeedOnStart` was turned on for this scene at
  the user's explicit request, so the published take can never be reproduced or re-rendered. This is
  a one-off exception; every other scene in the project keeps a committed, reproducible seed. Don't
  extend this pattern without deciding that again on purpose.
- **Both `esc` videos still dwarf the two pre-project videos** (712 + 1,201 vs. 11 + 21), but the
  eras remain incomparable: different channel state (dormant 16 months vs. active), different
  title/thumbnail conventions, and pre-project videos never got snapshotted at a comparable age.
- **Like% is stable around ~1.1%** for both current-era videos (1.12%, 1.08%) despite very different
  view counts — more useful than the pre-project videos' 36%/14%, which were almost certainly just
  early-subscriber effects at near-zero view counts, not a real like-rate.
- **First real same-format comparison is now possible** once both videos' day-7 snapshots land —
  video 3 (static gap) vs. video 4 (rotating gap) is the first genuine variant test on this channel.

## What would make this useful

- Record the day-7 snapshots on 2026-10-04 (video 3) and 2026-10-06 (video 4) — don't let the "due"
  markers in `PROJECTS.md` slip, they can't be recovered later.
- Once both snapshots exist, that's the first legitimate basis for a KB update to
  `containment-escape.md`'s variant table (does a timing mechanic help or hurt vs. pure size escalation).
- Set up OAuth for the Analytics API if retention ever needs to inform tuning decisions (see
  `KnowledgeBase/youtube-api-setup.md`).
