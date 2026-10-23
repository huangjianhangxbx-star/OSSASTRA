#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class ANKALUTEDevDebugActionsProbe
{
    public static string Run()
    {
        var registry = new DevDebugRegistry();
        IDisposable registration = ANKALUTEDevDebugActions.RegisterInto(
            registry,
            () => null,
            () => null,
            () => "context-ready");

        IReadOnlyList<IDevDebugAction> actions = registry.Snapshot();
        var expectedIds = new HashSet<string> {
            "battle.standard", "battle.defeat", "battle.force_victory", "battle.force_defeat",
            "battle.presentation_entry", "battle.horde_deploy", "menu.character", "system.context",
            "battle.action_spec", "battle.basic_only.start", "battle.basic_only.invalid",
            "battle.basic_only.reset", "battle.basic_only.disable", "battle.basic_only.inspect"
        };
        Assert(actions.Count == expectedIds.Count, "Development action registration set changed");
        foreach (var action in actions)
            Assert(expectedIds.Remove(action.Id), "Unexpected or duplicate action: " + action.Id);
        Assert(expectedIds.Count == 0, "Missing development actions");
        foreach (string suffix in new[] { "start", "invalid", "reset", "disable", "inspect" })
        {
            string id = "battle.basic_only." + suffix;
            Require(actions, id, "平A验收");
            Assert(registry.Execute(id).Status == DevDebugActionStatus.Unavailable,
                id + " must not run without its dependency");
        }
        IDevDebugAction actionSpec = Find(actions, "battle.action_spec");
        Require(actions, "battle.presentation_entry", "战斗演出");
        Require(actions, "battle.horde_deploy", "战斗演出");
        Find(actions, "battle.force_victory");
        Find(actions, "battle.force_defeat");
        Require(actions, "battle.standard", "战斗");
        Require(actions, "battle.defeat", "战斗");
        Require(actions, "menu.character", "菜单");
        IDevDebugAction context = Require(actions, "system.context", "系统");

        DevDebugActionResult battle = registry.Execute("battle.standard");
        Assert(battle.Status == DevDebugActionStatus.Unavailable,
            "Battle action must be unavailable without a launcher");
        Assert(battle.Message.Contains("DRPGBattleDebugLauncher"),
            "Battle action must explain which dependency is missing");

        DevDebugActionResult menu = registry.Execute("menu.character");
        Assert(menu.Status == DevDebugActionStatus.Unavailable,
            "Menu action must be unavailable without its input router");
        Assert(menu.Message.Contains("GameMenuInputRouter"),
            "Menu action must explain which dependency is missing");

        Assert(context.CloseHubOnSuccess == false,
            "Context report must keep the hub open so its result is readable");
        DevDebugActionResult contextResult =
            registry.Execute("system.context");
        Assert(contextResult.Status == DevDebugActionStatus.Succeeded,
            "Context action must report success");
        Assert(contextResult.Message == "context-ready",
            "Context action must show the resolved development context");

        DevDebugActionResult specResult = actionSpec.Execute();
        Assert(specResult.Status == DevDebugActionStatus.Unavailable,
            "Action-spec report must explain when no battle action exists");

        registration.Dispose();
        registration.Dispose();
        Assert(registry.Snapshot().Count == 0,
            "Disposing the project adapter must remove every action it added");

        var host = new UnityEngine.GameObject("BasicOnlyBoundaryProbe");
        try
        {
            var launcher = host.AddComponent<DRPGBattleDebugLauncher>();
            using (ANKALUTEDevDebugActions.RegisterInto(registry, () => launcher, () => null, () => "probe"))
            {
                Assert(registry.Execute("battle.basic_only.invalid").Status == DevDebugActionStatus.Succeeded,
                    "Actual launcher must reject mismatched seeds without starting a battle");
            }
            Assert(registry.Snapshot().Count == 0, "Repeated registration leaked actions");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }

        return "PASS ANKALUTE development debug action adapters";
    }

    private static IDevDebugAction Find(
        IReadOnlyList<IDevDebugAction> actions,
        string id)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Id == id)
                return actions[i];
        }
        throw new Exception("Missing development debug action: " + id);
    }

    private static IDevDebugAction Require(
        IReadOnlyList<IDevDebugAction> actions,
        string id,
        string category)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Id != id)
                continue;
            Assert(actions[i].Category == category,
                id + " must belong to category " + category);
            Assert(!string.IsNullOrWhiteSpace(actions[i].Summary),
                id + " requires a short summary");
            Assert(!string.IsNullOrWhiteSpace(actions[i].Details),
                id + " requires detailed hover help");
            return actions[i];
        }

        throw new Exception("Missing development debug action: " + id);
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
