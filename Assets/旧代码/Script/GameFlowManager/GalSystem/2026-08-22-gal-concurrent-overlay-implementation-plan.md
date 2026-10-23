# Gal ConcurrentOverlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不修改角色控制器的前提下，实现与 Gameplay 同时运行、自动推进、可排队且可被高级 Gal 模式抢占的左下角通讯卡片。

**Architecture:** 保留唯一 `GalFlowRoot → GalFlowStateMachine → GalDialogueView` 会话链，在 Root 与状态机之间增加 `GalConcurrentDispatcher`。状态机继续管理单会话；调度器负责 Concurrent 请求的 FIFO、去重、冷却、未缩放计时和抢占，现有 EventBus 只对外发布生命周期。

**Tech Stack:** Tuanjie 2022.3.62t12、C#、Unity UI、TextMeshPro、Input System、ScriptableObject、EventBus、Funplay Unity MCP。

**Spec:** `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-concurrent-overlay-design.md`

## Global Constraints

- 只保留一个 `GalFlowRoot`、一个 `GalFlowStateMachine` 和一个正式 `GameFlowArbitrator.RequestGal(GalPlayRequest)` 入口。
- 不修改 `Assets/Script/Character`、`CharacterRoot`、`CharacterDataBus` 或任何角色状态。
- 不新增 `EventCenter` 引用；Gal 生命周期继续使用 `GameFlowRoot.EventHub.Gal` 的现有 `EventBus`。
- `ConcurrentOverlay` 不改变 `Time.timeScale`、`HuntFlowDataBus.isPaused` 或 Character ControlAuthority。
- `PausedOverlay`、`FullscreenExclusive` 及其 P/Shift+P 调试入口必须保持回归通过。
- 运行时模式继续由 `GalPlayRequest.PresentationMode` 明确决定；不得改为隐式消费 `GalSequenceSO.DefaultPresentationMode`。
- 所有 Prefab 修改使用 `PrefabUtility.LoadPrefabContents` 原位升级，保留既有 local fileID、嵌套 Prefab 连接和场景 override。
- 不保存当前已脏的 `Assets/Scenes/测试场3_电车45.unity`；仅在确有连接差异时标脏，且 Installer 不调用 `SaveScene` 或 `SaveOpenScenes`。
- 每个任务严格执行 RED → GREEN → 回归 → 精确提交；不得把 MCP 断连或静态编译成功冒充 Unity PlayMode PASS。
- 不 stage 或提交用户现有 Character 与场景改动。

---

## File Map

### 新建

- `Runtime/Core/GalAutoAdvanceTiming.cs`：无状态的可见字符计数与自动停留时长计算。
- `Runtime/Core/GalConcurrentDispatcher.cs`：Concurrent 请求调度、FIFO、去重、冷却、计时与抢占。
- `Tests/Editor/GalAutoAdvanceTimingProbe.cs`：自动时长纯逻辑测试。
- `Tests/Editor/GalConcurrentDispatcherProbe.cs`：调度器纯逻辑生命周期测试。
- `Content/Test/GalSequence_Test_Concurrent_A.asset`：并行搭话测试内容 A。
- `Content/Test/GalSequence_Test_Concurrent_B.asset`：不同 Key 的队列测试内容 B。
- `Tests/Artifacts/GalConcurrentOverlay.png`：PlayMode 验收截图。
- `2026-08-22-gal-concurrent-overlay-verification.md`：最终真机验证记录。

### 修改

- `Runtime/Content/GalSequenceSO.cs`：为 `GalLine` 增加单句自动时长覆盖。
- `Runtime/Presentation/GalLineViewModel.cs`：携带解析后的时长覆盖值。
- `Runtime/Core/GalSequencePlayer.cs`：把 GalLine 的时长覆盖传入 ViewModel。
- `Runtime/Core/GalContracts.cs`：请求 Key、冷却、调度结果、抢占原因和队列清理事件。
- `Runtime/Core/GalFlowStateMachine.cs`：暴露活动请求、局部换句事件，并禁止 Concurrent 订阅确认按钮。
- `Runtime/Core/GalFlowRoot.cs`：创建并驱动 Dispatcher，统一 Request/Stop/Dispose。
- `Runtime/Presentation/GalDialogueView.cs`：第三套显隐矩阵、通讯卡片内容、进度与淡入淡出。
- `Runtime/Integration/GalDebugHotkeyTrigger.cs`：新增 O/Shift+O 测试入口。
- `Editor/GalSystemTestContentInstaller.cs`：原位生成测试 SO、升级 View/Root Prefab 并接线。
- `Editor/GalPlayModeVerificationRunner.cs`：加入 Concurrent 编辑态组成验证和 PlayMode 验收轮次。
- `Tests/Editor/GalSequencePlayerProbe.cs`：验证时长字段解析。
- `Tests/Editor/GalFlowLifecycleProbe.cs`：验证模式、确认输入与局部换句事件。
- `Tests/Editor/GalDialogueViewProbe.cs`：验证通讯卡片显隐和输入隔离。

