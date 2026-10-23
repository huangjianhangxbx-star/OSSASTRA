using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ANKALUTEDevDebugActions
{
    private static IDisposable runtimeRegistration;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRuntimeActions()
    {
        runtimeRegistration?.Dispose();
        runtimeRegistration = RegisterInto(
            DevDebugHubRuntime.Registry,
            ResolveBattleLauncher,
            ResolveGameMenuRouter,
            DescribeContext,
            ResolvePreparedAction,
            ResolveBattleSession,
            ResolvePresentationRoot);
    }
#endif

    public static IDisposable RegisterInto(
        DevDebugRegistry registry,
        Func<DRPGBattleDebugLauncher> battleLauncherResolver,
        Func<GameMenuInputRouter> gameMenuRouterResolver,
        Func<string> contextResolver,
        Func<DRPGBattlePreparedAction> preparedActionResolver = null,
        Func<DRPGBattleSession> battleSessionResolver = null,
        Func<DRPGBattlePresentationRoot> presentationResolver = null)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));
        if (battleLauncherResolver == null)
            throw new ArgumentNullException(nameof(battleLauncherResolver));
        if (gameMenuRouterResolver == null)
            throw new ArgumentNullException(nameof(gameMenuRouterResolver));
        if (contextResolver == null)
            throw new ArgumentNullException(nameof(contextResolver));
        if (preparedActionResolver == null)
            preparedActionResolver = () => null;
        if (battleSessionResolver == null)
            battleSessionResolver = () => null;
        if (presentationResolver == null)
            presentationResolver = () => null;

        var registrations = new List<IDisposable>
        {
            registry.Register(CreateStandardBattleAction(battleLauncherResolver)),
            registry.Register(CreateDefeatBattleAction(battleLauncherResolver)),
            registry.Register(CreateBasicOnlyStartAction(battleLauncherResolver, false)),
            registry.Register(CreateBasicOnlyStartAction(battleLauncherResolver, true)),
            registry.Register(CreateBasicOnlySessionAction(battleSessionResolver, "reset")),
            registry.Register(CreateBasicOnlySessionAction(battleSessionResolver, "disable")),
            registry.Register(CreateBasicOnlySessionAction(battleSessionResolver, "inspect")),
            registry.Register(CreateForceOutcomeAction(
                battleSessionResolver,
                DRPGBattleOutcome.Victory)),
            registry.Register(CreateForceOutcomeAction(
                battleSessionResolver,
                DRPGBattleOutcome.Defeat)),
            registry.Register(CreateEntryReplayAction(presentationResolver)),
            registry.Register(CreateHordeDeployAction(battleSessionResolver)),
            registry.Register(CreateCharacterMenuAction(gameMenuRouterResolver)),
            registry.Register(CreateContextAction(contextResolver)),
            registry.Register(CreateActionSpecReportAction(preparedActionResolver))
        };
        return new RegistrationGroup(registrations);
    }

    private static IDevDebugAction CreateBasicOnlyStartAction(
        Func<DRPGBattleDebugLauncher> resolver, bool invalid)
    {
        return new DelegateDevDebugAction(
            id: "battle.basic_only." + (invalid ? "invalid" : "start"),
            category: "平A验收",
            title: invalid ? "检查非法验收请求" : "开始单场平A验收",
            summary: invalid ? "验证种子不一致的请求在创建会话前被拒绝。" : "固定种子 42，通过正式入口限制本场敌方仅使用平A。",
            details: invalid ? "只检查请求边界，不创建战斗或取得事务所有权。" :
                "要求已绑定的 Standard 遭遇和合法普通攻击；不会开启资产开关或修改敌人技能表。退出本场后普通 AI 恢复。当前资产未就绪时明确拒绝，不回退旧流程。",
            order: invalid ? 20 : 10,
            closeHubOnSuccess: !invalid,
            evaluate: () => RequireBattleLauncher(resolver),
            execute: () =>
            {
                var launcher = resolver();
                if (launcher == null) return DevDebugActionResult.Unavailable("缺少 DRPGBattleDebugLauncher。");
                if (invalid) return launcher.VerifyInvalidAcceptanceBoundaryForDevelopment()
                    ? DevDebugActionResult.Succeeded("非法种子请求已在创建会话前拒绝；未修改战斗。")
                    : DevDebugActionResult.Failed("非法验收请求未被拒绝。");
                var result = launcher.StartBasicAttackAcceptanceForDevelopment();
                return result == DRPGBattleStartResult.Started
                    ? DevDebugActionResult.Succeeded("已请求正式单场平A验收，seed=42，scenario=normal-hit。")
                    : DevDebugActionResult.Unavailable("平A验收未启动：" + result + "。需要就绪的 Standard 遭遇及空闲远征状态。");
            });
    }

    private static IDevDebugAction CreateBasicOnlySessionAction(
        Func<DRPGBattleSession> resolver, string operation)
    {
        bool inspect = operation == "inspect";
        return new DelegateDevDebugAction(
            id: "battle.basic_only." + operation,
            category: "平A验收",
            title: inspect ? "查看平A验收状态" : operation == "reset" ? "清空本场验收" : "关闭本场验收模式",
            summary: inspect ? "只读显示种子、场景、行动阶段和事务锁。" : "通过正式 Abort 收尾本场验收，不影响普通战斗。",
            details: inspect ? "不会推进回合或改变目标。普通战斗显示验收关闭。" :
                "不会在行动中途更换 AI 策略；请求整场退出并等待正式清理。清理后可重新启动验收或普通战斗，不自动新建下一场。",
            order: inspect ? 50 : operation == "reset" ? 30 : 40,
            closeHubOnSuccess: false,
            evaluate: () => resolver() != null && (inspect || resolver().AcceptanceOptions.Enabled)
                ? DevDebugAvailability.Available()
                : DevDebugAvailability.Unavailable("当前没有可用的" + (inspect ? "战斗。" : "平A验收会话。")),
            execute: () =>
            {
                var session = resolver();
                if (session == null) return DevDebugActionResult.Unavailable("当前没有战斗。");
                var options = session.AcceptanceOptions;
                if (inspect) return DevDebugActionResult.Succeeded(
                    "BasicAttackOnly=" + options.Enabled + "; seed=" + options.BattleSeed +
                    "; scenario=" + options.ScenarioId + "; phase=" + session.Phase +
                    "; stage=" + session.ActionStage + "; owner=" + session.TransactionArbiter.CurrentOwner +
                    "; trace entries=" + session.TransactionTrace.Count);
                if (!options.Enabled || !session.IsActive)
                    return DevDebugActionResult.Unavailable("仅允许结束正在运行的平A验收，不修改普通战斗。");
                session.Abort();
                return DevDebugActionResult.Succeeded("已请求验收收尾；清理完成后本场选项失效。不会自动开始下一场。");
            });
    }

    private static IDevDebugAction CreateEntryReplayAction(
        Func<DRPGBattlePresentationRoot> resolver)
    {
        return new DelegateDevDebugAction(
            id: "battle.presentation_entry",
            category: "战斗演出",
            title: "重播战斗入场演出",
            summary: "重新播放怪物生成、镜头归位与 HUD 入场。",
            details: "不会重建战斗或修改数值，只重播当前战斗的固定入场表现，用于快速检查镜头、生成动效、角色缩放与 HUD 节奏。",
            order: 10,
            closeHubOnSuccess: true,
            evaluate: () => resolver() != null
                ? DevDebugAvailability.Available()
                : DevDebugAvailability.Unavailable("当前没有可用的战斗演出根节点。"),
            execute: () => resolver() != null &&
                resolver().ReplayEntryForDevelopment()
                    ? DevDebugActionResult.Succeeded("已重播战斗入场演出。")
                    : DevDebugActionResult.Unavailable("当前战斗尚未进入可重播状态。"));
    }

    private static IDevDebugAction CreateHordeDeployAction(
        Func<DRPGBattleSession> resolver)
    {
        return new DelegateDevDebugAction(
            id: "battle.horde_deploy",
            category: "战斗演出",
            title: "部署怪海至当前上限",
            summary: "把后备敌人立即投入战场，压力测试多排空间布局。",
            details: "遵守当前 FormationRules 的同屏硬上限与每排容量，不绕过正式队形规则；用于验证 6～15 敌人的透视缩放、遮挡、点击代理和镜头可读性。",
            order: 20,
            closeHubOnSuccess: true,
            evaluate: () => RequireActiveBattle(resolver),
            execute: () =>
            {
                DRPGBattleSession session = resolver();
                if (session == null || !session.IsActive)
                    return DevDebugActionResult.Unavailable("当前没有正在进行的战斗。");
                if (!session.TryForceDeployAllDevelopment(
                        out int deployed,
                        out string error))
                {
                    return DevDebugActionResult.Failed(error);
                }
                return DevDebugActionResult.Succeeded(
                    deployed > 0
                        ? $"已立即部署 {deployed} 名后备敌人。"
                        : "当前已达到同屏上限，或没有后备敌人。");
            });
    }

    private static IDevDebugAction CreateForceOutcomeAction(
        Func<DRPGBattleSession> resolver,
        DRPGBattleOutcome outcome)
    {
        bool victory = outcome == DRPGBattleOutcome.Victory;
        return new DelegateDevDebugAction(
            id: victory ? "battle.force_victory" : "battle.force_defeat",
            category: "战斗",
            title: victory ? "立即判定玩家胜利" : "立即判定玩家失败",
            summary: victory
                ? "直接进入胜利结算，用于验证奖励与战后流程。"
                : "直接进入失败流程，用于验证 Game Over 与标题返回。",
            details: victory
                ? "只在回合制战斗进行中可用。通过 DRPGBattleSession 的正式 BattleEnded 事件判定胜利，因此会完整经过经验、掉落和结算界面。"
                : "只在回合制战斗进行中可用。通过 DRPGBattleSession 的正式 BattleEnded 事件判定失败，因此会完整经过失败收尾并返回标题。",
            order: victory ? 30 : 31,
            closeHubOnSuccess: true,
            evaluate: () => RequireActiveBattle(resolver),
            execute: () => ForceOutcome(resolver, outcome));
    }

    private static DevDebugAvailability RequireActiveBattle(
        Func<DRPGBattleSession> resolver)
    {
        DRPGBattleSession session = resolver();
        return session != null && session.IsActive
            ? DevDebugAvailability.Available()
            : DevDebugAvailability.Unavailable("当前没有正在进行的回合制战斗。");
    }

    private static DevDebugActionResult ForceOutcome(
        Func<DRPGBattleSession> resolver,
        DRPGBattleOutcome outcome)
    {
        DRPGBattleSession session = resolver();
        if (session == null || !session.IsActive)
            return DevDebugActionResult.Unavailable(
                "当前没有正在进行的回合制战斗。");

        return session.TryForceDevelopmentOutcome(outcome, out string error)
            ? DevDebugActionResult.Succeeded(
                outcome == DRPGBattleOutcome.Victory
                    ? "已强制判定玩家胜利。"
                    : "已强制判定玩家失败。")
            : DevDebugActionResult.Failed(error);
    }

    private static IDevDebugAction CreateActionSpecReportAction(
        Func<DRPGBattlePreparedAction> resolver)
    {
        return new DelegateDevDebugAction(
            id: "battle.action_spec",
            category: "战斗诊断",
            title: "输出最近行动规格",
            summary: "查看最近一次已准备行动的最终不可变规格与全部修正来源。",
            details:
                "读取当前战斗正在执行或最近完成准备的行动，输出技能、消耗、效果、" +
                "站位范围，以及武器、附件、技能卡、饰品、成长被动、词条和状态的" +
                "逐项应用/跳过记录。该操作只读，不会改变战斗状态。",
            order: 90,
            closeHubOnSuccess: false,
            evaluate: () => RequirePreparedAction(resolver),
            execute: () => ReportPreparedAction(resolver));
    }

    private static DevDebugAvailability RequirePreparedAction(
        Func<DRPGBattlePreparedAction> resolver)
    {
        DRPGBattlePreparedAction action = resolver();
        return action != null && action.Spec != null
            ? DevDebugAvailability.Available()
            : DevDebugAvailability.Unavailable(
                "当前战斗还没有已准备或最近行动；请先执行一次战斗指令。");
    }

    private static DevDebugActionResult ReportPreparedAction(
        Func<DRPGBattlePreparedAction> resolver)
    {
        DRPGBattlePreparedAction action = resolver();
        if (action == null || action.Spec == null)
        {
            return DevDebugActionResult.Unavailable(
                "当前战斗还没有已准备或最近行动；请先执行一次战斗指令。");
        }

        string report = DRPGActionSpecTraceFormatter.Format(
            action.Spec,
            action.ActionId);
        Debug.Log("[DRPG Action Spec]\n" + report);
        return DevDebugActionResult.Succeeded(report);
    }

    private static IDevDebugAction CreateStandardBattleAction(
        Func<DRPGBattleDebugLauncher> resolver)
    {
        return new DelegateDevDebugAction(
            id: "battle.standard",
            category: "战斗",
            title: "开始标准测试战斗",
            summary: "进入当前配置的 DRPG 标准遭遇。",
            details:
                "调用场景中 DRPGBattleDebugLauncher.StartTestBattle()，" +
                "用于验证完整战斗闭环、行动条、指令与结算。等价于现有 F8 入口。",
            order: 10,
            closeHubOnSuccess: true,
            evaluate: () => RequireBattleLauncher(resolver),
            execute: () => StartBattle(resolver, false));
    }

    private static IDevDebugAction CreateDefeatBattleAction(
        Func<DRPGBattleDebugLauncher> resolver)
    {
        return new DelegateDevDebugAction(
            id: "battle.defeat",
            category: "战斗",
            title: "开始战败 / 大破压力测试",
            summary: "进入高压敌人配置，验证受击、大破与战败。",
            details:
                "调用场景中 DRPGBattleDebugLauncher.StartDefeatTestBattle()，" +
                "用于连续观察随机受击、角色大破、HP 归零与 Game Over 流程。" +
                "等价于现有 F9 入口。",
            order: 20,
            closeHubOnSuccess: true,
            evaluate: () => RequireBattleLauncher(resolver),
            execute: () => StartBattle(resolver, true));
    }

    private static IDevDebugAction CreateCharacterMenuAction(
        Func<GameMenuInputRouter> resolver)
    {
        return new DelegateDevDebugAction(
            id: "menu.character",
            category: "菜单",
            title: "打开角色总控菜单",
            summary: "打开状态、成长、技能树、技能配置与装备菜单。",
            details:
                "调用 GameMenuInputRouter.Open()，验证总控菜单能否获取模态所有权、" +
                "阻止角色输入并正确读取成长及装备运行时数据。",
            order: 10,
            closeHubOnSuccess: true,
            evaluate: () => RequireGameMenuRouter(resolver),
            execute: () => OpenCharacterMenu(resolver));
    }

    private static IDevDebugAction CreateContextAction(Func<string> resolver)
    {
        return new DelegateDevDebugAction(
            id: "system.context",
            category: "系统",
            title: "输出当前开发上下文",
            summary: "检查场景、GameFlow、GameUI 与模态占用状态。",
            details:
                "不会修改游戏状态。把当前场景、PlayMode、GameFlowRoot、GameUIRoot、" +
                "DRPGBattleDebugLauncher 与模态所有者摘要显示在结果栏并写入 Console。",
            order: 10,
            closeHubOnSuccess: false,
            evaluate: DevDebugAvailability.Available,
            execute: () => ReportContext(resolver));
    }

    private static DevDebugAvailability RequireBattleLauncher(
        Func<DRPGBattleDebugLauncher> resolver)
    {
        return resolver() != null
            ? DevDebugAvailability.Available()
            : DevDebugAvailability.Unavailable(
                "当前场景缺少 DRPGBattleDebugLauncher。请载入测试场景。 ");
    }

    private static DevDebugAvailability RequireGameMenuRouter(
        Func<GameMenuInputRouter> resolver)
    {
        GameMenuInputRouter router = resolver();
        if (router == null)
        {
            return DevDebugAvailability.Unavailable(
                "当前场景缺少 GameMenuInputRouter / GameUIRoot。");
        }

        return DevDebugAvailability.Available();
    }

    private static DevDebugActionResult StartBattle(
        Func<DRPGBattleDebugLauncher> resolver,
        bool defeatScenario)
    {
        DRPGBattleDebugLauncher launcher = resolver();
        if (launcher == null)
            return DevDebugActionResult.Unavailable(
                "当前场景缺少 DRPGBattleDebugLauncher。");

        DRPGBattleStartResult result = defeatScenario
            ? launcher.StartDefeatTestBattle()
            : launcher.StartTestBattle();
        return result == DRPGBattleStartResult.Started
            ? DevDebugActionResult.Succeeded(
                defeatScenario
                    ? "已启动战败 / 大破压力测试。"
                    : "已启动标准 DRPG 测试战斗。")
            : DevDebugActionResult.Failed("战斗启动失败：" + result);
    }

    private static DevDebugActionResult OpenCharacterMenu(
        Func<GameMenuInputRouter> resolver)
    {
        GameMenuInputRouter router = resolver();
        if (router == null)
            return DevDebugActionResult.Unavailable(
                "当前场景缺少 GameMenuInputRouter / GameUIRoot。");

        router.Open();
        GameUIRoot uiRoot = GameUIRoot.Instance;
        if (uiRoot != null && !uiRoot.ModalCoordinator.IsOwnedBy(router))
        {
            return DevDebugActionResult.Failed(
                "总控菜单未能取得模态 UI 所有权。");
        }

        return DevDebugActionResult.Succeeded("已打开角色总控菜单。");
    }

    private static DevDebugActionResult ReportContext(Func<string> resolver)
    {
        string context = resolver() ?? string.Empty;
        Debug.Log("[DevDebugHub] " + context);
        return DevDebugActionResult.Succeeded(context);
    }

    private static DRPGBattleDebugLauncher ResolveBattleLauncher()
    {
        return UnityEngine.Object.FindFirstObjectByType<DRPGBattleDebugLauncher>();
    }

    private static GameMenuInputRouter ResolveGameMenuRouter()
    {
        GameUIRoot root = GameUIRoot.Instance;
        if (root == null)
            root = UnityEngine.Object.FindFirstObjectByType<GameUIRoot>();
        return root != null ? root.GetComponent<GameMenuInputRouter>() : null;
    }

    private static DRPGBattlePreparedAction ResolvePreparedAction()
    {
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root == null)
            root = UnityEngine.Object.FindFirstObjectByType<GameFlowRoot>();

        DRPGBattleFlowRoot flow = root != null ? root.drpgBattleFlow : null;
        if (flow == null)
            flow = UnityEngine.Object.FindFirstObjectByType<DRPGBattleFlowRoot>();

        DRPGBattleSession session = flow != null ? flow.CurrentSession : null;
        if (session == null)
            return null;
        return session.ActiveAction ?? session.LastPreparedAction;
    }

    private static DRPGBattleSession ResolveBattleSession()
    {
        GameFlowRoot root = GameFlowRoot.Instance;
        if (root == null)
            root = UnityEngine.Object.FindFirstObjectByType<GameFlowRoot>();

        DRPGBattleFlowRoot flow = root != null ? root.drpgBattleFlow : null;
        if (flow == null)
            flow = UnityEngine.Object.FindFirstObjectByType<DRPGBattleFlowRoot>();
        return flow != null ? flow.CurrentSession : null;
    }

    private static DRPGBattlePresentationRoot ResolvePresentationRoot()
    {
        return UnityEngine.Object.FindFirstObjectByType<DRPGBattlePresentationRoot>();
    }

    private static string DescribeContext()
    {
        GameUIRoot uiRoot = GameUIRoot.Instance;
        if (uiRoot == null)
            uiRoot = UnityEngine.Object.FindFirstObjectByType<GameUIRoot>();
        GameFlowRoot flowRoot = GameFlowRoot.Instance;
        if (flowRoot == null)
            flowRoot = UnityEngine.Object.FindFirstObjectByType<GameFlowRoot>();
        DRPGBattleDebugLauncher launcher = ResolveBattleLauncher();
        object modalOwner = uiRoot != null
            ? uiRoot.ModalCoordinator.ActiveOwner
            : null;

        return "Scene=" + SceneManager.GetActiveScene().name +
               "; PlayMode=" + Application.isPlaying +
               "; GameFlowRoot=" + NameOrMissing(flowRoot) +
               "; GameUIRoot=" + NameOrMissing(uiRoot) +
               "; BattleLauncher=" + NameOrMissing(launcher) +
               "; ModalOwner=" +
               (modalOwner != null ? modalOwner.GetType().Name : "None");
    }

    private static string NameOrMissing(UnityEngine.Object value) =>
        value != null ? value.name : "Missing";

    private sealed class RegistrationGroup : IDisposable
    {
        private List<IDisposable> registrations;

        public RegistrationGroup(List<IDisposable> registrations)
        {
            this.registrations = registrations;
        }

        public void Dispose()
        {
            if (registrations == null)
                return;

            for (int i = registrations.Count - 1; i >= 0; i--)
                registrations[i]?.Dispose();
            registrations = null;
        }
    }
}
