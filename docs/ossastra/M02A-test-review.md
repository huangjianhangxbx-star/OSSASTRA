# M02-A 测试命令原文与审查
这些.cs.txt是Funplay MCP execute_code命令，不是Unity Test Runner注册测试。全为候选待用户审查；失败原件保留。

## Candidates/M02A/Tests/world-contract.cs.txt

双角色Play场：隔离虚拟键鼠，保存Action覆盖。83定时步骤检验26个移动/切换/Spine/物理HP/生命周期事实。Current/Bus/ActionId读原实体；不改正式场景；Cleanup解除更新委托、恢复覆盖、移除设备。

```text
001 | // M02-A Play合同：虚拟设备驱动实际InputAction、两套Root/Spine/物理命中；不代替人工键鼠手感。
002 | using System;
003 | using System.Linq;
004 | using System.Collections.Generic;
005 | using UnityEngine;
006 | using UnityEditor;
007 | using UnityEngine.InputSystem;
008 | using UnityEngine.InputSystem.LowLevel;
009 | using Ossastra.Characters;
010 | using Ossastra.Development;
011 | using Funplay.Editor.Tools.Scripting;
012 | public class DualWorldContract : IFunplayCommand
013 | {
014 |  public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx)
015 |  {
016 |   var c=UnityEngine.Object.FindObjectOfType<CharacterControlCoordinator>();
017 |   if(!Application.isPlaying||c==null||c.Characters.Count!=2)throw new Exception("Dual Play scene required");
018 |   var a=c.Characters[0];var b=c.Characters[1];var actors=new[]{a,b};
019 |   var inputs=actors.Select(x=>x.GetComponent<CharacterInputHandler>()).ToArray();
020 |   var moves=actors.Select(x=>x.GetComponent<CharacterMovement>()).ToArray();
021 |   var views=actors.Select(x=>x.GetComponentInChildren<Spine.Unity.SkeletonAnimation>()).ToArray();
022 |   var targets=UnityEngine.Object.FindObjectsOfType<CombatTestTarget>().OrderBy(x=>x.transform.position.z).ToArray();
023 |   if(targets.Length!=2)throw new Exception("Two targets required");
024 |   var keyboard=InputSystem.AddDevice<Keyboard>("M02WorldKeyboard");
025 |   var mouse=InputSystem.AddDevice<Mouse>("M02WorldMouse");
026 |   var originalOverrides=new Dictionary<InputAction,string>();
027 |   foreach(var action in inputs.SelectMany(x=>new[]{x.movementAction,x.mouseAction,x.attackAction,x.pauseAction,x.cancelAction}).Concat(new[]{c.switchAction}))
028 |   {
029 |    originalOverrides[action]=action.SaveBindingOverridesAsJson();
030 |    for(int i=0;i<action.bindings.Count;i++){var binding=action.bindings[i];if(binding.isComposite)continue;var p=binding.path;if(p==null)continue;
031 |     if(p.Contains("<Keyboard>"))action.ApplyBindingOverride(i,p.Replace("<Keyboard>",keyboard.path));
032 |     if(p.Contains("<Mouse>"))action.ApplyBindingOverride(i,p.Replace("<Mouse>",mouse.path));
033 |    }
034 |   }
035 |   var gameType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(gameType!=null)EditorWindow.GetWindow(gameType).Focus();
036 |   foreach(var actor in actors)actor.SendMessage("OnApplicationFocus",true);c.SendMessage("OnApplicationFocus",true);
037 |   var busA=a.DataBus;var busB=b.DataBus;var owner=new object();
038 |   var records=new List<object>();var steps=new List<(double at,Action work)>();int next=0,failed=0;
039 |   var started=EditorApplication.timeSinceStartup;
040 |   void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
041 |   void Pointer(int target,bool down=false){var s=Camera.main.WorldToScreenPoint(targets[target].transform.position);InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(s.x,s.y),buttons=(ushort)(down?1:0)});}
042 |   void Flush(string state){SessionState.SetString("OSSASTRA_M02_WORLD",Newtonsoft.Json.JsonConvert.SerializeObject(new{state,steps=next,failed,records},Newtonsoft.Json.Formatting.Indented));}
043 |   void Check(string label,bool pass){if(!pass)failed++;records.Add(new{label,pass,current=c.Current==null?"none":c.Current.ActorId,aPosition=a.transform.position.ToString("F3"),bPosition=b.transform.position.ToString("F3"),aInput=inputs[0].CurrentFrame.move.ToString(),bInput=inputs[1].CurrentFrame.move.ToString(),aHp=targets[0].Health.Current,bHp=targets[1].Health.Current,aFact=a.LastFact,bFact=b.LastFact,aClip=a.GetComponentInChildren<CharacterSpineView>().CurrentClip,bClip=b.GetComponentInChildren<CharacterSpineView>().CurrentClip});Flush("running");}
044 |   void Reset(){foreach(var actor in actors){actor.CancelAction("dual-contract-reset");actor.SetPaused(false);actor.SetAttackRestriction(owner,false);}if(!c.enabled)c.enabled=true;if(c.Current!=a)c.SwitchNext();moves[0].Teleport(new Vector3(-2,.03f,-1.7f));moves[1].Teleport(new Vector3(-2,.03f,1.7f));targets[0].transform.position=new Vector3(-.85f,0,-1.7f);targets[1].transform.position=new Vector3(-.85f,0,1.7f);foreach(var t in targets)t.RestoreHealth();Keys();Pointer(0);Physics.SyncTransforms();}
045 |   Reset();
046 |   steps.Add((.2,()=>Keys(Key.D)));
047 |   steps.Add((.55,()=>Check("A-only-real-movement",a.transform.position.x> -1.7f&&Mathf.Abs(b.transform.position.x+2)<.01f&&inputs[1].CurrentFrame.move==Vector2.zero)));
048 |   float aAfterSwitch=0;
049 |   steps.Add((.65,()=>{aAfterSwitch=a.transform.position.x;Keys(Key.D,Key.Tab);}));
050 |   steps.Add((.8,()=>Keys(Key.D)));
051 |   steps.Add((1.1,()=>Check("Inspector-switch-B-moves-A-stops",c.Current==b&&b.transform.position.x> -1.5f&&Mathf.Abs(a.transform.position.x-aAfterSwitch)<.09f)));
052 |   steps.Add((1.2,()=>{Keys();moves[0].Teleport(new Vector3(-2,.03f,-1.7f));moves[1].Teleport(new Vector3(-2,.03f,1.7f));Pointer(1);}));
053 |   steps.Add((1.45,()=>Pointer(1,true)));
054 |   steps.Add((1.9,()=>Check("B-actual-release-physics-HP-only-B-target",targets[0].Health.Current==100&&targets[1].Health.Current==88&&b.ActionActive&&!a.ActionActive)));
055 |   steps.Add((2.0,()=>{Pointer(0);Keys(Key.Tab);}));steps.Add((2.15,()=>Keys()));
056 |   steps.Add((2.4,()=>Pointer(0,true)));
057 |   steps.Add((2.85,()=>Check("switch-back-A-actual-hit-independent",c.Current==a&&targets[0].Health.Current==88&&targets[1].Health.Current==88&&a.DataBus.ActionId!=b.DataBus.ActionId)));
058 |   steps.Add((3.0,Reset));steps.Add((3.2,()=>Pointer(0,true)));
059 |   Guid windupId=Guid.Empty;
060 |   steps.Add((3.3,()=>{windupId=a.DataBus.ActionId;Keys(Key.Tab);}));steps.Add((3.45,()=>Keys()));
061 |   steps.Add((3.8,()=>Check("windup-switch-old-action-releases-once-new-held-suppressed",c.Current==b&&windupId!=Guid.Empty&&a.DataBus.ActionId==windupId&&targets[0].Health.Current==88&&targets[1].Health.Current==100&&!b.ActionActive)));
062 |   steps.Add((4.1,()=>{Keys(Key.Tab);Pointer(0,true);}));steps.Add((4.25,()=>Keys()));
063 |   steps.Add((4.65,()=>Check("switch-after-release-no-rehit-or-held-ghost",targets[0].Health.Current==88&&targets[1].Health.Current==100&&!a.ActionActive&&!b.ActionActive)));
064 |   steps.Add((4.8,()=>Pointer(0)));steps.Add((5.0,()=>Pointer(0,true)));
065 |   steps.Add((5.45,()=>Check("release-then-fresh-press-rearms",targets[0].Health.Current==76)));
066 |   steps.Add((5.6,Reset));
067 |   for(int i=0;i<10;i++){int n=i;double t=5.8+i*.24;steps.Add((t,()=>Keys(Key.Tab)));steps.Add((t+.1,()=>Keys()));steps.Add((t+.19,()=>Check("repeat-switch-"+(n+1),c.Current==((n%2==0)?b:a)&&ReferenceEquals(a.DataBus,busA)&&ReferenceEquals(b.DataBus,busB)&&a.ActorId=="temporary-a"&&b.ActorId=="temporary-b"&&!ReferenceEquals(a.DataBus,b.DataBus))));}
068 |   steps.Add((8.3,Reset));steps.Add((8.5,()=>{Keys(Key.A);Pointer(0);}));
069 |   steps.Add((8.85,()=>Check("R17-left-display-right-attack-direction",views[0].Skeleton.ScaleX<0&&a.DataBus.AttackDirection.x>.5f&&Mathf.Abs(b.transform.position.x+2)<.01f)));
070 |   steps.Add((9.0,()=>Keys(Key.W)));steps.Add((9.25,()=>Check("R17-WS-retains-visible-horizontal-facing",views[0].Skeleton.ScaleX<0)));
071 |   steps.Add((9.4,Reset));steps.Add((9.6,()=>{Pointer(0,true);c.enabled=false;}));
072 |   steps.Add((9.9,()=>Check("coordinator-disable-fails-closed",!inputs[0].HasPlayerInput&&!inputs[1].HasPlayerInput&&!a.ActionActive&&!b.ActionActive)));
073 |   steps.Add((10.0,()=>c.enabled=true));steps.Add((10.4,()=>Check("coordinator-enable-held-no-ghost",c.Current==a&&!a.ActionActive&&targets[0].Health.Current==100)));
074 |   steps.Add((10.5,()=>Pointer(0)));steps.Add((10.7,()=>Pointer(0,true)));
075 |   steps.Add((11.1,()=>Check("enable-fresh-press-works",targets[0].Health.Current==88)));
076 |   steps.Add((11.25,Reset));steps.Add((11.45,()=>{a.SetPaused(true);Keys(Key.Tab);}));steps.Add((11.6,()=>Keys(Key.D)));
077 |   steps.Add((11.9,()=>Check("per-actor-pause-does-not-contaminate-B",c.Current==b&&a.IsPaused&&!b.IsPaused&&b.transform.position.x> -1.5f&&Mathf.Abs(a.transform.position.x+2)<.01f)));
078 |   steps.Add((12.05,Reset));steps.Add((12.2,()=>{foreach(var actor in actors)actor.SendMessage("OnApplicationFocus",false);c.SendMessage("OnApplicationFocus",false);Keys(Key.Tab);Pointer(0,true);}));
079 |   steps.Add((12.45,()=>{foreach(var actor in actors)actor.SendMessage("OnApplicationFocus",true);c.SendMessage("OnApplicationFocus",true);}));
080 |   steps.Add((12.8,()=>Check("focus-return-held-does-not-switch-or-attack",c.Current==a&&!a.ActionActive&&!b.ActionActive&&targets[0].Health.Current==100)));
081 |   steps.Add((12.95,Reset));steps.Add((13.15,()=>{a.SetAttackRestriction(owner,true,"dual-contract-block");Pointer(0,true);}));
082 |   steps.Add((13.6,()=>Check("restriction-remains-per-actor",!a.DataBus.AllowsAttack&&b.DataBus.AllowsAttack&&!a.ActionActive&&targets[0].Health.Current==100)));
083 |   steps.Add((13.75,Reset));steps.Add((13.95,()=>{a.enabled=false;Keys();}));steps.Add((14.2,()=>Keys(Key.Tab)));steps.Add((14.35,()=>Keys()));
084 |   steps.Add((14.65,()=>Check("disabled-current-safe-switch-to-B",c.Current==b&&!inputs[0].HasPlayerInput&&inputs[1].HasPlayerInput)));
085 |   EditorApplication.CallbackFunction tick=null;
086 |   void Cleanup(){EditorApplication.update-=tick;a.enabled=true;foreach(var pair in originalOverrides)pair.Key.LoadBindingOverridesFromJson(pair.Value);Reset();InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);}
087 |   tick=()=>{try{if(!Application.isPlaying||c==null)throw new Exception("Scene ended");while(next<steps.Count&&EditorApplication.timeSinceStartup-started>=steps[next].at)steps[next++].work();if(next==steps.Count){Cleanup();Flush("completed");}}catch(Exception e){failed++;records.Add(new{error=e.ToString()});Cleanup();Flush("failed");}};
088 |   EditorApplication.update+=tick;Flush("running");ctx.Log("Scheduled 14.65s dual InputAction/Spine/physics contract; do not block Unity main thread during run");
089 |  }
090 | }
```

