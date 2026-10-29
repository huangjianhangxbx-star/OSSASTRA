/*
 * 脚本：GroundWorkshopCatalog
 * 功能：读取HTML与Unity共用的本机资源目录，以同一assetId/修订安全导入底纹、边缘族和已适配装饰。
 * 负责：相对媒体根、哈希/尺寸/选择集预检，稳定私有目录与失败精确撤回，离线图鉴定位。
 * 不负责：源游戏解析、修改旧Importer、恢复动画、复制原规则数组、地图格子或角色状态。
 * 协作：窗口消费只读目录；GroundAssetLedger拥有本次新文件；编辑器导入后只保存明确新资产。
 * 关键约束：整批预检先于创建；已有文件只读复用不覆写；静态预览/待定资源不能冒充Ready装饰。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch.Editor
{
    public sealed class GroundCatalogDocument
    {
        public string catalogId, version, htmlPath, manifestPath, directoryPath;
        public GroundCatalogEntry[] entries = Array.Empty<GroundCatalogEntry>();
        internal string allowedMediaScope;
        internal readonly Dictionary<string,string> mediaRoots = new Dictionary<string,string>(StringComparer.Ordinal);
    }

    public sealed class GroundCatalogEntry
    {
        public string assetId, revision, kind, name, displayName, evidenceLevel, compatibility;
        public string mediaRootId, mediaPath, thumbnailRootId, thumbnailPath, sha256, placementMode;
        public int width, height;
        public float pixelsPerUnit;
        public Vector2 pivot;
        public JObject raw;
    }

    public static class GroundWorkshopCatalog
    {
        private const string CatalogFormat = "ground-workshop-catalog";
        private const string SelectionFormat = "ground-workshop-selection";
        private const string ReferenceNativeRoot = "Assets/OSSASTRA/GroundResearch/LocalGenerated";
        private const long MaxJsonBytes = 32L * 1024 * 1024;
        private const long MaxImageBytes = 128L * 1024 * 1024;
        private static readonly Regex Identity = new Regex("^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$",RegexOptions.CultureInvariant);
        private static readonly Regex Sha = new Regex("^[0-9a-fA-F]{64}$",RegexOptions.CultureInvariant);
        private static readonly HashSet<string> Kinds = new HashSet<string>(new[]{"GroundDetail","EdgeMask","EdgeFamily","FlatSprite","UprightSprite","EnvironmentUnknown"},StringComparer.Ordinal);
        private static readonly HashSet<string> Compatibility = new HashSet<string>(new[]{"Ready","StaticPreviewOnly","Unsupported","Unresolved"},StringComparer.Ordinal);
        private sealed class ImportPlan
        {
            public GroundCatalogEntry entry;
            public string sourcePath, texturePath, definitionPath;
            public bool sprite;
            public RuleTile familyRule;
            public Tile familyFill;
            public GroundDecorationMode mode;
        }

        /// <summary>只读加载共用清单；根相对目录可整体搬移，所有媒体限制在包及其同级资料集合内。</summary>
        public static GroundCatalogDocument Load(string manifestPath)
        {
            string full = Path.GetFullPath(manifestPath ?? throw new ArgumentNullException(nameof(manifestPath)));
            var raw = ReadJson(full);
            RequireFormat(raw,CatalogFormat);
            if (raw.Value<bool?>("localOnly") != true) throw new InvalidOperationException("参考资源目录必须明确localOnly=true。");
            var doc = new GroundCatalogDocument
            {
                catalogId = Text(raw,"catalogId"), version = Text(raw,"version"), manifestPath=full,
                directoryPath=Path.GetDirectoryName(full)
            };
            // 一个资料包可引用其同级上轮媒体，最多上溯两级；绝对根、第三次上溯和重解析点均拒绝。
            var package = Directory.GetParent(doc.directoryPath);
            var collection = package == null ? null : package.Parent;
            if (collection == null) throw new InvalidOperationException("目录位置过浅；请把catalog.json放在资料包的目录子文件夹中。");
            doc.allowedMediaScope=collection.FullName;
            doc.htmlPath=Path.Combine(doc.directoryPath,"场景素材图鉴.html");
            var roots=raw["mediaRoots"] as JArray;
            if(roots==null || roots.Count<1 || roots.Count>16) throw new InvalidOperationException("目录必须有1–16个显式媒体根。");
            foreach(var value in roots)
            {
                var root=value as JObject ?? throw new InvalidOperationException("媒体根格式错误。");
                string id=Text(root,"rootId"); RequireIdentity(id);
                string relative=Text(root,"path"); RequireRelative(relative);
                string directory=Path.GetFullPath(Path.Combine(doc.directoryPath,relative));
                if(!Within(directory,doc.allowedMediaScope) || !Directory.Exists(directory) || doc.mediaRoots.ContainsKey(id))
                    throw new InvalidOperationException("媒体根越界、缺失或rootId重复："+id);
                RejectReparsePoints(directory,doc.allowedMediaScope);
                doc.mediaRoots.Add(id,directory);
            }
            var values=raw["entries"] as JArray;
            if(values==null || values.Count<1 || values.Count>20000) throw new InvalidOperationException("目录entries必须包含1–20000项。");
            var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries=new List<GroundCatalogEntry>();
            foreach(var value in values)
            {
                var row=value as JObject ?? throw new InvalidOperationException("资源条目格式错误。");
                var entry=new GroundCatalogEntry
                {
                    assetId=Text(row,"assetId"),revision=Text(row,"revision"),kind=Text(row,"kind"),
                    name=(string)row["name"] ?? "",displayName=(string)row["displayName"] ?? "",
                    evidenceLevel=Text(row,"evidenceLevel"),compatibility=Text(row,"compatibility"),
                    mediaRootId=Text(row,"mediaRootId"),mediaPath=Text(row,"mediaPath"),
                    thumbnailRootId=(string)row["thumbnailRootId"],thumbnailPath=(string)row["thumbnailPath"],
                    sha256=Text(row,"sha256"),placementMode=(string)row["placementMode"] ?? "Unresolved",
                    width=PositiveInt(row,"width"),height=PositiveInt(row,"height"),
                    pixelsPerUnit=row.Value<float?>("pixelsPerUnit") ?? 100f,
                    pivot=new Vector2(row["pivot"]?.Value<float?>("x") ?? .5f,row["pivot"]?.Value<float?>("y") ?? .5f),
                    raw=(JObject)row.DeepClone()
                };
                ValidateEntry(entry);
                if(!ids.Add(entry.assetId)) throw new InvalidOperationException("重复assetId（含Windows大小写冲突）："+entry.assetId);
                entries.Add(entry);
            }
            doc.entries=entries.ToArray();
            // 加载阶段验证每条根/相对路径；实际文件哈希留在导入预检，避免窗口重绘重复读取大图。
            foreach(var entry in doc.entries)
            {
                ResolveMedia(doc,entry);
                if(!string.IsNullOrEmpty(entry.thumbnailPath)) ResolveMedia(doc,entry,true);
            }
            return doc;
        }

        /// <summary>返回显式媒体根内的精确文件；不支持任意URI、绝对条目或路径穿越。</summary>
        public static string ResolveMedia(GroundCatalogDocument doc,GroundCatalogEntry entry,bool thumbnail=false)
        {
            RequireMember(doc,entry);
            string rootId=thumbnail ? entry.thumbnailRootId : entry.mediaRootId;
            string relative=thumbnail ? entry.thumbnailPath : entry.mediaPath;
            if(string.IsNullOrEmpty(rootId) && thumbnail) rootId=entry.mediaRootId;
            if(string.IsNullOrEmpty(relative)) throw new InvalidOperationException("条目缺少媒体路径："+entry.assetId);
            RequireRelative(relative);
            if(!doc.mediaRoots.TryGetValue(rootId ?? "",out string root)) throw new InvalidOperationException("未知媒体根："+rootId);
            string path=Path.GetFullPath(Path.Combine(root,relative));
            if(!Within(path,root) || !path.EndsWith(".png",StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidOperationException("媒体缺失、不是PNG或越出登记根："+entry.assetId);
            RejectReparsePoints(path,doc.allowedMediaScope);
            return path;
        }

        public static UnityEngine.Object ImportEntry(GroundCatalogDocument doc,GroundCatalogEntry entry)
        {
            RequireContext();
            var plan=Prepare(doc,entry);
            using(var ledger=new GroundAssetLedger())
            {
                var result=Import(plan,ledger);
                ledger.Commit(); return result;
            }
        }

        /// <summary>先验证整批目录身份、修订、路径、PNG和已有输出，再创建任何资产；失败只撤回本批新文件。</summary>
        public static UnityEngine.Object[] ImportSelection(GroundCatalogDocument doc,string selectionPath)
        {
            RequireContext();
            if(doc==null) throw new ArgumentNullException(nameof(doc));
            var raw=ReadJson(selectionPath); RequireFormat(raw,SelectionFormat);
            if(Text(raw,"catalogId")!=doc.catalogId || Text(raw,"version")!=doc.version)
                throw new InvalidOperationException("选择集目录或版本不符；请从当前图鉴重新导出。");
            var rows=raw["entries"] as JArray;
            if(rows==null || rows.Count<1 || rows.Count>2000) throw new InvalidOperationException("选择集必须包含1–2000项。");
            var selected=new List<GroundCatalogEntry>(); var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var value in rows)
            {
                var row=value as JObject ?? throw new InvalidOperationException("选择集条目格式错误。");
                string id=Text(row,"assetId"),revision=Text(row,"revision");
                var entry=doc.entries.FirstOrDefault(e=>e.assetId==id);
                if(entry==null || entry.revision!=revision || !seen.Add(id))
                    throw new InvalidOperationException("选择集有未知、重复或过期条目："+id);
                selected.Add(entry);
            }
            var plans=selected.Select(entry=>Prepare(doc,entry)).ToArray();
            using(var ledger=new GroundAssetLedger())
            {
                var result=plans.Select(plan=>Import(plan,ledger)).ToArray();
                ledger.Commit(); return result;
            }
        }

        public static void OpenEntry(GroundCatalogDocument doc,GroundCatalogEntry entry)
        {
            RequireMember(doc,entry);
            if(!File.Exists(doc.htmlPath)) throw new FileNotFoundException("同目录缺少离线场景素材图鉴.html。",doc.htmlPath);
            Application.OpenURL(new Uri(doc.htmlPath).AbsoluteUri+"#"+Uri.EscapeDataString(entry.assetId));
        }

        public static void RevealEntry(GroundCatalogDocument doc,GroundCatalogEntry entry) => EditorUtility.RevealInFinder(ResolveMedia(doc,entry));

        private static ImportPlan Prepare(GroundCatalogDocument doc,GroundCatalogEntry entry)
        {
            RequireMember(doc,entry); ValidateEntry(entry);
            if(entry.compatibility!="Ready") throw new InvalidOperationException("此条目仅供浏览，兼容状态为"+entry.compatibility+"；未导入或假定动画可用："+entry.assetId);
            if(entry.kind=="EnvironmentUnknown") throw new InvalidOperationException("未判定环境资源不能直接注册为装饰。");
            string source=ResolveMedia(doc,entry);
            var info=new FileInfo(source);
            if(info.Length<24 || info.Length>MaxImageBytes || Hash(source)!=entry.sha256.ToLowerInvariant())
                throw new InvalidOperationException("源PNG大小或SHA-256不符："+entry.assetId);
            ReadPngSize(source,out int width,out int height);
            if(width!=entry.width || height!=entry.height) throw new InvalidOperationException("实际PNG尺寸与清单不符："+entry.assetId);
            string signature=HashText(doc.catalogId+"\n"+doc.version+"\n"+entry.revision+"\n"+entry.kind+"\n"+
                entry.pixelsPerUnit.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n"+
                entry.pivot.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n"+
                entry.pivot.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\n"+entry.placementMode);
            string folder=GroundWorkshopOwnership.Root+"/LocalReference/"+entry.assetId.ToLowerInvariant()+"/"+signature.Substring(0,24);
            var plan=new ImportPlan {entry=entry,sourcePath=source,texturePath=folder+"/Source.png",definitionPath=folder+"/Definition.asset",sprite=entry.kind!="GroundDetail"};
            if(entry.kind=="EdgeFamily")
            {
                string rule=(string)entry.raw["ruleTileAssetPath"],fill=(string)entry.raw["fullTileAssetPath"];
                if(!AllowedNativeFamilyPath(rule) || !AllowedNativeFamilyPath(fill)) throw new InvalidOperationException("边缘族引用不在自有Originals或本机LocalGenerated白名单。");
                plan.familyRule=AssetDatabase.LoadAssetAtPath<RuleTile>(rule);
                plan.familyFill=AssetDatabase.LoadAssetAtPath<Tile>(fill);
                if(plan.familyRule==null || plan.familyRule.m_DefaultSprite==null || plan.familyRule.m_TilingRules==null || plan.familyRule.m_TilingRules.Count==0 ||
                    plan.familyRule.m_TilingRules.Any(r=>r.m_Sprites==null || r.m_Sprites.Length==0 || r.m_Sprites.Any(s=>s==null)) || plan.familyFill==null || plan.familyFill.sprite==null)
                    throw new InvalidOperationException("本机边缘族缺RuleTile、填充Tile或Sprite依赖；未重新生成源规则。");
                CheckExistingDefinition<GroundEdgeFamily>(plan.definitionPath,existing=>existing.assetId==entry.assetId && existing.revision==entry.revision &&
                    existing.paintTemplate==plan.familyRule && existing.fillTemplate==plan.familyFill && existing.validated);
                return plan;
            }
            if(entry.kind=="FlatSprite" || entry.kind=="UprightSprite")
            {
                if(!Enum.TryParse(entry.placementMode,false,out plan.mode) || !Enum.IsDefined(typeof(GroundDecorationMode),plan.mode)) throw new InvalidOperationException("装饰摆放模式尚未明确："+entry.assetId);
                CheckExistingDefinition<GroundDecorationDefinition>(plan.definitionPath,existing=>existing.assetId==entry.assetId && existing.revision==entry.revision && existing.mode==plan.mode &&
                    existing.sprite!=null && AssetDatabase.GetAssetPath(existing.sprite)==plan.texturePath && existing.footpointNormalized==entry.pivot);
            }
            else if(entry.kind!="GroundDetail" && entry.kind!="EdgeMask") throw new InvalidOperationException("当前导入器不支持类型："+entry.kind);
            CheckExistingImage(plan);
            return plan;
        }

        private static UnityEngine.Object Import(ImportPlan plan,GroundAssetLedger ledger)
        {
            var entry=plan.entry;
            if(entry.kind=="EdgeFamily")
            {
                var existing=AssetDatabase.LoadAssetAtPath<GroundEdgeFamily>(plan.definitionPath);
                if(existing!=null) return existing;
                var family=ScriptableObject.CreateInstance<GroundEdgeFamily>();
                family.assetId=entry.assetId;family.revision=entry.revision;family.displayName=entry.displayName;
                family.evidence=entry.evidenceLevel+"："+entry.name+"；本机原生规则引用，未复制原规则至代码。";
                family.paintTemplate=plan.familyRule;family.fillTemplate=plan.familyFill;family.preview=plan.familyFill.sprite;family.validated=true;
                return ledger.Create(family,plan.definitionPath);
            }
            if(!File.Exists(plan.texturePath))
            {
                ledger.Reserve(plan.texturePath);File.Copy(plan.sourcePath,plan.texturePath,false);
                AssetDatabase.ImportAsset(plan.texturePath,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(plan.texturePath) as TextureImporter;
                if(importer==null) throw new InvalidOperationException("PNG没有TextureImporter："+plan.texturePath);
                importer.textureType=plan.sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.sRGBTexture=entry.kind!="EdgeMask";importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=plan.sprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=plan.sprite;
                importer.isReadable=false;importer.maxTextureSize=Mathf.NextPowerOfTwo(Mathf.Max(32,Mathf.Max(entry.width,entry.height)));
                if(plan.sprite)
                {
                    importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=entry.pixelsPerUnit;importer.spritePivot=entry.pivot;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }
            CheckExistingImage(plan);
            if(entry.kind=="GroundDetail") return AssetDatabase.LoadAssetAtPath<Texture2D>(plan.texturePath);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(plan.texturePath);
            if(sprite==null) throw new InvalidOperationException("导入后缺少Sprite："+plan.texturePath);
            if(entry.kind=="EdgeMask") return sprite;
            var definition=AssetDatabase.LoadAssetAtPath<GroundDecorationDefinition>(plan.definitionPath);
            if(definition!=null) return definition;
            definition=ScriptableObject.CreateInstance<GroundDecorationDefinition>();
            definition.assetId=entry.assetId;definition.revision=entry.revision;definition.displayName=entry.displayName;
            definition.evidence=entry.evidenceLevel+"："+entry.name+"；"+((string)entry.raw["notes"] ?? "来源字段见共用图鉴。");
            definition.sprite=sprite;definition.mode=plan.mode;definition.footpointNormalized=entry.pivot;
            definition.scaleRange=Vector2.one;definition.allowMirror=false;
            return ledger.Create(definition,plan.definitionPath);
        }

        private static void CheckExistingImage(ImportPlan plan)
        {
            if(!File.Exists(plan.texturePath))
            {
                if(File.Exists(plan.texturePath+".meta") || AssetDatabase.LoadMainAssetAtPath(plan.texturePath)!=null)
                    throw new InvalidOperationException("已有不完整导入现场，保留不覆盖："+plan.texturePath);
                return;
            }
            if(Hash(plan.texturePath)!=plan.entry.sha256.ToLowerInvariant()) throw new InvalidOperationException("已有导入文件被修改，保留并拒绝覆盖："+plan.texturePath);
            var importer=AssetImporter.GetAtPath(plan.texturePath) as TextureImporter;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(plan.texturePath);
            if(importer==null || texture==null || texture.width!=plan.entry.width || texture.height!=plan.entry.height ||
                importer.textureType!=(plan.sprite ? TextureImporterType.Sprite : TextureImporterType.Default) ||
                importer.mipmapEnabled || importer.npotScale!=TextureImporterNPOTScale.None || importer.filterMode!=FilterMode.Bilinear ||
                importer.textureCompression!=TextureImporterCompression.Uncompressed || importer.sRGBTexture!=(plan.entry.kind!="EdgeMask") ||
                importer.wrapMode!=(plan.sprite ? TextureWrapMode.Clamp : TextureWrapMode.Repeat))
                throw new InvalidOperationException("已有导入配置不同，保留并拒绝自动重导："+plan.texturePath);
            if(plan.sprite)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(plan.texturePath);
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                if(sprite==null || importer.spriteImportMode!=SpriteImportMode.Single || !Mathf.Approximately(importer.spritePixelsPerUnit,plan.entry.pixelsPerUnit) ||
                    settings.spriteAlignment!=(int)SpriteAlignment.Custom || settings.spriteMeshType!=SpriteMeshType.FullRect ||
                    Vector2.Distance(importer.spritePivot,plan.entry.pivot)>.0001f)
                    throw new InvalidOperationException("已有Sprite的pivot/PPU/mesh配置不符，未修改用户导入设置："+plan.texturePath);
            }
        }

        private static void CheckExistingDefinition<T>(string path,Func<T,bool> valid) where T:UnityEngine.Object
        {
            var any=AssetDatabase.LoadMainAssetAtPath(path);
            if(any==null)
            {
                if(File.Exists(path) || File.Exists(path+".meta")) throw new InvalidOperationException("已有无法加载的定义文件，保留现场："+path);
                return;
            }
            if(!(any is T typed) || !valid(typed)) throw new InvalidOperationException("已有定义与当前目录修订不符，保留并拒绝覆写："+path);
        }

        private static bool AllowedNativeFamilyPath(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".asset",StringComparison.Ordinal) &&
            (GroundWorkshopOwnership.IsUnder(path,ReferenceNativeRoot) || GroundWorkshopOwnership.IsUnder(path,GroundWorkshopOwnership.Root+"/Originals"));
        private static void RequireContext() { if(!GroundWorkshopOwnership.ContextReady(out string reason)) throw new InvalidOperationException(reason); }
        private static void RequireMember(GroundCatalogDocument doc,GroundCatalogEntry entry)
        {
            if(doc==null || entry==null || doc.entries==null || !doc.entries.Any(value=>ReferenceEquals(value,entry)))
                throw new InvalidOperationException("条目不属于当前已加载目录；请重新选择素材。");
        }
        private static void ValidateEntry(GroundCatalogEntry entry)
        {
            RequireIdentity(entry.assetId);
            if(string.IsNullOrWhiteSpace(entry.revision) || entry.revision.Length>128 || !Kinds.Contains(entry.kind) ||
                !Compatibility.Contains(entry.compatibility) || !Sha.IsMatch(entry.sha256 ?? "") ||
                entry.width>16384 || entry.height>16384 || (long)entry.width*entry.height>32L*1024*1024 ||
                !GroundWorkshopOwnership.IsFinite(entry.pixelsPerUnit) || entry.pixelsPerUnit<=0 ||
                !GroundWorkshopOwnership.IsFinite(entry.pivot.x) || !GroundWorkshopOwnership.IsFinite(entry.pivot.y) ||
                entry.pivot.x<0 || entry.pivot.x>1 || entry.pivot.y<0 || entry.pivot.y>1)
                throw new InvalidOperationException("条目版本、类型、哈希、画布或pivot/PPU无效："+entry.assetId);
        }
        private static void RequireIdentity(string value)
        { if(string.IsNullOrEmpty(value) || !Identity.IsMatch(value) || value.Contains("..")) throw new InvalidOperationException("非法资源/root身份："+value); }
        private static void RequireRelative(string path)
        {
            if(string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') || path.IndexOf('\0')>=0)
                throw new InvalidOperationException("只接受明确的相对路径，不接受URI、绝对路径或反斜线。");
        }
        private static bool Within(string path,string root)
        {
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix,StringComparison.OrdinalIgnoreCase);
        }
        private static void RejectReparsePoints(string path,string stopRoot)
        {
            string cursor=Path.GetFullPath(path),stop=Path.GetFullPath(stopRoot).TrimEnd(Path.DirectorySeparatorChar);
            while(!string.Equals(cursor.TrimEnd(Path.DirectorySeparatorChar),stop,StringComparison.OrdinalIgnoreCase))
            {
                if((File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("媒体路径包含链接/重解析点，未跟随："+cursor);
                string parent=Path.GetDirectoryName(cursor);
                if(string.IsNullOrEmpty(parent) || parent==cursor) throw new InvalidOperationException("媒体根检查未收敛。");
                cursor=parent;
            }
        }
        private static JObject ReadJson(string path)
        {
            string full=Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            if(!File.Exists(full) || new FileInfo(full).Length>MaxJsonBytes) throw new InvalidOperationException("JSON文件缺失或大于32MiB："+full);
            using(var stream=File.OpenText(full)) using(var reader=new JsonTextReader(stream){MaxDepth=32,DateParseHandling=DateParseHandling.None})
                return JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
        }
        private static void RequireFormat(JObject raw,string format)
        { if((string)raw["format"]!=format || raw.Value<int?>("schemaVersion")!=1) throw new InvalidOperationException("格式或schemaVersion不兼容，未导入。"); }
        private static string Text(JObject raw,string key)
        {
            var value=raw[key];
            if(value==null || value.Type!=JTokenType.String || string.IsNullOrWhiteSpace((string)value) || ((string)value).Length>2048)
                throw new InvalidOperationException("缺失或非法文本字段："+key);
            return (string)value;
        }
        private static int PositiveInt(JObject raw,string key)
        { if(raw[key]?.Type!=JTokenType.Integer || !int.TryParse(raw[key].ToString(),out int value) || value<=0) throw new InvalidOperationException("非法正整数："+key);return value; }
        private static string Hash(string path)
        { using(var stream=File.OpenRead(path)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
        private static string HashText(string value)
        { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
        private static void ReadPngSize(string path,out int width,out int height)
        {
            var bytes=new byte[24];using(var stream=File.OpenRead(path)) if(stream.Read(bytes,0,24)!=24) throw new InvalidOperationException("PNG文件头不足24字节。");
            byte[] signature={137,80,78,71,13,10,26,10};
            if(!bytes.Take(8).SequenceEqual(signature) || bytes[12]!=73 || bytes[13]!=72 || bytes[14]!=68 || bytes[15]!=82) throw new InvalidOperationException("媒体不是合法PNG/IHDR。");
            width=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19];height=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];
        }
    }
}
#endif
