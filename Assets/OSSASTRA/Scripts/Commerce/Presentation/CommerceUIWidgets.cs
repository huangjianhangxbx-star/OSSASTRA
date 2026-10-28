/* 脚本：CommerceUIWidgets（CommerceUIRoot的uGUI构建部分）
 * 功能：以现有InventorySkin与物品Sprite创建三列贸易、目标旁菜单与SAMPLE对话表面。
 * 边界：固定参照几何经同一缩放用于绘制与射线；ScrollRect仅管理表现，不保存库存。
 */
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ossastra.Commerce.Presentation
{
    public sealed partial class CommerceUIRoot
    {
        private RectTransform Node(Transform parent,string name,Rect rect)
        {
            var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rect.x,-rect.y);rt.sizeDelta=rect.size;return rt;
        }
        private UnityEngine.UI.Image CreateImage(Transform parent,string name,Rect rect,Color color,Sprite sprite=null,bool raycast=false)
        {
            var image=Node(parent,name,rect).gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.sprite=sprite;
            if(sprite!=null&&sprite.border.sqrMagnitude>0)image.type=UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget=raycast;return image;
        }
        private UnityEngine.UI.Text CreateText(Transform parent,string name,Rect rect,string value,int size,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var text=Node(parent,name,rect).gameObject.AddComponent<UnityEngine.UI.Text>();text.font=name=="Title"?serif:font;text.fontSize=size;
            text.color=color??textColor;text.text=value;text.raycastTarget=false;text.alignment=align;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }
        private UnityEngine.UI.Button CreateButton(Transform parent,string name,Rect rect,string caption,Action clicked,Color? fill=null)
        {
            var image=CreateImage(parent,name,rect,fill??new Color32(22,30,35,245),skin.slotFill,true);
            CreateImage(image.transform,"Edge",new Rect(0,0,rect.width,rect.height),new Color32(129,155,160,255),skin.slotEdge);
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(()=>clicked());
            CreateText(image.transform,"Label",new Rect(5,2,rect.width-10,rect.height-4),caption,14,null,TextAnchor.MiddleCenter);return button;
        }
        private RectTransform CreateWindow(string name,Rect rect,string title,Action close)
        {
            var background=CreateImage(surface,name,rect,new Color32(8,3,5,250),null,true);var rt=background.rectTransform;
            CreateImage(rt,"Frame",new Rect(0,0,rect.width,rect.height),Color.white,skin.frame).fillCenter=false;
            CreateImage(rt,"Header",new Rect(11,11,rect.width-22,28),new Color32(155,32,67,255));
            CreateText(rt,"Title",new Rect(16,11,rect.width-65,28),title,22);
            CreateButton(rt,"Close",new Rect(rect.width-37,12,25,25),"×",close);return rt;
        }
        private void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(skin.fontName,16);serif=Font.CreateDynamicFontFromOSFont("SimSun",22);
            var barrier=new GameObject("CommerceInteractionBarrier",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));barrier.transform.SetParent(transform,false);
            barrierCanvas=barrier.GetComponent<Canvas>();barrierCanvas.renderMode=RenderMode.ScreenSpaceOverlay;barrierCanvas.sortingOrder=149;
            var barrierImage=barrier.AddComponent<UnityEngine.UI.Image>();barrierImage.color=Color.clear;barrierImage.raycastTarget=true;
            var barrierButton=barrier.AddComponent<UnityEngine.UI.Button>();barrierButton.targetGraphic=barrierImage;barrierButton.transition=UnityEngine.UI.Selectable.Transition.None;barrierButton.onClick.AddListener(()=>{if(IsOpen&&!IsTrading)Close();});
            var go=new GameObject("CommerceCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=180;surface=(RectTransform)go.transform;
            roleMenu=Node(surface,"RoleMenu",new Rect(0,0,220,320));var arc=roleMenu.gameObject.AddComponent<CommerceArcGraphic>();arc.raycastTarget=false;
            roleName=CreateText(roleMenu,"RoleName",new Rect(170,80,180,30),"",14,gold);
            CreateMenuEntry("Shop",new Rect(91,14,116,46),"商店",CommerceMenuIcon.Shop,OpenTargetShop);
            CreateMenuEntry("Dialogue",new Rect(22,82,116,46),"对话",CommerceMenuIcon.Dialogue,OpenTargetDialogue);
            CreateMenuEntry("Gift",new Rect(22,161,116,46),"送礼物",CommerceMenuIcon.Gift,()=>menuNotice.text="送礼物 · 后续开放；背包中的给予/收回仍可使用");
            CreateMenuEntry("Equipment",new Rect(91,232,116,46),"角色装备",CommerceMenuIcon.Equipment,()=>menuNotice.text="角色装备菜单 · 后续开放；背包手槽仍可操作");
            menuNotice=CreateText(roleMenu,"Notice",new Rect(-10,285,250,45),"",12,gold,TextAnchor.MiddleCenter);
            dialogueWindow=CreateWindow("Dialogue",new Rect(500,200,420,300),"对话",Close);
            dialogueSpeaker=CreateText(dialogueWindow,"Speaker",new Rect(20,48,320,24),"",14,new Color32(195,216,213,255));
            dialogueText=CreateText(dialogueWindow,"DialogueText",new Rect(20,82,380,115),"",18);
            dialogueChoices=Node(dialogueWindow,"Choices",new Rect(20,205,376,170));
            dialogueHint=CreateText(dialogueWindow,"Hint",new Rect(20,200,330,24),"",12,new Color32(156,171,175,255));
            nextDialogue=CreateButton(dialogueWindow,"Continue",new Rect(285,225,115,28),"继续",()=>AdvanceDialogue(-1));
            shelfWindow=CreateWindow("MerchantShelf",new Rect(941,55,475,470),"",Close);
            shelfName=shelfWindow.Find("Title").GetComponent<UnityEngine.UI.Text>();shelfName.fontSize=18;shelfName.rectTransform.sizeDelta=new Vector2(360,28);
            refreshText=CreateText(shelfWindow,"Refresh",new Rect(20,45,430,25),"",14,null,TextAnchor.MiddleRight);
            shelfContent=CreateShelfScroll(shelfWindow,new Rect(15,78,445,365));
            balanceText=CreateText(surface,"PaymentSources",new Rect(941,535,475,60),"",13,gold);
            saveText=CreateText(surface,"SaveStatus",new Rect(475,500,440,80),"",13,gold);
            retryButton=CreateButton(surface,"RetrySave",new Rect(475,585,160,32),"重试保存",()=>host?.RetrySave());
            detailWindow=CreateWindow("TradeDetail",new Rect(475,25,440,440),"交易",ClearSelection);
            itemIcon=CreateImage(detailWindow,"ItemIcon",new Rect(20,65,90,90),Color.white);itemIcon.preserveAspect=true;
            CreateText(detailWindow,"ItemFallback",new Rect(20,65,90,90),"",18,gold,TextAnchor.MiddleCenter);
            itemName=CreateText(detailWindow,"ItemName",new Rect(130,53,290,36),"",22,null,TextAnchor.MiddleCenter);
            itemDescription=CreateDetailScroll(detailWindow,new Rect(130,95,290,175));
            itemTags=CreateText(detailWindow,"Tags",new Rect(20,170,105,70),"",12,new Color32(193,209,214,255));
            priceText=CreateText(detailWindow,"Price",new Rect(20,274,400,24),"",14,gold,TextAnchor.MiddleRight);
            CreateText(detailWindow,"QuantityLabel",new Rect(235,303,75,28),"数量",13,null,TextAnchor.MiddleRight);
            var fieldBackground=CreateImage(detailWindow,"Quantity",new Rect(320,303,100,28),new Color32(32,42,49,255),null,true);
            quantityField=fieldBackground.gameObject.AddComponent<UnityEngine.UI.InputField>();quantityField.targetGraphic=fieldBackground;quantityField.contentType=UnityEngine.UI.InputField.ContentType.Standard;quantityField.characterLimit=12;
            quantityField.textComponent=CreateText(fieldBackground.transform,"Text",new Rect(6,2,88,24),"1",14,null,TextAnchor.MiddleLeft);
            quantityField.SetTextWithoutNotify("1");quantityField.onValueChanged.AddListener(QuantityChanged);
            confirmButton=CreateButton(detailWindow,"ConfirmTrade",new Rect(12,342,416,56),"",ConfirmSelection,new Color32(122,225,148,255));
            quoteButtonText=confirmButton.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();quoteButtonText.color=new Color32(16,45,32,255);
            detailNotice=CreateText(detailWindow,"Result",new Rect(15,405,410,30),"",11,gold);
            var persistenceGo=new GameObject("CommercePersistenceNotice",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));persistenceGo.transform.SetParent(transform,false);
            persistenceCanvas=persistenceGo.GetComponent<Canvas>();persistenceCanvas.renderMode=RenderMode.ScreenSpaceOverlay;persistenceCanvas.sortingOrder=185;
            persistencePanel=CreateImage(persistenceGo.transform,"Notice",new Rect(800,720,600,66),new Color32(8,12,16,245),skin.slotFill,false).rectTransform;
            CreateImage(persistencePanel,"Frame",new Rect(0,0,600,66),new Color32(157,32,69,255),skin.slotEdge);
            persistenceText=CreateText(persistencePanel,"Status",new Rect(12,8,445,48),"",13,gold);
            persistenceRetry=CreateButton(persistencePanel,"RetrySave",new Rect(470,15,116,36),"重试保存",()=>host?.RetrySave());
            SetPersistenceStatus(persistenceMessage,persistenceRetryable);persistenceCanvas.gameObject.SetActive(false);
            canvas.gameObject.SetActive(false);barrierCanvas.gameObject.SetActive(false);
        }
        private RectTransform CreateShelfScroll(Transform parent,Rect rect)
        {
            var root=Node(parent,"ShelfScroll",rect);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport=CreateImage(root,"Viewport",new Rect(0,0,rect.width,rect.height),new Color32(40,105,124,255),skin.slotFill,true).rectTransform;viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content=Node(viewport,"Content",new Rect(10,10,rect.width-20,rect.height-20));scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;return content;
        }
        private void CreateMenuEntry(string name,Rect rect,string text,CommerceMenuIcon icon,Action clicked)
        {
            var button=CreateButton(roleMenu,name,rect,text,clicked);
            var label=button.transform.Find("Label") as RectTransform;label.anchoredPosition=new Vector2(38,-2);label.sizeDelta=new Vector2(74,42);
            var graphic=Node(button.transform,"Icon",new Rect(10,11,24,24)).gameObject.AddComponent<CommerceMenuIconGraphic>();graphic.Kind=icon;graphic.raycastTarget=false;
            button.transform.Find("Edge").GetComponent<UnityEngine.UI.Image>().color=new Color32(181,165,115,255);
        }
        private UnityEngine.UI.Text CreateDetailScroll(Transform parent,Rect rect)
        {
            var root=Node(parent,"DescriptionScroll",rect);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport=CreateImage(root,"Viewport",new Rect(0,0,rect.width,rect.height),Color.clear,null,true).rectTransform;viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text=CreateText(viewport,"Content",new Rect(0,0,rect.width-8,rect.height),"",13);text.verticalOverflow=VerticalWrapMode.Overflow;
            var fit=text.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=text.rectTransform;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=22;return text;
        }
        private void BuildShelfCards(CommerceItemPresentation[] items)
        {
            foreach(Transform child in shelfContent)Destroy(child.gameObject);
            if(items.Length==0){CreateText(shelfContent,"Empty",new Rect(0,50,420,80),"暂无商品",18,null,TextAnchor.MiddleCenter);return;}
            const float pitchX=86,pitchY=113;
            shelfContent.sizeDelta=new Vector2(425,Mathf.Max(345,Mathf.Ceil(items.Length/5f)*pitchY));
            for(var i=0;i<items.Length;i++)
            {
                var item=items[i];if(item==null)continue;var rt=CreateButton(shelfContent,"Goods_"+item.definitionId,new Rect(i%5*pitchX,i/5*pitchY,80,105),"",()=>SelectGood(item),RarityColor(item.rarity)).transform;
                var image=CreateImage(rt,"Icon",new Rect(14,6,52,52),Color.white,item.icon);image.preserveAspect=true;image.enabled=item.icon!=null;
                var name=CreateText(rt,"Name",new Rect(3,59,74,17),item.displayName,10,null,TextAnchor.MiddleCenter);name.horizontalOverflow=HorizontalWrapMode.Wrap;
                CreateText(rt,"Stock",new Rect(3,77,74,13),"库存 "+item.stock,10,null,TextAnchor.MiddleCenter);
                CreateText(rt,"Price",new Rect(3,90,74,13),"◈ "+item.unitPrice,11,null,TextAnchor.MiddleCenter);
            }
        }
        private static Color RarityColor(string rarity)
        {
            switch((rarity??"").ToLowerInvariant())
            {case "blue":return new Color32(39,85,112,255);case "purple":return new Color32(81,51,106,255);case "gold":return new Color32(101,89,49,255);case "redgold":return new Color32(107,42,53,255);default:return new Color32(53,67,79,255);}
        }
        private void PositionRoleMenu(Vector2 screenPoint)
        {
            var scale=canvas.scaleFactor;var width=Screen.width/scale;var height=Screen.height/scale;var p=new Vector2(screenPoint.x/scale,(Screen.height-screenPoint.y)/scale);var right=p.x<240;
            var x=Mathf.Clamp(right?p.x+32:p.x-232,8,Mathf.Max(8,width-228));var y=Mathf.Clamp(p.y-162,8,Mathf.Max(8,height-328));roleMenu.anchoredPosition=new Vector2(x,-y);
            MoveRoleButton("Shop",right?13:91,14);MoveRoleButton("Dialogue",right?82:22,82);MoveRoleButton("Gift",right?82:22,161);MoveRoleButton("Equipment",right?13:91,232);
            var arc=roleMenu.GetComponent<CommerceArcGraphic>();arc.Flip=right;
            roleName.rectTransform.anchoredPosition=new Vector2(Mathf.Clamp(p.x-x-35,-20,150),-(p.y-y-87));
            var dialogueButton=roleMenu.Find("Dialogue")?.GetComponent<UnityEngine.UI.Button>();if(dialogueButton!=null)dialogueButton.interactable=allowDialogue;
            foreach(var name in new[]{"Gift","Equipment"}){var button=roleMenu.Find(name)?.GetComponent<UnityEngine.UI.Button>();if(button!=null)button.interactable=allowDialogue;}
        }
        private void MoveRoleButton(string name,float x,float y){var rt=roleMenu.Find(name) as RectTransform;if(rt!=null)rt.anchoredPosition=new Vector2(x,-y);}
        private void PositionDialogue(Vector2 screenPoint)
        {
            var scale=canvas.scaleFactor;var width=Screen.width/scale;var height=Screen.height/scale;var p=new Vector2(screenPoint.x/scale,(Screen.height-screenPoint.y)/scale);
            var size=dialogueWindow.sizeDelta;var y=p.y-size.y-40;if(y<8)y=p.y+35;
            dialogueWindow.anchoredPosition=new Vector2(Mathf.Clamp(p.x-size.x*.5f,8,Mathf.Max(8,width-size.x-8)),-Mathf.Clamp(y,8,Mathf.Max(8,height-size.y-8)));
        }
    }
}
