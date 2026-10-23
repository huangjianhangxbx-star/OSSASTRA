# Gal 全屏 AVG 第二阶段实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不复制 Gal 状态机、不修改角色控制器的前提下，交付由 `Shift + P` 启动、带原创二次元立绘与独立背景、可逐句推进并完整恢复 Gameplay 的 `FullscreenExclusive` 全屏 AVG 垂直切片。

**Architecture:** 继续使用 `GameFlowArbitrator → GalFlowRoot → GalFlowStateMachine` 的单一会话链路。内容层解析“行背景覆盖 → 序列默认背景 → null”，同一个 `GalDialogueView` 根据 `GalPresentationMode` 切换压暗层与全屏背景层；Prefab 和 ScriptableObject 只通过 Unity Editor API 原位更新。

**Tech Stack:** Tuanjie 2022.3.62t12、C#、Unity UI、TextMeshPro、Input System、ScriptableObject、EventBus、Funplay Unity MCP、内置 ImageGen。

**Spec:** `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-design.md`

## Global Constraints

- `P` 必须继续启动现有 `PausedOverlay`；`Shift + P` 只启动 `FullscreenExclusive`，一次按键只产生一个请求。
- 正式入口保持 `GameFlowArbitrator.RequestGal(GalPlayRequest)`，不增加第二个 Root、状态机、仲裁器或 EventCenter 引用。
- `GalSequenceSO.DefaultPresentationMode` 只表达内容作者意图；运行时模式仍由 `GalPlayRequest.PresentationMode` 显式决定。
- `null` 背景是合法内容：全屏 View 显示不透明纯黑，对话继续；缺少背景组件引用才是 `ViewUnavailable`。
- 不修改 `Character`、Hunt 状态机、武器、移动、朝向、动画或 DataBus 结构。
- 不手工编辑 `.unity`、`.prefab`、`.asset` 或 `.meta` 文本；所有序列化资产修改通过 Unity Editor API 完成。
- 当前活动场景已有无关 Git 差异且处于脏状态；不得调用 `SaveOpenScenes`，不得自动保存该场景。
- 更新 `PF_GalDialogueView` 与 `PF_GalFlowRoot` 时保留现有层级、local fileID、Prefab 连接及场景中的 `gameFlowArbitrator` override。
- 保留旧 `SPR_GalPortrait_Test.png`；新立绘使用新文件名，不做破坏性删除。
- 每次提交只暂存本任务列出的 GalSystem 文件，绝不纳入现有 Character 或场景差异。

## File Map

**运行时修改：**

- `Assets/Script/GameFlowManager/GalSystem/Runtime/Content/GalSequenceSO.cs`：序列默认背景与行背景覆盖。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalLineViewModel.cs`：解析后的背景。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalSequencePlayer.cs`：背景优先级解析。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalContracts.cs`：View 打开接口接收模式。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowStateMachine.cs`：把请求模式传入 View。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs`：全屏背景、纯黑兜底和模式显隐。
- `Assets/Script/GameFlowManager/GalSystem/Runtime/Integration/GalDebugHotkeyTrigger.cs`：`P` / `Shift + P` 互斥分流。

**测试与编辑器修改：**

- `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalSequencePlayerProbe.cs`
- `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalFlowLifecycleProbe.cs`
- `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs`（新建）
- `Assets/Script/GameFlowManager/GalSystem/Editor/GalSystemTestContentInstaller.cs`
- `Assets/Script/GameFlowManager/GalSystem/Editor/GalPlayModeVerificationRunner.cs`

**由 Unity 创建或更新：**

- `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab`
- `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab`
- `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalActor_Test.asset`
- `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test.asset`
- `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset`（新建）

**新美术与证据：**

- `Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png`
- `Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png`
- `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png`
- `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-verification.md`

---

### Task 1: 背景内容契约与解析优先级

**Files:**

- Modify: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalSequencePlayerProbe.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Content/GalSequenceSO.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalLineViewModel.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalSequencePlayer.cs`

**Interfaces:**

- Produces: `GalSequenceSO.DefaultBackground : Sprite`
- Produces: `GalLine.BackgroundOverride : Sprite`
- Produces: `GalLineViewModel.Background : Sprite`
- Produces: `GalSequencePlayer` 的固定解析顺序：行覆盖、序列默认、`null`

- [ ] **Step 1: 写入背景解析失败用例**

在 `GalSequencePlayerProbe.Run()` 中加入 `ResolvesBackgroundPriority()`，并实现以下测试主体：

```csharp
private static void ResolvesBackgroundPriority()
{
    Texture2D texture = new Texture2D(8, 8);
    Sprite sequenceBackground = Sprite.Create(
        texture,
        new Rect(0f, 0f, 8f, 8f),
        new Vector2(0.5f, 0.5f));
    Sprite lineBackground = Sprite.Create(
        texture,
        new Rect(0f, 0f, 8f, 8f),
        new Vector2(0.5f, 0.5f));
    GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
    GalSequenceSO noBackgroundSequence =
        ScriptableObject.CreateInstance<GalSequenceSO>();

    try
    {
        SetPrivateField(sequence, "defaultBackground", sequenceBackground);
        SetPrivateField(
            sequence,
            "lines",
            new List<GalLine>
            {
                new GalLine(
                    null,
                    "使用行覆盖",
                    backgroundOverride: lineBackground),
                new GalLine(null, "使用序列默认")
            });
        SetPrivateField(
            noBackgroundSequence,
            "lines",
            new List<GalLine> { new GalLine(null, "允许空背景") });

        var player = new GalSequencePlayer();
        AssertTrue(player.TryBegin(sequence, out GalLineViewModel first),
            "Background sequence must start");
        AssertSame(lineBackground, first.Background,
            "Line background must beat the sequence default");
        AssertTrue(player.TryAdvance(out GalLineViewModel second),
            "The default-background line must be available");
        AssertSame(sequenceBackground, second.Background,
            "Sequence default must be used without a line override");

        AssertTrue(player.TryBegin(noBackgroundSequence, out GalLineViewModel empty),
            "A missing background must not reject dialogue");
        AssertSame(null, empty.Background,
            "Missing line and sequence backgrounds must remain null");
        AssertEqual("允许空背景", empty.Text,
            "Missing background must not discard dialogue text");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(noBackgroundSequence);
        UnityEngine.Object.DestroyImmediate(sequence);
        UnityEngine.Object.DestroyImmediate(lineBackground);
        UnityEngine.Object.DestroyImmediate(sequenceBackground);
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
```

