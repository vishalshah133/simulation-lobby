#!/usr/bin/env python3
"""Freesound API v2 client for Simulation Lobby. Standard library only.

Reads FREESOUND_API_KEY from .env at the project root (or the environment).
The key is sent in an Authorization header, never in a URL, so it cannot leak into logs or output.

Subcommands:
  search   <query> [--license cc0|by|any] [--min-dur S] [--max-dur S] [--sort S] [--limit N] [--json]
  download <id> [<id> ...] [--out DIR] [--format mp3|ogg] [--quality hq|lq]

Downloads fetch the HQ *preview* (mp3/ogg). The original-quality file needs OAuth2, which an API key
alone cannot do. Every download is appended to <out>/manifest.json with its license + attribution.
"""

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass

API = "https://freesound.org/apiv2/"
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))))
DEFAULT_OUT = os.path.join(ROOT, "Assets", "Audio", "Sourced", "Freesound")
UA = "SimulationLobby-audio-skill/1.0"

# CC0 needs no credit. "by" adds CC-BY (credit required, commercial OK). Anything NonCommercial is
# excluded in every mode: the channel is monetised, so NC clips are unusable.
LICENSE_FILTERS = {
    "cc0": 'license:"Creative Commons 0"',
    "by": 'license:("Creative Commons 0" OR "Attribution")',
    "any": 'license:("Creative Commons 0" OR "Attribution" OR "Attribution NonCommercial" '
           'OR "Sampling+")',
}
FIELDS = "id,name,username,license,duration,tags,previews,url,avg_rating,num_downloads,description"


def load_key():
    key = os.environ.get("FREESOUND_API_KEY")
    if key:
        return key.strip()
    env_path = os.path.join(ROOT, ".env")
    if os.path.exists(env_path):
        with open(env_path, "r", encoding="utf-8-sig") as fh:
            for line in fh:
                line = line.strip()
                if line.startswith("FREESOUND_API_KEY="):
                    return line.split("=", 1)[1].strip().strip('"').strip("'")
    sys.exit("FREESOUND_API_KEY not found. Add FREESOUND_API_KEY=... to .env "
             "(get one at https://freesound.org/apiv2/apply).")


def get_json(path, params):
    url = API + path + "?" + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers={
        "Authorization": "Token " + load_key(), "User-Agent": UA})
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as err:
        sys.exit("Freesound HTTP %d: %s" % (err.code, err.read().decode("utf-8", "replace")[:300]))


def fmt_license(lic):
    return {"Creative Commons 0": "CC0", "Attribution": "CC-BY",
            "Attribution NonCommercial": "CC-BY-NC", "Sampling+": "Sampling+"}.get(lic, lic)


def cmd_search(args):
    filters = [LICENSE_FILTERS[args.license]]
    if args.min_dur is not None or args.max_dur is not None:
        lo = "*" if args.min_dur is None else args.min_dur
        hi = "*" if args.max_dur is None else args.max_dur
        filters.append("duration:[%s TO %s]" % (lo, hi))
    data = get_json("search/text/", {
        "query": args.query, "filter": " ".join(filters), "sort": args.sort,
        "fields": FIELDS, "page_size": args.limit})
    results = data.get("results", [])
    if args.json:
        print(json.dumps(results, indent=2))
        return
    print("%d total matches; showing %d (license: %s)\n" % (data.get("count", 0), len(results),
                                                           args.license))
    for r in results:
        print("[%s] %s  (%.1fs, %s, by %s)" % (r["id"], r["name"], r["duration"],
                                               fmt_license(r["license"]), r["username"]))
        print("      tags: %s" % ", ".join(r.get("tags", [])[:8]))
        print("      %s" % r["url"])


def safe_name(text):
    return re.sub(r"[^A-Za-z0-9._-]+", "_", text).strip("_")[:60] or "sound"


def cmd_download(args):
    os.makedirs(args.out, exist_ok=True)
    manifest_path = os.path.join(args.out, "manifest.json")
    manifest = []
    if os.path.exists(manifest_path):
        with open(manifest_path, "r", encoding="utf-8") as fh:
            manifest = json.load(fh)
    for sid in args.ids:
        info = get_json("sounds/%s/" % sid, {"fields": FIELDS})
        if "NonCommercial" in info["license"] and not args.allow_nc:
            print("[%s] skipped: %s is not usable on a monetised channel (pass --allow-nc to "
                  "override)" % (sid, fmt_license(info["license"])))
            continue
        key = "preview-%s-%s" % (args.quality, args.format)
        src = info["previews"][key]
        dest = os.path.join(args.out, "%s_%s.%s" % (sid, safe_name(info["name"].rsplit(".", 1)[0]),
                                                   args.format))
        req = urllib.request.Request(src, headers={"User-Agent": UA})
        with urllib.request.urlopen(req, timeout=60) as resp, open(dest, "wb") as out:
            out.write(resp.read())
        manifest = [m for m in manifest if m.get("id") != sid]
        manifest.append({
            "id": sid, "file": os.path.basename(dest), "title": info["name"],
            "creator": info["username"], "license": fmt_license(info["license"]),
            "attribution_required": info["license"] != "Creative Commons 0",
            "source_url": info["url"], "duration": info["duration"]})
        print("[%s] saved %s  (%s, by %s)" % (sid, os.path.relpath(dest, ROOT),
                                              fmt_license(info["license"]), info["username"]))
    with open(manifest_path, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawTextHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("search")
    s.add_argument("query")
    s.add_argument("--license", choices=sorted(LICENSE_FILTERS), default="cc0")
    s.add_argument("--min-dur", type=float)
    s.add_argument("--max-dur", type=float)
    s.add_argument("--sort", default="score",
                   choices=["score", "downloads_desc", "rating_desc", "duration_desc",
                            "duration_asc", "created_desc"])
    s.add_argument("--limit", type=int, default=10)
    s.add_argument("--json", action="store_true")
    s.set_defaults(fn=cmd_search)
    d = sub.add_parser("download")
    d.add_argument("ids", nargs="+")
    d.add_argument("--out", default=DEFAULT_OUT)
    d.add_argument("--format", choices=["mp3", "ogg"], default="ogg")
    d.add_argument("--quality", choices=["hq", "lq"], default="hq")
    d.add_argument("--allow-nc", action="store_true")
    d.set_defaults(fn=cmd_download)
    args = ap.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
