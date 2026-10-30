/* 功能：独立研究场的可复现合法站位入口，供开窗/扩大/整件/恢复与原控制链验证。
 * 边界：只调用已有Motor.Teleport和相机语义端口，不直接写透明状态、角色HP或动画，不暂停世界。
 * 生命周期：镜头固定使用MapResearchRoot已有follow/fixedTarget；离开本调试入口条件归还自己仍持有的值。
 */
using System;
using C = Ossastra.Characters;
using Ossastra.MapResearch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.SceneVisibility
{
    [DefaultExecutionOrder(-250), DisallowMultipleComponent]
    public sealed class SceneVisibilityLabRoutes : MonoBehaviour
    {
        public SceneVisibilityRoot visibility;
        public MapResearchRoot map;
        public C.CharacterControlCoordinator coordinator;
        public bool showPanel = true;
        public string Status { get; private set; } = "合法站位只改原Motor，世界持续运行";
        bool capturing, cameraOwned, beforeFollow;
        Vector3 beforeTarget;
        float beforeSmooth;
        readonly Vector3 demoTarget = new Vector3(-3, .02f, -11);
        C.CharacterRoot[] actors;
        Vector3[] spawns;

        void Start()
        {
            if (coordinator == null) return;
            actors = new C.CharacterRoot[coordinator.Characters.Count]; spawns = new Vector3[actors.Length];
            for (int i = 0; i < actors.Length; ++i) { actors[i] = coordinator.Characters[i]; spawns[i] = actors[i].transform.position; }
        }
        Rect Panel => new Rect(Mathf.Max(5, Screen.width - 370), Screen.height - 150, 360, 140);
        void Update()
        {
            bool over = false;
            if (showPanel && Mouse.current != null)
            { Vector2 point = Mouse.current.position.ReadValue(); point.y = Screen.height - point.y;
                over = Panel.Contains(point) || (visibility != null && visibility.showDiagnostics && new Rect(Mathf.Max(5, Screen.width - 370), 65, 360, 320).Contains(point)); }
            if (coordinator != null && over != capturing) { coordinator.SetInputCapture(this, over); capturing = over; }
        }
        public bool Stage(int stage)
        {
            if (visibility == null || map == null || coordinator == null || coordinator.Current == null || actors == null || actors.Length != 2)
            { Status = "缺少明确原角色/相机上下文"; return false; }
            var main = coordinator.Current; var buddy = actors[0] == main ? actors[1] : actors[0];
            if (!main.IsAlive || !buddy.IsAlive) { Status = "先沿原调试会话恢复角色；本入口不写HP"; return false; }
            Vector3 primary = stage == 0 ? new Vector3(-6.5f, .02f, -11) : stage == 1 ? new Vector3(-6.05f, .02f, -11) : demoTarget;
            // 伙伴留在主控外侧，避免正常跟随先进入墙后，让轻挡按钮被伙伴的整件需求提前覆盖。
            Vector3 companion = primary + Vector3.left * 2f;
            if (!Clear(main, primary) || !Clear(buddy, companion)) { Status = "该起点被原碰撞阻挡；不强行穿入"; return false; }
            main.GetComponent<C.CharacterMovement>().Teleport(primary); buddy.GetComponent<C.CharacterMovement>().Teleport(companion);
            if (!cameraOwned) { beforeFollow = map.followParticipant; beforeTarget = map.fixedTarget; beforeSmooth = map.followSmoothTime; cameraOwned = true; }
            map.followParticipant = false; map.fixedTarget = demoTarget; map.followSmoothTime = 0;
            if (map.IsStructureMode) map.SetStructureMode(false);
            if (map.cameraInput != null) map.cameraInput.DiscardPending();
            float delta = Mathf.DeltaAngle(map.TargetYaw, 180); int steps = Mathf.RoundToInt(Mathf.Abs(delta) / 45f);
            for (int i = 0; i < steps; ++i) map.RequestRotationStep(delta > 0 ? 1 : -1);
            visibility.effectEnabled = true;
            Status = stage == 0 ? "轻挡起点；镜头固定，沿原输入移动也可加深" : stage == 1 ? "扩大起点；覆盖程度来自真实代理检测" : "完整遮挡起点；小墙允许整件淡出";
            return true;
        }
        bool Clear(C.CharacterRoot actor, Vector3 position)
        {
            var controller = actor.GetComponent<CharacterController>(); if (controller == null) return false;
            foreach (var hit in Physics.OverlapCapsule(position + Vector3.up * controller.radius,
                position + Vector3.up * (controller.height - controller.radius), controller.radius - .015f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                bool ownActor = false; foreach (var candidate in actors) if (hit.transform.IsChildOf(candidate.transform)) ownActor = true;
                if (!ownActor) return false;
            }
            return true;
        }
        public void ResetRoute()
        {
            if (actors != null) for (int i = 0; i < actors.Length; ++i) if (actors[i] != null && Clear(actors[i], spawns[i])) actors[i].GetComponent<C.CharacterMovement>().Teleport(spawns[i]);
            ReleaseCamera(); if (map != null) map.ResetView();
            Status = "已沿原Motor回到出生点；遮挡需求自然恢复";
        }
        void ReleaseCamera()
        {
            if (cameraOwned && map != null)
            {
                if (!map.followParticipant) map.followParticipant = beforeFollow;
                if (map.fixedTarget == demoTarget) map.fixedTarget = beforeTarget;
                if (map.followSmoothTime == 0) map.followSmoothTime = beforeSmooth;
            }
            cameraOwned = false;
        }
        void OnGUI()
        {
            if (!showPanel || !Application.isPlaying) return;
            GUILayout.BeginArea(Panel, GUI.skin.box); GUILayout.Label("透视验证路线 · 同机位，世界不暂停");
            GUILayout.BeginHorizontal(); if (GUILayout.Button("轻挡")) Stage(0); if (GUILayout.Button("扩大")) Stage(1); if (GUILayout.Button("整件")) Stage(2); if (GUILayout.Button("回初始")) ResetRoute(); GUILayout.EndHorizontal();
            if (GUILayout.Button("检查真实登记与预算")) Status = visibility != null && visibility.ValidateConfiguration(out string reason) ? "登记有效；实际渲染请看上方诊断" : "登记无效";
            GUILayout.Label(Status); GUILayout.EndArea();
        }
        void OnDisable() { if (coordinator != null) coordinator.SetInputCapture(this, false); capturing = false; ReleaseCamera(); }
    }
}