- [ ] **Step 2: 编译并确认 RED 原因准确**

通过 Unity MCP 执行 `request_recompile`、`wait_for_compilation`、
`get_compilation_errors`。预期编译失败只来自新契约尚不存在，例如：

```text
CS1739: GalLine constructor has no parameter named backgroundOverride
CS1061: GalLineViewModel does not contain a definition for Background
```

- [ ] **Step 3: 实现最小内容契约**

在 `GalSequenceSO.cs` 中加入：

```csharp
[SerializeField] private Sprite backgroundOverride;
public Sprite BackgroundOverride => backgroundOverride;
```

把 `GalLine` 构造函数末尾扩展为：

```csharp
GalPortraitSlot portraitSlot = GalPortraitSlot.Left,
Sprite backgroundOverride = null
```

并赋值 `this.backgroundOverride = backgroundOverride;`。在 `GalSequenceSO` 中加入：

```csharp
[SerializeField] private Sprite defaultBackground;
public Sprite DefaultBackground => defaultBackground;
```

在 `GalLineViewModel` 中加入：

```csharp
public Sprite Background { get; }
```

构造函数末尾增加 `Sprite background = null`，并赋值：

```csharp
Background = background;
```

把播放器解析函数改成：

```csharp
private static GalLineViewModel ResolveLine(
    GalSequenceSO sequence,
    GalLine line,
    int lineIndex)
{
    Sprite background = line != null && line.BackgroundOverride != null
        ? line.BackgroundOverride
        : sequence != null
            ? sequence.DefaultBackground
            : null;

    if (line == null)
    {
        return new GalLineViewModel(
            lineIndex,
            string.Empty,
            string.Empty,
            null,
            GalPortraitSlot.Left,
            background);
    }

    GalActorSO actor = line.Actor;
    string speakerName = !string.IsNullOrWhiteSpace(line.SpeakerNameOverride)
        ? line.SpeakerNameOverride
        : actor != null
            ? actor.DisplayName
            : string.Empty;
    Sprite portrait = line.PortraitOverride != null
        ? line.PortraitOverride
        : actor != null
            ? actor.ResolvePortrait(line.PortraitVariantKey)
            : null;

    return new GalLineViewModel(
        lineIndex,
        speakerName,
        line.Text,
        portrait,
        line.PortraitSlot,
        background);
}
```

`TryBegin` 和 `TryAdvance` 都调用 `ResolveLine(activeSequence, line, index)`。

- [ ] **Step 4: 重编译并运行 Probe**

Unity MCP `execute_code` 使用：

```csharp
public static class RunGalSequencePlayerProbe
{
    public static string Run()
    {
        return GalSequencePlayerProbe.Run();
    }
}
```

预期精确结果：

```text
PASS gal sequence player
```

- [ ] **Step 5: 提交背景内容契约**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Content/GalSequenceSO.cs Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalLineViewModel.cs Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalSequencePlayer.cs Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalSequencePlayerProbe.cs
git commit -m "feat: add gal background content contract"
```

---

### Task 2: 把播放模式传递到 View

**Files:**

- Modify: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalFlowLifecycleProbe.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalContracts.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowStateMachine.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs`

**Interfaces:**

- Produces: `IGalDialogueView.Show(GalPresentationMode mode)`
- Preserves: `GalPlayRequest`、Gal Event 类型和 `RequestGal()` 公共签名

- [ ] **Step 1: 写入模式传递失败用例**

在 `GalFlowLifecycleProbe.Run()` 中加入
`PassesFullscreenExclusiveToViewAndSuspension()`：

```csharp
private static void PassesFullscreenExclusiveToViewAndSuspension()
{
    GalSequenceSO sequence = CreateSequence("全屏测试");
    var view = new FakeView();
    var suspension = new FakeSuspensionService();
    var machine = new GalFlowStateMachine(view, suspension, new EventBus());

    try
    {
        AssertEqual(
            GalStartResult.Started,
            machine.TryStart(new GalPlayRequest(
                sequence,
                GalPresentationMode.FullscreenExclusive,
                "FullscreenProbe")),
            "FullscreenExclusive must start");
        AssertEqual(
            GalPresentationMode.FullscreenExclusive,
            view.LastShownMode,
            "View must receive the requested mode");
        AssertEqual(
            GalPresentationMode.FullscreenExclusive,
            suspension.LastRequestedMode,
            "Suspension must receive the requested mode");

        machine.Stop(GalStopReason.Requested);
        machine.Stop(GalStopReason.Requested);
        machine.Dispose();
        AssertEqual(1, suspension.ReleaseCount,
            "Fullscreen suspension must release exactly once");
        AssertEqual(1, view.HideCount,
            "Fullscreen view must hide exactly once");
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(sequence);
    }
}
```

`FakeView` 增加：

```csharp
public GalPresentationMode LastShownMode { get; private set; }

public void Show(GalPresentationMode mode)
{
    ShowCount++;
    LastShownMode = mode;
}
```

`FakeSuspensionService` 增加：

