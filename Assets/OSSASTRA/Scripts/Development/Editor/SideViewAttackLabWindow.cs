// 横版攻击调试台。管理试验条件、动作参数、招式效果和连段行为候选，实际运动及判定由正式链推进。
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.Characters;
using Ossastra.Combat;
using Ossastra.UserInterface;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Development.Editor
{
    public sealed class SideViewAttackLabWindow : EditorWindow
    {
        private const string OwnStart = "Ossastra.AttackLab.OwnStart";
        private const string OldStart = "Ossastra.AttackLab.OldStart";
        private static readonly string[] Conditions = {"空挥", "固定位置受击", "正常受击", "格挡木桩", "致死靶", "双目标", "可动轻靶", "可动重靶", "免推靶"};
        private static readonly string[] Stations = {"平地", "平台边缘", "墙边", "低顶", "台边下落", "翻越高台"};
        [Serializable] private sealed class Condition
        {
            public int mode, station;
            public float distance, height;
            public bool right, enemyAttacks;
            public string actor, weapon, mainProfile, observedSkill;
            public int observedSlot;
        }

        private AttackLabSession session;
        private AttackLabTuningPanel tuning;
        private AttackLabHistory history;
        private CharacterInputHandler captured;
        private CharacterControlCoordinator capturedParty;
        private string note;
        private bool candidate, showBindings, windowActive;
        private readonly object captureOwner = new object();
        private double releaseAt, lastDraw;
        private int tab, rememberedTrial = -1;
        private CombatFeedbackView swingView;
        private bool previousSwingSuppression;
        private Vector2 scroll;
        private AttackLabAnimationCalibration calibration;
        private AttackLabFeedbackPanel feedback;
        private AttackLabMovementPanel movement;
        private AttackLabImpactPanel impact;
        private AttackLabBehaviorPanel behavior;
        private int impactMode;
        private ActorRoot sceneMovementActor;
        private int workMode;

        [MenuItem("OSSASTRA/战斗/横版攻击调试台")]
        [MenuItem("OSSASTRA/战斗/横版角色手感调试台")]
        public static void Open()
        {
            var window = GetWindow<SideViewAttackLabWindow>("横版角色手感调试台");
            window.minSize = new Vector2(410, 400);
            window.Show();
        }

        private void OnEnable()
        {
            windowActive = true;
            tuning = new AttackLabTuningPanel();
            tuning.RequestReset = StartTrial;
            tuning.RestoreCondition = RestoreCondition;
            history = new AttackLabHistory();
            EditorApplication.update += TickWindow;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private void OnDisable()
        {
            behavior?.Dispose();
            behavior = null;
            impact?.Dispose();
            impact = null;
            calibration?.Dispose();
            feedback?.Dispose();
            feedback = null;
            movement?.Dispose();
            movement = null;
            windowActive = false;
            EditorApplication.update -= TickWindow;
            EditorApplication.playModeStateChanged -= ModeChanged;
            ReleaseInput();
            RestoreSwing();
            tuning?.Dispose();
            if (session != null && Application.isPlaying && session.Trace != null)
                session.Trace.EndTrial();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RestoreStartScene();
        }

        private void OnDestroy() { windowActive = false; ReleaseInput(); }

        private void OnFocus() { releaseAt = 0; if (session != null) CaptureInput(); }
        private void OnLostFocus() { releaseAt = EditorApplication.timeSinceStartup + .06; }

        private void CaptureInput()
        {
            if (SideViewAttackLabAcceptance.IsRunning)
            {
                ReleaseInput();
                return;
            }
            if (!windowActive || !Application.isPlaying || session == null || session.Actor == null) return;
            var input = session.Actor.GetComponent<CharacterInputHandler>();
            if (captured == input && capturedParty == session.Party) return;
            ReleaseInput();
            captured = input;
            capturedParty = session.Party;
            if (captured != null) captured.SetInventoryCapture(captureOwner, true, true);
            if (capturedParty != null) capturedParty.SetInventoryCapture(captureOwner, true, true);
        }

        private void ReleaseInput()
        {
            if (captured != null) captured.SetInventoryCapture(captureOwner, false);
            if (capturedParty != null) capturedParty.SetInventoryCapture(captureOwner, false, true);
            captured = null;
            capturedParty = null;
        }

        private void TickWindow()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (session == null && SceneManager.GetActiveScene().path == AttackLabSceneBuilder.ScenePath)
            {
                session = FindObjectOfType<AttackLabSession>();
                if (session != null)
                {
                    history = new AttackLabHistory();
                    rememberedTrial = -1;
                    swingView = FindObjectOfType<CombatFeedbackView>();
                    if (swingView != null) previousSwingSuppression = swingView.SuppressLabSwing;
                }
            }
            behavior?.Observe(session);
            if (behavior?.Previewing != true) impact?.Observe(session);
            if (session == null) return;
            movement?.Observe(session);
            feedback?.Observe(session);
            if (focusedWindow == this) CaptureInput();
            else if ((captured != null || capturedParty != null) && EditorApplication.timeSinceStartup >= releaseAt &&
                     (Mouse.current == null || !Mouse.current.leftButton.isPressed)) ReleaseInput();
            if (session.Actor != null && session.Generation > 0 && rememberedTrial != session.Generation)
            {
                if (session.Trace != null && !session.Trace.Recording) session.Trace.BeginTrial(session.Actor, session.Generation, session.Version);
                tuning.ObservedSkillSlot = session.SkillSlot;
                tuning.Bind(session.Actor);
                history.Remember(session, CurrentCondition(), tuning.Summary);
                rememberedTrial = session.Generation;
            }
            if (EditorApplication.timeSinceStartup - lastDraw > .12)
            {
                lastDraw = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void RestoreSwing()
        {
            if (swingView != null) swingView.SuppressLabSwing = previousSwingSuppression;
            swingView = null;
        }

        private void ModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                behavior?.Dispose();
                behavior = null;
                impact?.Dispose();
                impact = null;
                ReleaseInput();
                RestoreSwing();
                feedback?.Dispose();
                feedback = null;
                movement?.Dispose();
                movement = null;
                tuning?.Dispose();
                tuning = new AttackLabTuningPanel { RequestReset = StartTrial, RestoreCondition = RestoreCondition };
                session = null;
                candidate = false;
            }
            if (change == PlayModeStateChange.EnteredEditMode) RestoreStartScene();
            Repaint();
        }

        [InitializeOnLoadMethod]
        private static void InstallStartSceneRestore()
        {
            EditorApplication.playModeStateChanged -= RestoreAfterTransition;
            EditorApplication.playModeStateChanged += RestoreAfterTransition;
        }

        private static void RestoreAfterTransition(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
                RestoreStartScene();
        }

        internal static void RestoreStartScene()
        {
            if (!SessionState.GetBool(OwnStart, false)) return;
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == AttackLabSceneBuilder.ScenePath)
            {
                var old = SessionState.GetString(OldStart, "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(old) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(old);
            }
            SessionState.EraseBool(OwnStart);
            SessionState.EraseString(OldStart);
        }

        // Play 启动场只在本次试玩持有，停止时返回作者原有编辑现场。
        internal static bool Launch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !AttackLabSceneBuilder.EnsureScene()) return false;
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(AttackLabSceneBuilder.ScenePath);
            if (asset == null) return false;
            SessionState.SetString(OldStart, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(OwnStart, true);
            EditorSceneManager.playModeStartScene = asset;
            EditorApplication.isPlaying = true;
            return true;
        }

        private void OnGUI()
        {
            workMode = GUILayout.Toolbar(workMode, new[] { "试玩与对照", "动画与事件", "命中与受击", "移动与翻越", "运动接触", "招式效果" });
            if (workMode == 1)
            {
                calibration ??= new AttackLabAnimationCalibration();
                calibration.Draw(session);
                return;
            }
            if (workMode == 2 || workMode == 4)
            {
                feedback ??= new AttackLabFeedbackPanel();
                feedback.Draw(session, workMode == 4, () => SendCommand(false));
                return;
            }
            if (workMode == 3)
            {
                movement ??= new AttackLabMovementPanel();
                if (session == null) DrawSceneMovement();
                else
                {
                    DrawActors();
                    movement.Draw(session);
                }
                return;
            }
            if (session == null || !Application.isPlaying || session.Actor == null)
            {
                EditorGUILayout.LabelField("横版攻击调试台", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(Application.isPlaying ? "等待独立测试场和角色就绪。若正在试玩其他场景，请先结束该次试玩。" :
                    "从平地开始。可在 Game 中用正式按键出招，再回到这里看攻击记录、比较候选。", MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("进入独立测试场并试玩", GUILayout.Height(32)) && !Launch()) note = "测试场尚未就绪，请查看 Console。";
                if (!string.IsNullOrEmpty(note)) EditorGUILayout.HelpBox(note, MessageType.Info);
                return;
            }

            if (workMode == 5)
            {
                DrawActors();
                DrawLoadout();
                session.Condition = (AttackLabTargetMode)EditorGUILayout.Popup("目标条件", (int)session.Condition, Conditions);
                session.Station = (AttackLabStation)EditorGUILayout.Popup("场地", (int)session.Station, Stations);
                impactMode = GUILayout.Toolbar(impactMode, new[] { "单个招式效果", "芙蕾链匕首连段" });
                if (impactMode == 0)
                {
                    behavior?.Dispose();
                    behavior = null;
                    impact ??= new AttackLabImpactPanel();
                    impact.Draw(session, ResetImpactTrial);
                }
                else
                {
                    impact?.ClearOverride();
                    behavior ??= new AttackLabBehaviorPanel();
                    behavior.Draw(session, ResetImpactTrial);
                }
                if (!string.IsNullOrEmpty(note)) EditorGUILayout.HelpBox(note, MessageType.Info);
                return;
            }
            GUILayout.Label(session.Actor.ActorId + "   /   " + session.WeaponLabel, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("试验 " + session.Generation + "   ·   " + session.Version + "   ·   " + session.SkillLabel, EditorStyles.wordWrappedMiniLabel);
            tab = GUILayout.Toolbar(tab, new[] {"试玩与记录", "参数与预设"});
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawPlay();
            else
            {
                tuning.Bind(session.Actor);
                tuning.ConditionJson = CurrentCondition();
                tuning.Draw();
            }
            if (!string.IsNullOrEmpty(note)) EditorGUILayout.HelpBox(note, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneMovement()
        {
            var scene = SceneManager.GetActiveScene();
            var party = FindObjectsOfType<CharacterControlCoordinator>()
                .FirstOrDefault(item => item.gameObject.scene == scene && item.Characters.Count != 0);
            IEnumerable<ActorRoot> roster = party != null ? party.Characters : FindObjectsOfType<ActorRoot>();
            var actors = roster.Where(item => item != null && item.gameObject.scene == scene && item.Definition != null &&
                    item.GetComponent<CharacterMovement>()?.AuthoredJumpProfile != null)
                .OrderByDescending(item => party != null ? item == party.Current : item.HasPlayerInput)
                .ThenBy(item => item.ActorId, StringComparer.Ordinal)
                .ToArray();

            if (actors.Length > 0)
            {
                int selected = Mathf.Max(0, Array.IndexOf(actors, sceneMovementActor));
                var names = actors.Select(item => string.IsNullOrEmpty(item.Definition.displayName)
                    ? item.name : item.Definition.displayName).ToArray();
                selected = EditorGUILayout.Popup("本场横版角色", selected, names);
                sceneMovementActor = actors[selected];
            }
            else sceneMovementActor = null;

            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                GUILayout.Button("进入独立测试场对照轨迹") && !Launch())
                note = "测试场尚未就绪，请查看 Console。";
            if (!string.IsNullOrEmpty(note)) EditorGUILayout.HelpBox(note, MessageType.Info);
            movement.Draw(null, sceneMovementActor);
        }

        private void DrawActors()
        {
            var actors = session.Party?.Characters;
            if (actors != null)
            {
                var labels = actors.Select(a => a == null ? "未装配" : a.ActorId).ToArray();
                int current = Array.IndexOf(actors.ToArray(), session.Actor);
                int next = GUILayout.Toolbar(current, labels);
                if (next != current && next >= 0)
                {
                    ReleaseInput();
                    if (session.SwitchActor(next)) { candidate = false; tuning.ObservedSkillSlot = session.SkillSlot; tuning.Bind(session.Actor); note = session.Status; }
                    else note = session.Status;
                }
            }
        }

        private void DrawPlay()
        {
            DrawActors();
            DrawLoadout();
            EditorGUI.BeginChangeCheck();
            session.Condition = (AttackLabTargetMode)EditorGUILayout.Popup("目标条件", (int)session.Condition, Conditions);
            session.Station = (AttackLabStation)EditorGUILayout.Popup("场地", (int)session.Station, Stations);
            session.Distance = EditorGUILayout.Slider("目标水平距离", session.Distance, .25f, 12f);
            session.Height = EditorGUILayout.Slider("目标相对高度", session.Height, -2f, 4f);
            session.FacingRight = GUILayout.Toolbar(session.FacingRight ? 1 : 0, new[]{"面向左", "面向右"}) == 1;
            session.EnemyAttacks = EditorGUILayout.Toggle("允许练习敌人反击", session.EnemyAttacks);
            if (EditorGUI.EndChangeCheck()) note = "条件已选。点「按此条件复位」开始新试验。";
            if (GUILayout.Button("按此条件复位", GUILayout.Height(25))) StartTrial(candidate);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("请求普攻")) SendCommand(false);
                if (GUILayout.Button("请求技能")) SendCommand(true);
            }
            showBindings = EditorGUILayout.Foldout(showBindings, "正式按键与当前状态", true);
            if (showBindings) DrawState();
            var trace = session.Trace;
            if (trace == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                trace.ShowGeometry = GUILayout.Toggle(trace.ShowGeometry,"实际判定","Button");
                if (swingView != null) swingView.SuppressLabSwing = !GUILayout.Toggle(!swingView.SuppressLabSwing,"挥击刀光","Button");
                trace.ShowTrail = GUILayout.Toggle(trace.ShowTrail,"移动轨迹","Button");
                trace.ShowScale = GUILayout.Toggle(trace.ShowScale,"空间标尺","Button");
                trace.Frozen = GUILayout.Toggle(trace.Frozen,"冻结记录","Button");
            }
            trace.Detailed = EditorGUILayout.Toggle("详细事件记录", trace.Detailed);
            history.Draw(trace, session.Generation);
        }

        // 临时释放本窗口的输入捕获，正式仲裁仍检查所有其他持有者。
        private void SendCommand(bool skill)
        {
            ReleaseInput();
            try
            {
                if (skill) session.RequestSkill(); else session.RequestAttack();
                note = session.Status;
            }
            finally { if (focusedWindow == this) CaptureInput(); }
        }

        private void DrawLoadout()
        {
            var actor = session.Actor;
            var equipment = actor.GetComponent<CharacterEquipment>();
            var right = equipment?.RightDefinition;
            var main = actor.GetComponent<CharacterMainAction>();
            EditorGUILayout.LabelField("当前主手", right ? right.displayName + "  ·  " + right.name : "未装配");
            var choices = main && main.isActiveAndEnabled && right
                ? main.Choices.Where(c => c).ToArray() : Array.Empty<MainActionProfileSO>();
            if (choices.Length > 0)
            {
                var names = new[] { "角色默认普攻" }.Concat(choices.Select(c => c.displayName)).ToArray();
                int selected = Array.FindIndex(choices, c => c.definitionId == main.SelectedId) + 1;
                int next = EditorGUILayout.Popup("主手动作组", selected, names);
                if (next != selected)
                {
                    // 验证脱战、控制权和当前装备许可仍走正式入口。
                    if (main.Configure(next == 0 ? "" : choices[next - 1].definitionId))
                    {
                        actor.StateArbitrator.Combat.RebindLaboratorySequence();
                        BeginLoadoutTrial();
                    }
                    else note = main.LastResult;
                }
            }
            else EditorGUILayout.LabelField("主手动作组", "角色默认普攻");
            var abilities = actor.GetComponent<CharacterAbilities>();
            if (!abilities || !abilities.isActiveAndEnabled || !right || equipment.State == null)
            {
                EditorGUILayout.LabelField("当前主手没有可观察的正式技能槽。", EditorStyles.miniLabel);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("观察与试放技能");
                for (int i = 0; i < 3; i++)
                {
                    var skill = abilities.SkillDefinition(i);
                    var key = "QER"[i];
                    var label = new GUIContent(key + " · " + (skill ? skill.displayName : "空槽"),
                        skill ? skill.name + "  ·  " + abilities.SkillAvailability(i) : "当前主手未装配");
                    using (new EditorGUI.DisabledScope(!skill || actor.ActionActive || abilities.IsBusy))
                        if (GUILayout.Toggle(i == session.SkillSlot, label, EditorStyles.miniButton) && i != session.SkillSlot && session.SelectSkillSlot(i))
                            BeginLoadoutTrial();
                }
            }
        }

        private void BeginLoadoutTrial()
        {
            tuning.ObservedSkillSlot = session.SkillSlot;
            tuning.Bind(session.Actor);
            StartTrial(false);
        }

        private void DrawState()
        {
            var actor = session.Actor;
            var bus = actor.DataBus;
            var input = actor.GetComponent<CharacterInputHandler>();
            if (input != null)
            {
                EditorGUILayout.LabelField("普攻 " + input.rightHandAction.GetBindingDisplayString() + "   跳跃 " + input.jumpAction.GetBindingDisplayString(), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("输入源 " + input.InputSources.AimSource + "   ·   面板捕获 " + (input.InventoryCaptured ? "是" : "否"), EditorStyles.miniLabel);
            }
            if (bus == null) return;
            var posture = actor.GetComponent<CombatPosture>();
            EditorGUILayout.LabelField("移动 " + actor.StateArbitrator?.Movement?.CurrentStateName + "   ·   垂直速度 " + bus.VerticalSpeed.ToString("F2") + "   ·   可再次攻击 " + bus.AllowsAttack);
            EditorGUILayout.LabelField("瞄准 " + bus.AttackDirection.ToString("F2") + "   ·   显示朝向 " + bus.VisualFacingX + "   ·   " + bus.DirectionReason, EditorStyles.wordWrappedMiniLabel);
            if (posture != null) EditorGUILayout.LabelField("架势 " + posture.Current.ToString("0.#") + " / " + posture.Maximum.ToString("0.#") + "   ·   恢复 " + actor.LastPostureRecoveryEvent, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("冻结记录只停止采集。", EditorStyles.wordWrappedMiniLabel);
        }

        private string CurrentCondition() => JsonUtility.ToJson(new Condition { mode=(int)session.Condition, station=(int)session.Station,
            distance=session.Distance, height=session.Height, right=session.FacingRight, enemyAttacks=session.EnemyAttacks, actor=session.Actor.ActorId,
            weapon=session.WeaponLabel, mainProfile=session.Actor.GetComponent<CharacterMainAction>()?.SelectedId,
            observedSlot=session.SkillSlot, observedSkill=session.SkillLabel });

        private void RestoreCondition(string json)
        {
            if (session == null || string.IsNullOrEmpty(json)) return;
            var value = JsonUtility.FromJson<Condition>(json);
            if (value == null) return;
            session.Condition = (AttackLabTargetMode)value.mode;
            session.Station = (AttackLabStation)value.station;
            session.Distance = value.distance;
            session.Height = value.height;
            session.FacingRight = value.right;
            session.EnemyAttacks = value.enemyAttacks;
            if (!string.IsNullOrEmpty(value.observedSkill) && session.SelectSkillSlot(value.observedSlot))
                tuning.ObservedSkillSlot = session.SkillSlot;
        }

        private bool ResetImpactTrial(string version)
        {
            if (!session.ResetTrial(version)) { note = session.Status; return false; }
            // 此试打只归还手感页的运行覆盖。手感候选的版本检查留给该页写入和应用。
            tuning.ReleasePreviewForImpactTrial();
            candidate = false;
            note = null;
            return true;
        }

        private void StartTrial(bool useCandidate)
        {
            if (session == null || !tuning.CanApply(useCandidate, out note)) return;
            string version = tuning.VersionFor(useCandidate);
            if (!session.ResetTrial(version)) { note = session.Status; return; }
            if (!tuning.ApplyAfterReset(session.Actor, useCandidate, out note))
            {
                // 已清场但配置未切换，避免把失败的配置版本标在下一次攻击上。
                session.ResetTrial(tuning.VersionFor(false));
                tuning.ApplyAfterReset(session.Actor, false, out _);
                candidate = false;
                return;
            }
            candidate = useCandidate;
            history.Remember(session, CurrentCondition(), tuning.Summary);
            rememberedTrial = session.Generation;
            note = "已开始试验 " + session.Generation + " · " + version;
        }
    }
}
