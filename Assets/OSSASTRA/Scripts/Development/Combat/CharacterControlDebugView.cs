/*
 * 脚本：CharacterControlDebugView
 * 功能：显示唯一实控来源与两角色的身份、位置、动作和许可，提供切换验证入口。
 * 边界：仅开发展示，不保存第二份选中状态，不从显示内容反向决定控制权。
 * 协作：读取协调者和各角色Bus；按钮调用真实SwitchNext，GUI区域登记到输入以阻挡穿透。
 */
using Ossastra.Characters;
using UnityEngine;

namespace Ossastra.Development
{
    public sealed class CharacterControlDebugView : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        private static readonly Rect panel = new Rect(12, 294, 430, 220);
        private void OnEnable()
        {
            if (coordinator == null) return;
            foreach (var character in coordinator.Characters)
            {
                var input = character == null ? null : character.GetComponent<CharacterInputHandler>();
                if (input != null) input.RegisterPointerRegion(this, panel);
            }
        }
        private void OnDisable()
        {
            if (coordinator == null) return;
            foreach (var character in coordinator.Characters)
            {
                var input = character == null ? null : character.GetComponent<CharacterInputHandler>();
                if (input != null) input.RemovePointerRegion(this);
            }
        }
        private void OnGUI()
        {
            if (coordinator == null) return;
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label("双实例控制验证候选 · 临时同素材 · Tab / 手柄Select切换");
            GUILayout.Label(coordinator.Status);
            foreach (var character in coordinator.Characters)
            {
                if (character == null) { GUILayout.Label("角色引用缺失"); continue; }
                var bus = character.DataBus;
                var input = character.GetComponent<CharacterInputHandler>();
                GUILayout.Label(character.ActorId + " | 玩家许可 " + (input != null && input.HasPlayerInput) +
                    " | " + character.transform.position.ToString("F2"));
                GUILayout.Label("动作 " + (bus == null ? "未初始化" : bus.ActionId.ToString("N").Substring(0, 8)) +
                    " | " + character.LastFact);
            }
            if (GUILayout.Button("切换到下一个可控制角色")) coordinator.SwitchNext();
            GUILayout.EndArea();
        }
    }
}
