// 独立手算几何夹具。每个断言针对越权、边界或遮挡错误；不依赖 Unity 或 Shader。
using System;
using System.Collections.Generic;
using Ossastra.SceneVisibility.Rooms.Core;

internal static class PortalSightTests
{
    private static int failed;
    private static int passed;
    private static SightVector3 V(float x, float y, float z) => new SightVector3(x, y, z);
    private static readonly SightVector3[] Opening = { V(-1, 0, 0), V(1, 0, 0), V(1, 2, 0), V(-1, 2, 0) };
    private static PortalSightSnapshot Window(string id = "window", string a = "a", string b = ObserverSnapshot.Exterior,
        bool open = true, float range = 4, float far = 5, SightVector3[] aperture = null, SightVector3? normal = null)
        => new PortalSightSnapshot(id, a, b, aperture ?? Opening, normal ?? V(0, 0, 1), open, range, far);
    private static ObserverSnapshot Inside(SightVector3? position = null, SightVector3? forward = null)
        => new ObserverSnapshot("a", position ?? V(0, 1, -2), forward ?? V(0, 0, 1));
    private static PortalSightResult Solve(ObserverSnapshot observer, params PortalSightSnapshot[] portals)
        => PortalSightSolver.Solve(observer, portals);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Check(string name, Action body)
    {
        try { body(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); failed++; }
    }
    private static PortalSightVolume Single(PortalSightResult r)
    {
        Assert(r.Volumes.Count == 1, "应有一个合法视体，实际 " + r.Volumes.Count);
        return r.Volumes[0];
    }
    private static int Main()
    {
        Check("室内经真实孔洞看到有限室外", () => {
            var result = Solve(Inside(), Window());
            Assert(result.FullSeedRoomId == "a", "当前种子丢失");
            var sight = Single(result);
            Assert(sight.SourceSpaceId == "a" && sight.TargetSpaceId == ObserverSnapshot.Exterior, "两面方向错误");
            Assert(sight.ContainsPoint(V(0, 1, 3)), "孔洞中心后的合法点被拒绝");
        });
        Check("室外同窗反向看内而不授予整房种子", () => {
            var r = Solve(new ObserverSnapshot(ObserverSnapshot.Exterior, V(0, 1, 2), V(0, 0, -1)), Window());
            Assert(r.FullSeedRoomId == null, "室外窥视不能获得整房种子");
            var s = Single(r);
            Assert(s.TargetSpaceId == "a" && s.ContainsPoint(V(0, 1, -3)), "同窗反向不可见");
            Assert(!s.ContainsPoint(V(4, 1, -2)), "室内窗侧盲区泄露");
        });
        Check("窗框两侧上沿与窗台拒绝窗外点", () => {
            var s = Single(Solve(Inside(), Window()));
            Assert(!s.ContainsPoint(V(2.1f, 1, 2)), "右窗框外泄露");
            Assert(!s.ContainsPoint(V(-2.1f, 1, 2)), "左窗框外泄露");
            Assert(!s.ContainsPoint(V(0, 3.1f, 2)), "窗上沿外泄露");
            Assert(!s.ContainsPoint(V(0, -1.1f, 2)), "窗台下泄露");
            Assert(s.ContainsPoint(V(1.9f, 2.9f, 2)), "合法孔洞斜射线被拒绝");
        });
        Check("开口近面和有限远面独立", () => {
            var s = Single(Solve(Inside(), Window()));
            Assert(!s.ContainsPoint(V(0, 1, -1)), "源侧点不可当目标");
            Assert(!s.ContainsPoint(V(0, 1, 0)), "孔洞面不等于目标区域");
            Assert(s.ContainsPoint(V(0, 1, 5)), "有限远面合法边界丢失");
            Assert(!s.ContainsPoint(V(0, 1, 5.01f)), "远面之外泄露");
        });
        Check("观察范围使用孔洞最近点而非中心", () => {
            Assert(Solve(Inside(V(.99f, 1.99f, -2)), Window(range: 2.01f)).Volumes.Count == 1, "孔洞角旁实际两米不应按中心距离拒绝");
            Assert(Solve(Inside(V(4, 1, -2)), Window(range: 3.60f)).Volumes.Count == 0, "到边缘距离sqrt13超范围");
            Assert(Solve(Inside(V(4, 1, -2)), Window(range: 3.61f)).Volumes.Count == 1, "到边缘距离sqrt13应在范围内");
        });
        Check("背对移远与站错面立即撤回", () => {
            Assert(Solve(Inside(forward: V(0, 0, -1)), Window()).Volumes.Count == 0, "背对窗仍获视域");
            Assert(Solve(Inside(V(0, 1, -4.1f)), Window()).Volumes.Count == 0, "远离仍获视域");
            Assert(Solve(Inside(V(0, 1, 2)), Window()).Volumes.Count == 0, "房间身份和开口几何面不符");
        });
        Check("封板状态双向撤回且不耦合通行", () => {
            var closed = Window(open: false);
            Assert(Solve(Inside(), closed).Volumes.Count == 0, "封板室内侧泄露");
            Assert(Solve(new ObserverSnapshot(ObserverSnapshot.Exterior, V(0, 1, 2), V(0, 0, -1)), closed).Volumes.Count == 0, "封板室外侧泄露");
            Assert(Solve(Inside(), Window()).Volumes.Count == 1, "不需任何可通行状态即可透过透明玻璃");
        });
        Check("开放门只给邻室局部且不递归传播", () => {
            var ab = Window("ab", "a", "b");
            var bc = new PortalSightSnapshot("bc", "b", "c", new[] { V(-1, 0, 3), V(1, 0, 3), V(1, 2, 3), V(-1, 2, 3) }, V(0, 0, 1), true, 10, 5);
            var r = Solve(Inside(), ab, bc);
            var s = Single(r);
            Assert(s.TargetSpaceId == "b" && r.FullSeedRoomId == "a", "邻室不应成为完整种子");
            Assert(!s.ContainsPoint(V(4, 1, 1)), "门侧整房被误开放");
            var entered = Solve(new ObserverSnapshot("b", V(0, 1, 1), V(0, 0, 1)), ab, bc);
            Assert(entered.FullSeedRoomId == "b" && Single(entered).TargetSpaceId == "c", "真实入B后未更换种子");
        });
        Check("观察方向裁剪而非相机位置授权", () => {
            var s = Single(Solve(Inside(forward: V(1, 0, 0)), Window()));
            Assert(s.ContainsPoint(V(1, 1, 2)), "朝向半空间合法点被拒绝");
            Assert(!s.ContainsPoint(V(-1, 1, 2)), "背后半空间泄露");
            // Solve没有相机参数；相同observer可被任意相机投影而不会扩权。
        });
        Check("旋转平移的孔洞保持真实三维约束", () => {
            var polygon = new[] { V(10, 3, 4), V(10, 3, 6), V(10, 5, 6), V(10, 5, 4) };
            var p = Window(aperture: polygon, normal: V(1, 0, 0));
            var s = Single(Solve(Inside(V(8, 4, 5), V(1, 0, 0)), p));
            Assert(s.ContainsPoint(V(13, 4, 5)), "旋转后合法点拒绝");
            Assert(!s.ContainsPoint(V(12, 6.1f, 5)), "旋转后窗上泄露");
            Assert(!s.ContainsPoint(V(15.1f, 4, 5)), "旋转后远面泄露");
        });
        Check("孔洞绕序不改变视野", () => {
            var reversed = (SightVector3[])Opening.Clone(); Array.Reverse(reversed);
            var a = Single(Solve(Inside(), Window()));
            var b = Single(Solve(Inside(), Window(aperture: reversed)));
            foreach (var p in new[] { V(0, 1, 3), V(3, 1, 1), V(0, -2, 1), V(0, 1, 6) })
                Assert(a.ContainsPoint(p) == b.ContainsPoint(p), "绕序影响许可");
        });
        Check("不可用几何与非有限观察值安全拒绝", () => {
            var nonplanar = new[] { V(-1, 0, 0), V(1, 0, 0), V(1, 2, .1f), V(-1, 2, 0) };
            var concave = new[] { V(-1, 0, 0), V(1, 0, 0), V(0, .5f, 0), V(1, 2, 0), V(-1, 2, 0) };
            Assert(Solve(Inside(), Window(aperture: nonplanar)).Volumes.Count == 0, "非平面孔洞不得退化放行");
            Assert(Solve(Inside(), Window(aperture: concave)).Volumes.Count == 0, "凹孔洞需明确分片，不能扩大成凸包");
            Assert(Solve(Inside(), Window(normal: V(0, 0, 0))).Volumes.Count == 0, "零法线放行");
            Assert(Solve(Inside(), Window(far: -1)).Volumes.Count == 0, "负远面放行");
            var invalidObserver = Solve(Inside(V(float.NaN, 1, -2)), Window());
            Assert(invalidObserver.Volumes.Count == 0, "NaN观察点放行");
            Assert(invalidObserver.FullSeedRoomId == null, "无效观察点不能保留完整种子授权");
            Assert(Solve(Inside(forward: V(0, 0, 0)), Window()).Volumes.Count == 0, "零朝向放行");
        });
        Check("快照和求解结果不受作者数组后改影响", () => {
            var input = (SightVector3[])Opening.Clone(); var p = Window(aperture: input);
            input[0] = V(-100, 0, 0);
            var s = Single(Solve(Inside(), p));
            Assert(!s.ContainsPoint(V(-10, 1, 1)), "作者数组修改扩权");
            Assert(s.Aperture.Count == 4, "丢失真实孔洞");
        });
        Check("GPU有向平面与CPU真值一致", () => {
            var s = Single(Solve(Inside(), Window()));
            Assert(s.ClipPlanes.Count == 7, "四边+近远+朝向应共七面");
            foreach (var p in new[] { V(0, 1, 2), V(3, 1, 1), V(0, 4, 2), V(0, 1, 7), V(0, 1, -1) }) {
                bool permit = true;
                foreach (var plane in s.ClipPlanes) permit &= plane.SignedDistance(p) >= 0;
                Assert(permit == s.ContainsPoint(p), "CPU/GPU平面符号不同");
            }
        });
        Check("实体封板真实射线阻挡独立于portal布尔值", () => {
            var board = new PortalSightBlocker(V(0, 1, 0), V(1, 0, 0), V(0, 1, 0), V(0, 0, 1), V(1, 1, .1f));
            Assert(board.IsValid && board.OccludesSegment(V(0, 1, -2), V(0, 1, 3)), "实体封板未挡视线");
            Assert(board.OccludesSegment(V(0, 1, 2), V(0, 1, -3)), "实体封板反向未挡视线");
            Assert(!board.OccludesSegment(V(3, 1, -2), V(3, 1, 3)), "封板旁射线被误挡");
        });
        Check("窗后高柜与旋转OBB按实际段遮挡", () => {
            float q = (float)Math.Sqrt(.5);
            var cabinet = new PortalSightBlocker(V(0, 1, 2), V(q, 0, q), V(0, 1, 0), V(-q, 0, q), V(.5f, 1, .5f));
            Assert(cabinet.OccludesSegment(V(0, 1, -2), V(0, 1, 4)), "旋转高柜后泄露");
            Assert(!cabinet.OccludesSegment(V(0, 1, -2), V(0, 1, 1)), "高柜后方不应遮挡前方目标");
            Assert(!cabinet.OccludesSegment(V(2, 1, -2), V(2, 1, 4)), "高柜侧边不应误挡");
        });
        Check("射线目标面偏置不把物件自身当遮挡", () => {
            var surface = new PortalSightBlocker(V(0, 1, 3.1f), V(1, 0, 0), V(0, 1, 0), V(0, 0, 1), V(1, 1, .1f));
            Assert(!surface.OccludesSegment(V(0, 1, -2), V(0, 1, 3)), "目标自身前表面被当挡板");
            Assert(surface.OccludesSegment(V(0, 1, -2), V(0, 1, 4)), "同一实体后方应被挡");
        });
        Check("前墙按实际相机方向剔除背侧保持", () => {
            var wall = new FrontWallSnapshot("wall", "a", ObserverSnapshot.Exterior, V(0, 1, 0), V(0, 0, -1), true);
            Assert(FrontWallSolver.ShouldHide(wall, "a", V(0, -.7f, 1)), "近镜头前墙未剔除");
            Assert(!FrontWallSolver.ShouldHide(wall, "a", V(0, -.7f, -1)), "背墙不应剔除");
            Assert(!FrontWallSolver.ShouldHide(wall, "a", V(1, -.7f, 0)), "侧墙不应剔除");
        });
        Check("非种子与未入室不授予前墙剔除", () => {
            var wall = new FrontWallSnapshot("wall", "a", ObserverSnapshot.Exterior, V(0, 1, 0), V(0, 0, -1), true);
            Assert(!FrontWallSolver.ShouldHide(wall, null, V(0, 0, 1)), "室外窥视误触发前墙剔除");
            Assert(!FrontWallSolver.ShouldHide(wall, "b", V(0, 0, 1)), "Partial邻室误获前墙权");
            var shared = new FrontWallSnapshot("shared", "a", "b", V(0, 1, 0), V(0, 0, -1), true);
            Assert(!FrontWallSolver.ShouldHide(shared, "c", V(0, 0, 1)), "不属于当前种子房间的共墙误获剔除权");
            Assert(!FrontWallSolver.ShouldHide(shared, null, V(0, 0, 1)), "室外窥视不能剔除共墙");
        });
        Check("共墙任一侧为实际种子时允许必要前墙段隐藏", () => {
            // A在共墙北侧，NormalFromPrimary向南；两侧以各自向外法线判断，不要求另一侧也Full。
            var shared = new FrontWallSnapshot("shared", "a", "b", V(0, 1, 0), V(0, 0, -1), true);
            Assert(FrontWallSolver.ShouldHide(shared, "a", V(0, -.7f, 1)), "A为实际种子时，面向镜头的必要共墙段仍挡视线");
            Assert(!FrontWallSolver.ShouldHide(shared, "a", V(0, -.7f, -1)), "A为种子时背面共墙不应自动隐藏");
            Assert(FrontWallSolver.ShouldHide(shared, "b", V(0, -.7f, -1)), "B为实际种子时，应反向使用该侧向外法线");
            Assert(!FrontWallSolver.ShouldHide(shared, "b", V(0, -.7f, 1)), "B为种子时背面共墙不应自动隐藏");
            Assert(!FrontWallSolver.ShouldHide(shared, "a", V(1, -.7f, 0)), "侧向共墙不应自动隐藏");
        });
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