---

### Task 1: 自动推进内容契约与时长算法

**Files:**
- Create: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalAutoAdvanceTiming.cs`
- Create: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalAutoAdvanceTimingProbe.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Content/GalSequenceSO.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalLineViewModel.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalSequencePlayer.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalSequencePlayerProbe.cs`

**Interfaces:**
- Consumes: 现有 `GalLine`、`GalLineViewModel`、`GalSequencePlayer`。
- Produces: `GalLine.AutoAdvanceSecondsOverride`、`GalLineViewModel.AutoAdvanceSecondsOverride`、`GalAutoAdvanceTiming.Calculate(string, float)`。

- [ ] **Step 1: 写入自动时长失败测试**

在新 Probe 的 `Run()` 中覆盖以下精确断言：

```csharp
AssertApproximately(1.8f, GalAutoAdvanceTiming.Calculate("", 0f));
AssertApproximately(2.2f, GalAutoAdvanceTiming.Calculate("abcdefghij", 0f));
AssertApproximately(2.06f,
    GalAutoAdvanceTiming.Calculate("<b>abc de fghi</b>", 0f));
AssertApproximately(6.5f,
    GalAutoAdvanceTiming.Calculate(new string('字', 100), 0f));
AssertApproximately(0.35f,
    GalAutoAdvanceTiming.Calculate("ignored", 0.35f));
```

扩展 `GalSequencePlayerProbe`：构造 `new GalLine(..., autoAdvanceSecondsOverride: 2.75f)`，断言解析后的 `GalLineViewModel.AutoAdvanceSecondsOverride == 2.75f`；默认行断言为 0。

- [ ] **Step 2: 运行 RED**

通过 Unity MCP 请求重编译并执行：

```csharp
Debug.Log(GalAutoAdvanceTimingProbe.Run());
Debug.Log(GalSequencePlayerProbe.Run());
```

预期 RED：缺少 `GalAutoAdvanceTiming`、`autoAdvanceSecondsOverride` 命名参数和 ViewModel 属性；失败原因不得来自无关编译错误。

- [ ] **Step 3: 实现最小内容契约**

在 `GalLine` 构造函数末尾追加可选参数，保持旧调用源码兼容：

```csharp
[SerializeField, Min(0f)] private float autoAdvanceSecondsOverride;
public float AutoAdvanceSecondsOverride => Mathf.Max(0f, autoAdvanceSecondsOverride);

public GalLine(
    GalActorSO actor,
    string text,
    string portraitVariantKey = "",
    Sprite portraitOverride = null,
    string speakerNameOverride = "",
    GalPortraitSlot portraitSlot = GalPortraitSlot.Left,
    Sprite backgroundOverride = null,
    float autoAdvanceSecondsOverride = 0f)
{
    this.actor = actor;
    this.text = text;
    this.portraitVariantKey = portraitVariantKey;
    this.portraitOverride = portraitOverride;
    this.speakerNameOverride = speakerNameOverride;
    this.portraitSlot = portraitSlot;
    this.backgroundOverride = backgroundOverride;
    this.autoAdvanceSecondsOverride = Mathf.Max(0f, autoAdvanceSecondsOverride);
}
```

在 `GalLineViewModel` 构造函数末尾追加 `float autoAdvanceSecondsOverride = 0f`，公开同名只读属性。`GalSequencePlayer.ResolveLine()` 把 `line.AutoAdvanceSecondsOverride` 传入；空行降级为 0。

- [ ] **Step 4: 实现无分配正则的时长计算器**

