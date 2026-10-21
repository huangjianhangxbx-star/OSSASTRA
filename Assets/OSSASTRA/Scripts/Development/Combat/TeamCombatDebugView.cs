/* 双人战斗开发观察：只读交战、候选、动作来源、敌预警及二维fallback。
 * AI开关仅启停本体能力，重测使用既有开发入口；不写HP或伪造入战成功。
 * 头顶只给敌/NPC测试身份，绝不添加友方头顶血条。正式美术缺失明确标记。
 */
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.AnimationPresentation;
using UnityEngine;
namespace Ossastra.Development
{
    public sealed class TeamCombatDebugView : MonoBehaviour
    {
        [SerializeField] private CombatEngagementSession engagement;
        [SerializeField] private Ossastra.Npcs.NpcRoot npc;
        private readonly Rect panel=new Rect(1570,12,410,340);
        private void OnEnable()
        {
            if(engagement!=null&&engagement.Coordinator!=null)
                foreach(var c in engagement.Coordinator.Characters)if(c!=null)c.GetComponent<CharacterInputHandler>()?.RegisterPointerRegion(this,panel);
        }
        private void OnDisable()
        {
            if(engagement!=null&&engagement.Coordinator!=null)
                foreach(var c in engagement.Coordinator.Characters)if(c!=null)c.GetComponent<CharacterInputHandler>()?.RemovePointerRegion(this);
        }
        private void OnGUI()
        {
            if(engagement==null||engagement.Coordinator==null)return;
            GUILayout.BeginArea(panel,GUI.skin.box);
            GUILayout.Label("ROADMAP02 / 技术试玩 / SAMPLE");
            GUILayout.Label("交战="+engagement.Engaged+" 平静="+engagement.QuietTime.ToString("F1"));
            foreach(var c in engagement.Coordinator.Characters)
            {
                if(c==null)continue;var brain=c.GetComponent<CompanionCombatBrain>();
                GUILayout.Label(c.ActorId+" 来源="+c.LastActionSource);
                if(brain!=null)
                {
                    GUILayout.Label("目标="+brain.TargetId+" / "+brain.Reason);
                    if(GUILayout.Button(new GUIContent(brain.enabled?"停用该角色自治":"启用该角色自治","仅启停本体候选；不改变Current或生命")))brain.enabled=!brain.enabled;
                }
            }
            foreach(var e in engagement.Enemies)
            {
                if(e==null)continue;var p=e.GetComponentInChildren<EntitySpinePresenter>();
                GUILayout.Label("敌预警="+e.WarningVisible+" 已释放="+e.AttackReleased);
                if(p!=null)GUILayout.Label(p.Semantic+" → "+p.CurrentClip+" "+p.FallbackReason);
            }
            GUILayout.Label("2D为本地现有Spine替身；正式敌人美术待补");
            GUILayout.EndArea();
            var camera=Camera.main;if(camera==null)return;
            foreach(var e in engagement.Enemies)if(e!=null&&e.Vitals!=null&&e.Vitals.IsAlive)
                Label(camera,e.transform.position+Vector3.up*1.8f,"敌方 TEMP · HP "+e.Vitals.Health.Current,new Color(1,.65f,.5f));
            if(npc!=null)Label(camera,npc.transform.position+Vector3.up*1.9f,"NPC TEMP · 非战斗",Color.white);
        }
        private static void Label(Camera camera,Vector3 world,string text,Color color)
        {
            var p=camera.WorldToScreenPoint(world);if(p.z<=0)return;
            var old=GUI.color;GUI.color=color;GUI.Label(new Rect(p.x-90,Screen.height-p.y-15,220,30),text);GUI.color=old;
        }
    }
}
