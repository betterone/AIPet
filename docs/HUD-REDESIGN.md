# Momo 操作界面改版

当前桌面默认操作已于 2026-10-28 改为 [探索界面精简与自动隐藏](QUIET-HUD.md)。下文保留上一版工具栏、方向盘和 build 9 手机交付记录，不代表新版界面已经更新到 iPhone。

更新：2026-10-27；目标版本：0.2.0 / iOS build 9。

## 界面与操作

- 主界面改为浮动猫咪卡、拍照／设置图标、圆形方向盘，以及「回忆／一起玩／找猫」三个底部入口。主要互动采用深青色爪印圆钮；提示放在方向盘右侧。
- 首次使用且没有已领养猫时，直接进入萌宠小店选择第一只猫；房间不显示占位猫。确认领养后才创建猫并进入空间，后续领养才会新增同伴。
- 点击猫咪卡查看当前精力、饱足、饮水和由无聊值推导的玩耍满足状态；这些数值来自现有 WorldState，不增加新存档字段。
- 感应开关、灵敏度、回正、回入口及退到窗外收进「设置 → 视角与移动」。原世界手势与 ARKit 功能沿用。
- 互动、设置、视角、回忆、状态和声音来源采用同一套底部面板；内嵌字体、圆角、图标、按压色和留白统一。选猫页沿用经过验证的分区取景，统一了颜色、圆角与按钮反馈。
- 方向盘改为游戏式模拟摇杆：拖动中央爪印可无级控制前后左右；中心附近是低速精细挪步，推到外环以更快速度移动。打开面板时清除移动，松手停止。触摸、菜单及世界拾取仍分开处理。
- 回忆只翻译明确对应的历史事件文案；保留原事件和存档。未知事件原文继续显示。

图标与圆角由 `GameHudStyle` 在代码中生成并缓存，不依赖 emoji 或手机系统字体。没有新增账号、商城、付费、在线服务或虚构的奖励数值。

## 设计与证据

`artifacts/hud-redesign/layout-proposal.png` 是明确标注的布局草图，背景取自已有 Unity 渲染；不能当作已实现截图。

实际 Unity Player 截图位于 `artifacts/mobile-hud`：

- `room-*`：主界面。
- `play-*`：四种互动。
- `settings-*`：设置。
- `view-*`：视角与移动。
- `status-*`：宠物状态。
- `journal-*`：真实回忆。

自动检查覆盖 390×844、320×700、430×932；所有上述面板使用内嵌字体，无截字或文字越界。逗猫棒拖动、连续抚摸与猫主动邀请继续更新现有宠物状态。探索回归继续验证长按／松手、开面板停止移动、门洞、动态找猫和完整躲藏提示循环。

33 项核心规则、桌面／iOS C# 和原生 ARKit 桥接的现有验证通过。代码编译、桌面尺寸截图、手机安装与真实手机手感是独立结论；真机持续性能与最终视觉接受度仍需实际体验。

手机更新前，当前设备的两份存档已备份至 `artifacts/hud-redesign/iphone-save-before`，并验证 SHA-256 完整性。该目录为本地用户数据，不应公开提交。

## 最终交付（2026-10-22）

- 最终 iOS 工程重新导出，Release 签名构建和签名校验通过；安装包确认为 `com.momo.aipet.prototype` / build 9。构建记录：`artifacts/hud-redesign/xcode-ios-build.log`。
- build 9 已覆盖更新到原 iPhone Momo，未卸载应用。临安装前再次备份于 `iphone-save-before-build9`，安装后启动前读回于 `iphone-save-after-build9`；两份检查点均通过 SHA-256 校验且逐字节一致。结果见 `save-preservation-build9.txt`。
- 用户解锁后，设备返回启动成功，后续查询确认更新后安装路径中的 Momo 进程仍在运行；设备操作记录在 `iphone-install-build9.json`、`iphone-launch-build9.json`、`iphone-processes-build9.json`。手机持续性能与实际触控手感尚未验收。
- 选猫、九猫档案、领养回房间和重新进入通过四种窗口比例回归；记录：`artifacts/hud-redesign/boutique-review.log`。
- `artifacts/hud-redesign/implemented-overview.png` 合并实际 Unity Player 的主界面、互动和设置截图，明确标为桌面手机比例画面。它与前期布局草图分开保存。
- 桌面自动检查确认可交互按钮中心能被 UI 射线命中；原生桌面点击工具坐标发生偏移，人工点击验收未完成，不将其计为通过。探索回归与互动状态更新使用隔离测试存档。

运行检查：

```sh
./scripts/check-exploration.sh
./scripts/build.sh mac
Builds/macOS/Momo.app/Contents/MacOS/Momo --mobile-hud-review -logFile "$PWD/artifacts/hud-redesign/mobile-review.log"
Builds/macOS/Momo.app/Contents/MacOS/Momo --exploration-review -logFile "$PWD/artifacts/hud-redesign/exploration-review.log"
```

`--mobile-hud-review --hud-interactive` 会在完成自动检查后停留在使用临时存档的实际游戏窗口，供鼠标检查按钮、面板与行走；不读取真实宠物存档。
