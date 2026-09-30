# Production Pipeline

Idea → build → run → render → edit → publish → retro.

## 1. Idea

Pull from `ideas-backlog.md`. An idea is ready when you can state it in one sentence that is also
a plausible title, and name which format slug it uses (`race`, `wars`, `rom`, `elim`, `surv`).

## 2. Build

Reuse an existing scene + new config asset wherever possible. A genuinely new format gets a new scene
and a new brief in `formats/`.

Determinism checklist before rendering anything:

- [ ] Fixed timestep set explicitly (start at 0.02; drop to 0.01 for fast/small colliders).
- [ ] All randomness from the run's seeded `System.Random`, never `UnityEngine.Random`.
- [ ] No simulation logic in `Update` — `FixedUpdate` only.
- [ ] Same seed twice → identical finish order. Verify before committing a config.

## 3. Run selection

Batch many seeds headlessly or at low quality, log the outcomes, then pick runs that are actually
*good television*: close finishes, lead changes, late saves. Discard blowouts.

This is the step that separates a watchable channel from a random one. Curate outcomes by choosing
seeds — never by scripting the physics.

## 4. Render

| Target | Resolution | FPS | Notes |
|---|---|---|---|
| Shorts | 1080x1920 | 60 | Keep UI inside the middle ~80% — the app chrome eats edges |
| Long form | 1920x1080 | 60 | PC_RPAsset, full post |
| Preview | 540x960 | 30 | Mobile_RPAsset, for seed scanning only |

Use `Unity_Camera_Capture` for quick framing checks before committing to a full render.

## 5. Edit

- Cold open for long form: the climax first, then cut back to the start.
- Sound matters more than expected: impacts, a rising bed under the finish, a clean win sting.
- **Sound that responds to collisions should come from the simulation, not the editor.** A melody
  advancing one note per bounce is a design input to the physics tuning (see
  `formats/containment-escape.md`), and it has to be baked into the render to stay in sync.
  Use public-domain or original melodies — recognizable licensed music is a claim risk.
- Burn in the leaderboard/count from the sim rather than adding it in the editor — it stays accurate.

## 6. Publish

Title from the premise. Thumbnail with one subject and a big number. Pinned comment asking for the
pick. Short and long form cross-linked.

## 7. Retro

Within a few days, write `retros/YYYY-MM-DD-<slug>-<title>.md`: retention shape, where viewers dropped,
comment themes, and one concrete change for next time.

## Asset hygiene

- Configs and seeds are committed; video is not (`.gitignore` excludes media).
- One config asset per published video, named for the video, so any upload is re-renderable.
