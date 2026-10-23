#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponStatsSO))]
public class WeaponStatsSOEditor : Editor
{
    private SerializedProperty weaponTypeProp;
    private SerializedProperty coreStatsProp;
    private SerializedProperty characterBonusStatsProp;
    private SerializedProperty firearmStatsProp;
    private SerializedProperty meleeStatsProp;

    private void OnEnable()
    {
        weaponTypeProp = serializedObject.FindProperty("weaponType");
        coreStatsProp = serializedObject.FindProperty("commonCoreStats");
        characterBonusStatsProp = serializedObject.FindProperty("characterBonusStats");
        firearmStatsProp = serializedObject.FindProperty("firearmStats");
        meleeStatsProp = serializedObject.FindProperty("meleeStats");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("武器数值配置", EditorStyles.boldLabel);

        DrawRequiredProperty(weaponTypeProp, "武器类型");
        DrawRequiredProperty(coreStatsProp, "武器核心硬数值");
        DrawRequiredProperty(characterBonusStatsProp, "装备后角色加成");

        if (weaponTypeProp != null)
        {
            WeaponType weaponType = (WeaponType)weaponTypeProp.enumValueIndex;

            switch (weaponType)
            {
                case WeaponType.Firearm:
                    DrawRequiredProperty(firearmStatsProp, "枪械硬数值");
                    break;

                case WeaponType.Melee:
                    DrawRequiredProperty(meleeStatsProp, "近战硬数值");
                    break;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRequiredProperty(SerializedProperty property, string label)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox($"找不到字段：{label}。请检查 WeaponStatsSO 字段名。", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label), true);
    }
}
#endif