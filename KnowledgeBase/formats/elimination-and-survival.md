# Formats: Elimination (`elim`) and Survival (`surv`)

Two related structures that both work by *removing* contenders over time.

---

## Elimination / Bracket (`elim`)

Rounds of heats; losers drop out until one remains. Long-form native — the bracket gives the video
an explicit structure and a visible progress bar, which is excellent for watch time.

### Build notes

- Show the bracket between rounds. It tells viewers how much is left and why to stay.
- Reuse the `race` or `wars` machinery per heat; `elim` is a wrapper around them.
- 16 → 8 → 4 → 2 is the sweet spot: four acts, each shorter than the last.
- Name contenders. Bracket formats are where persistent identities pay off most.

### What makes a good run

An early upset. Seed-scan for runs where a top-seeded contender goes out in round one.

### Failure modes

- Rounds feel identical → vary track/arena per round.
- Bracket too large; the early rounds drag → cap at 16.

---

## Survival (`surv`)

Contenders endure a hostile, usually shrinking arena. No goal but to last. Works in both lengths:
tight 40s Shorts version, or a slow-escalation long form.

### Build notes

- Escalate on a schedule the viewer can feel: visible shrink, faster hazards, rising audio.
- A survivor counter is mandatory — "23 left" is the retention device.
- The final 1v1 should be given room; slow the escalation once two remain.

### Variations

- **Wall vs Ball** (**built**, first `surv` video): one wall of blocks takes escalating hits until
  nothing stands. Hook: guess how many hits. The blocks are the "contenders" and the survivor
  counter is blocks standing. Full brief: [survival-wall.md](survival-wall.md).
- Shrinking arena (battle-royale ring).
- Rising water/lava.
- Hazards that spawn faster over time.
- Safe zones that move.

### Failure modes

- Random early deaths feel unfair and unsatisfying → hazards should threaten visibly before killing.
- Everyone dies at once at the end → decelerate escalation when survivors drop below ~4.

### Open questions

- Do viewers prefer a named survivor to win, or a random one? Test naming vs anonymous.
