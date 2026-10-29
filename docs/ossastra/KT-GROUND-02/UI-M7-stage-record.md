# KT-GROUND-02：UI与M7阶段回执

本记录截止首轮参考Preview的输入、有限Editor性能和第一次返回数据，**参考往返已确认，原创往返与M8尚未收口**。它是阶段事实，不是整轮完成声明。本记录未修改共享sync或玩法代码。

## 技术验证

本机 `研究/开拓物语地表/KT-GROUND-02/01-evidence/` 的真实Native回执支持：author-tile-semantics7；core-recipes20；save-as-isolation19（含两次真关场重开）；window-owned-state13；SceneView gesture86；catalog-import114与preflight9；原创样本工厂28；装饰17与边界9；合法删除前缀续清7。各数字是其断言数，含重复校准/清理，不相加为独立功能数。

关键文件分别为 `author-tile-semantics-green.json`、`core-recipes-i.json`、`save-as-isolation-i.json`、`window-owned-state-i.json`、`actual-gesture-poll.json`、`catalog-import.json`、`catalog-preflight.json`、`originals-first-green.json`、`decorations-first.json`、`decorations-edge-first.json`、`playground-cleanup-continuation.json`。公开仓库不复制这些私有完整源表或参考媒体。

Window的真实失败为CreateInstance后null MPB缓冲、随后Native brush/tool恢复回退外部target；原始stack/trace保留，现13检查验证惰性缓冲、完整MPB、独立外部Brush/target归属。Core迁移保留每格颜色、flags和有效手动矩阵，原生规则显示变换与作者解锁语义分别处理。SaveAs独立Tile/材质/主题和装饰身份，未用一份平行格子表绕开原生数据。

## 首轮参考Preview

`reference-preview-play-state.json`：7地层、43装饰、2玩家、1敌，编辑门拒绝临时试玩图；作者SceneHash未变。碰撞只有3个CharacterController与5个新连续地面/边墙BoxCollider，本次未见旧floor/墙碰撞混入。

`reference-preview-returned.json`：已回到原参考作者场Edit/clean，HasSession=false、editable=true、authorHashUnchanged=true、returnedIdentity=true，仍为7层/43装饰/两玩家/单敌；只证明第一次参考往返，未扩大到原创或全部故障恢复。

`reference-input-story-result.json`：75.1848秒，24/24；覆盖3072m²四角、实际八个45°镜头扇区、缩放、两次Current切换保持镜头、受控角色的Applied命中（敌HP600→558）、敌原释放/玩家受伤及既有重置入口。`physicalHardwareVerified=false`、`visualFlickerVerified=false`，不扩大为人工或无闪烁通过。

`reference-performance-result.json`：2.1915秒、8行，两cycle中每行60帧，同机固定1600×900/Perspective FOV35且相机漂移0，对比1024/3072m²与角色Renderer可见组合。Editor帧中位数2.4895—2.9076ms；地表Renderer7、装饰43、共享材质种类15。角色模拟持续，隐藏组仍保留装饰且enabledActorRenderers为3（显示组6），不能凭模式名断言全部角色渲染剥离；不属于纯地表CPU或纯地表渲染结果，GPU/Player/设备/长时泄漏未认证。

## 图鉴与教材

扫描边界是Demo build26043868的263个Addressables bundle；6候选容器。源资源11820行分为Included599/Dependency210/Excluded10115/Unresolved896；图鉴600条另含边缘族wrapper，Ready57/StaticPreviewOnly418/Unresolved125。媒体约52.3MiB，源写入0；bundle外嵌入资源未扫描。源保真2937/2937，覆盖62个Texture2D与537个Sprite；实际未遇旋转Sprite样本，不能声明该分支实测。

本机入口 `开始使用.html`、八章教程与三类绘制规范已生成；纯原创公开指南的11文件与SHA在 `Candidates/KT-GROUND-02/UI/PublicGuide/public-manifest.json`。其相互相对链接保持03/04目录结构。真实参考Game View帧仅嵌本机教材，不进入公开指南；自有SVG与HTML QA图不冒充Unity功能截图。

帮助可移植fallback先有真实 `help-routing-red.json`（缺resolver），随后在参考返回并获root解除该文件冻结后，仅应用Window候选补丁：本机资料根优先，再项目Documentation实际文件；无私有图鉴时明确告知，不造死链接。当前Window再冻结，等待prepareM与Native绿验，没有在Play期间热改。

## 当前A01—A25边界

A02—A04的声明范围/来源和实际保真已有支撑；A08—A17/A19的相关Native技术部分已有上述回执，但不是所有组合全覆盖。A01最终保护全体指纹、A05完整浏览交互、A07物理UI、A11/A20原创及更多生命周期往返、A18全场视觉、A22完整现场截图、A24新工程/Standalone与A25发布仍需最终结案。详见本机 `03-tutorial/验收进展_实际回执草稿.md` 的逐项矩阵。

**UI_PHYSICAL_UNVERIFIED**。桌面工具初始化及reset仍报kernel assets error3；没有因此将自动SceneView事件、Game View截图或合同伪称物理鼠标验收。审美、手感和制作体验待用户。后续结果由root补入最终验收/发布记录，此阶段稿不自动变成最终通过。