```csharp
using UnityEngine;

public static class GalAutoAdvanceTiming
{
    public const float BaseSeconds = 0.8f;
    public const float SecondsPerVisibleCharacter = 0.14f;
    public const float MinimumSeconds = 1.8f;
    public const float MaximumSeconds = 6.5f;

    public static float Calculate(string text, float overrideSeconds)
    {
        if (overrideSeconds > 0f)
            return overrideSeconds;

        int visibleCharacters = CountVisibleCharacters(text);
        return Mathf.Clamp(
            BaseSeconds + visibleCharacters * SecondsPerVisibleCharacter,
            MinimumSeconds,
            MaximumSeconds);
    }

    private static int CountVisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int count = 0;
        bool insideTag = false;
        foreach (char character in text)
        {
            if (character == '<') { insideTag = true; continue; }
            if (insideTag && character == '>') { insideTag = false; continue; }
            if (!insideTag && !char.IsWhiteSpace(character)) count++;
        }

        return count;
    }
}
```

- [ ] **Step 5: 运行 GREEN 与回归**

重编译后要求：

```text
PASS gal auto advance timing
PASS gal sequence player
```

随后运行 `GalFlowLifecycleProbe.Run()`，确认既有会话逻辑仍 PASS；执行 `git diff --check`。

- [ ] **Step 6: 提交 Task 1**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalAutoAdvanceTiming.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalAutoAdvanceTiming.cs.meta \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Content/GalSequenceSO.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalLineViewModel.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalSequencePlayer.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalAutoAdvanceTimingProbe.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalAutoAdvanceTimingProbe.cs.meta \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalSequencePlayerProbe.cs
git commit -m "feat: add gal auto advance timing"
```

---

### Task 2: 请求契约与模式感知的单会话状态机

**Files:**
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalContracts.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowStateMachine.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalFlowLifecycleProbe.cs`

**Interfaces:**
- Consumes: `GalLineViewModel.AutoAdvanceSecondsOverride`。
- Produces: 扩展后的 `GalPlayRequest`、`GalStartResult`、`GalStopReason`、`GalConcurrentQueueClearedEvent`、`GalFlowStateMachine.ActiveRequest`、`LinePresented`。

- [ ] **Step 1: 写入状态机失败测试**

新增用例必须证明：

```csharp
var request = new GalPlayRequest(
    sequence,
    GalPresentationMode.ConcurrentOverlay,
    "Probe",
    "probe.concurrent",
    1.25f);

int linePresentedCount = 0;
machine.LinePresented += _ => linePresentedCount++;
AssertEqual(GalStartResult.Started, machine.TryStart(request), "Start");

AssertEqual("probe.concurrent", request.RequestKey, "Request key");
AssertApproximately(1.25f, request.DuplicateCooldownSeconds);
AssertEqual(GalPresentationMode.ConcurrentOverlay,
    machine.ActiveRequest.Value.PresentationMode,
    "Active mode");
AssertEqual(1, linePresentedCount, "First line event");

int presentCountBeforeConfirm = view.PresentCount;
view.RaiseConfirmed();
AssertEqual(presentCountBeforeConfirm, view.PresentCount,
    "Concurrent must ignore confirm input");
```

保留 Paused/Fullscreen 的确认推进断言，并新增 `machine.Stop(GalStopReason.Preempted)` 只发布一次 Stopped 的断言。

- [ ] **Step 2: 运行 RED**

Unity 重编译并执行 `GalFlowLifecycleProbe.Run()`；预期因新构造参数、枚举值、`ActiveRequest` 或 `LinePresented` 缺失而失败。

- [ ] **Step 3: 扩展公共契约**

```csharp
public enum GalStartResult
{
    Started, Queued, DuplicateIgnored, QueueFull,
    Busy, InvalidSequence, ViewUnavailable, RejectedByFlow
}

public enum GalStopReason
{
    Completed, Requested, Preempted, ViewLost, Destroyed
}

public readonly struct GalPlayRequest
{
    public GalSequenceSO Sequence { get; }
    public GalPresentationMode PresentationMode { get; }
    public string Source { get; }
    public string RequestKey { get; }
    public float DuplicateCooldownSeconds { get; }

    public GalPlayRequest(
        GalSequenceSO sequence,
        GalPresentationMode presentationMode = GalPresentationMode.PausedOverlay,
        string source = "",
        string requestKey = "",
        float duplicateCooldownSeconds = 1f)
    {
        Sequence = sequence;
        PresentationMode = presentationMode;
        Source = source ?? string.Empty;
        RequestKey = requestKey ?? string.Empty;
        DuplicateCooldownSeconds = Math.Max(0f, duplicateCooldownSeconds);
    }
}
```

