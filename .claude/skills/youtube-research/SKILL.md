---
name: youtube-research
description: Research other YouTube channels in the physics-simulation genre (FuncFlow, satisfying.ba11s, Jack's Physics, marble race and bouncing-ball channels) to find what formats, lengths, titles and cadences are actually getting views. Use when asked what competitors are doing, what's working in the genre, whether a format is still performing, or to fill in KnowledgeBase/reference-channels.md.
---

# Reference channel research

Studies other channels in the genre so format decisions follow evidence instead of guesswork.
Findings go into `KnowledgeBase/reference-channels.md`.

Uses the same script as the `youtube` skill — it works on any public channel:

```
python .claude/skills/youtube/scripts/yt.py channel @FuncFlow
python .claude/skills/youtube/scripts/yt.py videos  @FuncFlow --sort views --limit 50
python .claude/skills/youtube/scripts/yt.py videos  @FuncFlow --shorts --sort date
```

## Why the API and not web fetching

**YouTube channel pages are JavaScript-rendered — WebFetch returns only the page footer.** An
earlier attempt to read FuncFlow's uploads that way produced nothing, which is why
`reference-channels.md` carries an unverified-research caveat. The API is the reliable path and
returns real numbers. When you verify something previously marked unverified, **remove the caveat
from that section** rather than leaving it to rot.

## Channels to study

| Handle | Why |
|---|---|
| `@FuncFlow` | Named popularizer of the "will the ball escape" format — our `esc` inspiration |
| `@satisfying.ba11s` | Originated the format (Nov 2023); videos at 38M–67M views |
| Jack's Physics, CodeCraftedPhysics | Also named in the genre; handles unconfirmed |

Handles may be wrong or renamed — if `no channel found`, search YouTube for the current handle
rather than guessing variations. The handle must match the `@` in the channel URL, not the display name.

## What to extract

1. **Is the format still alive in 2026?** Compare view counts on recent uploads vs. their peak.
   This is the open question blocking a commitment to an `esc` series — a channel whose 2023 videos
   did 10M and whose 2026 videos do 20k has answered it.
2. **Cadence** — upload frequency from the date column.
3. **Length** — where the Shorts cluster (`--shorts`, read the LEN column).
4. **Title patterns** — question form? numbers? emoji? Copy the shape, not the wording.
5. **Which escalation variants recur** — titles usually reveal grow / shrink / speed / layers.
6. **Outliers** — `--sort views` and look at what the top 3 do that the median doesn't.

## Interpreting the numbers honestly

- **Views correlate with age.** A 2023 video has had years to accumulate. Compare like-rate and
  views-per-day-since-publish, not raw totals, when comparing across eras.
- **Like-rate is the cleanest public quality signal** — it's roughly age-independent.
- **Retention is invisible.** The public API cannot return it for channels you don't own. Never
  claim to know another channel's retention or drop-off.
- **Survivorship bias.** Successful channels in a genre don't prove the genre works now; the failed
  ones aren't visible. Treat a single channel's success as weak evidence.
- Don't infer a channel's strategy from titles alone. Note what's observed vs. what's inferred.

## Writing up

Update `KnowledgeBase/reference-channels.md`: what we take, what we deliberately don't, and what's
still unverified. Record the **date of the data pull** — view counts go stale and a year-old
observation shouldn't be read as current.

Keep the existing structure: our differentiator is the *lobby* framing (many formats, one visual
language) rather than the one-trick channel these references mostly are. Research informs format
choice; it doesn't turn us into a copy.