```csharp
public GalPresentationMode LastRequestedMode { get; private set; }
```

并在 `TryAcquire` 第一行写入 `LastRequestedMode = mode;`。现有
`PausedOverlay` 用例同时断言两个 Fake 收到 `PausedOverlay`。

- [ ] **Step 2: 编译并确认 RED 原因准确**

预期编译失败说明 `FakeView` 尚未实现无参数 `IGalDialogueView.Show()`，或接口尚未接受
`GalPresentationMode`；不得用跳过测试的方式消除失败。

- [ ] **Step 3: 修改 View 契约与状态机调用**

在 `GalContracts.cs` 中改为：

```csharp
public interface IGalDialogueView
{
    bool IsReady { get; }
    event Action Confirmed;
    void Show(GalPresentationMode mode);
    void Hide();
    void Present(GalLineViewModel line);
}
```

在 `GalFlowStateMachine.TryStart()` 中改为：

```csharp
SubscribeConfirm();
view.Show(request.PresentationMode);
State = GalFlowState.PresentingLine;
```

同步把 `GalDialogueView` 的公开方法签名改为
`Show(GalPresentationMode mode)`；本任务中方法体仍只激活 `visualRoot`，视觉分支放入
Task 4。

- [ ] **Step 4: 重编译并回归两个 Core Probe**

分别用 `execute_code` 调用：

```csharp
public static class RunGalCoreProbes
{
    public static string Run()
    {
        return GalSequencePlayerProbe.Run() + " | " +
            GalFlowLifecycleProbe.Run();
    }
}
```

预期：

```text
PASS gal sequence player | PASS gal flow lifecycle
```

- [ ] **Step 5: 提交模式契约**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalContracts.cs Assets/Script/GameFlowManager/GalSystem/Runtime/Core/GalFlowStateMachine.cs Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalFlowLifecycleProbe.cs
git commit -m "feat: pass gal presentation mode to view"
```

---

### Task 3: 生成原创立绘与全屏背景

**Files:**

- Create: `Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png`
- Create: `Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png`
- Preserve: `Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test.png`

**Interfaces:**

- Produces: 带 Alpha 的原创角色 Sprite 源 PNG
- Produces: 无人物、无文字的原创横向 AVG 背景 PNG

- [ ] **Step 1: 建立生成暂存目录与项目背景目录**

用 `New-Item -ItemType Directory -Force` 创建：

```text
E:\WORLDCREATOR\new\Project_VELGITA\tmp\imagegen
E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script\GameFlowManager\GalSystem\Content\Test\Backgrounds
```

不把绿色底中间文件放进 `Assets`。

- [ ] **Step 2: 用内置 ImageGen 生成原创角色绿色底源图**

把用户两张参考图作为 `referenced_image_paths`，角色 Prompt 使用：

```text
Use case: stylized-concept
Asset type: Unity fullscreen visual-novel character portrait Sprite
Primary request: Create one original adult female anime character for a fullscreen AVG dialogue test. The result must be an original character, not a reproduction of either reference character.
Input images: Image 1 supplies only broad facial traits: short blonde bob, light blue eyes, and a slightly playful self-confident expression. Image 2 supplies only broad rendering language and fashion direction: polished contemporary Japanese anime illustration, crisp line art, refined cel shading, red-and-black military-inspired clothing, and a black beret. Do not copy either identity, recognizable costume, accessory, mascot, insignia, pose, or composition.
Subject: An original adult woman with a short golden-blonde bob, light blue eyes, and a calm subtly confident expression. Original structured crimson military-inspired jacket with black trim, restrained gold fasteners, black beret, and dark high-neck inner top. No logos, medals, mascots, or franchise elements.
Style/medium: Pure contemporary Japanese 2D anime illustration, clean expressive line art, polished commercial-quality cel shading, delicate facial rendering; not photorealistic, not 3D, not pixel art.
Composition/framing: Portrait canvas, three-quarter front view, natural relaxed upright pose, head to upper thighs. Full beret, hair silhouette, shoulders, arms, and clothing contour visible. Generous padding at top and sides; lower crop reaches the canvas bottom naturally for a bottom-center pivot.
Lighting/mood: Soft neutral key light with restrained warm accents; expression readable in dialogue UI.
Scene/backdrop: Perfectly flat solid #00ff00 chroma-key background, one uniform color with no shadow, gradient, texture, reflection, or floor plane.
Constraints: Subject fully separated from background with crisp edges. No green anywhere on the character. Opaque clothing. No cast shadow, reflection, text, watermark, frame, UI, or speech bubble.
Avoid: copied identity, exact costume, emblems, cropped head or arms, malformed hands, extra fingers, realism, painterly texture, green rim light.
```

将工具返回的选中结果复制到：

```text
E:\WORLDCREATOR\new\Project_VELGITA\tmp\imagegen\SPR_GalPortrait_Test_Anime_Keyed.png
```

- [ ] **Step 3: 移除绿色底并验证 Alpha**

执行：

```powershell
python "C:\Users\Administrator\.codex\skills\.system\imagegen\scripts\remove_chroma_key.py" `
  --input "E:\WORLDCREATOR\new\Project_VELGITA\tmp\imagegen\SPR_GalPortrait_Test_Anime_Keyed.png" `
  --out "E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script\GameFlowManager\GalSystem\Content\Test\Portraits\SPR_GalPortrait_Test_Anime.png" `
  --auto-key border `
  --soft-matte `
  --transparent-threshold 12 `
  --opaque-threshold 220 `
  --despill
```

用 `view_image` 检查完整头发、贝雷帽、手臂和服装轮廓；四角必须透明，主体必须不透明，
发丝不得有明显绿边。若只有一圈薄绿边，重跑一次并追加 `--edge-contract 1`。若两轮仍
不能通过，停止并征求用户是否允许使用需要 `OPENAI_API_KEY` 的原生透明 CLI 回退，
不得静默切换模型路径。