行1–90：双角色Play场：隔离虚拟键鼠，保存Action覆盖。83定时步骤检验26个移动/切换/Spine/物理HP/生命周期事实。Current/Bus/ActionId读原实体；不改正式场景；Cleanup解除更新委托、恢复覆盖、移除设备。
用户结论：待审。

## Candidates/M02A/Tests/boundary-contract.cs.txt

inactive父对象中克隆临时Prefab，先设身份/成员/绑定再激活执行真实Awake；空/缺引用/重身份/重引用/越界/无绑定失败关闭，同帧输入屏障与provider销毁不回退双控。15断言，临时层级finally销毁。

```text
001 | // M02-A 边界合同：无效配置失败关闭、重复owner拒绝、移交同帧禁止采样、销毁后不恢复双控。
002 | using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using Ossastra.Characters;using Funplay.Editor.Tools.Scripting;
003 | public class DualBoundaryContract:IFunplayCommand
004 | {
005 |  public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx)
006 |  {
007 |   if(!Application.isPlaying)throw new Exception("Play required");
008 |   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab");if(prefab==null)throw new Exception("Prefab missing");
009 |   int passed=0;void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);passed++;ctx.Log("PASS "+label);}
010 |   void Scenario(string label,string kind)
011 |   {
012 |    var parent=new GameObject("M02BoundaryProbe");parent.SetActive(false);
013 |    try
014 |    {
015 |     var actors=new Ossastra.Characters.CharacterRoot[2];for(int i=0;i<2;i++){var go=UnityEngine.Object.Instantiate(prefab,parent.transform);go.transform.position=new Vector3(-4+i*6,.03f,-3);actors[i]=go.GetComponent<Ossastra.Characters.CharacterRoot>();var so=new SerializedObject(actors[i]);so.FindProperty("actorId").stringValue=kind=="duplicate-id"?"duplicate":"boundary-"+i;so.ApplyModifiedPropertiesWithoutUndo();}
016 |     var g=new GameObject("Coordinator");g.transform.SetParent(parent.transform);var c=g.AddComponent<CharacterControlCoordinator>();var config=new SerializedObject(c);var array=config.FindProperty("characters");array.arraySize=kind=="empty"?0:2;
017 |     if(array.arraySize>0){array.GetArrayElementAtIndex(0).objectReferenceValue=actors[0];array.GetArrayElementAtIndex(1).objectReferenceValue=kind=="null"?null:kind=="duplicate-ref"?actors[0]:actors[1];}
018 |     if(kind=="bad-index")config.FindProperty("initialIndex").intValue=8;config.ApplyModifiedPropertiesWithoutUndo();if(kind!="no-binding")c.switchAction.AddBinding("<Keyboard>/tab");
019 |     parent.SetActive(true);
020 |     Check(!c.enabled&&c.Current==null&&!c.SwitchNext(),label+" rejected without Current");
021 |     if(kind!="empty")Check(!actors[0].GetComponent<CharacterInputHandler>().HasPlayerInput,label+" known member input closed");
022 |    }finally{UnityEngine.Object.DestroyImmediate(parent);}
023 |   }
024 |   foreach(var kind in new[]{"empty","null","duplicate-id","duplicate-ref","bad-index","no-binding"})Scenario(kind,kind);
025 |   var existing=UnityEngine.Object.FindObjectOfType<CharacterControlCoordinator>();if(existing==null)throw new Exception("Scene coordinator missing");
026 |   var original=existing.Current;existing.SwitchNext();
027 |   Check(existing.Characters.All(a=>!a.GetComponent<CharacterInputHandler>().HasPlayerInput&&a.GetComponent<CharacterInputHandler>().Read().move==Vector2.zero),"same-frame transfer embargo prevents two input consumers");
028 |   existing.SwitchNext();
029 |   Check(existing.Current==original,"same-frame repeated switch retains entity identity");
030 |   var sourceGo=new GameObject("M02DestroyedSourceProbe");sourceGo.SetActive(false);var orphanInput=sourceGo.AddComponent<CharacterInputHandler>();var controllerGo=new GameObject("M02DestroyedCoordinatorProbe");controllerGo.SetActive(false);var dead=controllerGo.AddComponent<CharacterControlCoordinator>();
031 |   try{Check(orphanInput.BindPlayerControl(dead),"input accepts explicit first control source");UnityEngine.Object.DestroyImmediate(controllerGo);Check(!orphanInput.HasPlayerInput&&orphanInput.Read().move==Vector2.zero,"destroyed control provider remains fail-closed");}
032 |   finally{if(controllerGo!=null)UnityEngine.Object.DestroyImmediate(controllerGo);UnityEngine.Object.DestroyImmediate(sourceGo);}
033 |   ctx.Log("RESULT "+passed+"/15 passed");
034 |  }
035 | }
```