增加队列清理消息：

```csharp
public readonly struct GalConcurrentQueueClearedEvent
{
    public int RemovedCount { get; }
    public GalStopReason Reason { get; }

    public GalConcurrentQueueClearedEvent(int removedCount, GalStopReason reason)
    {
        RemovedCount = Math.Max(0, removedCount);
        Reason = reason;
    }
}
```

- [ ] **Step 4: 让状态机保持单一职责并暴露局部观察点**

增加：

```csharp
public GalPlayRequest? ActiveRequest { get; private set; }
public event Action<GalLineViewModel> LinePresented;
```

`TryStart()` 在通过验证后保存完整 request；只有非 Concurrent 模式调用 `SubscribeConfirm()`。`Present()` 在 View 与 EventBus 更新后同步触发 `LinePresented?.Invoke(line)`。`Close()` 在发布生命周期事件前缓存 request/sequence，再把 `ActiveRequest = null`。

- [ ] **Step 5: 运行 GREEN 与三模式回归**

要求 `GalFlowLifecycleProbe.Run()` 返回 `PASS gal flow lifecycle`；再运行 `GalSequencePlayerProbe.Run()` 与 `GalDialogueViewProbe.Run()`。执行 `git diff --check`。

- [ ] **Step 6: 提交 Task 2**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalContracts.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowStateMachine.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalFlowLifecycleProbe.cs
git commit -m "feat: expose gal concurrent session contract"
```

---

### Task 3: Concurrent 调度器与 GalFlowRoot 接线

**Files:**
- Create: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalConcurrentDispatcher.cs`
- Create: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalConcurrentDispatcherProbe.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowRoot.cs`

**Interfaces:**
- Consumes: `GalFlowStateMachine.ActiveRequest`、`LinePresented`、Task 2 的请求/结果/事件。
- Produces: `GalConcurrentDispatcher.Request(GalPlayRequest)`、`Tick(float)`、`Stop(GalStopReason)`、`QueuedCount`、`CurrentLineProgress`、`ProgressChanged`；Root 的 `QueuedConcurrentCount` 与 `ActiveMode` 只读诊断属性。

- [ ] **Step 1: 写入调度器失败 Probe**

使用 FakeView、FakeSuspension 和内存 Sequence，分别验证：

```csharp
var dispatcher = new GalConcurrentDispatcher(machine, events, 2);
var requestA = new GalPlayRequest(
    sequenceA, GalPresentationMode.ConcurrentOverlay,
    "Probe:A", "probe.a", 1f);
var requestB = new GalPlayRequest(
    sequenceB, GalPresentationMode.ConcurrentOverlay,
    "Probe:B", "probe.b", 1f);
var requestC = new GalPlayRequest(
    sequenceC, GalPresentationMode.ConcurrentOverlay,
    "Probe:C", "probe.c", 1f);
var requestD = new GalPlayRequest(
    sequenceD, GalPresentationMode.ConcurrentOverlay,
    "Probe:D", "probe.d", 1f);
var fullscreenRequest = new GalPlayRequest(
    fullscreenSequence, GalPresentationMode.FullscreenExclusive,
    "Probe:Fullscreen");

GalStopReason? lastStoppedReason = null;
events.Subscribe<GalStoppedEvent>(e => lastStoppedReason = e.Reason);

AssertEqual(GalStartResult.Started, dispatcher.Request(requestA));
AssertEqual(GalStartResult.Queued, dispatcher.Request(requestB));
AssertEqual(GalStartResult.DuplicateIgnored, dispatcher.Request(requestB));
AssertEqual(1, dispatcher.QueuedCount);
AssertEqual(GalStartResult.Queued, dispatcher.Request(requestC));
AssertEqual(GalStartResult.QueueFull, dispatcher.Request(requestD));

dispatcher.Tick(GalAutoAdvanceTiming.Calculate(
    sequenceA.Lines[0].Text,
    sequenceA.Lines[0].AutoAdvanceSecondsOverride));
AssertSame(sequenceB, machine.ActiveRequest.Value.Sequence,
    "FIFO must start B after A completes");