- [ ] **Step 4: 用内置 ImageGen 生成全屏 AVG 背景**

这是无参考图的新生成，Prompt 使用：

```text
Use case: illustration-story
Asset type: fullscreen 16:9 Unity AVG background
Primary request: Create one original anime visual-novel background showing the interior of an old abandoned passenger train carriage at dusk.
Scene/backdrop: A believable disused carriage with worn seats, aged metal fittings, dusty windows, and subtle long-term abandonment. Cold blue dusk outside; a few weak amber carriage lights create restrained warm-and-cool contrast. No people, creatures, silhouettes, readable signs, or text.
Style/medium: Polished Japanese anime background illustration with crisp perspective, clean shapes, controlled detail, and refined cel-anime color design. Not photorealistic, not 3D, not pixel art.
Composition/framing: Exact 16:9 landscape, eye-level visual-novel camera, stable perspective. Calm visual space across middle-left and middle-right for portraits. Lower 30 percent low-contrast and free of critical focal objects. Important structures remain inside the central 80 percent for safe EnvelopeParent cropping.
Lighting/mood: Quiet and slightly mysterious dusk; cool exterior light and subdued warm interior light.
Constraints: One empty environment. No characters, animals, mascots, logo, text, watermark, UI, frame, vignette, fisheye, or tilted horizon.
```

将选中结果保存为：

```text
E:\WORLDCREATOR\new\Project_VELGITA\Assets\Script\GameFlowManager\GalSystem\Content\Test\Backgrounds\BG_GalFullscreen_Test.png
```

读取实际宽高并验证宽高比。若不是 16:9，使用 ImageGen 再生成一次，追加“the actual
canvas itself must be 16:9, not a 16:9 picture letterboxed inside another ratio”；不得用
非等比拉伸修正。第二次仍不是精确 16:9 时保留构图最好的版本，由
`AspectRatioFitter.EnvelopeParent` 做等比裁边，并在最终验证记录中写明实际尺寸。

- [ ] **Step 5: 视觉检查两张最终 PNG**

使用 `view_image` 逐张检查：

- 立绘为原创成年角色，纯二次元赛璐璐风格，无绿色底、文字或水印；
- 背景为无人列车车厢，整体横向构图，底部对话框区域没有关键焦点；
- 背景与立绘的冷暖、线条精细度不会产生明显风格冲突；
- 两张最终文件均位于 `Assets/Script/GameFlowManager/GalSystem/Content/Test` 内。

- [ ] **Step 6: 让 Unity 生成正规 `.meta` 并提交原始美术**

调用 `wait_for_compilation(force_refresh: true)` 触发导入，不手写 `.meta`。只暂存两张新
PNG及 Unity 实际生成的对应 `.meta`：

```bash
git add Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png.meta Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png.meta
git commit -m "art: add fullscreen gal test visuals"
```

---

### Task 4: 实现单 View 的模式视觉行为

**Files:**

- Create: `Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs`

**Interfaces:**

- Consumes: `IGalDialogueView.Show(GalPresentationMode)`、`GalLineViewModel.Background`
- Produces: `PausedOverlay`、`FullscreenExclusive`、`ConcurrentOverlay` 的确定性显隐规则

- [ ] **Step 1: 新建实际 View 的失败 Probe**

创建 `GalDialogueViewProbe.cs`，用临时 UI 对象组装 `GalDialogueView`。核心断言必须为：

```csharp
view.Show(GalPresentationMode.PausedOverlay);
AssertTrue(visualRoot.activeSelf, "Paused view must open");
AssertFalse(fullscreenStage.activeSelf, "Paused view must hide fullscreen stage");
AssertTrue(dimmer.activeSelf, "Paused view must show dimmer");

view.Show(GalPresentationMode.FullscreenExclusive);
AssertTrue(fullscreenStage.activeSelf, "Fullscreen view must show background stage");
AssertFalse(dimmer.activeSelf, "Fullscreen view must hide gameplay dimmer");
view.Present(new GalLineViewModel(
    0,
    "测试角色",
    "有背景",
    null,
    GalPortraitSlot.Left,
    backgroundSprite));
AssertSame(backgroundSprite, backgroundImage.sprite,
    "Fullscreen background Sprite must be presented");
AssertEqual(Color.white, backgroundImage.color,
    "A real background must render without tint");

view.Present(new GalLineViewModel(
    1,
    "测试角色",
    "黑底兜底",
    null,
    GalPortraitSlot.Left,
    null));
AssertSame(null, backgroundImage.sprite,
    "Null content must clear stale background Sprite");
AssertEqual(Color.black, backgroundImage.color,
    "Null fullscreen content must become opaque black");
AssertTrue(backgroundImage.enabled,
    "Black fallback Image must remain enabled");

view.Show(GalPresentationMode.ConcurrentOverlay);
AssertFalse(fullscreenStage.activeSelf,
    "Concurrent view must hide fullscreen stage");
AssertFalse(dimmer.activeSelf,
    "Concurrent view must not dim Gameplay");
```

Probe 通过反射设置以下字段：`visualRoot`、`fullscreenBackgroundStage`、
`fullscreenBackgroundImage`、`fullscreenBackgroundFitter`、
`gameplayBackdropDimmer`、`portraitImage`、`speakerNameText`、`dialogueText`、
`confirmButton`。`Run()` 在全部断言通过后返回 `PASS gal dialogue view`；测试结束时
`DestroyImmediate` 临时 GameObject、Sprite 和 Texture2D。反射字段不存在时立即抛出
`Missing test setup field: <fieldName>`，不跳过断言。

- [ ] **Step 2: 运行 Probe 并确认 RED**

通过 `execute_code` 调用：

