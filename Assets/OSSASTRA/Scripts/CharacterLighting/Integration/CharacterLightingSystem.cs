/* 功能：把登记角色的运行参数、同相机完整轮廓与既有环境光源接到唯一合法世界绘制。
 * 边界：不改源材质、角色控制、屋顶遮光或观察许可；点灯资格只作保守实体遮挡近似，不授予视野。
 * 生命周期：相机登记/每次绘制参数有明确作用域；失权、禁用、切相机立即清零，缓存资源归原PositivePass。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Ossastra.EnvironmentSimulation;
using Ossastra.EnvironmentSimulation.Rendering;
using Ossastra.SceneVisibility.Rooms.Rendering;
using Ossastra.CharacterLighting.Rendering;
using Ossastra.Development.CharacterLighting;
namespace Ossastra.CharacterLighting
{
 [DisallowMultipleComponent]
 public sealed class CharacterLightingSystem:MonoBehaviour
 {
  public Camera gameplayCamera;
  public EnvironmentSkyBinding environment;
  public CharacterLightingReceiver[] receivers=Array.Empty<CharacterLightingReceiver>();
  public Shader coverageShader;
  [Tooltip("外缘能量源、受限合成与当帧合法深度解析；缺任一引用时保守关闭外缘。")]
  public Shader outerGlowSeedShader,outerGlowCompositeShader,outerGlowDepthShader;
  public CharacterLightingPanel panel;
  public bool effectsEnabled=true;
  public event Action CoverageInvalidated;
  public string Status{get;private set;}="等待角色与环境登记";
  public int CoverageActors{get;private set;}
  public int CoverageDraws{get;private set;}
  public long CoverageBytes{get;private set;}
  public int OuterGlowDraws{get;private set;}
  public long OuterGlowBytes{get;private set;}
  public string OuterGlowStatus{get;private set;}="外缘尚未绘制";
  static readonly Dictionary<int,CharacterLightingSystem> cameras=new Dictionary<int,CharacterLightingSystem>();
  readonly List<RoomMaskVolume> rooms=new List<RoomMaskVolume>(32);
  readonly Dictionary<CharacterLightingReceiver,LampState> lamps=new Dictionary<CharacterLightingReceiver,LampState>();
  readonly List<CharacterLightingReceiver> staleReceivers=new List<CharacterLightingReceiver>(2);
  sealed class LampState{public Light selected,pending;public float weight;public int frame=-1;public string reason="无合格局部轮廓灯";}
  Camera registered;
  bool displayWasValid;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){cameras.Clear();ClearGlobals();}
  void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeginCamera;RenderPipelineManager.endCameraRendering+=EndCamera;EnvironmentCameraRegistry.Released+=EnvironmentReleased;Rebind();}
  void Update()
  {
   if(registered!=gameplayCamera)Rebind();if(panel!=null)panel.DiagnosticsReader=Describe;
   bool displayValid=TryGet(gameplayCamera,out var current)&&current==this;
   if(displayWasValid&&!displayValid){CoverageInvalidated?.Invoke();lamps.Clear();CoverageActors=CoverageDraws=0;CoverageBytes=0;ClearGlobals();}
   displayWasValid=displayValid;
   staleReceivers.Clear();foreach(var r in lamps.Keys)if(r==null||!r.isActiveAndEnabled||Array.IndexOf(receivers,r)<0||r.Owner==null||!r.Owner.isActiveAndEnabled||!r.Owner.IsAlive)staleReceivers.Add(r);
   foreach(var r in staleReceivers)lamps.Remove(r);
  }
  public void Rebind()
  {
   CoverageInvalidated?.Invoke();displayWasValid=false;Unregister();if(!isActiveAndEnabled||gameplayCamera==null)return;
   int id=gameplayCamera.GetInstanceID();if(cameras.TryGetValue(id,out var other)&&other!=null&&other!=this){Status="相机已有角色光感根；本实例未取得登记";return;}
   registered=gameplayCamera;cameras[id]=this;if(panel!=null)panel.DiagnosticsReader=Describe;
  }
  void BeginCamera(ScriptableRenderContext context,Camera camera){ClearGlobals();}
  void EndCamera(ScriptableRenderContext context,Camera camera){ClearGlobals();}
  static void ClearGlobals(){Shader.SetGlobalVector("_CharacterLightControls",Vector4.zero);Shader.SetGlobalFloat("_CharacterCoverageReady",0);Shader.SetGlobalVector("_CharacterRimLocalColor",Vector4.zero);Shader.SetGlobalVector("_CharacterOuterControls",Vector4.zero);}
  public static void Clear(CommandBuffer cmd){cmd.SetGlobalVector("_CharacterLightControls",Vector4.zero);cmd.SetGlobalFloat("_CharacterCoverageReady",0);cmd.SetGlobalVector("_CharacterRimLocalColor",Vector4.zero);cmd.SetGlobalVector("_CharacterOuterControls",Vector4.zero);}
  public static bool TryGet(Camera camera,out CharacterLightingSystem system)
  {system=null;return camera!=null&&cameras.TryGetValue(camera.GetInstanceID(),out system)&&system!=null&&system.gameplayCamera==camera&&system.isActiveAndEnabled&&system.effectsEnabled&&system.environment!=null&&system.environment.isActiveAndEnabled&&system.environment.environment!=null&&system.environment.environment.isActiveAndEnabled&&EnvironmentCameraRegistry.TryGet(camera,out var frame)&&frame.Enabled&&ReferenceEquals(frame,system.environment.environment.Current);}
  public bool WantsCoverage
  {get{foreach(var r in receivers)if(ValidReceiver(r)&&((r.Settings.rim&&r.Settings.rimStrength>0)||OuterEnabled(r.Settings)))return true;return false;}}
  public bool WantsOuterGlow
  {get{foreach(var r in receivers)if(ValidReceiver(r)&&OuterEnabled(r.Settings))return true;return false;}}
  public bool OuterDiagnostic
  {get{foreach(var r in receivers)if(ValidReceiver(r)&&OuterEnabled(r.Settings)&&r.Settings.outerDiagnostic)return true;return false;}}
  bool ValidReceiver(CharacterLightingReceiver r)=>r!=null&&r.gameplayCamera==gameplayCamera&&r.isActiveAndEnabled&&r.Owner!=null&&r.Owner.isActiveAndEnabled&&r.Owner.IsAlive;
  static bool OuterEnabled(CharacterLightingSettings s)=>s.outerGlow&&s.outerWidth>0&&s.outerStrength>0&&s.maxOuterGain>0;
  public void ReportOuterGlow(int draws,long bytes,string status){OuterGlowDraws=draws;OuterGlowBytes=bytes;OuterGlowStatus=status;}
  CharacterLightingReceiver ReceiverFor(Renderer renderer)
  {
   foreach(var r in receivers)if(ValidReceiver(r))foreach(var member in r.Members)
    if(member==renderer&&member!=null&&member.transform.IsChildOf(r.Owner.transform))return r;
   return null;
  }
  public void ReportCoverage(CharacterCoverageCache cache)
  {CoverageActors=cache==null?0:cache.ActiveActorCount;CoverageDraws=cache==null?0:cache.DrawCount;CoverageBytes=cache==null?0:cache.TextureBytes;Status=cache==null?"主体参数启用；轮廓缓存未占用":cache.Status;}
  public void Bind(CommandBuffer cmd,Renderer renderer,CharacterCoverageCache cache,RoomMaskFrame visibility)
  {
   Clear(cmd);var receiver=ReceiverFor(renderer);
   if(receiver==null)return;
   var settings=receiver.Settings;settings.Sanitize();
   cmd.SetGlobalVector("_CharacterLightControls",new Vector4(1,settings.rim?settings.rimStrength:0,settings.highlightProtection?1:0,settings.maxRimGain));
   cmd.SetGlobalVector("_CharacterLightShoulder",new Vector4(settings.knee,settings.ceiling,0,0));
   var tint=environment.ResolveStylizedSkyTint(environment.environment.Current);
   tint.w*=settings.environmentTint?settings.tintStrength:0;cmd.SetGlobalVector("_EnvironmentStylizedSkyTint",tint);
   cmd.SetGlobalVector("_CharacterRimShape",new Vector4(settings.innerWidth,settings.softness,0,0));
   if(!settings.rim||settings.rimStrength<=0||cache==null||!cache.Bind(cmd,renderer))return;
   BindLamp(cmd,receiver,visibility);
  }
  // 外缘与内带独立；亮度保底和内带预算都不能成为发光源或外缘开关。
  public bool BindOuter(CommandBuffer cmd,Renderer renderer,CharacterCoverageCache cache,RoomMaskFrame visibility)
  {
   Clear(cmd);var receiver=ReceiverFor(renderer);if(receiver==null)return false;
   var s=receiver.Settings;s.Sanitize();if(!OuterEnabled(s)||cache==null||!cache.Bind(cmd,renderer))return false;
   cmd.SetGlobalVector("_CharacterOuterControls",new Vector4(1,s.outerStrength,s.maxOuterGain,s.outerSoftness));
   cmd.SetGlobalVector("_CharacterOuterWidth",new Vector4(s.outerWidth,0,0,s.outerDiagnostic?1:0));
   BindLamp(cmd,receiver,visibility);return true;
  }
  void BindLamp(CommandBuffer cmd,CharacterLightingReceiver receiver,RoomMaskFrame visibility)
  {
   var lamp=ResolveLamp(receiver,visibility);if(lamp.selected==null||lamp.weight<=0)return;
   var light=lamp.selected;Color color=light.color.linear*light.intensity*lamp.weight;
   cmd.SetGlobalVector("_CharacterRimLocalPosition",new Vector4(light.transform.position.x,light.transform.position.y,light.transform.position.z,light.range));
   cmd.SetGlobalVector("_CharacterRimLocalColor",new Vector4(color.r,color.g,color.b,1));
  }
  LampState ResolveLamp(CharacterLightingReceiver receiver,RoomMaskFrame visibility)
  {
   if(!lamps.TryGetValue(receiver,out var state)){state=new LampState();lamps.Add(receiver,state);}if(state.frame==Time.frameCount)return state;state.frame=Time.frameCount;
   rooms.Clear();foreach(var room in environment.indoorRegions)if(room!=null&&room.isActiveAndEnabled)room.AppendMaskVolumes(rooms);
   Vector3 at=receiver.Owner.transform.position+Vector3.up*.8f;Bounds bounds=new Bounds(at,Vector3.zero);bool got=false;
   foreach(var r in receiver.Members)if(r!=null&&r.enabled&&r.gameObject.activeInHierarchy){if(!got){bounds=r.bounds;got=true;}else bounds.Encapsulate(r.bounds);}
   if(got)at=bounds.center;
   Light best=null;float bestScore=0,selectedScore=0;
   foreach(var light in environment.localLights)
   {
    if(light==null||!light.isActiveAndEnabled||light.type!=LightType.Point||light.intensity<=0||light.range<=0)continue;
    Vector3 position=light.transform.position;float distance=(position-at).sqrMagnitude;if(distance>=light.range*light.range||RoomIndex(at)!=RoomIndex(position))continue;
    if(visibility==null||!HasClearSegment(at,position,visibility.OpaqueBlockers)||!HasClearSegment(at+Vector3.up*Mathf.Min(.25f,bounds.extents.y*.5f),position,visibility.OpaqueBlockers)||!HasClearSegment(at-Vector3.up*Mathf.Min(.25f,bounds.extents.y*.5f),position,visibility.OpaqueBlockers))continue;
    float falloff=Mathf.Clamp01(1-distance/(light.range*light.range));float score=Luminance(light.color.linear)*light.intensity*falloff*falloff;
    if(light==state.selected)selectedScore=score;if(score>bestScore){bestScore=score;best=light;}
   }
   // 已选灯仍合格时使用15%滞回；换灯先灭后亮，避免插值虚构一盏穿墙移动的灯。
   if(state.selected!=null&&selectedScore>0&&bestScore<=selectedScore*1.15f)best=state.selected;
   float dt=Mathf.Clamp(Time.unscaledDeltaTime,0,.05f);
   if(selectedScore<=0){state.selected=null;state.weight=0;}
   if(best==null){state.selected=state.pending=null;state.weight=0;state.reason="局部灯缺失/异室/距离或实体遮挡拒绝";return state;}
   if(state.selected==null){state.selected=best;state.pending=null;state.weight=Mathf.MoveTowards(state.weight,1,dt/.1f);}
   else if(best!=state.selected){state.pending=best;state.weight=Mathf.MoveTowards(state.weight,0,dt/.05f);if(state.weight<=0){state.selected=state.pending;state.pending=null;}}
   else{state.pending=null;state.weight=Mathf.MoveTowards(state.weight,1,dt/.1f);}
   state.reason=state.selected==null?"无局部轮廓灯":state.selected.name+" / 保守三线段LOS / "+state.weight.ToString("F2");return state;
  }
  int RoomIndex(Vector3 point){for(int i=0;i<rooms.Count;i++){var local=rooms[i].WorldToLocal.MultiplyPoint3x4(point);var e=rooms[i].HalfExtents;if(Mathf.Abs(local.x)<=e.x+.002f&&Mathf.Abs(local.y)<=e.y+.002f&&Mathf.Abs(local.z)<=e.z+.002f)return i;}return -1;}
  static float Luminance(Color c)=>c.r*.2126f+c.g*.7152f+c.b*.0722f;
  // 只用本次明确登记的实体OBB，不用观察许可代替物理遮光；数据缺失时保守不放行。
  public static bool HasClearSegment(Vector3 from,Vector3 to,RoomMaskVolume[] blockers)
  {
   if(blockers==null||blockers.Length>64||!Finite(from)||!Finite(to))return false;
   foreach(var box in blockers)
   {
    if(!Finite(box.HalfExtents)||box.HalfExtents.x<=0||box.HalfExtents.y<=0||box.HalfExtents.z<=0)return false;
    for(int j=0;j<16;j++)if(float.IsNaN(box.WorldToLocal[j])||float.IsInfinity(box.WorldToLocal[j]))return false;
    Vector3 a=box.WorldToLocal.MultiplyPoint3x4(from),b=box.WorldToLocal.MultiplyPoint3x4(to),delta=b-a;float enter=0,exit=1;bool intersects=true;
    for(int axis=0;axis<3;axis++){float extent=box.HalfExtents[axis]+.002f;if(Mathf.Abs(delta[axis])<.000001f){if(Mathf.Abs(a[axis])>extent){intersects=false;break;}}else{float near=(-extent-a[axis])/delta[axis],far=(extent-a[axis])/delta[axis];if(near>far){float temp=near;near=far;far=temp;}enter=Mathf.Max(enter,near);exit=Mathf.Min(exit,far);if(enter>exit){intersects=false;break;}}}
    if(intersects&&exit>=.001f&&enter<=.999f)return false;
   }
   return true;
  }
  static bool Finite(Vector3 v)=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
  public string Describe(CharacterLightingReceiver receiver)
  {
   if(receiver==null)return "未选择观察角色";var text=receiver.Status+"\n参数：运行副本 / "+(receiver.profile==null?"中性缺省":receiver.profile.name);
   foreach(var r in receiver.Members)if(r!=null){text+="\n"+r.name+":";foreach(var m in r.sharedMaterials)if(m!=null)text+=" "+m.shader.name+(m.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT")?" [Straight]":" [源alpha]");}
   if(lamps.TryGetValue(receiver,out var lamp))text+="\n轮廓：主灯真实阴影＋"+lamp.reason;else text+="\n轮廓：等待合法相机绘制";
   var frame=environment==null||environment.environment==null?null:environment.environment.Current;
   float tint=frame==null||environment==null?0:environment.ResolveStylizedSkyTint(frame).w*(receiver.Settings.environmentTint?receiver.Settings.tintStrength:0);
   return text+"\n天空联动×角色响应="+tint.ToString("F2")+"；室内保持原环境色\n"+Status+"；仅两角色缓存 "+(CoverageBytes/1048576f).ToString("F2")+" MiB\n外缘："+OuterGlowStatus+"；draw "+OuterGlowDraws+" / "+(OuterGlowBytes/1048576f).ToString("F2")+" MiB";
  }
  void Unregister(){if(!ReferenceEquals(registered,null)&&cameras.TryGetValue(registered.GetInstanceID(),out var current)&&current==this)cameras.Remove(registered.GetInstanceID());registered=null;}
  void EnvironmentReleased(int id){if(!ReferenceEquals(registered,null)&&registered.GetInstanceID()==id){CoverageInvalidated?.Invoke();lamps.Clear();CoverageActors=CoverageDraws=0;CoverageBytes=0;ClearGlobals();}}
  void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderPipelineManager.endCameraRendering-=EndCamera;EnvironmentCameraRegistry.Released-=EnvironmentReleased;CoverageInvalidated?.Invoke();Unregister();lamps.Clear();CoverageActors=CoverageDraws=0;CoverageBytes=0;ClearGlobals();}
  void OnDestroy(){Unregister();lamps.Clear();}
 }
}
