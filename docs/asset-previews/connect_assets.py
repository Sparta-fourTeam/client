"""Connect approved artwork without modifying image pixels. Run from the Unity project root."""
from pathlib import Path
import hashlib, html, json, re, shutil, struct, uuid

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets/_Project/Sprites/Generated"
DOC = ROOT / "docs/asset-previews"
SLOTS = [
    ("hat", "Hood", "후드"), ("top", "Robe", "로브"), ("shoes", "Boots", "장화"),
    ("weapon", "Staff", "무기"), ("ring", "BoneRing", "뼈 반지"),
    ("tie", "SkullNecklace", "해골 목걸이"), ("employee_id", "Seal", "인장"),
]

def read(p):
    return p.read_text(encoding="utf-8-sig")
def write(p, text):
    p.write_text(text, encoding="utf-8", newline="\n")
def replace(p, old, new):
    text = read(p)
    assert old in text, (p, old)
    write(p, text.replace(old, new))
def guid(p):
    return re.search(r"^guid: (\w+)$", read(Path(str(p)+".meta")), re.M)[1]
def ref(p):
    return "{fileID: 21300000, guid: " + guid(p) + ", type: 3}"
def dims(p):
    return struct.unpack(">II", p.read_bytes()[16:24])

# Fail before any mutation if the replacement batch is incomplete.
for _, key, _ in SLOTS:
    assert (ART/"Equipment"/(key+".png")).is_file(), key
    assert (ART/"Items"/(key+"Scroll.png")).is_file(), key+"Scroll"
template = read(ART/"Review/FrostCrystal-v01.png.meta")
for src, dest in [
    ("Review/FrostCrystal-v01.png", "Skills/FrostCrystal.png"),
    ("Review/LogRoot-v01.png", "Skills/LogRoot.png"),
    ("Review/FrostCrystalBook-v01.png", "Items/FrostCrystalBook.png"),
]:
    shutil.copyfile(ART/src, ART/dest)

# Rejected artwork stays outside Assets so Unity does not package/import it.
archive = DOC/"superseded-equipment"
archive.mkdir(exist_ok=True)
for key in ["Hat", "Top", "Shoes", "Weapon", "Ring", "Tie", "EmployeeId"]:
    for folder, name in [("Equipment", key), ("Items", key+"Book")]:
        for suffix in [".png", ".png.meta"]:
            src = ART/folder/(name+suffix)
            dst = archive/(name+suffix)
            assert dst.resolve().is_relative_to(DOC.resolve())
            if src.exists():
                assert not dst.exists(), dst
                shutil.move(str(src), str(dst))

for _, key, _ in SLOTS:
    for suffix in [".png", ".png.meta"]:
        src = ART/"Items"/(key+"Book"+suffix)
        dst = archive/(key+"Book"+suffix)
        assert dst.resolve().is_relative_to(DOC.resolve())
        if src.exists():
            assert not dst.exists(), dst
            shutil.move(str(src), str(dst))

# Set explicit single-sprite import settings, preserving any GUID Unity assigned.
for p in ART.rglob("*.png"):
    meta = Path(str(p)+".meta")
    asset_guid = guid(p) if meta.exists() else uuid.uuid4().hex
    content = re.sub(r"^guid: \w+$", "guid: "+asset_guid, template, flags=re.M)
    content = re.sub(r"spriteID: \w+", "spriteID: "+uuid.uuid4().hex, content)
    size = 2048 if p.parent.name == "Effects" else 512
    content = re.sub(r"maxTextureSize: \d+", "maxTextureSize: "+str(size), content)
    if p.parent.name == "Effects":
        units = .16 if p.stem == "EnergyBeamBody" else .2
        content = re.sub(r"spritePixelsToUnits: [\d.]+",
                         "spritePixelsToUnits: "+str(dims(p)[0]/units), content)
    write(meta, content)

