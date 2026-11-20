// 独立横版训练场的试验生命周期。所有出招、受击、资源和运动由正式角色/敌人组件推进。
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.Enemies;
using LabEnemy = Ossastra.Enemies.EnemyRoot;
using Ossastra.UserInterface;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Development
{
    public enum AttackLabTargetMode { Whiff, Fixed, Normal }
    public enum AttackLabStation { Flat, Ledge, Wall }

    [DefaultExecutionOrder(900)]
    [RequireComponent(typeof(AttackLabTrace))]
    public sealed class AttackLabSession : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator party;
        [SerializeField] private LabEnemy target;
        [SerializeField] private SideViewCameraFollow follow;
        [SerializeField] private Transform playerPlatform, targetPlatform, wall;
        private readonly Dictionary<string, Action> equipmentBaselines = new Dictionary<string, Action>();
        private ActorRoot trialActor;
        public CharacterControlCoordinator Party => party;
        public ActorRoot Actor => party == null ? null : party.Current;
        public LabEnemy Target => target;
        public AttackLabTrace Trace { get; private set; }
        public int Generation { get; private set; }
        public string Version { get; private set; } = "基线";
        public string Status { get; private set; } = "正在连接正式角色";
        public AttackLabTargetMode Condition { get; set; } = AttackLabTargetMode.Fixed;
        public AttackLabStation Station { get; set; } = AttackLabStation.Flat;
        public float Distance { get; set; } = 1.4f;
        public float Height { get; set; }
        public bool FacingRight { get; set; } = true;
        public bool EnemyAttacks { get; set; }
        public string WeaponLabel => Actor?.GetComponent<CharacterEquipment>()?.RightDefinition?.name ?? "未装配主手";
        private CharacterAbilities Ability => Actor == null ? null : Actor.GetComponent<CharacterAbilities>();
        private int observedSkill = -1;

        public bool SelectSkillSlot(int slot)
        {
            if (slot < 0 || slot >= 3 || Ability?.SkillDefinition(slot) == null) return Fail("当前主手未装配该技能。");
            observedSkill = slot;
            return true;
        }
        public int SkillSlot
        {
            get
            {
                var abilities = Ability;
                if (observedSkill >= 0 && abilities?.SkillDefinition(observedSkill) != null) return observedSkill;
                if (abilities != null)
                    for (int i = 0; i < 3; i++)
                        if (abilities.SkillDefinition(i) != null) return i;
                return -1;
            }
        }
        public string SkillLabel => SkillSlot < 0 ? "未装配技能" :
            new[] { "Q", "E", "R" }[SkillSlot] + " · " + Ability.SkillDefinition(SkillSlot).definitionId;

        public void Configure(CharacterControlCoordinator control, LabEnemy dummy, SideViewCameraFollow cameraFollow,
            Transform playerStand, Transform enemyStand, Transform wallFixture)
        {
            party = control;
            target = dummy;
            follow = cameraFollow;
            playerPlatform = playerStand;
            targetPlatform = enemyStand;
            wall = wallFixture;
        }

        private void Awake() => Trace = GetComponent<AttackLabTrace>();
        private void Start() => ResetTrial("基线");

        private void LateUpdate()
        {
            // Game 窗口正式切控和调试台按钮共用同一代际边界。
            if (Actor != null && Actor != trialActor) ResetTrial("基线");
        }

        public bool SwitchActor(int index)
        {
            if (party == null || index < 0 || index >= party.Characters.Count) return Fail("角色槽无效。");
            var next = party.Characters[index];
            if (next == null || !party.TryRestoreControl(next.ActorId)) return Fail("当前无法交接角色。");
            return ResetTrial("基线");
        }

        public bool ResetTrial(string version)
        {
            if (!Application.isPlaying || party == null || target == null || Trace == null ||
                playerPlatform == null || targetPlatform == null || wall == null)
                return Fail("训练场缺少角色、目标、采集器或地形装配。");
            if (Actor == null)
            {
                foreach (var member in party.Characters)
                    if (member != null && member.Vitals?.Health != null) member.Vitals.RestoreForDevelopment();
                party.RestoreControlForDevelopment();
            }
            if (Actor == null || Actor.StateArbitrator?.Combat?.Runtime == null)
                return Fail("正式角色控制尚未就绪。");

            Trace.EndTrial();
            if (Actor != trialActor) observedSkill = -1;
            party.GetComponent<CharacterSwitchLink>()?.Interrupt();
            var facing = FacingRight ? Vector3.right : Vector3.left;
            var playerHeight = Mathf.Max(0, -Height) + (Station == AttackLabStation.Ledge ? .65f : 0f);
            var targetHeight = playerHeight + Height;
            var playerStart = new Vector3(0, playerHeight + .035f, 0);
            // 墙面调试把实体墙放在双方之间。给双胶囊与墙体留出实际碰撞间隔。
            float minDistance = Station == AttackLabStation.Wall ? 2.15f : .25f;
            float requestedDistance = Distance;
            Distance = Mathf.Clamp(Distance, minDistance, 12f);
            var enemyStart = new Vector3(facing.x * Distance, targetHeight + .035f, 0);
            // 边缘情景的长台在玩家身后收口，目标正对台沿。其他高度用独立窄托台支撑。
            float width = Station == AttackLabStation.Ledge ? 4f : .65f;
            SetFixture(playerPlatform, new Vector3((.325f-width*.5f)*facing.x, playerHeight*.5f, 0),
                new Vector3(width, playerHeight, 4));
            SetFixture(targetPlatform, new Vector3(enemyStart.x, targetHeight*.5f, 0),
                new Vector3(.65f, targetHeight, 4));
            SetFixture(wall, new Vector3(facing.x * .98f, 2.5f, 0),
                new Vector3(.75f, Station == AttackLabStation.Wall ? 5f : 0f, 4));
            Physics.SyncTransforms();

            foreach (var member in party.Characters)
            {
                if (member == null) continue;
                var state = member.GetComponent<CharacterEquipment>()?.State;
                if (state != null)
                {
                    string key = member.ActorId + "/" + state.RightInstanceId + "/" + state.LeftInstanceId;
                    if (!equipmentBaselines.TryGetValue(key, out var restore))
                        equipmentBaselines[key] = restore = state.CaptureAttackLabBaseline();
                    member.ResetAttackLabActor(member == Actor ? playerStart : new Vector3(-35, .035f, 0), facing);
                    restore();
                }
                else member.ResetAttackLabActor(member == Actor ? playerStart : new Vector3(-35, .035f, 0), facing);
                member.SetAttackRestriction(this, member != Actor, "练习场未出场");
            }

            Actor.GetComponent<CharacterInputHandler>()?.InputSources.BeginAttackLabTrial(FacingRight);
            target.gameObject.SetActive(true);
            target.RestoreForDevelopment();
            target.Vitals.SpatialPlane = CombatPlane.SideXY;
            target.GetComponent<CombatStatusEffects>()?.Clear();
            var movement = target.GetComponent<EnemyMovement>();
            if (movement != null) { movement.Stop(); movement.enabled = Condition == AttackLabTargetMode.Normal; }
            var controller = target.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            target.transform.position = enemyStart;
            if (controller != null) controller.enabled = true;
            target.AttacksEnabled = Condition != AttackLabTargetMode.Whiff && EnemyAttacks;
            target.gameObject.SetActive(Condition != AttackLabTargetMode.Whiff);
            Physics.SyncTransforms();

            var feedback = FindObjectOfType<CombatFeedbackView>();
            if (feedback != null && feedback.isActiveAndEnabled) { feedback.enabled = false; feedback.enabled = true; }
            follow?.ResetAttackLabCamera();
            trialActor = Actor;
            Version = string.IsNullOrEmpty(version) ? "基线" : version;
            Trace.BeginTrial(Actor, ++Generation, Version);
            Status = "试验 " + Generation + " 已就绪 · " + SkillLabel;
            if (requestedDistance < minDistance)
                Status += " · 墙体与碰撞体占位，距离已调整为 " + Distance.ToString("0.##");
            return true;
        }

        private static void SetFixture(Transform fixture, Vector3 position, Vector3 size)
        {
            fixture.gameObject.SetActive(size.y > .001f);
            if (!fixture.gameObject.activeSelf) return;
            fixture.position = position;
            fixture.localScale = size;
        }

        public bool RequestAttack()
        {
            Trace?.ObserveTestRequest("普攻");
            var combat = Actor?.StateArbitrator?.Combat;
            if (combat?.Runtime == null || !Actor.HasPlayerInput) return Fail("未获得正式玩家输入权。");
            PrepareShortcutFacing();
            var before = combat.Runtime.ActionId;
            bool buffered = combat.HasBufferedAttack;
            combat.RequestInput(new CharacterInputFrame { hasAttackEdges = true });
            combat.RequestInput(new CharacterInputFrame { hasAttackEdges = true, attackPressed = true, attackHeld = true });
            combat.RequestInput(new CharacterInputFrame { hasAttackEdges = true, attackReleased = true });
            bool accepted = combat.Runtime.ActionId != before || (!buffered && combat.HasBufferedAttack);
            Status = accepted ? (combat.HasBufferedAttack ? "普攻进入缓冲。" : combat.LastFact) : "普攻未受理 · " + combat.LastFact;
            return accepted;
        }

        public bool RequestSkill()
        {
            Trace?.ObserveTestRequest("代表技能");
            int slot = SkillSlot;
            if (slot < 0) return Fail("当前角色没有装备代表技能。");
            if (!Actor.HasPlayerInput) return Fail("未获得正式玩家输入权。");
            PrepareShortcutFacing();
            bool accepted = Ability.RequestSkill(slot, CharacterActionSource.UserInterface);
            Status = Ability.LastResult;
            return accepted;
        }

        // 面板按钮注明试验朝向。重建本次意图，让后续的真实设备输入仍可正常接管。
        private void PrepareShortcutFacing()
        {
            if (Actor.ActionActive) return;
            Actor.GetComponent<CharacterInputHandler>().InputSources.BeginAttackLabTrial(FacingRight);
            Actor.GetComponent<CharacterOrientation>().InitializeArrivalFacing(FacingRight ? Vector3.right : Vector3.left);
        }

        private bool Fail(string reason) { Status = reason; return false; }
        private void OnDisable()
        {
            Trace?.EndTrial();
            if (party == null) return;
            foreach (var member in party.Characters)
                if (member != null && member.DataBus != null) member.SetAttackRestriction(this, false);
        }
    }
}
