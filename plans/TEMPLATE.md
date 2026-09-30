# <slug>-<nnn> — <Title>

**Format:** `<slug>` · **Length:** ~__s · **Aspect:** 9:16 | 16:9 · **Status:** `planned`
**Brief:** `KnowledgeBase/formats/<format>.md`

## Premise

One sentence. It's usually also the title.

## Blocks used

`BUILD` = first implementation, this video pays the cost. `REUSE` = already exists, just verify.

- [ ] **REUSE** [determinism-core](../blocks/determinism-core.md)
- [ ] **REUSE** [seed-scan-harness](../blocks/seed-scan-harness.md)
- [ ] **REUSE** [capture-shorts](../blocks/capture-shorts.md)
- [ ] ...

> If a step you're about to write here already appears in another video plan, **extract it into a
> block instead**. Plans should get shorter over time — that's the system working.

## Video-specific work

Only what no block covers: this format's rules, this video's tuning, this video's look.

### Scene

### Simulation / rules

### Config + tuning targets

| Knob | Start at | Note |
|---|---|---|

### Look

## Acceptance criteria

- [ ] All block acceptance criteria met
- [ ] Same seed twice → identical outcome
- [ ] Readable at 480px wide; HUD clear of platform UI on a real phone
- [ ] Length within target band

## Seed selection

Scan range, scoring function, what makes a good run for this video.

## Render + publish

Resolution, thumbnail concept, pinned comment, music source.

## Retro

Write `KnowledgeBase/retros/YYYY-MM-DD-<slug>-<title>.md`, update `/PROJECTS.md`.
The open question this video answers: ______