```csharp
public static class RunGalDialogueViewProbe
{
    public static string Run()
    {
        return GalDialogueViewProbe.Run();
    }
}
```

预期失败为新增序列化字段不存在或 `IsReady`/显隐断言不满足；Core Probe 必须仍为 PASS。

- [ ] **Step 3: 增加 View 字段与就绪条件**

在 `GalDialogueView` 增加：

```csharp
[SerializeField] private GameObject fullscreenBackgroundStage;
[SerializeField] private Image fullscreenBackgroundImage;
[SerializeField] private AspectRatioFitter fullscreenBackgroundFitter;
[SerializeField] private GameObject gameplayBackdropDimmer;

private GalPresentationMode activeMode;
```

`IsReady` 必须额外要求这四个引用不为 `null`。内容背景 Sprite 为 `null` 不影响
`IsReady`。

- [ ] **Step 4: 实现模式显隐和背景兜底**

```csharp
public void Show(GalPresentationMode mode)
{
    activeMode = mode;
    bool fullscreen = mode == GalPresentationMode.FullscreenExclusive;
    bool pausedOverlay = mode == GalPresentationMode.PausedOverlay;

    fullscreenBackgroundStage.SetActive(fullscreen);
    gameplayBackdropDimmer.SetActive(pausedOverlay);

    if (fullscreen)
        ApplyBackground(null);
    else
        fullscreenBackgroundImage.enabled = false;

    visualRoot.SetActive(true);
}

private void ApplyBackground(Sprite background)
{
    if (activeMode != GalPresentationMode.FullscreenExclusive)
        return;

    fullscreenBackgroundImage.enabled = true;
    fullscreenBackgroundImage.sprite = background;
    fullscreenBackgroundImage.preserveAspect = true;
    fullscreenBackgroundImage.color = background != null
        ? Color.white
        : Color.black;
    fullscreenBackgroundFitter.aspectRatio =
        background != null && background.rect.height > 0f
            ? background.rect.width / background.rect.height
            : 16f / 9f;
}
```

在 `Present(GalLineViewModel line)` 完成文字和立绘赋值后调用：

```csharp
ApplyBackground(line.Background);
```

- [ ] **Step 5: 重编译并运行三个 Probe**

预期：

```text
PASS gal sequence player
PASS gal flow lifecycle
PASS gal dialogue view
```

- [ ] **Step 6: 提交 View 行为**

Unity 生成新测试文件 `.meta` 后提交：

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Presentation/GalDialogueView.cs Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs Assets/Script/GameFlowManager/GalSystem/Tests/Editor/GalDialogueViewProbe.cs.meta
git commit -m "feat: add fullscreen gal view mode"
```

---

### Task 5: 先写双模式 Unity 验收器

**Files:**

- Modify: `Assets/Script/GameFlowManager/GalSystem/Editor/GalPlayModeVerificationRunner.cs`

**Interfaces:**

- Produces: 可单独运行的 Edit Mode composition Probe
- Produces: 同一轮先 `P`、后 `Shift + P` 的完整 Play Mode 验收状态机

- [ ] **Step 1: 扩展 Edit Mode 资产断言**

将 Armed Key 升级为：

```csharp
private const string ArmedKey = "ProjectANOIR.GalPlayModeVerifier.Armed.V3";
```

加入以下路径并在 `VerifyEditModeComposition()` 中逐一加载：

```csharp
const string animePortraitPath =
    "Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png";
const string backgroundPath =
    "Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png";
const string fullscreenSequencePath =
    "Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset";
```

通过 `SerializedObject` 断言 `GalDialogueView` 的四个新增引用均不为空；断言 Prefab 精确
包含 `VisualRoot/FullscreenBackgroundStage/BackgroundImage`，且背景节点位于
`GameplayBackdropDimmer` 后方。为 TDD 增加公开入口：

```csharp
public static string RunEditModeCompositionProbe()
{
    VerifyEditModeComposition();
    return "PASS gal fullscreen edit-mode composition";
}
```

- [ ] **Step 2: 扩展 Play Mode 为两个顺序场景**

保留第一阶段 P 流程，再追加全屏流程。截图常量拆为：

```csharp
private const string PausedScreenshotPath =
    ArtifactFolder + "/GalPausedOverlay.png";
private const string FullscreenScreenshotPath =
    ArtifactFolder + "/GalFullscreenExclusive.png";
```

组合键必须一次排队：

```csharp
InputSystem.QueueStateEvent(
    Keyboard.current,
    new KeyboardState(Key.LeftShift, Key.P));
```

不能先单独排队 P。第一轮关闭并验证恢复后再进入第二轮。状态枚举至少区分：

```csharp
WaitingForPausedDialogue,
WaitingForPausedSecondLine,
WaitingForPausedThirdLine,
WaitingForPausedClose,
WaitingToQueueFullscreen,
WaitingForFullscreenDialogue,
WaitingForFullscreenScreenshot,
WaitingForFullscreenSecondLine,
WaitingForFullscreenThirdLine,
WaitingForFullscreenClose
```

- [ ] **Step 3: 写入两种模式的视觉断言**

Paused 开始时要求：

```csharp
fullscreenStage.activeSelf == false
dimmer.activeSelf == true
```

Fullscreen 开始时要求：

```csharp
fullscreenStage.activeSelf == true
dimmer.activeSelf == false
backgroundImage.enabled == true
backgroundImage.sprite != null
backgroundImage.color.a == 1f
canvas.renderMode == RenderMode.ScreenSpaceOverlay
canvas.sortingOrder >= 5000
```

两轮都检查 `Time.timeScale == 0`、Hunt 暂停、Move 权限封锁、三句 NEXT 推进和关闭后
恢复。最终成功日志固定为：

```text
[GalPlayModeVerifier] PASS P PausedOverlay and Shift+P FullscreenExclusive, pause, presentation, three-line Confirm flow, cleanup and restore.
```

- [ ] **Step 4: 运行 Edit Mode Probe 并确认 RED**

```csharp
public static class RunGalFullscreenCompositionProbe
{
    public static string Run()
    {
        return GalPlayModeVerificationRunner.RunEditModeCompositionProbe();
    }
}
```

预期失败首先指向尚不存在的全屏 Sequence 或 Prefab 背景层，而不是 Core 编译错误。

- [ ] **Step 5: 提交验收规格**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Editor/GalPlayModeVerificationRunner.cs
git commit -m "test: specify fullscreen gal acceptance"
```

