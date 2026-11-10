# 明亮玩具猫咪家园：独立视觉样片

日期：2026-10-20。

## 已接入现有游戏

按用户后续要求，视觉风格已通过共享 `ToyHomeStyle` 接入 `MomoApp` 的正式房间入口。现有猫、行为、存档 schema、家具位置、导航与观察操作继续使用原实现；生产模式保留完整外墙和分区墙，不采用比较样片的剖面处理。真实 BoxCollider 尺寸在几何倒角后保持一致。iOS 更新目标为现有 `com.momo.aipet.prototype`，版本 0.2.0 / build 8。

原独立样片继续作为对照工具。下文关于“不修改默认游戏／未打包到 iPhone”的内容仅描述最初样片阶段，不代表本次生产接入后的交付状态。

33 项核心规则、桌面与 iOS 分支 C#、原生 ARKit 编译链接、8 段音频检查通过。现有 macOS 游戏的自动检查确认新主题已启用且分区墙未被剖切；双区移动、门洞、动态找猫、主动躲猫猫及两种手机尺寸界面回归通过。更新前已备份实体手机的两份宠物检查点，并验证 SHA-256 完整性。设备签名、安装及启动结果以本轮设备记录为准。

现有 iPhone 工程已使用原签名配置完成 Release 构建和签名校验。build 8 已更新安装到已连接的实体 iPhone；安装后、首次启动前回读两份存档，与安装前备份逐字节一致。首次远程启动超时且设备要求解锁；后续重试返回启动成功（PID 62815），再次激活也成功，独立进程查询确认该 Momo 进程仍在运行。当前没有可交付的 Momo 前台真机截图，不将进程运行等同于视觉、触控或性能验收通过。

证据：`artifacts/xcode-ios-window-toy-home.log`、`artifacts/iphone-toy-home-install.json`、`artifacts/iphone-before-toy-home`、`artifacts/iphone-after-toy-home-install`、`artifacts/iphone-toy-home-launch-retry.json`、`artifacts/iphone-toy-home-foreground.json`、`artifacts/iphone-toy-home-process.json`。这些存档备份属于本地用户数据，不应随代码公开。

## 方向与范围

参考用户提供的「AI 红发魔女的工作台」实际画面，提取明亮色块、微缩家园、低多边形植被、夸张轮廓与接地阴影；没有下载或复制该页面的角色、代码或模型。

本轮使用现有 RoomView 的双区布局和家具位置，加入草坪边沿、果树、灌木、花朵、窗檐瓦片与猫耳门廊。现有虎斑猫、蒙皮、纹理和动画保持原样，行为继续调用同源 Simulation 与 CatView。环境为程序化创建的独立样片。

配色：草绿 #98C873、奶黄 #FFD475、湖蓝 #5599B5、陶红 #DE7957、暖白 #FFF5D8、木色 #DDA55F。家具增加真实几何倒角；右侧玩耍区以草坪色探索室内外相接的方向。Built-in 管线与 Standard 材质，无新增后处理或渲染包。

## 运行

构建入口：Unity 菜单 `Momo → Build Visual Style Study`。

独立场景：`Assets/Momo/Scenes/VisualStyleStudy.unity`。

独立应用：`Builds/VisualStyleStudy/MomoVisualStudy.app`。

- Tab 或左上按钮：切换当前空间／玩具家园。
- C 或左上按钮：切换全景／猫咪近景。
- 点击猫：抚摸；点击可行走地面：呼唤。
- R：重置样片中的这一刻；Esc：退出。

样片没有调用 SaveStore 或 PlayerPrefs，不读取或写入真实宠物。PetHome 场景、正式应用输出与 iPhone 工程没有替换。MomoApp 仅新增了独立样片场景的启动排除，避免自动创建正式游戏入口。

## 对照与验证

运行独立应用的自动检查：

```sh
Builds/VisualStyleStudy/MomoVisualStudy.app/Contents/MacOS/Momo --visual-style-review -logFile "$PWD/artifacts/visual-style-player.log"
python3 scripts/compose-visual-style-study.py
```

结果目录：`artifacts/visual-style-study`。

- `current-wide.png` / `toy-wide.png`：1440×1000、同机位全景。
- `current-close.png` / `toy-close.png`：1440×1000、同机位猫咪近景。
- `toy-portrait.png`：600×1000 竖屏构图。
- `comparison.png`：原始截图等比例排列，附比较标签，无场景像素修图。
- `PASS.txt`：自动检查完成标记。

为露出室内，两版都隐藏前侧外墙与前侧玩耍区墙。全景使用同一套场景边界求镜头覆盖；近景使用相同位置、视线与视野角；猫的位置、朝向和 Idle 采样相位相同。当前版保留原室内光照，提案版使用独立单盏方向光与三色环境光。检查包括灯光隔离、竖屏场景完整入镜，以及同源宠物模拟和移动表现可以实际推进。

构建日志：`artifacts/visual-style-build.log`；运行日志：`artifacts/visual-style-player.log`。

## 接受边界

这是桌面 Unity 实际渲染的视觉方向验证。当前猫在全景中偏小，近景仍能看到生成模型的材质与网格局限；它尚未随环境一起完成角色美术统一。门廊、植物、窗檐是风格样本，不能把它们当作已经通过导航、遮挡、寻猫玩法和手机触控验证的正式关卡。

未测手机帧率、发热、耗电与长时间交互；没有修改默认游戏画面、打包到 iPhone、上传或发布。下一步应以用户对真实截图的视觉判断为准，再决定将哪些元素合入正式房间。
