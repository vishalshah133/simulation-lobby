# YouTube API Setup

How to get credentials so `PROJECTS.md` view/retention columns can be filled automatically.

> **Navigation warning:** Google reorganized this console in 2024–2025. "OAuth consent screen" is now
> **Google Auth Platform** with Branding / Audience / Data Access / Clients tabs. Most tutorials
> online still describe the old single-page flow — the paths below are the current ones.

## First: which credential do you actually need?

| Need | Credential | Effort |
|---|---|---|
| Public view/like/comment counts (yours or anyone's) | **API key** | ~5 min |
| Watch time, avg view %, drop-off, traffic sources, subs gained | **OAuth client** | ~30 min |

Retros need avg view % and drop-off — those are **owner-only** and require OAuth. An API key cannot
return them for any channel, including your own. Get the API key first anyway; it's quick and the
OAuth flow reuses the same project.

---

## Part 1 — Create the project and enable APIs

1. Go to <https://console.cloud.google.com/> and sign in **with the Google account that owns the
   Simulation Lobby channel**. This matters — analytics are tied to channel ownership, not to the
   Cloud project.
2. Project dropdown (top bar) → **New Project**. Name it `simulation-lobby`. Create, then make sure
   it's the selected project.
3. Navigate to **APIs & Services → Library**.
4. Search **"YouTube Data API v3"** → **Enable**.
5. Search **"YouTube Analytics API"** → **Enable**. (Skip only if you truly just want public counts.)

Both APIs are free at this scale.

## Part 2 — API key (public data)

1. **APIs & Services → Credentials**.
2. **+ Create Credentials → API key**. It appears immediately — copy it.
3. **Restrict it right away** (click the key name):
   - *Application restrictions:* None is fine for a local CLI tool.
   - *API restrictions:* **Restrict key** → select **YouTube Data API v3** only.

   An unrestricted key that leaks can be used against any API in your project. Restricting it costs
   ten seconds.
4. Store it in the MCP server's env config — **never commit it**. `.gitignore` doesn't cover
   `.mcp.json`, so if you put the key there, add that file to `.gitignore` too.

**Quota:** 10,000 units/day. A video-stats lookup is ~1 unit, a search is ~100. Checking a few dozen
videos daily is nowhere near the ceiling.

## Part 3 — OAuth client (owner analytics)

1. **APIs & Services → Google Auth Platform**. If it's a fresh project you'll get a **Get started**
   wizard covering the steps below in one page.
2. **Branding** — app name (`Simulation Lobby Analytics`) and your support email. Nothing here is
   user-facing since only you will use it.
3. **Audience** — choose **External**. ("Internal" requires Google Workspace; a personal Gmail
   account can't use it.)
4. Still under Audience: add your own Google account under **Test users**. **This step is the one
   people miss** — while the app is in "Testing" status, only listed test users can authenticate,
   and omitting yourself produces an `access_denied` error that looks like a credentials problem.
5. **Data Access** → Add scopes. For read-only analytics:
   - `https://www.googleapis.com/auth/yt-analytics.readonly`
   - `https://www.googleapis.com/auth/youtube.readonly`

   Read-only scopes only. Nothing here should be able to modify the channel.
6. **Clients** → **+ Create client** → Application type: **Desktop app**. Name it, create.
7. Copy the **Client ID** and **Client secret**, or **Download JSON** (some servers expect
   `client_secret.json` on disk).

**Leave the app in "Testing" status.** Publishing triggers Google's verification review, which is
for apps serving other people. You are the only user — testing mode is correct. The only cost is
that refresh tokens expire every 7 days in testing mode, so you'll re-run the auth command
occasionally. Annoying, not blocking.

## Part 4 — Wire it into Claude Code

For the OAuth analytics server (`thejasmeetsingh/yt-analytics-mcp`):

```json
{
  "mcpServers": {
    "youtube-analytics": {
      "command": "yt-analytics-mcp",
      "env": {
        "GOOGLE_CLIENT_ID": "...apps.googleusercontent.com",
        "GOOGLE_CLIENT_SECRET": "...",
        "GOOGLE_REDIRECT_URL": "http://localhost:8080/callback"
      }
    }
  }
}
```

Then run the server's interactive token command once (`yt-analytics-mcp -token`) to complete the
browser consent flow. In Claude Code, prefix with `!` so the output lands in the session.

Verify with `/mcp` — the server should list its tools.

## Gotchas

- **Wrong Google account.** Credentials must be created under the account that owns the channel, or
  analytics calls return data for the wrong channel (or nothing).
- **Brand accounts.** If the channel lives under a Brand Account rather than your personal account,
  authenticate with the account that has Owner/Manager permission on it.
- **Forgot to add yourself as a test user** → `access_denied`. See step 4.
- **Analytics lag.** YouTube Analytics data is delayed a day or two and is unreliable for the first
  ~48h after publish. Fill `Views (7d)` no earlier than day 7.
- **Nothing to query before you publish.** Every tool returns empty on a channel with no uploads.

## When to do this

Not yet. Do it the week `esc-001` goes live — credentials are useless until there's data, and the
7-day refresh-token expiry in testing mode means setting it up early just means re-authenticating
later. Until then `PROJECTS.md` is filled by hand from YouTube Studio (~2 min per video, during the
retro, when Studio is open anyway).