AssertEqual(GalStartResult.Started,
    dispatcher.Request(fullscreenRequest),
    "Fullscreen must preempt concurrent");
AssertEqual(0, dispatcher.QueuedCount);
AssertEqual(GalStopReason.Preempted, lastStoppedReason);
```

额外覆盖：高级模式活动时 Concurrent 返回 Busy、冷却过期前后、无效 Sequence 不入队、Stop/Dispose 幂等、进度从 0 到 1、负 Tick 不推进。

- [ ] **Step 2: 运行 RED**

重编译并执行 `GalConcurrentDispatcherProbe.Run()`；预期缺少调度器类型。

- [ ] **Step 3: 实现调度器核心**

类的公共形状固定为：

```csharp
public sealed class GalConcurrentDispatcher : IDisposable
{
    public int QueuedCount { get; }
    public float CurrentLineProgress { get; }
    public event Action<float> ProgressChanged;

    public GalConcurrentDispatcher(
        GalFlowStateMachine stateMachine,
        EventBus galEvents,
        int maxQueuedRequests);

    public GalStartResult Request(GalPlayRequest request);
    public void Tick(float unscaledDeltaTime);
    public void Stop(GalStopReason reason = GalStopReason.Requested);
    public void Dispose();
}
```

实现约束：

- 使用 `Queue<GalPlayRequest>` 和 `HashSet<string>`，不每帧扫描场景；
- 有效 Key 为显式 Key → SequenceId → `GetInstanceID().ToString()`；
- 入队前验证 Sequence；
- 先清队列再 Stop 当前 Concurrent，避免 Session close 回调重启旧队列；
- `LinePresented` 只负责重置计时并调用 `GalAutoAdvanceTiming.Calculate()`；
- `Tick()` 用调用方提供的未缩放增量推进，达到 1 后只调用一次 `Advance()`；
- Sequence 完成后登记冷却并启动下一项；
- 只有实际移除了等待项才发布 `GalConcurrentQueueClearedEvent`；
- Dispose 必须退订 `LinePresented`。

- [ ] **Step 4: 接入 GalFlowRoot**

增加序列化容量及只读诊断属性：

```csharp
[SerializeField, Min(1)] private int maxQueuedConcurrentRequests = 3;
public int QueuedConcurrentCount => dispatcher?.QueuedCount ?? 0;
public GalPresentationMode? ActiveMode =>
    stateMachine?.ActiveRequest?.PresentationMode;
```

`Initialize()` 创建状态机后创建 Dispatcher。`Update()` 仅在初始化后调用 `dispatcher.Tick(Time.unscaledDeltaTime)`。`RequestGal()`、`StopGal()`、`OnDestroy()` 全部通过 Dispatcher，不能绕过调度策略。`ProgressChanged` 到 View 的接线推迟到 Task 4，与真实 `SetConcurrentProgress()` 行为一同完成；不得为了提前编译增加空占位方法。

- [ ] **Step 5: 运行 GREEN 与回归**

要求：

```text
PASS gal concurrent dispatcher
PASS gal flow lifecycle
PASS gal sequence player
```

再运行静态项目编译与 `git diff --check`。

- [ ] **Step 6: 提交 Task 3**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalConcurrentDispatcher.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalConcurrentDispatcher.cs.meta \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowRoot.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalConcurrentDispatcherProbe.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalConcurrentDispatcherProbe.cs.meta
git commit -m "feat: schedule concurrent gal dialogue"
```

---

### Task 4: 通讯卡片 View 行为

**Files:**
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowRoot.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs`

**Interfaces:**
- Consumes: `Show(GalPresentationMode.ConcurrentOverlay)`、`GalLineViewModel`、Dispatcher 的 `ProgressChanged` 0..1 Progress。
- Produces: `GalDialogueView.SetConcurrentProgress(float)`、Root 的进度事件接线/退订与完整三模式显隐矩阵。

- [ ] **Step 1: 扩展 View Probe 并取得 RED**

Probe 构造完整测试层级并反射注入以下新字段：

```text
portraitStage
dialoguePanel
concurrentCard
concurrentCanvasGroup
concurrentAvatarImage
concurrentSpeakerNameText
concurrentDialogueText
concurrentProgressFill
```

调用 `Show(ConcurrentOverlay)` 与 `Present(line)` 后断言：传统节点和 Confirm 隐藏、ConcurrentCard 可见、CanvasGroup 不交互且不挡 Raycast、头像/姓名/正文正确。空头像必须让 AvatarImage disabled。`SetConcurrentProgress(0.4f)` 后进度 fillAmount 精确为 0.4。

运行 `GalDialogueViewProbe.Run()`，预期因字段和方法缺失 RED。

- [ ] **Step 2: 实现显隐矩阵**

`Show(mode)` 必须明确设置每个节点，不能依赖上一模式遗留状态：

```csharp
bool concurrent = mode == GalPresentationMode.ConcurrentOverlay;
bool fullscreen = mode == GalPresentationMode.FullscreenExclusive;
bool paused = mode == GalPresentationMode.PausedOverlay;

