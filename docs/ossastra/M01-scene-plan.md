# M01 场景接入批次
用户授权target 1铺地继续；保留体素单位和素材导入尺度。独立场Assets/OSSASTRA/M01/SC_M01_CharacterTest.unity；源场SC_PRE00_Empty及源FBX/Spine数据不变。
核心6个.cs迁至Assets/OSSASTRA/CharacterCore作为唯一生产源码，旧Candidates项目以Compile链接读取它们，不保留双份权威。
新增真实模块：M01CharacterRoot组合初始化/调度/生命周期；M01PlayerInput设备采样；M01Motor控制器移动/平面朝向；M01SpineView播放去重；M01MeleeHit空间筛选；M01HealthTarget真实Health引用及受击反馈。
移动WASD/方向键，鼠标左键单普攻、X具名取消、P暂停；F1合法近靶/恢复、F2禁攻边界、F3清理、F4检查。只做单角色一次普攻，无AI/连招/技能库。
先保留核心29测试，接入后Play验证位移/动画/实际HP，空挥、重复释放、暂停、取消、禁启及卸载；来源素材未许可不发布。用户审查仍未通过，不自动commit/push。
