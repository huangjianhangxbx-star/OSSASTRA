// Finite candidate controls. Owns runtime profile clones and only its pointer capture;
// never writes author profiles, gameplay, weather or Main defaults.
using UnityEngine;
using UnityEngine.InputSystem;
using Ossastra.SceneVisibility;
using Ossastra.MapResearch;
using Ossastra.Characters;

namespace Ossastra.WorldRendering.Stylization
{
 [DisallowMultipleComponent]
 public sealed class StylizedLookPanel : MonoBehaviour
 {
  public WorldLookController look;
  public SceneVisibilityRoot visibility;
  public MapResearchRoot map;
  public StylizedLookProfileSO blue,red;
  public LookDevFiniteToon finiteToon;
  public bool showPanel=true,expanded=true;
  [Range(0,2)] public int initialVariant=1;
  public int Variant {get;private set;}
  public StylizedLookProfileSO RuntimeProfile=>runtime;
  StylizedLookProfileSO before,runtime;
  CharacterControlCoordinator capturedCoordinator;
  MapResearchRoot capturedMap;
  bool captured;
  bool ownsToon;LookDevToonVariant priorToon,writtenToon;bool priorOutline,writtenOutline;float priorWidth,writtenWidth;Color priorDark;
  Font font;GUISkin skin;
  Rect Panel=>new Rect(Mathf.Max(8,Screen.width*.34f),110,350,expanded?470:32);
  void OnEnable(){if(!Application.isPlaying||look==null)return;before=look.stylizedLook;SetVariant(initialVariant);}
  public bool SetVariant(int variant)
  {
   if(!Application.isPlaying||look==null||variant<0||variant>2)return false;
   var profile=variant==1?blue:variant==2?red:null;
   if(variant>0&&profile==null)return false;
   var old=runtime;runtime=profile!=null?Instantiate(profile):null;
   if(runtime!=null){runtime.name=profile.name+" runtime candidate";runtime.hideFlags=HideFlags.HideAndDontSave;}
   Variant=variant;look.stylizedLook=runtime;if(old!=null)Destroy(old);
   if(variant==0&&finiteToon!=null){SetToon(LookDevToonVariant.Original);SetOutline(false,1);}
   else if(ownsToon&&runtime!=null)finiteToon.SetOutlineTheme(runtime.shadowColor.gamma);
   return true;
  }
  void OwnToon(){if(ownsToon||finiteToon==null)return;priorToon=finiteToon.Variant;priorOutline=finiteToon.OutlineEnabled;priorWidth=finiteToon.OutlineOutputPixels;priorDark=finiteToon.OutlineDarkLinear.gamma;writtenToon=priorToon;writtenOutline=priorOutline;writtenWidth=priorWidth;ownsToon=true;}
  void SetToon(LookDevToonVariant value){if(finiteToon==null)return;OwnToon();if(finiteToon.TrySetVariant(value,out _)){writtenToon=value;writtenOutline=finiteToon.OutlineEnabled;writtenWidth=finiteToon.OutlineOutputPixels;}if(runtime!=null)finiteToon.SetOutlineTheme(runtime.shadowColor.gamma);}
  void SetOutline(bool value,float width){if(finiteToon==null)return;OwnToon();finiteToon.SetOutline(value,width);writtenOutline=finiteToon.OutlineEnabled;writtenWidth=finiteToon.OutlineOutputPixels;if(runtime!=null)finiteToon.SetOutlineTheme(runtime.shadowColor.gamma);}
  void RestoreToon(){if(!ownsToon||finiteToon==null)return;if(finiteToon.Variant==writtenToon&&finiteToon.OutlineEnabled==writtenOutline&&Mathf.Approximately(finiteToon.OutlineOutputPixels,writtenWidth)){finiteToon.TrySetVariant(priorToon,out _);finiteToon.SetOutline(priorOutline,priorWidth);finiteToon.SetOutlineTheme(priorDark);}ownsToon=false;}
  public void SetPixelSize(int n){if(runtime!=null)runtime.pixelSize=n==2||n==4?n:1;}
  public void SetBands(int n){if(runtime==null)return;runtime.bandsEnabled=n>0;if(n>0)runtime.bandLevels=n==4?4:6;}
  void Update()
  {
   bool over=false;if(showPanel&&Mouse.current!=null){var p=Mouse.current.position.ReadValue();over=Panel.Contains(new Vector2(p.x,Screen.height-p.y));}
   var owner=visibility!=null?visibility.coordinator:null;
   if(captured&&(capturedCoordinator!=owner||capturedMap!=map))Capture(false);
   if(over!=captured)Capture(over);
  }
  void Capture(bool value)
  {
   if(!value){if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,false);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,false);capturedCoordinator=null;capturedMap=null;captured=false;return;}
   capturedCoordinator=visibility!=null?visibility.coordinator:null;capturedMap=map;
   if(capturedCoordinator!=null)capturedCoordinator.SetInputCapture(this,true);if(capturedMap!=null)capturedMap.SetUiCameraCapture(this,true);captured=true;
  }
  void EnsureSkin()
  {
   if(skin!=null)return;skin=Instantiate(GUI.skin);skin.hideFlags=HideFlags.HideAndDontSave;
   font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},14);
   if(font!=null){font.hideFlags=HideFlags.HideAndDontSave;skin.font=font;}
   skin.button.fontSize=14;skin.label.fontSize=14;skin.label.wordWrap=true;
  }
  void OnGUI()
  {
   if(!Application.isPlaying||!showPanel||look==null)return;
   EnsureSkin();var previous=GUI.skin;GUI.skin=skin;
   GUILayout.BeginArea(Panel,GUI.skin.box);
   try {
    if(GUILayout.Button((expanded?"收起":"展开")+" 实机风格候选 / "+(Variant==0?"原效果":Variant==1?"蓝色沉浸":"红色警戒")))expanded=!expanded;
    if(!expanded)return;
    GUILayout.BeginHorizontal();if(GUILayout.Button("原效果"))SetVariant(0);if(GUILayout.Button("蓝色沉浸"))SetVariant(1);if(GUILayout.Button("红色警戒"))SetVariant(2);GUILayout.EndHorizontal();
    if(runtime!=null){
     GUILayout.Label("输出像素块（HUD保持原分辨率）");GUILayout.BeginHorizontal();if(GUILayout.Button("无像素"))SetPixelSize(1);if(GUILayout.Button("2 像素"))SetPixelSize(2);if(GUILayout.Button("4 像素"))SetPixelSize(4);GUILayout.EndHorizontal();
     GUILayout.Label("主体亮度分档（空气与泛光不分档）");GUILayout.BeginHorizontal();if(GUILayout.Button("关闭"))SetBands(0);if(GUILayout.Button("6 档"))SetBands(6);if(GUILayout.Button("4 档"))SetBands(4);GUILayout.EndHorizontal();
     runtime.themeEnabled=GUILayout.Toggle(runtime.themeEnabled,"主题配色");
     runtime.airEnabled=GUILayout.Toggle(runtime.airEnabled,"解析空气（仅合法室外片元）");
     GUILayout.Label("空气上限 "+runtime.maxOpacity.ToString("0.00"));runtime.maxOpacity=GUILayout.HorizontalSlider(runtime.maxOpacity,0,1);
     runtime.bloomEnabled=GUILayout.Toggle(runtime.bloomEnabled,"宽泛光（独立角色外缘仅合成一次）");
     GUILayout.Label("泛光强度 "+runtime.bloomIntensity.ToString("0.00"));runtime.bloomIntensity=GUILayout.HorizontalSlider(runtime.bloomIntensity,0,2);
     GUILayout.BeginHorizontal();if(GUILayout.Button("正常"))runtime.debugView=0;if(GUILayout.Button("覆盖"))runtime.debugView=1;if(GUILayout.Button("雾量"))runtime.debugView=3;if(GUILayout.Button("泛光"))runtime.debugView=5;GUILayout.EndHorizontal();
    }
    if(finiteToon!=null){
     GUILayout.BeginHorizontal();if(GUILayout.Button("3D 原材质"))SetToon(LookDevToonVariant.Original);if(GUILayout.Button("主光三档"))SetToon(LookDevToonVariant.ThreeBand);if(finiteToon.RealToonAvailable&&GUILayout.Button("RealToon"))SetToon(LookDevToonVariant.RealToon);GUILayout.EndHorizontal();
     GUILayout.BeginHorizontal();if(GUILayout.Button("轮廓关"))SetOutline(false,1);if(GUILayout.Button("0.5 px"))SetOutline(true,.5f);if(GUILayout.Button("1 px"))SetOutline(true,1);if(GUILayout.Button("1.5 px"))SetOutline(true,1.5f);GUILayout.EndHorizontal();
    }
    if(GUILayout.Button("恢复本候选默认")){SetVariant(Variant==0?initialVariant:Variant);SetToon(LookDevToonVariant.Original);SetOutline(false,1);}
    GUILayout.Label("只改本场运行副本；世界持续运行。完整参数位于独立 Profile Inspector。");
   } finally {GUILayout.EndArea();GUI.skin=previous;}
  }
  void OnDisable(){Capture(false);RestoreToon();if(look!=null&&look.stylizedLook==runtime)look.stylizedLook=before;if(runtime!=null)Destroy(runtime);runtime=null;}
  void OnDestroy(){if(skin!=null)Destroy(skin);if(font!=null)Destroy(font);}
 }
}
