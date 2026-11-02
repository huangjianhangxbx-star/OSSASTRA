"""Prepare only KT-VIS-06 owned code/tests/docs; never commit or push.

Run from the verified project after all writers stop. An empty real index is
required. A temporary index is checked before installation; working files are
never normalized or rewritten. Use --exclude-candidate NAME.cs.txt for an
explicitly unfinished root helper; --dry-run writes the local manifest only.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[2]
EXPECTED_ROOT = Path("E:/WORLDCREATOR/new/Project_ANKALUTE")
EVIDENCE = Path("E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06")
ROOMS = "Assets/OSSASTRA/Scripts/SceneVisibility/Rooms"
CANDIDATES = "Candidates/KT-VIS-06"
DOCS = "docs/ossastra/KT-VIS-06"
SEAMS = ("Assets/OSSASTRA/Scripts/Development/SceneVisibility/RoomVisibilityLabPanel.cs",)
NEVER_STAGE = {
    "Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterEquipmentView.cs",
    "Assets/OSSASTRA/Scripts/Development/SceneVisibility.meta",
    "docs/agent-sync/tasks/2026-11-02-camera-pitch45.md",
}
SYNC_BASELINES = {
    "docs/agent-sync/PROJECT_STATE.md": "PROJECT_STATE.md.before.bin",
    "docs/agent-sync/TASK_REGISTRY.md": "TASK_REGISTRY.md.before.bin",
    "docs/agent-sync/tasks/2026-11-02-kt-vis-04.md": "2026-11-02-kt-vis-04.md.before.bin",
    "OSSASTRA_RULES.md": "OSSASTRA_RULES.md.before.bin",
}
CODE_SUFFIXES = {".cs", ".shader", ".hlsl", ".meta"}
FORBIDDEN_SUFFIXES = {
    ".unity", ".prefab", ".asset", ".json", ".png", ".jpg", ".jpeg",
    ".gif", ".webp", ".bmp", ".tga", ".exr", ".psd", ".fbx", ".obj",
    ".blend", ".spine", ".skel", ".atlas", ".mp4", ".mov", ".webm",
    ".wav", ".ogg", ".mp3", ".flac", ".wem", ".fsb", ".bank",
    ".comsave", ".bak", ".session", ".lock", ".zip", ".dll", ".exe",
    ".mat", ".controller", ".anim", ".unitypackage",
}
UNFINISHED_NAME = re.compile(r"(?:^|[._-])(?:wip|draft|incomplete|unfinished|tmp)(?:[._-]|$)", re.I)
META_WARNING = re.compile(r"^(.+\.meta):(\d+): trailing whitespace\.$")
META_EMPTY_FIELD = re.compile(r"^\+[ \t]*(?:userData|assetBundleName|assetBundleVariant):[ \t]+$")


def git(*args: str, data: bytes | None = None, index: Path | None = None,
        check: bool = True) -> subprocess.CompletedProcess:
    env = os.environ.copy()
    if index is not None:
        env["GIT_INDEX_FILE"] = str(index)
    result = subprocess.run(["git", *args], cwd=ROOT, env=env, input=data,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if check and result.returncode:
        raise RuntimeError(f"git {args[0]} failed: {result.stderr.decode('utf-8', 'replace')}")
    return result


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_stable(path: Path) -> bytes:
    before = path.stat()
    data = path.read_bytes()
    after = path.stat()
    if (before.st_mtime_ns, before.st_size) != (after.st_mtime_ns, after.st_size):
        raise RuntimeError(f"Writer still active: {path}")
    return data


def assert_empty_index() -> None:
    if git("diff", "--cached", "--ita-visible-in-index", "--name-only", "-z").stdout:
        raise RuntimeError("Real index is not empty; do not consume another staged change")
    if git("ls-files", "--unmerged", "-z").stdout:
        raise RuntimeError("Unmerged real index; no publication staging")


def reject_forbidden(name: str) -> None:
    path = Path(name)
    if name in NEVER_STAGE or path.is_absolute() or ".." in path.parts:
        raise RuntimeError(f"Explicitly excluded publication path: {name}")
    if any(part.casefold() in {"library", "evidence", "bin", "obj", "__pycache__"} for part in path.parts):
        raise RuntimeError(f"Cache/local evidence is not publishable: {name}")
    check_path = path.with_suffix("") if path.suffix.casefold() == ".meta" else path
    if check_path.suffix.casefold() in FORBIDDEN_SUFFIXES:
        raise RuntimeError(f"Media/asset/save publication denied: {name}")


def collect_paths(excluded: set[str]) -> tuple[list[str], list[str]]:
    paths = set()
    skipped = []
    for path in (ROOT / ROOMS).rglob("*"):
        if path.is_file() and path.suffix in CODE_SUFFIXES:
            paths.add(path.relative_to(ROOT).as_posix())
    for name in SEAMS:
        if not (ROOT / name).is_file():
            raise RuntimeError(f"Required seam source missing: {name}")
        paths.add(name)
        if (ROOT / (name + ".meta")).is_file():
            paths.add(name + ".meta")
    for name in excluded:
        if Path(name).name != name or not name.endswith(".cs.txt") or not (ROOT / CANDIDATES / name).is_file():
            raise RuntimeError(f"Exclude only an existing root candidate NAME.cs.txt: {name}")
    for path in (ROOT / CANDIDATES).glob("*.cs.txt"):
        if path.name in excluded or UNFINISHED_NAME.search(path.name):
            skipped.append(path.relative_to(ROOT).as_posix())
        else:
            paths.add(path.relative_to(ROOT).as_posix())
    paths.update(f"{CANDIDATES}/{name}" for name in ("mcp_call.py", "prepare_publication.py"))
    for folder in ("CoreTests", "RenderTests"):
        for path in (ROOT / CANDIDATES / folder).iterdir():
            if path.is_file() and (path.suffix in {".cs", ".csproj", ".md", ".log", ".hlsl"} or path.name.endswith(".cs.txt")):
                if UNFINISHED_NAME.search(path.name):
                    skipped.append(path.relative_to(ROOT).as_posix())
                else:
                    paths.add(path.relative_to(ROOT).as_posix())
    paths.add(f"{CANDIDATES}/README.md")
    paths.add(f"{CANDIDATES}/check_protection.py")
    paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT / DOCS).glob("*.md") if p.is_file())
    for name in paths:
        reject_forbidden(name)
        path = ROOT / name
        if not path.is_file() or not path.resolve().is_relative_to(ROOT):
            raise RuntimeError(f"Missing or external publication file: {name}")
    return sorted(paths), sorted(skipped)


def inspect_dirty_baseline(manifest: set[str]) -> dict:
    baseline = json.loads((EVIDENCE / "protected-dirty-before.json").read_text(encoding="utf-8-sig"))
    preserved = []
    for name, expected in baseline.items():
        if name in SYNC_BASELINES:
            continue
        path = ROOT / name
        actual = sha(read_stable(path)) if path.is_file() else None
        if actual != expected:
            raise RuntimeError(f"Pre-existing dirty content changed: {name}")
        preserved.append(name)
    changed = git("diff", "--name-only", "--no-renames", "HEAD", "-z").stdout.split(b"\0")
    changed += git("ls-files", "--others", "--exclude-standard", "-z").stdout.split(b"\0")
    changed = sorted({p.decode("utf-8") for p in changed if p})
    owned_source, excluded_source = [], []
    for name in changed:
        if not name.startswith("Assets/OSSASTRA/Scripts/") or Path(name).suffix not in CODE_SUFFIXES:
            continue
        if name in manifest:
            owned_source.append(name)
        elif name in baseline or name in NEVER_STAGE:
            excluded_source.append(name)
        else:
            raise RuntimeError(f"New source change is outside the owned whitelist: {name}")
    return {"preservedOriginalDirty": preserved, "ownedChangedSources": owned_source,
            "excludedOriginalOrExplicitSources": excluded_source}


def synthetic_sync() -> tuple[dict[str, bytes], list[dict]]:
    baseline = json.loads((EVIDENCE / "protected-dirty-before.json").read_text(encoding="utf-8-sig"))
    contents, proofs = {}, []
    for name, backup_name in SYNC_BASELINES.items():
        before = (EVIDENCE / backup_name).read_bytes()
        current = read_stable(ROOT / name)
        head = git("show", "HEAD:" + name).stdout
        if name in baseline and sha(before) != baseline[name]:
            raise RuntimeError(f"Sync backup does not match original dirty baseline: {name}")
        if name not in baseline and before != head:
            raise RuntimeError(f"Unexpected original dirty content has no baseline proof: {name}")
        if not current.startswith(before):
            raise RuntimeError(f"Original sync bytes are not an exact preserved prefix: {name}")
        suffix = current[len(before):]
        contents[name] = head + suffix
        proofs.append({"path": name, "backup": str(EVIDENCE / backup_name),
                       "originalPrefixPreserved": True, "baselineSha256": sha(before),
                       "workingFileSha256": sha(current),
                       "headSha256": sha(head), "suffixBytes": len(suffix),
                       "suffixSha256": sha(suffix), "syntheticIndexSha256": sha(head + suffix)})
    return contents, proofs


def cached_checks(index: Path, allowed: set[str]) -> tuple[list[str], list[dict]]:
    names = [s.decode("utf-8") for s in git("diff", "--cached", "--name-only", "-z", index=index).stdout.split(b"\0") if s]
    for name in names:
        reject_forbidden(name)
        if name not in allowed:
            raise RuntimeError(f"Unexpected staged path: {name}")
    # Execute the full check; only native Unity empty meta fields are an explicit exception.
    full = git("diff", "--cached", "--check", index=index, check=False)
    exceptions = []
    if full.returncode:
        lines = full.stdout.decode("utf-8", "replace").splitlines()
        if full.returncode != 2 or full.stderr or not lines or len(lines) % 2:
            raise RuntimeError("Cached diff-check failed: " + full.stdout.decode("utf-8", "replace"))
        for pos in range(0, len(lines), 2):
            match = META_WARNING.fullmatch(lines[pos])
            if match is None or not META_EMPTY_FIELD.fullmatch(lines[pos + 1]):
                raise RuntimeError("Non-native-meta whitespace error: " + "\n".join(lines[pos:pos + 2]))
            exceptions.append({"path": match[1], "line": int(match[2]), "field": lines[pos + 1][1:].strip()})
    non_meta = [p for p in names if not p.endswith(".meta")]
    if non_meta:
        git("diff", "--cached", "--check", "--", *non_meta, index=index)
    return names, exceptions


def install_index(candidate: Path, real: Path, expected_head: str,
                  original_index: bytes | None) -> None:
    lock = real.with_name(real.name + ".lock")
    fd = os.open(lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    owns_lock = True
    try:
        assert_empty_index()
        if git("rev-parse", "HEAD").stdout.decode().strip() != expected_head:
            raise RuntimeError("HEAD changed while preparing publication")
        if (real.read_bytes() if real.is_file() else None) != original_index:
            raise RuntimeError("Real index metadata changed; preserve the other writer's index")
        with os.fdopen(fd, "wb") as stream:
            fd = -1
            stream.write(candidate.read_bytes())
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(lock, real)
        owns_lock = False
    finally:
        if fd >= 0:
            os.close(fd)
        if owns_lock and lock.exists():
            lock.unlink()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exclude-candidate", action="append", default=[], metavar="NAME.cs.txt")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    if any(os.environ.get(k) for k in ("GIT_INDEX_FILE", "GIT_DIR", "GIT_WORK_TREE")):
        raise RuntimeError("Do not inherit a Git index/directory/worktree override")
    if ROOT != EXPECTED_ROOT.resolve() or Path(git("rev-parse", "--show-toplevel").stdout.decode().strip()).resolve() != ROOT:
        raise RuntimeError("Wrong project root")
    assert_empty_index()
    head = git("rev-parse", "HEAD").stdout.decode().strip()
    paths, skipped = collect_paths(set(args.exclude_candidate))
    dirty = inspect_dirty_baseline(set(paths))
    sync, proofs = synthetic_sync()
    snapshot = {name: read_stable(ROOT / name) for name in [*paths, *sync]}
    if any(sha(snapshot[proof["path"]]) != proof["workingFileSha256"] for proof in proofs):
        raise RuntimeError("Sync writer changed an append while preparing the snapshot")
    for name in paths:
        if not snapshot[name] or re.search(rb"(?m)^(?:<<<<<<< |=======\r?$|>>>>>>> )", snapshot[name]):
            raise RuntimeError(f"Empty or unresolved candidate file: {name}")
    git_dir = Path(git("rev-parse", "--absolute-git-dir").stdout.decode().strip())
    real_index = git_dir / "index"
    original_index = read_stable(real_index) if real_index.is_file() else None
    report = {"head": head, "project": str(ROOT), "dryRun": args.dry_run,
              "manifestPaths": paths, "excludedUnfinishedCandidates": skipped,
              "syntheticSyncPaths": sorted(sync),
              "explicitNeverStage": sorted(NEVER_STAGE), "syncProofs": proofs, **dirty}
    with tempfile.TemporaryDirectory(prefix="kt-vis06-index-", dir=git_dir) as directory:
        index = Path(directory) / "index"
        if original_index is None:
            git("read-tree", "HEAD", index=index)
        else:
            index.write_bytes(original_index)  # Preserve unrelated index flags/stat metadata.
        git("add", "-f", "--", *paths, index=index)
        for name, content in sync.items():
            blob = git("hash-object", "-w", "--stdin", data=content).stdout.decode().strip()
            mode = git("ls-tree", "HEAD", "--", name).stdout.decode().split()[0]
            git("update-index", "--cacheinfo", f"{mode},{blob},{name}", index=index)
        staged, exceptions = cached_checks(index, set(paths) | set(sync))
        if any(read_stable(ROOT / name) != data for name, data in snapshot.items()):
            raise RuntimeError("Publication input changed; stop writers before staging")
        assert_empty_index()
        if git("rev-parse", "HEAD").stdout.decode().strip() != head:
            raise RuntimeError("HEAD changed; rebuild publication from the new checkpoint")
        report.update({"stagedPaths": staged, "stagedCount": len(staged),
                       "unchangedManifestPaths": sorted((set(paths) | set(sync)) - set(staged)),
                       "nativeMetaWhitespaceExceptions": exceptions, "cachedDiffCheckPassed": True,
                       "indexInstalled": False, "worktreeFilesRewritten": False})
        manifest = EVIDENCE / "publication-manifest.json"
        manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if not args.dry_run:
            install_index(index, real_index, head, original_index)
            report["indexInstalled"] = True
            manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"manifest": str(manifest), "stagedCount": report["stagedCount"],
                      "indexInstalled": report["indexInstalled"], "dryRun": args.dry_run}, ensure_ascii=False))


if __name__ == "__main__":
    main()
