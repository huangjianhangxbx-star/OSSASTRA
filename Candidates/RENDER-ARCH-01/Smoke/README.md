# 独立最小渲染烟测

这些是可复跑的测试模板，不是新增生产管理器。固定许可帧仅在独立场使用，绝不装入主场；真实房间观察权仍由生产模块提供。

1. 在安全Edit点，将RenderArchitectureSmokeProbe.cs.txt安装为同名.cs，交给Unity生成meta并编译；使用独立测试目录。
2. 保全并保存当前主场，再以Native执行BuildMinimalSmokeScene.cs.txt。它只读主场显式配置，在唯一Generated/Run目录创建一场，不修改BuildSettings或原管线。
3. Play后至少等待120真实绘制帧，执行ReadMinimalSmokeProbe.cs.txt；检查passed、实际Coverage/Outer/正向draw、环境登记和forbiddenComponents为空。
4. Edit保存独立场，以BuildMinimalSmokePlayer.cs.txt构建Development Windows64。目录可在命令前由宿主创建，避免MCP严格文件守卫阻断普通目录创建；不要关闭项目级保护设置。
5. 启动返回exe，参数为 `-logFile <本次日志绝对路径> -screen-width 1280 -screen-height 720 -screen-fullscreen 0 --render-smoke-quit`。不能用batchmode/nographics代替真实绘制。
6. 日志中cold-start与sunset两个RENDER_ARCH_SMOKE报告均须passed=true，进程退出码须为0。Editor与Player分别记录，不以注册成功冒充像素/视觉验证。
7. 恢复主场；测试完成后通过AssetDatabase删除本轮生成场与临时Probe，保留本目录模板即可。不要留下引用已删除Probe的场，也不要清理其他场景或旧dirty。

普通Renderer实体通过ICharacterLightingSource接入真实角色增强；测试Look由固定合法帧调用真实适配器与后端。没有真实房间成员上下文的WorldLookController应拒绝，不能拿Probe的成功宣称该生产Controller成功；主场另验真实成功链。

原角色Shader、轮廓、光照色、原画素材均未由本样板修改。样板自造一张简单轮廓检查图，生成资源仅本机。
