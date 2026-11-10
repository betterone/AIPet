# iPhone 0.3.0（16）：提示栏避让方向盘

2026-10-29。用户在 build 15 真机确认猫和方向盘正常显示，同时发现底部文字提示遮住方向盘。

已将提示栏从底部 88 逻辑点处，改为按方向盘完整触摸区的实际顶边向上留 12 点布局。方向盘仍为固定常驻底座，触摸区、灵敏度、拖动与松手规则不变；提示仍不拦截触摸。

127 项生产 HUD 检查通过，包含 320／390／768 尺寸下强制显示提示、提示与整个方向盘触摸区不重叠、真实 UI 射线能命中方向盘、按下／拖动／松手／看向、菜单与照料入口。截图 `artifacts/quiet-hud/hint-clear-390.png` 为桌面渲染，不是真机截图。

Unity macOS、iOS 导出、WindowViewChecks、Xcode Release 与严格签名验证通过。实体 iPhone 查询版本为 0.3.0（16）。原应用正常关闭后备份了最新存档；安装后首次启动前，四份存档逐字节一致，并通过 SHA-256 文件头与当前 Core 读取验证。

更新后远程启动被 iOS 因锁屏拒绝，尚未验证修复版的真机前台画面。用户可解锁后手动进入 Momo 验收。

证据：`artifacts/hint-layout-player.log`、`artifacts/quiet-hud/PASS.txt`，以及 `artifacts/iphone-hint-layout/` 中的 `xcode-build16.log`、`signature-verification.log`、`app-after.json`、`save-preservation-build16.json`、`launch-build16.json`、`release-summary.json`。
