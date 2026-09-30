#!/usr/bin/env python3
"""YouTube Data API v3 client for Simulation Lobby. Standard library only.

Reads YOUTUBE_API_KEY from .env at the project root (or the environment).
Quota-efficient: uses the uploads playlist rather than search() (1 unit vs 100).

Subcommands:
  channel  <handle|id>              Channel summary
  videos   <handle|id> [--limit N] [--sort views|date] [--shorts|--long]
  video    <video-id> [...]         Detail for specific video ids
  report   <handle|id>              Markdown rows ready for PROJECTS.md
  quota                             Explain quota cost of the above

Handles may be given as @SimulationLobby, SimulationLobby, or a UC... channel id.
Add --json to any command for raw machine-readable output.
"""

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

# Windows consoles default to cp1252, which cannot encode emoji or the zero-width spaces that show up
# in real video titles — printing a report would crash on UnicodeEncodeError rather than on anything
# to do with the API. Force UTF-8 on the way out.
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass
from datetime import datetime, timezone

API = "https://www.googleapis.com/youtube/v3/"
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))))


def load_key():
    key = os.environ.get("YOUTUBE_API_KEY")
    if key:
        return key.strip()
    env_path = os.path.join(ROOT, ".env")
    if os.path.exists(env_path):
        with open(env_path, "r", encoding="utf-8-sig") as fh:
            for line in fh:
                line = line.strip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                name, _, value = line.partition("=")
                if name.strip() == "YOUTUBE_API_KEY":
                    return value.strip().strip("'\"")
    die("YOUTUBE_API_KEY not found in environment or .env at the project root.")


def die(msg, code=1):
    print(f"error: {msg}", file=sys.stderr)
    sys.exit(code)


def call(endpoint, key, **params):
    params["key"] = key
    url = API + endpoint + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        try:
            detail = json.loads(body)["error"]
            reason = detail.get("errors", [{}])[0].get("reason", "")
            message = detail.get("message", body)
        except Exception:
            reason, message = "", body
        if reason == "quotaExceeded":
            die("Daily quota (10,000 units) exhausted. Resets at midnight Pacific.")
        if reason in ("keyInvalid", "badRequest") and "API key not valid" in message:
            die("API key rejected. Check YOUTUBE_API_KEY in .env, and that the key's "
                "API restrictions include YouTube Data API v3.")
        if reason == "accessNotConfigured":
            die("YouTube Data API v3 is not enabled on this Google Cloud project. "
                "Enable it under APIs & Services -> Library.")
        die(f"HTTP {e.code} ({reason}): {message}")
    except urllib.error.URLError as e:
        die(f"network error: {e.reason}")


# ---------------------------------------------------------------- resolution

def resolve_channel(ident, key):
    """Return the channels.list item for a handle, id, or bare name."""
    ident = ident.strip()
    if re.fullmatch(r"UC[\w-]{22}", ident):
        params = {"id": ident}
    else:
        params = {"forHandle": ident if ident.startswith("@") else "@" + ident}
    data = call("channels", key, part="snippet,statistics,contentDetails,brandingSettings",
                **params)
    items = data.get("items") or []
    if not items:
        die(f"no channel found for '{ident}'. Handles are exact and case-insensitive but "
            f"must match the @handle in the channel URL, not the display name.")
    return items[0]


def uploads_playlist(channel):
    return channel["contentDetails"]["relatedPlaylists"]["uploads"]


def fetch_videos(playlist_id, key, limit=50):
    """Video ids from the uploads playlist, newest first. 1 unit per 50."""
    ids, token = [], None
    while len(ids) < limit:
        page = call("playlistItems", key, part="contentDetails",
                    playlistId=playlist_id, maxResults=min(50, limit - len(ids)),
                    **({"pageToken": token} if token else {}))
        ids += [i["contentDetails"]["videoId"] for i in page.get("items", [])]
        token = page.get("nextPageToken")
        if not token:
            break
    return ids[:limit]


