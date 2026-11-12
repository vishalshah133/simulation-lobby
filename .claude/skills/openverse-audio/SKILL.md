---
name: openverse-audio
description: Search and download openly licensed music and sound effects via the Openverse API (aggregates Freesound, Jamendo, Wikimedia and others) with license filtering, no API key needed. Use when asked to find background music, a song, ambience or a sound effect under a CC license, or to search several free audio libraries at once.
---

# Openverse audio search + download

`scripts/ov.py` (stdlib Python) wraps `https://api.openverse.org/v1/audio/`. **No key needed**
(anonymous rate limits are low, so keep searches small). Openverse aggregates Freesound, Jamendo,
Wikimedia Audio and more, so it is the one to use for **music**; use the `freesound` skill for
focused SFX with richer filters.

**House style first:** sound is procedural (`ProceduralToneBank`). Use sampled audio only when it
can't make the sound.

## Commands (run from project root)

```
python .claude/skills/openverse-audio/scripts/ov.py search "calm piano" [--license cc0|by] [--category music|sound_effect] [--source jamendo] [--min-dur 30] [--max-dur 180] [--limit 10] [--json]
python .claude/skills/openverse-audio/scripts/ov.py download <id> [<id> ...] [--out DIR]
```

- `--license cc0` (default) = CC0 + public domain, no credit. `by` = any commercial-use CC license
  (CC-BY, CC-BY-SA...); credit required. NonCommercial is never searched, and `download` refuses it
  unless `--allow-nc`, because the channel is monetised.
- **CC-BY-SA is share-alike and CC-BY-ND is no-derivatives** (both appear under `--license by`): flag
  them to the user rather than assuming they're fine for a video. Prefer plain CC-BY or CC0.
- Openverse's API returns intermittent 500s/timeouts; the script retries up to 5 times.
- Downloads go to `Assets/Audio/Sourced/Openverse/` and Unity imports them. `manifest.json` beside
  them records license, creator, source URL and the ready-made attribution line.
- Some results have no direct file URL; the script skips those and prints the landing page.
- Quality varies by source, so audition by landing page before committing a clip to a video. If any
  download has `attribution_required: true`, give the user the credit line for the video description
  and note it in the video's plan.