items_path = ROOT/"Assets/_Project/Resources/MockData/Items.json"
items = json.loads(read(items_path))
for slot, key, name in SLOTS:
    row = next(i for i in items if i["Id"] == "book."+slot)
    row["Name"] = name+" 인챈트 주문서"
    row["IconKey"] = key+"Scroll"
write(items_path, json.dumps(items, ensure_ascii=False, indent=2)+"\n")
catalog = ROOT/"Assets/_Project/Scripts/Runtime/Core/Lobby/DummyGrowthCatalog.cs"
text = read(catalog)
for slot, _, name in SLOTS:
    text, count = re.subn(r'("equipment\.'+slot+r'", ")[^"]+("\))',
                          lambda m: m[1]+name+m[2], text)
    assert count == 1, slot
write(catalog, text)

table = ROOT/"Assets/_Project/Data/ItemIconTable.asset"
text = read(table).split("  _coin:")[0]
coin = "{fileID: 2691650823057339236, guid: 87ddbd6be03ec7c4ebbfed37cb7a578a, type: 3}"
text += "  _coin: "+coin+"\n  _exp: "+ref(ART/"Items/Experience.png")+"\n"
text += "  _randomSkillMaterial: "+ref(ART/"Items/RandomSkillMaterial.png")+"\n"
text += "  _randomEquipmentMaterial: "+ref(ART/"Items/RandomEquipmentMaterial.png")+"\n  _items:\n"
for i in items:
    text += "  - iconKey: "+i["IconKey"]+"\n    icon: "+ref(ART/"Items"/(i["IconKey"]+".png"))+"\n"
for slot, key, _ in SLOTS:
    text += "  - iconKey: equipment."+slot+"\n    icon: "+ref(ART/"Equipment"/(key+".png"))+"\n"
write(table, text)

skilltable = ROOT/"Assets/_Project/Data/SkillAssetTable.asset"
text = read(skilltable)
for skill, key in [
    ("frost_crystal", "FrostCrystal"), ("log", "LogRoot"), ("cold_zone", "ColdZone"),
    ("lightning_cloud", "LightningCloud"), ("energy_beam", "EnergyBeam"),
    ("chain_lightning", "ChainLightning"),
]:
    pattern = r"(  - key: weapon_"+skill+r"\n)(.*?)(?=  - key:|  hudFallback:)"
    def update(m):
        body = re.sub(r"(    (?:hudIcon|newCardIcon|upgradeCardIcon): )\{[^\n]+",
                      lambda n: n[1]+ref(ART/"Skills"/(key+".png")), m[2])
        return m[1]+body
    text, count = re.subn(pattern, update, text, flags=re.S)
    assert count == 1, skill
text = text.replace("  - key: wall_repair\n    prefab: {fileID: 0}\n    hudIcon: {fileID: 0}\n    newCardIcon: {fileID: 0}\n    upgradeCardIcon: {fileID: 0}",
    "  - key: wall_repair\n    prefab: {fileID: 0}\n    hudIcon: {fileID: 0}\n    newCardIcon: {fileID: 0}\n    upgradeCardIcon: "+ref(ART/"Skills/WallRepair.png"))
write(skilltable, text)

monsters = [
    (1,"Slime","슬라임"),(2,"Bat","박쥐"),(3,"Slime","작은 슬라임"),
    (4,"Golem","골렘"),(5,"Spider","거미"),(6,"Spider","작은 거미"),
    (11,"Slime","정예 슬라임"),(12,"Bat","정예 박쥐"),
    (21,"ArmoredCrab","갑옷 게"),(22,"EyeJelly","눈 해파리"),(23,"Ghost","유령"),
    (24,"Skeleton","해골"),(25,"Oni","뿔 요마"),(100,"BrainSpider","뇌 거미"),
]
monster_table = ROOT/"Assets/_Project/Data/MonsterDisplayTable.asset"
text = read(monster_table).split("  _monsters:")[0]+"  _monsters:\n"
elite = ROOT/"Assets/_Project/Sprites/Monster_Elite.png"
for mid, key, name in monsters:
    icon = ref(ART/"Monsters"/(key+".png"))
    if mid in (11,12):
        fid = -8215510745074873636 if mid == 11 else 1289800932146914806
        icon = "{fileID: "+str(fid)+", guid: "+guid(elite)+", type: 3}"
    text += f"  - monsterId: {mid}\n    displayName: {json.dumps(name, ensure_ascii=False)}\n    icon: {icon}\n"
