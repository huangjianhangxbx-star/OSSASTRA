/* 脚本：CommercePresentationContracts
 * 功能：定义交易uGUI从宿主读取的展示快照及发出的用户命令；与库存唯一账本保持单向通信。
 * 边界：展示快照不是可修改库存；报价token只能由宿主映射到领域TradeQuote并在确认时重验。
 * 协作：CommerceUIRoot不读取角色控制或音频；Commerce宿主集中适配身份、访问、保存与对话。
 */
using System;
using UnityEngine;

namespace Ossastra.Commerce.Presentation
{
    /// <summary>宿主唯一适配口。当前会话、目录事实和结算均留在宿主，UI不直接写Ledger。</summary>
    public interface ICommercePresentationHost
    {
        bool IsTargetValid(string targetId);
        Vector2 TargetScreenPosition(string targetId);
        CommerceShopPresentation OpenShop(string targetId);
        CommerceShopPresentation ReadShop();
        CommerceQuotePresentation QuotePurchase(string definitionId,int quantity);
        CommerceQuotePresentation QuoteSale(string itemId,int quantity);
        CommerceUiResult Confirm(string quoteToken);
        CommerceDialoguePresentation StartDialogue(string targetId);
        CommerceDialoguePresentation ContinueDialogue(int choiceIndex);
        void CloseSession();
        void RetrySave();
    }

    [Serializable] public sealed class CommerceItemPresentation
    {
        public string definitionId,displayName,description,category,rarity;
        public Sprite icon;
        public int stock,width=1,height=1,maxStack=1;
        public long unitPrice,unitWeightGrams;
        public bool canBuy=true;
        public string reason;
    }
    [Serializable] public sealed class CommerceShopPresentation
    {
        public bool available;
        public string merchantId,displayName,reason,currencySources,saveStatus;
        public long affordableCurrency,revision;
        public double refreshSeconds;
        public bool canRetrySave;
        public CommerceItemPresentation[] items=Array.Empty<CommerceItemPresentation>();
    }
    [Serializable] public sealed class CommerceQuotePresentation
    {
        public bool ok,buying;
        public string token,reason,definitionId,itemId;
        public int quantity,maximumQuantity;
        public long unitPrice,totalPrice;
    }
    [Serializable] public sealed class CommerceDialoguePresentation
    {
        public bool available,completed;
        public string speaker,text,reason;
        public string[] choices=Array.Empty<string>();
    }
    [Serializable] public sealed class CommerceUiResult
    {
        public bool ok;
        public string message;
        public CommerceUiResult(bool ok,string message){this.ok=ok;this.message=message;}
    }
}
