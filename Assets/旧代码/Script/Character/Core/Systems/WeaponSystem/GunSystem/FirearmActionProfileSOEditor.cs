#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FirearmActionProfileSO))]
public class FirearmActionProfileSOEditor : Editor
{
    private SerializedProperty firearmCategoryProp;
    private SerializedProperty defaultFireModeProp;
    private SerializedProperty fireModesProp;
    private SerializedProperty hitTypeProp;
    private SerializedProperty fireAnimationDurationProp;
    private SerializedProperty reloadAnimationFallbackDurationProp;

    private void OnEnable()
    {
        firearmCategoryProp = serializedObject.FindProperty("firearmCategory");
        defaultFireModeProp = serializedObject.FindProperty("defaultFireMode");
        fireModesProp = serializedObject.FindProperty("fireModes");
        hitTypeProp = serializedObject.FindProperty("hitType");
        fireAnimationDurationProp = serializedObject.FindProperty("fireAnimationDuration");
        reloadAnimationFallbackDurationProp = serializedObject.FindProperty("reloadAnimationFallbackDuration");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("ǹе��Ϊ����", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(firearmCategoryProp, new GUIContent("ǹе����"));
        EditorGUILayout.PropertyField(defaultFireModeProp, new GUIContent("Ĭ�����ģʽ"));

        EditorGUILayout.Space(8);
        DrawFireModes();

        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(hitTypeProp, new GUIContent("���з�ʽ"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("����", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(fireAnimationDurationProp, new GUIContent("���𶯻�ʱ��"));
        EditorGUILayout.PropertyField(reloadAnimationFallbackDurationProp, new GUIContent("������������ʱ��"));

        serializedObject.ApplyModifiedProperties();
    }
    private void DrawProperty(
    SerializedProperty property,
    string label,
    string fieldName)
    {
        if (property == null)
        {
            EditorGUILayout.HelpBox(
                $"�Ҳ������л��ֶΣ�{fieldName}",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.PropertyField(
            property,
            new GUIContent(label)
        );
    }
    private void DrawFireModes()
    {
        EditorGUILayout.LabelField("�������ģʽ", EditorStyles.boldLabel);

        if (fireModesProp == null)
        {
            EditorGUILayout.HelpBox("�Ҳ��� fireModes �ֶΡ�", MessageType.Error);
            return;
        }

        for (int i = 0; i < fireModesProp.arraySize; i++)
        {
            SerializedProperty element = fireModesProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            SerializedProperty modeProp = element.FindPropertyRelative("fireModeType");
            GunFireModeType mode = modeProp != null
                ? (GunFireModeType)modeProp.enumValueIndex
                : GunFireModeType.SemiAuto;

            EditorGUILayout.LabelField($"ģʽ {i + 1}: {mode}", EditorStyles.boldLabel);

            if (GUILayout.Button("ɾ��", GUILayout.Width(50)))
            {
                fireModesProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            DrawFireModeConfig(element);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("�������ģʽ"))
        {
            fireModesProp.arraySize++;

            SerializedProperty newElement = fireModesProp.GetArrayElementAtIndex(fireModesProp.arraySize - 1);
            SerializedProperty modeProp = newElement.FindPropertyRelative("fireModeType");

            if (modeProp != null)
                modeProp.enumValueIndex = (int)GunFireModeType.SemiAuto;
        }
    }

    private void DrawFireModeConfig(SerializedProperty configProp)
    {
        SerializedProperty modeProp = configProp.FindPropertyRelative("fireModeType");

        if (modeProp == null)
        {
            EditorGUILayout.HelpBox("�Ҳ��� fireModeType �ֶΡ�", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(modeProp, new GUIContent("���ģʽ"));

        GunFireModeType mode = (GunFireModeType)modeProp.enumValueIndex;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("ͨ��", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("fireInterval"),
            new GUIContent("������")
        );

        switch (mode)
        {
            case GunFireModeType.SemiAuto:
                DrawSemiAutoConfig(configProp);
                break;

            case GunFireModeType.Burst:
                DrawBurstConfig(configProp);
                break;

            case GunFireModeType.FullAuto:
                DrawFullAutoConfig(configProp);
                break;

            case GunFireModeType.Charge:
                DrawChargeConfig(configProp);
                break;

            case GunFireModeType.Beam:
                DrawBeamConfig(configProp);
                break;

            case GunFireModeType.Special:
                DrawSpecialConfig(configProp);
                break;
        }
    }

    private void DrawSemiAutoConfig(SerializedProperty configProp)
    {
    }

    private void DrawBurstConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Burst ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstCount"),
            new GUIContent("��������")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstShotInterval"),
            new GUIContent("�����������")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("burstCycleInterval"),
            new GUIContent("����ѭ�����")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס��������")
        );
    }

    private void DrawFullAutoConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Full Auto �Զ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס���")
        );
    }

    private void DrawChargeConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Charge ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("chargeTime"),
            new GUIContent("����ʱ��")
        );
    }

    private void DrawBeamConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Beam ��������", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("chargeTime"),
            new GUIContent("���� / ����ʱ��")
        );

        EditorGUILayout.PropertyField(
            configProp.FindPropertyRelative("allowHoldToFire"),
            new GUIContent("������ס��������")
        );
    }

    private void DrawSpecialConfig(SerializedProperty configProp)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("Special ģʽ��ʱû��ר�����á�", MessageType.Info);
    }
}
#endif
