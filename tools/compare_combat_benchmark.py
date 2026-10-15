#!/usr/bin/env python3
"""Compare two complete CombatBenchmark exports; reject unequal workloads."""
import argparse
import csv
import json
import math
from pathlib import Path
from statistics import mean

KEYS = ('unity', 'device', 'os', 'gpu', 'graphicsApi', 'resolution', 'enemyCount',
        'projectilesPerVolley', 'volleyEveryFrames', 'warmupFrames', 'measurementFrames',
        'repetitions', 'seed', 'enemyPrefab', 'projectilePrefab', 'hitscanPrefab', 'areaPrefab', 'beamPrefab', 'chainPrefab', 'workloads', 'scope', 'timing')


def read(folder):
    folder = Path(folder)
    report = json.loads((folder / 'summary.json').read_text())
    if not report['complete'] or report['interrupted']:
        raise ValueError(f'Incomplete or interrupted run: {folder}')
    with (folder / 'frames.csv').open() as source:
        frames = list(csv.DictReader(source))
    expected = report['measurementFrames'] * report['repetitions'] * len(report['workloads'])
    if len(frames) != expected:
        raise ValueError(f'Unexpected frame count: {len(frames)} != {expected}')
    expected_trials = {(w, r) for w in report['workloads'] for r in range(1, report['repetitions'] + 1)}
    actual_trials = {(t['workload'], t['repetition']) for t in report['trials']}
    if actual_trials != expected_trials or len(report['trials']) != len(expected_trials):
        raise ValueError('Missing or duplicate workload trials')
    if any(t['hpEvents'] <= 0 or t['attacks'] <= 0 for t in report['trials']):
        raise ValueError('A workload did not exercise damage')
    context_file = folder / 'measurement-context.json'
    report['measurementContext'] = json.loads(context_file.read_text()) if context_file.exists() else None
    return report, frames


def compare(before, after):
    b, bf = read(before)
    a, af = read(after)
    mismatches = [key for key in KEYS if b.get(key) != a.get(key)]
    if mismatches:
        raise ValueError('Different measurement conditions: ' + ', '.join(mismatches))
    workload_keys = lambda r: {(t['workload'], t['repetition']):
                              (t['frames'], t['attacks'], t['hpEvents'], t['deaths']) for t in r['trials']}
    if workload_keys(b) != workload_keys(a):
        raise ValueError('Attacks / HP events / deaths differ. Do not claim an equivalent-workload speedup.')
    result = {'before': b['label'], 'after': a['label'], 'conditionsMatch': True,
              'note': 'Timing includes frame pacing. Allocations include all threads. No statistical significance claim.',
              'measurementContext': {'before': b['measurementContext'], 'after': a['measurementContext']},
              'workloads': []}
    for workload in sorted({f['workload'] for f in bf}):
        row = {'workload': workload}
        for label, report, data in [('before', b, bf), ('after', a, af)]:
            subset = [f for f in data if f['workload'] == workload]
            times = sorted(float(f['wall_ms']) for f in subset)
            percentile = lambda q: times[max(0, math.ceil(len(times) * q) - 1)]
            counts = [t for t in report['trials'] if t['workload'] == workload]
            byte_count = sum(int(f['gc_allocated_bytes']) for f in subset)
            events = sum(t['hpEvents'] for t in counts)
            row[label] = {'frames': len(times), 'meanMs': mean(times), 'p50Ms': percentile(.5),
                          'p95Ms': percentile(.95), 'p99Ms': percentile(.99), 'maxMs': times[-1],
                          'hpEvents': events, 'deaths': sum(t['deaths'] for t in counts),
                          'meanAllocatedBytes': byte_count / len(times) if report['allocationAvailable'] else None,
                          'allocatedBytesPerHpEvent': byte_count / events if report['allocationAvailable'] and events else None,
                          'gcCollections': sum(t['collections'] for t in counts)}
        row['changePercent'] = {key: (row['after'][key] / row['before'][key] - 1) * 100
                                for key in ('meanMs', 'p95Ms', 'p99Ms', 'meanAllocatedBytes', 'allocatedBytesPerHpEvent')
                                if row['before'][key] not in (None, 0) and row['after'][key] is not None}
        result['workloads'].append(row)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('before'); parser.add_argument('after'); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    result = compare(args.before, args.after)
    output = Path(args.output); output.mkdir(parents=True, exist_ok=True)
    (output / 'comparison.json').write_text(json.dumps(result, ensure_ascii=False, indent=2))
    lines = ['# 전후 성능 비교', '', '동일 기기·설정·작업 횟수를 확인한 결과입니다. 음수 변화율은 감소입니다.', '',
             '| 부하 | P95 ms 전 → 후 | P99 ms 전 → 후 | 프레임당 할당 B 전 → 후 |', '|---|---:|---:|---:|']
    for row in result['workloads']:
        b, a = row['before'], row['after']
        alloc = lambda x: '측정 불가' if x is None else f'{x:.1f}'
        lines.append(f"| {row['workload']} | {b['p95Ms']:.2f} → {a['p95Ms']:.2f} | {b['p99Ms']:.2f} → {a['p99Ms']:.2f} | {alloc(b['meanAllocatedBytes'])} → {alloc(a['meanAllocatedBytes'])} |")
    contexts = result['measurementContext']
    if contexts['before'] and contexts['after']:
        states = lambda side: ' → '.join(contexts[side].get('thermalObserved', []))
        lines += ['', f"발열 관측: 전 {states('before')}, 후 {states('after')}.",
                  '녹화 OFF. 실행 순서는 최적화 후 → 최적화 전입니다. 두 실행 모두 Nominal로 시작했으나 이후 발열 상태가 달라 프레임 시간 개선율에는 발열 영향이 포함됩니다.']
    lines += ['', '프레임 시간에는 목표 FPS 대기가 포함됩니다. 할당은 모든 스레드의 프레임 할당입니다.',
              '이 부하는 HP 표시·사망 경로를 포함하지만 StageManager·보상·저장·전체 최대 강화 빌드를 포함하지 않습니다.']
    (output / 'comparison.md').write_text('\n'.join(lines) + '\n')
    print(output / 'comparison.md')


if __name__ == '__main__':
    main()