行1–35：inactive父对象中克隆临时Prefab，先设身份/成员/绑定再激活执行真实Awake；空/缺引用/重身份/重引用/越界/无绑定失败关闭，同帧输入屏障与provider销毁不回退双控。15断言，临时层级finally销毁。
用户结论：待审。

## Candidates/M02A/Tests/conflict-contract.cs.txt

虚拟D经实际InputAction.Read发布(1,0)，第二协调者拒绝抢占后不能清掉原快照；finally销毁冲突对象、恢复覆盖和设备。用于同一RED/GREEN语义。

```text
001 | // 所有权冲突探针：第二协调者不能清掉第一协调者已发布的真实移动快照。
002 | using System;using System.Linq;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using Ossastra.Characters;using Funplay.Editor.Tools.Scripting;
003 | public class ConflictRed:IFunplayCommand{public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx){var c=UnityEngine.Object.FindObjectOfType<CharacterControlCoordinator>();if(c==null||!Application.isPlaying)throw new Exception("Dual Play required");var a=c.Current;if(a==null)throw new Exception("Current missing");var input=a.GetComponent<CharacterInputHandler>();var keyboard=InputSystem.AddDevice<Keyboard>("M02ConflictKeyboard");var json=input.movementAction.SaveBindingOverridesAsJson();var go=new GameObject("ConflictProbe");go.SetActive(false);try{for(int i=0;i<input.movementAction.bindings.Count;i++){var bind=input.movementAction.bindings[i];if(bind.path!=null&&bind.path.Contains("<Keyboard>"))input.movementAction.ApplyBindingOverride(i,bind.path.Replace("<Keyboard>",keyboard.path));}InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));InputSystem.Update();var before=input.Read().move;var other=go.AddComponent<CharacterControlCoordinator>();var so=new SerializedObject(other);var arr=so.FindProperty("characters");arr.arraySize=2;arr.GetArrayElementAtIndex(0).objectReferenceValue=c.Characters[0];arr.GetArrayElementAtIndex(1).objectReferenceValue=c.Characters[1];so.ApplyModifiedPropertiesWithoutUndo();other.switchAction.AddBinding("<Keyboard>/tab");go.SetActive(true);ctx.Log(Newtonsoft.Json.JsonConvert.SerializeObject(new{before=before.ToString(),after=input.CurrentFrame.move.ToString(),rejected=!other.enabled,status=other.Status,pass=before.x>.9f&&input.CurrentFrame.move.x>.9f}));}finally{UnityEngine.Object.DestroyImmediate(go);input.movementAction.LoadBindingOverridesFromJson(json);InputSystem.RemoveDevice(keyboard);}}}
```

