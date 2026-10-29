/* 脚本：GroundWorkshopOwnership
 * 功能：集中校验注册地图、层绑定、受控资产路径与配方兼容，拒绝把通用工具开放给任意场景。
 * 边界：只读预检，不修复引用、不保存旧场；所有作者服务和窗口消费同一个判定。
 * 协作：MapRegistration声明身份，Unity实际路径/对象身份提供证据；不是信任一个字符串标记。
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopOwnership
    {
        public const string Root = "Assets/OSSASTRA/GroundWorkshop";
        public const string ShaderName = "Ossastra/GroundResearch/MaskWorldTile";
        public const string ReferenceScene = "Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopLab.unity";
        public const string OriginalScene = "Assets/OSSASTRA/Scenes/Research/SC_GroundWorkshopOriginalLab.unity";

        public static bool IsUnder(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder) || path.Contains('\\')) return false;
            string full = Path.GetFullPath(path), root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && path.StartsWith(folder + "/", StringComparison.Ordinal);
        }
        public static bool IsScenePath(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.Ordinal) &&
            (path == ReferenceScene || path == OriginalScene || IsUnder(path, Root + "/Maps") || IsUnder(path, Root + "/Samples"));

        public static bool ContextReady(out string reason)
        {
            reason = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                reason = "请先退出Play并等待Editor完成编译和导入。";
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                reason = "PrefabStage打开中；保留现场，工坊只读。";
            return reason == null;
        }
        public static bool CanEdit(GroundWorkshopMap map, out string reason)
        {
            if (!ContextReady(out reason)) return false;
            if (map == null) { reason = "请选择已登记的工坊地图；旧研究场保持只读。"; return false; }
            try
            {
                var scene = map.gameObject.scene;
                if (!map.isActiveAndEnabled || EditorUtility.IsPersistent(map) || !scene.IsValid() || !scene.isLoaded ||
                    scene != SceneManager.GetActiveScene() || EditorSceneManager.IsPreviewScene(scene) || !IsScenePath(scene.path))
                    throw new InvalidOperationException("仅当前活动、已保存的自有工坊地图可写；不接受保护旧场、Preview或资产对象。");
                var registration = map.registration;
                if (map.schemaVersion != 1 || registration == null || registration.schemaVersion != 1 ||
                    registration.owner != "Ossastra.GroundWorkshop" || !Guid.TryParseExact(registration.mapId,"N",out _) ||
                    registration.scenePath != scene.path || !IsUnder(registration.assetFolder, Root + "/Maps") ||
                    !registration.assetFolder.EndsWith("/" + registration.mapId, StringComparison.Ordinal) ||
                    AssetDatabase.GetAssetPath(registration) != registration.assetFolder + "/MapRegistration.asset")
                    throw new InvalidOperationException("地图注册身份、保存路径或受控资产目录不匹配；不会自动补造。");
                var surface = map.surface;
                if (surface == null || !surface.isActiveAndEnabled || surface.gameObject != map.gameObject || surface.WorldArea <= 0 ||
                    surface.layers == null || surface.layers.Length < 1 || surface.layers.Length > 16 ||
                    map.layerBindings == null || map.layerBindings.Length != surface.layers.Length || map.theme == null ||
                    map.theme.layers == null || map.theme.layers.Length != surface.layers.Length ||
                    !IsUnder(AssetDatabase.GetAssetPath(map.theme), registration.assetFolder) || map.decorationRoot == null ||
                    !map.decorationRoot.IsChildOf(map.transform))
                    throw new InvalidOperationException("地图Surface、主题、受控图层或装饰根不完整；不会重建覆盖作者数据。");
                if (surface.layers.Select(l=>l == null ? null : l.tilemap).Distinct().Count() != surface.layers.Length)
                    throw new InvalidOperationException("存在空或重复Tilemap图层。");
                for (int i=0; i<surface.layers.Length; i++)
                {
                    var layer = surface.layers[i]; var binding = map.layerBindings[i]; var slot = map.theme.layers[i];
                    var renderer = layer == null || layer.tilemap == null ? null : layer.tilemap.GetComponent<TilemapRenderer>();
                    if (layer == null || binding == null || slot == null || !surface.Owns(layer.tilemap) ||
                        string.IsNullOrEmpty(binding.slotId) || slot.slotId != binding.slotId ||
                        layer.paintTile == null || layer.paintTile != binding.ownedTile ||
                        renderer == null || renderer.sharedMaterial != binding.ownedMaterial || binding.ownedMaterial == null ||
                        binding.ownedMaterial.shader == null || binding.ownedMaterial.shader.name != ShaderName ||
                        !IsUnder(AssetDatabase.GetAssetPath(binding.ownedTile), registration.assetFolder) ||
                        !IsUnder(AssetDatabase.GetAssetPath(binding.ownedMaterial), registration.assetFolder) ||
                        binding.recipe != slot.recipe || !ValidateRecipe(binding.recipe, out string _))
                        throw new InvalidOperationException("第"+(i+1)+"层的原生Tile/材质/配方与注册绑定不一致，已拒绝写入。");
                }
                reason = null; return true;
            }
            catch (Exception e) { reason=e.Message; return false; }
        }
        public static void Require(GroundWorkshopMap map)
        { if (!CanEdit(map,out string reason)) throw new InvalidOperationException(reason); }

        public static bool ValidateRecipe(GroundRecipe recipe, out string reason)
        {
            reason = null;
            if (recipe == null || recipe.schemaVersion != 1 || string.IsNullOrEmpty(recipe.assetId) || recipe.detailTexture == null)
                reason = "配方缺少有效版本、assetId或真实底纹；先保存一份自有配方。";
            else if (recipe.detailTexture.width < 16 || recipe.detailTexture.height < 16 ||
                !IsFinite(recipe.worldRepeatPeriod) || recipe.worldRepeatPeriod < .1f ||
                !IsFinite(recipe.cutoff) || recipe.cutoff <= 0 || recipe.cutoff >= 1)
                reason = "底纹最小16像素；周期必须为至少0.1米的有限数，裁剪阈值必须在0与1之间。";
            else if (recipe.edgeFamily == null || recipe.edgeFamily.schemaVersion != 1 || !recipe.edgeFamily.validated ||
                string.IsNullOrEmpty(recipe.edgeFamily.assetId) || recipe.edgeFamily.paintTemplate == null ||
                recipe.edgeFamily.fillTemplate == null || recipe.edgeFamily.preview == null)
                reason = "缺少已校验的边缘族（绘制Tile、满覆盖Tile和预览）；不要仅凭切片数量导入。";
            else
            {
                var rule = recipe.edgeFamily.paintTemplate as RuleTile;
                var fill = recipe.edgeFamily.fillTemplate as Tile;
                if (rule == null || rule.m_DefaultSprite == null || rule.m_TilingRules == null || rule.m_TilingRules.Count == 0 ||
                    rule.m_TilingRules.Any(r=>r.m_Sprites == null || r.m_Sprites.Length == 0 || r.m_Sprites.Any(s=>s == null)) ||
                    fill == null || fill.sprite == null)
                    reason = "v1支持显式校验的Native RuleTile族，当前有缺失Sprite或满覆盖Tile。";
            }
            return reason == null;
        }
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
#endif
