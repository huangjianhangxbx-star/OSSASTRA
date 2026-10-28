/* 脚本：SkillCooldownBook
 * 功能：本体技能实例的冷却账本；用稳定定义ID跨槽位保留冷却。
 * 边界：只记录已接受的执行；不读取SO、设备或推进另一条动作时钟。
 * 持久化：Inventory/EquipmentPersistentSnapshot中的partial只导出/校验恢复字段，不改变下方能力算法。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore
{
    public sealed partial class SkillCooldownBook
    {
        private readonly Dictionary<string,double> deadlines=new Dictionary<string,double>();
        private readonly HashSet<Guid> accepted=new HashSet<Guid>();
        public double Remaining(string id,double time)=>id!=null&&deadlines.TryGetValue(id,out var end)?Math.Max(0,end-time):0;
        /// <summary>有资格的击杀回充解除当前期限，保留接受去重账本；不能重复提交同一执行。</summary>
        public bool MakeReady(string id,double time)
        {if(string.IsNullOrWhiteSpace(id)||double.IsNaN(time)||double.IsInfinity(time)||!deadlines.ContainsKey(id))return false;deadlines[id]=Math.Min(deadlines[id],time);return true;}
        public bool Commit(string id,Guid actionId,double time,double duration)
        {
            if(string.IsNullOrWhiteSpace(id)||actionId==Guid.Empty||double.IsNaN(time)||double.IsInfinity(time)||
                double.IsNaN(duration)||double.IsInfinity(duration)||duration<0||Remaining(id,time)>0||accepted.Contains(actionId))return false;
            accepted.Add(actionId);deadlines[id]=time+duration;return true;
        }
    }
}