行1–3：虚拟D经实际InputAction.Read发布(1,0)，第二协调者拒绝抢占后不能清掉原快照；finally销毁冲突对象、恢复覆盖和设备。用于同一RED/GREEN语义。
用户结论：待审。

## Candidates/M02A/Tests/dual-red.cs.txt

历史RED：两份未管理共享Prefab同时读取D，比较0.65秒实际位移，预期只A移动失败；不作为最终通过验收。定时结束清父对象/设备。

```text
001 | // 双控RED：真实共享Prefab与虚拟键盘，观察两个运动组件是否消费同一D输入。
002 | using System;using System.Linq;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using Ossastra.Characters;using Funplay.Editor.Tools.Scripting;
003 | public class DualRed:IFunplayCommand{public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx){
004 | if(!Application.isPlaying)throw new Exception("Play required");var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OSSASTRA/Prefabs/Characters/PlayerCharacter.prefab");if(prefab==null)throw new Exception("Prefab missing");var parent=new GameObject("M02RedProbe");parent.SetActive(false);var a=UnityEngine.Object.Instantiate(prefab,parent.transform);var b=UnityEngine.Object.Instantiate(prefab,parent.transform);a.transform.position=new Vector3(-3,.03f,-3);b.transform.position=new Vector3(2,.03f,-3);var keyboard=InputSystem.AddDevice<Keyboard>("M02RedKeyboard");int id=0;foreach(var g in new[]{a,b}){var so=new SerializedObject(g.GetComponent<Ossastra.Characters.CharacterRoot>());so.FindProperty("actorId").stringValue="red-"+(id++);so.ApplyModifiedPropertiesWithoutUndo();var input=g.GetComponent<CharacterInputHandler>();input.movementAction.Dispose();input.movementAction=new InputAction("Move",InputActionType.Value,keyboard.path+"/d",expectedControlType:"Vector2");input.movementAction.Dispose();input.movementAction=new InputAction("Move",InputActionType.Value);input.movementAction.AddCompositeBinding("2DVector").With("Right",keyboard.path+"/d");g.GetComponent<Ossastra.Characters.CharacterRoot>().SendMessage("OnApplicationFocus",true);}
005 | parent.SetActive(true);var posA=a.transform.position;var posB=b.transform.position;var start=EditorApplication.timeSinceStartup;EditorApplication.CallbackFunction tick=null;bool queued=false;
006 | tick=()=>{try{var elapsed=EditorApplication.timeSinceStartup-start;if(elapsed>.15&&!queued){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));queued=true;}if(elapsed<.65)return;var da=a.transform.position.x-posA.x;var db=b.transform.position.x-posB.x;SessionState.SetString("OSSASTRA_M02_RED",Newtonsoft.Json.JsonConvert.SerializeObject(new{expected="only selected A moves",aDelta=da,bDelta=db,aInput=a.GetComponent<CharacterInputHandler>().CurrentFrame.move.ToString(),bInput=b.GetComponent<CharacterInputHandler>().CurrentFrame.move.ToString(),pass=da>.1f&&Mathf.Abs(db)<.01f}));}finally{if(EditorApplication.timeSinceStartup-start>=.65){EditorApplication.update-=tick;InputSystem.RemoveDevice(keyboard);UnityEngine.Object.DestroyImmediate(parent);}}};EditorApplication.update+=tick;ctx.Log("RED scheduled .65s");}}
```

