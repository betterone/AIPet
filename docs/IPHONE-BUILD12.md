# iPhone 更新：0.3.0（12）

更新日期：2026-10-28。设备为已配对的实体 iPhone 17 Pro，iOS 26.5.2。

## 本次更新

从手机上的 0.3.0（11）覆盖更新至 0.3.0（12）。包含后院手动洗澡、逐猫脏污，以及精简操作界面、浮动摇杆和六秒无输入后的自动隐藏。操作见 [QUIET-HUD.md](QUIET-HUD.md) 和 [BATHING.md](BATHING.md)。

沿用原 Bundle ID `com.momo.aipet.prototype` 与现有本地签名配置，没有卸载原应用。Unity 重新导出 `Builds/iOS-Window`，Xcode 使用 Release / iphoneos 编译并签名。

## 已验证

- Unity iOS 导出、签名配置保留与 Xcode Release 构建通过。
- 安装包的严格签名验证通过，实际元数据为 `0.3.0` / `12` / 原 Bundle ID。
- 手机安装服务报告覆盖安装成功；安装后查询确认为构建 12。
- 安装前备份四份检查点；安装后、首次启动前再次读回，四份文件逐字节一致，SHA-256 完整性全部通过。
- 两份旧单猫检查点和两份家庭检查点均通过当前生产 Core 的读取规则验证；84 项同源核心测试通过。

首次启动被 iOS 锁屏状态拒绝；用户解锁后再次启动成功，设备返回激活成功，并在后续查询中确认 Momo 进程存在。截屏采集时设备再次处于锁屏，未将该图当作游戏画面验证。启动成功不代表触摸、动画自然度或持续性能验收。

## 本地证据

记录目录为 `artifacts/iphone-quiet-hud/`：`xcode-build.log`、`codesign-verify.log`、`built-app.json`、`install-build12.json`、`app-after.json`、`save-preservation-build12.json` 和启动日志。

解锁后启动证据为 `launch-unlocked-build12.json` 与 `processes-after-launch.json`，汇总为 `release-summary.json`。

该目录的 `private-before-device`、`private-before-install`、`private-after-install` 为设备存档备份，保存在本机，不应公开提交或上传。没有把宠物内容写入验证日志。
