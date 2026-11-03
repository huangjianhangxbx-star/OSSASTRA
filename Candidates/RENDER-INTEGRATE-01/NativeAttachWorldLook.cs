// 功能：把只读旧Look配置和新相机环境Feature增量接入当前认证主场，保留3D管线/原业务。
using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using Funplay.Editor.Tools.Scripting;using Ossastra.WorldRendering;using Ossastra.WorldRendering.Rendering;
public class NativeAttachWorldLook:IFunplayCommand{
public void Execute(ExecutionContext ctx){
const string lab="Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity";
if(Application.isPlaying||Application.dataPath!="E:/WORLDCREATOR/new/Project_ANKALUTE/Assets"||SceneManager.GetActiveScene().path!=lab)throw new Exception("Guard");
var camera=EditorUtility.InstanceIDToObject(67630) as Camera;var root=EditorUtility.InstanceIDToObject(69704) as Ossastra.SceneVisibility.SceneVisibilityRoot;var rooms=EditorUtility.InstanceIDToObject(69710) as Ossastra.SceneVisibility.Rooms.RoomVisibilityModule;
var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/Rendering/RoomVisibilityRenderer.asset");
var outdoor=AssetDatabase.LoadAssetAtPath<XingHai.T7C2.HD2DPostProfile>("Assets/XingHaiHuiLang/RenderLab/T7C3/Profiles/Amber_Light.asset");
var indoor=AssetDatabase.LoadAssetAtPath<XingHai.T7C2.HD2DPostProfile>("Assets/XingHaiHuiLang/RenderLab/T7C3/Profiles/Dungeon_Light.asset");
var atmosphere=AssetDatabase.LoadAssetAtPath<XingHai.T7B.OutdoorAtmosphereProfile>("Assets/XingHaiHuiLang/RenderLab/T7B/Profiles/Outdoor_BestVisual.asset");
var palette=AssetDatabase.LoadAssetAtPath<XingHai.T7C1.RegionalLookProfile>("Assets/XingHaiHuiLang/RenderLab/T7C3/Profiles/Green_Revision_A.asset");
var look=Shader.Find("Hidden/Ossastra/WorldRendering/EnvironmentLook");var copy=Shader.Find("Hidden/Ossastra/WorldRendering/LegalDepthCopy");
if(camera==null||root==null||rooms==null||renderer==null||outdoor==null||indoor==null||atmosphere==null||palette==null||look==null||copy==null)throw new Exception("Missing explicit assets");
if(root.gameplayCamera!=camera||root.roomModule!=rooms||camera.GetUniversalAdditionalCameraData().renderPostProcessing)throw new Exception("Existing route");
string backup="E:/WORLDCREATOR/XingHaiHuiLang/研究/渲染接入/RENDER-INTEGRATE-01";
foreach(var p in new[]{"ProjectSettings/EditorBuildSettings.asset",AssetDatabase.GetAssetPath(renderer)}){
string dst=Path.Combine(backup,Path.GetFileName(p)+".before.bin");if(!File.Exists(dst))File.Copy(p,dst);
}
ctx.RegisterObjectModification(renderer);
var feature=renderer.rendererFeatures.OfType<WorldLookRendererFeature>().SingleOrDefault();
if(feature==null){feature=ScriptableObject.CreateInstance<WorldLookRendererFeature>();feature.name="World Environment Look";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
feature.environmentLookShader=look;feature.legalDepthCopyShader=copy;feature.SetActive(true);EditorUtility.SetDirty(feature);renderer.SetDirty();EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
var controller=camera.GetComponent<WorldLookController>();if(controller==null){controller=camera.gameObject.AddComponent<WorldLookController>();ctx.RegisterObjectCreation(controller);}else ctx.RegisterObjectModification(controller);
controller.gameplayCamera=camera;controller.visibilityRoot=root;controller.rooms=rooms;controller.outdoorPresentation=outdoor;controller.interiorPresentation=indoor;controller.outdoorAtmosphere=atmosphere;controller.outdoorPalette=palette;controller.interiorAtmosphere=null;controller.interiorPalette=null;controller.effects=new WorldLookEffectOptions();
var build=EditorBuildSettings.scenes;
if(build.Any(s=>s.path=="Assets/Scenes/SampleScene2.unity")&&!File.Exists("Assets/Scenes/SampleScene2.unity"))
EditorBuildSettings.scenes=build.Select(s=>s.path=="Assets/Scenes/SampleScene2.unity"?new EditorBuildSettingsScene(lab,true):s).ToArray();
EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);bool saved=EditorSceneManager.SaveScene(camera.gameObject.scene);
ctx.ReturnValue=new{saved,controllerId=controller.GetInstanceID(),featureId=feature.GetInstanceID(),renderer=AssetDatabase.GetAssetPath(renderer),features=renderer.rendererFeatures.Select(f=>f.GetType().Name).ToArray(),outdoor=AssetDatabase.GetAssetPath(outdoor),indoor=AssetDatabase.GetAssetPath(indoor),atmosphere=AssetDatabase.GetAssetPath(atmosphere),palette=AssetDatabase.GetAssetPath(palette),pipeline=AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline),post=camera.GetUniversalAdditionalCameraData().renderPostProcessing,build=EditorBuildSettings.scenes.Select(s=>s.path).ToArray()};
}}
