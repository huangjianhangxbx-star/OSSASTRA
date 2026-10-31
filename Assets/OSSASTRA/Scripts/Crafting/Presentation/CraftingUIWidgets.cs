/* 脚本：CraftingUIWidgets（CraftingUIRoot的uGUI组装）
 * 功能：黑底细线两列、类别与配方ScrollRect、可滚动详情/材料/回执、显式导航。
 * 边界：只持有表现资源；不复制原游戏UI、不派生库存计数、不另建EventSystem/InputAction。
 */
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ossastra.Crafting.Presentation
{
    public sealed partial class CraftingUIRoot
    {
        private RectTransform Node(Transform parent,string name,Rect rect)
        {
            var node=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            node.SetParent(parent,false);node.anchorMin=node.anchorMax=node.pivot=new Vector2(0,1);
            node.anchoredPosition=new Vector2(rect.x,-rect.y);node.sizeDelta=rect.size;return node;
        }

        private UnityEngine.UI.Image Image(Transform parent,string name,Rect rect,Color color,Sprite sprite=null,bool raycast=false)
        {
            var image=Node(parent,name,rect).gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.sprite=sprite;image.raycastTarget=raycast;
            if(sprite!=null&&sprite.border.sqrMagnitude>0)image.type=UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        private UnityEngine.UI.Text Text(Transform parent,string name,Rect rect,string value,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var text=Node(parent,name,rect).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font=font;text.fontSize=size;text.color=color;text.text=value;text.alignment=align;text.raycastTarget=false;
            text.supportRichText=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;
        }

        private UnityEngine.UI.Button Button(Transform parent,string name,Rect rect,string caption,Action clicked)
        {
            var image=Image(parent,name,rect,new Color32(22,21,28,255),skin.slotFill,true);
            var border=Image(image.transform,"Edge",new Rect(0,0,rect.width,rect.height),edge,skin.slotEdge);
            Stretch(border.rectTransform);
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            image.gameObject.AddComponent<CraftingControlCancel>().Configure(this);
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color32(180,210,207,255);colors.selectedColor=new Color32(180,210,207,255);colors.pressedColor=new Color32(140,170,170,255);colors.disabledColor=new Color32(105,105,112,255);button.colors=colors;
            button.onClick.AddListener(()=>clicked());
            var label=Text(image.transform,"Label",new Rect(8,3,rect.width-16,rect.height-6),caption,16,paper,TextAnchor.MiddleCenter);
            Stretch(label.rectTransform,8,3,-8,-3);return button;
        }

        private static void Stretch(RectTransform node,float left=0,float bottom=0,float right=0,float top=0)
        {node.anchorMin=Vector2.zero;node.anchorMax=Vector2.one;node.offsetMin=new Vector2(left,bottom);node.offsetMax=new Vector2(right,top);}

        private void Build()
        {
            font=Font.CreateDynamicFontFromOSFont(skin.fontName,18);
            var go=new GameObject("CraftingCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));go.transform.SetParent(transform,false);
            canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=sortingOrder;
            // 全屏真实射线屏障；鼠标点击与滚轮不会穿过窗口落到世界。
            var barrier=go.AddComponent<UnityEngine.UI.Image>();barrier.color=new Color32(0,0,0,105);barrier.raycastTarget=true;
            panel=Image(go.transform,"CraftingPanel",new Rect(0,0,1100,700),ink,null,true).rectTransform;
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;
            var frame=Image(panel,"Frame",new Rect(0,0,1100,700),edge,skin.frame);if(skin.frame!=null)frame.fillCenter=false;
            Image(panel,"HeaderAccent",new Rect(24,26,4,46),edge);
            Text(panel,"Eyebrow",new Rect(42,21,910,22),"CRAFT  /  装备制作",13,cyan);
            stationTitle=Text(panel,"StationName",new Rect(42,45,920,38),"装备台",27,paper);
            closeButton=Button(panel,"Close",new Rect(1018,28,56,44),"×",Close);
            Image(panel,"HeaderRule",new Rect(24,94,1052,1),edge);
            Text(panel,"RecipeHeading",new Rect(25,112,370,26),"配方目录",18,paper);
            categoryContent=BuildCategoryScroll(panel,new Rect(24,150,376,42));
            recipeContent=BuildRecipeScroll(panel,new Rect(24,207,376,432));
            Text(panel,"ListHint",new Rect(25,651,370,26),"方向键 / 摇杆选择配方",12,muted);
            Image(panel,"ColumnRule",new Rect(424,112,1,562),edge);
            outputIcon=Image(panel,"OutputIcon",new Rect(451,112,86,86),Color.white);outputIcon.preserveAspect=true;
            recipeName=Text(panel,"RecipeName",new Rect(553,112,514,37),"请选择配方",23,paper);
            description=TextScroll(panel,"Description",new Rect(553,155,514,68),15,muted);
            Text(panel,"MaterialsHeading",new Rect(451,235,616,25),"所需材料   可用 / 需要",16,cyan);
            materials=TextScroll(panel,"Materials",new Rect(451,268,616,138),17,paper);
            output=TextScroll(panel,"OutputSummary",new Rect(451,418,616,54),15,muted);
            Text(panel,"QuantityLabel",new Rect(451,486,86,40),"制作份数",16,paper,TextAnchor.MiddleLeft);
            minusButton=Button(panel,"MinusQuantity",new Rect(548,486,40,40),"−",()=>StepQuantity(-1));
            var fieldBackground=Image(panel,"Quantity",new Rect(596,486,108,40),new Color32(22,21,28,255),skin.slotFill,true);
            quantityField=fieldBackground.gameObject.AddComponent<UnityEngine.UI.InputField>();quantityField.targetGraphic=fieldBackground;
            fieldBackground.gameObject.AddComponent<CraftingControlCancel>().Configure(this);
            quantityField.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;quantityField.characterLimit=9;
            quantityField.textComponent=Text(fieldBackground.transform,"Text",new Rect(8,3,92,34),"1",18,paper,TextAnchor.MiddleCenter);
            quantityField.onValueChanged.AddListener(QuantityChanged);
            plusButton=Button(panel,"PlusQuantity",new Rect(712,486,40,40),"＋",()=>StepQuantity(1));
            maximumText=Text(panel,"BatchLimit",new Rect(769,486,298,40),"",13,muted,TextAnchor.MiddleRight);
            reason=TextScroll(panel,"Reason",new Rect(451,535,616,40),15,cyan);
            confirmButton=Button(panel,"ConfirmCraft",new Rect(451,588,235,48),"合成",ConfirmSelection);
            resultText=TextScroll(panel,"CraftResult",new Rect(701,582,366,88),13,cyan);
            Text(panel,"InputHint",new Rect(451,654,235,26),"交互键确认 · 返回键关闭",12,muted);
            canvas.gameObject.SetActive(false);
        }

        private RectTransform BuildRecipeScroll(Transform parent,Rect rect)
        {
            var root=Node(parent,"RecipeScroll",rect);recipeScroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport=Image(root,"Viewport",new Rect(0,0,rect.width,rect.height),Color.white,null,true).rectTransform;
            var mask=viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();mask.showMaskGraphic=false;
            var content=Node(viewport,"Content",new Rect(0,0,rect.width,0));content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(2,8,2,2);
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            recipeScroll.viewport=viewport;recipeScroll.content=content;recipeScroll.horizontal=false;recipeScroll.vertical=true;
            recipeScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;recipeScroll.scrollSensitivity=30;return content;
        }

        private RectTransform BuildCategoryScroll(Transform parent,Rect rect)
        {
            var root=Node(parent,"CategoryScroll",rect);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();categoryScroll=scroll;
            var viewport=Image(root,"Viewport",new Rect(0,0,rect.width,rect.height),Color.white,null,true).rectTransform;
            var mask=viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();mask.showMaskGraphic=false;
            var content=Node(viewport,"Content",new Rect(0,0,0,rect.height));
            var layout=content.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=8;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=false;layout.childForceExpandHeight=true;
            var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;return content;
        }

        private UnityEngine.UI.Text TextScroll(Transform parent,string name,Rect rect,int size,Color color)
        {
            var root=Node(parent,name+"Scroll",rect);var scroll=root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport=Image(root,"Viewport",new Rect(0,0,rect.width,rect.height),Color.white,null,true).rectTransform;
            var mask=viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();mask.showMaskGraphic=false;
            var text=Text(viewport,"Content",new Rect(0,0,rect.width-10,rect.height),"",size,color);text.verticalOverflow=VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin=new Vector2(0,1);text.rectTransform.anchorMax=Vector2.one;text.rectTransform.sizeDelta=new Vector2(-10,0);
            var fit=text.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=text.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;return text;
        }

        private void BuildCategories()
        {
            ClearChildren(categoryContent);categoryButtons.Clear();
            for(var i=0;i<categoryNames.Count;i++)
            {
                var name=categoryNames[i];var width=Mathf.Clamp(name.Length*16+28,72,150);
                var button=Button(categoryContent,"Category_"+i,new Rect(0,0,width,40),name,()=>SelectCategory(name));
                var layout=button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredWidth=width;layout.preferredHeight=40;
                button.transform.Find("Edge").GetComponent<UnityEngine.UI.Image>().color=name==category?cyan:edge;categoryButtons.Add(button);
                button.gameObject.AddComponent<CraftingRecipeSelection>().Configure(categoryScroll,button.transform as RectTransform);
            }
        }

        private UnityEngine.UI.Button MakeRecipeRow(CraftingRecipePresentation row,Action clicked)
        {
            var button=Button(recipeContent,"Recipe_"+row.Id,new Rect(0,0,366,84),"",clicked);
            var layout=button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredHeight=84;layout.minHeight=84;
            var icon=Image(button.transform,"Icon",new Rect(14,14,56,56),Color.white,row.Icon);icon.preserveAspect=true;icon.enabled=row.Icon!=null;
            var label=button.transform.Find("Label").GetComponent<UnityEngine.UI.Text>();label.text=row.Name??row.Id;label.alignment=TextAnchor.UpperLeft;label.fontSize=17;
            label.rectTransform.anchorMin=new Vector2(0,1);label.rectTransform.anchorMax=Vector2.one;label.rectTransform.pivot=new Vector2(0,1);label.rectTransform.offsetMin=new Vector2(84,-47);label.rectTransform.offsetMax=new Vector2(-12,-12);
            var tag=Text(button.transform,"Category",new Rect(84,55,265,22),row.Category??"其他",12,muted);
            tag.rectTransform.anchorMax=new Vector2(1,1);tag.rectTransform.offsetMax=new Vector2(-12,-55);return button;
        }

        private void MakeEmptyList()
        {
            var empty=Text(recipeContent,"Empty",new Rect(0,0,350,110),"当前类别暂无配方",17,muted,TextAnchor.MiddleCenter);
            var layout=empty.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredHeight=110;
        }

        private void UpdateRecipeSelection()
        {
            for(var i=0;i<recipeButtons.Count;i++)
                recipeButtons[i].transform.Find("Edge").GetComponent<UnityEngine.UI.Image>().color=visibleRecipes[i].Id==selectedId?cyan:edge;
        }

        private static void ClearChildren(RectTransform content)
        {foreach(Transform child in content){child.gameObject.SetActive(false);UnityEngine.Object.Destroy(child.gameObject);}}

        private void Resize()
        {
            var scale=Mathf.Max(.1f,Mathf.Min(Screen.width/1434f,Screen.height/804f));
            if(!Mathf.Approximately(canvas.scaleFactor,scale))canvas.scaleFactor=scale;
        }

        private static void Navigation(UnityEngine.UI.Selectable item,UnityEngine.UI.Selectable up,UnityEngine.UI.Selectable down,UnityEngine.UI.Selectable left,UnityEngine.UI.Selectable right)
        {
            item.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,selectOnLeft=left,selectOnRight=right};
        }

        private void WireNavigation()
        {
            UnityEngine.UI.Selectable first=recipeButtons.Count>0?recipeButtons[0]:minusButton;
            UnityEngine.UI.Selectable last=recipeButtons.Count>0?recipeButtons[recipeButtons.Count-1]:minusButton;
            UnityEngine.UI.Selectable selectedRow=first;
            for(var i=0;i<recipeButtons.Count;i++)if(visibleRecipes[i].Id==selectedId)selectedRow=recipeButtons[i];
            for(var i=0;i<categoryButtons.Count;i++)Navigation(categoryButtons[i],closeButton,first,categoryButtons[Mathf.Max(0,i-1)],categoryButtons[Mathf.Min(categoryButtons.Count-1,i+1)]);
            for(var i=0;i<recipeButtons.Count;i++)Navigation(recipeButtons[i],i==0?(UnityEngine.UI.Selectable)(categoryButtons.Count>0?categoryButtons[0]:closeButton):recipeButtons[i-1],i+1<recipeButtons.Count?recipeButtons[i+1]:minusButton,recipeButtons[i],quantityField);
            Navigation(minusButton,selectedRow,confirmButton,last,quantityField);
            Navigation(quantityField,selectedRow,confirmButton,minusButton,plusButton);
            Navigation(plusButton,selectedRow,confirmButton,quantityField,confirmButton);
            Navigation(confirmButton,quantityField,closeButton,last,closeButton);
            Navigation(closeButton,confirmButton,categoryButtons.Count>0?categoryButtons[0]:first,confirmButton,first);
        }
    }
}
