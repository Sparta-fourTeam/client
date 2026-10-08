"""Copy image_gen outputs unchanged; inspect alpha to plan native Unity sprite slicing."""
from pathlib import Path
import ast, hashlib, json, shutil, struct, zlib
from collections import deque
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'docs/asset-previews'
manifest_path=DOC/'sheet-layer-manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
tree=ast.parse((DOC/'finalize_assets.py').read_text(encoding='utf-8-sig'))
decoder=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='rgba')
exec(compile(ast.Module(body=[decoder],type_ignores=[]),'<read-only PNG decoder>','exec'))

def bounds(pixels,w,h,cell):
 x0,y0,x1,y1=cell; cw=x1-x0; ch=y1-y0
 mask=bytearray(pixels[((y0+y)*w+x0+x)*4+3]>=160 for y in range(ch) for x in range(cw))
 components=[]
 for index in range(len(mask)):
  if not mask[index]:continue
  mask[index]=0; queue=deque([index]); count=0; minx=cw; miny=ch; maxx=maxy=0
  while queue:
   pos=queue.popleft(); y,x=divmod(pos,cw);count+=1
   minx=min(minx,x);maxx=max(maxx,x);miny=min(miny,y);maxy=max(maxy,y)
   for nxt in (pos-1 if x else -1,pos+1 if x+1<cw else -1,pos-cw if y else -1,pos+cw if y+1<ch else -1):
    if nxt>=0 and mask[nxt]:mask[nxt]=0;queue.append(nxt)
  components.append((count,minx,miny,maxx,maxy))
 assert components,cell
 cutoff=max(c[0] for c in components)*.01
 selected=[c for c in components if c[0]>=cutoff]
 return [max(x0,x0+min(c[1] for c in selected)-3),max(y0,y0+min(c[2] for c in selected)-3),min(x1,x0+max(c[3] for c in selected)+4),min(y1,y0+max(c[4] for c in selected)+4)]

plan=[]
for a in manifest:
 p=ROOT/a['path']; assert p.resolve().is_relative_to(ROOT.resolve())
 p.parent.mkdir(parents=True,exist_ok=True)
 shutil.copyfile(a['generatedSource'],p)
 data=p.read_bytes();w,h=struct.unpack('>II',data[16:24]);a.update(width=w,height=h,sha256=hashlib.sha256(data).hexdigest())
 rects=[]
 if a.get('fullCanvas'):
  rects=[dict(name=a['names'][0],rect=[0,0,w,h],border=[0,0,0,0])]
 else:
  iw,ih,pixels=rgba(p); assert (iw,ih)==(w,h)
  alpha=pixels[3::4];assert min(alpha)==0 and max(alpha)>200
  a['cornerAlpha']=[alpha[0],alpha[w-1],alpha[-w],alpha[-1]]
  assert max(a['cornerAlpha'])<=2,(a['key'],a['cornerAlpha'])
  for i,name in enumerate(a['names']):
   col=i%a['cols'];row=i//a['cols'];
   y0=round(row*h/a['rows']);y1=round((row+1)*h/a['rows'])
   if a['key']=='WallStates':y0=[0,round(h*.40),round(h*.66)][row];y1=[round(h*.40),round(h*.66),h][row]
   x0,top,x1,bottom=bounds(pixels,w,h,(round(col*w/a['cols']),y0,round((col+1)*w/a['cols']),y1))
   bw=x1-x0;bh=bottom-top;rect=[x0,h-bottom,bw,bh]
   border=round(min(bw,bh)*(.24 if a['key']=='UIFrames' else .08)) if a['key'] in ['UIFrames','UIFills','UIStrokes'] else 0
   rects.append(dict(name=name,rect=rect,border=[border]*4))
  if a['key']=='WallStates':
   left=min(r['rect'][0] for r in rects);right=max(r['rect'][0]+r['rect'][2] for r in rects);width=right-left
   height=max(max(r['rect'][3] for r in rects),round(width*54/440))
   for r in rects:r['rect']=[left,r['rect'][1],width,height];r['border']=[round(width*13/440),round(width*12/440),round(width*12/440),0]
   a['pixelsPerUnit']=width/4.4
 a['sprites']=rects;a['maxSize']=2048 if a['key'] in ['LobbySky','LobbyGrass'] else 1024
 for r in rects:
  assert r['rect'][0]>=0 and r['rect'][1]>=0 and r['rect'][0]+r['rect'][2]<=w and r['rect'][1]+r['rect'][3]<=h,(a['key'],r)
  plan.append('\t'.join(map(str,[a['path'],r['name'],*r['rect'],*r['border'],a.get('pixelsPerUnit',100),a['maxSize'],'single' if a.get('fullCanvas') else 'multiple'])))
manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DOC/'sheet-slice-plan.tsv').write_text('\n'.join(plan)+'\n',encoding='utf-8')
print(json.dumps({'pngs':len(manifest),'sprites':len(plan),'pixelEdits':False,'paths':[a['path'] for a in manifest]}))