write(monster_table, text)

def component(p, fid, update):
    text = read(p)
    pattern = r"(--- !u!\d+ &"+str(fid)+r"\n)(.*?)(?=--- !u!|\Z)"
    text, count = re.subn(pattern, lambda m: m[1]+update(m[2]), text, flags=re.S)
    assert count == 1, (p, fid)
    write(p, text)
def image(p, fid, sprite):
    def update(s):
        s = re.sub(r"  m_Sprite: \{[^\n]+", "  m_Sprite: "+sprite, s)
        s = re.sub(r"  m_Color: \{[^\n]+", "  m_Color: {r: 1, g: 1, b: 1, a: 1}", s)
        s = re.sub(r"  m_Type: \d+", "  m_Type: 0", s)
        return re.sub(r"  m_PreserveAspect: \d+", "  m_PreserveAspect: 1", s)
    component(p, fid, update)

UI = ROOT/"Assets/_Project/Prefabs/UI"
image(UI/"CharacterScreen.prefab", 695758624039524025, ref(ART/"Characters/Player.png"))
image(UI/"CharacterCard.prefab", 6552220374344404809, ref(ART/"Characters/Player.png"))
# Slot order is explicit in CharacterScreenView._equipSlots.
screen = UI/"CharacterScreen.prefab"
equip_guid = guid(UI/"EquipSlot.prefab")
instance_ids = [3978999363797974388,7715428471256606530,2607836396414736518,
                5072191169485434887,1816509556360959193,5992773099420271688,5493565620082715857]
for iid, (_, key, _) in zip(instance_ids, SLOTS):
    def update(body):
        assert "guid: "+equip_guid in body
        target = "{fileID: 238753612176629771, guid: "+equip_guid+", type: 3}"
        overrides = ""
        for prop, value, obj in [
            ("m_Sprite","",ref(ART/"Equipment"/(key+".png"))),
            ("m_Type","0","{fileID: 0}"), ("m_PreserveAspect","1","{fileID: 0}"),
            ("m_Color.r","1","{fileID: 0}"), ("m_Color.g","1","{fileID: 0}"),
            ("m_Color.b","1","{fileID: 0}"),
        ]:
            overrides += f"    - target: {target}\n      propertyPath: {prop}\n      value: {value}\n      objectReference: {obj}\n"
        return body.replace("    m_RemovedComponents:", overrides+"    m_RemovedComponents:")
    component(screen, iid, update)
for name, fid in [("EquipUpgradePopup",2967679369804990486),
                  ("SkillUpgradePopup",742168655657772642),("SkillScreen",6791289779681139559)]:
    image(UI/(name+".prefab"),fid,coin)
image(UI/"EquipUpgradePopup.prefab",5194120879874992164,ref(ART/"Equipment/Staff.png"))
image(UI/"EquipUpgradePopup.prefab",7287157731033604338,ref(ART/"Items/StaffScroll.png"))
popup = UI/"EquipUpgradePopup.prefab"
replace(popup,"  _materialNameText: {fileID: 2177928232330467635}",
    "  _materialNameText: {fileID: 2177928232330467635}\n"
    "  _icons: {fileID: 11400000, guid: "+guid(table)+", type: 2}\n"
    "  _equipmentIcon: {fileID: 5194120879874992164}\n"
    "  _materialIcon: {fileID: 7287157731033604338}")

