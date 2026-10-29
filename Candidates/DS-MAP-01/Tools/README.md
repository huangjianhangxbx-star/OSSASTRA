# 定向转换工具源码（GPL-2.0-or-later）

此目录只包含本轮独立Python命令行工具源码与许可，不包含Klei原作代码、游戏媒体、二进制工具或原作动画帧表。格式字段依据nsimplex/ktools@a1d1362bdb2b9aa9146d7177fbf0e351eab414ba，出处和所读文件指纹见tool-provenance.json；参考原文未随包复制，附上游NOTICE与GPL2全文，薄适配同GPL-2.0-or-later。此许可仅描述本目录独立工具，不替其他项目代码重授权，也不授予原作媒体再分发权。

使用已有Python/Pillow/NumPy。先将这三个py复制到一个专用本机研究目录的tools子目录，或将selected_assets.py的ROOT设置为该专用研究目录；SOURCE设置为用户已安装的游戏data根。确认位置后运行export_selected.py，再运行validate_selected.py。写入仅位于ROOT的02-selected-source/03-converted等输出，不回写游戏安装。当前本机实际转换119/119，Pillow12.3/NumPy2.3.5；无需下载或执行新的二进制程序。

只读取显式白名单的物件/地表，保留源哈希、每帧时长、完整物件合成和固定pivot。不处理人物、音频、存档、加密或DRM，不遍历全游戏。输出含受限源副本/图集/帧数据，始终保持本机；不能随本工具源码提交。现有场景壳需要配套GUID，不能把转换/首次Build误当作既有壳的跨机自动修复。
