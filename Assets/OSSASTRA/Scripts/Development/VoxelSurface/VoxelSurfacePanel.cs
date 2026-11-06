/* 功能：既有体素受光窗口增加C独立明暗百分比、绝对档位、C0回退与显式保存。
 * 边界：只消费比较组件会话，不改共享材质、灯、时钟、角色光感或正式默认。
 * 输入：使用明确协调者/地图owner及角色指针区域，防止滑条穿透攻击、转镜头；禁用精确释放。
 * 生命周期：参数由比较组件合并刷新；可滚动且不暂停世界，不新建材质/RT/Canvas。
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using Ossastra.Characters;
using Ossastra.MapResearch;
using Ossastra.WorldRendering.VoxelSurface;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.Development.VoxelSurface
{
    public enum VoxelSurfacePreset { OriginalC0, LitPlusOne, LitPlusTwo, BackMinusOne, BackMinusTwo, Balanced }

    [DefaultExecutionOrder(-220), DisallowMultipleComponent]
    public sealed class VoxelSurfacePanel : MonoBehaviour
    {
        public VoxelSurfaceComparison comparison;
        [Tooltip("只捕获本窗口交互，不暂停或切换主控。")] public CharacterControlCoordinator coordinator;
        public MapResearchRoot map;
        public bool showPanel = true;
        [SerializeField] bool expanded;
        public bool Expanded => expanded;
        public bool IsCapturingInput => captured;
        public Rect PanelRect => new Rect(12, Mathf.Clamp(Screen.height * .22f, 42, Mathf.Max(42, Screen.height - 320)),
            Mathf.Min(350, Mathf.Max(180, Screen.width - 24)), expanded ? Mathf.Max(180, Mathf.Min(620, Screen.height * .76f - 16)) : 34);
        GUIStyle wrappingLabel;
        Vector2 scroll;
        CharacterControlCoordinator capturedCoordinator, regionCoordinator;
        MapResearchRoot capturedMap;
        readonly List<CharacterInputHandler> registeredInputs = new List<CharacterInputHandler>();
        bool captured, dragging, numericSelected, focusAllowed = true;
        string litText = "0", backText = "0";
        static readonly string[] DiagnosticLabels = { "0 合成", "1 固有色", "2 世界法线", "3 主灯漫反射", "4 间接漫反射", "5 直接镜面", "6 环境反射", "7 真实阴影" };

        public void SetExpanded(bool value)
        { expanded = value; if (!value) { dragging = false; numericSelected = false; Capture(false); } RegisterRegions(showPanel); }
        public void SetLitFacePercent(float percent)
        { var s = comparison == null ? null : comparison.SessionSettings; if (s != null) comparison.SetSessionAdjustments(Percent(percent), s.backFaceDarkening); }
        public void SetBackFacePercent(float percent)
        { var s = comparison == null ? null : comparison.SessionSettings; if (s != null) comparison.SetSessionAdjustments(s.litFaceBoost, Percent(percent)); }
        static float Percent(float percent) => float.IsNaN(percent) || float.IsInfinity(percent) ? 0 : Mathf.Round(Mathf.Clamp(percent, 0, 50)) / 100;

        // 单项档位是相对C0的绝对目标，保留另一项；不反复累乘。
        public void ApplyPreset(VoxelSurfacePreset preset)
        {
            if (comparison == null) return;
            var s = comparison.SessionSettings; if (s == null) return;
            switch (preset)
            {
                case VoxelSurfacePreset.OriginalC0: comparison.ResetSessionToC0(); break;
                case VoxelSurfacePreset.LitPlusOne: comparison.SetSessionAdjustments(.1f, s.backFaceDarkening); break;
                case VoxelSurfacePreset.LitPlusTwo: comparison.SetSessionAdjustments(.2f, s.backFaceDarkening); break;
                case VoxelSurfacePreset.BackMinusOne: comparison.SetSessionAdjustments(s.litFaceBoost, .1f); break;
                case VoxelSurfacePreset.BackMinusTwo: comparison.SetSessionAdjustments(s.litFaceBoost, .2f); break;
                case VoxelSurfacePreset.Balanced: comparison.SetSessionAdjustments(.15f, .1f); break;
            }
            SyncText();
        }
        void Update()
        {
            if (!Application.isPlaying) return;
            RegisterRegions(showPanel && isActiveAndEnabled);
            var pointer = Pointer.current;
            var mouse = pointer == null ? new Vector2(-1, -1) : pointer.position.ReadValue();
            bool hover = PanelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            bool pressed = pointer != null && pointer.press.isPressed;
            if (hover && pressed) dragging = true;
            if (!pressed) dragging = false;
            if (captured && (capturedCoordinator != coordinator || capturedMap != map)) Capture(false);
            Capture(showPanel && focusAllowed && (hover || dragging || expanded && numericSelected));
        }
        void RegisterRegions(bool add)
        {
            if (!add || regionCoordinator != coordinator)
            {
                foreach (var input in registeredInputs) if (input != null) input.RemovePointerRegion(this);
                registeredInputs.Clear(); regionCoordinator = null;
            }
            if (!add || coordinator == null) return;
            if (regionCoordinator == null)
            {
                regionCoordinator = coordinator;
                foreach (var actor in coordinator.Characters)
                    if (actor != null) { var input = actor.GetComponent<CharacterInputHandler>(); if (input != null) registeredInputs.Add(input); }
            }
            foreach (var input in registeredInputs) if (input != null) input.RegisterPointerRegion(this, PanelRect);
        }
        void Capture(bool value)
        {
            if (value == captured) return;
            if (!value)
            {
                if (capturedCoordinator != null) capturedCoordinator.SetInputCapture(this, false);
                if (capturedMap != null) capturedMap.SetUiCameraCapture(this, false);
                capturedCoordinator = null; capturedMap = null; captured = false; return;
            }
            capturedCoordinator = coordinator; capturedMap = map;
            if (capturedCoordinator != null) capturedCoordinator.SetInputCapture(this, true);
            if (capturedMap != null) capturedMap.SetUiCameraCapture(this, true);
            captured = true;
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !showPanel) return;
            if (wrappingLabel == null) wrappingLabel = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var area = PanelRect;
            var focused = GUI.GetNameOfFocusedControl();
            if (Event.current.type == EventType.MouseDown && !area.Contains(Event.current.mousePosition) && focused.StartsWith("VoxelFace", StringComparison.Ordinal)) GUI.FocusControl("");
            GUILayout.BeginArea(area, GUI.skin.box);
            if (GUILayout.Button((expanded ? "▼ " : "▶ ") + "体素受光", GUILayout.Height(25))) SetExpanded(!expanded);
            if (expanded) { scroll = GUILayout.BeginScrollView(scroll); DrawBody(); GUILayout.EndScrollView(); }
            GUILayout.EndArea();
            numericSelected = expanded && GUI.GetNameOfFocusedControl().StartsWith("VoxelFace", StringComparison.Ordinal);
            var e = Event.current;
            if (area.Contains(e.mousePosition) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)) e.Use();
        }
        void DrawBody()
        {
            if (comparison == null) { GUILayout.Label("未接线体素比较组件。", wrappingLabel); return; }
            bool previous = GUI.enabled;
            GUI.enabled = previous && comparison.isActiveAndEnabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("A 原版")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Original);
            if (GUILayout.Button("B 校准")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Calibrated);
            if (GUILayout.Button("C 轻分面")) comparison.ApplyMode(VoxelSurfaceComparisonMode.Faceted);
            GUILayout.EndHorizontal();
            var settings = comparison.SessionSettings;
            bool activeC = comparison.mode == VoxelSurfaceComparisonMode.Faceted && settings != null;
            GUILayout.Label(activeC ? "C独立会话 · 两项0恢复原C" : "C参数暂不应用（A/B保持原响应）", wrappingLabel);
            GUI.enabled = previous && comparison.isActiveAndEnabled && activeC;
            if (settings != null)
            {
                DrawPercent("亮面增强", "VoxelFaceLit", settings.litFaceBoost, ref litText, SetLitFacePercent);
                DrawPercent("背光压暗", "VoxelFaceBack", settings.backFaceDarkening, ref backText, SetBackFacePercent);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("仅亮面 +1")) ApplyPreset(VoxelSurfacePreset.LitPlusOne);
            if (GUILayout.Button("仅亮面 +2")) ApplyPreset(VoxelSurfacePreset.LitPlusTwo);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("仅背光 −1")) ApplyPreset(VoxelSurfacePreset.BackMinusOne);
            if (GUILayout.Button("仅背光 −2")) ApplyPreset(VoxelSurfacePreset.BackMinusTwo);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("恢复原 C0")) ApplyPreset(VoxelSurfacePreset.OriginalC0);
            if (GUILayout.Button("均衡候选")) ApplyPreset(VoxelSurfacePreset.Balanced);
            GUILayout.EndHorizontal();
            if (comparison.saveCandidateProfile != null && GUILayout.Button("读取已存候选")) { comparison.LoadSessionFromProfile(comparison.saveCandidateProfile); SyncText(); }
            GUILayout.Label("当前Profile：" + (comparison.SessionSourceProfile == null ? "未配置" : comparison.SessionSourceProfile.name) + "\n对象：" + comparison.BindingCount + " · 只调体素材质", wrappingLabel);
            GUI.enabled = GUI.enabled && Application.isEditor;
            if (GUILayout.Button("保存为候选 Profile")) comparison.RequestSaveCandidate();
            if (GUILayout.Button("显式设为本对象集默认")) comparison.RequestSetBindingDefault();
            GUI.enabled = previous;
            GUILayout.Label(comparison.SaveStatus, wrappingLabel);
            if (!string.IsNullOrEmpty(comparison.LastSavePath)) GUILayout.Label(comparison.LastSavePath, wrappingLabel);
            GUILayout.Label(comparison.Status, wrappingLabel);
            if (settings != null)
            {
                GUILayout.Label("高级只读：分面 " + settings.mainShape.ToString("0.00") + " · GI " + settings.indirectDiffuse.ToString("0.00") + " · 环境反射 " + settings.environmentSpecular.ToString("0.00"), wrappingLabel);
                GUI.enabled = false; GUILayout.SelectionGrid(Mathf.Clamp(settings.diagnostic, 0, 7), DiagnosticLabels, 2, GUILayout.Height(88)); GUI.enabled = previous;
            }
        }
        void DrawPercent(string label, string control, float value, ref string text, Action<float> set)
        {
            float percent = value * 100;
            GUILayout.Label(label + "  " + percent.ToString("0") + "%");
            GUILayout.BeginHorizontal();
            float next = Mathf.Round(GUILayout.HorizontalSlider(percent, 0, 50));
            if (Mathf.Abs(next - percent) > .1f) { set(next); text = next.ToString("0", CultureInfo.InvariantCulture); }
            if (GUI.GetNameOfFocusedControl() != control) text = percent.ToString("0", CultureInfo.InvariantCulture);
            GUI.SetNextControlName(control);
            string edit = GUILayout.TextField(text, 4, GUILayout.Width(42));
            if (edit != text)
            { text = edit; if (float.TryParse(edit, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) { set(parsed); text = (Percent(parsed) * 100).ToString("0", CultureInfo.InvariantCulture); } }
            GUILayout.Label("%", GUILayout.Width(16)); GUILayout.EndHorizontal();
        }
        void SyncText()
        { var s = comparison == null ? null : comparison.SessionSettings; if (s != null) { litText = (s.litFaceBoost * 100).ToString("0"); backText = (s.backFaceDarkening * 100).ToString("0"); } }
        void OnApplicationFocus(bool value) { focusAllowed = value; if (!value) { dragging = false; numericSelected = false; Capture(false); } }
        void OnDisable() { RegisterRegions(false); dragging = false; numericSelected = false; Capture(false); }
        void OnDestroy() { RegisterRegions(false); Capture(false); }
    }
}
