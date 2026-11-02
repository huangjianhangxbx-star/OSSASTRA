/*
 * 功能：以手算空间候选验证真实房间归属解析器；测试不模拟角色或渲染系统。
 * 边界：只调用生产Core；构造器与候选数据均为实际实现，不含Unity或外部测试依赖。
 * 覆盖：出生/切控/门槛/失去主控/移除登记/确定性冲突与热路径分配。
 */
using System;
using Ossastra.SceneVisibility.Rooms.Core;

internal static class RoomMembershipTests
{
    private static int passed;
    private static int failed;

    private static void Main()
    {
        Run("出生室内不依赖越过门槛事件", SpawnInside);
        Run("同身份入室须越过收缩门槛", EnterThreshold);
        Run("现房间退出滞回优先于重叠邻室", RetainAtThreshold);
        Run("真正离开后转入邻室或室外", LeaveAndTransfer);
        Run("切控立即解析新主控而不扩大旧房间", SwitchActor);
        Run("无存活主控冻结最后合法房间", NoLivingActor);
        Run("恢复同身份立即解析准确空间", ResumeSameActor);
        Run("同身份传送可明确绕过门槛滞回", ForceImmediate);
        Run("重叠选择优先级并以稳定ID打破平局", DeterministicOverlap);
        Run("删除登记立即清理已活动房间", RemoveRegistration);
        Run("无效重登记被拒且原配置仍可用", InvalidRegistrationIsAtomic);
        Run("候选未登记或重复时拒绝且保留当前状态", InvalidCandidateIsAtomic);
        Run("缺失活动候选不会无限保留旧域", MissingActiveCandidate);
        Run("存活主控必须有身份和候选快照", InvalidLiveContext);
        Run("稳定逐帧解析不分配托管集合", SteadyStateAllocation);
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    private static RoomMembershipResolver NewResolver()
    {
        return new RoomMembershipResolver(new[] { "A", "B" });
    }

    private static RoomCandidate C(string id, bool inside, bool enter, bool retain, int priority = 0)
    {
        return new RoomCandidate(id, priority, inside, enter, retain);
    }

    // 若把初次解析误写成CanEnter，此用例会漏掉已出生在室内但处于门槛带的角色。
    private static void SpawnInside()
    {
        var resolver = NewResolver();
        Equal("A", resolver.Resolve("player", true, new[] { C("A", true, false, true) }));
    }

    private static void EnterThreshold()
    {
        var resolver = NewResolver();
        Equal(null, resolver.Resolve("player", true, new[] { C("A", false, false, false) }));
        Equal(null, resolver.Resolve("player", true, new[] { C("A", true, false, true) }));
        Equal("A", resolver.Resolve("player", true, new[] { C("A", true, true, true) }));
    }

    private static void RetainAtThreshold()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Equal("A", resolver.Resolve("player", true,
            new[] { C("A", false, false, true), C("B", true, true, true, 50) }));
    }

