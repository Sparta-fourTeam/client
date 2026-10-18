"""레벨 디자인 수치화 도구.

curves.json(곡선 파라미터) -> Stages 초안 생성(generate) / 현재 Stages 검증(report).
전투력 공식은 몬스터·플레이어 모두 sqrt(유효HP * DPS) 계열로 통일한다.

사용법 (client 루트에서):
  python Tools/LevelDesign/balance.py report   [--stages 경로]
  python Tools/LevelDesign/balance.py generate [--stages 원본] [--out 출력]
"""
import argparse
import json
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import player as pl

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/_Project/Resources/MockData"
HERE = Path(__file__).resolve().parent


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


# ---------- 몬스터 전투력 ----------

def solo_power(m, hp_m=1.0, dmg_m=1.0, spd_m=1.0):
    """소환 제외 단일 몬스터 전투력. 배수는 스테이지 MonsterBonuses."""
    hp = m["Hp"] * hp_m
    dps = m["Damage"] * dmg_m / m["AttackInterval"]
    speed = m["Speed"] * spd_m
    for p in m.get("Passives", []):
        k = p["Kind"]
        if k == "Evade":
            hp /= 1 - p["Chance"]
        elif k == "Shield" and p.get("Trigger") == "Start":
            hp *= 1.15
        elif k == "Shield" and p.get("Trigger") == "OnHit":
            hp *= 1 + 0.1 * p["Chance"] * p["Hits"]
        elif k == "Modifier":
            hp *= 1.05
        elif k == "Immunity":
            hp *= 1 + 0.03 * len(p["Statuses"])
        elif k == "SpeedBoost":
            speed *= 1 + (p["SpeedMultiplier"] - 1) * p["Duration"] / p["Interval"]
    if m.get("BlocksProjectile"):
        hp *= 1.3
    factor = math.sqrt(speed / 0.15)
    if m["AttackRange"] > 0:
        factor *= 1.2
    return math.sqrt(hp * dps) * factor * 3


def power(m, monsters, mult):
    v = solo_power(m, *mult)
    for p in m.get("Passives", []):
        if p["Kind"] == "Spawn":
            n = p["Count"] if p["Trigger"] == "OnDeath" else min(p["MaxTotal"], p["Count"] * 2)
            v += n * solo_power(monsters[p["MonsterId"]], *mult)
    return v


def stage_mult(stage):
    b = {x["Stat"]: 1 + x["Percent"] / 100 for x in stage.get("MonsterBonuses", [])}
    return (b.get("Hp", 1.0), b.get("Damage", 1.0), b.get("Speed", 1.0))


# ---------- 곡선 ----------

def curve(spec, s):
    """base * s^exponent * growth^(s-1) 기본형에 knots(스테이지->배수)를 선형 보간해 곱한다."""
    v = spec["base"] * s ** spec.get("exponent", 0.0) * spec.get("growth", 1.0) ** (s - 1)
    knots = spec.get("knots")
    if knots:
        pts = sorted((int(k), x) for k, x in knots.items())
        if s <= pts[0][0]:
            v *= pts[0][1]
        elif s >= pts[-1][0]:
            v *= pts[-1][1]
        else:
            for (a, x), (b, y) in zip(pts, pts[1:]):
                if a <= s <= b:
                    v *= x + (y - x) * (s - a) / (b - a)
                    break
    return v


def target_ratio(cfg, s):
    t = cfg["target"]
    r = t["base"] + t.get("perStage", 0.0) * (s - 1)
    every = t.get("spikeEvery", 0)
    if every and s % every == 0:
        r += t.get("spike", 0.0)
    return r


def tuning_k(cfg, s):
    """오토플레이로 맞춘 스테이지별 난이도 배수 (curves.json tuning.stageMultiplier, 없으면 1)."""
    return cfg.get("tuning", {}).get("stageMultiplier", {}).get(str(s), 1.0)


def wave_weights(cfg, n, player_by_wave=None):
    """웨이브별 예산 비중 = 압박 곡선(waveShape) * 그 웨이브 시점 플레이어 전투력.
    압박 곡선이 평평하면 모든 웨이브에서 적/플레이어 비율이 같아진다."""
    w = cfg["waveShape"]
    vals = []
    for i in range(n):
        t = i / (n - 1) if n > 1 else 1
        v = w["start"] + (w["end"] - w["start"]) * t ** w.get("exponent", 1.0)
        if player_by_wave:
            v *= player_by_wave[i]
        vals.append(v)
    total = sum(vals)
    return [v / total for v in vals]


# ---------- 스테이지 분석 ----------

