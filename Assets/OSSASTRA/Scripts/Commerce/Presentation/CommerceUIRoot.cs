/* 脚本：CommerceUIRoot
 * 功能：组装同身份角色菜单、SAMPLE对话与三列交易uGUI；复用InventoryView团队包和皮肤资源。
 * 负责：窗口层级、选物/数字草稿、报价展示、确认回执、失焦与失效退栈、自有输入捕获。
 * 不负责：商店库存、价格规则、扣币交货、保存写入、世界F仲裁、角色或音频更新。
 * 协作：宿主通过ICommercePresentationHost提供事实；唯一交互路由调用TryConfirmInteraction。
 */
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Ossastra.UserInterface;

namespace Ossastra.Commerce.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)]
    public sealed partial class CommerceUIRoot:MonoBehaviour
    {
        private InventoryView inventory;
        private InventorySkinSO skin;
        private ICommercePresentationHost host;
        private readonly CommerceWindowState state=new CommerceWindowState();
        private Canvas canvas,barrierCanvas,persistenceCanvas;
        private RectTransform persistencePanel;
        private UnityEngine.UI.Text persistenceText;
        private UnityEngine.UI.Button persistenceRetry;
        private string persistenceMessage;
        private bool persistenceRetryable;
        private RectTransform surface,roleMenu,dialogueWindow,shelfWindow,detailWindow,shelfContent;
        private UnityEngine.UI.Text roleName,menuNotice,dialogueSpeaker,dialogueText,dialogueHint,shelfName,refreshText,balanceText,saveText;
        private UnityEngine.UI.Text itemName,itemDescription,itemTags,priceText,detailNotice,quoteButtonText;
        private UnityEngine.UI.Image itemIcon;
        private UnityEngine.UI.Button confirmButton,retryButton,nextDialogue;
        private UnityEngine.UI.InputField quantityField;
        private RectTransform dialogueChoices;
        private Font font,serif;
        private CommerceShopPresentation shop;
        private CommerceItemPresentation displayedItem;
        private CommerceQuotePresentation quote;
        private CommerceDialoguePresentation dialogue;
        private long shelfRevision=long.MinValue;
        private string shelfMerchant,targetName;
        private int openingFrame=-1,confirmFrame=-1;
        private float nextRefresh;
        private bool closing,selectingGood,allowDialogue,isDialogue,quantityWasFocused,sessionOwned;
        private readonly Color textColor=new Color32(238,233,233,255);
        private readonly Color gold=new Color32(210,190,128,255);

        public bool IsOpen=>state.IsOpen;
        public bool IsTrading=>state.IsOpen&&state.IsTrading;
        public bool IsDialogueOpen=>state.IsOpen&&isDialogue;
        public string TargetId=>state.TargetId;
        public string SelectedId=>state.SelectedId;
        public string Notice=>detailNotice==null?null:detailNotice.text;
        public bool IsQuantityFocused=>quantityField!=null&&quantityField.isFocused;

        /// <summary>宿主可在关店后继续显示保存中/拒写/恢复提示；这个角标不捕获任何角色输入。</summary>
        public void SetPersistenceStatus(string message,bool canRetry)
        {
            persistenceMessage=message;persistenceRetryable=canRetry;
            if(persistenceText!=null){persistenceText.text=message??"";persistenceRetry.gameObject.SetActive(canRetry);}
        }

        /// <summary>由场景模块根集中注入。未就绪时保留诊断，禁止全局找对象猜服务。</summary>
        public void Initialize(InventoryView inventory,InventorySkinSO skin,ICommercePresentationHost host)
        {
            if(this.inventory!=null)UnsubscribeInventory();
            Close();this.inventory=inventory;this.skin=skin;this.host=host;
            if(inventory==null||skin==null||host==null||skin.frame==null||skin.slotFill==null||skin.slotEdge==null)
                throw new ArgumentException("CommerceUIRoot需要明确的InventoryView、完整InventorySkin和宿主");
            inventory.CommerceSelectionChanged+=OnInventorySelected;
            inventory.CommerceInventoryClosed+=OnInventoryClosed;
            if(canvas==null)Build();
        }

        public bool OpenRoleMenu(string targetId,string displayName,Vector2 screenPoint,bool allowDialogue=true)
        {
            if(host==null||inventory==null||!inventory.IsPresentationReady||!host.IsTargetValid(targetId))return false;
            Close();inventory.CloseSystemMenu();inventory.SetOpen(false);targetName=displayName;this.allowDialogue=allowDialogue;state.OpenMenu(targetId);sessionOwned=true;openingFrame=Time.frameCount;
            roleName.text=displayName;menuNotice.text="";SetVisible(roleMenu,true);SetVisible(dialogueWindow,false);SetVisible(shelfWindow,false);SetVisible(detailWindow,false);
            canvas.gameObject.SetActive(true);barrierCanvas.gameObject.SetActive(true);PositionRoleMenu(screenPoint);
            SetTradeStatusVisible(false);
            inventory.SetCommerceInputCapture(this,true,true);
            return true;
        }

        /// <summary>唯一F路由调用；打开窗口同帧只吞掉按下，数字焦点不成交也不漏向世界。</summary>
        public bool TryConfirmInteraction()
        {
            if(!IsOpen)return false;
            if(Time.frameCount<=openingFrame||IsQuantityFocused)return true;
            if(isDialogue){AdvanceDialogue(-1);return true;}
            if(IsTrading&&quote!=null&&quote.ok)ConfirmSelection();
            return true;
        }

        public void OpenTargetShop()
        {
            if(!IsOpen||host==null)return;
            var target=state.TargetId;var result=host.OpenShop(target);
            if(result==null||!result.available){menuNotice.text=result?.reason??"商店暂不可用";return;}
            if(!inventory.BeginCommerce()){host.CloseSession();menuNotice.text="库存视图未就绪，未打开交易";return;}
            state.OpenTrade(target);shop=result;isDialogue=false;quote=null;openingFrame=Time.frameCount;
            SetVisible(roleMenu,false);SetVisible(dialogueWindow,false);SetVisible(shelfWindow,true);SetVisible(detailWindow,false);
            inventory.SetCommerceInputCapture(this,true,true);RenderShop(true);
            SetTradeStatusVisible(true);
        }

        public void OpenTargetDialogue()
        {
            if(!IsOpen||host==null||!allowDialogue)return;
            dialogue=host.StartDialogue(state.TargetId);
            if(dialogue==null||!dialogue.available){menuNotice.text=dialogue?.reason??"对话暂不可用";return;}
            isDialogue=true;openingFrame=Time.frameCount;SetVisible(roleMenu,false);SetVisible(dialogueWindow,true);RenderDialogue();
            inventory.SetCommerceInputCapture(this,true,true);
        }

        private void Update()
        {
            if(canvas==null||inventory==null)return;
            var scale=Mathf.Min(Screen.width/1434f,Screen.height/804f);
            if(scale>0&&!Mathf.Approximately(canvas.scaleFactor,scale)){canvas.scaleFactor=scale;barrierCanvas.scaleFactor=scale;}
            if(persistenceCanvas!=null)
            {
                persistenceCanvas.scaleFactor=canvas.scaleFactor;
                persistenceCanvas.gameObject.SetActive(!IsTrading&&!string.IsNullOrEmpty(persistenceMessage));
                persistencePanel.anchoredPosition=new Vector2(Mathf.Max(8,Screen.width/canvas.scaleFactor-620),-(Screen.height/canvas.scaleFactor-82));
            }
            if(!IsOpen)return;
            if(host==null||!host.IsTargetValid(state.TargetId)){Close();return;}
            if(inventory.ConsumeCommerceCancel()){CancelTop();return;}
            var focused=IsQuantityFocused;
            if(focused&&!quantityWasFocused)state.BeginQuantityEdit();
            quantityWasFocused=focused;inventory.SetCommerceQuantityFocus(focused);
            // COM-01 D10：菜单、对话、贸易都捕获移动；普通背包仍使用InventoryView自己的原策略。
            inventory.SetCommerceInputCapture(this,true,true);
            if(!IsTrading)
            {
                var point=host.TargetScreenPosition(state.TargetId);
                if(isDialogue)PositionDialogue(point);else PositionRoleMenu(point);
            }
            else if(Time.unscaledTime>=nextRefresh)
            {
                nextRefresh=Time.unscaledTime+.1f;var current=host.ReadShop();
                if(current==null||!current.available){Close();return;}
                // 保存失败/恢复不一定改变货架revision；重取当前报价同步确认按钮，保留选物和数量草稿。
                var accessChanged=shop==null||shop.canRetrySave!=current.canRetrySave||!string.Equals(shop.reason,current.reason,StringComparison.Ordinal);
                shop=current;RenderShop(false);
                if(accessChanged&&state.SelectedId!=null)RefreshQuote();
            }
        }

        private void RenderShop(bool force)
        {
            if(shop==null)return;
            shelfName.text=shop.displayName;
            var seconds=Math.Max(0,(int)Math.Ceiling(shop.refreshSeconds));refreshText.text="下次刷新 "+(seconds/60).ToString("00")+":"+(seconds%60).ToString("00");
            balanceText.text="可支付 ◈ "+shop.affordableCurrency+"\n"+(shop.currencySources??"合法持有容器");
            saveText.text=shop.saveStatus??"";retryButton.gameObject.SetActive(shop.canRetrySave);
            if(force||shelfRevision!=shop.revision||shelfMerchant!=shop.merchantId)
            {
                shelfRevision=shop.revision;shelfMerchant=shop.merchantId;
                BuildShelfCards(shop.items??Array.Empty<CommerceItemPresentation>());
                if(state.SelectedId!=null)RefreshQuote();
            }
        }

        private void SelectGood(CommerceItemPresentation item)
        {
            if(!IsTrading||item==null)return;
            selectingGood=true;inventory.Select(null);selectingGood=false;
            displayedItem=item;state.Select(item.definitionId,true,Math.Max(0,item.stock));quantityField.SetTextWithoutNotify("1");
            RefreshQuote();
        }

        private void OnInventorySelected(string itemId)
        {
            if(!IsTrading||selectingGood)return;
            if(itemId==null){ClearSelection();return;}
            var item=inventory.Session.Ledger.Find(itemId);var definition=inventory.Session.Definition(item?.DefinitionId);
            if(item==null||definition==null){ClearSelection();return;}
            displayedItem=new CommerceItemPresentation{definitionId=item.DefinitionId,displayName=definition.displayName,description=definition.description,
                icon=definition.icon,category=definition.category,rarity=definition.rarity.ToString(),width=definition.width,height=definition.height,
                maxStack=definition.maxStack,unitWeightGrams=definition.unitWeightGrams,stock=item.Quantity,unitPrice=definition.sellValue};
            state.Select(itemId,false,item.Quantity);quantityField.SetTextWithoutNotify("1");RefreshQuote();
        }

        private void QuantityChanged(string value)
        {if(!IsTrading||state.SelectedId==null)return;state.SetQuantity(value);RefreshQuote();}

        private void RefreshQuote()
        {
            if(!IsTrading||state.SelectedId==null)return;
            if(state.IsBuying)
            {
                var current=(shop?.items??Array.Empty<CommerceItemPresentation>()).FirstOrDefault(i=>i.definitionId==state.SelectedId);
                if(current!=null)displayedItem=current;
            }
            var maximum=state.IsBuying?displayedItem?.stock??0:inventory.Session.Ledger.Find(state.SelectedId)?.Quantity??0;
            if(!CommerceWindowState.TryQuantity(state.QuantityText,maximum,out var count,out var reason))
                quote=new CommerceQuotePresentation{ok=false,buying=state.IsBuying,reason=reason,maximumQuantity=maximum};
            else quote=state.IsBuying?host.QuotePurchase(state.SelectedId,count):host.QuoteSale(state.SelectedId,count);
            if(quote==null)quote=new CommerceQuotePresentation{ok=false,reason="报价服务没有返回有效结果"};
            state.SetQuote(quote.ok?quote.token:null);RenderDetail();
        }

        private void RenderDetail()
        {
            if(displayedItem==null){SetVisible(detailWindow,false);return;}
            SetVisible(detailWindow,true);itemName.text=displayedItem.displayName;
            itemIcon.sprite=displayedItem.icon;itemIcon.enabled=displayedItem.icon!=null;
            var pictureFallback=detailWindow.Find("ItemFallback")?.GetComponent<UnityEngine.UI.Text>();
            if(pictureFallback!=null)pictureFallback.text=displayedItem.icon==null?displayedItem.displayName:"";
            itemDescription.text=(displayedItem.description??"")+"\n\n"+(state.IsBuying?"店铺库存 ":"所选实例 ")+(quote?.maximumQuantity>0?quote.maximumQuantity:displayedItem.stock)+
                "\n占格 "+displayedItem.width+"×"+displayedItem.height+" · 每堆 "+displayedItem.maxStack+"\n重量 "+(displayedItem.unitWeightGrams/1000d).ToString("0.00")+" kg/件";
            itemTags.text=(displayedItem.category??"道具")+"\n品质 "+(displayedItem.rarity??"未配置");
            priceText.text=quote!=null&&quote.ok?"单价 ◈ "+quote.unitPrice+" × "+quote.quantity+" = ◈ "+quote.totalPrice:"暂无有效报价";
            detailNotice.text=quote?.reason??"";confirmButton.interactable=quote!=null&&quote.ok&&!string.IsNullOrEmpty(quote.token);
            quoteButtonText.text=(state.IsBuying?"F 购买":"F 出售")+(quote!=null&&quote.ok?"\n◈ "+quote.totalPrice+" 钱币":"\n当前不可成交");
        }

        public void ConfirmSelection()
        {
            if(!IsTrading||quote==null||!quote.ok||state.QuoteId==null||IsQuantityFocused||confirmFrame==Time.frameCount)return;
            confirmFrame=Time.frameCount;var token=state.QuoteId;state.SetQuote(null);confirmButton.interactable=false;
            var result=host.Confirm(token);var message=result?.message??"交易服务没有返回回执";
            inventory.Feedback(message);RefreshQuote();
            shop=host.ReadShop();if(shop!=null&&shop.available){RenderShop(true);detailNotice.text=message;}else Close();
        }

        private void ClearSelection()
        {
            if(inventory!=null&&inventory.SelectedItemId!=null){selectingGood=true;inventory.Select(null);selectingGood=false;}
            if(state.SelectedId!=null)state.Select(null,false,0);quote=null;displayedItem=null;
            if(quantityField!=null)quantityField.SetTextWithoutNotify("1");SetVisible(detailWindow,false);
        }
        private void CancelTop()
        {
            if(inventory.HasInventoryDrag){inventory.CancelDrag();return;}
            var result=state.Cancel();
            if(result==CommerceCancelResult.Quantity)
            {quantityField.DeactivateInputField();quantityField.SetTextWithoutNotify("1");EventSystem.current?.SetSelectedGameObject(null);quantityWasFocused=false;RefreshQuote();return;}
            if(result==CommerceCancelResult.Selection){selectingGood=true;inventory.Select(null);selectingGood=false;ClearSelection();return;}
            if(result==CommerceCancelResult.Window)Close();
        }
        private void AdvanceDialogue(int choice)
        {
            if(!IsDialogueOpen||Time.frameCount<=openingFrame)return;
            // 有选项时F不能绕过玩家选择；鼠标明确点击选项后再交给宿主。
            if(choice<0&&(dialogue?.choices?.Length??0)>0)return;
            dialogue=host.ContinueDialogue(choice);
            if(dialogue==null||!dialogue.available||dialogue.completed){Close();return;}
            RenderDialogue();
        }
        private void RenderDialogue()
        {
            dialogueSpeaker.text=dialogue.speaker??targetName;dialogueText.text=dialogue.text??"";
            dialogueHint.text="SAMPLE · F继续 / Esc退出";
            foreach(Transform child in dialogueChoices)Destroy(child.gameObject);
            var options=dialogue.choices??Array.Empty<string>();
            for(var i=0;i<options.Length;i++){var choice=i;CreateButton(dialogueChoices,"Choice"+i,new Rect(0,i*42,376,36),options[i],()=>AdvanceDialogue(choice));}
            nextDialogue.gameObject.SetActive(options.Length==0);
            dialogueHint.rectTransform.anchoredPosition=new Vector2(20,-(options.Length==0?200:210+options.Length*42));
            dialogueWindow.sizeDelta=new Vector2(420,260+options.Length*42);
            var frame=dialogueWindow.Find("Frame") as RectTransform;if(frame!=null)frame.sizeDelta=dialogueWindow.sizeDelta;
            PositionDialogue(host.TargetScreenPosition(state.TargetId));
        }

        public void Close()
        {
            if(closing)return;closing=true;
            var wasOpen=IsOpen||sessionOwned;sessionOwned=false;state.Close();quote=null;shop=null;displayedItem=null;dialogue=null;isDialogue=false;
            if(inventory!=null){inventory.SetCommerceQuantityFocus(false);inventory.SetCommerceInputCapture(this,false,false);if(inventory.IsCommerceOpen)inventory.EndCommerce();}
            if(canvas!=null)canvas.gameObject.SetActive(false);if(barrierCanvas!=null)barrierCanvas.gameObject.SetActive(false);
            if(wasOpen)host?.CloseSession();quantityWasFocused=false;closing=false;
        }
        private void OnInventoryClosed(){if(IsOpen)Close();}
        private void SetTradeStatusVisible(bool visible)
        {balanceText.gameObject.SetActive(visible);saveText.gameObject.SetActive(visible);retryButton.gameObject.SetActive(visible&&shop!=null&&shop.canRetrySave);}
        private void UnsubscribeInventory(){inventory.CommerceSelectionChanged-=OnInventorySelected;inventory.CommerceInventoryClosed-=OnInventoryClosed;}
        private void OnApplicationFocus(bool focused){if(!focused)Close();}
        private void OnDisable(){Close();if(persistenceCanvas!=null)persistenceCanvas.gameObject.SetActive(false);}
        private void OnDestroy(){Close();if(inventory!=null)UnsubscribeInventory();if(font!=null)Destroy(font);if(serif!=null)Destroy(serif);}
        private static void SetVisible(RectTransform rect,bool visible){if(rect!=null)rect.gameObject.SetActive(visible);}
    }
}
