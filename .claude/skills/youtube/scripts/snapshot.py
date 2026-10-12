"""Append a timestamped stats row per video to KnowledgeBase/data/snapshots.csv.

Run once or twice a day (Task Scheduler). The API only returns current totals, so this is the
only way to get view *velocity* (views at 6h / 24h / 72h / 7d) after the fact.
    python .claude/skills/youtube/scripts/snapshot.py
"""
import csv, os, sys
from datetime import datetime, timezone
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import yt

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
path = os.path.join(root, "KnowledgeBase", "data", "snapshots.csv")
key = yt.load_key()
ch = yt.resolve_channel("@simulationlobby", key)
vids = yt.hydrate(yt.fetch_videos(yt.uploads_playlist(ch), key, limit=100), key)
new = not os.path.exists(path)
now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%MZ")
with open(path, "a", newline="", encoding="utf-8") as f:
    w = csv.writer(f)
    if new:
        w.writerow(["snapshot_utc", "video_id", "published_utc", "views", "likes", "comments", "channel_subs"])
    for v in vids:
        s = v["statistics"]
        w.writerow([now, v["id"], v["snippet"]["publishedAt"], s.get("viewCount", 0),
                    s.get("likeCount", 0), s.get("commentCount", 0),
                    ch["statistics"].get("subscriberCount", "")])
print(f"{len(vids)} videos appended to {path}")
