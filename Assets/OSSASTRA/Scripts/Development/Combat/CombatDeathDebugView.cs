/*
 * 脚本：CombatDeathDebugView
 * 功能：开发HP/可选架势读数、正式结算入口注入受击和同实体重置，便于复验生命周期。
 * 边界：Development Only，不代表自动返场；不直接写HP私有字段、不伪造UI成功。
 * 协作：注入独立开发来源的Released→Health→目标通知；恢复顺序为取消/恢复原对象/重选实控。
 */
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using Ossastra.Characters;
using Ossastra.Enemies;
using UnityEngine;
namespace Ossastra.Development
{
    public sealed class CombatDeathDebugView : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private CompanionFollowDirector director;
        [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
        private Vector3[] spawns;
        private Vector2 postureScroll;
        private static readonly Rect panel=new Rect(490,12,650,370);
        private void Start() { spawns=coordinator.Characters.Select(c=>c.transform.position).ToArray(); Register(true); }
        private void Register(bool add)
        {
            if (coordinator==null) return;
            foreach (var c in coordinator.Characters)
            { var input=c==null?null:c.GetComponent<CharacterInputHandler>(); if (input==null) continue; if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this); }
        }
        private void OnDisable() { Register(false); }
        private void OnEnable() { if (spawns!=null) Register(true); }

        public static void ApplyDevelopmentHit(CombatVitals target, int damage)
        {
            if (target==null || !target.IsAlive || damage<=0) return;
            var runtime=new BasicActionRuntime("development-hit-probe");
            runtime.Request(new BasicAttackDefinition(0,0,0,.01,damage),"development-only",new ControlAuthority());
            var release=runtime.Advance(.001,false).Single(f=>f.Release!=null).Release;
            foreach (var outcome in release.Resolve(new[]{target.Health})) target.OnHitResolved(outcome);
        }
        public void RestartForDevelopment()
        {
            if (spawns==null) return;
            director.enabled=false;
            for (var i=0;i<coordinator.Characters.Count;i++)
            {
                var c=coordinator.Characters[i]; c.CancelAction("development-reset"); 
                c.Vitals.RestoreForDevelopment();
                var posture=c.GetComponent<CombatPosture>();
                if(posture!=null) posture.ResetState();
                c.GetComponent<CharacterMovement>().Teleport(spawns[i]);
            }
            enemy.RestoreForDevelopment(); coordinator.RestoreControlForDevelopment(); director.enabled=true;
        }
        private void OnGUI()
        {
            if (coordinator==null || enemy==null) return;
            GUILayout.BeginArea(panel,GUI.skin.box);
            var showPosture=enemy.GetComponent<CombatPosture>()!=null;
            if(showPosture) postureScroll=GUILayout.BeginScrollView(postureScroll);
            GUILayout.Label("Development Only · 双向攻防 / 真死亡 · 返场由场景服务决定");
            GUILayout.Label("Current: "+(coordinator.Current==null?"无可控制角色":coordinator.Current.ActorId));
            foreach (var c in coordinator.Characters)
            {
                var v=c.Vitals; GUILayout.Label(c.ActorId+" HP "+v.Health.Current+"/"+v.Health.Maximum+" "+(v.IsAlive?"Alive":"Dead")+" "+c.EffectiveIntentKind+" | "+c.LastFact);
                DrawPosture(c.GetComponent<CombatPosture>(),v.LastResult);
                if (GUILayout.Button("合法受击检查："+c.ActorId+" -20HP")) ApplyDevelopmentHit(v,20);
                if (GUILayout.Button("死亡边界："+c.ActorId+" 致死一击")) ApplyDevelopmentHit(v,v.Health.Maximum);
            }
            GUILayout.Label("Enemy HP "+enemy.Vitals.Health.Current+"/"+enemy.Vitals.Health.Maximum+" | "+enemy.LastFact);
            DrawPosture(enemy.GetComponent<CombatPosture>(),enemy.LastHit);
            if (GUILayout.Button("敌人攻击："+(enemy.AttacksEnabled?"开启（点击关闭）":"关闭（点击开启）"))) enemy.AttacksEnabled=!enemy.AttacksEnabled;
            if (GUILayout.Button("Development Only：同实体重新开始测试")) RestartForDevelopment();
            if(showPosture) GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        // 只读真实能力；旧场没挂架势时仍显示原命中信息，不伪造一份UI状态。
        private static void DrawPosture(CombatPosture posture,string fallback)
        {
            if(posture==null) { GUILayout.Label(fallback); return; }
            GUILayout.Label("架势 "+posture.Current.ToString("F1")+"/"+posture.Maximum+" · "+posture.Phase+
                " · 硬直 "+posture.StaggerRemaining.ToString("F2")+"s · 等待 "+posture.RegenDelayRemaining.ToString("F2")+"s");
            GUILayout.Label(new GUIContent("来源："+posture.LastImpact,posture.LastImpact));
        }
    }
}

