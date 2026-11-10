# iPhone 0.3.0（15）覆盖更新

日期：2026-10-29。已安装到原实体 iPhone，沿用 `com.momo.aipet.prototype` 与原开发签名。

## 本次变化

- 右侧盆沿托盘：直立泵瓶、前侧花洒挂座、海绵与挂起的毛巾。同一件工具在存放与使用位置之间移动。
- 自动伸手、取工具、食指按泵、左手接液、海绵交接、靠近猫身、放回、空手收回。动画期间不计清洗覆盖；保持原四步与原换工具按钮。
- 花洒增加握柄、喷孔、挂座和连续软管。九束细水按重力轨迹移动，先碰到实际猫身接触体、盆壁或水面即截断；偏离猫身仍出水，关水留少量余滴。喷孔方向、落水位置与水面扰动相互关联，实际喷到头脸也会提醒／暂停。
- 起泡由实际接触附近开始，冲洗先去除接触处泡沫。海绵整体压缩，毛巾按局部身体曲面弯曲、带布纹并逐渐吸水变深。排水时浮泡向排水口运动。
- 托盘纳入猫与观察者避让，保留旧存档读取与位置整理；修复快速再次邀请时使用上一帧观察者位置的误拒绝。

## 已验证

85 项同源 Core、3928 项洗澡 Player、109 项常驻方向盘 HUD 检查通过；原喂食、1332 帧五猫生活回归通过。洗澡使用生产指针路径完整操作导入猫和原始程序猫，另验证五种导入模型的泡泡绑定。拿取挂接点距离、按泵取液、工具动画期间零清洗进度、独立出水、关水余滴、喷孔方向、毛巾变形以及后台／设置／切猫清理均有检查。

Unity iOS 导出、Xcode Release 和严格签名验证通过。安装前应用未运行，四份存档先备份。安装后首次启动前回读的 `world-0.save`、`world-1.save`、`household-0.save`、`household-1.save` 与最终安装前备份逐字节一致，且全部通过 SHA-256 文件头和当前生产 Core 读取规则。安装后设备查询确认版本为 0.3.0（15）。

首次启动被 iOS 因锁屏拒绝；用户解锁后已成功启动 PID 37711，后续查询确认同一新版进程仍在运行。采集画面分别为主屏与锁屏；随后用户手动打开，确认猫和方向盘正常显示，但提示栏遮住方向盘，该布局问题由后续构建修复。真人触控、材质自然度、持续性能与发热仍需实体 iPhone 体验。

## 证据

`artifacts/iphone-bath-tools/`：`xcode-build15.log`、`signature-verification.log`、`install-build15.json`、`app-after.json`、`save-preservation-build15.json`、`launch-build15.json`、`launch-build15-unlocked.json`、`processes-recheck.json`、`release-summary.json`。原始存档仅放在本机私有备份目录，不打印宠物数据。

`artifacts/bath-tools-core.log`、`bath-tools-compile.log`、`bath-tools-player.log`、`bath-tools-hud.log`、`bath-tools-feeding.log`、`bath-tools-multicat.log` 为本地验证记录。

`artifacts/bathing/bath-tools-preview.mp4` 是真实客户端拿取与取液序列的桌面渲染预览；`shampoo-pump-390.png`、`shower-body-contact-390.png`、`spray-into-water-390.png`、`towel-contact-390.png` 为对应操作画面。不是手机录屏。
