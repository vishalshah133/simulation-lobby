import sys,json,os
sys.path.insert(0,'.claude/skills/youtube/scripts')
import yt
key=yt.load_key()
SP=os.path.dirname(os.path.abspath(__file__))
chs="@satisfying.ba11s @funcflow @CodeCraftedPhysics @satisfying2dsims @AlgoMarblerYT @renderzen @Oyen_3D @Kawaken_3DCG @MusicMarble3D @moreballs @simulationlobby".split()
out={}
for h in chs:
    ch=yt.resolve_channel(h,key)
    ids=yt.fetch_videos(yt.uploads_playlist(ch),key,limit=700)
    vs=yt.hydrate(ids,key)
    out[h]=vs
    print(h,len(vs),file=sys.stderr)
json.dump(out,open(os.path.join(SP,"all.json"),"w"))