def hydrate(video_ids, key):
    """Full stats for video ids. 1 unit per 50."""
    out = []
    for i in range(0, len(video_ids), 50):
        chunk = video_ids[i:i + 50]
        data = call("videos", key, part="snippet,statistics,contentDetails",
                    id=",".join(chunk))
        out += data.get("items", [])
    return out


# ---------------------------------------------------------------- formatting

def parse_duration(iso):
    m = re.fullmatch(r"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?", iso or "")
    if not m:
        return 0
    h, mi, s = (float(x) if x else 0 for x in m.groups())
    return int(h * 3600 + mi * 60 + s)


def fmt_duration(sec):
    return f"{sec // 60}:{sec % 60:02d}" if sec >= 60 else f"{sec}s"


def days_since(iso):
    try:
        published = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return None
    return (datetime.now(timezone.utc) - published).days


def num(v):
    try:
        return int(v)
    except (TypeError, ValueError):
        return 0


def simplify(v):
    stats = v.get("statistics", {})
    secs = parse_duration(v.get("contentDetails", {}).get("duration"))
    views = num(stats.get("viewCount"))
    likes = num(stats.get("likeCount"))
    return {
        "id": v["id"],
        "title": v["snippet"]["title"],
        "published": v["snippet"]["publishedAt"][:10],
        "days": days_since(v["snippet"]["publishedAt"]),
        "seconds": secs,
        # YouTube treats <= 3 min vertical as Shorts-eligible; duration is the only
        # signal the public API exposes, so this is a heuristic, not ground truth.
        "is_short": secs <= 180,
        "views": views,
        "likes": likes,
        "comments": num(stats.get("commentCount")),
        "like_rate": round(likes / views * 100, 2) if views else 0.0,
        "url": f"https://youtube.com/watch?v={v['id']}",
    }


def table(rows, headers):
    widths = [max(len(str(r[i])) for r in [headers] + rows) for i in range(len(headers))]
    line = lambda r: "  ".join(str(c).ljust(widths[i]) for i, c in enumerate(r)).rstrip()
    return "\n".join([line(headers), "  ".join("-" * w for w in widths)]
                     + [line(r) for r in rows])


# ---------------------------------------------------------------- commands

def cmd_channel(args, key):
    ch = resolve_channel(args.channel, key)
    st = ch.get("statistics", {})
    if args.json:
        print(json.dumps(ch, indent=2))
        return
    sn = ch["snippet"]
    print(f"{sn['title']}  ({sn.get('customUrl', 'no handle')})")
    print(f"id:          {ch['id']}")
    print(f"created:     {sn['publishedAt'][:10]}")
    print(f"subscribers: {num(st.get('subscriberCount')):,}"
          + ("  (hidden)" if st.get("hiddenSubscriberCount") else ""))
    print(f"videos:      {num(st.get('videoCount')):,}")
    print(f"total views: {num(st.get('viewCount')):,}")
    if sn.get("description"):
        print(f"\n{sn['description'][:300]}")


def _video_rows(args, key):
    ch = resolve_channel(args.channel, key)
    ids = fetch_videos(uploads_playlist(ch), key, args.limit)
    if not ids:
        die(f"'{ch['snippet']['title']}' has no public uploads yet. "
            "Nothing to report until the first video is published.")
    vids = [simplify(v) for v in hydrate(ids, key)]
    if args.shorts:
        vids = [v for v in vids if v["is_short"]]
    if args.long:
        vids = [v for v in vids if not v["is_short"]]
    if args.sort == "views":
        vids.sort(key=lambda v: v["views"], reverse=True)
    return ch, vids


def cmd_videos(args, key):
    ch, vids = _video_rows(args, key)
    if args.json:
        print(json.dumps(vids, indent=2))
        return
    rows = [[v["published"], "S" if v["is_short"] else "L", fmt_duration(v["seconds"]),
             f"{v['views']:,}", f"{v['likes']:,}", f"{v['like_rate']}%",
             v["title"][:58]] for v in vids]
    print(f"{ch['snippet']['title']} — {len(vids)} videos "
          f"(sorted by {'views' if args.sort == 'views' else 'date, newest first'})\n")
    print(table(rows, ["DATE", "T", "LEN", "VIEWS", "LIKES", "LIKE%", "TITLE"]))
    if vids:
        tot = sum(v["views"] for v in vids)
        print(f"\ntotal {tot:,} views · median "
              f"{sorted(v['views'] for v in vids)[len(vids) // 2]:,} · "
              f"best {max(v['views'] for v in vids):,}")