---

### Task 6: 热键、测试内容与 Prefab 原位升级

**Files:**

- Modify: `Assets/Script/GameFlowManager/GalSystem/Runtime/Integration/GalDebugHotkeyTrigger.cs`
- Modify: `Assets/Script/GameFlowManager/GalSystem/Editor/GalSystemTestContentInstaller.cs`
- Update through Unity API: `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab`
- Update through Unity API: `Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab`
- Update through Unity API: `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalActor_Test.asset`
- Update through Unity API: `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test.asset`
- Create through Unity API: `Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset`

**Interfaces:**

- Consumes: 两张 Task 3 PNG、View 的四个新增序列化引用
- Produces: `GalDebugHotkeyTrigger.fullscreenTestSequence`
- Produces: 可重复运行且不重建现有 Prefab 的安装器

- [ ] **Step 1: 实现 `P` / `Shift + P` 互斥热键**

保留 `testSequence` 和 `presentationMode` 字段，新增：

```csharp
[SerializeField] private GalSequenceSO fullscreenTestSequence;
```

将 P 键处理改为：

```csharp
if (Keyboard.current == null ||
    !Keyboard.current.pKey.wasPressedThisFrame)
{
    return;
}

bool shiftPressed = Keyboard.current.leftShiftKey.isPressed ||
    Keyboard.current.rightShiftKey.isPressed;
GalSequenceSO sequence = shiftPressed
    ? fullscreenTestSequence
    : testSequence;
GalPresentationMode mode = shiftPressed
    ? GalPresentationMode.FullscreenExclusive
    : presentationMode;
string source = shiftPressed
    ? "DebugHotkey:Shift+P"
    : "DebugHotkey:P";

GalStartResult result = gameFlowArbitrator.RequestGal(
    new GalPlayRequest(sequence, mode, source));
```

缺少相关序列时输出包含具体组合键的警告；`Started` 和 `Busy` 不输出错误。

- [ ] **Step 2: 扩展 Installer 路径和导入设置**

新增常量：

```csharp
private const string BackgroundFolder = TestContentFolder + "/Backgrounds";
private const string AnimePortraitPath =
    PortraitFolder + "/SPR_GalPortrait_Test_Anime.png";
private const string BackgroundPath =
    BackgroundFolder + "/BG_GalFullscreen_Test.png";
private const string FullscreenSequencePath =
    TestContentFolder + "/GalSequence_Test_Fullscreen.asset";
```

用一个明确参数化的方法配置两张图：

```csharp
private static void ConfigureSpriteImporter(
    string assetPath,
    bool hasAlpha,
    Vector2 pivot)
{
    if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        return;

    importer.textureType = TextureImporterType.Sprite;
    importer.spriteImportMode = SpriteImportMode.Single;
    importer.sRGBTexture = true;
    importer.alphaSource = hasAlpha
        ? TextureImporterAlphaSource.FromInput
        : TextureImporterAlphaSource.None;
    importer.alphaIsTransparency = hasAlpha;
    importer.mipmapEnabled = false;
    importer.textureCompression = TextureImporterCompression.Uncompressed;
    importer.filterMode = FilterMode.Bilinear;
    importer.wrapMode = TextureWrapMode.Clamp;
    importer.npotScale = TextureImporterNPOTScale.None;
    importer.maxTextureSize = 2048;
    importer.spritePixelsPerUnit = 100f;

    var settings = new TextureImporterSettings();
    importer.ReadTextureSettings(settings);
    settings.spriteAlignment = (int)SpriteAlignment.Custom;
    settings.spritePivot = pivot;
    importer.SetTextureSettings(settings);
    importer.SaveAndReimport();
}
```

立绘调用 `(AnimePortraitPath, true, new Vector2(0.5f, 0f))`；背景调用
`(BackgroundPath, false, new Vector2(0.5f, 0.5f))`。移除安装器对旧 `_Keyed` 文件的
删除逻辑。`InstallTestVerticalSlice()` 必须先执行 `EnsureFolder(BackgroundFolder)`，
再配置两个 Importer，并分别用 `AssetDatabase.LoadAssetAtPath<Sprite>()` 回读；任一回读
为 `null` 时返回包含具体 AssetPath 的 FAILED，不继续修改 SO 或 Prefab。

- [ ] **Step 3: 更新 Actor 和两套 Sequence**

`CreateOrUpdateActor()` 改为引用 Anime Sprite。保留原 `GalSequence_Test.asset` 作为
Paused 回归，新增 `CreateOrUpdateFullscreenSequence(actor, background)`：

