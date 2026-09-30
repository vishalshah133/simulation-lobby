---
name: youtube
description: Pull live stats for the Simulation Lobby YouTube channel (@simulationlobby) or any reference channel — subscribers, video view counts, like rates, Shorts vs long-form breakdown. Use when asked how a video or the channel is performing, what's getting views, which format is working, when updating PROJECTS.md with view numbers, or when researching what competitor/reference channels are publishing.
---

# YouTube channel data

Pulls live data from the YouTube Data API v3 via `scripts/yt.py` (stdlib Python, no dependencies).

**Our channel:** `@simulationlobby` — channel id `UCRyMyxpvggZYYIpwn-DtrlQ`.
When the user says "the channel", "our channel", or "my channel", that's this one.

The key is read from `YOUTUBE_API_KEY` in `.env` at the project root. `.env` is gitignored.
**Never print the key, and never paste it into a command line** — the script loads it itself.

## Commands

Run from the project root:

```
python .claude/skills/youtube/scripts/yt.py channel @simulationlobby
python .claude/skills/youtube/scripts/yt.py videos  @simulationlobby [--limit N] [--sort views|date] [--shorts] [--long]
python .claude/skills/youtube/scripts/yt.py video   <videoId> [<videoId> ...]
python .claude/skills/youtube/scripts/yt.py report  @simulationlobby
python .claude/skills/youtube/scripts/yt.py quota
```

Add `--json` to any command when you need to compute over the results rather than show them.

- **`channel`** — subs, video count, total views. Start here for "how's the channel doing".
- **`videos`** — per-video stats table. `--sort views` answers "what's working best".
  `--shorts` / `--long` splits the two content types.
- **`video`** — detail for specific ids, e.g. after the user pastes a URL.
- **`report`** — markdown rows formatted for the Published videos table in `/PROJECTS.md`.

Works on any public channel, not just ours — pass another handle to research a reference channel
(see the `youtube-research` skill for that workflow).

## Critical limitation — read before answering any retention question

An API key returns **public data only**: views, likes, comments, duration. It **cannot** return:

- average view duration / average percentage viewed
- audience retention curves or drop-off points
- traffic sources, impressions, click-through rate
- subscribers gained per video, watch time

Those are owner-only metrics requiring the **YouTube Analytics API with OAuth** — a different
credential, not yet set up (see `KnowledgeBase/youtube-api-setup.md`). If the user asks about
retention or drop-off, say plainly that it needs YouTube Studio or OAuth setup. **Do not substitute
like-rate as a proxy for retention and present it as if it were the real thing** — say what the
number actually is.

`retros/TEMPLATE.md` asks for "Avg % viewed" and "biggest drop-off". Both come from Studio by hand
until OAuth exists.

## Updating PROJECTS.md

1. Run `report @simulationlobby`.
2. Merge into the Published videos table — **don't blind-overwrite**. The existing rows carry
   slug, seed, config, and retro links that the API has no idea about. Preserve those columns.
3. The `Views (7d)` / `Views (30d)` columns are meant to be *snapshots at those ages*. The API only
   returns current totals, so a row filled late reads high. The script prints `_pending_` for videos
   younger than the threshold; for older ones, keep whatever was recorded on time rather than
   replacing it with today's number.
4. Update the "What's working" section only when there's enough data to support a conclusion. Two
   videos is not a trend.

## Quota

10,000 units/day, free. A full channel report costs ~3 units. The script deliberately avoids
`search.list` (100 units). Effectively unlimited for this use.

## Interpreting small numbers

The channel is small (2 videos, 26 total views as of Sept 2026, published May 2025). At this scale
**view differences are noise, not signal.** 17 views vs 9 views is not a format insight. Resist
drawing conclusions from single-digit deltas — say the sample is too small and keep the finding
provisional. Like-rate is more meaningful than raw views at low volume, but still not conclusive.
