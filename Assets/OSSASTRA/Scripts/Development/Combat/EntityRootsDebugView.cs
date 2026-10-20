/*
 * 脚本：EntityRootsDebugView
 * 功能：三根验证场的真实NPC交互消费者，展示成功回执和明确拒绝原因。
 * 边界：仅Development，不修改NPC计数，不构造正式对话/商店，不增加玩家输入映射。
 * 协作：当前玩家只作为请求来源；NpcRoot公开入口完成资格判断，事件反馈可观察。
 */
using Ossastra.Characters;
using Ossastra.Npcs;
using UnityEngine;
namespace Ossastra.Development
{
    public sealed class EntityRootsDebugView : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private NpcRoot npc;
        private readonly Rect panel=new Rect(490,390,650,120);
        private string lastReply="靠近独立圆柱NPC后点击交互；范围2单位";
        public int ObservedReplies { get; private set; }
        private void OnEnable()
        {
            if(npc!=null) npc.Replied+=OnReplied;
            Register(true);
        }
        private void Start() { Register(true); }
        private void OnDisable()
        {
            if(npc!=null) npc.Replied-=OnReplied;
            Register(false);
        }
        private void Register(bool add)
        {
            if(coordinator==null) return;
            foreach(var character in coordinator.Characters)
            {
                if(character==null) continue;
                var input=character.GetComponent<CharacterInputHandler>();
                if(input==null) continue;
                if(add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this);
            }
        }
        private void OnReplied(NpcReply reply) { ObservedReplies++; lastReply=reply.npcId+"：NPC已回应 #"+reply.count; }
        public NpcReply RequestInteraction()
        {
            var current=coordinator==null?null:coordinator.Current;
            if(npc==null) return new NpcReply(false,"","","npc-missing",0);
            var reply=npc.TryInteract(current==null?"":current.ActorId,current==null?Vector3.zero:current.transform.position,
                current!=null && current.IsAlive && current.isActiveAndEnabled);
            if(!reply.accepted) lastReply="交互拒绝："+reply.reason;
            return reply;
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(panel,GUI.skin.box);
            GUILayout.Label("Development Only · 独立NPC交互 / 不参与战斗");
            if(GUILayout.Button("向NPC发起交互请求")) RequestInteraction();
            GUILayout.Label(lastReply+" · 已观察事件 "+ObservedReplies);
            GUILayout.EndArea();
        }
    }
}