fullscreenBackgroundStage.SetActive(fullscreen);
gameplayBackdropDimmer.SetActive(paused);
portraitStage.SetActive(!concurrent);
dialoguePanel.SetActive(!concurrent);
confirmButton.gameObject.SetActive(!concurrent);
concurrentCard.SetActive(concurrent);
```

`Present()` 在 Concurrent 分支写通讯卡片字段并忽略 Background/PortraitSlot；其他模式保持现有行为。`SetConcurrentProgress()` 使用 `Mathf.Clamp01()`。

在 `GalFlowRoot.Initialize()` 中订阅 `dispatcher.ProgressChanged += dialogueView.SetConcurrentProgress`；在重建或销毁 Dispatcher 前对称退订。该接线必须与 View 方法在同一次 GREEN 中落地，避免空占位 API。

- [ ] **Step 3: 实现未缩放淡入淡出**

在 View 内维护一个 Coroutine 引用。Show Concurrent 时从当前 alpha 淡到 1，Hide 时若当前模式为 Concurrent 则用 `Time.unscaledDeltaTime` 在 0.15 秒内淡到 0 后关闭；新的 Show 必须取消旧 Coroutine。非 Concurrent Hide 保持立即关闭，避免改变既有模式节奏。

队列衔接发生在同一帧时，新的 Show 取消尚未渲染的淡出并从当前 alpha 恢复，不能出现一帧闪烁。

- [ ] **Step 4: 运行 GREEN 与回归**

要求 `PASS gal dialogue view`，并重新执行 Lifecycle、Dispatcher 两个 Probe。验证 `IsReady` 在新引用缺失时为 false，且状态机返回 ViewUnavailable、不暂停 Gameplay。

- [ ] **Step 5: 提交 Task 4**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs \
  Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowRoot.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs
git commit -m "feat: add concurrent gal communication card"
```

---

### Task 5: 测试内容、Prefab 原位升级与调试热键

**Files:**
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Integration/GalDebugHotkeyTrigger.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Editor/GalSystemTestContentInstaller.cs`
- Modify in Unity: `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab`
- Modify in Unity: `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab`
- Create in Unity: `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_A.asset`
- Create in Unity: `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_B.asset`

**Interfaces:**
- Consumes: View 新序列化字段、`GalPlayRequest` 新参数、Root Dispatcher。
- Produces: O/Shift+O 可运行竖切片和可重复执行的 Installer。

- [ ] **Step 1: 先扩展 Installer 验证并取得资产 RED**

在 Installer 的验证阶段要求：

- 两个 Concurrent Sequence 存在、模式均为 `ConcurrentOverlay`、各至少两行；
- A/B 的 `SequenceId` 分别为 `gal_test_concurrent_a` / `gal_test_concurrent_b`；
- View Prefab 的八个新引用非空；
- `ConcurrentCard` 锚定左下、安全边距有效；
- `CanvasGroup.blocksRaycasts == false` 且 `interactable == false`；
- Root Prefab Trigger 的 A/B 引用有效；
- 既有 View/Root local fileID 与 nested Prefab source 不变。

重编译后只运行验证入口，预期明确报告缺少 Concurrent assets/fields，而不是自动跳过。

- [ ] **Step 2: 扩展调试热键**

为 Trigger 增加 `concurrentTestSequenceA/B`。Update 中分别检测 P 与 O；O 分支构造：

```csharp
bool shift = Keyboard.current.leftShiftKey.isPressed ||
    Keyboard.current.rightShiftKey.isPressed;
GalSequenceSO sequence = shift
    ? concurrentTestSequenceB
    : concurrentTestSequenceA;
