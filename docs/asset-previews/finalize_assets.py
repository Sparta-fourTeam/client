"""Finalize generated image records; validate files without editing their pixels."""
from pathlib import Path
import hashlib, html, json, re, shutil, struct, subprocess, uuid, zlib
ROOT = Path(__file__).resolve().parents[2]
ART = ROOT/"Assets/_Project/Sprites/Generated"
DOC = ROOT/"docs/asset-previews"
layer_sources = json.loads((DOC/"sheet-layer-manifest.json").read_text(encoding="utf-8"))
layer_by_key = {a["key"]:a for a in layer_sources}
def read(p): return p.read_text(encoding="utf-8-sig")
def write(p,s): p.write_text(s,encoding="utf-8",newline="\n")
def guid(p): return re.search(r"^guid: (\w+)$", read(Path(str(p)+".meta")), re.M)[1]

# Keep unused working images outside Unity Assets.
for folder, name in [("Items","EnchantmentScrollFrame"),("Equipment","UnknownEquipmentSymbol")]:
    for suffix in [".png",".png.meta"]:
        src=ART/folder/(name+suffix)
        dst=DOC/"superseded-equipment"/(name+suffix)
        assert dst.resolve().is_relative_to(DOC.resolve())
        if src.exists():
            assert not dst.exists(), dst
            shutil.move(str(src),str(dst))
screen=ROOT/"Assets/_Project/Prefabs/UI/CharacterScreen.prefab"
write(screen,re.sub(r"(propertyPath: m_Sprite\n      value:)[ \t]+\n",r"\1\n",read(screen)))

