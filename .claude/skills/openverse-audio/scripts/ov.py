#!/usr/bin/env python3
"""Openverse audio client for Simulation Lobby. Standard library only.

Anonymous access works (no key). Anonymous rate limits are low, so keep searches small.

Subcommands:
  search   <query> [--license cc0|by|any] [--category music|sound_effect|...] [--source S]
                   [--min-dur S] [--max-dur S] [--limit N] [--json]
  download <id> [<id> ...] [--out DIR]

Every download is appended to <out>/manifest.json with its license + attribution line.
"""

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass

API = "https://api.openverse.org/v1/"
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
    os.path.abspath(__file__))))))
DEFAULT_OUT = os.path.join(ROOT, "Assets", "Audio", "Sourced", "Openverse")
UA = "SimulationLobby-audio-skill/1.0"


def get_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "application/json"})
    # The audio endpoint is sometimes slow on anonymous access, so retry timeouts before giving up.
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=60) as resp:
                return json.load(resp)
        except urllib.error.HTTPError as err:
            if err.code < 500 or attempt == 4:
                sys.exit("Openverse HTTP %d: %s" % (err.code,
                                                    err.read().decode("utf-8", "replace")[:200]))
            time.sleep(2)
        except (TimeoutError, urllib.error.URLError):
            if attempt == 4:
                sys.exit("Openverse timed out 5 times. Try again shortly or use the freesound skill.")
            time.sleep(2)


def cmd_search(args):
    params = {"q": args.query, "page_size": args.limit, "mature": "false"}
    # NonCommercial is excluded in every mode: the channel is monetised.
    if args.license == "cc0":
        params["license"] = "cc0,pdm"
    else:
        params["license_type"] = "commercial"
    if args.category:
        params["category"] = args.category
    if args.source:
        params["source"] = args.source
    data = get_json(API + "audio/?" + urllib.parse.urlencode(params))
    results = data.get("results", [])
    if args.min_dur is not None:
        results = [r for r in results if (r.get("duration") or 0) / 1000 >= args.min_dur]
    if args.max_dur is not None:
        results = [r for r in results if (r.get("duration") or 0) / 1000 <= args.max_dur]
    if args.json:
        print(json.dumps(results, indent=2))
        return
    print("%d total matches; showing %d (license: %s)\n" % (data.get("result_count", 0),
                                                           len(results), args.license))
    for r in results:
        secs = (r.get("duration") or 0) / 1000
        print("[%s] %s  (%.1fs, %s %s, by %s, %s)" % (
            r["id"], r.get("title"), secs, str(r.get("license", "")).upper(),
            r.get("license_version") or "", r.get("creator"), r.get("source")))
        print("      %s | %s" % (r.get("category"), r.get("foreign_landing_url")))


def safe_name(text):
    return re.sub(r"[^A-Za-z0-9._-]+", "_", text or "").strip("_")[:60] or "audio"


def cmd_download(args):
    os.makedirs(args.out, exist_ok=True)
    manifest_path = os.path.join(args.out, "manifest.json")
    manifest = []
    if os.path.exists(manifest_path):
        with open(manifest_path, "r", encoding="utf-8") as fh:
            manifest = json.load(fh)
    for aid in args.ids:
        # No trailing slash: the slashed detail URL returns a 500 from Openverse.
        info = get_json(API + "audio/%s" % aid)
        lic = str(info.get("license", ""))
        if "nc" in lic.split("-") and not args.allow_nc:
            print("[%s] skipped: license %s is not usable on a monetised channel "
                  "(--allow-nc to override)" % (aid, lic))
            continue
        src = info.get("url")
        if not src:
            print("[%s] skipped: no direct file URL (open %s)" % (aid, info.get("foreign_landing_url")))
            continue
        ext = (info.get("filetype") or os.path.splitext(urllib.parse.urlparse(src).path)[1].lstrip(".")
               or "mp3")
        dest = os.path.join(args.out, "%s_%s.%s" % (aid[:8], safe_name(info.get("title")), ext))
        req = urllib.request.Request(src, headers={"User-Agent": UA})
        with urllib.request.urlopen(req, timeout=90) as resp, open(dest, "wb") as out:
            out.write(resp.read())
        manifest = [m for m in manifest if m.get("id") != aid]
        manifest.append({
            "id": aid, "file": os.path.basename(dest), "title": info.get("title"),
            "creator": info.get("creator"),
            "license": "%s %s" % (lic.upper(), info.get("license_version") or ""),
            "attribution_required": lic.lower() not in ("cc0", "pdm"),
            "attribution": info.get("attribution"), "source": info.get("source"),
            "source_url": info.get("foreign_landing_url")})
        print("[%s] saved %s  (%s, by %s)" % (aid, os.path.relpath(dest, ROOT), lic.upper(),
                                              info.get("creator")))
    with open(manifest_path, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawTextHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("search")
    s.add_argument("query")
    s.add_argument("--license", choices=["cc0", "by"], default="cc0")
    s.add_argument("--category", help="music, sound_effect, podcast, ...")
    s.add_argument("--source", help="e.g. freesound, jamendo, wikimedia_audio")
    s.add_argument("--min-dur", type=float)
    s.add_argument("--max-dur", type=float)
    s.add_argument("--limit", type=int, default=10)
    s.add_argument("--json", action="store_true")
    s.set_defaults(fn=cmd_search)
    d = sub.add_parser("download")
    d.add_argument("ids", nargs="+")
    d.add_argument("--out", default=DEFAULT_OUT)
    d.add_argument("--allow-nc", action="store_true")
    d.set_defaults(fn=cmd_download)
    args = ap.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
