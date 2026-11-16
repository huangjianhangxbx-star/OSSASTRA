/*
 * 功能：检查现行实控角色架势、恢复与反应，并为真实输入和受击演练准备数值起点。
 * 边界：仅编辑器与开发构建可用；不代替正式伤害、动作或运动入口。
 */
using System.Linq;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.CombatCore;
using UnityEngine;
using Actor = Ossastra.Characters.CharacterRoot;
using PracticeEnemy = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.Development
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterControlCoordinator))]
    public sealed class ActionPostureDebugView : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private PracticeEnemy practiceEnemy;
        private CharacterControlCoordinator session;
        private CharacterInputHandler input;
        private CombatPosture seededPosture;
        private System.Guid seededOwner;
        private bool expanded;
        private string result = "";

        private static readonly Rect Closed = new Rect(12, 305, 152, 30);
        private static readonly Rect Open = new Rect(12, 305, 425, 302);

        private void OnEnable() => session = GetComponent<CharacterControlCoordinator>();

        private void Update()
        {
            var actor = session?.Current;
            var next = actor == null ? null : actor.GetComponent<CharacterInputHandler>();
            if (input != next)
            {
                if (input != null) input.RemovePointerRegion(this);
                input = next;
            }
            if (input != null) input.RegisterPointerRegion(this, expanded ? Open : Closed);
        }

        private void OnDisable()
        {
            if (input != null) input.RemovePointerRegion(this);
            input = null;
            ReleaseSeed();
            expanded = false;
        }

        private void ReleaseSeed()
        {
            if (seededPosture != null) seededPosture.EndActionExposure(seededOwner);
            seededPosture = null;
            seededOwner = System.Guid.Empty;
        }

        private void Seed(Actor actor, CombatPosture posture, bool empty)
        {
            if (actor == null || !actor.IsAlive || posture == null || !posture.UsesActionPosture || posture.IsStaggered)
            {
                result = "请等当前角色存活并完成受击反应，再准备起点";
                return;
            }
            ReleaseSeed();
            actor.CancelAction("development-posture-seed");
            posture.ResetState();
            seededPosture = posture;
            seededOwner = System.Guid.NewGuid();
            // 留住本组缺口，方便从零架势验证输入或真实敌袭，其余缺口仍能自然恢复。
            posture.BeginActionExposure(seededOwner, PostureActionGroup.Locomotion,
                posture.Maximum * (empty ? 1 : .55), false);
            result = empty ? "已设为零架势，可验证合法动作和敌人实际命中" : "已设为半损，其他动作照常演练";
        }

        private void OnGUI()
        {
            if (session == null || !Application.isPlaying) return;
            if (!expanded)
            {
                if (GUI.Button(Closed, new GUIContent("动作架势调试", "查看当前实控角色的唯一架势账本和已结算的反应")))
                    expanded = true;
                return;
            }

            var actor = session.Current;
            var posture = actor == null ? null : actor.GetComponent<CombatPosture>();
            var ledger = posture?.ActionLedger;
            GUILayout.BeginArea(Open, GUI.skin.box);
            if (GUILayout.Button("收起动作架势调试"))
            {
                expanded = false;
                ReleaseSeed();
            }
            if (ledger == null) GUILayout.Label("当前角色未接入动作架势");
            else
            {
                var reaction = actor.StateArbitrator?.Snapshots.FirstOrDefault(snapshot => snapshot.machine == "CharacterReactionStateMachine");
                var movement = actor.GetComponent<CharacterMovement>();
                GUILayout.Label($"{actor.ActorId}　生命 {actor.LifeGeneration}／切控 {actor.ActionControlRevision}");
                GUILayout.Label($"架势 {posture.Current:0.0}/{posture.Maximum:0}　恢复 {ledger.RecoveryPerSecond:0.##}/s"
                    + $"　等待 {ledger.WaitingSeconds:0.##}s　旧体力准入：停用");
                GUILayout.Label($"诊断预置 {ledger.GetDeficit(PostureActionGroup.Locomotion):0.0}　垂直 {ledger.GetDeficit(PostureActionGroup.VerticalTraversal):0.0}"
                    + $"　主手 {ledger.GetDeficit(PostureActionGroup.MainAttack):0.0}"
                    + $"　闪避 {ledger.GetDeficit(PostureActionGroup.Dodge):0.0}　副手 {ledger.GetDeficit(PostureActionGroup.OffhandAction):0.0}");
                GUILayout.Label($"防御 {ledger.GuardDeficit:0.0}（{(ledger.GuardHeld ? "保持" : "松开")}）　受击 {ledger.HitDeficit:0.0}"
                    + $"　恢复占用 {ledger.RecoveryBlockers}／全局 {ledger.GlobalRecoveryBlockers}");
                GUILayout.Label($"反应 {reaction?.state ?? "未绑定"}　落地 {movement?.Grounded}　速度 {movement?.Velocity.ToString("F2")}");
                if (input != null)
                    GUILayout.Label($"输入 {input.CurrentFrame.moveSource}／{input.CurrentFrame.aimSource}　可采样 {input.HasPlayerInput}");
                GUILayout.Label($"上次冲击 {posture.LastImpact}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("准备半损", "只设置缺口。正常走、跳、攻击和防御继续走正式输入。")))
                    Seed(actor, posture, false);
                if (GUILayout.Button(new GUIContent("准备零架势", "让架势归零并保持危机状态。实际命中仍由场景敌人完成。")))
                    Seed(actor, posture, true);
                if (GUILayout.Button(new GUIContent("放开恢复", "释放本面板设置的缺口锁，等待后按正常速率恢复。")))
                {
                    ReleaseSeed();
                    result = "已开放自然恢复";
                }
                if (GUILayout.Button(new GUIContent("重置", "结束开发准备并恢复选中角色的架势。进行中的受击反应结束后可操作。")) &&
                    actor.IsAlive && !posture.IsStaggered)
                {
                    ReleaseSeed();
                    actor.CancelAction("development-posture-reset");
                    posture.ResetState();
                    result = "架势已重置";
                }
                GUILayout.EndHorizontal();
            }
            if (practiceEnemy != null && GUILayout.Button(new GUIContent(practiceEnemy.AttacksEnabled ? "暂停练习敌人攻击" : "启用练习敌人攻击",
                "只调练习敌人的原攻击许可，接触和伤害仍经碰撞及战斗网关。")))
                practiceEnemy.AttacksEnabled = !practiceEnemy.AttacksEnabled;
            GUILayout.Label(result);
            GUILayout.EndArea();
        }
#endif
    }
}
