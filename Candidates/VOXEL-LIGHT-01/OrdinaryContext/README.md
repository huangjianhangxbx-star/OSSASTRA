# 普通Voxel同帧环境上下文

Root单一Unity写者在当前Play主场运行 `OrdinaryContextContract.Run()`。旧实现缺少公开静态 `EnvironmentSkyBinding.UploadOrdinaryForCamera(CommandBuffer,Camera)` 时立即报告 `methodMissing=true`，不触碰当前环境。这是本窄接缝的RED。

收到Root有效RED后，沿现有SkyBinding接入：公开方法只构建专用环境缓冲，按明确相机owner上传已有Frame/工作集并设普通role1；空/未登记相机或构造失败只关环境。Begin复用它，非owner回调不覆盖当前owner；End仅归还自身相机环境。GPU提交异常不伪装成可恢复构造失败，不改变2D算法、灯/天气或材质。

GREEN使用真实 `Graphics.ExecuteCommandBuffer`，私有停用环境根与绑定验证两相机登记顺序、room/cloud/wet、role残留、空/未登记相机、无效工作集、构造异常与修复。仅临时登记条目通过反射注入，避免向主场消费者发布测试Frame。两字典原条目对象、所有涉及的GPU globals在finally恢复；原工作集不被调用。合同不伪造ScriptableRenderContext，因此真实begin/end回调与最终像素另由Root实景验证。

为了不留下原本不存在的GPU数组，本合同要求主场已至少完成一次真实环境帧；缺少数组返回 `prerequisitesMissing`，不能当作通过或有效RED。

## 整数API诊断修正

首次GREEN中只有 `matchingCameraUploaded` 失败。已从本机UnityEngine.CoreModule的实际IL确认：`Shader.GetGlobalInt(string)`读取浮点存储后转整数，`GetGlobalInteger(string)`才读取`SetGlobalInteger`写入的整数存储。原合同误用旧读取API，并在finally用旧`SetGlobalInt`还原，不能据原结果证明整数全局已恢复。此处已改为成对的Integer API，仍保留原来的 `roomCount == 1` 断言，并增加四项实际读数，不放宽期望。

`IntegerApiDiagnosis.cs.txt` / `IntegerApiDiagnosis.Run()` 用一个私有probe分别写float=1.75与integer=23，验证旧读取为1、新读取为23，并精确恢复两个存储。生产代码没有因此改动。Root应在真实环境帧后重跑修正合同，重建整数恢复证据。
