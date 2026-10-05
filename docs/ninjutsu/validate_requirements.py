"""Validate documentation references and runtime mappings; does not load game data."""
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
data = json.loads((HERE / 'requirements.json').read_text())
skills = {s['id']: s for s in data['skills']}
cards = {c['id']: c for c in data['cards']}
assert len(skills) == len(data['skills']) == 15
assert len(cards) == len(data['cards']) == 168
source = (HERE / 'ninjutsu-source-additional.txt').read_text().splitlines()
runtime = json.loads((REPO / 'Assets/_Project/Resources/MockData/Weapons.json').read_text())
runtime_cards = {(w['id'], c['id']): c for w in runtime for c in w['upgrades']}
mapped = set()
edges = {}
for c in cards.values():
    assert c['skillId'] in skills, c['id']
    assert c['maxAppearanceCount'] is None or c['maxAppearanceCount'] > 0, c['id']
    assert c['minPermanentLevel'] >= 0, c['id']
    assert c['source']['lines'], c['id']
    assert all(1 <= n <= len(source) for n in c['source']['lines']), c['id']
    assert c['decisionStatus'] != 'hold' or c['unresolvedIssues'], c['id']
    dependencies = []
    for r in c['requirements']:
        if r['kind'] == 'selectedCard':
            assert r['cardId'] in cards and r['cardId'] != c['id'], c['id']
            assert r['count'] > 0, c['id']
            cap = cards[r['cardId']]['maxAppearanceCount']
            assert cap is None or r['count'] <= cap, c['id']
            dependencies.append(r['cardId'])
        elif r['kind'] == 'ownedSkill':
            assert r['skillId'] in skills, c['id']
        else:
            assert r.get('status') in ('unresolved', 'unmapped'), c['id']
    edges[c['id']] = dependencies
    for e in c['exclusions']:
        assert e['selectedCardId'] in cards and e['selectedCardId'] != c['id'], c['id']
        assert e['belowPermanentLevel'] >= 0, c['id']
    if 'cardId' in c['runtime']:
        key = (c['runtime']['weaponId'], c['runtime']['cardId'])
        assert key in runtime_cards and key not in mapped, key
        assert c['runtime']['enabled'] == runtime_cards[key].get('enabled', True), key
        definition = runtime_cards[key]
        assert c['maxAppearanceCount'] == definition['maxPickCount'], key
        if c['runtime'].get('variantConnected'):
            rule = c['levelVariant']
            variants = [v for v in definition['variants'] if v['minPermanentLevel'] == rule['minPermanentLevel']]
            assert len(variants) == 1, key
            base_damage = [e['value'] for e in definition['effects'] if e['kind'] == 'damage']
            upgraded_damage = [e['value'] for e in variants[0]['effects'] if e['kind'] == 'damage']
            assert base_damage == [rule['damageModifierBefore']], key
            expected_after = [] if rule['damageModifierAfter'] == 0 else [rule['damageModifierAfter']]
            assert upgraded_damage == expected_after, key
            assert [e for e in definition['effects'] if e['kind'] != 'damage'] == [e for e in variants[0]['effects'] if e['kind'] != 'damage'], key
        mapped.add(key)
assert mapped == set(runtime_cards), 'Every current runtime card needs exactly one documentation mapping'

def visit(node, visiting, visited):
    assert node not in visiting, ('prerequisite cycle', node)
    if node in visited:
        return
    visiting.add(node)
    for dependency in edges[node]:
        visit(dependency, visiting, visited)
    visiting.remove(node)
    visited.add(node)

visited = set()
for node in cards:
    visit(node, set(), visited)

ice = {c['name']: c for c in cards.values() if c['skillId'] == 'ice_spear'}
assert ice['얼음창 연발+']['exclusions'][0]['selectedCardId'] == ice['얼음창 일제 사격']['id']
assert ice['얼음창 일제 사격']['exclusions'][0]['selectedCardId'] == ice['얼음창 관통']['id']
assert not ice['얼음창 관통']['exclusions'], 'Do not turn acquisition order into mutual exclusion'
assert all(s['maxLevelMeaning'] == 'maximum-successful-battle-upgrades-excluding-initial-acquisition' for s in skills.values())
expected_variants = {
    'kunai.01': (9, -20, 0), 'kunai.06': (17, -30, 10),
    'fireball.04': (9, -30, 0), 'ice_spear.01': (5, -20, 0),
    'lightning.02': (9, -20, 0), 'log.02': (5, -20, 0)
}
assert {c['id'] for c in cards.values() if 'levelVariant' in c} == set(expected_variants)
for card_id, expected in expected_variants.items():
    v = cards[card_id]['levelVariant']
    assert (v['minPermanentLevel'], v['damageModifierBefore'], v['damageModifierAfter']) == expected
    assert v['sharedSelectionCounter'] is True
for c in cards.values():
    if 'sharedUpgrade' in c:
        assert len(c['sharedUpgrade']['affectedSkillUpgradeCounts']) == 2
        assert c['skillId'] in c['sharedUpgrade']['affectedSkillUpgradeCounts']
print(f'PASS: {len(skills)} skills, {len(cards)} card rows, {len(mapped)} runtime mappings; IDs, references, counts, source lines and prerequisite graph valid')
