/* 脚本：ActionAdmissionLedger
 * 功能：请求身份的一次接受事务，拒绝与重复请求不调用提交端口。
 * 边界：不持有角色数值或时钟；提交端口仅在完整资格校验后执行。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore
{
    public sealed class ActionAdmissionLedger
    {
        private readonly HashSet<Guid> committed=new HashSet<Guid>();
        public bool TryCommit(Guid requestId,string rejection,Func<bool> commit)
        {
            if(requestId==Guid.Empty||rejection!=null||committed.Contains(requestId))return false;
            if(commit==null)throw new ArgumentNullException(nameof(commit));
            if(!commit())return false;
            committed.Add(requestId);return true;
        }
    }
}
