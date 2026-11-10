# 第一人称持手：腕部、前臂与喂食视角

> 2026-10-26 补充：后续发现原喂食检查的嘴部距离过宽，且家庭朝向和手猫接触仍有缺陷。本文的历史流程通过不代表这些问题已被验收；最新修正和逐帧证据以 [FEEDING-CONTACT.md](FEEDING-CONTACT.md) 为准。

2026-10-23。用户提供实际截图，要求手部像 CS 的第一人称角色一样自然，并指出腕部／关节扭曲。这个目标是视觉与连续动作要求；此前的左右手映射、臂长与流程检查不足以验收它。

## 根因与修改

旧版以掌心为根，反向求肘／肩，同时固定掌心朝向。离身体较远的托粮点迫使肘部在侧边追位置，手腕却仍朝前，产生明显侧折；前臂又只有一处旋转，翻掌的扭转会集中在局部网格。用户指定的左右模型对调继续保留，本轮没有再次交换模型。

- 资源烘焙改为肩 → 上臂 → 前臂 → 手腕的正向链，重新计算绑定矩阵；加入两处前臂旋转过渡，并按网格沿前臂的位置分配权重，每顶点最多四权重。
- 肩的位置跟随玩家身体，使用固定上下臂长求解；手掌朝向随前臂调整，目标腕部弯折上限 18°，数值复核容差 1°。这是工程约束，不是医学活动范围结论。
- 前臂从中立姿态向腕部逐段分配转腕角，避免全部旋转集中在腕部。仍使用现有皮肤资源，没有引入 CS 模型或动画素材。
- 喂食及短时抚摸时，视角平滑从 1.55 降至 1.05 世界单位；结束后平滑恢复。玩家 XZ 位置与导航没有因俯身改变。
- 托粮距离收近至 0.68–0.78 世界单位；俯身到位之前不开始吃粮。原来宽范围的上下拖手不再把手臂伸到够不着的位置。近距离喂食取景改为 22° 俯仰，减少头部与提示卡重叠。
- 保留粮袋点击、右侧托粮、左侧抚摸、实际掌心接触、四口收益、移动暂停恢复及拒绝规则。

## 本轮实际证据

- macOS Development Player 构建及原有 WindowViewChecks 通过；UNITY_IOS 源码编译通过。
- 完整喂食 Player 回归通过，含 390／320 宽实际双手画面、拖手、屋内外进食、摸头、暂停恢复、饱腹拒绝与面板取消。
- 校准检查通过：用户指定模型绑定、正单位缩放、臂长误差小于 0.0001、腕部弯折不超过 19°。
- 额外记录 420 帧、固定 1/30 秒生产步进的连续场景，包含抓粮、翻掌、接近、吃粮、摸头、收手和恢复站姿，编码为 30 fps／14 秒录像。这不是实际 iPhone 或桌面性能测试。
- 本轮过程最大腕部弯折分别约 18.256° 和 18.300°；最大臂长误差约 0.00000066 世界单位。恢复站姿高度检查通过。

证据：`artifacts/first-person-hands/player.log`、`calibration.log`、`after/`、`comparison.png`、`motion.mp4`。旧／新截图机位不同，不作为严格同镜头 A/B；场景像素未修绘。

复核：

```sh
./scripts/build.sh mac
Builds/macOS/Momo.app/Contents/MacOS/Momo -batchmode --garden-feeding-review --hand-motion-review -screen-width 390 -screen-height 844 -logFile artifacts/first-person-hands/player.log
Builds/macOS/Momo.app/Contents/MacOS/Momo -batchmode --hand-anatomy-review -screen-width 1024 -screen-height 768 -logFile artifacts/first-person-hands/calibration.log
ffmpeg -framerate 30 -i artifacts/first-person-hands/motion/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 20 artifacts/first-person-hands/motion.mp4
```

## 接受边界

本轮修正了截图中的急折成因，并增加连续动作证据。现有皮肤贴图、指腹接触、抓粮动作编排、腕部极限附近的蒙皮和猫的嘴部动作仍有原型感，尚不能称为 CS 同等级的角色资产与动画。

本轮没有更新 iPhone。新的俯身视角、触控、走近感应的组合与设备性能仍需真机验收；没有以录制成功或数值检查替代用户对自然度的接受。