string key = shift ? "debug.concurrent.b" : "debug.concurrent.a";

new GalPlayRequest(
    sequence,
    GalPresentationMode.ConcurrentOverlay,
    shift ? "DebugHotkey:Shift+O" : "DebugHotkey:O",
    key,
    1f);
```

`Queued` 与 `DuplicateIgnored` 属于预期调度结果，不打印 Warning；`QueueFull` 与真正错误仍打印。

- [ ] **Step 3: 原位创建/更新两个 Sequence**

Installer 通过 `SerializedObject` 写入稳定 ID、`defaultPresentationMode=ConcurrentOverlay`、复用现有 Anime actor，并写入短句：

```text
A1：前方区域信号有点奇怪，小心一点。
A2：我会继续监控，有变化马上告诉你。
B1：补充情报：右侧通道检测到移动目标。
B2：先别停下，我正在确认它的身份。
```

A1/A2 使用自动时长，B1/B2 使用 1.2 秒覆盖，确保两条路径都进入真机验收。

- [ ] **Step 4: 原位升级 PF_GalDialogueView**

使用 `PrefabUtility.LoadPrefabContents(ViewPrefabPath)`；只在节点缺失时创建 `VisualRoot/ConcurrentCard`，不得重建 VisualRoot 或既有节点。

卡片采用 1920×1080 参考分辨率下约 560×150、左下边距 36×36；包含半透明 Image、AvatarMask、AvatarImage、姓名 TMP、两行正文 TMP 和底部进度 Image。使用 LayoutGroup 让头像隐藏后文本自然扩展；所有非交互 Graphic 的 `raycastTarget=false`。

用 `SerializedObject` 精确回填 Task 4 的八个新字段后保存同一路径并 `UnloadPrefabContents`。

- [ ] **Step 5: 原位升级 PF_GalFlowRoot 与场景连接**

Load Root Prefab contents，复用现有 nested `PF_GalDialogueView` 与 Trigger，只补 A/B 引用和 Root 的 queue capacity=3。保存后核对旧 Root/Trigger local fileID 未变化。

场景只允许修复缺失的 `GameFlowRoot.galFlow` 或 Trigger arbitrator 连接。活动场景原本为 dirty 时不得调用 SaveScene；不得产生 A/B Sequence 的场景 override。

- [ ] **Step 6: 运行 Installer 两次验证幂等**

通过 Unity MCP 调用 `GalSystemTestContentInstaller.InstallTestVerticalSlice()` 两次。两次都必须 PASS；第二次不新增 GameObject、Component、Prefab modification 或 Scene dirty 差异。回读 Prefab、SO 和场景连接，确认 P/Shift+P 引用仍有效。

- [ ] **Step 7: 提交 Task 5**

只 stage GalSystem 文件：

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Integration/GalDebugHotkeyTrigger.cs \
  Assets/Script/GameFlowManager/GalSystem/Editor/GalSystemTestContentInstaller.cs \
  Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab \
  Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab \
  Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_A.asset \
  Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_A.asset.meta \
  Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_B.asset \
  Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Concurrent_B.asset.meta
git commit -m "feat: install concurrent gal test content"
```

---

### Task 6: PlayMode 验收与回归证据

**Files:**
- Modify: `Assets/Script/GameFlowManager/GalSystem/Editor/GalPlayModeVerificationRunner.cs`
- Create: `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalConcurrentOverlay.png`
- Create: `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-concurrent-overlay-verification.md`

**Interfaces:**
- Consumes: 完成后的 Root、Dispatcher 诊断属性、两条测试 Sequence 与通讯卡片 Prefab。
- Produces: 三模式完整验收、截图和可审计验证记录。

- [ ] **Step 1: 先扩展 EditMode Composition Probe**

在进入 Play 前验证：

- 两个 Concurrent Sequence 的 ID、模式、行数和时长覆盖；
- View Prefab 精确层级、引用、CanvasGroup 和 Raycast 配置；
- Root Prefab 的 Dispatcher 容量和 Trigger A/B 引用；
- 活动场景仍只有一个 GameFlowRoot、GalFlowRoot、GalDialogueView 与 Gal Canvas；
- 场景 GalFlowRoot 仍来自 `PF_GalFlowRoot`，arbitrator override 有效；
- Character 源码和场景不由本任务写入。