    private static void LeaveAndTransfer()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Equal("B", resolver.Resolve("player", true,
            new[] { C("A", false, false, false), C("B", true, true, true) }));
        Equal(null, resolver.Resolve("player", true, new[] { C("B", false, false, false) }));
    }

    private static void SwitchActor()
    {
        var resolver = NewResolver();
        resolver.Resolve("player-a", true, new[] { C("A", true, true, true) });
        Equal("B", resolver.Resolve("player-b", true,
            new[] { C("A", false, false, true), C("B", true, false, true) }));
        Equal(null, resolver.Resolve("player-c", true,
            new[] { C("A", false, false, false), C("B", false, false, false) }));
    }

    private static void NoLivingActor()
    {
        var resolver = NewResolver();
        Equal(null, resolver.Resolve(null, false, null));
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Equal("A", resolver.Resolve(null, false, new[] { C("B", true, true, true) }));
        Equal("A", resolver.Resolve(null, false, null));
    }

    private static void ResumeSameActor()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        resolver.Resolve(null, false, null);
        Equal("B", resolver.Resolve("player", true,
            new[] { C("A", false, false, true), C("B", true, false, true) }));
    }

    private static void ForceImmediate()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Equal("B", resolver.Resolve("player", true,
            new[] { C("A", false, false, true), C("B", true, false, true) }, true));
    }

    private static void DeterministicOverlap()
    {
        var resolver = NewResolver();
        Equal("B", resolver.Resolve("first", true,
            new[] { C("A", true, true, true, 1), C("B", true, true, true, 2) }));
        Equal("A", resolver.Resolve("second", true,
            new[] { C("B", true, true, true, 2), C("A", true, true, true, 2) }));
        Equal("A", resolver.Resolve("third", true,
            new[] { C("A", true, true, true, 2), C("B", true, true, true, 2) }));
    }

    private static void RemoveRegistration()
    {
        var resolver = NewResolver();
        Equal("A", resolver.Resolve("player", true, new[] { C("A", true, true, true) }));
        resolver.SetRegisteredRooms(new[] { "B" });
        Equal(null, resolver.ActiveRoomId);
        Equal(null, resolver.Resolve(null, false, null));
        resolver.Resolve("player", true, new[] { C("B", true, true, true) });
        resolver.SetRegisteredRooms(new string[0]);
        Equal(null, resolver.Resolve(null, false, null));
    }

    private static void InvalidRegistrationIsAtomic()
    {
        Throws<ArgumentException>(() => new RoomMembershipResolver(new[] { "A", "A" }));
        Throws<ArgumentException>(() => new RoomMembershipResolver(new[] { " " }));
        Throws<ArgumentNullException>(() => new RoomMembershipResolver(null));
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Throws<ArgumentException>(() => resolver.SetRegisteredRooms(new[] { "B", "B" }));
        Equal("A", resolver.ActiveRoomId);
        Equal("A", resolver.Resolve("player", true, new[] { C("A", false, false, true) }));
    }

    private static void InvalidCandidateIsAtomic()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Throws<ArgumentException>(() => resolver.Resolve("new-player", true,
            new[] { C("B", true, true, true), C("unknown", true, true, true) }));
        Equal("A", resolver.ActiveRoomId);
        Throws<ArgumentException>(() => resolver.Resolve("new-player", true,
            new[] { C("B", true, true, true), C("B", true, true, true) }));
        Throws<ArgumentException>(() => resolver.Resolve("new-player", true,
            new[] { default(RoomCandidate) }));
        // 拒绝帧不能偷偷提交new-player身份；原player仍应沿滞回保留A。
        Equal("A", resolver.Resolve("player", true, new[] { C("A", false, false, true) }));
    }

    private static void MissingActiveCandidate()
    {
        var resolver = NewResolver();
        Equal("A", resolver.Resolve("player", true, new[] { C("A", true, true, true) }));
        Equal(null, resolver.Resolve("player", true, new RoomCandidate[0]));
    }

    private static void InvalidLiveContext()
    {
        var resolver = NewResolver();
        resolver.Resolve("player", true, new[] { C("A", true, true, true) });
        Throws<ArgumentException>(() => resolver.Resolve(null, true, new RoomCandidate[0]));
        Throws<ArgumentException>(() => resolver.Resolve(" ", true, new RoomCandidate[0]));
        Throws<ArgumentNullException>(() => resolver.Resolve("player", true, null));
        Equal("A", resolver.ActiveRoomId);
    }

    private static void SteadyStateAllocation()
    {
        var resolver = NewResolver();
        var candidates = new[] { C("A", true, true, true), C("B", false, false, false) };
        for (int i = 0; i < 100; ++i) resolver.Resolve("player", true, candidates);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; ++i) resolver.Resolve("player", true, candidates);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated != 0) throw new Exception("Stable Resolve allocated " + allocated + " bytes.");
        Equal("A", resolver.ActiveRoomId);
    }

    private static void Equal(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception("Expected " + (expected ?? "<outside>") + ", got " + (actual ?? "<outside>"));
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static void Run(string name, Action test)
    {
        try { test(); ++passed; Console.WriteLine("PASS " + name); }
        catch (Exception error) { ++failed; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }
}
