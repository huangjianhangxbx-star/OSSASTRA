"""Read-only task baseline comparison. Only writes this task's external result JSON."""
import hashlib
import json
import re
from datetime import datetime, timezone, timedelta
from pathlib import Path

PROJECT = Path('E:/WORLDCREATOR/new/Project_ANKALUTE')
EVIDENCE = Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-03')
digest = lambda data: hashlib.sha256(data).hexdigest()
baseline = json.loads((EVIDENCE / 'dirty-before.json').read_text(encoding='utf-8-sig'))
rows = []
for relative, expected in baseline.items():
    if relative == 'docs/agent-sync/TASK_REGISTRY.md':
        continue
    path = PROJECT / relative
    actual = digest(path.read_bytes()) if path.is_file() else None
    rows.append({'path': relative, 'passed': actual == expected and (expected is not None or not path.exists())})
normalize = lambda value: value.decode('utf-8-sig').replace('\r\n', '\n')
original = normalize((EVIDENCE / 'registry-before.bin').read_bytes())
current = normalize((PROJECT / 'docs/agent-sync/TASK_REGISTRY.md').read_bytes())
remaining, count = re.subn(r'(?m)^## KT-VIS-03[^\n]*\n[\s\S]*?(?=^## |\Z)', '', current)
registry_ok = count == 1 and remaining == original
saves = []
for row in json.loads((EVIDENCE / 'protected-files-before.json').read_text(encoding='utf-8-sig')):
    if row['kind'] != 'protected-save':
        continue
    data = Path(row['path']).read_bytes()
    saves.append({'path': row['path'], 'passed': len(data) == row['bytes'] and digest(data) == row['sha256']})
passed = len(rows) == 36 and all(row['passed'] for row in rows) and registry_ok and len(saves) == 8 and all(row['passed'] for row in saves)
result = {'status': 'PASS' if passed else 'FAIL', 'observedAt': datetime.now(timezone(timedelta(hours=8))).isoformat(),
          'originalDirtyNonRegistry': rows, 'registryUnownedContentPreserved': registry_ok, 'protectedSaveFiles': saves,
          'scope': 'Current baseline only; Main scene and four templates intentionally changed through Native. No repair, recovery, Git or Unity mutation.'}
(EVIDENCE / 'final-safety.json').write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + '\n').encode())
print(json.dumps({'status': result['status'], 'originalDirty': len(rows)+1, 'protectedSaveFiles': len(saves), 'registry': registry_ok}))
raise SystemExit(0 if passed else 1)
