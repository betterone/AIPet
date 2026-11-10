# iPhone 0.3.0（14）覆盖更新

日期：2026-10-29。已安装到原实体 iPhone，沿用 `com.momo.aipet.prototype` 与现有开发签名。

## 本次变化

- 探索界面左下方向盘固定常驻，96 逻辑点底座；静置透明度 0.65，按住为 1，松手停止、回中。次要按钮仍可自动隐藏。
- 地面入口浴盆有防滑盆底、三面盆壁与可降下入口挡板。猫自主走入，水位逐渐升到躯干中部；水面有传播、衰减的波纹和少量水滴，擦干前排水、打开入口。
- 大小不同的立体球形泡泡聚团，细泡更乳白，大泡更透明，带薄膜边缘反光；随猫身骨骼更新，冲洗后缩散、漂落、消失。每步完成时收起工具、拉开镜头。

## 已验证

Unity iOS 导出、Xcode Release 构建与严格签名验证通过，安装后查询手机版本为 0.3.0（14）。安装前应用未运行；最终备份与安装后首次启动前回读的 `world-0.save`、`world-1.save`、`household-0.save`、`household-1.save` 四份文件逐字节一致，均通过 SHA-256 文件头和当前 Core 规则验证。未卸载、重置或替换宠物数据。

正常启动使用空参数列表，成功获得 PID 36152；后续设备进程查询确认同一新版应用仍在运行。采集到的屏幕仍是锁屏，已明确命名为 `device-lock-screen-after-launch.png`，不作为游戏 UI 验证。

本地同源 Core 85 项、完整手势洗澡 Player 3617 项、固定方向盘 HUD 109 项检查通过。五种导入猫均检查了 12 个泡泡片区与实际蒙皮绑定；另有原始程序猫的完整洗澡。原喂食与五猫 Player 1332 帧回归通过。桌面离屏运行速度不代表 iPhone 帧率。

实体手机的方向盘触控、洗澡镜头与材质观感、持续性能和发热仍待体验。

## 证据

`artifacts/iphone-bath-materials/`：`xcode-build14.log`、`signature-verification.log`、`install-build14.json`、`app-after.json`、`save-preservation-build14.json`、`launch-build14.json`、`processes-after-launch.json`、`release-summary.json`。存档原文件仅保存在该目录的私有备份子目录，不打印内容。

桌面画面：`artifacts/bathing/step-2-390.png` 为生产照料镜头；`foam-overview.png` 和 `bath-water-and-bubbles.mp4` 为开发观察镜头。对应实现与检查规则见 [BATHING.md](BATHING.md) 和 [QUIET-HUD.md](QUIET-HUD.md)。
