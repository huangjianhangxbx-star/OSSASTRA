"""Read-only verification of the fixed ENV-01 source/save and dirty-byte baselines."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = Path("E:/WORLDCREATOR/XingHaiHuiLang/研究/环境模拟/ENV-01")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def main():
    sources = json.loads((EVIDENCE / "sources-saves-before.json").read_text(encoding="utf-8-sig"))
    dirty = json.loads((EVIDENCE / "protected-dirty-before.json").read_text(encoding="utf-8-sig"))
    sync = ("docs/agent-sync/PROJECT_STATE.md", "docs/agent-sync/TASK_REGISTRY.md")
    differences = [name for name, expected in sources.items() if digest(Path(name)) != expected]
    dirty_differences = [name for name, expected in dirty.items() if name not in sync and digest(ROOT / name) != expected]
    prefix = all((ROOT / name).read_bytes().startswith((EVIDENCE / (Path(name).name + ".before.bin")).read_bytes()) for name in sync)
    report = dict(pass_=not differences and not dirty_differences and prefix, protectedCount=len(sources), dirtyCount=len(dirty), sourceDifferences=differences, dirtyDifferences=dirty_differences, syncPrefixesPreserved=prefix)
    (EVIDENCE / "protection-final.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
    if not report["pass_"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
