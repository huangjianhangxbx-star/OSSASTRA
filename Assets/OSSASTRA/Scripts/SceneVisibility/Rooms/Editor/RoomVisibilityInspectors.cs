/*
 * 功能：为房间可见性的五类作者组件提供中文字段、提示和明确的登记检查入口。
 * 边界：仅编辑已有序列化配置、显示只读运行状态；不创建EditorWindow，不驱动角色或改运行规则。
 * 协作：登记检查调用同一GameObject上的SceneVisibilityRoot与模块现有验证；数组和对象引用沿用原生编辑。
 */
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ossastra.SceneVisibility.Rooms.Editor
{
    internal static class RoomInspectorFields
    {
        internal static GUIContent Label(string name, string tooltip)
        {
            return new GUIContent(name, tooltip);
        }

        internal static void Draw(SerializedObject serialized, Dictionary<string, GUIContent> labels)
        {
            serialized.Update();
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property, Label("脚本", "当前组件的脚本资源，只读。"));
                    continue;
                }
                // 仍迭代全部可见字段；以后Runtime增加配置时不会被固定Inspector列表悄悄隐藏。
                if (!labels.TryGetValue(property.name, out GUIContent content))
                    content = new GUIContent(property.displayName, property.tooltip);
                EditorGUILayout.PropertyField(property, content, true);
            }
            serialized.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(RoomVisibilityModule)), CanEditMultipleObjects]
    public sealed class RoomVisibilityModuleInspector : UnityEditor.Editor
    {
        private string validationMessage;
        private MessageType validationType;
        private static readonly Dictionary<string, GUIContent> labels = new Dictionary<string, GUIContent>
        {
            { "buildings", RoomInspectorFields.Label("登记建筑", "明确引用本相机负责的建筑；不根据名称、Layer或门户连接自动猜测。") },
            { "sharedSurfaces", RoomInspectorFields.Label("跨空间地表", "可跨多个房间的地表Renderer。许可遮罩逐片元裁剪；不能同时混入屋顶或房间内容列表。") },
            { "effectEnabled", RoomInspectorFields.Label("启用房间可见性", "控制本模块的呈现需求；关闭不暂停世界，也不改变角色、碰撞或存档。") },
            { "revealRoofs", RoomInspectorFields.Label("入室揭开建筑顶盖", "主控进入登记建筑后，统一隐藏该建筑登记的顶盖视觉；碰撞、脚本和业务根继续运行。") },
            { "positiveRenderingEnabled", RoomInspectorFields.Label("合法视域合成", "仅绘制当前种子和合法首跳片段；关闭只用于原图对照。") },
            { "truthMode", RoomInspectorFields.Label("纯色真值诊断", "用纯色检查实际允许像素；关闭恢复原材质适配。") },
            { "localObjectRevealEnabled", RoomInspectorFields.Label("局部物件透视", "独立扩展入口，本轮默认关闭；不扩大房间或门窗权限。") },
            { "observerHeight", RoomInspectorFields.Label("稳定观察点高度", "从原Current脚点向世界上方偏移的米数；不使用相机位置作视线权威。") },
            { "naturalPlayerVision", RoomInspectorFields.Label("玩家自然视野", "前方180°、不套旧6/8米门窗距离；关闭仅作有限策略对照。") },
            { "realWindowScenery", RoomInspectorFields.Label("真实窗孔外景", "相机只通过当前房间通往室外的洞口显示真实景物，不给人物标签、锁定或邻室许可。") },
            { "fog", RoomInspectorFields.Label("黑幕呈现", "柔边向合法域内部收缩；只有明确静态环境允许短暂退场。") },
            { "staticEnvironmentSurfaces", RoomInspectorFields.Label("静态过渡环境名单", "明确登记地表、地板、固定墙/柜；角色、动画、可破坏物与活动门窗不得登记。移动后即时降级。") },
            { "movingSightBlockers", RoomInspectorFields.Label("活动门窗遮挡盒", "明确登记活动门板、窗板；绝不保留静态退场，不修改碰撞和业务。") },
            { "staticEnvironmentRoots", RoomInspectorFields.Label("固定地表作者根", "初始化时登记其生成显示叶；禁止混入角色、可破坏物和业务根，不逐帧扫场。") },
            { "sightBlockers", RoomInspectorFields.Label("不透光实体遮挡盒", "明确登记真实墙、窗板、门板和高柜的BoxCollider；透明玻璃不登记，最多64项。") },
            { "immediateResolveDistance", RoomInspectorFields.Label("立即重判位移阈值", "同一角色跨越此世界距离视作传送，绕过旧门槛滞回重新判定；不会移动角色。") }
        };

        public override void OnInspectorGUI()
        {
            RoomInspectorFields.Draw(serializedObject, labels);
            EditorGUILayout.Space();
            if (GUILayout.Button(new GUIContent("检查建筑登记", "检查当前组件引用、稳定ID、体积、顶盖/结构/内容互斥和局部透视冲突；不会自动修复或重新初始化。")))
                ValidateSelectedModules();
            if (!string.IsNullOrEmpty(validationMessage))
                EditorGUILayout.HelpBox(validationMessage, validationType);

            foreach (Object selected in targets)
            {
                var module = selected as RoomVisibilityModule;
                if (module == null) continue;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(targets.Length > 1 ? "当前状态 · " + module.name : "当前状态", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("状态", module.Status ?? "未初始化");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("活动建筑", module.ActiveBuilding, typeof(BuildingVisibilityRoot), true);
                    EditorGUILayout.ObjectField("活动房间", module.ActiveRoom, typeof(RoomRegion), true);
                    EditorGUILayout.IntField("历史到达（不授显示权）", module.DiscoveredRoomCount);
                }
            }
        }

        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        private void ValidateSelectedModules()
        {
            var text = new StringBuilder();
            bool allValid = true;
            foreach (Object selected in targets)
            {
                var module = selected as RoomVisibilityModule;
                if (module == null) continue;
                var root = module.GetComponent<SceneVisibilityRoot>();
                bool valid = module.TryValidateConfiguration(root, out string reason);
                if (text.Length > 0) text.AppendLine();
                text.Append(module.name).Append(valid ? "：登记检查通过。" : "：" + reason);
                allValid &= valid;
            }
            validationMessage = text.ToString();
            validationType = allValid ? MessageType.Info : MessageType.Error;
        }
    }

    [CustomEditor(typeof(BuildingVisibilityRoot)), CanEditMultipleObjects]
    public sealed class BuildingVisibilityRootInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string, GUIContent> labels = new Dictionary<string, GUIContent>
        {
            { "buildingId", RoomInspectorFields.Label("建筑稳定ID", "登记范围内必须唯一且非空；不使用GameObject名称或Tag作为建筑身份。") },
            { "occupancyVolumes", RoomInspectorFields.Label("建筑内部体积", "相对本Transform的定向盒并集；明确建筑的实际内部与高度范围，不从屋顶包围盒推断。") },
            { "enterInset", RoomInspectorFields.Label("进入水平收缩", "以世界单位收缩水平进入门槛，减少边缘闪换；不收缩贴地脚点的Y高度范围。") },
            { "exitMargin", RoomInspectorFields.Label("退出水平余量", "以世界单位扩张水平保留范围，角色短暂贴近门槛时保留原建筑归属。") },
            { "regions", RoomInspectorFields.Label("室内区域", "登记本栋的房间、走廊等逻辑区域；实际高度和内容归属由各区域明确配置。") },
            { "roofGroups", RoomInspectorFields.Label("全栋顶盖组", "本栋所有登记顶盖共同揭开；房间不各自控制独立揭顶开关。") },
            { "portals", RoomInspectorFields.Label("门窗观察开口", "登记真实门窗孔洞；观察连接不扩大建筑身份，也不自动解锁未到达房间。") },
            { "frontWalls", RoomInspectorFields.Label("前向墙段", "仅当前种子房间按实际相机方向剔除必要墙段；共墙取种子所在侧法线，不扩大另一侧内容许可。") },
            { "structuralMembers", RoomInspectorFields.Label("永久结构成员", "墙、压顶、柱和门框等保持实体深度，不参与揭顶或角色局部透视。") }
        };

        public override void OnInspectorGUI()
        {
            RoomInspectorFields.Draw(serializedObject, labels);
        }
    }

    [CustomEditor(typeof(RoomRegion)), CanEditMultipleObjects]
    public sealed class RoomRegionInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string, GUIContent> labels = new Dictionary<string, GUIContent>
        {
            { "roomId", RoomInspectorFields.Label("区域稳定ID", "本模块登记范围内唯一且非空，用于主控空间归属与本会话到达记录。") },
            { "floorId", RoomInspectorFields.Label("楼层标识", "作者明确的楼层身份；实际高度归属以体积Y范围为准，不由该名称自动推断。") },
            { "priority", RoomInspectorFields.Label("重叠选择优先级", "多个区域同时有效时较大值优先，同值按稳定ID确定；普通帧仍先尊重已有区域的退出滞回。") },
            { "volumes", RoomInspectorFields.Label("区域体积", "相对本Transform的定向盒并集；可用少量盒表示凹角房间，Y尺寸定义有效楼层高度。") },
            { "members", RoomInspectorFields.Label("区域内容成员", "本区域内容和地板；墙/压顶归建筑永久结构，顶盖归建筑顶盖组。不同区域不可共用一个内容Renderer。") },
            { "enterInset", RoomInspectorFields.Label("进入水平收缩", "只收缩水平进入门槛，避免在门槛边缘反复切换；不排除贴地脚点。") },
            { "exitMargin", RoomInspectorFields.Label("退出水平余量", "只扩张水平保留范围；离开真实高度区间时仍会失去本层区域资格。") }
        };

        public override void OnInspectorGUI()
        {
            RoomInspectorFields.Draw(serializedObject, labels);
        }
    }

    [CustomEditor(typeof(RoofVisibilityGroup)), CanEditMultipleObjects]
    public sealed class RoofVisibilityGroupInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string, GUIContent> labels = new Dictionary<string, GUIContent>
        {
            { "roofGroupId", RoomInspectorFields.Label("顶盖组稳定ID", "本模块登记范围内唯一且非空；由所属建筑统一提交显隐需求。") },
            { "members", RoomInspectorFields.Label("可隐藏顶盖成员", "仅登记屋顶或遮盖天花板Renderer；不混入墙顶、地板、台阶、承载面或业务根。") }
        };

        public override void OnInspectorGUI()
        {
            RoomInspectorFields.Draw(serializedObject, labels);
        }
    }

    [CustomEditor(typeof(ViewPortal)), CanEditMultipleObjects]
    public sealed class ViewPortalInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string, GUIContent> labels = new Dictionary<string, GUIContent>
        {
            { "portalId", RoomInspectorFields.Label("开口稳定ID", "本模块登记范围内唯一且非空；用于明确门窗的作者身份。") },
            { "inside", RoomInspectorFields.Label("内侧区域", "开口的内部所属区域；本Transform.forward必须从该内侧朝向外侧。") },
            { "outside", RoomInspectorFields.Label("外侧区域", "空引用表示室外；引用邻室时仅登记结构关系，不借此自动解锁邻室。") },
            { "apertureMesh", RoomInspectorFields.Label("真实孔洞网格", "位于本地XY平面的可读Mesh，形状与实体墙洞吻合；不使用静态假窗贴图。") },
            { "open", RoomInspectorFields.Label("开口处于开放状态", "控制本开口的观察许可；真实遮板或门的几何状态应由原有适配或样板控件同步。") },
            { "canSeeThrough", RoomInspectorFields.Label("允许透过观察", "是否允许通过真实孔洞看到当前帧景物；仍需满足房间资格与实际深度。") },
            { "observerRange", RoomInspectorFields.Label("有限策略 · 观察距离", "只供独立有限观察策略；玩家自然视野和真实窗景忽略此上限。") },
            { "maxSightDistance", RoomInspectorFields.Label("有限策略 · 延伸距离", "只供独立有限观察策略；玩家自然视野和真实窗景忽略此上限。") },
            { "canTraverse", RoomInspectorFields.Label("允许通行（作者信息）", "与观察许可独立；本组件不会据此修改碰撞、攻击或拾取规则。") }
        };

        public override void OnInspectorGUI()
        {
            RoomInspectorFields.Draw(serializedObject, labels);
        }
    }

    [CustomPropertyDrawer(typeof(Ossastra.SceneVisibility.Rooms.Rendering.RoomFogSettings))]
    public sealed class RoomFogSettingsDrawer : PropertyDrawer
    {
        static readonly string[] names={"Enabled","InitialVisibility","GainSeconds","LossSeconds","SpreadSeconds","SpreadRadius","InwardFeather","Color","DitherEnabled","DitherCellSize","DitherStrength"};
        static readonly string[] labels={"启用静态过渡与柔边","初始可见度","显现时间（最多0.5秒）","退场时间（最多0.3秒）","扩散时间","扩散参考半径","内向柔边宽度","黑幕颜色","稳定世界点阵","点阵单元尺寸","点阵边缘强度"};
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)=>EditorGUIUtility.singleLineHeight+(property.isExpanded?names.Length*(EditorGUIUtility.singleLineHeight+EditorGUIUtility.standardVerticalSpacing):0);
        public override void OnGUI(Rect rect,SerializedProperty property,GUIContent label)
        {
            EditorGUI.BeginProperty(rect,label,property);float line=EditorGUIUtility.singleLineHeight,step=line+EditorGUIUtility.standardVerticalSpacing;
            property.isExpanded=EditorGUI.Foldout(new Rect(rect.x,rect.y,rect.width,line),property.isExpanded,label,true);
            if(property.isExpanded){++EditorGUI.indentLevel;for(int i=0;i<names.Length;i++)EditorGUI.PropertyField(new Rect(rect.x,rect.y+step*(i+1),rect.width,line),property.FindPropertyRelative(names[i]),new GUIContent(labels[i]));--EditorGUI.indentLevel;}
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(RoomLocalBox))]
    public sealed class RoomLocalBoxDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return property.isExpanded ? line * 3f + EditorGUIUtility.standardVerticalSpacing * 2f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float step = line + EditorGUIUtility.standardVerticalSpacing;
            var title = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(title, property.isExpanded,
                new GUIContent(label.text, "相对所属组件Transform的局部定向盒；尺寸必须为有限正数。"), true);
            if (property.isExpanded)
            {
                ++EditorGUI.indentLevel;
                EditorGUI.PropertyField(new Rect(position.x, position.y + step, position.width, line),
                    property.FindPropertyRelative("center"), new GUIContent("局部中心", "相对所属组件Transform的位置。"));
                EditorGUI.PropertyField(new Rect(position.x, position.y + step * 2f, position.width, line),
                    property.FindPropertyRelative("size"), new GUIContent("局部尺寸", "定向盒的完整XYZ尺寸；Y尺寸参与实际高度归属。"));
                --EditorGUI.indentLevel;
            }
            EditorGUI.EndProperty();
        }
    }
}
