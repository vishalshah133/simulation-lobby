# Knowledge Base

Brainstorming, strategy, and production notes for the **Simulation Lobby** channel. Plain markdown,
deliberately outside `Assets/` so Unity never imports it.

## Structure

| Path | What lives here |
|---|---|
| `channel-strategy.md` | Positioning, audience, upload cadence, Shorts→long-form funnel |
| `architecture.md` | Layering, assembly definitions, isolation rules, determinism — the reasoning behind `CLAUDE.md` |
| `production-pipeline.md` | Idea → build → render → edit → publish, and the render settings |
| `formats/` | One brief per content format — rules, hooks, failure modes, variations |
| `ideas-backlog.md` | Raw idea dump, unfiltered. Promote to a format brief when it earns it |
| `reference-channels.md` | What's working on other channels in this genre, and what to take from it |
| `youtube-api-setup.md` | Getting API key / OAuth credentials so `PROJECTS.md` can auto-fill |
| `retros/` | Per-video post-mortems: what the numbers said, what to change |

## How to use it

Before building a simulation, read its format brief. After publishing, write a retro. The retro file
is where the channel actually compounds — code is cheap, knowing which hook retains is not.

Naming: `retros/YYYY-MM-DD-<slug>-<short-title>.md`, e.g. `2026-10-02-race-100-marbles-spiral.md`.
