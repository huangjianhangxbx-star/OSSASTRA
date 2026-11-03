/* 功能：创建环境面板自有uGUI叶节点，统一锚点、可读文字、滚动布局与指针租约转发。
 * 边界：只创建调用者指定父节点下的控件，不查找/重建原HUD，不推进环境或游戏状态。
 * 生命周期：所有节点归面板自有Canvas，跟随面板统一销毁。
 */
using System;
using TMPro;
using UnityEngine;
namespace Ossastra.Development.Environment
{
 internal sealed class EnvironmentPanelWidgets
 {
  readonly EnvironmentSimulationPanel owner;
  readonly TMP_FontAsset font;
  internal static readonly Color Background=new Color(.045f,.07f,.085f,.97f),Line=new Color(.31f,.52f,.43f,1),Ink=new Color(.89f,.94f,.92f,1);
  internal EnvironmentPanelWidgets(EnvironmentSimulationPanel owner,TMP_FontAsset font){this.owner=owner;this.font=font;}
  internal RectTransform Rect(string name,Transform parent)
  {
   var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
  }
  internal void Stretch(RectTransform r,float left=0,float bottom=0,float right=0,float top=0)
  {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);}
  internal UnityEngine.UI.Image Image(RectTransform rect,Color color,bool raycast=false)
  {var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=raycast;return image;}
  internal TextMeshProUGUI Text(string name,Transform parent,string value,int size=14)
  {
   var r=Rect(name,parent);Stretch(r);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;
   text.text=value;text.color=Ink;text.raycastTarget=false;text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Ellipsis;text.richText=false;
   text.alignment=TextAlignmentOptions.MidlineLeft;return text;
  }
  internal void Relay(GameObject go){var relay=go.AddComponent<EnvironmentPanelInputRelay>();relay.owner=owner;}
  internal RectTransform Row(Transform parent,string name,float height=34)
  {
   var r=Rect(name,parent);var layout=r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.preferredHeight=height;layout.minHeight=height;return r;
  }
  internal void Title(Transform parent,string text)
  {
   var r=Row(parent,"Section",29);var label=Text("Title",r,text,15);label.fontStyle=FontStyles.Bold;label.color=new Color(.7f,.9f,.78f,1);
  }
  internal UnityEngine.UI.Button Button(Transform parent,string name,string label,Action action)
  {
   var r=Row(parent,name,31);var image=Image(r,new Color(.12f,.19f,.19f,1),true);var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
   var edge=r.gameObject.AddComponent<UnityEngine.UI.Outline>();edge.effectColor=Line;edge.effectDistance=new Vector2(1,-1);
   Text("Label",r,label).alignment=TextAlignmentOptions.Center;button.onClick.AddListener(()=>action());Relay(r.gameObject);return button;
  }
  internal void Buttons(Transform parent,string name,string[] labels,Action<int> action)
  {
   var row=Row(parent,name,31);var group=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();group.spacing=5;group.childControlWidth=true;group.childControlHeight=true;group.childForceExpandWidth=true;group.childForceExpandHeight=true;
   for(int i=0;i<labels.Length;i++){int index=i;Button(row,"Button"+i,labels[i],()=>action(index));}
  }
  internal UnityEngine.UI.Toggle Toggle(Transform parent,string name,string label,bool initial,Action<bool> changed)
  {
   var row=Row(parent,name,31);var toggle=row.gameObject.AddComponent<UnityEngine.UI.Toggle>();var hit=Image(row,Color.clear,true);toggle.targetGraphic=hit;
   var box=Rect("Box",row);box.anchorMin=box.anchorMax=new Vector2(0,.5f);box.pivot=new Vector2(0,.5f);box.anchoredPosition=new Vector2(1,0);box.sizeDelta=new Vector2(21,21);Image(box,new Color(.09f,.14f,.14f,1));
   var mark=Rect("Check",box);Stretch(mark,4,4,4,4);toggle.graphic=Image(mark,new Color(.5f,.83f,.65f,1));var text=Text("Label",row,label);Stretch(text.rectTransform,30,0,0,0);
   toggle.isOn=initial;toggle.onValueChanged.AddListener(v=>changed(v));Relay(row.gameObject);return toggle;
  }
  internal UnityEngine.UI.Slider Slider(Transform parent,string name,string label,float min,float max,float initial,Action<float> changed,out TextMeshProUGUI caption)
  {
   var row=Row(parent,name+"Row",48);caption=Text("Label",row,label);caption.rectTransform.anchorMin=new Vector2(0,.5f);caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=Vector2.zero;caption.rectTransform.offsetMax=Vector2.zero;
   var r=Rect(name,row);r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,.5f);r.offsetMin=new Vector2(3,4);r.offsetMax=new Vector2(-3,-1);
   var track=Image(r,new Color(.1f,.17f,.17f,1),true);var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=false;
   var fillArea=Rect("FillArea",r);Stretch(fillArea,5,5,5,5);var fill=Rect("Fill",fillArea);Stretch(fill);slider.fillRect=fill;Image(fill,new Color(.33f,.59f,.46f,1));
   var handleArea=Rect("HandleArea",r);Stretch(handleArea,6,0,6,0);var handle=Rect("Handle",handleArea);handle.anchorMin=new Vector2(0,0);handle.anchorMax=new Vector2(0,1);handle.sizeDelta=new Vector2(12,0);
   slider.handleRect=handle;slider.targetGraphic=Image(handle,new Color(.79f,.92f,.83f,1),true);slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;slider.value=initial;
   slider.onValueChanged.AddListener(v=>changed(v));Relay(r.gameObject);return slider;
  }
  internal TMP_Dropdown Dropdown(Transform parent,string name,string title,string[] options,Action<int> changed)
  {
   var row=Row(parent,name+"Row",61);var label=Text("Title",row,title);label.rectTransform.anchorMin=new Vector2(0,.55f);label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
   var r=Rect(name,row);r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,.55f);r.offsetMin=new Vector2(0,1);r.offsetMax=new Vector2(0,-1);var image=Image(r,new Color(.12f,.19f,.19f,1),true);
   var dropdown=r.gameObject.AddComponent<TMP_Dropdown>();dropdown.targetGraphic=image;var caption=Text("Caption",r,"");Stretch(caption.rectTransform,8,0,20,0);dropdown.captionText=caption;
   var arrow=Text("Arrow",r,"▾");arrow.alignment=TextAlignmentOptions.MidlineRight;Stretch(arrow.rectTransform,0,0,7,0);
   var template=Rect("Template",r);template.anchorMin=new Vector2(0,0);template.anchorMax=new Vector2(1,0);template.pivot=new Vector2(.5f,1);template.anchoredPosition=Vector2.zero;template.sizeDelta=new Vector2(0,Mathf.Min(185,options.Length*31+6));Image(template,Background,true);
   var scroll=template.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
   var viewport=Rect("Viewport",template);Stretch(viewport);Image(viewport,Color.white,true);viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
   var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,31);
   var item=Rect("Item",content);item.anchorMin=new Vector2(0,.5f);item.anchorMax=new Vector2(1,.5f);item.sizeDelta=new Vector2(0,31);var toggle=item.gameObject.AddComponent<UnityEngine.UI.Toggle>();toggle.targetGraphic=Image(item,new Color(.15f,.26f,.22f,1),true);
   var check=Rect("Check",item);check.anchorMin=check.anchorMax=new Vector2(0,.5f);check.pivot=new Vector2(0,.5f);check.anchoredPosition=new Vector2(4,0);check.sizeDelta=new Vector2(15,15);toggle.graphic=Image(check,new Color(.55f,.86f,.67f,1));
   var itemText=Text("ItemLabel",item,"");Stretch(itemText.rectTransform,27,0,5,0);dropdown.itemText=itemText;dropdown.template=template;scroll.viewport=viewport;scroll.content=content;Relay(item.gameObject);template.gameObject.SetActive(false);
   dropdown.options=options.SelectOptionData();dropdown.onValueChanged.AddListener(v=>changed(v));Relay(r.gameObject);return dropdown;
  }
 }
 internal static class EnvironmentDropdownOptions
 {
  internal static System.Collections.Generic.List<TMP_Dropdown.OptionData> SelectOptionData(this string[] values)
  {var result=new System.Collections.Generic.List<TMP_Dropdown.OptionData>();foreach(var value in values)result.Add(new TMP_Dropdown.OptionData(value));return result;}
 }
}
