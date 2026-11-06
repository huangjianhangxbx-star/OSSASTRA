/* 功能：默认折叠的体素A/B/C开发入口，只消费明确比较组件与当前作者档。
 * 边界：不创建Canvas，不扫描角色/相机，不持有RT，不修改Profile、灯、天气、时钟或业务。
 * 输入：仅处理自身IMGUI区域事件，不注册全局输入/按键；诊断与比例为只读回显。
 */
using Ossastra.WorldRendering.VoxelSurface;
using UnityEngine;

namespace Ossastra.Development.VoxelSurface
{
    [DisallowMultipleComponent]
    public sealed class VoxelSurfacePanel : MonoBehaviour
    {
        [Tooltip("明确体素比较组件；不自动寻找或接管场景对象。")]
        public VoxelSurfaceComparison comparison;
        public bool showPanel = true;
        [SerializeField, Tooltip("默认折叠；仅影响此开发窗口。")]
        bool expanded;
        public bool Expanded => expanded;
        GUIStyle wrappingLabel;

        static readonly string[] DiagnosticLabels =
        {
            "0 合成", "1 固有色", "2 世界法线", "3 主灯漫反射",
            "4 间接漫反射", "5 直接镜面", "6 环境反射", "7 真实阴影"
        };

        public void SetExpanded(bool value) { expanded = value; }

        void OnGUI()
        {
            if (!Application.isPlaying || !showPanel) return;
            if (wrappingLabel == null) wrappingLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            float top = Mathf.Clamp(Screen.height * .22f, 42, Mathf.Max(42, Screen.height - 320));
            var area = new Rect(12, top, Mathf.Min(318, Mathf.Max(180, Screen.width - 24)), expanded ? 304 : 34);
            GUILayout.BeginArea(area, GUI.skin.box);
            if (GUILayout.Button((expanded ? "▼ " : "▶ ") + "体素受光", GUILayout.Height(25))) SetExpanded(!expanded);
            if (expanded) DrawBody();
            GUILayout.EndArea();
            var current = Event.current;
            if (area.Contains(current.mousePosition) &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseUp || current.type == EventType.ScrollWheel)) current.Use();
        }

        void DrawBody()
        {
            if (comparison == null)
            {
                GUILayout.Label("未接线体素比较组件。", wrappingLabel);
                return;
            }
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && comparison.isActiveAndEnabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("A 原版")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Original);
            if (GUILayout.Button("B 校准")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated);
            if (GUILayout.Button("C 轻分面")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Faceted);
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
            var profile = comparison.mode == VoxelSurfaceComparisonMode.Calibrated ? comparison.calibratedProfile :
                comparison.mode == VoxelSurfaceComparisonMode.Faceted ? comparison.shapedProfile : null;
            var settings = profile == null ? null : profile.settings;
            GUILayout.Label("当前档：" + comparison.mode + " · 诊断/比例只读", wrappingLabel);
            if (settings == null)
                GUILayout.Label(comparison.mode == VoxelSurfaceComparisonMode.Original ? "A保留原材质；无体素参数覆盖。" : "直接使用作者材质；未配置Profile覆盖。", wrappingLabel);
            else
                GUILayout.Label("Profile " + profile.name + "\n间接漫反射 " + settings.indirectDiffuse.ToString("0.00") +
                    "  环境反射 " + settings.environmentSpecular.ToString("0.00"), wrappingLabel);
            int diagnostic = settings == null ? 0 : Mathf.Clamp(settings.diagnostic, 0, 7);
            GUI.enabled = false;
            GUILayout.SelectionGrid(diagnostic, DiagnosticLabels, 2, GUILayout.Height(88));
            GUI.enabled = previousEnabled;
            GUILayout.Label(comparison.Status, wrappingLabel);
        }
    }
}
