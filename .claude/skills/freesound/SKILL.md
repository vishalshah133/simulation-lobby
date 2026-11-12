---
name: freesound
description: Search and download sound effects and audio clips from Freesound (freesound.org) with license filtering. Use when asked to find, fetch or download a sound effect, ambience, impact, whoosh, click, or any audio clip from Freesound, or when a video needs a sampled sound the procedural ToneBank can't make.
---

# Freesound audio search + download

`scripts/fs.py` (stdlib Python) wraps the Freesound API v2. Key is `FREESOUND_API_KEY` in `.env`
(gitignored). **Never print the key or paste it into a command line**; the script loads it and sends
it in a header. Get a key at https://freesound.org/apiv2/apply.

**House style first:** sound in this project is procedural (`Presentation/Audio/ProceduralToneBank`).
Only reach for a sampled clip when procedural can't do the job (real-world texture, voice, crowd).

## Commands (run from project root)

```
python .claude/skills/freesound/scripts/fs.py search "wood block hit" [--license cc0|by|any] [--min-dur 0.1] [--max-dur 3] [--sort downloads_desc] [--limit 10] [--json]
python .claude/skills/freesound/scripts/fs.py download <id> [<id> ...] [--out DIR] [--format ogg|mp3] [--quality hq|lq]
```

- `--license cc0` (default) is safe with no credit. `by` adds CC-BY (credit required in the video
  description). `any` also returns Sampling+ and NonCommercial clips for browsing only; `download`
  refuses NonCommercial unless `--allow-nc`, because the channel is monetised.
- Search results show id, duration, license, creator, tags and the page URL. Offer the user the top
  few and let them pick, or audition by URL, before downloading in bulk.
- Downloads go to `Assets/Audio/Sourced/Freesound/` as `<id>_<name>.ogg` and Unity imports them.
  Each download is recorded in `manifest.json` beside the files (license, creator, source URL,
  `attribution_required`).

## Limits

- Downloads are the **HQ preview** (OGG/MP3), not the original WAV; the original needs OAuth2, which
  an API key can't do. Previews are fine for short SFX; say so if the user needs lossless.
- If any downloaded clip has `attribution_required: true`, tell the user the credit line to add to the
  video description and record it in the video's plan.
- Freesound throttles API keys (about 60 requests/min, 2000/day). Search once, download deliberately.
