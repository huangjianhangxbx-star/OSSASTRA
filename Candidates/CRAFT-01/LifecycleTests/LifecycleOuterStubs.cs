// 未参与本次生命周期路径的UI/商人SO外壳；不会替代CommerceService或其保存路径。
using System;
using Ossastra.Inventory;
using Ossastra.Commerce;
using Ossastra.Commerce.Presentation;
namespace Ossastra.UserInterface
{
    public sealed class InventoryView { public object Skin; }
}
namespace Ossastra.Commerce.Presentation
{
    public sealed class CommerceUIRoot
    {
        public void Close(){}public void SetPersistenceStatus(string text,bool retry){}
        public void Initialize(Ossastra.UserInterface.InventoryView view,object skin,ICommercePresentationHost host){}
        public bool OpenRoleMenu(string id,string name,UnityEngine.Vector2 point,bool personal)=>true;
    }
}
namespace Ossastra.Combat {public static class CombatSpatialTarget {public static object Find(UnityEngine.Collider value)=>null;}}
namespace Ossastra.Commerce.Runtime
{
    public sealed class MerchantDefinitionSO
    {
        public string definitionId="lifecycle-merchant";
        public MerchantDefinition Freeze()=>new MerchantDefinition(definitionId,Array.Empty<MerchantGoods>());
    }
    public sealed class CommerceDialogueSO {public CommerceDialogueDefinition Freeze()=>throw new NotSupportedException("Not used in lifecycle fixture");}
    public sealed class MerchantShop:UnityEngine.MonoBehaviour
    {public string Id="lifecycle-shop",displayName;public MerchantDefinitionSO definition=new MerchantDefinitionSO();public bool personalShop;public float interactionRange=3;public CommerceDialogueSO dialogue;}
    public sealed class CommerceWorldContainer:UnityEngine.MonoBehaviour
    {public string instanceId,mapId,displayName;public int width=1,height=1;public InventoryGrant[] initialContents=Array.Empty<InventoryGrant>();}
}
