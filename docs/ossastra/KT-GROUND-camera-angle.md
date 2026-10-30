# 地表工坊相机俯角

2026-10-30，按用户图2将本机`SC_GroundWorkshopLab`的`MapResearchRoot.pitch`由42.857改为35度；透视距离25、FOV35、初始Yaw45保留，同时Native保存相机位置与姿态。直接改Camera的Transform会被Root每帧覆盖；后续在Hierarchy选MapResearchRoot，修改Map Research Root组件的Pitch，越小越侧。当前是视觉候选，不据单张参考反推出原作精确相机参数。

工坊“当前地图进入角色试玩”原先复制Frontier模板并只克隆作者地图根，导致作者相机设置丢失。现只在进入前捕获作者场唯一MapResearchRoot.pitch，并赋给本次临时副本；无Root的原创作者图沿模板，多个Root在落盘前拒绝。只读模板、运行时相机算法与角色控制不改，Core包不依赖此可选适配。

验证：Unity实际编译ready；直接运行35度；真实当前地图Begin进入`PreviewSessions/35e39899502d4821ab6e3200294d0fe3/Playground.unity`读回pitch35/Camera.x34.999996；同Yaw45、同Actor pose分别渲染42.857和35度，所有非Camera根位置不变。返回作者Lab clean/35、HasSession=false，磁盘SHA与进入前相同；Console无error。无Root/多Root分支本轮未另开场验证。

首次普通GameView前后截图期间Yaw已变化，不作为同机位比较；后补单命令Camera.Render同帧双图。截图与Native回执在本机`Candidates/KT-GROUND-CAMERA/`，均不冒称桌面/物理鼠标验证。Native保存亦序列化花草显示子节点派生yaw与URP补充相机Data，作者格与装饰/角色根位置不动。原参考场/受限媒体仅本机，自有可选适配与说明按R11发布。