运行该 Probe，若任何资产不符必须 RED 并给出精确路径。

- [ ] **Step 2: 加入 Concurrent PlayMode 轮次**

在既有 Paused/Fullscreen 回归之前增加：

1. 记录初始 `Time.timeScale`、Hunt pause、Move authority 与角色位置；
2. 请求 A，断言 `Started`、ActiveMode=Concurrent、卡片显示且其他 Gal 节点隐藏；
3. 再请求 A，断言 `DuplicateIgnored`；
4. 请求 B，断言 `Queued` 且 `QueuedConcurrentCount==1`；
5. 在 A 播放期间注入持续移动输入，断言角色位置发生变化；
6. 逐帧等待 A 自动完成并确认 B 自动开始；
7. 截图 `Tests/Artifacts/GalConcurrentOverlay.png`；
8. B 活动时请求 Fullscreen，断言 B 收到 Preempted、队列为 0、全屏立即显示；
9. 完成全屏后等待至少一个旧 B 时长，断言 Concurrent 不恢复；
10. 继续执行原 Paused/Fullscreen 验收轮次。

所有等待使用未缩放编辑器/运行时时间并设置明确超时；不得用固定长 Sleep。

- [ ] **Step 3: 运行全部 Editor Probes**

依次要求：

```text
PASS gal auto advance timing
PASS gal sequence player
PASS gal flow lifecycle
PASS gal concurrent dispatcher
PASS gal dialogue view
PASS gal edit mode composition
```

- [ ] **Step 4: 运行 PlayMode Verifier**

通过 Unity MCP 启动 Runner。Play 期间 MCP 短断按项目 workflow 轮询恢复；Runner 自行完成并退出 Play 后检查 Console，要求明确：

```text
PASS Gal ConcurrentOverlay + PausedOverlay + FullscreenExclusive PlayMode verification
```

并目视检查截图：卡片位于左下，不遮挡角色与准心，头像裁切可辨认，姓名/两行正文/进度条清楚。

- [ ] **Step 5: 写入验证文档**

验证文档必须记录：

- Tuanjie 版本与测试场景；
- 每个 Probe 的原始 PASS 文本；
- PlayMode 最终 PASS 文本；
- A→B FIFO、重复去重、全屏抢占不恢复的观察结果；
- `Time.timeScale`、Hunt pause、Move authority 的前后值；
- Installer 连续两次运行的幂等结果；
- 截图相对路径；
- 当前场景原本已脏且本任务未保存它。

- [ ] **Step 6: 最终范围审计**

运行：

```bash
git diff --check
git status --short
git diff --name-only HEAD~1
```

确认没有 Character 文件或 scene 被本阶段提交；重新打开两张既有 Gal 截图与新 Concurrent 截图，确认没有空白或错误模式。

- [ ] **Step 7: 提交 Task 6**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Editor/GalPlayModeVerificationRunner.cs \
  Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalConcurrentOverlay.png \
  Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalConcurrentOverlay.png.meta \
  Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-concurrent-overlay-verification.md \
  Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-concurrent-overlay-verification.md.meta
git commit -m "test: verify concurrent gal overlay"
```

---

## Completion Gate

本阶段只有同时满足以下条件才算完成：

- 六个 Editor Probe 全部真机 PASS；
- Concurrent、Paused、Fullscreen 三模式 PlayMode 验收全部 PASS；
- O/Shift+O/P/Shift+P 均可用；
- Gameplay 在 Concurrent 期间继续运行且输入未被 UI 截获；
- FIFO、去重、冷却、QueueFull 和高级模式抢占都有自动化证据；
- Prefab Installer 连续运行两次幂等；
- 场景与 Character 的用户改动未被保存或提交；
- 新截图和中文验证文档已提交。

## Self-Review Record

- Spec coverage：设计文档第 1～12 节均映射到 Task 1～6，无遗漏。
- Placeholder scan：计划不包含 TBD、TODO、模糊的“适当处理”或未定义接口。
- Type consistency：`AutoAdvanceSecondsOverride`、`RequestKey`、`DuplicateCooldownSeconds`、`GalConcurrentDispatcher`、`ProgressChanged`、`QueuedConcurrentCount`、`Preempted` 在生产与测试任务中名称一致。
- Scope：只扩展 GalSystem；Character 和场景用户改动被列为显式禁区。
