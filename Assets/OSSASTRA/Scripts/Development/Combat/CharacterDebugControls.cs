/*
 * 脚本：CharacterDebugControls
 * 功能：为单个角色提供合法／非法／清理／检查的开发入口。
 * 边界：开发命令调用真实Root接口；不是正式UI，Root不引用本组件。
 * 协作：读取该角色Bus快照；注册GUI阻挡区；禁用只解除自己的禁攻owner。
 */
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.Development
{
    [DefaultExecutionOrder(100)]
    public sealed class CharacterDebugControls : MonoBehaviour
    {
        [SerializeField] private Ossastra.Characters.CharacterRoot character;
        [SerializeField] private CombatTestTarget target;
        private CharacterInputHandler input;
        private bool blocked;
        private static readonly Rect panel = new Rect(12, 12, 400, 270);

        private void OnEnable()
        {
            if (character == null) character = GetComponent<Ossastra.Characters.CharacterRoot>();
            input = GetComponent<CharacterInputHandler>();
            if (input != null) input.RegisterPointerRegion(this, panel);
        }
        private void OnDisable()
        {
            if (input != null) input.RemovePointerRegion(this);
            if (character != null) character.SetAttackRestriction(this, false);
            blocked = false;
        }
        private void Update()
        {
            if (character == null || character.DataBus == null) return;
            var command = character.DataBus.Input.debugCommand;
            if (command != 0) DebugCommand(command);
        }
        public void DebugCommand(int command)
        {
            if (character == null || character.DataBus == null) return;
            if (command == 1 && target != null)
            {
                character.CancelAction("debug-near-target");
                target.RestoreHealth();
                var movement = GetComponent<CharacterMovement>();
                var orientation = GetComponent<CharacterOrientation>();
                if (movement != null) movement.Teleport(target.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
                if (orientation != null) { orientation.OrientTowards(target.transform.position); orientation.FaceAttackDirection(); }
            }
            if (command == 2)
            {
                blocked = !blocked;
                character.SetAttackRestriction(this, blocked, "debug-attack-block");
            }
            if (command == 3)
            {
                character.CancelAction("debug-clear");
                character.SetAttackRestriction(this, false);
                character.SetPaused(false);
                blocked = false;
                if (target != null) target.RestoreHealth();
            }
            if (command == 4) Debug.Log("Character " + character.DataBus.ActorId + " " + character.LastFact, character);
        }
        private void OnGUI()
        {
            if (character == null) return;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("角色候选 · WASD移动 / 左键普攻 / X取消 / P暂停");
            GUILayout.Label(character.LastFact);
            GUILayout.Label("暂停: " + character.IsPaused + " | 禁攻: " + blocked);
            if (GUILayout.Button("F1 合法状态：恢复靶并站到攻击距离")) DebugCommand(1);
            if (GUILayout.Button("F2 非法状态：切换owner禁攻")) DebugCommand(2);
            if (GUILayout.Button("F3 清理：取消／解除禁攻／恢复靶")) DebugCommand(3);
            if (GUILayout.Button("F4 检查：输出动作状态")) DebugCommand(4);
            GUILayout.EndArea();
        }
    }
}