行1–6：历史RED：两份未管理共享Prefab同时读取D，比较0.65秒实际位移，预期只A移动失败；不作为最终通过验收。定时结束清父对象/设备。
用户结论：待审。

## Candidates/M01/Tests/InputBehaviorContract.cs.txt

Edit预览场与临时InputSettings，虚拟设备检验真实绑定/隔离/启停。重新启用后先实际Read采样松键，再按N验证恢复；finally清临时组件、设备、预览场并恢复原设置。

```text
001 | // 输入合同：虚拟设备与预览场；重新启用后必须真实采样一次释放，再形成新攻击。
002 | using System;
003 | using System.Linq;
004 | using UnityEngine;
005 | using UnityEngine.InputSystem;
006 | using UnityEngine.InputSystem.LowLevel;
007 | using UnityEditor.SceneManagement;
008 | using UnityEngine.SceneManagement;
009 | using Funplay.Editor.Tools.Scripting;
010 | public class InputBehaviorContract : IFunplayCommand {
011 |  public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx) {
012 |  var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Ossastra.Characters.CharacterInputHandler")).FirstOrDefault(t=>t!=null);
013 |  if(type==null){ctx.LogError("FAIL input remapping: configurable per-character input component is absent");return;}
014 |  var originalSettings=InputSystem.settings;var testSettings=UnityEngine.Object.Instantiate(originalSettings);testSettings.hideFlags=HideFlags.HideAndDontSave;InputSystem.settings=testSettings;testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE",true);testSettings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;var scene=EditorSceneManager.NewPreviewScene();Keyboard keyboard=null;Gamepad gamepad=null;Mouse mouse=null;GameObject a=null,b=null;int passed=0;
015 |  Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("FAIL "+label);passed++;ctx.Log("PASS "+label);};
016 |  Func<Component,string,object> read=(c,name)=>{var f=type.GetMethod("Read").Invoke(c,null);return f.GetType().GetField(name).GetValue(f);};
017 |  Action<Component,string,InputAction> set=(c,name,action)=>type.GetField(name).SetValue(c,action);
018 |  Func<string,string,InputAction> button=(name,path)=>new InputAction(name,InputActionType.Button,path);
019 |  try {
020 |  keyboard=InputSystem.AddDevice<Keyboard>();gamepad=InputSystem.AddDevice<Gamepad>();mouse=InputSystem.AddDevice<Mouse>();
021 |  a=new GameObject("InputContractA");a.SetActive(false);SceneManager.MoveGameObjectToScene(a,scene);var ca=a.AddComponent(type);
022 |  b=new GameObject("InputContractB");b.SetActive(false);SceneManager.MoveGameObjectToScene(b,scene);var cb=b.AddComponent(type);
023 |  foreach(var component in new[]{ca,cb}){
024 |  var move=new InputAction("move",InputActionType.Value,expectedControlType:"Vector2");move.AddCompositeBinding("2DVector").With("Up",component==ca?"<Keyboard>/j":"<Keyboard>/i");move.AddBinding("<Gamepad>/leftStick");
025 |  set(component,"movementAction",move);set(component,"mouseAction",new InputAction("pointer",InputActionType.Value,"<Mouse>/position",expectedControlType:"Vector2"));set(component,"gamepadAimAction",new InputAction("Aim",InputActionType.Value,"<Gamepad>/rightStick",expectedControlType:"Vector2"));
026 |  set(component,"attackAction",button("Attack",component==ca?"<Keyboard>/n":"<Keyboard>/m"));set(component,"cancelAction",button("cancel","<Keyboard>/x"));set(component,"pauseAction",button("pause","<Keyboard>/p"));
027 |  }
028 |  a.SetActive(true);b.SetActive(true);a.SendMessage("OnEnable",SendMessageOptions.DontRequireReceiver);b.SendMessage("OnEnable",SendMessageOptions.DontRequireReceiver);
029 |  InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J,Key.N));InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(320,240)});InputSystem.Update();
030 |  check(((Vector2)read(ca,"move")).y>.9f,"serialized remapping J moves without WASD");check((bool)read(ca,"attackHeld"),"serialized N attack binding is honored");check(((Vector2)read(cb,"move")).sqrMagnitude<.001f && !(bool)read(cb,"attackHeld"),"two character binding sets are independent");check(Vector2.Distance((Vector2)read(ca,"pointer"),new Vector2(320,240))<.1f,"pointer action returns actual mouse coordinates");
031 |  InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();check(!(bool)read(ca,"attackHeld"),"release clears held attack");
032 |  var gamepadState=new GamepadState{leftStick=new Vector2(.6f,.8f),rightStick=new Vector2(.5f,.8f)};InputSystem.QueueStateEvent(gamepad,gamepadState);InputSystem.Update();check(((Vector2)read(ca,"move")).sqrMagnitude>.5f,"same movement action accepts gamepad stick");check((bool)read(ca,"hasDirectionalAim"),"gamepad aim stays directional rather than pixel coordinates");
033 |  a.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N));InputSystem.Update();check(!(bool)read(ca,"attackHeld"),"disabled input component cannot emit held attack");check(((InputAction)type.GetField("attackAction").GetValue(cb)).enabled,"disabling one input does not disable another character");
034 |  a.SendMessage("OnEnable",SendMessageOptions.DontRequireReceiver);InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();read(ca,"attackHeld");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.N));InputSystem.Update();check((bool)read(ca,"attackHeld"),"enable restores configured bindings");
035 |  ctx.Log("RESULT "+passed+"/10 passed; isolated devices and preview scene cleaned in finally");
036 |  } finally {
037 |  if(a!=null){a.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);a.SendMessage("OnDestroy",SendMessageOptions.DontRequireReceiver);UnityEngine.Object.DestroyImmediate(a);}if(b!=null){b.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);b.SendMessage("OnDestroy",SendMessageOptions.DontRequireReceiver);UnityEngine.Object.DestroyImmediate(b);}
038 |  if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(gamepad!=null)InputSystem.RemoveDevice(gamepad);if(mouse!=null)InputSystem.RemoveDevice(mouse);EditorSceneManager.ClosePreviewScene(scene);InputSystem.settings=originalSettings;UnityEngine.Object.DestroyImmediate(testSettings);
039 |  }
040 |  }
041 | }
042 | 
043 | 
```

