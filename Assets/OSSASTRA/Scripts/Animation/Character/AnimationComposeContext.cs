/* 原合成上下文的只读适配：条件只访问真实所有者，不复制旧可写Bus。 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using UnityEngine;
namespace Ossastra.CharacterAnimation
{
    public sealed class AnimationComposeContext
    {
        public Ossastra.Characters.CharacterRoot root;
        public Ossastra.Characters.CharacterDataBus dataBus;
        public IReadOnlyDictionary<AnimationIntentSourceStateMachine, AnimationIntent> activeIntents;
        public Ossastra.Characters.EquipmentDefinitionSO sourceEquipment;
        public string target = "body";
        public int configurationVersion;
        private static readonly HashSet<string> warned = new HashSet<string>();
        public bool HasWeapon => sourceEquipment != null;
        public bool HasIntent(AnimationIntentSourceStateMachine source) => GetIntent(source) != null;
        public AnimationIntent GetIntent(AnimationIntentSourceStateMachine source)
        {
            if (source == AnimationIntentSourceStateMachine.None || activeIntents == null) return null;
            return activeIntents.TryGetValue(source, out var intent) && intent != null && intent.IsValid(root, target) ? intent : null;
        }
        public bool TryRead(string key, out object value)
        {
            value = null;
            if (root == null || dataBus == null || string.IsNullOrEmpty(key)) return false;
            switch (key)
            {
                case "IsMoving": case "isMoving": value = dataBus.IsMoving; break;
                case "currentMoveSpeed": case "Speed": value = dataBus.Velocity.magnitude; break;
                case "ActionActive": value = dataBus.ActionActive; break;
                case "MoveReady": value = dataBus.MoveReady; break;
                case "AttackReady": value = dataBus.AttackReady; break;
                case "AllowsMovement": value = dataBus.AllowsMovement; break;
                case "AllowsAttack": value = dataBus.AllowsAttack; break;
                case "IsAlive": value = root.IsAlive; break;
                case "HasPlayerInput": value = root.HasPlayerInput; break;
                case "VisualFacingX": value = dataBus.VisualFacingX; break;
                case "AttackStage": value = root.AttackStage; break;
                case "EffectiveIntentKind": value = root.EffectiveIntentKind; break;
                case "LastActionSource": value = root.LastActionSource; break;
                case "Health": if (root.Vitals != null) value = root.Vitals.Health.Current; break;
                case "Stamina": if (dataBus.Stamina != null) value = dataBus.Stamina.Current; break;
            }
            if (value != null) return true;
            var warning = root.ActorId + ":" + configurationVersion + ":" + key;
            if (warned.Add(warning)) Debug.LogWarning("动画条件未支持的只读事实：" + key + "；配置版本 " + configurationVersion, root);
            return false;
        }
    }
}
