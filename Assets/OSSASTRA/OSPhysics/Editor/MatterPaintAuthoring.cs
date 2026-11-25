// 职责：创建及查看物质实验室随项目发布的两套手绘配置。
// 只在编辑模式补建缺失的样板。自定义外观与本机研究素材由各自入口管理。
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterPaintAuthoring
    {
        private const string ResourceRoot = "Assets/OSSASTRA/OSPhysics/Visual/Resources/OSPhysics";
        private const string ProfilesFolder = ResourceRoot + "/Profiles";
        private const string MenuPath = "OSSASTRA/实验室/创建两套手绘美术配置";

        [MenuItem(MenuPath)]
        private static void CreateSampleProfiles()
        {
            Debug.Log(EnsureSamples());
            var ruins = AssetDatabase.LoadAssetAtPath<MatterPaintProfile>(ProfilesFolder + "/Ruins.asset");
            if (ruins != null)
            {
                Selection.activeObject = ruins;
                EditorGUIUtility.PingObject(ruins);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanCreateSampleProfiles() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// 保留已有美术配置，仅为缺失样板创建独立的资源资产。调用者无需打开场景。
        /// </summary>
        public static string EnsureSamples()
        {
            if (!CanCreateSampleProfiles())
                throw new InvalidOperationException("手绘配置只能在编辑模式创建。");

            string project = Directory.GetParent(Application.dataPath).FullName;
            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
            {
                string diskFolder = Path.Combine(project, ProfilesFolder);
                if (!AssetDatabase.IsValidFolder(ResourceRoot) || Directory.Exists(diskFolder) || File.Exists(diskFolder))
                    throw new IOException("配置目录未就绪。请先刷新实验室资源。");

                string folder = AssetDatabase.CreateFolder(ResourceRoot, "Profiles");
                if (AssetDatabase.GUIDToAssetPath(folder) != ProfilesFolder)
                    throw new InvalidOperationException("无法在实验室资源目录创建 Profiles 文件夹。");
            }

            int added = 0;
            if (EnsureSample(project, MatterPaintStyle.Ruins)) added++;
            if (EnsureSample(project, MatterPaintStyle.Moss)) added++;
            return $"手绘美术配置就绪。新建 {added} 套，保留 {2 - added} 套。";
        }

        private static bool EnsureSample(string project, MatterPaintStyle style)
        {
            string path = ProfilesFolder + "/" + style + ".asset";
            var stored = AssetDatabase.LoadMainAssetAtPath(path);
            if (stored is MatterPaintProfile)
                return false;

            // CreateAsset 会覆盖同名文件。类型错误或尚未导入也必须保留原文件。
            if (stored != null || File.Exists(Path.Combine(project, path)) || Directory.Exists(Path.Combine(project, path)))
                throw new IOException($"预设路径已被占用，保留原内容。{path}");

            var sample = MatterPaintProfile.CreateSample(style);
            try
            {
                // 只接受实验室自身的原创背景。场景和样板由此保持可直接发布的依赖关系。
                var background = sample.Background;
                string backgroundPath = AssetDatabase.GetAssetPath(background);
                if (background == null || background.width < 2 || background.height < 2 ||
                    !backgroundPath.StartsWith(ResourceRoot + "/Art/", StringComparison.Ordinal))
                    throw new InvalidOperationException($"缺少 {style} 的实验室背景，请先导入该样板的原创画片。");

                AssetDatabase.CreateAsset(sample, path);
                AssetDatabase.SaveAssetIfDirty(sample);
                if (AssetDatabase.LoadAssetAtPath<MatterPaintProfile>(path) != sample)
                    throw new IOException($"手绘配置保存后无法读取。{path}");

                return true;
            }
            finally
            {
                if (sample != null && !EditorUtility.IsPersistent(sample))
                    UnityEngine.Object.DestroyImmediate(sample);
            }
        }
    }

    /// <summary>
    /// 在原生配置 Inspector 中展示实际背景以及运行预览的配置来源。
    /// </summary>
    [CustomEditor(typeof(MatterPaintProfile))]
    internal sealed class MatterPaintProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var profile = (MatterPaintProfile)target;
            if (profile.Background != null)
            {
                Rect preview = GUILayoutUtility.GetRect(48f, 116f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(preview, profile.Background, null, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox("未指定背景画片。实验室将使用内置的原创背景。", MessageType.Info);
            }

            DrawDefaultInspector();
        }
    }
}
