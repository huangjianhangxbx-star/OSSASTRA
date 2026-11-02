/* 脚本：RoomVisibilityLabPanel
 * 功能：在明确接线的房间样板中操作黑幕诊断、实体遮板与原镜头/角色定位。
 * 边界：仅开发显示入口；不定义第二Current/按键，不暂停世界，不写经济/探索存档。
 * 协作：复用RoomVisibilityModule、MapResearchRoot与原CharacterMovement；定位按钮明确为调试夹具。
 * 生命周期：指针位于面板时只捕获自己的玩家/相机输入；禁用后按owner归还，不影响其他窗口。
 */
using UnityEngine;
using Ossastra.MapResearch;
using Ossastra.SceneVisibility;
using Ossastra.SceneVisibility.Rooms;
using Ossastra.SceneVisibility.Rooms.Rendering;
namespace Ossastra.Development.SceneVisibility
{
 [DisallowMultipleComponent]
 public sealed class RoomVisibilityLabPanel : MonoBehaviour
 {
  [Header("仅明确样板接线")]
  public SceneVisibilityRoot visibility;
  public MapResearchRoot map;
  public ViewPortal shutterPortal;
  public Transform shutter;
  public Vector3 shutterClosedPosition, shutterOpenPosition;
  public ViewPortal doorPortal;
  public Transform doorLeaf;
  public Vector3 doorClosedPosition, doorOpenPosition;
  [Header("调试定位点；不替代正常走门体验")]
  public Vector3 exteriorPosition, knownRoomPosition, unknownRoomPosition;
  public bool showPanel=true;
  public bool showPortalProjection=true;
  bool captured;
  Vector2 scroll;
  Rect Area=>new Rect(Mathf.Max(5,Screen.width-400),145,390,440);
  void Capture(bool value)
  {
   if(captured==value)return;captured=value;
   if(visibility!=null&&visibility.coordinator!=null)visibility.coordinator.SetInputCapture(this,value);
   if(map!=null)map.SetUiCameraCapture(this,value);
  }
  public void SetShutterOpen(bool value)
  {
   if(shutterPortal==null||shutter==null)return;
   shutter.position=value?shutterOpenPosition:shutterClosedPosition;shutterPortal.open=value;
  }
  public void SetDoorOpen(bool value)
  {
   if(doorPortal==null||doorLeaf==null)return;
   doorLeaf.position=value?doorOpenPosition:doorClosedPosition;doorPortal.open=value;
  }
  public bool PlaceCurrent(Vector3 position)
  {
   var actor=visibility==null||visibility.coordinator==null?null:visibility.coordinator.Current;
   if(actor==null||!actor.IsAlive)return false;
   var motor=actor.GetComponent<Ossastra.Characters.CharacterMovement>();if(motor==null)return false;
   motor.Teleport(position);visibility.roomModule?.RequestImmediateMembershipRefresh();return true;
  }
  void OnGUI()
  {
   if(!Application.isPlaying||!showPanel||visibility==null||visibility.roomModule==null){Capture(false);return;}
   Capture(Area.Contains(Event.current.mousePosition));var module=visibility.roomModule;
   if(showPortalProjection&&Event.current.type==EventType.Repaint)
   {
    foreach(var volume in module.ObserverSightVolumes)
    {
     var aperture=volume.Aperture;var origin=volume.Observer.Position;var normal=volume.TravelNormal;
     for(int i=0;i<aperture.Count;i++)
     {
      var a=aperture[i];var b=aperture[(i+1)%aperture.Count];DrawWorldLine(new Vector3(a.X,a.Y,a.Z),new Vector3(b.X,b.Y,b.Z),Color.cyan);
      var delta=a-origin;float along=Ossastra.SceneVisibility.Rooms.Core.SightVector3.Dot(delta,normal);
      if(along>.0001f){float drawnDistance=module.naturalPlayerVision?20:volume.MaxSightDistance;var far=origin+delta*((along+drawnDistance)/along);DrawWorldLine(new Vector3(a.X,a.Y,a.Z),new Vector3(far.X,far.Y,far.Z),new Color(1,.6f,0,.6f));}
     }
    }
   }
   GUILayout.BeginArea(Area,GUI.skin.box);scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("建筑可见性样板 · 开发诊断");
   GUILayout.Label(module.Status);
   GUILayout.Label(module.SightStatus);
   module.positiveRenderingEnabled=GUILayout.Toggle(module.positiveRenderingEnabled,"启用合法视域合成（关闭用于原图对照）");
   module.truthMode=GUILayout.Toggle(module.truthMode,"纯色真值：整房 / 单跳片段 / 顶盖");
   showPortalProjection=GUILayout.Toggle(showPortalProjection,"开口与方向示意（橙线不代表视距）");
   module.localObjectRevealEnabled=GUILayout.Toggle(module.localObjectRevealEnabled,"局部物件透视接口（默认关闭）");
   module.naturalPlayerVision=GUILayout.Toggle(module.naturalPlayerVision,"自然视野180°（不限制旧6/8米）");
   module.realWindowScenery=GUILayout.Toggle(module.realWindowScenery,"真实窗孔外景（不扩大人物许可）");
   if(module.fog!=null){module.fog.Enabled=GUILayout.Toggle(module.fog.Enabled,"静态环境快速展开与内向柔边");module.fog.DitherEnabled=GUILayout.Toggle(module.fog.DitherEnabled,"稳定世界点阵边缘");}
   GUILayout.Label("过渡参数：RoomVisibilityModule → 黑幕呈现；静态名单 "+module.StaticEnvironmentCount);
   foreach(var building in module.buildings)if(building!=null)foreach(var portal in building.portals)if(portal!=null)
    GUILayout.Label(portal.portalId+"  "+(portal.inside==null?"室外":portal.inside.roomId)+" ↔ "+(portal.outside==null?"室外":portal.outside.roomId)+"  "+(portal.open&&portal.canSeeThrough?"通视":"封闭"));
   GUILayout.BeginHorizontal();if(GUILayout.Button("左转45°"))map?.RequestRotationStep(-1);if(GUILayout.Button("右转45°"))map?.RequestRotationStep(1);GUILayout.EndHorizontal();
   if(shutterPortal!=null&&GUILayout.Button(shutterPortal.open?"关闭西窗实体遮板":"打开西窗实体遮板"))SetShutterOpen(!shutterPortal.open);
   if(doorPortal!=null&&GUILayout.Button(doorPortal.open?"关闭 A—走廊实体门":"打开 A—走廊实体门"))SetDoorOpen(!doorPortal.open);
   if(GUILayout.Button("清除到达历史（不授予显示权）"))module.ResetSessionDiscovery();
   GUILayout.Label("以下为调试定位；正常体验请走门：");GUILayout.BeginHorizontal();
   if(GUILayout.Button("室外"))PlaceCurrent(exteriorPosition);if(GUILayout.Button("A室"))PlaceCurrent(knownRoomPosition);if(GUILayout.Button("B室"))PlaceCurrent(unknownRoomPosition);GUILayout.EndHorizontal();
   if(RoomMaskCameraRegistry.TryGetStatistics(visibility.gameplayCamera,out var stats)){GUILayout.Label("mask绘制 "+stats.SurfaceDraws+" / 门窗 "+stats.PortalDraws+" / RT "+stats.OwnedTextureCount);if(!string.IsNullOrEmpty(stats.Error))GUILayout.Label(stats.Error);}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void OnDisable()=>Capture(false);
  void OnDestroy()=>Capture(false);
  void DrawWorldLine(Vector3 from,Vector3 to,Color color)
  {
   var camera=visibility.gameplayCamera;if(camera==null)return;var a=camera.WorldToScreenPoint(from);var b=camera.WorldToScreenPoint(to);if(a.z<=0||b.z<=0)return;
   Vector2 start=new Vector2(a.x,Screen.height-a.y),end=new Vector2(b.x,Screen.height-b.y);var delta=end-start;if(delta.sqrMagnitude<1)return;
   var old=GUI.matrix;var oldColor=GUI.color;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,start);GUI.DrawTexture(new Rect(start.x,start.y,delta.magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=old;GUI.color=oldColor;
  }
 }
}
