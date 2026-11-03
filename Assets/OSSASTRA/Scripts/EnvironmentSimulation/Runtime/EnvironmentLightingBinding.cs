/* 功能：把环境快照应用到明确登记的太阳、补光及三色底光；首次取得租约时保存原值，归还时精确恢复。
 * 边界：不寻找灯，不改局部点光、不改天空盒/反射/雾/GI/管线；跨场景不回写上一场景的全局值。
 */
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace Ossastra.EnvironmentSimulation
{
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class EnvironmentLightingBinding:MonoBehaviour
 {
  [Header("仅允许当前场景环境根与明确的方向灯")]
  public EnvironmentRoot root;
  public Light sun,fill;
  public bool HasLease=>owner!=null;
  static EnvironmentLightingBinding globalOwner;
  EnvironmentRoot owner;
  LightSnapshot sunBefore,fillBefore;
  AmbientSnapshot ambientBefore;
  bool hasSun,hasFill,pendingAmbient;
  struct LightSnapshot
  {
   internal Light light;internal bool enabled,useTemperature;internal Quaternion rotation;
   internal Color color;internal float intensity,temperature,shadowStrength;internal LightShadows shadows;
   internal static LightSnapshot Capture(Light light)=>new LightSnapshot{light=light,enabled=light.enabled,rotation=light.transform.rotation,color=light.color,intensity=light.intensity,useTemperature=light.useColorTemperature,temperature=light.colorTemperature,shadows=light.shadows,shadowStrength=light.shadowStrength};
   internal void Restore()
   {
    if(light==null)return;light.transform.rotation=rotation;light.color=color;light.intensity=intensity;light.useColorTemperature=useTemperature;light.colorTemperature=temperature;light.shadows=shadows;light.shadowStrength=shadowStrength;light.enabled=enabled;
   }
  }
  struct AmbientSnapshot
  {
   internal int sceneHandle;internal Light sun;internal AmbientMode mode;
   internal Color sky,equator,ground;internal float intensity;
   internal static AmbientSnapshot Capture()=>new AmbientSnapshot{sceneHandle=SceneManager.GetActiveScene().handle,sun=RenderSettings.sun,mode=RenderSettings.ambientMode,sky=RenderSettings.ambientSkyColor,equator=RenderSettings.ambientEquatorColor,ground=RenderSettings.ambientGroundColor,intensity=RenderSettings.ambientIntensity};
   internal void Restore(){RenderSettings.sun=sun;RenderSettings.ambientMode=mode;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;RenderSettings.ambientIntensity=intensity;}
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  static void ResetStatics(){globalOwner=null;}
  void OnEnable(){SceneManager.activeSceneChanged+=OnActiveSceneChanged;RestorePendingAmbient();}
  void OnDisable(){SceneManager.activeSceneChanged-=OnActiveSceneChanged;if(owner!=null)Release(owner);RestorePendingAmbient();}
  void OnDestroy(){if(owner!=null)Release(owner);RestorePendingAmbient();}
  void OnActiveSceneChanged(Scene before,Scene after){RestorePendingAmbient();}
  void RestorePendingAmbient()
  {
   if(!pendingAmbient||SceneManager.GetActiveScene().handle!=ambientBefore.sceneHandle||globalOwner!=null)return;
   ambientBefore.Restore();pendingAmbient=false;
  }
  public void Apply(EnvironmentRoot requestedOwner,EnvironmentFrame frame)
  {
   if(requestedOwner==null||frame==null)throw new ArgumentNullException("灯光租约需要根和完整快照。");
   if(!isActiveAndEnabled||!frame.Enabled){Release(requestedOwner);return;}
   if(requestedOwner.gameObject.scene!=SceneManager.GetActiveScene()||gameObject.scene!=requestedOwner.gameObject.scene)throw new InvalidOperationException("灯光绑定仅服务自己的当前场景。");
   if(root!=null&&root!=requestedOwner)throw new InvalidOperationException("灯光绑定已明确分配给另一个根。");
   if(owner!=null&&owner!=requestedOwner)throw new InvalidOperationException("灯光绑定已有其他租约。");
   if(globalOwner!=null&&globalOwner!=this)throw new InvalidOperationException("当前场景底光已有其他绑定。");
   if(sun!=null&&(sun.type!=LightType.Directional||sun.gameObject.scene!=gameObject.scene))throw new InvalidOperationException("太阳必须是本场景明确的方向灯。");
   if(fill!=null&&(fill==sun||fill.type!=LightType.Directional||fill.gameObject.scene!=gameObject.scene))throw new InvalidOperationException("补光必须是独立的本场景方向灯。");
   // 禁用Domain/Scene Reload时实例快照可跨子系统清空存活；重新确认原owner，不能丢掉归还底光的资格。
   if(owner==requestedOwner&&globalOwner==null)globalOwner=this;
   if(owner!=null&&((hasSun&&sunBefore.light!=sun)||(!hasSun&&sun!=null)||(hasFill&&fillBefore.light!=fill)||(!hasFill&&fill!=null)))Release(requestedOwner);
   if(owner==null)
   {
    RestorePendingAmbient();ambientBefore=AmbientSnapshot.Capture();owner=requestedOwner;globalOwner=this;
    hasSun=sun!=null;hasFill=fill!=null;if(hasSun)sunBefore=LightSnapshot.Capture(sun);if(hasFill)fillBefore=LightSnapshot.Capture(fill);
   }
   if(sun!=null)
   {
    sun.transform.rotation=Quaternion.LookRotation(-frame.KeyLightDirection,Mathf.Abs(frame.KeyLightDirection.y)>.999f?Vector3.forward:Vector3.up);
    sun.color=frame.SunColor;sun.useColorTemperature=false;sun.intensity=frame.SunIntensity;sun.shadows=LightShadows.Soft;sun.shadowStrength=frame.ShadowStrength;sun.enabled=frame.SunIntensity>0;
   }
   if(fill!=null)
   {
    Vector3 direction=new Vector3(-frame.KeyLightDirection.x,.55f,-frame.KeyLightDirection.z).normalized;
    fill.transform.rotation=Quaternion.LookRotation(-direction,Vector3.up);fill.color=frame.FillColor;fill.useColorTemperature=false;fill.intensity=frame.FillIntensity;fill.shadows=LightShadows.None;fill.enabled=frame.FillIntensity>0;
   }
   // Trilight直接消费三色，不以ambientIntensity缩放；先在线性光强中乘倍率，再转回Native作者色。
   RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
   RenderSettings.ambientSkyColor=(frame.AmbientSky.linear*frame.AmbientIntensity).gamma;
   RenderSettings.ambientEquatorColor=(frame.AmbientEquator.linear*frame.AmbientIntensity).gamma;
   RenderSettings.ambientGroundColor=(frame.AmbientGround.linear*frame.AmbientIntensity).gamma;
   RenderSettings.ambientIntensity=frame.AmbientIntensity;
  }
  public void Release(EnvironmentRoot requestedOwner)
  {
   if(owner==null||owner!=requestedOwner)return;
   if(hasSun)sunBefore.Restore();if(hasFill)fillBefore.Restore();hasSun=hasFill=false;
   if(globalOwner==this)
   {
    globalOwner=null;
    if(SceneManager.GetActiveScene().handle==ambientBefore.sceneHandle){ambientBefore.Restore();pendingAmbient=false;}else pendingAmbient=true;
   }
   owner=null;
  }
 }
}
