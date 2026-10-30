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
        Vector3 beforeTarget, writtenTarget;
        float beforeSmooth;
        [SerializeField, Tooltip("整件演示与固定镜头中心；目标必须满足当前主体净空")]
        Vector3 demoTarget = new Vector3(-3, .02f, -11.2f);
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
                over = Panel.Contains(point) || (visibility != null && visibility.showDiagnostics && visibility.DiagnosticsRect.Contains(point)); }
            if (coordinator != null && over != capturing) { coordinator.SetInputCapture(this, over); capturing = over; }
        }
        public bool Stage(int stage)
        {
            if (visibility == null || map == null || coordinator == null || coordinator.Current == null || actors == null || actors.Length != 2)
            { Status = "缺少明确原角色/相机上下文"; return false; }
            var main = coordinator.Current; var buddy = actors[0] == main ? actors[1] : actors[0];
            if (!main.IsAlive || !buddy.IsAlive) { Status = "先沿原调试会话恢复角色；本入口不写HP"; return false; }
            Vector3 primary = stage == 0 ? new Vector3(-6.5f, .02f, -11.2f) : stage == 1 ? new Vector3(-6.05f, .02f, -11.2f) : demoTarget;
            // 伙伴留在主控外侧，避免正常跟随先进入墙后，让轻挡按钮被伙伴的整件需求提前覆盖。
            Vector3 companion = primary + Vector3.left * 2f;
            if (!ClearTargets(new[] { main, buddy }, new[] { primary, companion }))
            { Status = "该起点不满足主体净空；未移动任何角色"; return false; }
            main.GetComponent<C.CharacterMovement>().Teleport(primary); buddy.GetComponent<C.CharacterMovement>().Teleport(companion);
            if (!cameraOwned) { beforeFollow = map.followParticipant; beforeTarget = map.fixedTarget; beforeSmooth = map.followSmoothTime; cameraOwned = true; }
            map.followParticipant = false; map.fixedTarget = demoTarget; writtenTarget = demoTarget; map.followSmoothTime = 0;
            if (map.IsStructureMode) map.SetStructureMode(false);
            if (map.cameraInput != null) map.cameraInput.DiscardPending();
            float delta = Mathf.DeltaAngle(map.TargetYaw, 180); int steps = Mathf.RoundToInt(Mathf.Abs(delta) / 45f);
            for (int i = 0; i < steps; ++i) map.RequestRotationStep(delta > 0 ? 1 : -1);
            visibility.effectEnabled = true;
            Status = stage == 0 ? "轻挡起点；镜头固定，沿原输入移动也可加深" : stage == 1 ? "扩大起点；覆盖程度来自真实代理检测" : "完整遮挡起点；小墙允许整件淡出";
            return true;
        }
        struct TargetCapsule
        {
            public Vector3 lower, upper;
            public float radius;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool TryCapsule(C.CharacterRoot actor, Vector3 position, out TargetCapsule capsule)
        {
            capsule = default;
            if (actor == null || !Finite(position) || Vector3.Dot(actor.transform.up, Vector3.up) < .9999f ||
                actor.GetComponent<C.CharacterMovement>() == null) return false;
            var controller = actor.GetComponent<CharacterController>(); if (controller == null) return false;
            var clearance = actor.GetComponent<C.CharacterEnvironmentClearance>();
            if (clearance != null && clearance.isActiveAndEnabled)
            {
                float radius = clearance.coreRadius, height = clearance.coreHeight, margin = clearance.contactMargin;
                if (!Finite(radius) || !Finite(height) || !Finite(margin) || radius <= 0 || height < 2 * radius || margin <= 0) return false;
                // The module's world-metre body starts at Root's foot; the margin is a conservative
                // preflight allowance. It does not alter the original movement/hit CharacterController.
                capsule.lower = position + Vector3.up * radius;
                capsule.upper = position + Vector3.up * (height - radius);
                capsule.radius = radius + margin;
            }
            else
            {
                // Disabled/uninstalled optional module retains the actual old CC body, including its center/TRS.
                var scale = actor.transform.lossyScale;
                if (!Finite(scale) || !Finite(controller.center) || !Finite(controller.radius) || !Finite(controller.height)) return false;
                float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float height = Mathf.Max(controller.height * Mathf.Abs(scale.y), radius * 2);
                if (radius <= 0 || height <= 0) return false;
                var center = position + actor.transform.TransformVector(controller.center);
                float half = height * .5f - radius;
                capsule.lower = center - Vector3.up * half;
                capsule.upper = center + Vector3.up * half;
                capsule.radius = radius;
            }
            return Finite(capsule.lower) && Finite(capsule.upper) && Finite(capsule.radius);
        }
        static bool Intersects(TargetCapsule a, TargetCapsule b)
        {
            // Supported bodies are upright. This is the exact distance between their vertical axis segments.
            float dx = a.lower.x - b.lower.x, dz = a.lower.z - b.lower.z;
            float dy = Mathf.Max(0, Mathf.Max(a.lower.y, b.lower.y) - Mathf.Min(a.upper.y, b.upper.y));
            float sum = a.radius + b.radius;
            return dx * dx + dy * dy + dz * dz < sum * sum;
        }
        bool ClearTargets(C.CharacterRoot[] movers, Vector3[] destinations)
        {
            if (movers == null || destinations == null || movers.Length == 0 || movers.Length != destinations.Length) return false;
            var capsules = new TargetCapsule[movers.Length];
            for (int i = 0; i < movers.Length; ++i)
            {
                if (!TryCapsule(movers[i], destinations[i], out capsules[i])) return false;
                for (int j = 0; j < i; ++j) if (movers[i] == movers[j]) return false;
            }
            for (int i = 0; i < capsules.Length; ++i)
            {
                for (int j = 0; j < i; ++j) if (Intersects(capsules[i], capsules[j])) return false;
                foreach (var hit in Physics.OverlapCapsule(capsules[i].lower, capsules[i].upper,
                    capsules[i].radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    // Both actors are moving as one validated batch; their CURRENT positions do not
                    // block each other's destinations. Their FUTURE bodies were checked above.
                    bool movingActor = false;
                    foreach (var candidate in movers)
                        if (hit.transform.IsChildOf(candidate.transform)) { movingActor = true; break; }
                    if (!movingActor) return false;
                }
            }
            return true;
        }
        public void ResetRoute()
        {
            if (!ClearTargets(actors, spawns))
            { Status = "复位目标不满足主体净空；未移动角色与镜头"; return; }
            for (int i = 0; i < actors.Length; ++i) actors[i].GetComponent<C.CharacterMovement>().Teleport(spawns[i]);
            ReleaseCamera(); if (map != null) map.ResetView();
            Status = "已沿原Motor回到出生点；遮挡需求自然恢复";
        }
        void ReleaseCamera()
        {
            if (cameraOwned && map != null)
            {
                if (!map.followParticipant) map.followParticipant = beforeFollow;
                if (map.fixedTarget == writtenTarget) map.fixedTarget = beforeTarget;
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
