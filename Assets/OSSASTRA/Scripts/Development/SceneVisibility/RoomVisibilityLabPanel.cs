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
  [Header("调试定位点；不替代正常走门体验")]
  public Vector3 exteriorPosition, knownRoomPosition, unknownRoomPosition;
  public bool showPanel=true;
  bool captured;
  Rect Area=>new Rect(Mathf.Max(5,Screen.width-370),145,360,330);
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
   GUILayout.BeginArea(Area,GUI.skin.box);GUILayout.Label("建筑可见性样板 · 开发诊断");
   GUILayout.Label(module.Status);
   module.exteriorBlackoutEnabled=GUILayout.Toggle(module.exteriorBlackoutEnabled,"室外隔离黑幕");
   module.interiorUnexploredEnabled=GUILayout.Toggle(module.interiorUnexploredEnabled,"室内探索黑幕（本次运行记忆）");
   module.maskDebugMode=GUILayout.Toolbar(module.maskDebugMode,new[]{"正常","室外R","室内G","最终许可"});
   GUILayout.BeginHorizontal();if(GUILayout.Button("左转45°"))map?.RequestRotationStep(-1);if(GUILayout.Button("右转45°"))map?.RequestRotationStep(1);GUILayout.EndHorizontal();
   if(shutterPortal!=null&&GUILayout.Button(shutterPortal.open?"关闭西窗实体遮板":"打开西窗实体遮板"))SetShutterOpen(!shutterPortal.open);
   if(GUILayout.Button("重置本次运行的到达记录"))module.ResetSessionDiscovery();
   GUILayout.Label("以下为调试定位；正常体验请走门：");GUILayout.BeginHorizontal();
   if(GUILayout.Button("室外"))PlaceCurrent(exteriorPosition);if(GUILayout.Button("A室"))PlaceCurrent(knownRoomPosition);if(GUILayout.Button("B室"))PlaceCurrent(unknownRoomPosition);GUILayout.EndHorizontal();
   if(RoomMaskCameraRegistry.TryGetStatistics(visibility.gameplayCamera,out var stats)){GUILayout.Label("mask绘制 "+stats.SurfaceDraws+" / 门窗 "+stats.PortalDraws+" / RT "+stats.OwnedTextureCount);if(!string.IsNullOrEmpty(stats.Error))GUILayout.Label(stats.Error);}
   GUILayout.EndArea();
  }
  void OnDisable()=>Capture(false);
  void OnDestroy()=>Capture(false);
 }
}
