/*
 * 脚本：EnemyChaseDebugView
 * 功能：只读有限敌动状态、真实位移和战斗事实；无输入/动作权威。
 */
using Ossastra.Enemies;
using UnityEngine;
namespace Ossastra.Development
{
    public sealed class EnemyChaseDebugView : MonoBehaviour
    {
        [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
        private void OnGUI()
        {
            if(enemy==null)return;
            var brain=enemy.GetComponent<ChaseEnemyBrain>();var movement=enemy.GetComponent<EnemyMovement>();
            if(brain==null||movement==null)return;
            GUILayout.BeginArea(new Rect(1150,390,410,180),GUI.skin.box);
            GUILayout.Label("EN-AI-01 / 胶囊仅技术占位 / SAMPLE");
            GUILayout.Label(brain.Phase+" target="+brain.TargetId);
            GUILayout.Label("实际速度="+movement.Velocity.magnitude.ToString("F2")+" HP="+(enemy.Vitals==null?0:enemy.Vitals.Health.Current));
            GUILayout.Label(enemy.LastFact);GUILayout.Label(enemy.LastHit);
            GUILayout.EndArea();
        }
    }
}
