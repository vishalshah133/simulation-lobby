import json,re,os,sys,random,math,statistics as st
sys.path.insert(0,'.claude/skills/youtube/scripts'); import yt
from datetime import datetime,timezone
SP=os.path.dirname(os.path.abspath(__file__))
D=json.load(open(os.path.join(SP,"all.json"))); rows=json.load(open(os.path.join(SP,"rows.json")))
# re-attach title/publish info
meta={}
for ch,vs in D.items():
    for v in vs: meta[(ch,math.log(max(1,yt.num(v["statistics"].get("viewCount")))))]=v
for r in rows:
    v=meta[(r["ch"],r["lv"])]; r["t"]=re.sub(r"#\S+","",v["snippet"]["title"]).strip(); r["dt"]=datetime.fromisoformat(v["snippet"]["publishedAt"].replace("Z","+00:00"))
def test(name,fn,minn=5):
    perch=[]
    for c in {r['ch'] for r in rows}:
        a=[r["y"] for r in rows if r["ch"]==c and fn(r)]; b=[r["y"] for r in rows if r["ch"]==c and not fn(r)]
        if len(a)>=minn and len(b)>=minn: perch.append((c,len(a),st.median(a)-st.median(b)))
    if len(perch)<2: print(f"{name:34s} insufficient"); return
    e=[p[2] for p in perch]; random.seed(2)
    bs=sorted(st.mean(random.choices(e,k=len(e))) for _ in range(2000))
    print(f"{name:34s} {len(e)} ch {math.exp(st.mean(e)):5.2f}x [{math.exp(bs[100]):.2f}-{math.exp(bs[1899]):.2f}] {sum(x>0 for x in e)}/{len(e)}+ n={sum(p[1] for p in perch)}  "+" ".join(f"{p[0][1:5]}:{math.exp(p[2]):.1f}" for p in perch))
print("-- weekday (UTC) vs rest")
for i,n in enumerate("Mon Tue Wed Thu Fri Sat Sun".split()): test(n,lambda r,i=i:r["dt"].weekday()==i,8)
print("-- title length excluding hashtags")
test("clean title <=25ch",lambda r:len(r["t"])<=25,8); test("clean title 26-45ch",lambda r:26<=len(r["t"])<=45,8); test("clean title >45ch",lambda r:len(r["t"])>45,8)
print("-- topic/hook keywords")
K={"song/guess/melody":r"song|guess|melody|tune","reach center/core":r"center|centre|core|reach","break/shatter/destroy":r"break|shatter|destroy|crush|smash","multiply/split/count":r"split|multipl|\d{3,}|thousand|million","how many":r"how many","vs":r"\bvs\b|versus","escape/exit":r"escape|exit|gap","can it / will it / could":r"\b(can|will|could)\b","ending/finally/last":r"finally|last|ending|one more|never","soft/jelly/squeez":r"soft|jelly|squeez|squish","perfect fit/hole":r"fit|hole","ball":r"\bball","marble":r"marble","satisfying":r"satisfying","ASMR":r"asmr"}
for k,p in K.items(): test(k,lambda r,p=p:bool(re.search(p,r["t"].lower())),5)
