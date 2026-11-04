/* 功能：提供独立、可折叠的中文角色光感面板；按明确接收者调节运行时副本、同对象A/B与显式保存候选配置。
 * 边界：不切真正主控、不写共享Profile、不读取设备、不暂停世界；不新建Bloom后端，不改原环境/HUD。
 * 协作：复用原协调器和地图的owner输入租约；仅停止自有环境时钟。真实渲染诊断由显式服务委托提供。
 * 生命周期：打开时捕获当前运行参数；恢复按钮归还该副本与本owner时钟修改。禁用/销毁释放输入、时钟和自有Canvas，不覆盖外部后改时钟。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Ossastra.CharacterLighting;
using Ossastra.Characters;
using Ossastra.EnvironmentSimulation;
using Ossastra.MapResearch;
namespace Ossastra.Development.CharacterLighting
{
 [DisallowMultipleComponent]
 public sealed class CharacterLightingPanel:MonoBehaviour
 {
  [Header("明确场景引用；观察选择不改变真正主控")]
  public CharacterLightingReceiver[] receivers=Array.Empty<CharacterLightingReceiver>();
  public EnvironmentRoot environment;
  public CharacterControlCoordinator coordinator;
  public MapResearchRoot map;
  public Ossastra.Development.Environment.EnvironmentSimulationPanel environmentPanel;
  public TMP_FontAsset font;
  [Header("与原环境面板并排；默认折叠")]
  public Vector2 anchor=new Vector2(1,1),offset=new Vector2(-404,-63);
  [Min(280)] public float width=350;
  [Min(260)] public float expandedHeight=620;
  [SerializeField] bool expanded;
  public bool Expanded=>expanded;
  public RectTransform Surface=>surface;
  public bool IsCapturingInput=>captured;
  public CharacterLightingReceiver ObservedReceiver=>observed;
  public string ObservedActorId=>observed==null||observed.Owner==null?"":observed.Owner.ActorId;
  public string Status{get;private set;}="角色光感面板尚未建立";
  // 委托只读取渲染服务自己的诊断，不由UI猜测灯、房间或GPU前后亮度。
  public Func<CharacterLightingReceiver,string> DiagnosticsReader;
  readonly HashSet<int> hovering=new HashSet<int>(),pressed=new HashSet<int>(),selected=new HashSet<int>();
  readonly List<Action> refreshers=new List<Action>();readonly List<UnityEngine.UI.Selectable> parameterControls=new List<UnityEngine.UI.Selectable>();
  readonly Dictionary<CharacterLightingReceiver,CharacterLightingSettings> before=new Dictionary<CharacterLightingReceiver,CharacterLightingSettings>();
  GameObject viewRoot,body,hintRoot;RectTransform surface;TextMeshProUGUI header,statusText,hintText,bloomReason;
  TMP_Dropdown observationDrop;UnityEngine.UI.Toggle freezeClock,bloomToggle;UnityEngine.UI.LayoutElement statusLayout;
  CharacterLightingPanelWidgets widgets;CharacterLightingReceiver observed;
  CharacterControlCoordinator capturedCoordinator;MapResearchRoot capturedMap;
  CharacterLightingPanelInputRelay hintOwner;bool captured,building,refreshing,previewCaptured;
  bool hasClockLease,clockBefore;EnvironmentTimeMode clockModeBefore,clockModeWritten;EnvironmentRoot leasedClock;
  float nextRefresh;CharacterLightingReceiver[] displayed=Array.Empty<CharacterLightingReceiver>();

  void OnEnable(){if(!Application.isPlaying)return;if(viewRoot==null)Build();else viewRoot.SetActive(true);SetExpanded(expanded);}
  void Update()
  {
   if(!Application.isPlaying)return;if(viewRoot==null){Build();return;}ReconcileCapture();
   if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.12f;RefreshWidgets();
  }
  void Build()
  {
   if(building||viewRoot!=null)return;
   if(EventSystem.current==null){Status="缺少原场EventSystem；未建立不能交互的窗口";return;}
   building=true;
   try
   {
    widgets=new CharacterLightingPanelWidgets(this,ResolveFont());
    viewRoot=new GameObject("CharacterLightingPanelCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
    viewRoot.transform.SetParent(transform,false);viewRoot.hideFlags=HideFlags.DontSave;var canvas=viewRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2401;
    var scaler=viewRoot.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
    surface=widgets.Rect("CharacterLightingPanel",viewRoot.transform);surface.anchorMin=surface.anchorMax=anchor;surface.pivot=anchor;surface.anchoredPosition=offset;widgets.Image(surface,CharacterLightingPanelWidgets.Background,true);widgets.Relay(surface.gameObject);
    var border=surface.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectColor=CharacterLightingPanelWidgets.Line;border.effectDistance=new Vector2(1,-1);
    var title=widgets.Rect("Header",surface);title.anchorMin=new Vector2(0,1);title.anchorMax=Vector2.one;title.pivot=new Vector2(.5f,1);title.sizeDelta=new Vector2(0,35);var button=title.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=widgets.Image(title,new Color(.09f,.15f,.16f,1),true);header=widgets.Text("Label",title,"角色光感",16);widgets.Stretch(header.rectTransform,10,0,10,0);button.onClick.AddListener(()=>SetExpanded(!expanded));widgets.Relay(title.gameObject,"展开或收起角色光感；世界仍正常运行。只有指针或导航在本窗口时才捕获输入。");
    var bodyRect=widgets.Rect("Body",surface);widgets.Stretch(bodyRect,8,8,8,40);body=bodyRect.gameObject;RectTransform content;widgets.ScrollBody(bodyRect,out content);
    BuildControls(content);BuildHint();RefreshReceiverList();Status="仅调节运行时副本；原Profile与环境夜间基线保持";RefreshWidgets();
   }
   catch(Exception error){Status="角色光感面板建立失败："+error.Message;Debug.LogException(error,this);if(viewRoot!=null)Destroy(viewRoot);viewRoot=null;refreshers.Clear();parameterControls.Clear();}
   finally{building=false;}
  }
  TMP_FontAsset ResolveFont()
  {
   if(font!=null)return font;
   if(environmentPanel!=null)
   {if(environmentPanel.font!=null)return environmentPanel.font;var label=environmentPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t=>t.font!=null);if(label!=null)return label.font;}
   return TMP_Settings.defaultFontAsset;
  }
  void BuildControls(RectTransform content)
  {
   observationDrop=widgets.Dropdown(content,"ObservedCharacter","观察 / 调节对象（不切主控）",SelectObserved,"选择被观察与调节的正式接收者，不改变CharacterControlCoordinator.Current，也不移动角色。");
   widgets.Title(content,"独立效果与同对象对照");
   SettingToggle(content,"EnvironmentTintEnabled","环境染色",s=>s.environmentTint,(s,v)=>s.environmentTint=v,"只调环境贡献色度；直射与局部灯保留各自颜色。强度0回对应旧路径。");
   SettingToggle(content,"ContourEnabled","受光细轮廓",s=>s.rim,(s,v)=>s.rim=v,"对完整角色的内侧细带提亮；无有效灯不产生轮廓，不能获得墙后或黑幕可见权。");
   SettingToggle(content,"HighlightProtectionEnabled","强光高亮保护",s=>s.highlightProtection,(s,v)=>s.highlightProtection=v,"压缩角色本体与轮廓合计光照增益的过亮段；不调全局曝光，也不硬截白。");
   AddParameter(widgets.Button(content,"OldEffect","旧效果",()=>ApplyPreset(0),"关闭三个新增效果，保留当前原受光与夜间最低可读度；不是切回另一名角色。"));
   AddParameter(widgets.Button(content,"EnvironmentOnly","仅环境染色",()=>ApplyPreset(1),"只启用此对象的环境染色；轮廓与高亮保护关闭，使用同一角色比较。"));
   AddParameter(widgets.Button(content,"RestrainedContour","染色 + 克制轮廓",()=>ApplyPreset(2),"启用环境融合、细内轮廓与高亮保护。数值为可继续调节的候选，不代表最终审美定稿。"));
   widgets.Title(content,"环境与轮廓参数");
   SettingSlider(content,"TintStrength","环境染色强度",0,1,s=>s.tintStrength,(s,v)=>s.tintStrength=v,"环境染色强度；手动天空同步的总开关与颜色来源仍归环境服务。");
   SettingSlider(content,"RimStrength","轮廓强度",0,2,s=>s.rimStrength,(s,v)=>s.rimStrength=v,"方向性轮廓的受控贡献倍率。灯资格、真实遮光与像素许可不会被此值放宽。");
   SettingSlider(content,"InnerWidth","内带宽度（最终像素）",.25f,4,s=>s.innerWidth,(s,v)=>s.innerWidth=v,"参考1080p约1–2个最终画面像素；不是图集像素，外扩始终为0。");
   SettingSlider(content,"Softness","内带柔度",.05f,2,s=>s.softness,(s,v)=>s.softness=v,"只柔化内部窄带，不向墙面、黑幕或角色外侧扩光。");
   SettingSlider(content,"MaxRimGain","轮廓贡献上限",0,2,s=>s.maxRimGain,(s,v)=>s.maxRimGain=v,"轮廓独立预算；主体强光下额外轮廓还会收敛，避免变成常亮白圈。");
   widgets.Title(content,"高亮软肩与环境时钟");
   SettingSlider(content,"HighlightKnee","保护拐点（线性增益）",.05f,16,s=>s.knee,(s,v)=>s.knee=v,"低于拐点维持原响应；高于拐点逐渐压缩，保留原图深浅纹理。");
   SettingSlider(content,"HighlightCeiling","保护渐近上限",.1f,32,s=>s.ceiling,(s,v)=>s.ceiling=v,"必须大于拐点；输入会由同一Settings.Sanitize校验，不逐通道硬剪RGB。");
   freezeClock=widgets.Toggle(content,"FreezeClock","固定环境时刻（世界不停）",SetClockFrozen,"只停止明确EnvironmentRoot的演示时钟；不暂停天气、角色或AI。外部时刻源模式不可用。");
   bloomToggle=widgets.Toggle(content,"BloomEnabled","Bloom（本轮不依赖）",v=>{},"本轮不创建或接管Bloom后端。当前未登记可安全控制的Bloom，故此开关不可用；基础受光仍必须成立。");bloomToggle.interactable=false;bloomToggle.SetIsOnWithoutNotify(false);
   bloomReason=widgets.Text("BloomUnavailableReason",widgets.Row(content,"BloomReason",49),"Bloom未接管；基础效果不依赖Bloom。",12);bloomReason.enableWordWrapping=true;
   widgets.Title(content,"恢复、候选保存与真实诊断");
   widgets.Button(content,"RestorePreview","一键恢复进入面板前状态",RestorePreview,"恢复本轮打开窗口时捕获的角色运行副本；只归还本窗口拥有的时钟修改，不回写共享SO或存档。");
   var save=widgets.Button(content,"SaveCandidateProfile","显式保存此对象候选Profile",SaveCandidateProfile,"仅Editor：保存一个新的唯一候选资产，不覆盖原共享Profile，也不自动替换角色当前Profile引用。");AddParameter(save);if(!Application.isEditor)save.interactable=false;
   var status=widgets.Row(content,"Status",210);statusLayout=status.GetComponent<UnityEngine.UI.LayoutElement>();statusText=widgets.Text("Text",status,"等待真实诊断",12);statusText.enableWordWrapping=true;statusText.overflowMode=TextOverflowModes.Overflow;statusText.alignment=TextAlignmentOptions.TopLeft;
  }
  void AddParameter(UnityEngine.UI.Selectable control){parameterControls.Add(control);}
  void SettingToggle(Transform parent,string name,string title,Func<CharacterLightingSettings,bool> read,Action<CharacterLightingSettings,bool> write,string hint)
  {var toggle=widgets.Toggle(parent,name,title,v=>Change(s=>write(s,v)),hint);AddParameter(toggle);refreshers.Add(()=>toggle.SetIsOnWithoutNotify(observed!=null&&read(observed.Settings)));}
  void SettingSlider(Transform parent,string name,string title,float min,float max,Func<CharacterLightingSettings,float> read,Action<CharacterLightingSettings,float> write,string hint)
  {TextMeshProUGUI label;var slider=widgets.Slider(parent,name,title,min,max,v=>Change(s=>write(s,v)),hint,out label);AddParameter(slider);refreshers.Add(()=>{float value=observed==null?min:read(observed.Settings);slider.SetValueWithoutNotify(value);label.text=title+"  "+value.ToString("0.00");});}
  void Change(Action<CharacterLightingSettings> change)
  {
   if(refreshing||observed==null||!observed.isActiveAndEnabled)return;CapturePreviewIfNeeded();
   change(observed.Settings);observed.Settings.Sanitize();Status="已调整 "+ActorLabel(observed)+" 的运行时副本；未写共享SO";RefreshWidgets();
  }
  public void ApplyPreset(int index)
  {
   if(observed==null)return;CapturePreviewIfNeeded();var state=before.TryGetValue(observed,out var saved)?saved.Clone():observed.Settings.Clone();
   state.environmentTint=index>0;state.rim=state.highlightProtection=index>1;observed.Settings=state;
   Status="当前对象对照："+new[]{"旧效果","仅环境染色","染色 + 克制轮廓"}[Mathf.Clamp(index,0,2)]+"；原环境亮度未改";RefreshWidgets();
  }
  public void SelectObserved(int index)
  {if(refreshing||index<0||index>=displayed.Length)return;observed=displayed[index];Status="观察 → "+ActorLabel(observed)+"；真正主控保持";RefreshWidgets();}
  static string ActorLabel(CharacterLightingReceiver receiver)
  {return receiver==null?"无对象":receiver.Owner==null?receiver.name:receiver.Owner.ActorId+" / "+receiver.Owner.name;}
  void RefreshReceiverList()
  {
   var next=(receivers??Array.Empty<CharacterLightingReceiver>()).Where(r=>r!=null&&r.gameObject.scene==gameObject.scene).Distinct().ToArray();
   if(next.SequenceEqual(displayed)&&observationDrop!=null&&observationDrop.options.Count>0)return;
   displayed=next;if(observed==null||!displayed.Contains(observed))observed=displayed.FirstOrDefault();
   if(observationDrop==null)return;observationDrop.ClearOptions();observationDrop.AddOptions(displayed.Length==0?new List<string>{"未登记正式角色"}:displayed.Select(ActorLabel).ToList());observationDrop.SetValueWithoutNotify(Mathf.Max(0,Array.IndexOf(displayed,observed)));observationDrop.interactable=displayed.Length>0;
  }
  void CapturePreviewIfNeeded()
  {if(previewCaptured)return;before.Clear();foreach(var receiver in receivers??Array.Empty<CharacterLightingReceiver>())if(receiver!=null&&!before.ContainsKey(receiver))before.Add(receiver,receiver.Settings.Clone());previewCaptured=true;}
  public void RestorePreview()
  {
   foreach(var pair in before)if(pair.Key!=null)pair.Key.Settings=pair.Value;ReleaseClock();
   Status=previewCaptured?"已恢复本次进入前的角色运行参数；原Profile未改":"尚无预览改动可恢复";RefreshWidgets();
  }
  public void SetClockFrozen(bool value)
  {
   if(refreshing)return;if(!value){ReleaseClock();RefreshWidgets();return;}
   if(environment==null||environment.timeMode==EnvironmentTimeMode.External){Status="时刻由外部来源或缺失环境根负责，不能在此冻结";RefreshWidgets();return;}
   if(!hasClockLease){leasedClock=environment;clockBefore=environment.clockRunning;clockModeBefore=environment.timeMode;hasClockLease=true;}
   environment.SetClockRunning(false);clockModeWritten=environment.timeMode;Status="仅固定环境时刻，世界继续运行";RefreshWidgets();
  }
  void ReleaseClock()
  {
   if(hasClockLease&&leasedClock!=null&&leasedClock.timeMode==clockModeWritten&&!leasedClock.clockRunning)
   {leasedClock.timeMode=clockModeBefore;leasedClock.clockRunning=clockBefore;leasedClock.RefreshNow();}
   hasClockLease=false;leasedClock=null;
  }
  void DiscardSupersededClockLease()
  {
   // 外部来源或别的作者已改写mode/clock时，本窗口仅撤销自己的记录，绝不把旧时钟写回去。
   if(!hasClockLease)return;
   if(leasedClock==null||leasedClock!=environment||leasedClock.timeMode!=clockModeWritten||leasedClock.clockRunning)
   {hasClockLease=false;leasedClock=null;Status="环境时钟已由外部接管；本窗口冻结记录已撤销";}
  }
  public void SaveCandidateProfile()
  {
   if(observed==null){Status="没有可保存的正式接收者";return;}
   try{string path;if(CharacterLightingProfileSave.TrySave(observed.Settings,observed.Owner==null?observed.name:observed.Owner.ActorId,out path))Status="已保存新候选："+path+"；原Profile未覆盖";else Status=path;}
   catch(Exception error){Status="候选未保存："+error.Message;Debug.LogWarning(Status,this);}RefreshWidgets();
  }
  void RefreshWidgets()
  {
   if(viewRoot==null||refreshing)return;DiscardSupersededClockLease();refreshing=true;
   try
   {
    RefreshReceiverList();foreach(var refresh in refreshers)refresh();bool valid=observed!=null&&observed.isActiveAndEnabled;foreach(var control in parameterControls)if(control!=null)control.interactable=valid&&(control.name!="SaveCandidateProfile"||Application.isEditor);
    if(header!=null)header.text=(expanded?"收起  ":"展开  ")+"角色光感";
    if(freezeClock!=null){freezeClock.interactable=environment!=null&&environment.timeMode!=EnvironmentTimeMode.External;freezeClock.SetIsOnWithoutNotify(hasClockLease&&leasedClock==environment&&environment!=null&&environment.timeMode!=EnvironmentTimeMode.External&&!environment.clockRunning);}
    if(bloomReason!=null)bloomReason.text="未登记可控制的Bloom后端；开关不可用。\n基础染色、轮廓与保护不依赖Bloom。";
    if(statusText!=null){statusText.text=Status+"\n"+ReadDiagnostics();if(statusLayout!=null)statusLayout.preferredHeight=Mathf.Max(210,statusText.GetPreferredValues(statusText.text,Mathf.Max(180,width-55),float.PositiveInfinity).y+14);}
   }
   finally{refreshing=false;}
  }
  string ReadDiagnostics()
  {
   if(observed==null)return "未登记角色接收者；不会自动搜索全场。";
   string profileName=observed.profile==null?"中性缺省 / 运行副本":observed.profile.name+" / 运行副本";
   string value="对象："+ActorLabel(observed)+"\nProfile："+profileName+"\n"+observed.Status;
   if(DiagnosticsReader!=null){try{return value+"\n"+DiagnosticsReader(observed);}catch(Exception e){return value+"\n服务诊断读取失败："+e.Message;}}
   return value+"\n渲染服务详细诊断尚未绑定，未推断room、灯或GPU亮度。";
  }
  public void SetExpanded(bool value)
  {
   if(value&&!expanded){previewCaptured=false;CapturePreviewIfNeeded();}else if(value)CapturePreviewIfNeeded();expanded=value;
   if(surface!=null){surface.sizeDelta=new Vector2(width,value?expandedHeight:35);surface.anchorMin=surface.anchorMax=anchor;surface.pivot=anchor;surface.anchoredPosition=offset;}
   if(!value){if(observationDrop!=null&&observationDrop.IsExpanded)observationDrop.Hide();ClearOwnSelection();hovering.Clear();pressed.Clear();selected.Clear();HideAllHints();}
   if(body!=null)body.SetActive(value);ReconcileCapture();RefreshWidgets();
  }
  void BuildHint()
  {
   var hint=widgets.Rect("CharacterLightingTooltip",surface);hint.anchorMin=hint.anchorMax=Vector2.zero;hint.pivot=new Vector2(0,1);hint.anchoredPosition=new Vector2(0,-6);hint.sizeDelta=new Vector2(width,82);widgets.Image(hint,CharacterLightingPanelWidgets.Background);hintRoot=hint.gameObject;hintText=widgets.Text("Text",hint,"");widgets.Stretch(hintText.rectTransform,8,6,8,6);hintText.enableWordWrapping=true;hintText.alignment=TextAlignmentOptions.TopLeft;hintRoot.SetActive(false);
  }
  internal void ShowHint(CharacterLightingPanelInputRelay relay,string text){if(hintText==null||string.IsNullOrWhiteSpace(text))return;hintOwner=relay;hintText.text=text;hintRoot.SetActive(true);}
  internal void HideHint(CharacterLightingPanelInputRelay relay){if(hintOwner==relay)HideAllHints();}
  void HideAllHints(){hintOwner=null;if(hintRoot!=null)hintRoot.SetActive(false);}
  internal void NotifyHover(CharacterLightingPanelInputRelay relay,bool value){if(value)hovering.Add(relay.GetInstanceID());else hovering.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void NotifyPressed(CharacterLightingPanelInputRelay relay,bool value){if(value)pressed.Add(relay.GetInstanceID());else pressed.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void NotifySelection(CharacterLightingPanelInputRelay relay,bool value){if(value)selected.Add(relay.GetInstanceID());else selected.Remove(relay.GetInstanceID());ReconcileCapture();}
  internal void ForgetPointer(CharacterLightingPanelInputRelay relay){hovering.Remove(relay.GetInstanceID());pressed.Remove(relay.GetInstanceID());selected.Remove(relay.GetInstanceID());HideHint(relay);ReconcileCapture();}
  void ReconcileCapture()
  {
   bool want=isActiveAndEnabled&&viewRoot!=null&&viewRoot.activeInHierarchy&&(hovering.Count>0||pressed.Count>0||selected.Count>0||(observationDrop!=null&&observationDrop.IsExpanded));
   if(captured&&(capturedCoordinator!=coordinator||capturedMap!=map))Capture(false);if(want!=captured)Capture(want);
  }
  void Capture(bool value)
  {
   if(!value){if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,false);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,false);capturedCoordinator=null;capturedMap=null;captured=false;return;}
   capturedCoordinator=coordinator;capturedMap=map;if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,true);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,true);captured=true;
  }
  void ClearOwnSelection(){if(EventSystem.current==null||surface==null)return;var current=EventSystem.current.currentSelectedGameObject;if(current!=null&&current.transform.IsChildOf(surface))EventSystem.current.SetSelectedGameObject(null);}
  void OnApplicationFocus(bool focus){if(!focus){ClearOwnSelection();hovering.Clear();pressed.Clear();selected.Clear();Capture(false);HideAllHints();}}
  void OnDisable(){ClearOwnSelection();hovering.Clear();pressed.Clear();selected.Clear();Capture(false);ReleaseClock();HideAllHints();if(viewRoot!=null)viewRoot.SetActive(false);}
  void OnDestroy(){Capture(false);ReleaseClock();if(viewRoot!=null)Destroy(viewRoot);viewRoot=null;refreshers.Clear();parameterControls.Clear();before.Clear();DiagnosticsReader=null;}
 }
}


