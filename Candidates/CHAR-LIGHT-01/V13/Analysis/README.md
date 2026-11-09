# v1.3 屏幕像素分析（候选）

子租约：2026-11-09，Root 分配 `/root/char13_pixels`，仅本 `Candidates/CHAR-LIGHT-01/V13/Analysis/` 目录可写。Root 仍为唯一 Unity、生产与 Git writer。这里只读已有截图，未操作 Unity，未修改或重新编码任何图像。

`AnalyzeImages.ps1` 读取当前八张正午及五环境 A/B 共 18 张 1600×900 原 PNG，使用 `System.Drawing.LockBits` 取得显示编码 RGB。先保存正午 A1/A2 的噪声，再比较候选；五环境缺少各自 A/A，单独记录局限。内部小 ROI 根据原尺寸图及逐像素文本定位固定，不使用含大块地面的整个人物外接矩形统计“人物细节”。脚本末尾核对输入运行前后 SHA-256。主要结论见 `pixel-findings.md`。

近白/近端点比例、亮度分布与梯度均只是显示图像指标：不等于 HDR 增益/线性主光，不证明原画有怎样的细节，也不构成用户审美验收。只有背景色、动画姿态和坐标相同的截图可使用同一ROI。原图透明素材与GPU阶段由Root的另外证据核实。
