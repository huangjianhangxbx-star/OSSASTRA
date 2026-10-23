#if UNITY_EDITOR
using System;
using System.Collections.Generic;

public static class DevDebugRegistryProbe
{
    public static string Run()
    {
        var registry = new DevDebugRegistry();
        int changedCount = 0;
        int executionCount = 0;
        registry.Changed += () => changedCount++;

        IDisposable laterRegistration = registry.Register(
            CreateAction(
                "battle.later",
                "战斗",
                "后执行",
                20,
                () =>
                {
                    executionCount++;
                    return DevDebugActionResult.Succeeded("later ran");
                }));
        IDisposable earlierRegistration = registry.Register(
            CreateAction(
                "battle.earlier",
                "战斗",
                "先执行",
                10,
                () => DevDebugActionResult.Succeeded("earlier ran")));

        IReadOnlyList<IDevDebugAction> snapshot = registry.Snapshot();
        Assert(snapshot.Count == 2, "Registry must expose registered actions");
        Assert(snapshot[0].Id == "battle.earlier",
            "Actions must be sorted by category, order, then title");
        Assert(snapshot[1].Id == "battle.later",
            "Higher-order action must follow lower-order action");
        Assert(changedCount == 2,
            "Each successful registration must publish one change");

        AssertThrows<InvalidOperationException>(
            () => registry.Register(
                CreateAction(
                    "battle.earlier",
                    "战斗",
                    "重复",
                    99,
                    () => DevDebugActionResult.Succeeded("must not run"))),
            "Duplicate action IDs must be rejected");

        DevDebugActionResult success = registry.Execute("battle.later");
        Assert(success.Status == DevDebugActionStatus.Succeeded,
            "Available action must report success");
        Assert(executionCount == 1,
            "Available action must execute exactly once");

        int unavailableExecutionCount = 0;
        registry.Register(
            new DelegateDevDebugAction(
                id: "battle.unavailable",
                category: "战斗",
                title: "条件不足",
                summary: "不可执行测试",
                details: "用于验证不可执行动作不会进入 Execute。",
                order: 30,
                closeHubOnSuccess: false,
                evaluate: () => DevDebugAvailability.Unavailable("缺少战斗场景"),
                execute: () =>
                {
                    unavailableExecutionCount++;
                    return DevDebugActionResult.Succeeded("must not run");
                }));
        DevDebugActionResult unavailable =
            registry.Execute("battle.unavailable");
        Assert(unavailable.Status == DevDebugActionStatus.Unavailable,
            "Unavailable action must return the unavailable status");
        Assert(unavailable.Message == "缺少战斗场景",
            "Unavailable result must preserve the actionable reason");
        Assert(unavailableExecutionCount == 0,
            "Unavailable action must not execute");

        registry.Register(
            CreateAction(
                "system.exception",
                "系统",
                "异常动作",
                10,
                () => throw new InvalidOperationException("probe boom")));
        DevDebugActionResult failed = registry.Execute("system.exception");
        Assert(failed.Status == DevDebugActionStatus.Failed,
            "Action exceptions must become failed results");
        Assert(failed.Message.Contains("probe boom"),
            "Failed result must expose the exception message");

        laterRegistration.Dispose();
        snapshot = registry.Snapshot();
        Assert(snapshot.Count == 3,
            "Disposing a registration must remove only its own action");
        Assert(registry.Execute("battle.later").Status ==
            DevDebugActionStatus.Failed,
            "Removed action must no longer execute");
        Assert(changedCount == 5,
            "Register, register, register, register and dispose must publish changes");

        earlierRegistration.Dispose();
        return "PASS pluggable development debug registry";
    }

    private static DelegateDevDebugAction CreateAction(
        string id,
        string category,
        string title,
        int order,
        Func<DevDebugActionResult> execute)
    {
        return new DelegateDevDebugAction(
            id,
            category,
            title,
            "简短说明",
            "详细测试说明",
            order,
            false,
            () => DevDebugAvailability.Available(),
            execute);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new Exception(message);
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
