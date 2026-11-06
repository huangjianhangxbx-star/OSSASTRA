# EnvironmentState 定向加固合同

本候选目录归 RENDER-ARCH-01 的 EnvironmentState 子任务。生产改动仅 `EnvironmentRoot`、`EnvironmentCameraRegistry` 和新增无相机天气状态服务；不修改照明公式、作者Profile、业务时间/存档或主场。

## RED / GREEN

- `RegistryContract.csproj`链接真实Registry源码，用最小Camera identity外壳隔离通知逻辑。旧实现RED：`A failing Published observer escaped and can revoke an upstream publisher`。新实现5项GREEN。此离线检查不模拟Unity销毁或渲染，不能代替Native。
- `EnvironmentIsolationContract.cs.txt`：真实Unity临时对象，验证Published/Released消费者隔离、OnValidate无同步提交、相机无关世界事件与诊断。Root已确认旧实现Native RED；新实现Native 11项GREEN。
- `WeatherStateContract.cs.txt`：直接推进实际天气状态服务，不创建GameObject/Root/Camera/Renderer；验证中断混合、固定种子调度、64步预算与源数据不变。Native 10项GREEN。
- `EnvironmentStateCompile.csproj`：实际Runtime/Core源码离线编译，0错误0警告。

旧 `Candidates/ENV-01/RuntimeAdvancedContract.cs.txt`、`ClimateDeterminismContract.cs.txt`、`ClimateDurationBudgetContract.cs.txt`继续用于回归。其临时Camera禁用但显式编辑预览的用法保留；Play禁用Camera撤显示，世界天气状态仍推进。

## 生命周期与边界

`EnvironmentWeatherState`持当前值/目标/混合/随机序列，显示暂停不清状态。`ResetPreview`或明确更换初始档才重置；没有新增天气存档。`IEnvironmentTimeSource`仍接收已有游戏时钟，预览时钟不成为第二份日历。

Root分离帧求值错误、显示登记拒绝、原生灯光提交错误；原生Apply前预检相机租约，成功后再发布Registry。`Changed`通知完整世界帧且不依赖相机。单个订阅者异常记录事件/方法/异常类型及有界消息，不再把消费者故障误报为环境输入错误，也不每帧刷日志。Root请求同步重入会推迟到主线程更新。

OnValidate只置volatile工作标记，不调用Unity对象、RenderSettings或委托。Update在合法主线程应用；编辑预览仍须显式开启。
