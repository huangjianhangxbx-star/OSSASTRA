/*
 * 脚本：CharacterIntentSelector
 * 功能：在Root惰性采样时，只从玩家、伙伴、空意图中选择一个有效Frame。
 * 边界：不决定距离、不采InputAction、不写运动/动作；未绑定跟随时委托原玩家输入。
 * 协作：Root与Bus共用同一选择器；会话代际或当帧变化立刻使旧快照失效。
 */
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    public sealed class CharacterIntentSelector : ICharacterInputSource
    {
        private readonly CharacterRoot character;
        private readonly ICharacterInputSource player;
        private ICharacterControlSession session;
        private ICompanionIntentSource companion;
        private bool managed;
        private CharacterInputFrame frame;
        private CharacterIntentKind kind;
        private int sampledFrame = -1, sampledRevision;

        public CharacterIntentSelector(CharacterRoot character, ICharacterInputSource player)
        { this.character=character; this.player=player; }
        public int ControlRevision => Alive(session)?session.ControlRevision:0;
        public bool IsConfigured => player != null && player.IsConfigured;
        private bool ValidSample => sampledFrame == Time.frameCount && character != null && character.IsAlive && character.isActiveAndEnabled &&
            (!managed || (Alive(session) && session.IsIntentSamplingAllowed && session.ControlRevision == sampledRevision)) &&
            // 即使旧场未绑定Director，玩家适配器被撤权也必须立即使外层帧归零。
            (kind != CharacterIntentKind.Player || !(player is ICharacterInputAvailability availability) || availability.HasPlayerInput) &&
            (kind != CharacterIntentKind.Companion || (Alive(companion) && companion.IsAvailable));
        public CharacterInputFrame CurrentFrame => ValidSample ? frame : default;
        public CharacterIntentKind EffectiveKind => ValidSample ? kind : CharacterIntentKind.Neutral;

        public bool CanBind(ICharacterControlSession authority, ICompanionIntentSource source)
            => Alive(authority) && Alive(source) && (!managed || !Alive(companion) || ReferenceEquals(companion,source));

        /// <summary>只允许同一伙伴提供者重绑或已销毁提供者被替代；不抢占活owner。</summary>
        public bool Bind(ICharacterControlSession authority, ICompanionIntentSource source)
        {
            if (!CanBind(authority,source)) return false;
            session=authority; companion=source; managed=true; Clear(); return true;
        }

        public void Clear(ICompanionIntentSource owner = null)
        {
            if (owner != null && !ReferenceEquals(owner,companion)) return;
            frame=default; kind=CharacterIntentKind.Neutral; sampledFrame=-1;
        }

        public CharacterInputFrame Read()
        {
            Clear();
            if (character == null || !character.IsAlive || !character.isActiveAndEnabled) return default;
            var selectedKind=CharacterIntentPolicy.Select(managed, Alive(session) && session.IsIntentSamplingAllowed,
                Alive(session) && session.Current == character, Alive(companion) && companion.IsAvailable);
            if (selectedKind == CharacterIntentKind.Player) frame=player.Read();
            else if (selectedKind == CharacterIntentKind.Companion)
            {
                // 伙伴即使给了其它字段，也只准传递move；不能借AI帧自动攻击、取消或暂停。
                var candidate=character.CombatMove != null && character.CombatMove.TryGetCombatMove(out var battleMove)
                    ? new CharacterInputFrame { move=battleMove } : companion.ReadCompanionIntent(character,Time.deltaTime);
                frame=new CharacterInputFrame { move=candidate.move };
            }
            // 提供者发现新目标会先撤回旧帧；只有本次读取完成后才发布新来源身份。
            kind=selectedKind;
            sampledFrame=Time.frameCount;
            sampledRevision=Alive(session) ? session.ControlRevision : 0;
            return CurrentFrame;
        }

        private static bool Alive(object value) => value != null && (!(value is Object unityObject) || unityObject != null);
    }
}