def analyze(stage, monsters):
    """풀 구성을 읽는다: 등장 순서대로 역할(base/second/third), 엘리트는 1마리 등장."""
    first, total = {}, {}
    for wi, wave in enumerate(stage["Waves"]):
        for sp in wave["Spawns"]:
            first.setdefault(sp["MonsterId"], wi)
            total[sp["MonsterId"]] = total.get(sp["MonsterId"], 0) + sp["Count"]
    elite = None
    roles = []
    for mid in sorted(first, key=lambda i: first[i]):
        if monsters[mid]["Grade"] != "Normal" and total[mid] == 1:
            elite = (mid, first[mid])
        else:
            roles.append((mid, first[mid]))
    return roles, elite


def stage_power(stage, monsters):
    mult = stage_mult(stage)
    per_wave = []
    for wave in stage["Waves"]:
        per_wave.append(sum(sp["Count"] * power(monsters[sp["MonsterId"]], monsters, mult)
                            for sp in wave["Spawns"]))
    return per_wave



# ---------- 플레이어 전투력 (공식) ----------

_tables = None


def tables():
    global _tables
    if _tables is None:
        _tables = pl.Tables()
    return _tables


def player_curve(cfg, stage, monsters):
    """스테이지의 웨이브별 플레이어 전투력(scale 적용)과 계정 레벨. mode가 formula가 아니면 곡선 값을 쓴다."""
    spec = cfg["player"]
    n = len(stage["Waves"])
    if spec.get("mode") != "formula":
        v = curve(spec, stage["Id"])
        return [v] * n, 0
    sd = dict(stage)
    _, elite = analyze(stage, monsters)
    sd["_eliteWave"] = elite[1] + 1 if elite else None
    raw, lvl = pl.power_curve(tables(), cfg, stage["Id"], sd)
    return [spec["scale"] * r for r in raw], lvl


def player_power(cfg, stage, monsters):
    c, lvl = player_curve(cfg, stage, monsters)
    return sum(c) / len(c), lvl


# ---------- 명령 ----------

def cmd_report(args):
    cfg = load(HERE / "curves.json")
    monsters = {m["Id"]: m for m in load(DATA / "Monsters.json")}
    stages = load(args.stages)
    print(f"{'S':>3} {'lv':>3} {'player':>8} {'tgtR':>5} {'tgtSum':>7} {'actSum':>7} {'actR':>6} "
          f"{'diff':>6} {'wv1R':>5} {'wvLastR':>7} {'HP%':>5} {'DMG%':>5}")
    for st in stages:
        s = st["Id"]
        pc, lvl = player_curve(cfg, st, monsters)
        pw = sum(pc) / len(pc)
        r = target_ratio(cfg, s) * tuning_k(cfg, s)
        want = pw * r
        got = stage_power(st, monsters)
        tot = sum(got)
        hp, dm, _ = stage_mult(st)
        # 웨이브 압박 = 그 웨이브 적 전투력 / 그 시점 플레이어 전투력 * 웨이브 수 (스테이지 평균이 actR과 같은 눈금)
        n = len(got)
        w1 = got[0] / pc[0] * n
        wl = got[-1] / pc[-1] * n
        print(f"{s:>3} {lvl:>3} {pw:>8.0f} {r:>5.2f} {want:>7.0f} {tot:>7.0f} {tot / pw:>6.2f} "
              f"{(tot / want - 1) * 100:>+5.0f}% {w1:>5.2f} {wl:>7.2f} {(hp - 1) * 100:>5.0f} {(dm - 1) * 100:>5.0f}")


def cmd_calibrate(args):
    """기준 스테이지의 실제 적 총전투력이 목표 비율과 맞도록 player.scale을 정해 curves.json에 쓴다."""
    path = HERE / "curves.json"
    cfg = load(path)
    cfg["player"]["mode"] = "formula"
    cfg["player"]["scale"] = 1.0
    monsters = {m["Id"]: m for m in load(DATA / "Monsters.json")}
    stages = load(args.stages)
    anchor = next(s for s in stages if s["Id"] == args.anchor)
    raw, _ = player_curve(cfg, anchor, monsters)
    enemy = sum(stage_power(anchor, monsters))
    scale = enemy / (sum(raw) / len(raw) * target_ratio(cfg, anchor["Id"]) * tuning_k(cfg, anchor["Id"]))
    text = path.read_text(encoding="utf-8")
    if '"scale"' in text:
        text = re.sub(r'"scale":\s*[0-9.eE+-]+', f'"scale": {scale:.6g}', text)
        path.write_text(text, encoding="utf-8")
    else:
        sys.exit('curves.json의 player 항목에 "scale": 1 을 먼저 적어 두세요')
    print(f"scale = {scale:.6g} (anchor stage {args.anchor}: enemy {enemy:.0f}, player raw {sum(raw) / len(raw):.1f})")


