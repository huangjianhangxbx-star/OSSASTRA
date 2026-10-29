/* 场景会话窄端口：聚合观察绑定/玩家捕获并提供当前跟随目标；地图程序集不依赖具体组队根。 */
using UnityEngine;
using Ossastra.Characters.ViewSpace;
namespace Ossastra.MapResearch
{
    public interface IMapResearchSession
    {
        Transform FollowTarget { get; }
        string ViewDiagnostics { get; }
        bool BindViewContext(object owner,ICharacterViewContext context);
        void ReleaseViewContext(object owner);
        bool SetViewInputCapture(object owner,bool captured);
    }
}
