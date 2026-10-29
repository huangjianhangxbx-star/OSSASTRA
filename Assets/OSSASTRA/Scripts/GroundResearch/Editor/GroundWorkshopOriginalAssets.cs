/*
 * 脚本：GroundWorkshopOriginalAssets
 * 功能：首次创建独立原创技术样本：两套原生八邻域边族、四底纹配方、三装饰和三层主题。
 * 负责：在自有目录生成PNG并以Editor API导入、创建和逐项验证定义；已有文件只校验复用。
 * 不负责：正式美术质量、材质装配、地图格布局、场景保存、角色或原作素材读取。
 * 协作：工坊显式调用EnsureSamples取得GroundTheme，再由地图作者事务消费配方与装饰。
 * 关键约束：规则与像素由独立数学构造产生；不覆盖用户改图，不全局SaveAssets，不静默修复冲突。
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopOriginalAssets
    {
        public const string Root = "Assets/OSSASTRA/GroundWorkshop";
        private const string ThemePath = Root + "/Palettes/OriginalTechnicalTheme.asset";
        private const string Evidence = "ORIGINAL_GENERATED_TECHNICAL：本项目独立数学生成的原创技术样本；供接入、拓扑与替换教程使用，不代表正式美术。";
        private const int SliceSize = 128;
        private const int AtlasSize = 2048;
        private const float PixelsPerCell = 99f;
        private static readonly Vector3Int[] Neighbors =
        {
            new Vector3Int(-1,1,0), new Vector3Int(0,1,0), new Vector3Int(1,1,0),
            new Vector3Int(-1,0,0), new Vector3Int(1,0,0),
            new Vector3Int(-1,-1,0), new Vector3Int(0,-1,0), new Vector3Int(1,-1,0)
        };
        private static readonly Vector2[] GrassTips =
        {
            new Vector2(.17f,.57f), new Vector2(.32f,.81f), new Vector2(.49f,.91f),
            new Vector2(.69f,.76f), new Vector2(.84f,.52f)
        };

        /// <summary>显式创建缺失的初始样本；已存在完整主题时只验证并返回，不覆盖作者改图。</summary>
        public static GroundTheme EnsureSamples()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("原创技术样本只能在Edit模式显式创建。");
            PreflightPaths();
            var existing = Existing<GroundTheme>(ThemePath);
            if (existing != null) { ValidateComplete(existing); return existing; }

            Folder(Root + "/Originals/Edges");
            Folder(Root + "/Originals/Textures");
            Folder(Root + "/Originals/Recipes");
            Folder(Root + "/Originals/Decorations");
            Folder(Root + "/Palettes");
            var rounded = EnsureFamily("Rounded", false, "圆弧柔和边缘");
            var chamfered = EnsureFamily("Chamfered", true, "直线倒角边缘");
            var grass = EnsureRecipe("Grass", "技术草地", new Color(.27f,.35f,.22f), rounded, 1);
            var moss = EnsureRecipe("Moss", "技术苔地", new Color(.20f,.28f,.23f), rounded, 2);
            var road = EnsureRecipe("Road", "技术土石道路", new Color(.46f,.39f,.29f), chamfered, 3);
            EnsureRecipe("Alternate", "备用暖灰底纹", new Color(.35f,.32f,.28f), rounded, 4);
            var tuft = EnsureDecoration("GrassTuft", "原创技术草丛", 0, GroundDecorationMode.UprightYaw);
            var flower = EnsureDecoration("SmallFlower", "原创技术小花", 1, GroundDecorationMode.UprightYaw);
            var leaves = EnsureDecoration("FallenLeaves", "原创技术落叶", 2, GroundDecorationMode.Flat);

            // Theme最后落盘：中断发生在前面时，下一次只复用已完成的单项，不覆盖或删除残留。
            var theme = Create(ThemePath, () =>
            {
                var value = ScriptableObject.CreateInstance<GroundTheme>();
                value.assetId = "original.theme.technical";
                value.displayName = "原创技术样本 · 三层地表";
                value.evidence = Evidence;
                value.layers = new[]
                {
                    new GroundThemeLayer { slotId="base", displayName="基层草地", recipe=grass, purpose=GroundLayerPurpose.Base, locked=true },
                    new GroundThemeLayer { slotId="overlay", displayName="覆盖苔地", recipe=moss, purpose=GroundLayerPurpose.Overlay },
                    new GroundThemeLayer { slotId="road", displayName="道路", recipe=road, purpose=GroundLayerPurpose.Road }
                };
                value.decorations = new[] { tuft, flower, leaves };
                value.densityPer100 = 4f;
                value.minimumSpacing = .7f;
                value.roadExclusionCells = 1;
                value.seed = 123;
                return value;
            });
            ValidateComplete(theme);
            return theme;
        }

        private static void PreflightPaths()
        {
            RequireType<GroundTheme>(ThemePath);
            foreach (string name in new[] { "Rounded", "Chamfered" })
            {
                string path = EdgePath(name);
                RequireType<GroundEdgeFamily>(path + ".asset");
                RequireType<RuleTile>(path + "_Rule.asset");
                RequireType<Tile>(path + "_Fill.asset");
                RequireTexture(path + ".png", true, false, Vector2.zero);
            }
            foreach (string name in new[] { "Grass", "Moss", "Road", "Alternate" })
            {
                RequireType<GroundRecipe>(RecipePath(name));
                RequireTexture(DetailPath(name), false, false, Vector2.zero);
            }
            for (int i = 0; i < 3; i++)
            {
                string name = new[] { "GrassTuft", "SmallFlower", "FallenLeaves" }[i];
                RequireType<GroundDecorationDefinition>(DecorationPath(name) + ".asset");
                RequireTexture(DecorationPath(name) + ".png", false, true, Footpoint(i));
            }
        }

        private static string EdgePath(string name) { return Root + "/Originals/Edges/" + name; }
        private static string DetailPath(string name) { return Root + "/Originals/Textures/" + name + ".png"; }
        private static string RecipePath(string name) { return Root + "/Originals/Recipes/" + name + ".asset"; }
        private static string DecorationPath(string name) { return Root + "/Originals/Decorations/" + name; }
        private static Vector2 Footpoint(int kind) { return kind == 2 ? new Vector2(.5f,.5f) : new Vector2(.5f,.0625f); }
        private static void Require(bool valid, string path, string reason)
        {
            if (!valid) throw new InvalidOperationException("保留已有原创样本，停止自动创建：" + path + "；" + reason);
        }
        private static string Absolute(string path)
        {
            Require(path.StartsWith(Root + "/", StringComparison.Ordinal), path, "超出工厂自有路径");
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Require(!File.Exists(path), path, "文件占用了文件夹路径");
            int split = path.LastIndexOf('/');
            Require(split > 0, path, "无有效父目录");
            Folder(path.Substring(0, split));
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1))), path, "无法建立目录");
        }
        private static void RequireType<T>(string path) where T : UnityEngine.Object
        {
            if (!File.Exists(Absolute(path)) && AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                Require(!File.Exists(Absolute(path)+".meta"),path,"存在孤立Meta，需人工确认，不重建GUID");
                return;
            }
            Require(AssetDatabase.LoadMainAssetAtPath(path) is T, path, "同名文件不是预期" + typeof(T).Name + "，不覆盖");
        }
        private static T Existing<T>(string path) where T : UnityEngine.Object
        {
            RequireType<T>(path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
        private static T Create<T>(string path, Func<T> build) where T : UnityEngine.Object
        {
            T old = Existing<T>(path);
            if (old != null) return old;
            Require(!File.Exists(Absolute(path)), path, "目标文件刚出现，不覆盖");
            T value = build();
            value.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(value, path);
            EditorUtility.SetDirty(value);
            AssetDatabase.SaveAssetIfDirty(value);
            return value;
        }

        private static GroundEdgeFamily EnsureFamily(string name, bool chamfered, string label)
        {
            string path = EdgePath(name);
            var old = Existing<GroundEdgeFamily>(path + ".asset");
            if (old != null) { ValidateFamily(old, name); return old; }
            if (!File.Exists(Absolute(path + ".png")))
            {
                var pixels = new Color32[AtlasSize * AtlasSize];
                for (int mask = 0; mask < 256; mask++)
                {
                    int startX = (mask % 16) * SliceSize, startY = (mask / 16) * SliceSize;
                    for (int y = 0; y < SliceSize; y++) for (int x = 0; x < SliceSize; x++)
                    {
                        float alpha = MaskAlpha(mask, (x + .5f - 64f) / PixelsPerCell, (y + .5f - 64f) / PixelsPerCell, chamfered);
                        pixels[(startY + y) * AtlasSize + startX + x] = new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha * 255f));
                    }
                }
                WritePng(path + ".png", AtlasSize, AtlasSize, pixels, true);
                ConfigureMask(path + ".png");
            }
            var sprites = AtlasSprites(path + ".png");
            var rule = Create(path + "_Rule.asset", () =>
            {
                var value = ScriptableObject.CreateInstance<RuleTile>();
                value.m_DefaultSprite = sprites[255];
                value.m_DefaultColliderType = Tile.ColliderType.None;
                value.m_DefaultGameObject = null;
                value.m_TilingRules = new List<RuleTile.TilingRule>();
                // 精确八邻域全覆盖：没有通配规则、随机旋转或原作邻接表，顺序不影响匹配唯一性。
                for (int mask = 0; mask < 256; mask++) value.m_TilingRules.Add(new RuleTile.TilingRule
                {
                    m_Id = mask, m_Sprites = new[] { sprites[mask] },
                    m_NeighborPositions = new List<Vector3Int>(Neighbors),
                    m_Neighbors = Enumerable.Range(0,8).Select(bit => (mask & (1 << bit)) != 0 ? 1 : 2).ToList(),
                    m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                    m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                    m_ColliderType = Tile.ColliderType.None
                });
                value.UpdateNeighborPositions();
                return value;
            });
            var fill = Create(path + "_Fill.asset", () =>
            {
                var value = ScriptableObject.CreateInstance<Tile>();
                value.sprite = sprites[255]; value.color = Color.white; value.colliderType = Tile.ColliderType.None;
                return value;
            });
            var family = Create(path + ".asset", () =>
            {
                var value = ScriptableObject.CreateInstance<GroundEdgeFamily>();
                value.assetId = "original.edge." + name.ToLowerInvariant();
                value.displayName = "原创技术 · " + label;
                value.evidence = Evidence + " 独立8邻域256精确规则；白色RGB与Alpha轮廓，128切片/PPU99。";
                value.paintTemplate = rule; value.fillTemplate = fill; value.preview = sprites[0]; value.validated = true;
                return value;
            });
            ValidateFamily(family, name);
            return family;
        }

        // 逻辑格边长为1；128/99的Sprite外沿仅用于邻格连续处重叠，轮廓仍按±0.5逻辑边求值。
        private static float MaskAlpha(int mask, float x, float y, bool chamfered)
        {
            const float radius = .24f;
            float distance = 1f;
            if ((mask & 16) == 0) distance = Mathf.Min(distance, .5f - x);
            if ((mask & 8) == 0) distance = Mathf.Min(distance, .5f + x);
            if ((mask & 2) == 0) distance = Mathf.Min(distance, .5f - y);
            if ((mask & 64) == 0) distance = Mathf.Min(distance, .5f + y);
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = (corner & 1) != 0, top = (corner & 2) != 0;
                float u = right ? x : -x, v = top ? y : -y;
                if (u <= .5f - radius || v <= .5f - radius) continue;
                int horizontal = right ? 16 : 8, vertical = top ? 2 : 64;
                int diagonal = top ? (right ? 4 : 1) : (right ? 128 : 32);
                bool side = (mask & horizontal) != 0, above = (mask & vertical) != 0;
                if (!side && !above)
                {
                    float a = u - (.5f - radius), b = v - (.5f - radius);
                    distance = Mathf.Min(distance, chamfered ? 1f - radius - u - v : radius - Mathf.Sqrt(a*a + b*b));
                }
                else if (side && above && (mask & diagonal) == 0)
                {
                    float a = u - .5f, b = v - .5f;
                    distance = Mathf.Min(distance, chamfered ? Mathf.Abs(a) + Mathf.Abs(b) - radius : Mathf.Sqrt(a*a + b*b) - radius);
                }
            }
            return Mathf.Clamp01(distance * PixelsPerCell / 1.2f + .5f);
        }

        private static GroundRecipe EnsureRecipe(string name, string label, Color baseColor, GroundEdgeFamily family, int seed)
        {
            string texturePath = DetailPath(name);
            if (!File.Exists(Absolute(texturePath)))
            {
                const int size = 256;
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    // 整数频率周期波保证Repeat接缝连续；只提供克制的技术底纹，不仿制来源照片。
                    float u = x * Mathf.PI * 2f / size, v = y * Mathf.PI * 2f / size;
                    float variation = .045f * Mathf.Sin(u*3f + Mathf.Sin(v*2f) + seed) + .025f * Mathf.Cos(v*7f-u*5f+seed*.7f) + .014f * Mathf.Sin(u*23f+v*19f+seed);
                    pixels[y * size + x] = new Color(baseColor.r + variation, baseColor.g + variation*.85f, baseColor.b + variation*.7f, 1f);
                }
                WritePng(texturePath,size,size,pixels,false);
                ConfigureDetail(texturePath);
            }
            var recipe = Create(RecipePath(name), () =>
            {
                var value = ScriptableObject.CreateInstance<GroundRecipe>();
                value.assetId = "original.recipe." + name.ToLowerInvariant();
                value.detailAssetId = "original.detail." + name.ToLowerInvariant();
                value.displayName = "原创技术 · " + label; value.evidence = Evidence;
                value.detailTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath); value.edgeFamily = family;
                value.worldRepeatPeriod = 16f; value.cutoff = .5f;
                return value;
            });
            ValidateRecipe(recipe, RecipePath(name));
            return recipe;
        }

        private static GroundDecorationDefinition EnsureDecoration(string name, string label, int kind, GroundDecorationMode mode)
        {
            string path = DecorationPath(name);
            if (!File.Exists(Absolute(path + ".png")))
            {
                var pixels = new Color32[SliceSize * SliceSize];
                for (int y = 0; y < SliceSize; y++) for (int x = 0; x < SliceSize; x++)
                    pixels[y * SliceSize + x] = DecorationPixel(kind, (x + .5f)/SliceSize, (y + .5f)/SliceSize);
                WritePng(path + ".png",SliceSize,SliceSize,pixels,false);
                ConfigureDecoration(path + ".png", Footpoint(kind));
            }
            var result = Create(path + ".asset", () =>
            {
                var value = ScriptableObject.CreateInstance<GroundDecorationDefinition>();
                value.assetId = "original.decoration." + name.ToLowerInvariant();
                value.displayName = label; value.evidence = Evidence;
                value.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
                value.mode = mode; value.footpointNormalized = Footpoint(kind);
                value.scaleRange = new Vector2(.8f,1.2f); value.groundOffset = .03f; value.allowMirror = true;
                return value;
            });
            ValidateDecoration(result,path + ".asset");
            return result;
        }

        private static Color DecorationPixel(int kind, float x, float y)
        {
            Color result = Color.clear;
            if (kind == 0)
            {
                for (int i = 0; i < GrassTips.Length; i++)
                {
                    float d = Triangle(new Vector2(x,y),new Vector2(.42f+i*.017f,.0625f),new Vector2(.53f+i*.017f,.0625f),GrassTips[i]);
                    Composite(ref result,new Color(.17f+i*.018f,.30f+i*.019f,.13f),d);
                }
            }
            else if (kind == 1)
            {
                Composite(ref result,new Color(.21f,.38f,.17f),.018f - SegmentDistance(new Vector2(x,y),new Vector2(.5f,.0625f),new Vector2(.5f,.65f)));
                Composite(ref result,new Color(.25f,.42f,.19f),Ellipse(x,y,.39f,.30f,.14f,.038f,-.45f));
                Composite(ref result,new Color(.29f,.45f,.20f),Ellipse(x,y,.59f,.39f,.13f,.036f,.5f));
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2f / 5f;
                    float dx = x - (.5f + .09f*Mathf.Cos(angle)), dy = y - (.68f + .09f*Mathf.Sin(angle));
                    Composite(ref result,new Color(.88f,.85f,.70f),.073f - Mathf.Sqrt(dx*dx+dy*dy));
                }
                float cx=x-.5f, cy=y-.68f;
                Composite(ref result,new Color(.79f,.58f,.22f),.052f - Mathf.Sqrt(cx*cx+cy*cy));
            }
            else
            {
                Composite(ref result,new Color(.51f,.35f,.16f),Ellipse(x,y,.34f,.44f,.23f,.082f,-.55f));
                Composite(ref result,new Color(.68f,.49f,.23f),Ellipse(x,y,.64f,.65f,.23f,.09f,.48f));
                Composite(ref result,new Color(.57f,.40f,.21f),Ellipse(x,y,.63f,.29f,.19f,.073f,2.3f));
            }
            return result;
        }
        private static float Ellipse(float x,float y,float cx,float cy,float rx,float ry,float angle)
        {
            float dx=x-cx,dy=y-cy,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            float u=(dx*c+dy*s)/rx,v=(-dx*s+dy*c)/ry;
            return (1f-Mathf.Sqrt(u*u+v*v))*Mathf.Min(rx,ry);
        }
        private static float Triangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {
            return Mathf.Min(EdgeDistance(p,a,b),Mathf.Min(EdgeDistance(p,b,c),EdgeDistance(p,c,a)));
        }
        private static float EdgeDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 edge=b-a,delta=p-a;
            return (edge.x*delta.y-edge.y*delta.x)/Mathf.Max(edge.magnitude,.0001f);
        }
        private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {
            Vector2 d=b-a;
            return (p-(a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude))).magnitude;
        }
        private static void Composite(ref Color destination, Color color, float distance)
        {
            float alpha=Mathf.Clamp01(distance*SliceSize+.5f),output=alpha+destination.a*(1f-alpha);
            if(output<=0f)return;
            destination=new Color((color.r*alpha+destination.r*destination.a*(1f-alpha))/output,(color.g*alpha+destination.g*destination.a*(1f-alpha))/output,(color.b*alpha+destination.b*destination.a*(1f-alpha))/output,output);
        }

        private static void WritePng(string path,int width,int height,Color32[] pixels,bool linear)
        {
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false,linear) { hideFlags=HideFlags.HideAndDontSave };
            try
            {
                texture.SetPixels32(pixels); texture.Apply(false,false);
                byte[] bytes=texture.EncodeToPNG();
                // CreateNew把“保留用户文件”落实为文件系统条件，不存在先检查后直接覆盖的写法。
                using(var stream=new FileStream(Absolute(path),FileMode.CreateNew,FileAccess.Write,FileShare.Read)) stream.Write(bytes,0,bytes.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        private static TextureImporter ConfigureCommon(string path,bool sprite,bool linear)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer!=null,path,"缺少新图Importer");
            importer.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
            importer.sRGBTexture=!linear; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.crunchedCompression=false; importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=2048; importer.isReadable=true; importer.filterMode=FilterMode.Bilinear;
            importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=sprite;
            return importer;
        }
        private static void ConfigureMask(string path)
        {
            var importer=ConfigureCommon(path,true,true);
            importer.spriteImportMode=SpriteImportMode.Multiple; importer.spritePixelsPerUnit=PixelsPerCell;
            importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=false;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.spritesheet=Enumerable.Range(0,256).Select(mask=>new SpriteMetaData
            {
                name="mask_"+mask.ToString("D3"), rect=new Rect((mask%16)*SliceSize,(mask/16)*SliceSize,SliceSize,SliceSize),
                alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(.5f,.5f)
            }).ToArray();
            importer.SaveAndReimport();
        }
        private static void ConfigureDetail(string path)
        {
            var importer=ConfigureCommon(path,false,false); importer.wrapMode=TextureWrapMode.Repeat; importer.mipmapEnabled=true;
            importer.SaveAndReimport();
        }
        private static void ConfigureDecoration(string path,Vector2 pivot)
        {
            var importer=ConfigureCommon(path,true,false); importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=128f;
            importer.spritePivot=pivot; importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=false;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        private static void RequireTexture(string path,bool atlas,bool sprite,Vector2 pivot)
        {
            if(!File.Exists(Absolute(path)))
            {
                Require(!File.Exists(Absolute(path)+".meta"),path,"存在孤立Meta，不重新生成图像身份");
                return;
            }
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(importer!=null&&texture!=null,path,"已有图未正确导入，不自动重编码或改Importer");
            if(atlas) { AtlasSprites(path); return; }
            Require(importer.isReadable&&importer.sRGBTexture,path,"已有图需要可读sRGB导入");
            if(sprite)
            {
                var value=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Single&&value!=null,path,"已有装饰须为独立Sprite");
                Require((new Vector2(value.pivot.x/value.rect.width,value.pivot.y/value.rect.height)-pivot).sqrMagnitude<.0001f,path,"已有图脚点与样本定义不匹配");
            }
            else Require(importer.textureType==TextureImporterType.Default&&importer.wrapMode==TextureWrapMode.Repeat,path,"已有底纹须为Repeat纹理");
        }
        private static Sprite[] AtlasSprites(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(importer!=null&&texture!=null&&texture.width==AtlasSize&&texture.height==AtlasSize,path,"边族图集须为2048平方");
            Require(importer.spriteImportMode==SpriteImportMode.Multiple&&!importer.sRGBTexture&&!importer.mipmapEnabled&&importer.wrapMode==TextureWrapMode.Clamp&&importer.isReadable,path,"遮罩导入须Multiple/Linear/Clamp/可读/无mip");
            var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s=>s.name,StringComparer.Ordinal).ToArray();
            Require(sprites.Length==256,path,"边族应有256个自有切片");
            for(int mask=0;mask<256;mask++)Require(sprites[mask].name=="mask_"+mask.ToString("D3")&&sprites[mask].rect.size==new Vector2(128,128)&&Mathf.Approximately(sprites[mask].pixelsPerUnit,PixelsPerCell)&&(sprites[mask].pivot-new Vector2(64,64)).sqrMagnitude<.001f,path,"切片名称/尺寸/PPU/中心不符");
            return sprites;
        }
        private static void ValidateFamily(GroundEdgeFamily family,string name)
        {
            string path=EdgePath(name);
            Require(family!=null,path,"缺少边族定义，不修补已有Theme");
            var sprites=AtlasSprites(path+".png");
            var rule=family.paintTemplate as RuleTile;var fill=family.fillTemplate as Tile;
            Require(family.validated&&rule!=null&&fill!=null&&AssetDatabase.GetAssetPath(rule)==path+"_Rule.asset"&&AssetDatabase.GetAssetPath(fill)==path+"_Fill.asset",path,"边族引用或验证标记缺失");
            Require(rule.m_TilingRules!=null&&rule.m_TilingRules.Count==256&&rule.m_DefaultSprite==sprites[255]&&fill.sprite==sprites[255],path,"缺少精确规则或满覆盖Tile");
            Require(rule.m_DefaultColliderType==Tile.ColliderType.None&&rule.m_DefaultGameObject==null&&fill.colliderType==Tile.ColliderType.None,path,"技术边族不能夹带碰撞/对象");
            for(int mask=0;mask<256;mask++)
            {
                var r=rule.m_TilingRules[mask];
                Require(r.m_NeighborPositions!=null&&r.m_NeighborPositions.SequenceEqual(Neighbors)&&r.m_Neighbors!=null&&r.m_Neighbors.Count==8&&r.m_Sprites!=null&&r.m_Sprites.Length==1&&r.m_Sprites[0]==sprites[mask]&&r.m_RuleTransform==RuleTile.TilingRuleOutput.Transform.Fixed&&r.m_Output==RuleTile.TilingRuleOutput.OutputSprite.Single&&r.m_ColliderType==Tile.ColliderType.None&&r.m_GameObject==null,path,"邻接规则结构已改变，保留并停止");
                for(int bit=0;bit<8;bit++)Require(r.m_Neighbors[bit]==(((mask&(1<<bit))!=0)?1:2),path,"邻接位不一致");
            }
        }
        private static void ValidateRecipe(GroundRecipe recipe,string path)
        {
            Require(recipe!=null&&recipe.detailTexture!=null&&recipe.edgeFamily!=null&&!string.IsNullOrWhiteSpace(recipe.assetId)&&!string.IsNullOrWhiteSpace(recipe.detailAssetId),path,"配方定义缺少底纹/边族/身份");
            Require(!float.IsNaN(recipe.worldRepeatPeriod)&&!float.IsInfinity(recipe.worldRepeatPeriod)&&recipe.worldRepeatPeriod>0&&recipe.cutoff>0&&recipe.cutoff<1,path,"配方尺度或裁剪值无效");
            string detail=AssetDatabase.GetAssetPath(recipe.detailTexture);
            Require(detail.StartsWith(Root+"/Originals/Textures/",StringComparison.Ordinal),path,"配方不属于原创样本底纹");
            string family=AssetDatabase.GetAssetPath(recipe.edgeFamily);
            Require(family==EdgePath("Rounded")+".asset"||family==EdgePath("Chamfered")+".asset",path,"配方边族不属于独立原创样本");
        }
        private static void ValidateDecoration(GroundDecorationDefinition definition,string path)
        {
            Require(definition!=null&&definition.sprite!=null&&!string.IsNullOrWhiteSpace(definition.assetId),path,"装饰缺少Sprite/身份");
            var sprite=definition.sprite;var pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
            Require((pivot-definition.footpointNormalized).sqrMagnitude<.0001f&&definition.scaleRange.x>0&&definition.scaleRange.y>=definition.scaleRange.x,path,"装饰脚点/缩放范围不合法");
            Require(AssetDatabase.GetAssetPath(sprite).StartsWith(Root+"/Originals/Decorations/",StringComparison.Ordinal),path,"装饰不属于原创样本图片");
        }
        private static void ValidateComplete(GroundTheme theme)
        {
            ValidateFamily(Existing<GroundEdgeFamily>(EdgePath("Rounded")+".asset"),"Rounded");
            ValidateFamily(Existing<GroundEdgeFamily>(EdgePath("Chamfered")+".asset"),"Chamfered");
            foreach(string name in new[]{"Grass","Moss","Road","Alternate"})ValidateRecipe(Existing<GroundRecipe>(RecipePath(name)),RecipePath(name));
            Require(theme!=null&&theme.layers!=null&&theme.layers.Length==3&&theme.layers.Select(l=>l==null?(GroundLayerPurpose)(-1):l.purpose).SequenceEqual(new[]{GroundLayerPurpose.Base,GroundLayerPurpose.Overlay,GroundLayerPurpose.Road}),ThemePath,"样本主题须有三种有序职责层");
            foreach(var layer in theme.layers)ValidateRecipe(layer.recipe,ThemePath+"/"+layer.slotId);
            Require(theme.decorations!=null&&theme.decorations.Length==3,ThemePath,"样本主题须有三个装饰定义");
            foreach(var decoration in theme.decorations)ValidateDecoration(decoration,ThemePath);
            Require(!AssetDatabase.GetDependencies(ThemePath,true).Any(p=>p.Contains("ReferenceLocal")||p.Contains("LocalGenerated")||p.Contains("LocalMedia")||p.Contains("/Characters/")||p.EndsWith("manifest.json",StringComparison.Ordinal)),ThemePath,"出现非原创来源或角色依赖");
        }
    }
}
#endif
