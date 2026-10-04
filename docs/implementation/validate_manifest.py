"""Validate planning structure, not runtime truth. Python 3.10+; standard library only."""
from __future__ import annotations
import argparse
import json
import re
from itertools import combinations
from pathlib import Path, PurePosixPath

FEATURES = {'Accounts','Profiles','Catalog','Inventory','Wallet','Entitlements','RewardFulfillment','Progression','Quests','Achievements','Store','Purchases','Leaderboards','Teams','RemoteConfig','Inbox'}
REQUIRED = {'id','title','owner_repo','role','milestone','dependencies','status','blocked_reason','owned_paths','locks','acceptance_ids','test_environment','peer_dependencies','baseline_commit','card','evidence_paths','evidence','wave'}
TASK_STATES = {'planned','in_progress','blocked','implemented','complete'}
SDK_STATES = {'stubbed','planned','implemented','unverified','blocked'}
UNITY_STATES = {'not_installed','planned','wired','integrated','unverified','blocked'}

def safe_path(value: str) -> bool:
    return isinstance(value,str) and bool(value) and '\\' not in value and not PurePosixPath(value).is_absolute() and '..' not in PurePosixPath(value).parts and not re.match(r'^[A-Za-z]:',value)

def ownership_collision(a: dict, b: dict) -> bool:
    if a['owner_repo'] != b['owner_repo']:
        return False
    if set(a['locks']) & set(b['locks']):
        return True
    return any(x.rstrip('/') == y.rstrip('/') or x.startswith(y.rstrip('/')+'/') or y.startswith(x.rstrip('/')+'/') for x in a['owned_paths'] for y in b['owned_paths'])

