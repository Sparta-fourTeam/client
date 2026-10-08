"""Refresh layer records and browser previews; no image pixel changes."""
from pathlib import Path
import hashlib,html,json,re
ROOT=Path(__file__).resolve().parents[2];DOC=ROOT/'docs/asset-previews'
manifest=json.loads((DOC/'sheet-layer-manifest.json').read_text(encoding='utf-8'))
recipes=json.loads((DOC/'sheet-layer-recipes.json').read_text(encoding='utf-8'))
sprites={}
for a in manifest:
 p=ROOT/a['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==a['sha256']
 a['guid']=re.search(r'^guid: (\w+)',Path(str(p)+'.meta').read_text(encoding='utf-8-sig'),re.M)[1]
 for s in a['sprites']:sprites[s['name']]=dict(path=a['path'],width=a['width'],height=a['height'],category=a['key'],**s)
for a in json.loads((DOC/'production-manifest.json').read_text(encoding='utf-8')):
 if a['category'] in ['Items','Skills']:sprites[a['category']+'/'+a['key']]=dict(path=a['path'],width=a['width'],height=a['height'],rect=[0,0,a['width'],a['height']],category=a['category'],name=a['key'])
(DOC/'sheet-layer-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for r in recipes:
 assert (ROOT/r['prefab']).is_file(),r['prefab']
 for field in ['base','frame','glyph','overlay']:
  if r[field]:assert r[field] in sprites,(r['name'],field,r[field])

page='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>시트 레이어 검토</title>
<style>body{font:15px system-ui;margin:24px;background:#eee;color:#222}body.dark{background:#20242a;color:#eee}nav{display:flex;gap:12px;align-items:center;flex-wrap:wrap;position:sticky;top:0;background:inherit;z-index:5;padding:12px 0}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:14px}article{border:1px solid #888;border-radius:8px;padding:14px;overflow:hidden}article[hidden]{display:none}.preview{position:relative;height:180px;display:flex;align-items:center;justify-content:center;background:repeating-conic-gradient(#ffffff15 0% 25%,#88888815 0% 50%) 0/16px 16px}.composition{position:relative;width:150px;height:150px}.sprite{position:relative;overflow:hidden;flex-shrink:0}.sprite img{position:absolute;max-width:none;pointer-events:none}.composition>.sprite{position:absolute}small{display:block;word-break:break-all;color:#888}select,button{padding:7px}label{white-space:nowrap}</style>
<h1>시트 재제작 · 71개 독립 레이어</h1><p>11개 PNG를 역할별로 구성했습니다. 배경색·테두리·아이콘·상태 표시·하늘·잔디·조명을 따로 교체할 수 있습니다.</p>
<p>기존 시트 117개 조각의 대응 조합 프리팹을 준비했습니다. PSD 제외. Unity에서는 버튼·패널 테두리를 9-slice로 사용합니다.</p>
<nav><select id="mode"><option value="parts">레이어별 보기</option><option value="recipes">조합 보기</option></select><select id="category"><option value="all">전체</option></select><select id="size"><option>128</option><option>64</option><option>256</option></select><button id="theme">밝게 / 어둡게</button><label><input type="checkbox" id="border" checked>테두리</label><label><input type="checkbox" id="glyph" checked>아이콘</label><label><input type="checkbox" id="overlay" checked>전경·조명</label></nav><main></main><script>
const sprites=__SPRITES__, recipes=__RECIPES__;
const q=s=>document.querySelector(s);
function crop(key,w,h){const a=sprites[key],r=a.rect,box=document.createElement('div');box.className='sprite';box.style.width=w+'px';box.style.height=h+'px';const img=document.createElement('img');img.src='../../'+a.path;img.alt=a.name;img.loading='lazy';img.style.width=a.width*w/r[2]+'px';img.style.height=a.height*h/r[3]+'px';img.style.left=-r[0]*w/r[2]+'px';img.style.top=-(a.height-r[1]-r[3])*h/r[3]+'px';box.append(img);return box}
function add(container,key,w,h,x=0,y=0,opacity=1){if(!key)return;const box=crop(key,w,h);box.style.left=x+'px';box.style.top=y+'px';box.style.opacity=opacity;container.append(box)}
function render(){const mode=q('#mode').value,filter=q('#category').value,size=+q('#size').value;const main=q('main');main.replaceChildren();const items=mode==='parts'?Object.entries(sprites).filter(([key,a])=>!key.includes('/')):recipes.map(r=>[r.sheet+'/'+r.name,r]);for(const [key,a] of items){const cat=mode==='parts'?a.category:a.sheet;if(filter!=='all'&&filter!==cat)continue;const card=document.createElement('article'),preview=document.createElement('div');preview.className='preview';if(mode==='parts'){const r=a.rect,scale=Math.min(size/r[2],size/r[3]);preview.append(crop(key,r[2]*scale,r[3]*scale))}else{const c=document.createElement('div');c.className='composition';const ratio=Math.max(.35,Math.min(3,a.rect[2]/a.rect[3])),w=Math.min(210,size*ratio),h=Math.min(165,size/ratio);c.style.width=w+'px';c.style.height=h+'px';add(c,a.base,w,h);if(a.special==='energyWindow'){add(c,'NavyFill',w*.972,h*.964,w*.014,h*.018);if(q('#border').checked)add(c,'GoldenCircleOutline',w*.1,h*.12,w*.025,h*.02)}if(q('#border').checked)add(c,a.frame,w,h);if(q('#glyph').checked&&a.glyph){const r=sprites[a.glyph].rect,scale=Math.min(w*.64/r[2],h*.64/r[3]);add(c,a.glyph,r[2]*scale,r[3]*scale,(w-r[2]*scale)/2,(h-r[3]*scale)/2)}if(q('#overlay').checked)add(c,a.overlay,w,h,0,0,a.special==='spotlight'?.2:1);if(a.special==='nodes')for(let i=0;i<3;i++)add(c,'GreenNode',h,h,i*(w-h)/2,0);preview.append(c)}card.append(preview);const caption=document.createElement('p');caption.textContent=key;card.append(caption);const detail=document.createElement('small');detail.textContent=mode==='parts'?a.path:[a.base,a.frame,a.glyph,a.overlay].filter(Boolean).join(' + ');card.append(detail);main.append(card)}}
function categories(){q('#category').replaceChildren(new Option('전체','all'));const values=q('#mode').value==='parts'?[...new Set(Object.values(sprites).filter(a=>!['Items','Skills'].includes(a.category)).map(a=>a.category))]:[...new Set(recipes.map(r=>r.sheet))];for(const value of values)q('#category').add(new Option(value,value));render()}
q('#mode').onchange=categories;for(const id of ['category','size','border','glyph','overlay'])q('#'+id).onchange=render;q('#theme').onclick=()=>document.body.classList.toggle('dark');categories();
</script></html>'''
page=page.replace('__SPRITES__',json.dumps(sprites,ensure_ascii=False).replace('</','<\\/')).replace('__RECIPES__',json.dumps(recipes,ensure_ascii=False).replace('</','<\\/'))
(DOC/'sheet-layer-gallery.html').write_text(page,encoding='utf-8')
# Formatting only: Unity's serializer emits blank component names with trailing spaces.
# Preserve every serialized token; do not modify YAML values, object IDs or metadata.
paths=set(r['prefab'] for r in recipes)
paths.update(line.split(' | ')[0] for line in (DOC/'sheet-reference-changes.txt').read_text(encoding='utf-8').splitlines() if line)
paths.add('Assets/_Project/Prefabs/UI/CardSelect.prefab')
for rel in paths:
 if not rel.endswith('.prefab'):continue
 p=ROOT/rel;before=p.read_text(encoding='utf-8-sig');after='\n'.join(line.rstrip() for line in before.splitlines())+'\n'
 assert before.split()==after.split(),rel
 p.write_text(after,encoding='utf-8')
report={'pngs':len(manifest),'spriteLayers':sum(len(a['sprites']) for a in manifest),'oldSheetSlices':len(recipes),'kitPrefabs':len(recipes),'imagePixelsUnchanged':True,'errors':[]}
(DOC/'sheet-static-validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(json.dumps(report))
