// 调试台从当前装备实例捕获试验起点。仅还原这对手槽实例的易变战斗资源，保留玩家的技能选择。
using System;
using Ossastra.CharacterCore.Abilities;

namespace Ossastra.CharacterCore
{
    public sealed partial class EquipmentState
    {
        public Action CaptureAttackLabBaseline()
        {
            var right = RightRuntime?.CaptureAttackLabResources();
            var left = LeftRuntime?.CaptureAttackLabResources();
            var item = slots?.Left;
            var restoreWear = item?.CaptureAttackLabWear();
            var legacy = Current;
            int legacyDurability = legacy?.durability ?? 0;
            Guid[] legacyWear = legacy == null ? null : new System.Collections.Generic.List<Guid>(legacy.wear).ToArray();
            return () =>
            {
                right?.Invoke();
                left?.Invoke();
                restoreWear?.Invoke();
                if (legacy == null) return;
                legacy.durability = legacyDurability;
                legacy.wear.Clear();
                legacy.wear.UnionWith(legacyWear);
            };
        }
    }

    public sealed partial class EquipmentItemState
    {
        internal Action CaptureAttackLabWear()
        {
            int initialDurability = Durability;
            var initialWear = new System.Collections.Generic.List<Guid>(wear);
            return () => { Durability = initialDurability; wear.Clear(); wear.UnionWith(initialWear); };
        }
    }

    public sealed partial class SkillCooldownBook
    {
        internal Action CaptureAttackLabCooldowns()
        {
            var initial = CapturePersistent();
            return () => { deadlines.Clear(); accepted.Clear(); RestorePersistent(initial); };
        }
    }
}

namespace Ossastra.CharacterCore.Abilities
{
    public sealed partial class MagazineState
    {
        internal Action CaptureAttackLabMagazine()
        {
            var initial = CapturePersistent();
            return () => { shots.Clear(); reloads.Clear(); RestorePersistent(initial); };
        }
    }

    public sealed partial class EquipmentAbilityState
    {
        internal Action CaptureAttackLabResources()
        {
            var restoreCooldowns = Cooldowns.CaptureAttackLabCooldowns();
            var restoreMagazine = Magazine.CaptureAttackLabMagazine();
            var initialFrost = Frost.CapturePersistent();
            var initialShots = ShotEnhancements.CapturePersistent();
            double equipped = EquippedTime, controlled = ControlledTime;
            double cooldown = cooldownTime, controlledCooldown = controlledCooldownTime;
            return () =>
            {
                EquippedTime = equipped;
                ControlledTime = controlled;
                cooldownTime = cooldown;
                controlledCooldownTime = controlledCooldown;
                restoreCooldowns();
                restoreMagazine();
                Frost.RestorePersistent(initialFrost);
                ShotEnhancements.Clear();
                ShotEnhancements.RestorePersistent(initialShots, Sources);
            };
        }
    }
}
