/* 脚本：CombatRegeneration
 * 功能：本体持续恢复适配，所有食物/药共用同一互斥状态，沿Vitals原更新推进。
 * 边界：无自主Update，无第二HP；死亡/禁用清理有效恢复，不重发已消费道具。
 */
using System;
using UnityEngine;
using Ossastra.Inventory;
namespace Ossastra.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatRegeneration:MonoBehaviour
    {
        private CombatVitals vitals;
        private RegenerationState state;
        public bool Active=>isActiveAndEnabled&&(state?.Active??false);
        public double Remaining=>state?.Remaining??0;
        public void Initialize(CombatVitals owner){if(state!=null)return;vitals=owner;if(owner?.Health!=null)state=new RegenerationState(owner.Health);}
        public bool CanBegin(double rate,double seconds)=>isActiveAndEnabled&&vitals!=null&&vitals.IsAlive&&(state?.CanBegin(rate,seconds)??false);
        public bool TryBegin(Guid execution,double rate,double seconds)=>isActiveAndEnabled&&vitals!=null&&vitals.IsAlive&&(state?.TryBegin(execution,rate,seconds)??false);
        public void Tick(double delta){if(isActiveAndEnabled)state?.Tick(delta,false);}
        public void Clear()=>state?.Clear();
        private void OnDisable()=>Clear();
    }
}