# Unity meta GUIDs must remain stable after replacement of the underlying PNG.
manifest=[]
for p in sorted(ART.rglob("*.png")):
    if p.parent.name=="Review": continue
    w,h=struct.unpack(">II",p.read_bytes()[16:24])
    assert p.read_bytes()[25] == 6 or (p.parent.name=="LayeredSheets" and p.stem=="LobbySky" and p.read_bytes()[25]==2), ("Expected RGBA or opaque RGB sky",p)
    meta=read(Path(str(p)+".meta"))
    if p.parent.name=="EffectTextures":
        assert "textureType: 0" in meta, p
    elif p.parent.name=="LayeredSheets":
        mode=1 if layer_by_key[p.stem].get("fullCanvas") else 2
        assert "textureType: 8" in meta and f"spriteMode: {mode}" in meta,p
    else:
        assert "textureType: 8" in meta and "spriteMode: 1" in meta, p
    manifest.append(dict(key=p.stem,category=p.parent.name,path=p.relative_to(ROOT).as_posix(),
        guid=guid(p),width=w,height=h,source="existing PSD composite" if p.parent.name in ("Monsters","Characters") else "built-in image_gen",
        sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
assert len(manifest)==77+len(layer_sources),len(manifest)
assert len({a["guid"] for a in manifest}) == len(manifest), "Generated GUID collision"
write(DOC/"production-manifest.json",json.dumps(manifest,ensure_ascii=False,indent=2)+"\n")

# Read-only PNG decoder used to inspect transparency and scroll canvas placement.
def rgba(p):
    data=p.read_bytes(); pos=8; compressed=bytearray()
    w,h,depth,color=struct.unpack(">IIBB",data[16:26])
    assert depth==8 and color==6,p
    while pos<len(data):
        size=struct.unpack(">I",data[pos:pos+4])[0]; kind=data[pos+4:pos+8]
        chunk=data[pos+8:pos+8+size]
        assert zlib.crc32(kind+chunk)&0xffffffff == struct.unpack(">I",data[pos+8+size:pos+12+size])[0],p
        if kind==b"IDAT": compressed.extend(chunk)
        pos+=size+12
    raw=zlib.decompress(compressed); stride=w*4; previous=bytearray(stride); pixels=bytearray()
    for y in range(h):
        start=y*(stride+1); f=raw[start]; row=bytearray(raw[start+1:start+1+stride])
        if f==1:
            for x in range(4,stride): row[x]=(row[x]+row[x-4])&255
        elif f==2:
            for x in range(stride): row[x]=(row[x]+previous[x])&255
        elif f==3:
            for x in range(stride): row[x]=(row[x]+((row[x-4] if x>=4 else 0)+previous[x])//2)&255
        elif f==4:
            for x in range(stride):
                a=row[x-4] if x>=4 else 0; b=previous[x]; c=previous[x-4] if x>=4 else 0
                pred=a+b-c; pa,pb,pc=abs(pred-a),abs(pred-b),abs(pred-c)
                row[x]=(row[x]+(a if pa<=pb and pa<=pc else b if pb<=pc else c))&255
        else: assert f==0,(p,f)
        pixels.extend(row); previous=row
    return w,h,pixels

alpha_checks=[]
for a in manifest:
    if a["category"]=="LayeredSheets" and a["key"]=="LobbySky" and (ROOT/a["path"]).read_bytes()[25]==2:
        alpha_checks.append({"key":a["key"],"fullCanvasLayer":True,"opaqueRGB":True})
        continue
    w,h,pixels=rgba(ROOT/a["path"])
    alpha=pixels[3::4]
    if a["category"]=="LayeredSheets" and layer_by_key[a["key"]].get("fullCanvas"):
        if a["key"]!="LobbySky": assert min(alpha)==0,a["key"]
        assert max(alpha)>20,a["key"]
        alpha_checks.append({"key":a["key"],"fullCanvasLayer":True,"minAlpha":min(alpha),"maxAlpha":max(alpha)})
        continue
    assert min(alpha)==0 and max(alpha)>200,a["key"]
    corners=[alpha[0],alpha[w-1],alpha[-w],alpha[-1]]
    assert max(corners)<=2,(a["key"],corners)
    alpha_checks.append({"key":a["key"],"cornerAlpha":corners})

items=json.loads(read(ROOT/"Assets/_Project/Resources/MockData/Items.json"))
baseline=json.loads(subprocess.check_output(["git","show","HEAD:Assets/_Project/Resources/MockData/Items.json"],cwd=ROOT).decode("utf-8-sig"))
assert {i["Id"]:i.get("TargetId") for i in items}=={i["Id"]:i.get("TargetId") for i in baseline}
table=read(ROOT/"Assets/_Project/Data/ItemIconTable.asset")
keys=re.findall(r"^  - iconKey: (.+)$",table,re.M)
assert len(keys)==24 and len(set(keys))==24
assert all(i["IconKey"] in keys for i in items)
assert sum(i["IconKey"].endswith("Scroll") for i in items)==7
monster_ids=re.findall(r"^  - monsterId: (\d+)$",read(ROOT/"Assets/_Project/Data/MonsterDisplayTable.asset"),re.M)
assert set(monster_ids)==set("1 2 3 4 5 6 11 12 21 22 23 24 25 100".split())

guid_index={}
for p in (ROOT/"Assets").rglob("*.meta"):
    m=re.search(r"^guid: (\w+)$",read(p),re.M)
    if m: guid_index[m[1]]=p
assert len(guid_index)==len(set(guid_index))
for name in ["ItemIconTable","MonsterDisplayTable","SkillAssetTable"]:
    text=read(ROOT/"Assets/_Project/Data"/(name+".asset"))
    for asset_guid in re.findall(r"guid: (\w+)",text):
        assert asset_guid in guid_index,(name,asset_guid)

scrolls=[a for a in manifest if a["key"].endswith("Scroll") or a["key"]=="RandomEquipmentMaterial"]
dimensions={(a["width"],a["height"]) for a in scrolls}
assert len(scrolls)==8 and len(dimensions)==1,(len(scrolls),dimensions)
report={"artwork":len(manifest),"itemDefinitions":len(items),"itemIconKeys":len(keys),
        "equipmentScrolls":len(scrolls),"monsterIds":len(monster_ids),
        "preservedItemIdsAndTargets":True,"resolvedTableGuids":True,
        "transparentPNGs":alpha_checks,"errors":[]}
write(DOC/"static-validation.json",json.dumps(report,ensure_ascii=False,indent=2)+"\n")

refresh_keys={a["key"] for a in json.loads(read(DOC/"existing-refresh-manifest.json"))}
cards=[]
for a in manifest:
    isscroll=a["key"].endswith("Scroll") or a["key"]=="RandomEquipmentMaterial"
    cards.append('<figure data-category="'+a["category"]+'" data-refresh="'+str(a["key"] in refresh_keys).lower()+'" data-scroll="'+str(isscroll).lower()+
        '"><img loading="lazy" src="../../'+a["path"]+'" alt="'+html.escape(a["key"])+
        '"><figcaption>'+html.escape(a["key"])+'<small>'+a["category"]+'</small></figcaption></figure>')
gallery="""<!doctype html><html lang="ko"><meta charset="utf-8"><title>제작 에셋 검토</title>
<style>body{font:16px system-ui;margin:24px;background:#eee;color:#111}body.dark{background:#222;color:#eee}
nav{display:flex;gap:12px;flex-wrap:wrap;position:sticky;top:0;background:inherit;padding:12px}
main{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:16px}
figure{margin:0;padding:16px;border:1px solid #888;border-radius:8px;text-align:center}
figure[hidden]{display:none}img{width:var(--size,128px);height:var(--size,128px);object-fit:contain}
small{display:block;color:#888}select,button{padding:6px}</style><h1>제작 에셋 · 88장</h1>
<p>시트 재제작: 11개 PNG · 71개 독립 레이어. <a href="sheet-layer-gallery.html">레이어·조합 미리보기</a></p>
<p>장비 기준: 플레이어 PSD. 기존 무기는 지팡이, 사원증은 인장. 장비 강화 재료는 같은 틀의 완성 주문서 PNG.</p>
<p>추가 제작 28장: 기존 스킬·아이템·전투 효과 컨셉 유지. PSD·UI 버튼·패널·배경 제외. 공용 몬스터 그림자와 빈 회색 원형 프레임 포함.</p>
<nav><select id="category"><option value="LayeredSheets" selected>시트 레이어</option><option>추가 제작</option><option>주문서</option><option>전체</option><option>Equipment</option><option>Items</option>
<option>Skills</option><option>Effects</option><option>EffectTextures</option><option>Common</option><option>Monsters</option><option>Characters</option></select>
<select id="size"><option>128</option><option>64</option><option>256</option></select>
<button id="theme">밝게 / 어둡게</button></nav><main>"""+ "\n".join(cards)+"""</main><script>
function filter(){let value=document.querySelector('#category').value;document.querySelectorAll('figure').forEach(f=>
f.hidden=value==='추가 제작'?f.dataset.refresh!=='true':value==='주문서'?f.dataset.scroll!=='true':value!=='전체'&&f.dataset.category!==value)}
document.querySelector('#category').onchange=filter;filter();
document.querySelector('#size').onchange=e=>document.documentElement.style.setProperty('--size',e.target.value+'px');
document.querySelector('#theme').onclick=()=>document.body.classList.toggle('dark');</script></html>"""
write(DOC/"gallery.html",gallery)
print(json.dumps({k:v for k,v in report.items() if k!="transparentPNGs"},ensure_ascii=False))
