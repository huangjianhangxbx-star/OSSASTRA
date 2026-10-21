/* 脚本：CombatDefense
 * 功能：组合本体可选闪避/盾资格，冻结来袭XZ方向的防御快照。
 * 边界：无HP或时钟权威，不认识CharacterRoot；合法回执后才通知盾磨损。
 */
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatDefense:MonoBehaviour,ICombatDefense
    {
        private ICombatEvasion evasion;
        private ICombatGuard guard;
        private void Awake(){evasion=GetComponents<MonoBehaviour>().OfType<ICombatEvasion>().FirstOrDefault();guard=GetComponents<MonoBehaviour>().OfType<ICombatGuard>().FirstOrDefault();}
        public DefenseSnapshot CaptureDefense(Vector3 incomingOrigin)
        {
            var d=incomingOrigin-transform.position;d.y=0;
            var dot=d.sqrMagnitude>.0001f&&guard!=null?Vector3.Dot(guard.GuardDirection.normalized,d.normalized):-1;
            return new DefenseSnapshot(evasion!=null&&evasion.IsEvading,guard!=null&&guard.IsGuarding,dot,guard?.MinimumDot??1,guard?.PostureScale??1);
        }
        public void OnDefenseResolved(HitOutcome outcome){if(outcome.Reason==HitReason.Guarded)guard?.OnGuarded(outcome);}
    }
}
