/* 脚本：CraftingUIRoot
 * 功能：固定装备台的两列uGUI；配方/类别/数量、宿主预览与真实提交回执。
 * 负责：自有窗口和输入捕获、打开帧防误确认、失焦/失效关闭、按修订刷新。
 * 不负责：库存扫描/扣料、资格、装备实例、工位距离、游戏时钟与磁盘保存。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Ossastra.UserInterface;

namespace Ossastra.Crafting.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed partial class CraftingUIRoot : MonoBehaviour, ICancelHandler
    {
        [Header("装备台界面")]
        [Tooltip("屏幕显示顺序；高于原库存表面，关闭时只释放本组件持有的捕获。")]
        [SerializeField] private int sortingOrder=182;
        private InventoryView inventory;
        private InventorySkinSO skin;
        private ICraftingPresentationHost host;
        private Canvas canvas;
        private RectTransform panel,recipeContent,categoryContent;
        private UnityEngine.UI.ScrollRect recipeScroll,categoryScroll;
        private UnityEngine.UI.Text stationTitle,recipeName,description,materials,output,reason,resultText,maximumText;
        private UnityEngine.UI.Image outputIcon;
        private UnityEngine.UI.Button confirmButton,minusButton,plusButton,closeButton;
        private UnityEngine.UI.InputField quantityField;
        private Font font;
        private readonly List<UnityEngine.UI.Button> recipeButtons=new List<UnityEngine.UI.Button>();
        private readonly List<UnityEngine.UI.Button> categoryButtons=new List<UnityEngine.UI.Button>();
        private readonly List<string> categoryNames=new List<string>();
        private readonly List<CraftingRecipePresentation> visibleRecipes=new List<CraftingRecipePresentation>();
        private IReadOnlyList<CraftingRecipePresentation> rows;
        private CraftingRecipePresentation selected;
        private CraftingPreviewPresentation preview;
        private string selectedId,category="全部",quantityText="1";
        private long seenRevision=long.MinValue;
        private int openingFrame=-1,confirmFrame=-1,cancelFrame=-1;
        private bool isOpen,captured,closing,quantityFocused,refreshPending,initialSelectionPending;
        private readonly Color ink=new Color32(7,7,10,253);
        private readonly Color edge=new Color32(151,44,75,255);
        private readonly Color cyan=new Color32(126,191,190,255);
        private readonly Color paper=new Color32(232,229,226,255);
        private readonly Color muted=new Color32(167,167,179,255);

        public bool IsOpen=>isOpen;
        public bool IsQuantityFocused=>quantityField!=null&&quantityField.isFocused;
        public string SelectedRecipeId=>selectedId;
        public string Notice=>reason==null?null:reason.text;
        public string ResultNotice=>resultText==null?null:resultText.text;

        /// <summary>由CraftingRoot注入原库存视图、既有皮肤与领域宿主，不全局查找服务。</summary>
        public void Initialize(InventoryView inventory,InventorySkinSO skin,ICraftingPresentationHost host)
        {
            Close();
            if(inventory==null||skin==null||host==null)
                throw new ArgumentException("CraftingUIRoot需要原InventoryView、InventorySkin与明确宿主。");
            this.inventory=inventory;this.skin=skin;this.host=host;
            if(canvas==null)Build();
        }

        public bool Open()
        {
            if(isOpen)return host!=null&&host.IsSessionValid;
            if(!isActiveAndEnabled||canvas==null||inventory==null||!inventory.IsPresentationReady||host==null||!host.IsSessionValid)return false;
            inventory.CloseSystemMenu();inventory.SetOpen(false);
            selected=null;selectedId=null;category="全部";quantityText="1";preview=null;rows=null;
            seenRevision=long.MinValue;confirmFrame=-1;openingFrame=Time.frameCount;refreshPending=true;initialSelectionPending=true;
            quantityField.SetTextWithoutNotify("1");resultText.text="";reason.text="请选择配方";confirmButton.interactable=false;
            isOpen=true;canvas.gameObject.SetActive(true);Resize();
            inventory.SetCommerceInputCapture(this,true,true);captured=true;
            // Root在Open返回后记录捕获导致的controlRevision；首份票据延迟到Update签发。
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            return true;
        }

        /// <summary>只由原InventoryInteraction的唯一交互路由调用；不绑定第二个F。</summary>
        public bool TryConfirmInteraction()
        {
            if(!isOpen)return false;
            // 手柄A可同时属于世界交互和uGUI Submit。窗口内优先交给当前选中控件，
            // 否则选择配方的同一次A会顺带制作；F等未与Submit重叠的原交互仍可确认。
            var module=EventSystem.current==null?null:EventSystem.current.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;
            if(module!=null&&module.submit!=null&&module.submit.action!=null&&module.submit.action.WasPressedThisFrame())return true;
            if(Time.frameCount<=openingFrame||IsQuantityFocused)return true;
            ConfirmSelection();return true;
        }

        public void Close()
        {
            if(closing)return;
            closing=true;
            var hadSession=isOpen;
            isOpen=false;preview=null;selected=null;selectedId=null;rows=null;refreshPending=false;seenRevision=long.MinValue;
            if(quantityField!=null)quantityField.DeactivateInputField();
            var eventSystem=EventSystem.current;
            if(eventSystem!=null&&eventSystem.currentSelectedGameObject!=null&&canvas!=null&&eventSystem.currentSelectedGameObject.transform.IsChildOf(canvas.transform))
                eventSystem.SetSelectedGameObject(null);
            if(captured&&inventory!=null)
            {
                inventory.SetCommerceQuantityFocus(false);
                inventory.SetCommerceInputCapture(this,false,true);
            }
            captured=false;quantityFocused=false;
            if(canvas!=null)canvas.gameObject.SetActive(false);
            try { if(hadSession&&host!=null)host.CloseSession(); }
            finally { closing=false; }
        }

        private void Update()
        {
            if(canvas==null)return;
            Resize();
            if(!isOpen)return;
            if(host==null||!host.IsSessionValid){Close();return;}
            if(inventory==null||!inventory.IsPresentationReady){Close();return;}
            if(inventory.ConsumeCommerceCancel())CancelTop();
            if(!isOpen)return;
            var focused=IsQuantityFocused;
            if(focused!=quantityFocused){quantityFocused=focused;inventory.SetCommerceQuantityFocus(focused);}
            // 只比较宿主修订，不扫描库存、物品实例或场景工位。
            if(refreshPending||seenRevision!=host.RefreshRevision)
            {
                refreshPending=false;seenRevision=host.RefreshRevision;
                stationTitle.text=host.StationName??"装备台";
                RefreshRows();RefreshPreview();
            }
        }

        private void RefreshRows()
        {
            var eventSystem=EventSystem.current;
            var focused=eventSystem==null?null:eventSystem.currentSelectedGameObject;
            var ownedRow=focused!=null&&(focused.transform.IsChildOf(recipeContent)||focused.transform.IsChildOf(categoryContent));
            var focusedName=ownedRow?focused.name:null;
            rows=host.Recipes;
            categoryNames.Clear();categoryNames.Add("全部");
            if(rows!=null)
                for(var i=0;i<rows.Count;i++)
                {
                    var row=rows[i];if(row==null)continue;
                    var value=string.IsNullOrWhiteSpace(row.Category)?"其他":row.Category;
                    if(!categoryNames.Contains(value))categoryNames.Add(value);
                }
            if(!categoryNames.Contains(category))category="全部";
            BuildCategories();BuildRecipeRows();
            if(ownedRow&&eventSystem!=null)
            {
                UnityEngine.UI.Button replacement=null;
                for(var i=0;i<recipeButtons.Count;i++)if(recipeButtons[i].name==focusedName)replacement=recipeButtons[i];
                for(var i=0;i<categoryButtons.Count;i++)if(categoryButtons[i].name==focusedName)replacement=categoryButtons[i];
                if(replacement==null&&recipeButtons.Count>0)replacement=recipeButtons[0];
                eventSystem.SetSelectedGameObject(replacement==null?closeButton.gameObject:replacement.gameObject);
            }
            else if(initialSelectionPending&&eventSystem!=null)
                eventSystem.SetSelectedGameObject(recipeButtons.Count>0?recipeButtons[0].gameObject:closeButton.gameObject);
            initialSelectionPending=false;
        }

        private void BuildRecipeRows()
        {
            ClearChildren(recipeContent);recipeButtons.Clear();visibleRecipes.Clear();
            if(rows!=null)
                for(var i=0;i<rows.Count;i++)
                {
                    var row=rows[i];if(row==null||string.IsNullOrEmpty(row.Id))continue;
                    if(category!="全部"&&(string.IsNullOrWhiteSpace(row.Category)?"其他":row.Category)!=category)continue;
                    visibleRecipes.Add(row);
                }
            selected=null;
            for(var i=0;i<visibleRecipes.Count;i++)if(visibleRecipes[i].Id==selectedId){selected=visibleRecipes[i];break;}
            if(selected==null){selected=visibleRecipes.Count>0?visibleRecipes[0]:null;selectedId=selected?.Id;}
            for(var i=0;i<visibleRecipes.Count;i++)
            {
                var row=visibleRecipes[i];var button=MakeRecipeRow(row,()=>SelectRecipe(row));recipeButtons.Add(button);
                var relay=button.gameObject.AddComponent<CraftingRecipeSelection>();relay.Configure(recipeScroll,button.transform as RectTransform);
            }
            if(visibleRecipes.Count==0)MakeEmptyList();
            UpdateRecipeSelection();WireNavigation();
        }

        private void SelectRecipe(CraftingRecipePresentation row)
        {
            if(!isOpen||row==null)return;
            selected=row;selectedId=row.Id;quantityText="1";quantityField.SetTextWithoutNotify(quantityText);resultText.text="";
            UpdateRecipeSelection();WireNavigation();RefreshPreview();
        }

        private void SelectCategory(string value)
        {
            if(!isOpen||category==value)return;
            category=value;resultText.text="";BuildCategories();BuildRecipeRows();RefreshPreview();
            if(recipeButtons.Count>0&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(recipeButtons[0].gameObject);
        }

        private void QuantityChanged(string value)
        {quantityText=value??"";if(isOpen){resultText.text="";RefreshPreview();}}

        private void StepQuantity(int delta)
        {
            if(!isOpen)return;
            if(!int.TryParse(quantityText,out var quantity)||quantity<1)quantity=1;
            var cap=preview!=null?Math.Max(1,preview.MaxBatch):1;
            var next=(int)Math.Max(1L,Math.Min((long)cap,(long)quantity+delta));
            quantityText=next.ToString();quantityField.SetTextWithoutNotify(quantityText);resultText.text="";RefreshPreview();
        }

        private void RefreshPreview()
        {
            preview=null;confirmButton.interactable=false;minusButton.interactable=false;plusButton.interactable=false;
            if(!isOpen||host==null||!host.IsSessionValid)return;
            recipeName.text=selected?.Name??"暂无配方";description.text=selected?.Description??"这个装备台没有当前可用的配方。";
            outputIcon.sprite=selected?.Icon;outputIcon.enabled=selected?.Icon!=null;
            if(selected==null){reason.text="请选择配方";materials.text="";output.text="";maximumText.text="";return;}
            if(!int.TryParse(quantityText,out var quantity)||quantity<1)
            {reason.text="请输入正整数数量";materials.text="数量无效，未生成合成预览。";maximumText.text="";output.text="";return;}
            preview=host.Preview(selected.Id,quantity);
            if(preview==null){reason.text="暂时无法读取配方";materials.text="";maximumText.text="";output.text="";return;}
            reason.text=preview.Reason??(preview.Ok?"可以合成":"当前无法合成");
            reason.color=preview.Ok?cyan:new Color32(232,159,158,255);
            materials.text=preview.MaterialsSummary??"未配置材料摘要";output.text=preview.OutputSummary??"";
            maximumText.text="单批上限 "+Math.Max(0,preview.MaxBatch);
            minusButton.interactable=quantity>1;plusButton.interactable=quantity<preview.MaxBatch;
            confirmButton.interactable=preview.Ok&&!string.IsNullOrEmpty(preview.RequestId);
        }

        private void ConfirmSelection()
        {
            if(!isOpen||host==null||Time.frameCount<=openingFrame||Time.frameCount==confirmFrame||IsQuantityFocused)return;
            confirmFrame=Time.frameCount;
            if(!host.IsSessionValid){Close();return;}
            // 依赖已变时让宿主拒绝旧票，不在确认按下时悄悄换成另一份新报价。
            var request=preview;
            if(request==null||!request.Ok||string.IsNullOrEmpty(request.RequestId))return;
            preview=null;confirmButton.interactable=false;
            var result=host.Confirm(request.RequestId);
            if(result==null){resultText.text="合成未完成：没有有效回执";resultText.color=new Color32(232,159,158,255);}
            else
            {
                resultText.color=result.Ok?cyan:new Color32(232,159,158,255);
                resultText.text=(result.Ok?"合成完成\n":"合成未完成\n")+(result.Reason??"")+"\n"+(result.OutputSummary??"");
            }
            refreshPending=true;
        }

        private void CancelTop()
        {
            if(!isOpen||cancelFrame==Time.frameCount)return;
            cancelFrame=Time.frameCount;
            if(IsQuantityFocused)
            {
                quantityField.DeactivateInputField();
                if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                return;
            }
            Close();
        }

        public void OnCancel(BaseEventData eventData)
        {
            if(!isOpen)return;
            if(inventory!=null)inventory.ConsumeCommerceCancel();
            CancelTop();eventData.Use();
        }

        private void OnApplicationFocus(bool focused){if(!focused)Close();}
        private void OnDisable(){Close();}
        private void OnDestroy(){Close();if(font!=null)Destroy(font);}
    }

    /// <summary>让手柄选中的配方在实际ScrollRect视口内；不参与库存或输入轮询。</summary>
    internal sealed class CraftingRecipeSelection : MonoBehaviour,ISelectHandler
    {
        private UnityEngine.UI.ScrollRect scroll;
        private RectTransform row;
        public void Configure(UnityEngine.UI.ScrollRect scroll,RectTransform row){this.scroll=scroll;this.row=row;}
        public void OnSelect(BaseEventData eventData)
        {
            if(scroll==null||row==null||scroll.content==null||scroll.viewport==null)return;
            Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,row);
            var view=scroll.viewport.rect;var delta=Vector2.zero;
            if(scroll.vertical&&bounds.min.y<view.yMin)delta.y=view.yMin-bounds.min.y;
            else if(scroll.vertical&&bounds.max.y>view.yMax)delta.y=view.yMax-bounds.max.y;
            if(scroll.horizontal&&bounds.min.x<view.xMin)delta.x=view.xMin-bounds.min.x;
            else if(scroll.horizontal&&bounds.max.x>view.xMax)delta.x=view.xMax-bounds.max.x;
            if(delta.sqrMagnitude>.0001f){scroll.StopMovement();scroll.content.anchoredPosition+=delta;}
        }
    }

    /// <summary>InputSystemUIInputModule只向当前选中物体发送cancel，不向父物体冒泡。</summary>
    internal sealed class CraftingControlCancel : MonoBehaviour,ICancelHandler
    {
        private CraftingUIRoot owner;
        public void Configure(CraftingUIRoot value){owner=value;}
        public void OnCancel(BaseEventData eventData){if(owner!=null)owner.OnCancel(eventData);}
    }
}
