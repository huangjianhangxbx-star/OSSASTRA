/* 脚本：MapFlickerTracePanel
 * 功能：独立研究场的小型诊断面板，明确开始、停止、异常标记导出。
 * 边界：不替换正式HUD、不采样快捷键、不控制角色；按钮点击才触发导出。
 * 协作：为配置角色注册同一owner的指针区域，禁用时精确释放，防止点击面板同时攻击。
 */
using System.Collections.Generic;
using UnityEngine;
using C = Ossastra.Characters;

namespace Ossastra.MapResearchIntegration.Diagnostics
{
    [DefaultExecutionOrder(-380)]
    [DisallowMultipleComponent]
    public sealed class MapFlickerTracePanel : MonoBehaviour
    {
        public MapFlickerTraceRecorder recorder;
        [Min(0)] public float left = 12;
        [Min(0)] public float bottomOffset = 100;
        [Min(400)] public float width = 690;
        private readonly List<C.CharacterInputHandler> registered = new List<C.CharacterInputHandler>();
        public Rect PanelRect => new Rect(left, Mathf.Max(0, Screen.height - bottomOffset - 94), Mathf.Min(width, Mathf.Max(400, Screen.width - left - 12)), 94);

        private void OnEnable() => RefreshPointerRegions();
        private void Update() => RefreshPointerRegions();
        private void RefreshPointerRegions()
        {
            if (!Application.isPlaying) return;
            for (var i = registered.Count - 1; i >= 0; i--)
            {
                var input = registered[i];
                if (input != null && ContainsActorInput(input)) continue;
                if (input != null) input.RemovePointerRegion(this);
                registered.RemoveAt(i);
            }
            if (recorder == null || recorder.actors == null) return;
            foreach (var actor in recorder.actors)
            {
                if (actor == null) continue;
                var input = actor.GetComponent<C.CharacterInputHandler>();
                if (input == null) continue;
                if (!registered.Contains(input)) registered.Add(input);
                input.RegisterPointerRegion(this, PanelRect);
            }
        }
        private bool ContainsActorInput(C.CharacterInputHandler input)
        {
            if (recorder == null || recorder.actors == null) return false;
            foreach (var actor in recorder.actors) if (actor != null && actor.gameObject == input.gameObject) return true;
            return false;
        }
        private void OnDisable() => ReleasePointerRegions();
        private void OnDestroy() => ReleasePointerRegions();
        private void ReleasePointerRegions()
        {
            foreach (var input in registered) if (input != null) input.RemovePointerRegion(this);
            registered.Clear();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || recorder == null) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("闪烁诊断：" + recorder.Status);
            GUILayout.Label("样本 " + recorder.SampleCount + " / 正式mesh " + recorder.MeshEventCount, GUILayout.Width(220));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !recorder.IsTracing;
            if (GUILayout.Button("开始追踪", GUILayout.Width(110))) recorder.StartFromButton();
            GUI.enabled = previousEnabled && recorder.IsTracing;
            if (GUILayout.Button("停止追踪", GUILayout.Width(110))) recorder.StopTracing();
            GUI.enabled = previousEnabled && recorder.SampleCount > 0;
            if (GUILayout.Button("标记异常并导出", GUILayout.Width(160))) recorder.MarkAndExportFromButton();
            GUI.enabled = previousEnabled;
            GUILayout.Label("标记/导出不会暂停世界；仅点击写盘");
            GUILayout.EndHorizontal();
            var result = !string.IsNullOrEmpty(recorder.LastError) ? recorder.LastError :
                !string.IsNullOrEmpty(recorder.LastExportPath) ? "已导出：" + recorder.LastExportPath : "看见异常后点击标记导出；需后续帧可稍等再用Inspector导出。";
            GUILayout.Label(result);
            GUILayout.EndArea();
        }
    }
}
