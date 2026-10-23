using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Development-only reachability. Boundary checks use disposable domain state.</summary>
public static class DRPGExpeditionDebugActions
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    static readonly List<IDisposable> registrations=new List<IDisposable>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        foreach(var r in registrations)r.Dispose();registrations.Clear();
        Add("route","交互测试路线","当前探索替换为固定种子42；依次经过石堆、宝箱、地图台、战斗。只改本次运行地图，不写资产或存档。",10,d=>{d.ResetRoute(42,true);return d.Inspect();});
        Add("boundary","检查满包边界","用隔离的一格满包验证新战利品无法收取或交换；不修改玩家行囊。",20,d=>{
            var bag=new DRPGPartyInventoryState(1,100);bag.TryAddLoot(d.shovel,d.shovel.MaxStack);
            var loot=new DRPGLootContainerState();loot.Add(d.gem,1);
            if(DRPGExpeditionItems.Collect(loot,bag,d.gem.ItemId)!=0||DRPGExpeditionItems.Collect(loot,bag,d.gem.ItemId,0)!=0)throw new InvalidOperationException("满包规则失败");
            return "通过：满包拒收、拒绝替换；玩家物品未改变。";
        });
        Add("reset","重置测试地图","重新生成普通随机地图并回到起点；保留真实行囊和战斗角色状态。",30,d=>{d.ResetRoute(Environment.TickCount,false);return d.Inspect();});
        Add("inspect","查看探索状态","只读显示种子、房间、走廊、连续距离、事件、战斗状态与行囊容量。",40,d=>d.Inspect());
    }
    static void Add(string id,string title,string details,int order,Func<DRPGDungeonRuntime,string> run)
    {
        registrations.Add(DevDebugHubRuntime.Registry.Register(new DelegateDevDebugAction(
            "expedition."+id,"远征探索",title,details,details,order,id=="route"||id=="reset",
            ()=>{var d=UnityEngine.Object.FindFirstObjectByType<DRPGDungeonRuntime>();return d!=null&&d.State!=null&&(id=="inspect"||d.CanExplore)?DevDebugAvailability.Available():DevDebugAvailability.Unavailable("请先正常出发进入探索；战斗中不可重置。");},
            ()=>DevDebugActionResult.Succeeded(run(UnityEngine.Object.FindFirstObjectByType<DRPGDungeonRuntime>())))));
    }
#endif
}