def validate(data: dict) -> list[str]:
    errors=[]
    if data.get('schema_version') != 1 or data.get('planning_only') is not True:
        errors.append('unsupported schema or missing planning_only marker')
    tasks=data.get('tasks',[])
    if not isinstance(tasks,list) or not tasks:
        return errors+['tasks must be a nonempty list']
    baselines=data.get('baseline_commits',{})
    peers=data.get('external_prerequisites',{})
    ids=[]
    for t in tasks:
        if not isinstance(t,dict):
            errors.append('task must be an object'); continue
        missing=REQUIRED-set(t)
        if missing:
            errors.append(f"{t.get('id','?')}: missing fields {sorted(missing)}"); continue
        ident=t['id']; ids.append(ident)
        if not isinstance(ident,str) or not re.fullmatch(r'(CL|INT)-\d{3}',ident): errors.append(f'invalid task ID {ident!r}')
        if t['owner_repo'] not in baselines: errors.append(f'{ident}: unknown owner repository')
        if t['baseline_commit'] != baselines.get(t['owner_repo']): errors.append(f'{ident}: baseline mismatch')
        if t['status'] not in TASK_STATES: errors.append(f'{ident}: invalid task status')
        if not isinstance(t['wave'],int) or isinstance(t['wave'],bool) or t['wave']<0: errors.append(f'{ident}: invalid wave')
        for field in ('dependencies','owned_paths','locks','acceptance_ids','test_environment','peer_dependencies','evidence_paths','evidence'):
            if not isinstance(t[field],list): errors.append(f'{ident}: {field} must be a list')
        if t['status']=='blocked' and not t['blocked_reason']: errors.append(f'{ident}: blocked without reason')
        if t['status'] in {'implemented','complete'} and not t['evidence']: errors.append(f"{ident}: {t['status']} without evidence")
        if not t['owned_paths'] or not t['evidence_paths'] or not t['acceptance_ids'] or not t['test_environment']: errors.append(f'{ident}: empty ownership/evidence/acceptance/environment')
        for p in t['owned_paths']+t['evidence_paths']:
            if not safe_path(p): errors.append(f'{ident}: unsafe path {p!r}')
        if not t['card'].endswith('#'+ident.lower()): errors.append(f'{ident}: card anchor mismatch')
        for peer in t['peer_dependencies']:
            if peer not in peers: errors.append(f'{ident}: unknown peer prerequisite {peer}')
        for ac in t['acceptance_ids']+t.get('conditional_acceptance_ids',[]):
            if not re.fullmatch(r'(A(?:0[1-9]|1[0-3])|LC(?:0[1-9]|1[0-3])|DI(?:0[1-9]|1[0-2]))',ac): errors.append(f'{ident}: invalid acceptance ID {ac}')
    if len(ids)!=len(set(ids)): errors.append('duplicate task IDs')
    if errors: return errors
    byid={t['id']:t for t in tasks}
    for t in tasks:
        for dep in t['dependencies']:
            if dep not in byid: errors.append(f"{t['id']}: missing dependency {dep}")
            elif byid[dep]['wave']>=t['wave']: errors.append(f"{t['id']}: dependency {dep} not in earlier wave")
    visiting=set(); visited=set()
    def visit(ident: str) -> None:
        if ident in visiting:
            errors.append('dependency cycle at '+ident); return
        if ident in visited: return
        visiting.add(ident)
        for dep in byid[ident]['dependencies']:
            if dep in byid: visit(dep)
        visiting.remove(ident); visited.add(ident)
    for ident in byid: visit(ident)
    for a,b in combinations(tasks,2):
        if a['wave']==b['wave'] and ownership_collision(a,b): errors.append(f"same-wave ownership collision: {a['id']} / {b['id']}")
    ledger=data.get('feature_ledger',[])
    names=[row.get('feature') for row in ledger]
    if len(names)!=16 or set(names)!=FEATURES: errors.append('ledger must cover all sixteen features exactly once')
    for row in ledger:
        name=row.get('feature','?')
        for key in ('sdk_task','integration_tasks','sdk_status','mrsquare_integration_status','sdk_evidence','mrsquare_evidence','package_compatibility','backend_compatibility'):
            if key not in row: errors.append(f'{name}: missing ledger field {key}')
        if any(key not in row for key in ('sdk_task','integration_tasks','sdk_status','mrsquare_integration_status','sdk_evidence','mrsquare_evidence','package_compatibility','backend_compatibility')): continue
        if row['sdk_status'] not in SDK_STATES or row['mrsquare_integration_status'] not in UNITY_STATES: errors.append(f'{name}: invalid ledger status')
        for ident in [row['sdk_task']]+row['integration_tasks']:
            if ident not in byid: errors.append(f'{name}: unknown task {ident}')
        if row['sdk_status']=='implemented' and (not row['sdk_evidence'] or byid.get(row['sdk_task'],{}).get('status')!='complete'): errors.append(f'{name}: implemented without completed task/evidence')
        if row['mrsquare_integration_status']=='integrated':
            if row['sdk_status']!='implemented' or not row['mrsquare_evidence'] or not row['package_compatibility'] or not row['backend_compatibility'] or any(byid.get(i,{}).get('status')!='complete' for i in row['integration_tasks']): errors.append(f'{name}: integrated without independent implementation/consumer/compatibility evidence')
    return errors

def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest',nargs='?',type=Path,default=Path(__file__).with_name('execution-manifest.json'))
    parser.add_argument('--ready',action='store_true')
    args=parser.parse_args()
    try:
        data=json.loads(args.manifest.read_text(encoding='utf-8'))
        errors=validate(data)
    except (OSError,ValueError,TypeError,KeyError,AttributeError) as exc:
        print('INVALID:',exc); return 1
    if errors:
        print('\n'.join('INVALID: '+e for e in errors)); return 1
    tasks=data['tasks']; complete={t['id'] for t in tasks if t['status']=='complete'}
    print(f"VALID: {len(tasks)} tasks; {len(data['feature_ledger'])} feature ledgers; {len({t['wave'] for t in tasks})} conservative waves")
    if args.ready:
        peer_evidence=data.get('peer_evidence',{})
        ready=[t['id'] for t in tasks if t['status']=='planned' and set(t['dependencies'])<=complete and all(peer_evidence.get(p) for p in t['peer_dependencies'])]
        print('READY TO START (environment verification still required): '+', '.join(ready))
    return 0

if __name__=='__main__':
    raise SystemExit(main())
