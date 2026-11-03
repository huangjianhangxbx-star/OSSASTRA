/* 功能：区分原生Lit与无光照受光适配的配置入口，避免显示实际不会消费的参数。
 * 边界：仅Inspector；不改材质、角色或环境状态，变更只由明确编辑操作产生。
 */
using UnityEditor;
namespace Ossastra.EnvironmentSimulation.Editor
{
 [CustomEditor(typeof(EnvironmentMaterialResponse))]
 public sealed class EnvironmentMaterialResponseEditor:UnityEditor.Editor
 {
  public override void OnInspectorGUI()
  {
   serializedObject.Update();var role=serializedObject.FindProperty("role");EditorGUILayout.PropertyField(role);
   if(role.enumValueIndex==(int)EnvironmentMaterialRole.NativeLit)
    EditorGUILayout.HelpBox("原生 Lit 由原材质及 URP 灯光负责受光。此角色不使用无光照适配的响应倍率、接影开关或最低亮度覆盖。",MessageType.Info);
   else
   {
    EditorGUILayout.PropertyField(serializedObject.FindProperty("response"));EditorGUILayout.PropertyField(serializedObject.FindProperty("receiveSunShadow"));
    EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumReadability"));EditorGUILayout.PropertyField(serializedObject.FindProperty("renderers"),true);
   }
   serializedObject.ApplyModifiedProperties();
  }
 }
}
