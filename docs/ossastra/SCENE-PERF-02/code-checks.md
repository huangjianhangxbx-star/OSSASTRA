# SCENE-PERF-02 接续检查记录（未完成性能验收）

2026-11-08。基线 74fcae95cdef11c58f3482332f145069a82165ea。保留原用户 dirty；当前未应用任何新性能优化，生产 Bucket 仅有四个有限 CPU 观测标记。

## 本次真实结果

- 复用原完整主场 Development Player，保留45°、1600×900、Ultra、4×MSAA与原观察规则。
- 可见 Player 的64帧诊断正常退出，运行错误0，原始raw约106MB；Editor实际反序列化报错，CPU树不可用。不能用该文件声称已完成M1，也不以Editor树替代。
- 正常60秒室外窗口925帧，P50/P95/P99为65.3874/85.0621/91.3233ms；运行错误0、机位与质量未变化。但复查发现PBZ同时运行、GPU总负载约98%，此窗口仅保留为争用现场，排除于基线和优化收益比较。GPU读数为实际有值，未写成0。
- 用户选择继续PBZ、先做代码检查；随后不再运行Player、GPU验收或Play重型测试，不关闭PBZ。

## 已完成的代码与方法级检查

1. 材质结构计划缓存仍在 Candidates/SCENE-PERF-02/Static，不在生产。
2. Native在Edit直接调用真实生产和独立overlay的Prepare/Restore，各31项合同。原版语义通过、稳定计划复用真实RED；候选31项全部GREEN。无异常，原主场路径/数量/dirty/模式保持。
3. 检查覆盖源材质同引用颜色/纹理/ST/keyword变更、私有材质污染、队列排序、槽位重排、Shader/Surface/resolver变更、Full与独立路由、外部clone pin让权、注销重生、销毁、拒绝帧、16槽边界和资源释放。它不是完整Play生命周期、多相机/GPU或净帧率证据。
4. CPU探针已改为独立early Update控制、完整帧预滚/尾帧排空、显式关闭log文件；正常采样分支不改。离线编译与Unity导入编译通过，尚未重建Player/重新验证raw解析。
5. 扩展负载装配候选离线编译0警告/0错误；真实1/2/4倍注册及2倍可见负载未执行，不声称通过。
6. 动态路线为实际虚拟键鼠输入，未逐帧传送；世界路线、门洞与状态仍待真实Player校准。不能将候选编译当120秒路线通过。
7. 静态身份审查发现：环境标签公开字段/数组元素/祖先组件无完整版本通知，不能仅用父级或矩阵签名跨帧缓存；下一较安全切片是每次相机构建内共享房间体积与renderer语义。待实际CPU树决定是否实施。

## 证据

- 当前原完整Player：artifacts/SCENE-PERF-02/player-build-latest.json，request f1f04b8925d049db80c957a013328730。
- 争用正常窗口：Player_20261108_042603_3801351/Run_run_exterior_20261108_052735_9231487/result.json。
- CPU坏解析raw：同Player目录 Run_cpu_exterior_20261108_052058_3499319。
- 合同：artifacts/SCENE-PERF-02/bucket-plan-cache-contract-20261108-054836-0608253.json。
- CPU边界导入：artifacts/SCENE-PERF-02/update-probe-boundary.json，目标SHA A35E51E6DDF79AA4616268E232FA43A086E1D6FC22F7FBD049C7F1C1DF142BAB。

## 下一次继续

PBZ退出后确认GPU无争用；先重建包含CPU边界修正的同类型完整主场副本，验证raw确实可读，再取得正常Player工作/等待树与同机位基线。只有M1证明结构计划成本足够，才应用已过合同候选并做单变量整帧复测。收益不明则撤回候选，不先堆新视觉。

尚未达成稳定60帧；正式室外/室内双轮60秒、动态双轮120秒、真实60Hz与余量/扩展仍待执行。不得发布“已稳定60帧”。当前业务存档入口正常、主场Edit，不保存或覆盖用户dirty。