行1–43：Edit预览场与临时InputSettings，虚拟设备检验真实绑定/隔离/启停。重新启用后先实际Read采样松键，再按N验证恢复；finally清临时组件、设备、预览场并恢复原设置。
用户结论：待审。

## Candidates/M01/Tests/r17-world-contract.cs.txt

限定单角色Play场，以免取到未受控双角色。键鼠手柄绑定隔离，39定时步骤/15结论检测方向/Spine/物理左右靶HP；UI owner/粗帧释放/暂停边界均记录；Cleanup恢复覆盖、靶与设备。

```text
001 | // 单角色R17真实世界回归；严格限定M01单角色场，防止误取未受控双角色实例。
002 | using System;
003 | using System.Linq;
004 | using System.Collections.Generic;
005 | using UnityEngine;
006 | using UnityEditor;
007 | using UnityEngine.InputSystem;
008 | using UnityEngine.InputSystem.LowLevel;
009 | using Ossastra.Characters;
010 | using Ossastra.Development;
011 | using Ossastra.CharacterCore;
012 | using Funplay.Editor.Tools.Scripting;
013 | public class R17WorldContract : IFunplayCommand {
014 |  public void Execute(Funplay.Editor.Tools.Scripting.ExecutionContext ctx) {
015 |  var p=UnityEngine.Object.FindObjectOfType<Ossastra.Characters.CharacterRoot>();if(!Application.isPlaying||p==null||UnityEngine.Object.FindObjectsOfType<Ossastra.Characters.CharacterRoot>().Length!=1)throw new Exception("Own single-character Play scene required");var input=p.GetComponent<CharacterInputHandler>();var movement=p.GetComponent<CharacterMovement>();var hit=p.GetComponent<CharacterMeleeHit>();var skeleton=p.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();var targets=UnityEngine.Object.FindObjectsOfType<CombatTestTarget>();var right=targets.Single(t=>t.Health.Id=="target-a");var left=targets.Single(t=>t.Health.Id=="target-b");
016 |  var oldRight=right.transform.position;var oldLeft=left.transform.position;var keyboard=InputSystem.AddDevice<Keyboard>("R17WorldKeyboard");var mouse=InputSystem.AddDevice<Mouse>("R17WorldMouse");var pad=InputSystem.AddDevice<Gamepad>("R17WorldGamepad");var pointerJson=input.mouseAction.SaveBindingOverridesAsJson();var keyboardJson=input.movementAction.SaveBindingOverridesAsJson();var stickJson=input.gamepadAimAction.SaveBindingOverridesAsJson();input.mouseAction.ApplyBindingOverride(0,mouse.path+"/position");input.gamepadAimAction.ApplyBindingOverride(0,pad.path+"/rightStick");for(int i=0;i<input.movementAction.bindings.Count;i++){var b=input.movementAction.bindings[i];if(b.path!=null&&b.path.Contains("<Keyboard>"))input.movementAction.ApplyBindingOverride(i,b.path.Replace("<Keyboard>",keyboard.path));}
017 |  var gameType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(gameType==null)throw new Exception("GameView missing");EditorWindow.GetWindow(gameType).Focus();p.SendMessage("OnApplicationFocus",true);var owner=new object();var records=new List<object>();var steps=new List<(double at,Action work)>();var started=EditorApplication.timeSinceStartup;int next=0,failed=0;
018 |  void Keys(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
019 |  void Pointer(Vector3 world,bool down=false,bool invalid=false){var screen=Camera.main.WorldToScreenPoint(world);InputSystem.QueueStateEvent(mouse,new MouseState{position=invalid?new Vector2(-20,-20):new Vector2(screen.x,screen.y),buttons=(ushort)(down?1:0)});}
020 |  void Stick(Vector2 direction){InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=direction});}
021 |  void Reset(){p.CancelAction("world-contract-reset");p.SetPaused(false);movement.Teleport(new Vector3(0,.03f,0));right.transform.position=new Vector3(1.15f,0,0);left.transform.position=new Vector3(-1.15f,0,0);right.RestoreHealth();left.RestoreHealth();Keys();Stick(Vector2.zero);Pointer(right.transform.position);Physics.SyncTransforms();}
022 |  void Flush(string state){SessionState.SetString("OSSASTRA_R17_WORLD",Newtonsoft.Json.JsonConvert.SerializeObject(new{state,completedSteps=next,failed,records},Newtonsoft.Json.Formatting.Indented));}
023 |  void Check(string label,bool pass){if(!pass)failed++;records.Add(new{label,pass,rightHp=right.Health.Current,leftHp=left.Health.Current,visual=skeleton.Skeleton.ScaleX,attack=p.DataBus.AttackDirection.ToString("F3"),position=p.transform.position.ToString("F3"),last=p.LastFact});Flush("running");}
024 |  Reset();
025 |  steps.Add((.15,()=>{Keys(Key.A);Pointer(right.transform.position);}));steps.Add((.5,()=>Check("A-left-run-with-pointer-right",p.transform.position.x<-.3f&&skeleton.Skeleton.ScaleX<0&&p.DataBus.AttackDirection.x>.5f)));
026 |  steps.Add((.6,Reset));steps.Add((.85,()=>Pointer(right.transform.position,true)));steps.Add((1.25,()=>Check("right-pointer-damages-right-only-and-faces-right",right.Health.Current==88&&left.Health.Current==100&&skeleton.Skeleton.ScaleX>0)));
027 |  steps.Add((1.4,Reset));steps.Add((1.6,()=>{Keys(Key.D);Pointer(left.transform.position);}));steps.Add((1.95,()=>Check("D-right-run-with-pointer-left",p.transform.position.x>.3f&&skeleton.Skeleton.ScaleX>0&&p.DataBus.AttackDirection.x<-.5f)));
028 |  steps.Add((2.05,Reset));steps.Add((2.3,()=>Pointer(left.transform.position,true)));steps.Add((2.7,()=>Check("left-pointer-damages-left-only-and-faces-left",left.Health.Current==88&&right.Health.Current==100&&skeleton.Skeleton.ScaleX<0)));
029 |  steps.Add((2.85,Reset));steps.Add((3.1,()=>Pointer(right.transform.position,true)));steps.Add((3.25,()=>Pointer(left.transform.position,true)));steps.Add((3.65,()=>Check("windup-retarget-is-sampled-at-release",left.Health.Current==88&&right.Health.Current==100)));
030 |  steps.Add((3.8,Reset));steps.Add((4.0,()=>{Pointer(right.transform.position,false,true);Stick(Vector2.left);}));steps.Add((4.2,()=>Check("invalid-pointer-falls-back-to-gamepad",p.DataBus.AttackDirection.x<-.9f)));
031 |  steps.Add((4.3,()=>{Stick(Vector2.zero);Keys(Key.D);}));steps.Add((4.5,()=>Check("no-pointer-or-stick-falls-back-to-movement",p.DataBus.AttackDirection.x>.9f&&skeleton.Skeleton.ScaleX>0)));
032 |  steps.Add((4.6,()=>Keys()));steps.Add((4.8,()=>Check("idle-persists-legal-direction-and-facing",p.DataBus.AttackDirection.x>.9f&&skeleton.Skeleton.ScaleX>0)));
033 |  steps.Add((4.9,Reset));steps.Add((5.05,()=>{var point=Camera.main.WorldToScreenPoint(right.transform.position);input.RegisterPointerRegion(owner,new Rect(point.x-30,Screen.height-point.y-30,60,60));Pointer(right.transform.position,true);}));steps.Add((5.4,()=>Check("UI-pointer-does-not-start-attack",!p.ActionActive&&right.Health.Current==100&&left.Health.Current==100)));
034 |  steps.Add((5.5,()=>input.RemovePointerRegion(owner)));steps.Add((5.7,()=>Check("dragging-held-pointer-out-of-UI-does-not-create-ghost-press",!p.ActionActive&&right.Health.Current==100)));
035 |  steps.Add((5.8,()=>Pointer(right.transform.position)));steps.Add((6.0,()=>Pointer(right.transform.position,true)));steps.Add((6.4,()=>Check("fresh-press-after-UI-releases-still-attacks",right.Health.Current==88)));
036 |  steps.Add((6.55,Reset));steps.Add((6.8,()=>{var runtime=new BasicActionRuntime("coarse-frame-actor");runtime.Request(new BasicAttackDefinition(.3,.5,.8,1.1,12),"coarse",new ControlAuthority());var releases=runtime.Advance(3,false).Where(f=>f.Release!=null).ToArray();hit.Resolve(releases[0].Release,Vector3.left);bool duplicate=false;try{hit.Resolve(releases[0].Release,Vector3.right);}catch(InvalidOperationException){duplicate=true;}Check("coarse-frame-one-release-one-real-target-settlement",releases.Length==1&&left.Health.Current==88&&right.Health.Current==100&&duplicate);}));
037 |  steps.Add((7.0,Reset));steps.Add((7.2,()=>{Pointer(right.transform.position);Stick(Vector2.left);}));steps.Add((7.4,()=>Check("valid-pointer-has-priority-over-stick",p.DataBus.AttackDirection.x>.9f)));
038 |  steps.Add((7.5,()=>Pointer(p.transform.position)));steps.Add((7.7,()=>Check("zero-pointer-vector-falls-back-to-stick",p.DataBus.AttackDirection.x<-.9f)));
039 |  steps.Add((7.8,()=>{p.SetPaused(true);Pointer(right.transform.position);Keys(Key.D);}));steps.Add((8.1,()=>Check("pause-freezes-orientation-despite-new-input",p.DataBus.AttackDirection.x<-.9f)));
040 |  EditorApplication.CallbackFunction tick=null;void Cleanup(){EditorApplication.update-=tick;input.RemovePointerRegion(owner);input.mouseAction.LoadBindingOverridesFromJson(pointerJson);input.movementAction.LoadBindingOverridesFromJson(keyboardJson);input.gamepadAimAction.LoadBindingOverridesFromJson(stickJson);p.CancelAction("world-contract-cleanup");p.SetPaused(false);right.transform.position=oldRight;left.transform.position=oldLeft;right.RestoreHealth();left.RestoreHealth();InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(pad);}
041 |  tick=()=>{try{if(!Application.isPlaying||p==null)throw new Exception("Scene ended");while(next<steps.Count&&EditorApplication.timeSinceStartup-started>=steps[next].at)steps[next++].work();if(next==steps.Count){Cleanup();Flush("completed");}}catch(Exception error){failed++;records.Add(new{error=error.ToString()});Cleanup();Flush("failed");}};EditorApplication.update+=tick;Flush("running");ctx.Log("Scheduled 15 actual input/Spine/physical-hit contracts; 8.1 seconds");
042 |  }
043 | }
044 | 
```

行1–44：限定单角色Play场，以免取到未受控双角色。键鼠手柄绑定隔离，39定时步骤/15结论检测方向/Spine/物理左右靶HP；UI owner/粗帧释放/暂停边界均记录；Cleanup恢复覆盖、靶与设备。
用户结论：待审。