```csharp
private static GalSequenceSO CreateOrUpdateFullscreenSequence(
    GalActorSO actor,
    Sprite background)
{
    GalSequenceSO sequence =
        AssetDatabase.LoadAssetAtPath<GalSequenceSO>(FullscreenSequencePath);
    if (sequence == null)
    {
        sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
        AssetDatabase.CreateAsset(sequence, FullscreenSequencePath);
    }

    var serialized = new SerializedObject(sequence);
    serialized.FindProperty("sequenceId").stringValue = "gal_test_fullscreen";
    serialized.FindProperty("defaultPresentationMode").enumValueIndex =
        (int)GalPresentationMode.FullscreenExclusive;
    serialized.FindProperty("defaultBackground").objectReferenceValue = background;
    SerializedProperty lines = serialized.FindProperty("lines");
    lines.arraySize = 3;
    ConfigureLine(lines.GetArrayElementAtIndex(0), actor,
        "这里不是我们刚才所在的地方。", GalPortraitSlot.Left, null);
    ConfigureLine(lines.GetArrayElementAtIndex(1), actor,
        "全屏背景已经接管画面，但 Gameplay 仍安全地停在原处。",
        GalPortraitSlot.Center, null);
    ConfigureLine(lines.GetArrayElementAtIndex(2), actor,
        "再确认一次，我们就回到游戏。", GalPortraitSlot.Right, null);
    serialized.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(sequence);
    return sequence;
}
```

把 `ConfigureLine` 明确扩展为接收 `GalPortraitSlot slot` 和
`Sprite backgroundOverride`，并设置新增序列化字段。原 Paused 序列的
`defaultBackground` 与每行覆盖均设为 `null`。

- [ ] **Step 4: 原位升级 View Prefab**

现有 `CreateDialogueViewPrefab()` 只允许在 Asset 不存在时调用。Asset 已存在时必须：

```csharp
GameObject contents = PrefabUtility.LoadPrefabContents(ViewPrefabPath);
try
{
    Transform visualRoot = contents.transform.Find("VisualRoot");
    Transform dimmer = visualRoot.Find("GameplayBackdropDimmer");
    Transform stage = visualRoot.Find("FullscreenBackgroundStage");
    if (stage == null)
    {
        stage = CreateUIObject(
            "FullscreenBackgroundStage",
            visualRoot).transform;
    }

    Stretch(stage.GetComponent<RectTransform>());
    stage.SetSiblingIndex(0);

    Transform backgroundTransform = stage.Find("BackgroundImage");
    Image backgroundImage;
    if (backgroundTransform == null)
    {
        backgroundImage = CreateImage(
            "BackgroundImage",
            stage,
            Color.black);
    }
    else
    {
        backgroundImage = backgroundTransform.GetComponent<Image>();
    }

    RectTransform backgroundRect = backgroundImage.rectTransform;
    backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
    backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
    backgroundRect.pivot = new Vector2(0.5f, 0.5f);
    backgroundRect.sizeDelta = new Vector2(1920f, 1080f);
    backgroundImage.raycastTarget = true;
    AspectRatioFitter fitter =
        backgroundImage.GetComponent<AspectRatioFitter>();
    if (fitter == null)
        fitter = backgroundImage.gameObject.AddComponent<AspectRatioFitter>();
    fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
    fitter.aspectRatio = 16f / 9f;

    var serialized = new SerializedObject(
        contents.GetComponent<GalDialogueView>());
    serialized.FindProperty("fullscreenBackgroundStage").objectReferenceValue =
        stage.gameObject;
    serialized.FindProperty("fullscreenBackgroundImage").objectReferenceValue =
        backgroundImage;
    serialized.FindProperty("fullscreenBackgroundFitter").objectReferenceValue =
        fitter;
    serialized.FindProperty("gameplayBackdropDimmer").objectReferenceValue =
        dimmer.gameObject;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    stage.gameObject.SetActive(false);
    PrefabUtility.SaveAsPrefabAsset(contents, ViewPrefabPath);
}
finally
{
    PrefabUtility.UnloadPrefabContents(contents);
}
```

不得删除或重建 `VisualRoot`、`PortraitStage/ActivePortrait`、
`DialoguePanel/ConfirmButton`。

- [ ] **Step 5: 原位升级 Root Prefab 与场景连接**

通过 `PrefabUtility.LoadPrefabContents(RootPrefabPath)` 获取现有 Root；复用其中的
`GalFlowRoot`、`GalDebugHotkeyTrigger` 和 nested `GalDialogueView`，只补引用：

```csharp
private static void ConfigureDebugTrigger(
    GalDebugHotkeyTrigger trigger,
    GameFlowArbitrator arbitrator,
    GalSequenceSO pausedSequence,
    GalSequenceSO fullscreenSequence)
{
    var serialized = new SerializedObject(trigger);
    if (arbitrator != null)
    {
        serialized.FindProperty("gameFlowArbitrator").objectReferenceValue =
            arbitrator;
    }
    serialized.FindProperty("testSequence").objectReferenceValue = pausedSequence;
    serialized.FindProperty("fullscreenTestSequence").objectReferenceValue =
        fullscreenSequence;
    serialized.FindProperty("presentationMode").enumValueIndex =
        (int)GalPresentationMode.PausedOverlay;
    serialized.ApplyModifiedPropertiesWithoutUndo();
    EditorUtility.SetDirty(trigger);
}
```

场景中已有 `GalFlowRoot` 时不重新实例化。用 `sceneChanged` 单独记录本轮是否真正修改
场景；只有 `!sceneWasDirty && sceneChanged` 时才允许 `SaveScene`。当前场景预期
`sceneWasDirty == true`、`sceneSaved == false`。

- [ ] **Step 6: 重编译、安装并运行 Edit Mode 验收**

顺序固定为：

1. `request_recompile`；
2. `wait_for_compilation`；
3. `get_compilation_errors`，要求零错误；
4. `execute_code` 返回 `GalSystemTestContentInstaller.InstallTestVerticalSlice()`；
5. 再运行同一安装器一次验证幂等；
6. 运行 `GalPlayModeVerificationRunner.RunEditModeCompositionProbe()`。

预期最终结果：

```text
[GalInstaller] PASS
PASS gal fullscreen edit-mode composition
```