def apply_tuning(stage, k, monsters):
    """오토플레이 배수 k를 적용한다: HP·공격 강화 배수를 k배로 곱하면 전투력이 k배가 된다.
    게임은 Percent <= 0 인 강화 줄을 거절하므로 강화 배수가 1 이하가 되면 그 줄을 빼고, 모자란 만큼은 일반 몬스터 수량으로 맞춘다.
    강화 줄이 없는 스테이지(예: 1관문)는 배수 1에서 시작한다."""
    if k == 1.0:
        return
    rows = stage.get("MonsterBonuses", [])
    factor = {st: 1.0 for st in ("Hp", "Damage")}
    for r in rows:
        if r["Stat"] in factor:
            factor[r["Stat"]] = 1 + r["Percent"] / 100
    kept = {}
    for st, f in factor.items():
        want = f * k
        kept[st] = want if round((want - 1) * 100) > 0 else 1.0
    achieved = ((kept["Hp"] * kept["Damage"]) / (factor["Hp"] * factor["Damage"])) ** 0.5
    others = [r for r in rows if r["Stat"] not in factor]
    new_rows = [{"Stat": st, "Percent": round((kept[st] - 1) * 100)} for st in ("Hp", "Damage") if kept[st] != 1.0]
    stage["MonsterBonuses"] = new_rows + others
    if not stage["MonsterBonuses"]:
        stage.pop("MonsterBonuses")
    scale = k / achieved
    if abs(scale - 1) > 0.01:
        for wave in stage["Waves"]:
            for sp in wave["Spawns"]:
                if monsters[sp["MonsterId"]]["Grade"] == "Normal":
                    sp["Count"] = max(1, round(sp["Count"] * scale))


def cmd_generate(args):
    cfg = load(HERE / "curves.json")
    monsters = {m["Id"]: m for m in load(DATA / "Monsters.json")}
    stages = load(args.stages)
    g = cfg["generate"]
    role_w = g["roleWeights"]
    out = []
    peak = 0
    first = stages[0]
    base_budget = player_power(cfg, first, monsters)[0] * target_ratio(cfg, first["Id"])
    for st in stages:
        s = st["Id"]
        new = json.loads(json.dumps(st))
        pw, _ = player_power(cfg, st, monsters)
        budget = pw * target_ratio(cfg, s)
        # 성장분 중 bonusShare 만큼은 HP/공격 강화율로, 나머지는 수량으로 흡수한다.
        gain = budget / base_budget
        bonus = gain ** g["bonusShare"]
        skew = g["hpShare"]
        # 전투력이 sqrt(HP*공격)에 비례하므로 배수 곱이 bonus^2 가 되어야 전투력이 bonus 배가 된다.
        hp_m = bonus ** (2 * skew)
        dm_m = bonus ** (2 * (1 - skew))
        if s == 1 and not g.get("bonusOnStage1", False):
            hp_m = dm_m = 1.0
        bl = [b for b in new.get("MonsterBonuses", []) if b["Stat"] not in ("Hp", "Damage")]
        if hp_m > 1.0005 or dm_m > 1.0005:
            bl = [{"Stat": "Hp", "Percent": round((hp_m - 1) * 100)},
                  {"Stat": "Damage", "Percent": round((dm_m - 1) * 100)}] + bl
        if bl:
            new["MonsterBonuses"] = bl
        else:
            new.pop("MonsterBonuses", None)
        mult = stage_mult(new)

        roles, elite = analyze(st, monsters)
        n = len(st["Waves"])
        weights = wave_weights(cfg, n, player_curve(cfg, st, monsters)[0])
        for wi, wave in enumerate(new["Waves"]):
            wb = budget * weights[wi]
            spawns = []
            if elite and elite[1] == wi:
                spawns.append({"MonsterId": elite[0], "Count": 1})
                wb = max(0, wb - power(monsters[elite[0]], monsters, mult))
            active = [(mid, role_w[min(i, len(role_w) - 1)])
                      for i, (mid, start) in enumerate(roles) if start <= wi]
            wsum = sum(w for _, w in active)
            for mid, w in active:
                p = power(monsters[mid], monsters, mult)
                spawns.append({"MonsterId": mid, "Count": max(1, round(wb * w / wsum / p))})
            wave["Spawns"] = spawns
        # 수량은 위에서 target 기준으로 만들었다. 오토플레이 배수는 그 위에 곱한다
        apply_tuning(new, tuning_k(cfg, s), monsters)
        peak = max(peak, max(sum(sp["Count"] for sp in w["Spawns"]) for w in new["Waves"]))
        out.append(new)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    print(f"draft: {len(out)} stages -> {args.out} (max monsters per wave: {peak})")
    args.stages = args.out
    cmd_report(args)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["report", "generate", "calibrate"])
    ap.add_argument("--anchor", type=int, default=1)
    ap.add_argument("--stages", default=str(DATA / "Stages.json"))
    ap.add_argument("--out", default=str(HERE / "out/Stages.draft.json"))
    args = ap.parse_args()
    {"report": cmd_report, "generate": cmd_generate, "calibrate": cmd_calibrate}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
