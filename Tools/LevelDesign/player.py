"""플레이어 전투력 모델.

전투력 = scale * sqrt(유효HP * DPS)  (몬스터와 같은 sqrt(HP*DPS) 계열, scale은 calibrate가 정한다)
- DPS: Skills.json 기본 스탯 + 영구 강화(스킬 %, 장비 %) + 한 판 안의 카드 선택을 시뮬레이션한 값
- 유효HP: 방벽 HP (+ 방벽 수리 카드)
- 스테이지별 영구 성장은 curves.json의 progress 가정으로 정한다 (경제 시뮬레이션 아님)

반영하는 카드 효과: damage, attackSpeed, castCount, projectileCount, beamPulses(+Flat), chainBounces.
그 외 효과(상태이상, 폭발, 형태 변환, onEvent 등)는 0으로 본다 -> 카드 성장을 과소평가하므로 cardEfficiency로 보정한다.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/_Project/Resources/MockData"

EQUIP_DMG = "skillDamagePercent"


def _load(name):
    with open(DATA / name, encoding="utf-8") as f:
        return json.load(f)


class Tables:
    def __init__(self):
        self.skills = {s["id"]: s for s in _load("Skills.json")}
        self.upgrades = {u["UpgradeId"]: u for u in _load("Upgrades.json")}
        self.levels = _load("PlayerLevels.json")
        self.rewards = {r["StageId"]: r for r in _load("StageRewards.json")}
        self.combat = _load("Combat.json")
        self.cards_general = _load("GeneralCards.json")


# ---------- 계정 레벨 ----------

def account_level(t, exp):
    """레벨 L -> L+1 필요 경험치 = BaseExp + StepExp*(L-1)."""
    lvl = 1
    need = t.levels["BaseExp"]
    while lvl < t.levels["MaxLevel"] and exp >= need:
        exp -= need
        lvl += 1
        need = t.levels["BaseExp"] + t.levels["StepExp"] * (lvl - 1)
    return lvl


def cum_exp_before(t, stage, three_star=True):
    """stage를 시작하기 전까지 첫 클리어로 쌓은 경험치 (별 3개 가정)."""
    total = 0
    for s in range(1, stage):
        r = t.rewards.get(s)
        if r:
            total += r["BaseExp"] + (r.get("ThreeStarBonusExp", 0) if three_star else 0)
    return total


# ---------- 영구 강화 ----------

def upgrade_value(up, level, kind=None):
    """레벨 1..level의 효과량 누적 (Effects[kind].LevelValues)."""
    if not up or level <= 0:
        return 0.0
    tot = 0.0
    for e in up.get("Effects", []):
        if kind and e["Kind"] != kind:
            continue
        vals = e["LevelValues"]
        tot += sum(vals[: min(level, len(vals))])
    return tot


def equipment_pct(t, account_lvl, equip_level):
    """해금된 장비 부위들의 skillDamagePercent 합 (부위끼리는 더한다)."""
    tot = 0.0
    for uid, up in t.upgrades.items():
        if uid.startswith("equipment.") and up.get("UnlockLevel", 1) <= account_lvl:
            tot += upgrade_value(up, equip_level, EQUIP_DMG)
    return tot


# ---------- 스킬 DPS ----------

def hits_per_cast(sk, st):
    ct = sk["castType"]
    b = sk["baseStats"]
    if ct == "Area":
        a = b["area"]
        return st["areaDuration"] * a["duration"] / a["pulseInterval"] if "areaDuration" in st else a["duration"] / a["pulseInterval"]
    if ct == "Beam":
        return st["beamPulses"]
    if ct == "Chain":
        return 1 + b["chain"]["bounces"] + st["chainBounces"]
    return 1.0


def new_state(sk):
    b = sk["baseStats"]
    c = b["cast"]
    st = {
        "cooldown": c["cooldown"], "damage": float(c["baseDamage"]),
        "cast": c.get("castCount", 1), "proj": c.get("projectileCount", 1),
        "beamPulses": float(b.get("beam", {}).get("pulses", 1)),
        "chainBounces": 0, "picks": {},
    }
    return st


def apply_effect(sk, st, kind, v):
    ct = sk["castType"]
    if kind == "damage":
        st["damage"] *= 1 + v * 0.01
    elif kind == "attackSpeed":
        st["cooldown"] = max(0.1, st["cooldown"] * (1 - v * 0.01))
    elif kind == "castCount":
        st["cast"] = max(1, st["cast"] + int(v))
    elif kind == "projectileCount":
        st["proj"] = max(1, st["proj"] + int(round(v)))
    elif kind == "beamPulses" and ct == "Beam":
        st["beamPulses"] = max(1, st["beamPulses"] * (1 + v * 0.01))
    elif kind == "beamPulsesFlat" and ct == "Beam":
        st["beamPulses"] += v
    elif kind == "chainBounces" and ct == "Chain":
        st["chainBounces"] += int(v)


def skill_dps(sk, st, crit):
    per_cast = st["damage"] * st["cast"] * (st["proj"] if sk["castType"] in ("Projectile", "Instant") else 1)
    return per_cast * hits_per_cast(sk, st) / st["cooldown"] * crit


def card_effects(card, perm_level):
    eff = card["effects"]
    for var in card.get("variants", []):
        if perm_level >= var.get("minPermanentLevel", 0):
            eff = var["effects"]
    return eff


def eligible(card, owned, picks_by_card, perm_level):
    """모델이 쓰는 최소 조건만 본다: 선행 카드·필수 스킬·영구 레벨. 배타 조건·enabled 플래그는 무시한다."""
    if card.get("enabled") is False:
        return False
    if perm_level < card.get("minPermanentLevel", 0):
        return False
    for r in card.get("requiredCardCounts", []):
        if picks_by_card.get(r["cardId"], 0) < r.get("count", 1):
            return False
    for w in card.get("requiredWeaponIds", []):
        if w not in owned:
            return False
    return True


# ---------- 스테이지 상태 -> DPS 곡선 ----------

def perm_state(t, cfg, stage):
    p = cfg["progress"]
    lvl = account_level(t, cum_exp_before(t, stage))
    skills = {}
    for sid in p.get("pool", []):
        sk = t.skills[sid]
        unlock = sk.get("unlockLevel")
        if unlock is None or unlock > lvl:
            continue
        up = t.upgrades.get(sk.get("progressionId"))
        cap = up["MaxLevel"] if up else 0
        pl = min(cap, int(p["skillBase"] + p["skillPerLevel"] * (lvl - unlock)))
        skills[sid] = max(pl, 0)
    equip = min(10, int(p["equipBase"] + p["equipPerLevel"] * (lvl - 1)))
    return lvl, skills, equip


def run_dps_curve(t, cfg, stage, stage_def, waves):
    """웨이브 w(1..waves)에 시작할 때의 플레이어 DPS 리스트."""
    p = cfg["progress"]
    crit = 1 + t.combat["CriticalChance"] * (t.combat["CriticalMultiplier"] - 1)
    lvl, perm, equip_lvl = perm_state(t, cfg, stage)
    eq_pct = equipment_pct(t, lvl, equip_lvl)

    def perm_mult(sid):
        sk = t.skills[sid]
        up = t.upgrades.get(sk.get("progressionId"))
        return (1 + upgrade_value(up, perm.get(sid, 0), "damage") / 100) * (1 + eq_pct / 100)

    owned = {}  # sid -> state
    for sid in p["start"]:
        if sid in t.skills:
            owned[sid] = new_state(t.skills[sid])
    picks_by_card = {}

    def total_dps():
        return sum(skill_dps(t.skills[s], st, crit) * perm_mult(s) for s, st in owned.items())

    # 엘리트 룰렛 카드 수
    rmin, rmax = stage_def.get("EliteRouletteMinCards", 0), stage_def.get("EliteRouletteMaxCards", 0)
    roulette = (rmin + rmax) / 2
    elite_wave = stage_def.get("_eliteWave")

    def cards_at(w):
        n = max(0, w - 1)
        if elite_wave is not None and w > elite_wave + 0:
            n += roulette
        return n * p["cardEfficiency"]

    # 카드 선택을 탐욕으로 시뮬레이션: 한 장씩 가장 큰 DPS 증가분을 고른다. 소수 장수는 마지막 장을 비율로 반영한다.
    steps = []  # (누적 카드 수, DPS)
    steps.append((0.0, total_dps()))
    max_cards = int(cards_at(waves + 1)) + 2
    slot_max = 5
    for k in range(1, max_cards + 1):
        best, best_gain = None, 0.0
        cur = total_dps()
        # 새 스킬
        if len(owned) < slot_max:
            for sid, perm_l in perm.items():
                sk = t.skills[sid]
                if sid in owned or sk.get("childOnly"):
                    continue
                st = new_state(sk)
                gain = skill_dps(sk, st, crit) * perm_mult(sid)
                if gain > best_gain:
                    best, best_gain = ("new", sid, None), gain
        # 강화 카드
        for sid, st in owned.items():
            sk = t.skills[sid]
            pl = perm.get(sid, 0)
            for card in sk.get("upgrades", []):
                if st["picks"].get(card["id"], 0) >= card.get("maxPickCount", 1):
                    continue
                if not eligible(card, owned, picks_by_card, pl):
                    continue
                trial = json.loads(json.dumps(st))
                for e in card_effects(card, pl):
                    if e.get("value") is not None:
                        apply_effect(sk, trial, e["kind"], e["value"])
                gain = (skill_dps(sk, trial, crit) - skill_dps(sk, st, crit)) * perm_mult(sid)
                if gain > best_gain:
                    best, best_gain = ("up", sid, card), gain
        if best is None:
            steps.append((float(k), cur))
            continue
        kind, sid, card = best
        if kind == "new":
            owned[sid] = new_state(t.skills[sid])
        else:
            st = owned[sid]
            for e in card_effects(card, perm.get(sid, 0)):
                if e.get("value") is not None:
                    apply_effect(t.skills[sid], st, e["kind"], e["value"])
            st["picks"][card["id"]] = st["picks"].get(card["id"], 0) + 1
            picks_by_card[card["id"]] = picks_by_card.get(card["id"], 0) + 1
        steps.append((float(k), total_dps()))

    def dps_at(n):
        for (a, da), (b, db) in zip(steps, steps[1:]):
            if a <= n <= b:
                return da + (db - da) * (n - a) / (b - a)
        return steps[-1][1]

    return [dps_at(cards_at(w)) for w in range(1, waves + 1)], lvl


def effective_hp(t, cfg, stage_def):
    p = cfg["progress"]
    return stage_def.get("WallHp", 3000) + p.get("wallRepairs", 0) * 600


def power_curve(t, cfg, stage, stage_def, monsters_for_elite=None):
    """스테이지의 웨이브별 플레이어 전투력(scale 적용 전 원시값)과 계정 레벨."""
    waves = len(stage_def["Waves"])
    dps, lvl = run_dps_curve(t, cfg, stage, stage_def, waves)
    hp = effective_hp(t, cfg, stage_def)
    return [(hp * d) ** 0.5 for d in dps], lvl
