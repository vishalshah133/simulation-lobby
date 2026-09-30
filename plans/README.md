# Plans

One plan per video. Plans are **composed from reusable blocks**, not written from scratch — the same
principle the code follows (`KnowledgeBase/architecture.md`), applied to the production process.

```
plans/
  README.md      this file — how composition works
  TEMPLATE.md    starting point for a new video plan
  blocks/        reusable sub-plans, each one self-contained
  videos/        one plan per video, composed of block references + what's unique
```

## The rule

**A video plan contains only what is unique to that video.** Everything shared lives in a block and
is *referenced*, never copied.

If you find yourself writing the same steps into a second video plan, stop — extract a block. This is
the exact same move as promoting code into `Shared`, and the same test applies: could you delete this
video plan entirely and have the blocks still make sense on their own? If not, something leaked.

## How a video plan is structured

1. **Premise** — the one-sentence hook, which is usually also the title.
2. **Blocks used** — a checklist of block links, in build order. Each line says whether the block
   is being *built for the first time* or *reused as-is*.
3. **Video-specific work** — only the parts no block covers (this format's rule, its tuning, its look).
4. **Acceptance criteria** — what "done" means, concretely enough to check.
5. **Render + publish** — the specifics for this upload.

The first video using a block builds it; every later video just checks it off. Plan length should
drop sharply after the first few videos — that's the system working.

## Block rules

A block is a reusable sub-plan. To stay reusable it must:

- **Be independently understandable.** No "as described in esc-001".
- **Own one concern.** If it has two "and then also" sections, it's two blocks.
- **State acceptance criteria**, so a video plan can check it off without re-deriving what done means.
- **List what depends on it** (`Used by`), so changing it is a known-blast-radius decision.
- **Say what's parameterized** — the knobs a video plan sets, vs. what's fixed for everyone.

Blocks map roughly onto the code layers: most correspond to something in `Core`, `Shared`,
`Presentation`, or `Capture`. That's deliberate — a block that doesn't map to a reusable code
location is usually video-specific work in disguise.

## Status

Blocks carry a status (`planned` / `built` / `stable`) mirrored in `/PROJECTS.md`. A video can't
start until its blocks are at least `planned` and its *first-build* blocks are scheduled.

`stable` means: used by 3+ videos, no changes in the last 3. Changing a `stable` block requires
re-verifying the determinism baseline of every video listed under `Used by`.

## Naming

- Videos: `videos/<slug>-<nnn>-<short-title>.md` → `videos/esc-001-will-the-ball-escape.md`
- Blocks: `blocks/<concern>.md`, kebab-case, named for the concern — not for the video that birthed it.
