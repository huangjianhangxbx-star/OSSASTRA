/* 玩家会话级来源记忆。保留移动/战斗上下文归属；手柄方向来自移动，右杆不夺取上下文。
 * 只处理经过配置Action采样和UI分类的候选，不读硬件、不生成玩法请求。
 */
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.Characters
{
    public enum PlayerInputSource { None, KeyboardMouse, Gamepad }

    public sealed class PlayerInputSourceState
    {
        public int MoveDeviceId { get; private set; } = -1;
        public int AimDeviceId { get; private set; } = -1;
        public PlayerInputSource AimSource { get; private set; }
        public Vector2 LastAimDirection { get; private set; } = Vector2.right;
        public Vector2 Move { get; private set; }

        public void Resolve(IReadOnlyList<DeviceInputCandidate> devices, bool exploration, bool combat)
        {
            DeviceInputCandidate owner = null, newest = null;
            foreach (var d in devices)
            {
                if (d.DeviceId == MoveDeviceId) owner = d;
                if (exploration && d.MoveActive && d.MoveChanged &&
                    (newest == null || d.MoveTime > newest.MoveTime || d.MoveTime == newest.MoveTime && d.DeviceId == MoveDeviceId)) newest = d;
            }
            if (newest != null) owner = newest;
            if (owner == null || !owner.MoveActive)
            {
                owner = null;
                foreach (var d in devices) if (d.MoveActive && (owner == null || d.DeviceId < owner.DeviceId)) owner = d;
            }
            Move = exploration && owner != null ? owner.Move : Vector2.zero;
            MoveDeviceId = owner?.DeviceId ?? -1;

            DeviceInputCandidate aim = null; int priority = 0; double time = -1;
            foreach (var d in devices)
            {
                int p = combat && d.BattlePressed ? 3 :
                    exploration && d.MoveActive && (d.MoveChanged || AimSource == PlayerInputSource.None) ? 1 : 0;
                double t = p == 3 ? d.BattleTime : d.MoveTime;
                if (p > priority || p > 0 && p == priority && (t > time || t == time && d.DeviceId == AimDeviceId))
                { aim = d; priority = p; time = t; }
            }
            if (aim != null)
            { AimDeviceId = aim.DeviceId; AimSource = aim.IsGamepad ? PlayerInputSource.Gamepad : PlayerInputSource.KeyboardMouse; }
            foreach (var d in devices)
                if (AimSource == PlayerInputSource.Gamepad && d.DeviceId == AimDeviceId)
                {
                    if (d.MoveActive) LastAimDirection = d.Move.normalized;
                }
            if (AimSource == PlayerInputSource.None && Move.sqrMagnitude > .0001f) LastAimDirection = Move.normalized;
        }
    }

    public sealed class DeviceInputCandidate
    {
        public int DeviceId;
        public bool IsGamepad, MoveActive, MoveChanged, AimActive, AimChanged, BattlePressed;
        public double MoveTime, AimTime, BattleTime;
        public Vector2 Move, Aim;
    }
}
