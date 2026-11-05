# v1.2 环境源合同

`EnvironmentTintSourceContract.cs.txt` 使用独立、禁用、DontSave 的 Binding 与反射构造的不可变 Frame，不改变主场环境、Profile、时钟或灯。

执行方式：Root 通过 Unity execute_code 运行全文。旧版缺少新方法时，探针回退调用实际旧方法，预期自然有效源、手动目标无旧 0.25 下限、诊断入口三项失败；这是真实功能 RED，而非缺方法编译失败。

## 源选择

- A 始终使用原 `ResolveStylizedSkyTint`，兼容原手动覆盖、0.25 色度下限和原强度。
- B 的新入口使用现有 `syncSkyColorTo2D` 与 `stylizedSkyTintStrength`；手动覆盖优先，否则读取 Frame 当前环境色。
- 自然 Frame 的 Ambient/Sky 本身只有昼夜插值，没有独立夕阳环境色。经 Root 同意，B 的风格化目标在低太阳高度向 Frame 自己的 SunColor 靠拢；不修改 Frame、太阳、天空或反射输出。
- 低日照暖色权重在 0° 处归零，因为 Frame.SunColor 在地平线下切到月光色。此方式在地平线、正午与深夜连续退回自然环境色，不能冒称真实天空积分或窗洞 GI。
- 作者颜色各解码一次到 Linear，再混合并归一化色度；不重复乘原环境底光。
- 室内外许可由原逐片元空间资格处理；解析器不根据主控是否入室给所有角色切色。

本轮不改变天空实际绘制、反射、RT、相机、光源或其他 role。

## 本轮检查

- Root 已执行旧实现 Native RED：13项中恰好上述3项失败，其余兼容项通过；随后授权源码实现。
- `dotnet build Candidates/CHAR-LIGHT-01-v12/Source/SourceCompile.csproj -v:q`：实际SkyBinding源码定向编译，0错误0警告。
- 原A方法体与HEAD比较完全一致；diff-check通过。
- Root 编译刷新后执行原13项 SourceContract，以及新增8项 `EnvironmentTintSourceBoundaryContract.cs.txt`。后者覆盖各颜色只解码一次、Linear混合、低太阳暖色、0°日月切换连续性和实际源诊断。

离线编译不代替实际Unity行为验证；最终Native结果由Root记录。
