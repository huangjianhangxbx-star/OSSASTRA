// 横版运动作者页。正式角色引用、实验候选和实测轨迹各自有明确的生命周期。
using System;
using System.Linq;
using Ossastra.Characters;
using UnityEditor;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using UObject = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    internal sealed class AttackLabMovementPanel : IDisposable
    {
        private readonly AttackLabMovementTrace trace = new AttackLabMovementTrace();
        private ActorRoot actor;
        private CharacterMovement motor;
        private CharacterJumpProfile source, draft, applied;
        private string sourceVersion, status;
        private Vector2 scroll;
        private bool traversalOpen;
        private bool velocityChart;

        public void Observe(AttackLabSession session)
        {
            var active = session != null ? session.Actor : null;
            if (active != actor)
            {
                ReleaseCandidate();
                DestroyProfile(ref draft);
                actor = active;
                motor = actor != null ? actor.GetComponent<CharacterMovement>() : null;
                ReadSource();
            }
            if (motor != null && motor.JumpProfile != applied)
                DestroyProfile(ref applied);
            trace.Observe(session, applied != null);
        }

        public void Draw(AttackLabSession session)
        {
            Observe(session);
            if (!Application.isPlaying || session == null || actor == null || motor == null || draft == null)
            {
                EditorGUILayout.HelpBox("进入调试台独立测试场后，在此调整已装配的横版角色。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { DrawBody(session); }
            finally { EditorGUILayout.EndScrollView(); }
        }

        private void DrawBody(AttackLabSession session)
        {
            EditorGUILayout.LabelField("移动、跳跃与翻越", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("正在调整", actor.Definition != null ? actor.Definition.displayName : actor.name);
            EditorGUILayout.ObjectField("正式运动来源", motor.AuthoredJumpProfile, typeof(CharacterJumpProfile), false);
            int sceneUsers = session.Party.Characters.Count(item =>
                item != null && item.GetComponent<CharacterMovement>()?.AuthoredJumpProfile == source);
            bool hasRoleOverride = actor.Definition != null && actor.Definition.sideViewMovementOverride == source;
            EditorGUILayout.LabelField("写回影响范围",
                (hasRoleOverride ? "角色定义引用的配置" : "场景和 Prefab 引用的配置") +
                $" · 本场 {sceneUsers} 名角色共用 · 写回影响全部引用者", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("候选在复位时生效", applied != null ? "试验当前使用候选" : "试验当前使用正式值");

            var authored = motor.AuthoredJumpProfile;
            bool conflict = authored != source || source == null || sourceVersion != EditorJsonUtility.ToJson(source);
            if (conflict) EditorGUILayout.HelpBox("正式来源或其数值已变化。本页保留草稿，重新读取后才可应用或写回。", MessageType.Warning);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                var names = new[] { "平地", "低台沿", "侧墙", "低顶", "定点下落", "高台沿" };
                int selected = Mathf.Clamp((int)session.Station, 0, names.Length - 1);
                var station = (AttackLabStation)EditorGUILayout.Popup("运动测试区", selected, names);
                if (station != session.Station)
                {
                    session.Station = station;
                    Reset(session);
                }
                if (GUILayout.Button("重新开始本区", GUILayout.Width(104))) Reset(session);
            }

            DrawMeasurements(session);
            EditorGUILayout.Space();
            var candidateData = new SerializedObject(draft);
            var originalData = new SerializedObject(source != null ? source : draft);
            var activeData = new SerializedObject(motor.JumpProfile != null ? motor.JumpProfile : draft);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("正式来源", GUILayout.Width(70));
                GUILayout.Label("候选", GUILayout.Width(90));
                GUILayout.Label("当前生效", GUILayout.Width(70));
            }
            EditorGUILayout.LabelField("地面移动 · 世界单位/秒", EditorStyles.boldLabel);
            Row("runSpeed", "跑速", originalData, candidateData, activeData);
            Row("walkSpeedRatio", "慢走速度倍率", originalData, candidateData, activeData);
            EditorGUILayout.LabelField("普通跳跃", EditorStyles.boldLabel);
            Row("jumpSpeed", "起跳速度", originalData, candidateData, activeData);
            Row("sustainSteps", "长按持续步数", originalData, candidateData, activeData);
            Row("minimumSustainSteps", "最少维持步数", originalData, candidateData, activeData);
            Row("releaseSpeedScale", "松开时上升速度倍率", originalData, candidateData, activeData);
            Row("gravity", "重力", originalData, candidateData, activeData);
            Row("fallSpeed", "最大下落速度", originalData, candidateData, activeData);
            Row("hangInitialGravity", "悬顶初始重力倍率", originalData, candidateData, activeData);
            Row("hangGravityRamp", "悬顶重力恢复速率", originalData, candidateData, activeData);
            Row("queueSteps", "落地前缓冲步数", originalData, candidateData, activeData);
            Row("ledgeGraceSteps", "离台后宽容步数", originalData, candidateData, activeData);
            Row("headLockSteps", "碰顶后锁定步数", originalData, candidateData, activeData);
            EditorGUILayout.LabelField("模拟步长（只读）", $"{Mathf.Clamp(source != null ? source.stepSeconds : .02f, .005f, .05f) * 1000f:0.##} ms");

            traversalOpen = EditorGUILayout.Foldout(traversalOpen, "抓边、翻越与墙边能力", true);
            if (traversalOpen) DrawTraversal(originalData, candidateData, activeData);
            candidateData.ApplyModifiedPropertiesWithoutUndo();
            string invalid = InvalidReason(draft);
            if (!string.IsNullOrEmpty(invalid)) EditorGUILayout.HelpBox(invalid, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(conflict || invalid != null))
                    if (GUILayout.Button("应用候选并复位")) ApplyCandidate(session);
                if (GUILayout.Button("恢复基线并复位"))
                {
                    ReleaseCandidate();
                    Reset(session);
                }
                if (GUILayout.Button("重读正式配置")) ReadSource();
            }
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(conflict || invalid != null))
            {
                if (GUILayout.Button("写回共享来源")) Save(false);
                if (GUILayout.Button("另存并绑定角色配置")) Save(true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(invalid != null))
                    if (GUILayout.Button("另存运动预设")) SavePreset(false);
                var preset = (CharacterJumpProfile)EditorGUILayout.ObjectField("读取候选预设", null, typeof(CharacterJumpProfile), false);
                if (preset != null)
                {
                    float step = draft.stepSeconds;
                    EditorUtility.CopySerialized(preset, draft);
                    draft.stepSeconds = step;
                    status = "预设已读取到候选。";
                }
            }
        }

        private void DrawTraversal(SerializedObject original, SerializedObject candidate, SerializedObject active)
        {
            var settings = draft.traversal;
            if (settings == null) return;
            Row("traversal.enabled", "启用现有抓边能力", original, candidate, active);
            using (new EditorGUI.DisabledScope(!settings.enabled))
            {
                Row("traversal.wallReach", "侧墙探测距离", original, candidate, active);
                Row("traversal.tapGrabWallReach", "短按补探距离", original, candidate, active);
                Row("traversal.tapGrabBodyFraction", "短按身体探测比例", original, candidate, active);
                Row("traversal.gripBelowHand", "挂点向下探测", original, candidate, active);
                Row("traversal.gripAboveHand", "挂点向上探测", original, candidate, active);
                Row("traversal.gripMaxRisingSpeed", "可抓边最大上升速度", original, candidate, active);
                Row("traversal.surfaceClearance", "胶囊离墙净距", original, candidate, active);
                Row("traversal.grabSpeed", "贴合挂点速度", original, candidate, active);
                Row("traversal.climbUpSpeed", "上提基速", original, candidate, active);
                Row("traversal.climbLiftSpeedScale", "主动翻越提身倍率", original, candidate, active);
                Row("traversal.climbAcrossSpeed", "横移上台速度", original, candidate, active);
                Row("traversal.allowWallSlide", "允许墙滑", original, candidate, active);
                using (new EditorGUI.DisabledScope(!settings.allowWallSlide))
                {
                    Row("traversal.wallSlideStickSeconds", "墙滑停留秒数", original, candidate, active);
                    Row("traversal.wallSlideAcceleration", "墙滑下落加速度", original, candidate, active);
                    Row("traversal.wallSlideMaxSpeed", "墙滑最大速度", original, candidate, active);
                }
                Row("traversal.allowWallJump", "允许墙跳", original, candidate, active);
                using (new EditorGUI.DisabledScope(!settings.allowWallJump))
                {
                    Row("traversal.wallJumpKickSpeed", "墙跳横向速度", original, candidate, active);
                    Row("traversal.wallJumpUpSpeed", "墙跳向上速度", original, candidate, active);
                    Row("traversal.wallJumpShortLockSteps", "墙跳短锁步数", original, candidate, active);
                    Row("traversal.wallJumpLongLockSteps", "墙跳长锁步数", original, candidate, active);
                    Row("traversal.wallJumpSameWallSteps", "同墙再次起跳步数", original, candidate, active);
                }
            }
        }

        private void DrawMeasurements(AttackLabSession session)
        {
            var live = trace.Live;
            var moveState = actor.StateArbitrator?.Movement;
            var last = live != null && live.steps.Count > 0 ? live.steps.Last() : default;
            float requested = motor.RequestedVelocity.x;
            string reason = moveState?.TraversalLastRejection;
            var runProfile = motor.JumpProfile;
            var support = last.SupportCollider;
            EditorGUILayout.LabelField("实测速度", $"请求 {requested:0.00} → 位移 {motor.Velocity.x:0.00} U/s"
                + $" · 竖向 {motor.VerticalSpeed:0.00} · {last.Driver}", EditorStyles.wordWrappedMiniLabel);
            if (runProfile != null)
            {
                float inputScale = Mathf.Abs(actor.DataBus?.Input.move.x ?? 0f);
                float inputRun = runProfile.runSpeed * inputScale * (motor.IsWalking ? runProfile.walkSpeedRatio : 1f);
                string multiplier = inputRun > .0001f ? $"{Mathf.Abs(requested) / inputRun:0.00}×" : "—";
                EditorGUILayout.LabelField("运动条件", $"输入 {inputScale:0.00} · 请求/输入基准 {multiplier} · " +
                    (last.HorizontalBlocked ? "最近运动步被侧面截断" : "最近运动步无侧面截断"), EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.LabelField("当前子状态", $"{moveState?.TraversalPhase ?? "未装配"} · " +
                $"{(moveState?.TraversalGrip.HasValue == true ? "挂点 " + moveState.TraversalGrip.Value.ToString("F2") : support != null ? support.name : "无支撑")}",
                EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(reason)) EditorGUILayout.LabelField("最近墙边限制", reason, EditorStyles.wordWrappedMiniLabel);
            if (live == null) return;

            var baseline = trace.Baseline?.station == session.Station ? trace.Baseline : null;
            var candidate = trace.Candidate?.station == session.Station ? trace.Candidate : null;
            velocityChart = EditorGUILayout.ToggleLeft("查看竖直速度曲线", velocityChart);
            DrawChart(live, baseline, candidate);
            DrawTrial(live, "本次");
            DrawTrial(baseline, "上次基线");
            DrawTrial(candidate, "上次候选");
            foreach (var mark in live.marks.Skip(Mathf.Max(0, live.marks.Count - 7)))
                EditorGUILayout.LabelField($"{mark.time:0.000} s · 步 {mark.step}", mark.label, EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawTrial(AttackLabMovementTrace.Trial trial, string title)
        {
            if (trial == null) return;
            var jump = trial.jump;
            string numbers = jump == null ? "" :
                $" · {jump.Height:0.00} U 高 · {jump.ascent:0.00}/{jump.elapsed:0.00} s 上升/腾空 · {jump.Horizontal:0.00} U 横移 · {jump.landingSpeed:0.00} U/s 落地";
            string condition = jump == null ? "" : jump.interruption != null ? " · 中断：" + jump.interruption :
                jump.movingSupport ? " · 移动支撑" : Mathf.Abs(jump.end.y - jump.origin.y) > .1f && jump.finished ? " · 异高落点" : "";
            EditorGUILayout.LabelField(title, $"{trial.version} · 跑 {trial.runSpeed:0.##} / 跳 {trial.jumpSpeed:0.##}" + numbers + condition,
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawChart(AttackLabMovementTrace.Trial live, AttackLabMovementTrace.Trial baseline, AttackLabMovementTrace.Trial candidate)
        {
            Rect area = EditorGUILayout.GetControlRect(false, 137f);
            EditorGUI.DrawRect(area, new Color(.13f, .16f, .19f));
            var trials = new[] { baseline, candidate, live };
            var colors = new[] { new Color(.45f, .72f, .95f), new Color(.95f, .63f, .32f), new Color(.46f, .86f, .71f) };
            var lines = trials.Where(trial => trial != null && trial.steps.Count > 1).ToArray();
            if (lines.Length == 0)
            {
                EditorGUI.LabelField(area, "在 Game 窗口使用正式输入跑、跳或抓边，轨迹将从实际物理步采样。", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            var all = lines.SelectMany(Points).ToArray();
            float minX = Mathf.Min(0, all.Min(p => p.x));
            float maxX = Mathf.Max(.1f, all.Max(p => p.x));
            float minY = Mathf.Min(0, all.Min(p => p.y));
            float maxY = Mathf.Max(.1f, all.Max(p => p.y));
            Rect graph = new Rect(area.x + 9f, area.y + 8f, area.width - 18f, area.height - 28f);
            var label = velocityChart ? "时间 / 竖向速度" : "相对起点的 XY 轨迹";
            EditorGUI.LabelField(new Rect(area.x + 8f, area.yMax - 19f, area.width - 16f, 18f),
                label + " · 绿 本次 · 蓝 基线 · 橙 候选", EditorStyles.miniLabel);
            if (Event.current.type != EventType.Repaint) return;
            Handles.BeginGUI();
            var oldColor = Handles.color;
            try
            {
                for (int i = 0; i < trials.Length; i++)
                {
                    if (trials[i] == null || trials[i].steps.Count < 2) continue;
                    var points = Points(trials[i]).Select(point => new Vector3(
                        Mathf.Lerp(graph.xMin, graph.xMax, Mathf.InverseLerp(minX, maxX, point.x)),
                        Mathf.Lerp(graph.yMax, graph.yMin, Mathf.InverseLerp(minY, maxY, point.y)))).ToArray();
                    Handles.color = colors[i];
                    Handles.DrawAAPolyLine(2f, points);
                }
            }
            finally { Handles.color = oldColor; Handles.EndGUI(); }
        }

        private Vector2[] Points(AttackLabMovementTrace.Trial trial)
        {
            var first = trial.steps[0];
            return trial.steps.Select(step => velocityChart
                ? new Vector2((step.StepId - first.StepId) * step.SimulatedSeconds, step.VerticalSpeed)
                : (Vector2)(step.WorldPosition - first.PreviousWorldPosition)).ToArray();
        }

        private static void Row(string path, string label, SerializedObject original, SerializedObject candidate, SerializedObject active)
        {
            var proposed = candidate.FindProperty(path);
            var initial = original.FindProperty(path);
            var running = active.FindProperty(path);
            if (proposed == null || initial == null || running == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Format(initial), GUILayout.Width(70));
                if (proposed.propertyType == SerializedPropertyType.Boolean)
                    proposed.boolValue = EditorGUILayout.Toggle(proposed.boolValue, GUILayout.Width(90));
                else if (proposed.propertyType == SerializedPropertyType.Integer)
                    proposed.intValue = EditorGUILayout.DelayedIntField(proposed.intValue, GUILayout.Width(90));
                else
                    proposed.floatValue = EditorGUILayout.DelayedFloatField(proposed.floatValue, GUILayout.Width(90));
                GUILayout.Label(Format(running), GUILayout.Width(70));
            }
        }

        private static string Format(SerializedProperty value)
        {
            switch (value.propertyType)
            {
                case SerializedPropertyType.Boolean: return value.boolValue ? "开" : "关";
                case SerializedPropertyType.Integer: return value.intValue.ToString();
                default: return value.floatValue.ToString("0.###");
            }
        }

        private void ApplyCandidate(AttackLabSession session)
        {
            var next = UObject.Instantiate(draft);
            next.name = source.name + " · 运行候选";
            next.hideFlags = HideFlags.HideAndDontSave;
            if (!motor.TryUseDevelopmentProfile(this, source, next))
            {
                DestroyProfile(ref next);
                status = "候选未应用。正式来源已变，或另一实验正在使用该角色。";
                return;
            }
            DestroyProfile(ref applied);
            applied = next;
            if (!Reset(session)) ReleaseCandidate();
        }

        private bool Reset(AttackLabSession session)
        {
            session.Condition = AttackLabTargetMode.Whiff;
            bool ready = session.ResetTrial(applied != null ? "运动候选" : "运动基线");
            status = session.Status;
            trace.Observe(session, applied != null);
            return ready;
        }

        private void Save(bool separate)
        {
            if (separate)
            {
                var role = actor.Definition;
                if (role == null || !EditorUtility.IsPersistent(role))
                {
                    status = "角色没有可写入的正式角色定义。";
                    return;
                }
                var asset = SavePreset(true);
                if (asset == null) return;
                Undo.RecordObject(role, "为角色绑定专用运动配置");
                role.sideViewMovementOverride = asset;
                EditorUtility.SetDirty(role);
                Undo.FlushUndoRecordObjects();
                AssetDatabase.SaveAssetIfDirty(role);
                ReadSource();
                status = "角色专用配置已绑定。下一次「恢复基线并复位」将使用新的正式配置。";
                return;
            }
            if (source == null || !EditorUtility.IsPersistent(source) ||
                !EditorUtility.DisplayDialog("写回共享运动配置",
                    $"「{source.name}」的所有正式角色引用都会使用新值。", "写回", "取消")) return;
            Undo.RecordObject(source, "写回运动配置");
            var flags = source.hideFlags;
            draft.name = source.name;
            EditorUtility.CopySerialized(draft, source);
            source.hideFlags = flags;
            EditorUtility.SetDirty(source);
            Undo.FlushUndoRecordObjects();
            AssetDatabase.SaveAssetIfDirty(source);
            ReadSource();
            status = "正式来源已写回。";
        }

        private CharacterJumpProfile SavePreset(bool separate)
        {
            var label = separate ? "新建角色专用运动配置" : "另存运动候选预设";
            string path = EditorUtility.SaveFilePanelInProject(label, actor.name + "_运动", "asset", "");
            if (string.IsNullOrEmpty(path)) return null;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                status = "目标路径已有资产。请选择新名字。";
                return null;
            }
            var asset = UObject.Instantiate(draft);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            asset.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, label);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static string InvalidReason(CharacterJumpProfile profile)
        {
            bool AtLeast(float value, float min) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= min;
            bool Between(float value, float min, float max) => AtLeast(value, min) && value <= max;
            if (!AtLeast(profile.runSpeed, .01f) || !AtLeast(profile.jumpSpeed, .01f) ||
                !AtLeast(profile.gravity, .01f) || !AtLeast(profile.fallSpeed, .01f) ||
                !Between(profile.walkSpeedRatio, .05f, 1f) || !Between(profile.releaseSpeedScale, 0f, 1f) ||
                !Between(profile.hangInitialGravity, 0f, 1f) || !AtLeast(profile.hangGravityRamp, 0f))
                return "速度与重力使用正数，倍率保持在字段标注范围内。";
            if (profile.sustainSteps < 1 || profile.minimumSustainSteps < 0 || profile.minimumSustainSteps > profile.sustainSteps ||
                profile.queueSteps < 0 || profile.ledgeGraceSteps < 0 || profile.headLockSteps < 0)
                return "步数不能为负，最少维持不能大于长按持续步数。";
            var t = profile.traversal;
            if (t != null && (!AtLeast(t.wallReach, .01f) || !AtLeast(t.tapGrabWallReach, 0f) ||
                !Between(t.tapGrabBodyFraction, 0f, 1f) || !AtLeast(t.gripBelowHand, .02f) || !AtLeast(t.gripAboveHand, .02f) ||
                !AtLeast(t.gripMaxRisingSpeed, 0f) || !AtLeast(t.surfaceClearance, .02f) ||
                !AtLeast(t.grabSpeed, .1f) || !AtLeast(t.climbUpSpeed, .1f) || !AtLeast(t.climbLiftSpeedScale, .1f) ||
                !AtLeast(t.climbAcrossSpeed, .1f) || !AtLeast(t.wallSlideStickSeconds, 0f) ||
                !AtLeast(t.wallSlideAcceleration, .01f) || !AtLeast(t.wallSlideMaxSpeed, .01f) ||
                !AtLeast(t.wallJumpKickSpeed, .01f) || !AtLeast(t.wallJumpUpSpeed, .01f) ||
                t.wallJumpShortLockSteps < 0 || t.wallJumpLongLockSteps < 1 || t.wallJumpSameWallSteps < 1 ||
                t.wallJumpLongLockSteps < t.wallJumpShortLockSteps))
                return "请检查探测距离、抓边速度或墙边锁步。短锁步数不能超过长锁步数。";
            return null;
        }

        private void ReadSource()
        {
            DestroyProfile(ref draft);
            source = motor != null ? motor.AuthoredJumpProfile : null;
            if (source == null) { sourceVersion = null; return; }
            // Instantiate 会复制序列化的普通 class traversal；编辑候选不会写到正式来源。
            draft = UObject.Instantiate(source);
            draft.hideFlags = HideFlags.HideAndDontSave;
            sourceVersion = EditorJsonUtility.ToJson(source);
            status = null;
        }

        private void ReleaseCandidate()
        {
            if (motor != null && applied != null) motor.ReleaseDevelopmentProfile(this);
            DestroyProfile(ref applied);
        }

        private static void DestroyProfile(ref CharacterJumpProfile profile)
        {
            if (profile != null) UObject.DestroyImmediate(profile);
            profile = null;
        }

        public void Dispose()
        {
            trace.Dispose();
            ReleaseCandidate();
            DestroyProfile(ref draft);
            source = null;
            actor = null;
            motor = null;
        }
    }
}
