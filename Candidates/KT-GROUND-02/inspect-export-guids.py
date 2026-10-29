"""Read-only diagnosis of Tuanjie Native-export GUID serialization differences."""
import json, re, tarfile, difflib
from pathlib import Path
root=Path("E:/WORLDCREATOR/new/Project_ANKALUTE")
delivery=Path("E:/WORLDCREATOR/XingHaiHuiLang/研究/开拓物语地表/KT-GROUND-02/06-delivery")
plan=json.loads((delivery/"CoreExportPlan_ba314e638528a8e8.json").read_text(encoding="utf-8-sig"))
lookup={}
for row in plan["paths"]:
    match=re.search(r"(?m)^guid: ([^\r\n]+)", (root/(row["path"]+".meta")).read_text(encoding="utf-8-sig"))
    if match: lookup[match[1]]=row["guid"]
for name in plan["requiredExternalPackagePaths"]:
    if name.startswith("Packages/com.unity.2d.tilemap.extras/"):
        tail=name.removeprefix("Packages/com.unity.2d.tilemap.extras/")
        path=root/"Library/PackageCache/com.unity.2d.tilemap.extras@3.1.3"/(tail+".meta")
        if path.is_file():
            match=re.search(r"(?m)^guid: ([^\r\n]+)",path.read_text(encoding="utf-8-sig"))
            if match: lookup.setdefault(match[1],match[1])
def normalize(data):
    try: text=data.decode("utf-8-sig")
    except UnicodeDecodeError: return data
    text=re.sub(r"(guid: )([^\s,}]+)",lambda m:m[1]+lookup.get(m[2],m[2]),text)
    return text.replace("\r\n","\n").encode("utf-8")
rows=[]
with tarfile.open(delivery/"GroundWorkshop-Core_ba314e638528a8e8.unitypackage","r:gz") as archive:
    for row in plan["paths"]:
        for label,member,disk in (("asset","asset",root/row["path"]),("meta","asset.meta",root/(row["path"]+".meta"))):
            before=disk.read_bytes();after=archive.extractfile(row["guid"]+"/"+member).read()
            same=before==after;normalized=normalize(before)==normalize(after)
            result={"path":row["path"],"part":label,"exact":same,"nativeGuidNormalized":normalized}
            if not normalized:
                try: result["diff"]=list(difflib.unified_diff(normalize(before).decode().splitlines(),normalize(after).decode().splitlines()))[:16]
                except UnicodeDecodeError: result["diff"]=["binary mismatch"]
            rows.append(result)
result={"checked":len(rows),"exact":sum(r["exact"] for r in rows),"nativeGuidNormalized":sum(r["nativeGuidNormalized"] for r in rows),"unmatched":[r for r in rows if not r["nativeGuidNormalized"]],"encryptedMetaGuidCount":sum(len(k)!=32 for k in lookup)}
(delivery/"native-guid-serialization-diagnostic.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(result,ensure_ascii=False,indent=2))
