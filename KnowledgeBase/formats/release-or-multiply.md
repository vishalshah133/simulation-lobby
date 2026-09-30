# Format: Release or Multiply (`rom`)

Marbles fall through gates labeled with operations — ×2, ×5, +10, RELEASE, HALVE, ×0 — and the count
compounds or collapses on the way down. Built for Shorts: the number on screen *is* the hook.

## Rules on screen

The number is the rules. A huge running count top-center, gates labeled in the marbles' path. No
explanation line needed beyond the title.

## Build notes

- The count must be **enormous and always visible** — it's the thing viewers watch, not the marbles.
- Spawning must be pooled and capped. Design for a hard ceiling (e.g. 5,000 active) with the
  displayed count continuing past the visual cap, or the run will tank the frame rate.
- Gate layout should make the *final* gate the highest-variance one. The payoff is the last second.
- Tune so the expected value trends up but the variance is real — viewers need to believe it can fail.

## Pacing (Shorts)

| Time | Beat |
|---|---|
| 0–3s | Drop starts, count visible |
| 3–35s | Compounding through gates, count climbing |
| 35–50s | High-variance final gate |
| 50–55s | Final number held on screen |

## Variations

- Elimination gates that can zero the run entirely.
- Viewer choice: two paths, pinned-comment vote decides next video's route.
- Escalating series where each video starts from the previous video's ending number.
- Reverse: start huge and survive the halving gates.

## Failure modes

- Exponential growth destroying performance → pooling and a hard cap, non-negotiable.
- Number climbing so fast it becomes meaningless → cap multiplier magnitude, or use log-scaled pacing.
- Anticlimactic ending — the last gate must matter.

## Open questions

- Does the escalating series (carry-over numbers) actually build return viewership? Test over 5 videos.
- Best final-gate odds for a satisfying-but-tense finish — 70/30?
