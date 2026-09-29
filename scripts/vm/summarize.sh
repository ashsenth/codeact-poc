#!/usr/bin/env bash
# Print a compact, human-readable summary of the newest comparison JSON per profile.
python3 - <<'PY'
import json, glob, os

def find(d, *names):
    for n in names:
        if isinstance(d, dict) and n in d:
            return d[n]
    return None

for prof in ('small', 'medium', 'large'):
    files = sorted(glob.glob(f'/root/codeact-poc/artifacts/comparisons/{prof}-*.json'))
    if not files:
        print(f'[{prof}] (no results yet)')
        continue
    f = files[-1]
    d = json.load(open(f))
    print(f'=== {prof}: {os.path.basename(f)} ===')
    print('  households:', find(d, 'datasetHouseholdCount'))
    fair = find(d, 'fairness') or {}
    print('  fairness matched:', find(fair, 'matched'), '| mismatches:', find(fair, 'mismatches'))
    for key in ('classic', 'codeAct'):
        m = find(d, key) or {}
        dur = find(m, 'durationMs') or {}
        rt = find(m, 'roundTrips') or {}
        cor = find(m, 'correctness') or {}
        cost = find(m, 'cost') or {}
        print(f'  {key:8}: trials={find(m,"trials")}'
              f' durMedian={find(dur,"median")}'
              f' toolCalls={find(rt,"medianToolCalls")}'
              f' execCode={find(rt,"medianExecuteCodeCalls")}'
              f' exact={find(cor,"exactCount")} failed={find(cor,"failedCount")}'
              f' costMed={find(cost,"median")}')
    rawt = find(d, 'rawTrials') or []
    print('  rawTrials statuses:', [f"{find(t,'mode')}:{find(t,'status')}" for t in rawt])
    delta = find(d, 'delta')
    print('  delta:', json.dumps(delta) if delta else None)
PY
