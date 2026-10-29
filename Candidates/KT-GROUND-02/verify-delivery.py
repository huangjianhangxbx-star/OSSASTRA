"""Verify Native Unity export against its exact plan, then build the matching source archive."""
from __future__ import annotations
import argparse
import hashlib
import json
import re
from pathlib import Path, PurePosixPath
import tarfile
import zipfile


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--package", type=Path, required=True)
    args = parser.parse_args()
    project = args.project.resolve()
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    expected = {row["path"]: row for row in plan["paths"]}
    assert len(expected) == 83 and plan["includeDependencies"] is False
    assert plan["recurse"] is False
    ancestors = {str(parent) for name in expected for parent in PurePosixPath(name).parents}
    archive_paths: dict[str, dict[str, bytes]] = {}
    with tarfile.open(args.package, "r:gz") as archive:
        groups: dict[str, dict[str, bytes]] = {}
        for member in archive:
            if not member.isfile():
                continue
            parts = PurePosixPath(member.name).parts
            assert len(parts) == 2 and ".." not in parts, member.name
            groups.setdefault(parts[0], {})[parts[1]] = archive.extractfile(member).read()
        for group in groups.values():
            assert "pathname" in group
            name = group["pathname"].decode("utf-8-sig").rstrip("\x00\n\r")
            assert name not in archive_paths
            assert name in expected or name in ancestors, name
            archive_paths[name] = group
    assert set(expected) <= archive_paths.keys()
    native_meta_equivalent = 0
    raw_meta_equal = 0
    for name, row in expected.items():
        group = archive_paths[name]
        assert sha(group["asset"]) == row["sha256"], name
        assert sha((project / name).read_bytes()) == row["sha256"], name
        disk_meta = (project / (name + ".meta")).read_bytes()
        assert sha(disk_meta) == row["metaSha256"], name
        # Tuanjie stores an encoded top-level .meta GUID. Its Native exporter emits the
        # AssetDatabase GUID. Compare that exact field through the reviewed Native plan;
        # every other metadata byte and every asset payload must stay identical.
        guid_pattern = rb"(?m)^guid: [^\r\n]+"
        native_guid = ("guid: " + row["guid"]).encode()
        assert re.search(guid_pattern, group["asset.meta"]).group() == native_guid, name
        assert re.sub(guid_pattern, native_guid, disk_meta, count=1) == group["asset.meta"], name
        native_meta_equivalent += 1
        raw_meta_equal += disk_meta == group["asset.meta"]
    forbidden = ("/LocalReference/", "/LocalGenerated/", "/LocalMedia/", "SC_GroundWorkshopLab.unity")
    assert not any(part in name for name in archive_paths for part in forbidden)
    source_files = set(expected) | {name + ".meta" for name in expected}
    readme = """# 星骸地表工坊 Core · KT-GROUND-02

这是纯自有源码、技术样板和制作手册，不包含原作媒体、角色、交易或战斗系统。

验证环境：Tuanjie 2022.3.62t16，URP 14.2，2D Tilemap Extras 3.1.3，Newtonsoft Json 3.2.2。
在Unity项目先安装相应Tilemap编辑器/Extras、URP和Newtonsoft依赖，再导入unitypackage，或把源码包的Assets合并到项目。
不要把单独的编译验证DLL放进Assets，它仅是排除游戏程序集的Editor源码编译证据。

菜单：Tools/OSSASTRA/星骸地表工坊。原创样板：Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopOriginalLab.unity。
手册：Assets/OSSASTRA/GroundWorkshop/Documentation/03-tutorial/地面组合逻辑教程.html。
新图请保存到Assets/OSSASTRA/GroundWorkshop/Maps或Samples。现有样板采用Native Tilemap，世界面积3072平方米、三层、44装饰。
纯Core不带游戏试玩适配；完整项目中的可选Playground模块另由GitHub源码提供，本机参考场及媒体不随包分发。

独立Editor Core实际25源编译0错误；已有工程和无参考依赖原创场已验证。
尚未验证全新空工程导入、Player构建或实体鼠标完整流程。UI_PHYSICAL_UNVERIFIED；审美和制作体验由用户确认。
7张手册图是原生Camera.Render完整帧，不是Editor主窗口截图；所有图片为本项目原创技术内容。
"""
    destination = args.package.with_name("GroundWorkshop-Source_" + plan["planHash"][:16] + ".zip")
    if destination.exists():
        raise FileExistsError(destination)
    public_plan = {key: value for key, value in plan.items() if key != "publicDocumentationManifest"}
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        # Copy Native export bytes intact, including portable Native GUID metadata.
        # Never rewrite the project's encoded .meta files or invent a decryption routine.
        for name in sorted(expected):
            archive.writestr(name, archive_paths[name]["asset"])
            archive.writestr(name + ".meta", archive_paths[name]["asset.meta"])
        archive.writestr("README.md", readme)
        archive.writestr("export-manifest.json", json.dumps(public_plan, ensure_ascii=False, indent=2))
    receipt = {
        "passed": True, "explicitAssets": len(expected), "nativeArchiveEntries": len(archive_paths),
        "exactAssetByteChecks": len(expected), "nativeGuidMetadataChecks": native_meta_equivalent,
        "rawMetadataByteEqual": raw_meta_equal, "sourceArchiveFiles": len(source_files) + 2,
        "metadataSerialization": "83 encoded Tuanjie top-level meta GUIDs are written as AssetDatabase GUIDs by Native ExportPackage; all other metadata bytes match. Source ZIP copies Native-export bytes intact.",
        "referenceMediaExcluded": True, "includeDependencies": False,
        "nativePackage": {"name": args.package.name, "bytes": args.package.stat().st_size, "sha256": sha(args.package.read_bytes())},
        "sourcePackage": {"name": destination.name, "bytes": destination.stat().st_size, "sha256": sha(destination.read_bytes())},
        "scope": "Archive payload and current files exactly match Native plan; this is not a new-project import or Player build."
    }
    args.package.with_name("delivery-verification.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
