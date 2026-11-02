// 独立手算孔洞夹具；回归玩家远距、相机窗口与业务种子互不授权的合同。
// RED阶段缺少新API时接现有二参路线，使断言呈现旧行为缺陷，而不是以编译错误冒充RED。
using System;
using Ossastra.SceneVisibility.Rooms.Core;

internal static class PortalNaturalSightTests
{
    private static int passed, failed;
    private static SightVector3 V(float x, float y, float z) => new SightVector3(x, y, z);
    private static readonly SightVector3[] Opening = { V(-1, 0, 0), V(1, 0, 0), V(1, 2, 0), V(-1, 2, 0) };
    private static PortalSightSnapshot Window(string id = "window", string a = "a", string b = ObserverSnapshot.Exterior,
        bool open = true, float range = 6, float far = 8, SightVector3[] aperture = null, SightVector3? normal = null)
        => new PortalSightSnapshot(id, a, b, aperture ?? Opening, normal ?? V(0, 0, 1), open, range, far);
    private static ObserverSnapshot Inside(SightVector3? position = null, SightVector3? forward = null)
        => new ObserverSnapshot("a", position ?? V(0, 1, -2), forward ?? V(0, 0, 1));
    private static PortalSightResult Natural(ObserverSnapshot observer, params PortalSightSnapshot[] portals)
    {
#if HAS_POLICY_API
        return PortalSightSolver.Solve(observer, portals, PortalSightPolicy.PlayerNatural);
#else
        return PortalSightSolver.Solve(observer, portals);
#endif
    }
    private static PortalSightResult Camera(string seed, SightVector3 camera, params PortalSightSnapshot[] portals)
    {
#if HAS_POLICY_API
        return PortalSightSolver.SolveWindowScene(seed, camera, portals);
#else
        return PortalSightSolver.Solve(new ObserverSnapshot(seed, camera, V(0, 0, 1)), portals);
#endif
    }
    private static PortalSightResult Finite(ObserverSnapshot observer, float range, float far, params PortalSightSnapshot[] portals)
    {
#if HAS_POLICY_API
        return PortalSightSolver.Solve(observer, portals, PortalSightPolicy.Finite(range, far));
#else
        return PortalSightSolver.Solve(observer, portals);
#endif
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static PortalSightVolume Single(PortalSightResult result)
    {
        Assert(result.Volumes.Count == 1, "应有一个真实孔洞视体，实际 " + result.Volumes.Count);
        return result.Volumes[0];
    }
    private static void Check(string name, Action body)
    {
        try { body(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); failed++; }
    }
    private static int Main()
    {
        Check("自然视野不以六米靠窗距离整片关闭", () => {
            Assert(Single(Natural(Inside(V(0, 1, -20)), Window())).ContainsPoint(V(0, 1, 2)), "二十米外应仍从真实开口看到合法点");
        });
        Check("自然视野不在窗后八米截断", () => {
            Assert(Single(Natural(Inside(), Window())).ContainsPoint(V(0, 1, 80)), "八十米外同射线被旧远面截断");
        });
        Check("自然视野不读取已无效的旧局部距离参数", () => {
            Assert(Single(Natural(Inside(), Window(range: float.NaN, far: -1))).ContainsPoint(V(0, 1, 80)), "独立玩家策略仍被旧参数控制");
        });
        Check("自然视野继续拒绝窗框外及孔洞源侧", () => {
            var s = Single(Natural(Inside(), Window()));
            Assert(!s.ContainsPoint(V(2.1f, 1, 2)), "右窗框外泄露");
            Assert(!s.ContainsPoint(V(0, 3.1f, 2)), "窗上沿外泄露");
            Assert(!s.ContainsPoint(V(0, 1, -1)) && !s.ContainsPoint(V(0, 1, 0)), "孔洞近面错误");
        });
        Check("自然视野在近窗处仅按孔洞真实角度展开", () => {
            var far = Single(Natural(Inside(V(0, 1, -20)), Window()));
            var near = Single(Natural(Inside(V(0, 1, -1)), Window()));
            Assert(!far.ContainsPoint(V(3, 1, 5)), "远处不应越过实体侧边");
            Assert(near.ContainsPoint(V(3, 1, 5)), "靠近真实窗洞应自然获得更广视角");
        });
        Check("玩家前半空间保留并拒绝背后", () => {
            var s = Single(Natural(Inside(forward: V(1, 0, 0)), Window()));
            Assert(s.ContainsPoint(V(1, 1, 2)) && !s.ContainsPoint(V(-1, 1, 2)), "玩家前后半空间混淆");
            Assert(Natural(Inside(forward: V(0, 0, -1)), Window()).Volumes.Count == 0, "背向开口仍授权");
        });
        Check("自然室外窥视不提升房间种子", () => {
            var r = Natural(new ObserverSnapshot(ObserverSnapshot.Exterior, V(0, 1, 20), V(0, 0, -1)), Window());
            Assert(r.FullSeedRoomId == null && Single(r).TargetSpaceId == "a", "室外窥视升级完整房间或受旧距离限制");
        });
        Check("自然首跳不递归邻房", () => {
            var r = Natural(Inside(), Window("ab", "a", "b"), Window("bc", "b", "c"));
            Assert(r.FullSeedRoomId == "a" && Single(r).TargetSpaceId == "b", "邻房成为第二种子");
        });
        Check("自然策略拒绝封闭洞和错误来源面", () => {
            Assert(Natural(Inside(), Window(open: false)).Volumes.Count == 0, "关闭窗仍授予外景");
            Assert(Natural(Inside(V(0, 1, 2)), Window()).Volumes.Count == 0, "几何错误一侧仍授权");
        });
        Check("独立有限策略不从门窗旧参数取值", () => {
            var s = Single(Finite(Inside(), 3, 50, Window(range: .1f, far: .1f)));
            Assert(s.ContainsPoint(V(0, 1, 49)) && !s.ContainsPoint(V(0, 1, 50.1f)), "独立有限窗后距离未生效");
            Assert(Finite(Inside(V(0, 1, -4)), 3, 50, Window(range: 99, far: 99)).Volumes.Count == 0, "独立靠窗距离未生效");
        });
        Check("旧二参路线仍保留六米八米", () => {
            Assert(PortalSightSolver.Solve(Inside(V(0, 1, -20)), new[] { Window() }).Volumes.Count == 0, "Legacy远距语义变化");
            Assert(!Single(PortalSightSolver.Solve(Inside(), new[] { Window() })).ContainsPoint(V(0, 1, 20)), "Legacy远面语义变化");
        });
        Check("相机窗口域绝不授予完整种子", () => {
            var r = Camera("a", V(0, 1, -2), Window());
            Assert(r.FullSeedRoomId == null && Single(r).TargetSpaceId == ObserverSnapshot.Exterior, "相机窗景升级业务房间");
        });
        Check("相机可以在房间体积外但孔洞内侧观察", () => {
            var s = Single(Camera("a", V(0, 10, -20), Window()));
            Assert(s.ContainsPoint(V(0, -1.25f, 5)), "相机通过洞中心(0,1,0)的延长射线被拒绝");
            Assert(s.Observer.Position.Y == 10 && s.Observer.Position.Z == -20, "相机域没有保留实际原点");
        });
        Check("相机窗景不在远处截断", () => {
            Assert(Single(Camera("a", V(0, 1, -2), Window())).ContainsPoint(V(0, 1, 80)), "窗景使用旧有限远面");
        });
        Check("相机窗景只接受当前房间到室外的开放洞", () => {
            var r = Camera("a", V(0, 1, -2), Window("ab", "a", "b"), Window("outside-b", "b"), Window("closed", open: false));
            Assert(r.Volumes.Count == 0 && r.FullSeedRoomId == null, "相机域扩展邻室或关闭洞");
        });
        Check("相机窗景没有玩家朝向平面", () => {
            var opening = new[] { V(0,0,-1), V(0,0,1), V(0,2,1), V(0,2,-1) };
            var s = Single(Camera("a", V(-2, 1, 2), Window(aperture: opening, normal: V(1,0,0))));
            Assert(s.ContainsPoint(V(2, 1, -2)), "穿过真实侧窗中心的相机射线被玩家半空间裁掉");
        });
        Check("相机窗口支持SideB为当前房间的反向定义", () => {
            var s = Single(Camera("a", V(0, 1, 20), Window(a: ObserverSnapshot.Exterior, b: "a")));
            Assert(s.SourceSpaceId == "a" && s.TargetSpaceId == ObserverSnapshot.Exterior && s.ContainsPoint(V(0, 1, -20)), "相反登记方向被拒绝");
        });
        Check("相机窗景拒绝室外种子和非有限相机", () => {
            Assert(Camera(ObserverSnapshot.Exterior, V(0, 1, 2), Window()).Volumes.Count == 0, "室外camera域反向授权室内");
            Assert(Camera("a", V(float.NaN, 1, -2), Window()).Volumes.Count == 0, "无效相机生成视体");
        });
        Check("相机窗口拒绝窗框外和源侧及错误相机面", () => {
            var s = Single(Camera("a", V(0, 1, -2), Window()));
            Assert(!s.ContainsPoint(V(2.1f, 1, 2)) && !s.ContainsPoint(V(0, 3.1f, 2)), "相机窗框外泄露");
            Assert(!s.ContainsPoint(V(0, 1, -1)) && !s.ContainsPoint(V(0, 1, 0)), "相机近面泄露");
            Assert(Camera("a", V(0, 1, 2), Window()).Volumes.Count == 0, "错误相机侧生成窗景");
        });
        Check("玩家与相机均拒绝非凸及非平面孔洞", () => {
            var concave = new[] { V(-1,0,0), V(1,0,0), V(0,1,0), V(1,2,0), V(-1,2,0) };
            var nonplanar = new[] { V(-1,0,0), V(1,0,0), V(1,2,.1f), V(-1,2,0) };
            Assert(Natural(Inside(), Window(aperture: concave)).Volumes.Count == 0, "自然视野对凹洞取凸包");
            Assert(Camera("a", V(0,1,-2), Window(aperture: nonplanar)).Volumes.Count == 0, "相机接受非平面洞");
        });
        Console.WriteLine("RESULT passed=" + passed + " failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
