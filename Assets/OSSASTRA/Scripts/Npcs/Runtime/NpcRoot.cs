/*
 * 脚本：NpcRoot
 * 功能：NPC独立组合根，持有唯一身份和自身交互模块，提供公开请求/回应事件。
 * 边界：无玩家Root/敌人根/Health/InputAction/动作时钟，不实现生活或商店系统。
 * 协作：外部请求到根检查空间/可用性，再由NpcInteraction产生真实回执；销毁撤订阅。
 */
using System;
using UnityEngine;
namespace Ossastra.Npcs
{
    public sealed class NpcRoot : MonoBehaviour
    {
        [SerializeField] private string npcId="npc-structure-sample";
        [SerializeField, Min(.1f)] private float interactionRange=2f;
        private NpcInteraction interaction;
        private Ossastra.AnimationPresentation.IEntityPresentation presentation;
        private void LateUpdate() { presentation?.Present(); }
        public string NpcId => npcId;
        public int InteractionCount => interaction==null?0:interaction.Count;
        public event Action<NpcReply> Replied;
        private void Awake()
        {
            presentation=GetComponent<NpcSpineView>();
            try { interaction=new NpcInteraction(npcId,interactionRange); interaction.Replied+=OnReplied; }
            catch(ArgumentException error) { Debug.LogError("NPC configuration: "+error.Message,this); enabled=false; }
        }
        public NpcReply TryInteract(string actorId,Vector3 actorPosition,bool actorAvailable)
        {
            if(interaction==null) return new NpcReply(false,npcId,actorId,"not-initialized",0);
            return interaction.TryInteract(actorId,(actorPosition-transform.position).sqrMagnitude,
                isActiveAndEnabled && actorAvailable);
        }
        private void OnReplied(NpcReply reply) { Replied?.Invoke(reply); }
        private void OnDestroy()
        {
            if(interaction!=null) { interaction.Replied-=OnReplied; interaction.Dispose(); }
            Replied=null;
        }
    }
}
