# Block: Bounce Audio Sequencer

**Status:** `planned` · **Layer:** `Assets/Scripts/Shared/` · **Used by:** `esc`, `race`, `surv`

Each collision plays the next note of a melody. As the action accelerates, notes come faster and the
tune becomes recognizable, resolving near the climax.

## Why

This is the highest-leverage retention device in the genre — documented as a defining trait of the
formats that reached tens of millions of views (`KnowledgeBase/reference-channels.md`). It's not
post-production polish: **the collision event drives the note**, so it must be simulation-driven and
baked into the render to stay in sync.

Worth testing on competitive formats too — collision-triggered audio isn't inherently `esc`-only.

## Parameterized

| Knob | Notes |
|---|---|
| Note sequence | array of pitches/clips; advances one per trigger |
| On sequence end | `Loop` · `Hold` · `Advance octave` |
| Pitch ramp | optional pitch rise tracking escalation, layered under the melody |
| Velocity → volume | louder on harder impacts; keep subtle |

## Steps

1. `BounceAudioSequencer` — subscribes to the same collision event the escalation rule uses,
   advances an index, plays note[i].
2. Use a **pooled set of `AudioSource`s** (8–16). A single source cuts off its own previous note once
   bounces get fast, which destroys the melody exactly when it matters most.
3. Sequence stored in a `ScriptableObject` so melodies are swappable per video without code changes.
4. **Presentation layer — read-only.** It observes collisions; it must never affect physics. Audio
   must be removable without changing a run's outcome.
5. Expose `NotesPlayed` — this is the tunneling check in [collision-safety](collision-safety.md):
   notes played must equal collisions registered.

## Acceptance criteria

- [ ] Disabling audio entirely produces an identical `RunResult` (proves read-only).
- [ ] At peak bounce rate, no note is cut off by the next (verify the pool is deep enough).
- [ ] `NotesPlayed == collision count` for a full run.
- [ ] Melody is recognizable on phone speakers at the run's climax — check on an actual phone, not
      studio monitors.

## Licensing

**Public-domain or original melodies only.** Recognizable licensed music is a copyright-claim risk on
a monetized channel, and it's common practice in this genre precisely because most of those channels
aren't managing that risk. Safe sources: classical works out of copyright, or simple original motifs.
Note the melody's source in the video plan.

## Gotchas

- Latency: `PlayOneShot` on a pooled source is fine; instantiating an `AudioSource` per bounce is not.
- If bounces outpace note length, the tune turns to mush — cap effective note rate, or shorten clips
  as speed rises.
- Bake audio into the render. Reconstructing sync in the editor afterward is painful and fragile.