两次安装后必须仍只有一个场景 `GalFlowRoot` 和一个 Gal Canvas。

- [ ] **Step 7: Unity 回读 Prefab、资产与场景**

用 `execute_code` 或专用 MCP 工具逐项返回：

- `GalActor_Test.DefaultPortrait` 的 AssetPath 为新版 Anime PNG；
- Fullscreen Sequence 的模式为 `FullscreenExclusive`，默认背景为新背景，行数为 3；
- View Prefab `IsReady == true`，背景层层级与四个引用完整；
- Canvas 为 `ScreenSpaceOverlay` 且 `sortingOrder == 5000`；
- Root Prefab Trigger 同时引用 Paused 和 Fullscreen Sequence；
- 活动场景只有一个 `GameFlowRoot` 和一个已连接的 `GalFlowRoot` PrefabInstance；
- `gameFlowArbitrator` 场景 override 仍有效；
- 当前场景未由安装器保存。

- [ ] **Step 8: 提交 Unity 集成**

仅暂存 GalSystem 范围内本任务代码、Prefab、SO 和由 Unity 生成的 `.meta`：

```bash
git add Assets/Script/GameFlowManager/GalSystem/Runtime/Integration/GalDebugHotkeyTrigger.cs Assets/Script/GameFlowManager/GalSystem/Editor/GalSystemTestContentInstaller.cs Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalDialogueView.prefab Assets/Script/GameFlowManager/GalSystem/Prefabs/PF_GalFlowRoot.prefab Assets/Script/GameFlowManager/GalSystem/Content/Test/GalActor_Test.asset Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test.asset Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset Assets/Script/GameFlowManager/GalSystem/Content/Test/GalSequence_Test_Fullscreen.asset.meta Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds.meta Assets/Script/GameFlowManager/GalSystem/Content/Test/Portraits/SPR_GalPortrait_Test_Anime.png.meta Assets/Script/GameFlowManager/GalSystem/Content/Test/Backgrounds/BG_GalFullscreen_Test.png.meta
git commit -m "feat: install fullscreen gal test content"
```

---

### Task 7: Play Mode 双模式验收与验证记录

**Files:**

- Update: `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalPausedOverlay.png`
- Create: `Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png`
- Create: `Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-verification.md`

**Interfaces:**

- Consumes: 完成的双模式 Verifier、Prefab 与测试内容
- Produces: 可审计的编译、Probe、Unity 回读、Play Mode 与截图证据

- [ ] **Step 1: 做最终静态和编译检查**

运行：

```text
git diff --check -- Assets/Script/GameFlowManager/GalSystem
request_recompile
wait_for_compilation
get_compilation_errors
```

要求 GalSystem 无 whitespace 错误，Unity 编译错误数为 0。

- [ ] **Step 2: 运行全部 Editor Probe**

用一个 `execute_code` wrapper 顺序调用：

```csharp
public static class RunAllGalFullscreenProbes
{
    public static string Run()
    {
        return GalSequencePlayerProbe.Run() + " | " +
            GalFlowLifecycleProbe.Run() + " | " +
            GalDialogueViewProbe.Run() + " | " +
            GalPlayModeVerificationRunner.RunEditModeCompositionProbe();
    }
}
```

要求四段均返回 PASS。

- [ ] **Step 3: 运行完整 Play Mode Verifier**

调用 `GalPlayModeVerificationRunner.Begin()`。进入 Play Mode 后等待 domain reload 恢复，
不要在 live Play Mode 中触发 AssetDatabase Refresh。等待 Verifier 自行完成并退出 Play，
然后读取 Console，要求出现固定双模式 PASS 日志且没有本轮异常。

- [ ] **Step 4: 检查全屏截图**

用 `view_image` 打开：

`Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png`

确认：背景不透明铺满、没有 Gameplay 漏出、背景未拉伸、立绘无绿色底或明显绿边、角色
脸部无遮挡、姓名/正文/NEXT 清楚可读。再打开 `GalPausedOverlay.png`，确认原模式仍是
Gameplay 可见加压暗层。

- [ ] **Step 5: 写入最终验证记录**

创建 `2026-08-22-gal-fullscreen-avg-verification.md`，只有全部通过后才写入以下事实：

```markdown
# Gal 全屏 AVG 第二阶段验证记录

- Unity/Tuanjie 编译：PASS，0 errors
- GalSequencePlayerProbe：PASS
- GalFlowLifecycleProbe：PASS
- GalDialogueViewProbe：PASS
- Edit Mode composition：PASS
- 安装器幂等：PASS，场景仍为单一 GalFlowRoot/Canvas
- P / PausedOverlay：PASS，三句推进并完整恢复
- Shift+P / FullscreenExclusive：PASS，背景、立绘、三句推进并完整恢复
- 当前脏场景自动保存：未发生
- 角色控制器文件：未修改
- 视觉证据：Tests/Artifacts/GalPausedOverlay.png、GalFullscreenExclusive.png
```

- [ ] **Step 6: 最终范围审计**

运行 `git status --short` 和 `git diff --name-only HEAD~1`。确认 Character 与活动场景的
既有差异仍未被暂存或改写；本阶段新差异全部位于 `GalSystem`。

- [ ] **Step 7: 提交验证证据与文档**

```bash
git add Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalPausedOverlay.png Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalPausedOverlay.png.meta Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png Assets/Script/GameFlowManager/GalSystem/Tests/Artifacts/GalFullscreenExclusive.png.meta Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-design.md.meta Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-implementation-plan.md.meta Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-verification.md Assets/Script/GameFlowManager/GalSystem/2026-08-22-gal-fullscreen-avg-verification.md.meta
git commit -m "test: verify fullscreen gal flow"
```

最终向用户报告所有提交、生成资产、截图、验证结果，以及活动场景仍需由用户决定是否
手动保存。
