/*
 * 脚本：CharacterControlCoordinator
 * 功能：保存同组角色唯一的玩家实控对象，并读取Inspector切换输入。
 * 边界：不接管各角色动作时钟、HP或AI；不使用全局单例。
 * 协作：在Root之前更新；玩家与伙伴选择器只读查询许可/代际，不复制Current或选中布尔。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.Characters
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class CharacterControlCoordinator : MonoBehaviour, ICharacterPlayerControlSource, ICharacterControlSession
    {
        [SerializeField] private CharacterRoot[] characters = Array.Empty<CharacterRoot>();
        [SerializeField, Min(0)] private int initialIndex;
        [Header("切换实控角色 / 可在Inspector修改")]
        public InputAction switchAction = new InputAction("SwitchCharacter", InputActionType.Button);
        private readonly AttackIntent switchIntent = new AttackIntent();
        private CharacterInputHandler[] inputs;
        private bool configured, bindingsOwned, focused = true, applicationPaused;
        private int handoverFrame = -1;
        public CharacterRoot Current { get; private set; }
        public IReadOnlyList<CharacterRoot> Characters => Array.AsReadOnly(characters);
        public string Status { get; private set; } = "未初始化";
        public int ControlRevision { get; private set; }
        public bool IsIntentSamplingAllowed => configured && isActiveAndEnabled && focused && !applicationPaused &&
            Current != null && Current.isActiveAndEnabled && Current.DataBus != null && Time.frameCount > handoverFrame;

        private void Awake()
        {
            if (characters == null) characters = Array.Empty<CharacterRoot>();
            inputs = new CharacterInputHandler[characters.Length];
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var sources = new HashSet<CharacterInputHandler>();
            var valid = characters.Length >= 2 && initialIndex < characters.Length && initialIndex >= 0 &&
                switchAction != null && switchAction.bindings.Count > 0;
            // 先完整校验，再绑定；合法成员在配置错误时也归本协调者关闭，不能退回双控。
            for (var i = 0; i < characters.Length; i++)
            {
                var character = characters[i];
                if (character == null) { valid = false; continue; }
                inputs[i] = character.GetComponent<CharacterInputHandler>();
                if (inputs[i] == null || !sources.Add(inputs[i]) ||
                    string.IsNullOrWhiteSpace(character.ActorId) || !identities.Add(character.ActorId)) valid = false;
            }
            foreach (var input in inputs)
                if (input != null && !input.CanBindPlayerControl(this))
                { Status = "输入已归另一协调者；拒绝抢占"; enabled = false; return; }
            foreach (var input in inputs) if (input != null) input.BindPlayerControl(this);
            bindingsOwned = true;
            configured = valid;
            if (!valid) { Status = "角色／身份／输入配置无效，输入关闭"; enabled = false; }
        }

        private void OnEnable()
        {
            switchIntent.SuppressUntilRelease();
            if (!configured) return;
            switchAction.Enable();
            Select(characters[initialIndex]);
        }

        private void Update()
        {
            if (Current != null && (!Current.isActiveAndEnabled || Current.DataBus == null)) Revoke("当前角色不可用");
            var held = switchAction != null && switchAction.enabled && switchAction.IsPressed();
            if (switchIntent.Observe(held) && focused && !applicationPaused) SwitchNext();
        }

        /// <summary>只有Current对应的输入源可消费玩家输入；切换当帧新源禁止采样。</summary>
        public bool IsPlayerInputAllowed(ICharacterInputSource source)
        {
            if (!IsIntentSamplingAllowed) return false;
            for (var i = 0; i < characters.Length; i++)
                if (characters[i] == Current) return ReferenceEquals(inputs[i], source);
            return false;
        }

        /// <summary>场景级切人入口；不会取消、复制或重建任一角色的已接受动作。</summary>
        public bool SwitchNext()
        {
            if (!configured || !isActiveAndEnabled || !focused || applicationPaused) return false;
            var index = Array.IndexOf(characters, Current);
            for (var step = 1; step <= characters.Length; step++)
            {
                var candidate = characters[(index + step) % characters.Length];
                if (candidate != null && candidate.isActiveAndEnabled && candidate.DataBus != null)
                { Select(candidate); return true; }
            }
            Revoke("没有可控制角色");
            return false;
        }

        private void Select(CharacterRoot next)
        {
            // 先撤回旧权威并清所有快照，再授予新身份。整帧禁入避免外部晚调用导致同帧双控。
            Current = null;
            ControlRevision++;
            foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
            handoverFrame = Time.frameCount;
            Current = next;
            Status = next == null ? "无实控" : "玩家输入 → " + next.ActorId;
        }

        private void Revoke(string reason) { Select(null); Status = reason; }
        private void OnDisable()
        {
            if (switchAction != null) switchAction.Disable();
            switchIntent.SuppressUntilRelease();
            // 未取得绑定所有权的失败实例不能清理其它协调者的快照或重武装状态。
            if (bindingsOwned)
            {
                if (configured) Revoke("协调者禁用，输入关闭");
                else foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
            }
        }
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            ControlRevision++;
            switchIntent.SuppressUntilRelease();
            if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
        }
        private void OnApplicationPause(bool value)
        {
            applicationPaused = value;
            ControlRevision++;
            switchIntent.SuppressUntilRelease();
            if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
        }
        // 输入仍保留“曾受管理”的关系；本对象销毁后查询失败关闭，不能突然恢复多个玩家源。
        private void OnDestroy() { if (switchAction != null) switchAction.Dispose(); }
    }
}
