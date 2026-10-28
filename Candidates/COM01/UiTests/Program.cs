/* 测试：交易展示状态的取消、旧报价隔离和数量边界；不替代真实uGUI输入验证。 */
using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using Ossastra.Commerce.Presentation;
static class Program
{
    static int failures;
    static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
    // 直接解释生产入口的布尔守卫，覆盖菜单持有捕获但尚未进入贸易布局的状态；不模拟Unity按键更新。
    static bool OrdinaryEntryAllowed(string guard,bool trading,bool captured)
    {
        var expression=guard.Replace("HasCommerceCapture",captured?"true":"false").Replace("IsCommerceOpen",trading?"true":"false")
            .Replace("&&"," AND ").Replace("||"," OR ").Replace("!"," NOT ");
        return Convert.ToBoolean(new DataTable().Compute(expression,null));
    }
    static string InventoryEntryGuard(string suffix)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory!=null&&!File.Exists(Path.Combine(directory.FullName,"Assets/OSSASTRA/Scripts/UI/InventoryView.cs")))directory=directory.Parent;
        if(directory==null)throw new InvalidOperationException("找不到真实InventoryView入口源码");
        var source=File.ReadAllText(Path.Combine(directory.FullName,"Assets/OSSASTRA/Scripts/UI/InventoryView.cs"));
        var match=Regex.Match(source,@"if\((?<guard>[^\r\n]+?)&&"+suffix);
        if(!match.Success)throw new InvalidOperationException("找不到普通背包入口守卫："+suffix);
        return match.Groups["guard"].Value;
    }
    static int Main()
    {
        int quantity;string reason;
        Check(!CommerceWindowState.TryQuantity("",3,out quantity,out reason),"空白数量不能默认卖整堆");
        Check(!CommerceWindowState.TryQuantity("-1",3,out quantity,out reason),"负数不能成交");
        Check(!CommerceWindowState.TryQuantity("99999999999999999999",3,out quantity,out reason),"粘贴溢出不能成交");
        Check(!CommerceWindowState.TryQuantity("４",3,out quantity,out reason),"全角输入有明确拒绝");
        Check(!CommerceWindowState.TryQuantity("4",3,out quantity,out reason),"数量上限按所选实例");
        Check(CommerceWindowState.TryQuantity("2",3,out quantity,out reason)&&quantity==2,"所选实例内数量可受理");
        var state=new CommerceWindowState();state.OpenMenu("merchant-a");state.OpenTrade("merchant-a");state.Select("item-a",false,3);state.SetQuote("quote-a");
        state.Select("item-b",false,7);Check(state.QuoteId==null&&state.QuantityText=="1","换实例撤销旧报价并重置数量");
        state.SetQuote("quote-b");state.SetQuantity("2");Check(state.QuoteId==null,"数量改变必须重新报价");
        state.SetQuote("quote-c");state.BeginQuantityEdit();Check(state.Cancel()==CommerceCancelResult.Quantity&&state.IsOpen&&state.SelectedId=="item-b","Esc先取消数字草稿而非关窗");
        Check(state.QuoteId==null,"取消数量后旧报价不可提交");
        Check(state.Cancel()==CommerceCancelResult.Selection&&state.IsOpen&&state.SelectedId==null,"第二次Esc只撤销详情");
        Check(state.Cancel()==CommerceCancelResult.Window&&!state.IsOpen,"最后Esc关闭自有窗口");
        state.OpenMenu("florecia");state.OpenTrade("different-target");Check(state.TargetId=="different-target"&&state.SelectedId==null,"切会话不能保留旧目标详情");
        var gate=new CommerceMenuGate();var owner=new object();var other=new object();gate.Capture(owner,true,9);
        Check(!gate.AllowsMenu(10),"菜单Installer先执行也不能穿过现有交易捕获");
        gate.Capture(owner,false,10);Check(!gate.AllowsMenu(10)&&!gate.AllowsMenu(11),"关闭交易的同帧及后一帧不能打开SystemMenu");
        Check(gate.AllowsMenu(12),"保护帧之后新的菜单输入可正常打开");
        gate.Capture(owner,true,20);gate.Capture(other,true,20);gate.Capture(owner,false,21);Check(gate.HasCapture&&!gate.AllowsMenu(30),"仅释放自己的owner不能清除其他交易捕获");
        gate.Capture(other,false,30);gate.ConsumeCancel(32);Check(!gate.AllowsMenu(33)&&gate.AllowsMenu(34),"无活动贸易时的取消边沿也只有有限保护帧");
        var toggleGuard=InventoryEntryGuard(@"toggleAction\.WasPressedThisFrame\(\)");
        var cancelGuard=InventoryEntryGuard(@"IsOpen&&cancelAction\.WasPressedThisFrame\(\)");
        Check(!OrdinaryEntryAllowed(toggleGuard,false,true),"角色菜单或对话持有捕获时Tab不能打开下层普通背包");
        Check(!OrdinaryEntryAllowed(cancelGuard,false,true),"角色菜单或对话持有捕获时Esc不能由普通背包再次消费");
        Check(!OrdinaryEntryAllowed(toggleGuard,true,true)&&!OrdinaryEntryAllowed(cancelGuard,true,true),"贸易捕获仍阻止普通背包入口");
        Check(OrdinaryEntryAllowed(toggleGuard,false,false)&&OrdinaryEntryAllowed(cancelGuard,false,false),"贸易捕获释放后普通背包Tab和Esc恢复");
        return failures==0?0:1;
    }
}