def cmd_video(args, key):
    vids = [simplify(v) for v in hydrate(args.ids, key)]
    if not vids:
        die("no videos found for those ids")
    if args.json:
        print(json.dumps(vids, indent=2))
        return
    for v in vids:
        print(f"{v['title']}\n  {v['url']}")
        print(f"  published {v['published']} ({v['days']}d ago) · {fmt_duration(v['seconds'])}"
              f" · {'Short' if v['is_short'] else 'long form'}")
        print(f"  {v['views']:,} views · {v['likes']:,} likes ({v['like_rate']}%)"
              f" · {v['comments']:,} comments\n")


def cmd_report(args, key):
    """Markdown rows for the Published videos table in PROJECTS.md."""
    args.sort = "date"
    ch, vids = _video_rows(args, key)
    print(f"<!-- {ch['snippet']['title']} · generated {datetime.now():%Y-%m-%d} · "
          f"public Data API stats only -->\n")
    print("| # | Date | Slug | Title | Length | Seed | Config | Views (7d) | Views (30d) | Avg % | Retro |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for n, v in enumerate(reversed(vids), 1):
        age = v["days"]
        v7 = f"{v['views']:,}" if age is not None and age >= 7 else "_pending_"
        v30 = f"{v['views']:,}" if age is not None and age >= 30 else "_pending_"
        print(f"| {n} | {v['published']} | ? | {v['title'][:40]} | "
              f"{fmt_duration(v['seconds'])} | ? | ? | {v7} | {v30} | n/a | ? |")
    print("\n<!-- 'Avg %' needs the YouTube Analytics API (OAuth, owner-only) — an API key "
          "cannot return it. Fill by hand from YouTube Studio. See "
          "KnowledgeBase/youtube-api-setup.md. Views columns show CURRENT totals, not a "
          "snapshot at day 7/30; record them on time or they drift high. -->")


def cmd_quota(args, key):
    print("""YouTube Data API v3 — 10,000 units/day, free, resets midnight Pacific.

  channel        1 unit
  videos  N      1 + ceil(N/50)*2   (50 videos ~ 3 units)
  video   N ids  ceil(N/50)
  report  N      same as videos

This script never calls search.list (100 units) — it reads the uploads playlist
instead. You would need ~3,000 full channel reports to exhaust a day's quota.""")


def main():
    p = argparse.ArgumentParser(prog="yt.py", description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    def channel_args(sp, listing=False):
        sp.add_argument("channel", help="@handle, bare handle, or UC... channel id")
        sp.add_argument("--json", action="store_true")
        if listing:
            sp.add_argument("--limit", type=int, default=50)
            sp.add_argument("--sort", choices=["views", "date"], default="date")
            sp.add_argument("--shorts", action="store_true", help="only Shorts (<=3min)")
            sp.add_argument("--long", action="store_true", help="only long form")

    channel_args(sub.add_parser("channel", help="channel summary"))
    channel_args(sub.add_parser("videos", help="list videos with stats"), listing=True)
    channel_args(sub.add_parser("report", help="markdown rows for PROJECTS.md"), listing=True)

    sp = sub.add_parser("video", help="detail for specific video ids")
    sp.add_argument("ids", nargs="+")
    sp.add_argument("--json", action="store_true")

    sub.add_parser("quota", help="explain quota costs")

    args = p.parse_args()
    key = load_key()
    {"channel": cmd_channel, "videos": cmd_videos, "video": cmd_video,
     "report": cmd_report, "quota": cmd_quota}[args.cmd](args, key)


if __name__ == "__main__":
    main()
