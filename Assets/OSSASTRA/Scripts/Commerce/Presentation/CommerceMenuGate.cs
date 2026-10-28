/* 脚本：CommerceMenuGate
 * 功能：将交易窗口的owner与关闭边沿转为系统菜单的有限帧门禁。
 * 边界：只隔离同一物理Esc被两个Action消费的展示入口，不改变InputAction绑定或角色采样。
 */
using System.Collections.Generic;
namespace Ossastra.Commerce.Presentation
{
    public sealed class CommerceMenuGate
    {
        private readonly HashSet<object> owners=new HashSet<object>();
        private int suppressedThrough=-1;
        public bool HasCapture=>owners.Count>0;
        public void Capture(object owner,bool captured,int frame)
        {
            if(owner==null)return;
            var changed=captured?owners.Add(owner):owners.Remove(owner);
            if(changed)ConsumeCancel(frame);
        }
        public void ConsumeCancel(int frame)
        {var until=frame==int.MaxValue?int.MaxValue:frame+1;if(until>suppressedThrough)suppressedThrough=until;}
        public bool AllowsMenu(int frame)=>owners.Count==0&&frame>suppressedThrough;
    }
}
