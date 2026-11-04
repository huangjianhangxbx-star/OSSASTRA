# 完整角色缓存接线（候选说明，非验收报告）

`CharacterCoverageCache`由唯一`RoomPositivePass`持有，不能做共享全局跨相机实例。

1. `new CharacterCoverageCache(explicitCoverageShader)`；Shader正式主场应保留显式引用。
2. `Execute`完成当前Frame及环境全局上传后、最终角色绘制前调用`Append(cmd,camera,renderingData.cameraData.GetGPUProjectionMatrix()*renderingData.cameraData.GetViewMatrix(),descriptor.width,descriptor.height)`。
3. Append只写private atlas与独有coverage uniforms；随后调用方立即恢复真实color/depth target和完整viewport。它不改Unity view/projection矩阵。
4. 每个最终角色/装备合法draw前`cache.Bind(cmd,renderer)`；其它draw/批次前明确`_CharacterCoverageReady=0`。没有登记/当前无有效网格时Bind返回false，必须关闭本次轮廓。
5. 最终Shader在原许可clip、原sourceAlpha确定、原EnvironmentShade之后消费形状；它只能在原有合法片元中改RGB，不新增overlay，不改alpha/depth。
6. CameraCleanup清Ready；场景退出、feature禁用、Root关闭轮廓后调用`Dispose`或空Prepare释放cache。实际texture与material资源计数独立检查。

## 取样约定

- `_CharacterCoverageRect` = top-left origin的最终target UV矩形；包括不在屏幕中的自身形状和四个最终像素padding。
- `_CharacterCoverageAtlasRect` = 同UV约定的tile offset/scale。两个actor只横向分列，互不合并。
- `_CharacterCoverageScreenSize.xy` = 最终target W/H。
- `_CharacterCoverageTexelSize` = atlas纹素和尺寸。

```hlsl
float2 screenUV = rasterPosition.xy / _CharacterCoverageScreenSize.xy;
float2 localUV = (screenUV-_CharacterCoverageRect.xy)/_CharacterCoverageRect.zw;
// 候选采样函数：只允许当前tile的内部；超出角色rect返回0，不能采相邻tile。
float CoverageAlpha(float2 localUV) {
 if(any(localUV<0)||any(localUV>1))return 0;
 float2 uv=_CharacterCoverageAtlasRect.xy+localUV*_CharacterCoverageAtlasRect.zw;
 float2 edge=_CharacterCoverageTexelSize.xy*.5;
 uv=clamp(uv,_CharacterCoverageAtlasRect.xy+edge,
              _CharacterCoverageAtlasRect.xy+_CharacterCoverageAtlasRect.zw-edge);
 return SAMPLE_TEXTURE2D(_CharacterCoverageTexture,sampler_CharacterCoverageTexture,uv).a;
}
float2 radiusLocalUV=innerWidthPixels/_CharacterCoverageScreenSize.xy/_CharacterCoverageRect.zw;
```

UV y/图像翻转与左右光符号必须由Native非对称GPU夹具确认；不能根据CPU投影说明就标通过。缓存不是许可mask，不能直接叠到主画面。

## Receiver

作者明确`bodyRenderers`与`gameplayCamera`。Root在装备初始化之后`Initialize(this)`，LateUpdate完成表现提交后`TickPresentation()`，OnDisable `Suspend()`，OnDestroy `DisposeReceiver()`。组件不自行Update，不全场扫描，不修改Renderer。

身体成员只指定真正生成Spine网格的Renderer；不包含阴影/深度辅助、血条、FX。旧Sprite装备和两手Spine装备仅读现有端口；实际enabled与owner层级在缓存时再次检查。支持外部附件显式RegisterAccessory/UnregisterAccessory。

## 资源预算

两个actor，单actor最多8个明确成员，每Renderer最多16个material槽。单RT1024×512 RGBA8、2MiB；一material；每个实际submesh额外一个alpha-only覆盖draw；无深度RT，无每角色Camera，无每帧CPU纹理读回，无骨骼拷贝。

超预算仅关闭本轮轮廓并诊断，主体仍走原合法绘制，不静默把第三角色塞进别人槽。
