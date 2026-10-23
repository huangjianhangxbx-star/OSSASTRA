/* 功能：角色Inspector直接编辑实际生效的共享输入，隐藏被覆盖的本地动作；组合键沿InputSystem原生绘制。 */
using UnityEditor;
using UnityEngine;
namespace Ossastra.Characters.Editor
{
 [CustomEditor(typeof(CharacterInputHandler))]
 public sealed class CharacterInputHandlerEditor:UnityEditor.Editor
 {
  private UnityEditor.Editor layoutEditor;
  private Object inspected;
  public override void OnInspectorGUI()
  {
   serializedObject.Update();
   var layout=serializedObject.FindProperty("inputLayout");
   EditorGUILayout.PropertyField(layout,new GUIContent("共享输入配置"));
   serializedObject.ApplyModifiedProperties();
   if(layout.objectReferenceValue==null){DrawDefaultInspector();EditorGUILayout.HelpBox("当前使用组件本地Action。Action右上角 + → Add Button With One Modifier Composite，展开后分别设置Modifier和Button。",MessageType.Info);return;}
   EditorGUILayout.HelpBox("下方直接编辑当前生效的共享配置；所有引用它的角色与场景服务共同采用。停止Play后修改，下次运行生效。",MessageType.Info);
   EditorGUILayout.HelpBox("组合键：目标Action右上角 + → Button With One Modifier Composite（有些版本显示One Modifier）。展开组合项：Modifier填LB，Button填X，即LB+X。两个普通Binding表示二选一，不表示同时按。X=West、Y=North、B=East、A=South。",MessageType.None);
   if(inspected!=layout.objectReferenceValue){if(layoutEditor!=null)DestroyImmediate(layoutEditor);inspected=layout.objectReferenceValue;layoutEditor=CreateEditor(inspected);}
   using(new EditorGUI.DisabledScope(Application.isPlaying)){layoutEditor.OnInspectorGUI();}
   EditorGUILayout.Space();EditorGUILayout.LabelField("本角色开发检查输入",EditorStyles.boldLabel);
   serializedObject.Update();foreach(var name in new[]{"debugValidAction","debugBlockedAction","debugResetAction","debugInspectAction"})EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);serializedObject.ApplyModifiedProperties();
  }
  private void OnDisable(){if(layoutEditor!=null)DestroyImmediate(layoutEditor);}
 }
}
