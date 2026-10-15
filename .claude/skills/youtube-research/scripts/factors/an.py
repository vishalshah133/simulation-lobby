import json,re,os,sys,random,statistics as st
from datetime import datetime,timezone
sys.path.insert(0,'.claude/skills/youtube/scripts'); import yt
SP=os.path.dirname(os.path.abspath(__file__))
D=json.load(open(os.path.join(SP,"all.json")))
now=datetime.now(timezone.utc)
EMO=re.compile("[\U0001F300-\U0001FAFF\u2600-\u27BF]")
rows=[]
for ch,vs in D.items():
    if ch=="@simulationlobby": continue
    for v in vs:
        s=v["snippet"]; sec=yt.parse_duration(v["contentDetails"]["duration"])
        if sec>180 or sec==0: continue
        dt=datetime.fromisoformat(s["publishedAt"].replace("Z","+00:00"))
        if dt<datetime(2024,1,1,tzinfo=timezone.utc) or (now-dt).days<14: continue
        views=yt.num(v["statistics"].get("viewCount"))
        if views<=0: continue
        t=s["title"]; d=s.get("description","") or ""; tags=s.get("tags") or []
        low=t.lower()
        rows.append(dict(ch=ch,q=(dt.year,(dt.month-1)//3),lv=__import__("math").log(views),sec=sec,
          f={
          "title has ?":"?" in t,
          "title has digit":bool(re.search(r"\d",t)),
          "title has emoji":bool(EMO.search(t)),
          "title has #hashtag":"#" in t,
          "title has 4+ hashtags":t.count("#")>=4,
          "title long (>60ch)":len(t)>60,
          "title short (<=30ch)":len(t)<=30,
          "title has 'vs'":bool(re.search(r"\bvs\.?\b",low)),
          "title: song/guess/name":bool(re.search(r"song|guess|name (it|this|that)|tune|melody",low)),
          "title: 'how many/much'":"how many" in low or "how much" in low,
          "title: can/will it":bool(re.search(r"\b(can|will|could)\b",low)),
          "title: first person/reaction":bool(re.search(r"\b(it|this|that)\b.*(💀|😭|😱)|\bi\b|\bmy\b",low)),
          "title has ALLCAPS word":bool(re.search(r"\b[A-Z]{4,}\b",t)),
          "desc has #shorts":"#shorts" in (d+t).lower(),
          "desc has hashtags (3+)":d.count("#")>=3,
          "desc long (>200ch)":len(d)>200,
          "has tags":len(tags)>0,
          "tags 15+":len(tags)>=15,
          "publish weekend UTC":dt.weekday()>=5,
          "publish 00-06 UTC":dt.hour<6,
          "publish 06-12 UTC":6<=dt.hour<12,
          "publish 12-18 UTC":12<=dt.hour<18,
          "publish 18-24 UTC":dt.hour>=18,
          "dur <20s":sec<20,
          "dur 20-45s":20<=sec<45,
          "dur 45-75s":45<=sec<75,
          "dur >75s":sec>75,
          "category 24 Entertainment":s.get("categoryId")=="24",
          }))
# baseline: channel-quarter median of log views
from collections import defaultdict
g=defaultdict(list)
for r in rows: g[(r["ch"],r["q"])].append(r["lv"])
for r in rows:
    k=(r["ch"],r["q"]); m=st.median(g[k]) if len(g[k])>=5 else st.median([x["lv"] for x in rows if x["ch"]==r["ch"]])
    r["y"]=r["lv"]-m   # log ratio vs own channel-quarter median
print("videos analysed:",len(rows),"channels:",len({r['ch'] for r in rows}))
for c in sorted({r['ch'] for r in rows}): print("  ",c,sum(r['ch']==c for r in rows))
random.seed(1)
res=[]
for f in rows[0]["f"]:
    perch=[]
    for c in {r['ch'] for r in rows}:
        a=[r["y"] for r in rows if r["ch"]==c and r["f"][f]]
        b=[r["y"] for r in rows if r["ch"]==c and not r["f"][f]]
        if len(a)>=8 and len(b)>=8: perch.append((c,len(a),len(b),st.median(a)-st.median(b)))
    if len(perch)<3: res.append((f,None,perch)); continue
    effs=[p[3] for p in perch]
    # cluster bootstrap over channels
    bs=[]
    for _ in range(2000):
        smp=[random.choice(effs) for _ in effs]; bs.append(st.mean(smp))
    bs.sort(); lo,hi=bs[50],bs[1949]
    res.append((f,(st.mean(effs),lo,hi,sum(e>0 for e in effs),len(effs),sum(p[1] for p in perch)),perch))
import math
print("\nfactor | channels w/ data | mean effect (x views) | 90% CI (x) | channels positive | n with-factor")
for f,r,p in sorted(res,key=lambda x:-(x[1][0] if x[1] else -9)):
    if r is None: print(f"{f:32s} insufficient (channels with >=8 each side: {len(p)})"); continue
    m,lo,hi,pos,n,nw=r
    sig="  <-- CI excludes 1x" if (lo>0 or hi<0) else ""
    print(f"{f:32s} {n:2d} ch  {math.exp(m):5.2f}x  [{math.exp(lo):4.2f}-{math.exp(hi):4.2f}]  {pos}/{n} +  n={nw}{sig}")
json.dump([{k:v for k,v in r.items()} for r in rows],open(os.path.join(SP,"rows.json"),"w"))
