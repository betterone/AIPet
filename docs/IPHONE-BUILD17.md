# iPhone 0.3.0（17）：逗猫连扑与成长家园

2026-11-02。新增第一人称连扑短局、近远落点、完美时机与三段连击，一键重开；接入每猫本领、三种家庭设施、每日与过去七天路线，以及本地试玩记录。入口是「互动 → 逗猫连扑」；自由逗猫仍在玩法页内。

本次通过：101 项同源 Core 检查、桌面与 UNITY_IOS 源码编译、Unity macOS 和 iOS 导出、WindowViewChecks；新增 Player 的 320×700／390×844／768×1024 显式渲染目标各 156 项检查。原有 127 项 HUD、1332 帧多猫、掌心喂食和 3928 项洗澡生产检查也通过。

Xcode Release 签名构建、`codesign --verify --deep --strict` 通过。沿用原 Bundle Identifier，覆盖更新到实体 iPhone 的 0.3.0（17）。安装前四份旧存档已备份，校验和有效；安装后首次启动前与原文件逐字节一致。

首次启动被 iOS 以设备锁屏为由拒绝，未计为成功启动、屏幕验证或触控验证。解锁手机后手动进入 Momo 即可开始试玩。schema 2 → 3 的首次真实设备迁移须在首次打开后核查；同一迁移链已通过 Core 和 Unity codec 检查。自然度、连续 30 分钟真机性能和十人／七天回访仍需独立验收。

证据：`artifacts/wand-iphone/` 下的 Xcode 构建、严格签名、安装、启动拒绝、存档备份和逐字节比较记录。完整玩法与边界见 [WAND-CHALLENGE.md](WAND-CHALLENGE.md)，真人验收记录见 [PLAYTEST.md](PLAYTEST.md)。