for prefab, asset, width in [
    ("Skills/FrostPrison","ColdZoneGround",.2), ("Skills/LightningCloud","LightningCloudBody",.2),
    ("Skills/SunBeam","EnergyBeamBody",.16), ("Stage/Slime_Projectile","SlimeProjectile",.2),
]:
    p = ROOT/"Assets/_Project/Prefabs"/(prefab+".prefab")
    def update(s):
        s = re.sub(r"  m_Sprite: \{[^\n]+", "  m_Sprite: "+ref(ART/"Effects"/(asset+".png")), s)
        s = re.sub(r"  m_Color: \{r: [^,]+, g: [^,]+, b: [^,]+, a: ([^}]+)\}",
                   r"  m_Color: {r: 1, g: 1, b: 1, a: \1}", s)
        return s
    text = read(p)
    text, count = re.subn(r"(--- !u!212 &\d+\n)(.*?)(?=--- !u!|\Z)",
                         lambda m: m[1]+update(m[2]),text,flags=re.S)
    assert count == 1, prefab
    write(p,text)

manifest = []
for p in sorted(ART.rglob("*.png")):
    if p.parent.name == "Review":
        continue
    manifest.append({"key":p.stem,"category":p.parent.name,"path":p.relative_to(ROOT).as_posix(),
        "guid":guid(p),"width":dims(p)[0],"height":dims(p)[1],
        "source":"existing PSD composite" if p.parent.name in ("Monsters","Characters") else "built-in image_gen",
        "sha256":hashlib.sha256(p.read_bytes()).hexdigest()})
write(DOC/"production-manifest.json",json.dumps(manifest,ensure_ascii=False,indent=2)+"\n")
cards = "\n".join('<figure data-category="'+a["category"]+'"><img loading="lazy" src="../../'+
    a["path"]+'" alt="'+html.escape(a["key"])+'"><figcaption>'+html.escape(a["key"])+
    '<small>'+a["category"]+'</small></figcaption></figure>' for a in manifest)
gallery = """<!doctype html><html lang="ko"><meta charset="utf-8"><title>에셋 검토</title>
<style>body{font:16px system-ui;margin:24px;background:#eee;color:#111}body.dark{background:#222;color:#eee}
nav{display:flex;gap:12px;flex-wrap:wrap;position:sticky;top:0;background:inherit;padding:12px}
main{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:16px}
figure{margin:0;padding:16px;border:1px solid #888;border-radius:8px;text-align:center}
img{width:var(--size,128px);height:var(--size,128px);object-fit:contain}small{display:block;color:#888}
select,button{padding:6px}</style><h1>승인된 그림체 · 제작 에셋</h1>
<p>장비 기준: 플레이어 PSD. 기존 무기 → 지팡이, 사원증 → 인장. 몬스터·플레이어는 기존 PSD의 원본 합성 픽셀.</p>
<nav><select id="category"><option>전체</option><option>Equipment</option><option>Items</option>
<option>Skills</option><option>Effects</option><option>Monsters</option><option>Characters</option></select>
<select id="size"><option>128</option><option>64</option><option>256</option></select>
<button id="theme">밝게 / 어둡게</button></nav><main>"""+cards+"""</main>
<script>document.querySelector('#category').onchange=e=>document.querySelectorAll('figure').forEach(f=>
f.hidden=e.target.value!=='전체'&&f.dataset.category!==e.target.value);
document.querySelector('#size').onchange=e=>document.documentElement.style.setProperty('--size',e.target.value+'px');
document.querySelector('#theme').onclick=()=>document.body.classList.toggle('dark');</script></html>"""
write(DOC/"gallery.html",gallery)
print(json.dumps({"active_artwork":len(manifest),"item_keys":len(items),"equipment_slots":len(SLOTS),
                  "monster_ids":len(monsters),"status":"connected"}, ensure_ascii=False))
