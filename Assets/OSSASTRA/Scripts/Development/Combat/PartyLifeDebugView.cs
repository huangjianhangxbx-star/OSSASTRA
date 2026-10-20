/*
 * 脚本：PartyLifeDebugView
 * 功能：只读呈现真实场景返场时钟、成员生命/代际；提供明确开发致死入口。
 * 边界：不保存生命真值、不执行生产恢复，生产返场由PartyRespawnCoordinator负责。
 */
using Ossastra.Characters;
using UnityEngine;
namespace Ossastra.Development
{
    public sealed class PartyLifeDebugView : MonoBehaviour
    {
        [SerializeField] private PartyRespawnCoordinator respawn;
        [SerializeField] private CharacterControlCoordinator session;
        private static readonly Rect panel=new Rect(1150,12,410,220);
        private void OnEnable(){Register(true);}
        private void OnDisable(){Register(false);}
        private void Register(bool add)
        {
            if(session==null)return;
            foreach(var root in session.Characters)
            {
                if(root==null)continue;
                var input=root.GetComponent<CharacterInputHandler>();if(input==null)continue;
                if(add)input.RegisterPointerRegion(this,panel);else input.RemovePointerRegion(this);
            }
        }
        private void OnGUI()
        {
            if(respawn==null || session==null)return;
            GUILayout.BeginArea(panel,GUI.skin.box);
            GUILayout.Label("场景内返场 SAMPLE / Development读数");
            GUILayout.Label(respawn.Status+" / 模拟秒 "+respawn.SimulationTime.ToString("F2"));
            for(var i=0;i<session.Characters.Count;i++)
            {
                var root=session.Characters[i];if(root==null)continue;
                GUILayout.Label(root.ActorId+" Alive="+root.IsAlive+" Gen="+root.LifeGeneration+" Current="+(session.Current==root));
                GUILayout.Label("HP="+root.Vitals.Health.Current+" Posture="+root.GetComponent<Ossastra.Combat.CombatPosture>().Current+" Died="+respawn.DiedAt(i).ToString("F2")+" Due="+respawn.DueAt(i).ToString("F2"));
                if(GUILayout.Button(new GUIContent("开发致死："+root.ActorId,"通过真实伤害结算触发死亡；由生产场内返场服务等待恢复。")))
                    CombatDeathDebugView.ApplyDevelopmentHit(root.Vitals,10000);
            }
            GUILayout.EndArea();
        }
    }
}
