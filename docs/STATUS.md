## 家园行为与后院寻趣（2026-11-09）

已实现分散休息／观察位置、错峰后院巡游、当前猫主动靠近／注视／短距离跟随，以及三站共同探索小游戏。方案、操作和验收边界见 [HOME-LIFE-OPTIMIZATION.md](HOME-LIFE-OPTIMIZATION.md)。

107 项 Core 通过，UNITY_IOS 源码编译零警告、零错误。Unity Player 构建因本机许可客户端启动 ObjectDisposedException 阻断；未生成新安装包，新增画面、真机触控和趣味性仍待验收。以下为历史记录。

## 逗猫连扑与成长家园（2026-11-02）

已接入单指连扑短局、近远落点与时机判定、三段连击、即时重开、各猫本领、三种实体设施、七天现实日期路线与本地试玩记录。家园生命模拟与短局分开；后台中止未完成局，结算只提交一次。schema 3 显式迁移旧档并在首次覆盖前保留 schema 2 原字节备份。入口、配置与数据边界见 [WAND-CHALLENGE.md](WAND-CHALLENGE.md)。

当前同源 Core 101 项通过，含真实低／高帧率指针采样、时机边界、无效输入、种子路线、成长、每日组合、防重复结算和旧档备份。桌面与 UNITY_IOS 源码编译通过；Unity macOS 构建与 WindowViewChecks 通过。新增 Player 在 320×700／390×844／768×1024 显式渲染目标下各 156 项通过，覆盖生产输入、全部可领养外观蒙皮、结算／重开、模型复用、后台中断、Unity 存档往返及 UI 文本边界。证据在 `artifacts/wand-challenge/`。

原有 127 项 HUD、1332 帧五猫、掌心喂食和 3928 项洗澡生产回归通过，日志为 `artifacts/wand-regression-*.log`。这些是桌面和离屏证据，不能作为 iPhone 实际触控、持续帧率或留存证明。真人动作与趣味性评价、10 人初次试玩和七天自由回访尚待按 [PLAYTEST.md](PLAYTEST.md) 执行。

iPhone 0.3.0（17）已沿用原标识签名并覆盖安装；四份原存档在首次启动前逐字节保留。远程启动因设备锁屏被 iOS 拒绝，未计为真机前台或触控验证。详情见 [IPHONE-BUILD17.md](IPHONE-BUILD17.md)。

## 提示栏避让方向盘，iPhone 0.3.0（16）（2026-10-29）

用户确认 build 15 的猫和方向盘能在真机正常显示，并指出提示栏遮挡方向盘。已按完整触摸区顶边布局提示卡、留 12 逻辑点间距。127 项 HUD 检查通过，覆盖 320／390／768、提示显示时的区域不重叠与真实 UI 命中，以及既有拖动、看向、菜单和照料入口。

iPhone 已覆盖安装到 0.3.0（16），四份最新存档在首次启动前逐字节保留并验证有效。远程启动因锁屏被拒绝，修复后的真机画面待手动进入验收。详见 [IPHONE-BUILD16.md](IPHONE-BUILD16.md)。以下为历史记录。

## 工具拿取、取液与花洒水束，iPhone 0.3.0（15）（2026-10-29）

已加入盆沿侧托盘、直立泵瓶、花洒挂座与软管，换工具自动执行拿取、按泵、交接、放回和收手。花洒九束细水按实际喷孔方向和重力运动，在猫身、盆壁或水面碰撞，带余滴和短距离毛发流水；实际喷到头脸也会暂停。海绵整体压缩，毛巾以局部弯曲网格贴合，泡沫响应实际接触。托盘避让与快速再次邀请的观察者位置检查已修复。

85 项 Core、3928 项洗澡 Player、109 项 HUD，以及原喂食与 1332 帧五猫回归通过。Unity iOS、Xcode Release、严格签名与实体 iPhone 覆盖安装通过，设备查询版本为 0.3.0（15）；四份存档在首次启动前逐字节保留。首次启动因锁屏被拒绝，用户解锁后已成功启动并确认进程；采集画面仍为主屏／锁屏，尚未完成真机游戏画面与触控验收。详见 [IPHONE-BUILD15.md](IPHONE-BUILD15.md) 和 [BATHING.md](BATHING.md)。以下为历史记录。

## 常驻方向盘与洗澡材质、iPhone 0.3.0（14）（2026-10-29）

已将探索方向盘恢复为左下固定常驻；次要按钮保留自动隐藏。洗澡改为地面入口浴盆，猫脚踩盆底、水位到躯干中部，加入水波和小水滴。泡沫改成大小不同的立体薄膜球面，贴合实际蒙皮并能被冲散；步骤完成时收工具、拉开镜头。具体操作见 [BATHING.md](BATHING.md)。

85 项 Core、3617 项洗澡 Player、109 项固定方向盘 HUD 检查，以及原喂食和 1332 帧五猫回归通过。Unity iOS、Xcode Release、严格签名与实体 iPhone 覆盖安装通过；手机查询为 0.3.0（14），首次启动前四份存档逐字节保留。新版成功启动并确认进程；采集屏幕时手机再次锁屏，未完成真机游戏画面和触控验收。详情见 [IPHONE-BUILD14.md](IPHONE-BUILD14.md)。以下条目是历史记录，以本条为当前状态。

## iPhone 覆盖更新：0.3.0（12）（2026-10-28）

已沿用原 Bundle ID 和签名配置，将洗澡与精简界面覆盖安装到连接的实体 iPhone。Unity iOS 导出、Xcode Release 签名构建与严格签名验证通过，手机版本查询确认为 0.3.0（12）。安装前后四份存档逐字节一致、完整性与当前 Core 读取验证通过；原文件已在本机备份。首次启动被锁屏拒绝，用户解锁后已成功启动并确认进程存在；后续截图采集到锁屏，未计为游戏画面验证。真机触摸、自然度与持续性能仍须单独体验。证据见 [IPHONE-BUILD12.md](IPHONE-BUILD12.md)。

## 探索界面精简与自动隐藏（2026-10-28）

普通探索撤掉底部工具栏，保留猫咪、菜单与互动三个入口；左下改为按下出现、松手停止并淡出的浮动摇杆。六秒无输入后辅助界面淡出，仅留菜单；首个轻点只唤回，拖动则同一手势立即看向。喂食／洗澡保留必要操作和退出；洗澡卡缩为 108 点，当前步骤完成后才显示换工具。设置可选自动／常显，感应异常提示不会因静置隐藏。参考与操作见 [QUIET-HUD.md](QUIET-HUD.md)。

84 项 Core 检查、macOS 构建、WindowViewChecks 与 UNITY_IOS 源码编译通过；106 项新 HUD Player 检查、完整洗澡与原喂食、1332 帧五猫生活、实际桌面窗口三种手机宽度的 UI 点击与布局回归通过。实际图像在 `artifacts/quiet-hud`。本轮未更新 iPhone；真机触摸、感应与持续性能仍须单独验收。

## 后院手动洗澡（2026-10-28）

后院加入可自行走入的浅洗澡盘，当前关注的猫可接受或拒绝邀请。第一人称花洒、海绵与毛巾按打湿、搓泡泡、冲净和擦干推进，按真实工具接触与身体分区覆盖计进度；操作过急可暂停或结束。每猫独立保存后院活动脏污，离线不增加，完成才提交清洁；后台、设置与切猫会清理临时会话。泡沫与湿润按角色独立着色，旧档默认干净。

80 项核心测试、macOS 构建与 WindowViewChecks、UNITY_IOS 源码编译通过；最终 Player 洗澡流程通过 3483 次检查，覆盖导入猫与原有猫、三种布局和生命周期中断。原掌心喂食、1332 帧五猫生活及桌面窗口三种手机宽度的 Mobile HUD 回归通过。操作与证据见 [BATHING.md](BATHING.md)。尚未更新 iPhone，动作自然度、实际触控、持续性能与发热仍待真人／真机验收。

## 掌心接触、转身进食与手猫避碰（2026-10-26）

修正家庭朝向覆盖喂食转身、宽松嘴部距离判断、避碰前后掌心目标不一致，以及地面食盆动作干扰颈部的问题；猫会转向最终食物位置，从上方低头接近，再允许吃粮。手指、掌心、前臂和猫头／身体加入接触代理；托粮时猫避让稳定的手，摸头与收手持续解重叠。原始卡通猫也改为使用网格表面接触代理，不再把放大的点击区域当作物理表面。保留用户指定的左右模型绑定。

67 项核心测试、macOS 构建与 WindowViewChecks、UNITY_IOS 源码编译通过；六种外观分别实际吃粮并完成同时摸头。最终 540 帧／18 秒完整动作逐帧检查通过，接触代理最大重叠约 0.00276，低于 0.006 阈值。录像、验证来源与限制见 [FEEDING-CONTACT.md](FEEDING-CONTACT.md)。这些检查不证明完整皮肤逐三角形无穿透，也不包含独立下颌／舌头动画或用户自然度接受；本轮未更新 iPhone。

## 第一人称持手与腕部扭曲修正（2026-10-23）

按用户截图与 CS 式自然持手要求，改用肩—肘—腕正向骨骼求解，手掌随前臂调整方向，加入两处前臂旋转过渡和腕部 18° 目标限制；喂食时平滑降低视角、收近托粮距离，结束恢复站姿。用户指定的左右模型绑定保留。

macOS 构建、WindowViewChecks、iOS 分支源码编译、完整喂食回归及腕部校准通过；420 帧／14 秒连续动作录像已生成，最大腕部弯折约 18.3°，臂长保持固定。实际画面、录像及限制见 [FIRST-PERSON-HANDS.md](FIRST-PERSON-HANDS.md)。尚未更新 iPhone；质感与动作仍未达到 CS 级别，也未获得用户自然度接受。

## 按用户要求对调左右手模型（2026-10-23）

左侧抚摸改用原 Right 模型，右侧托粮改用原 Left 模型；拇指对掌随模型切换，操作与入画侧沿用原逻辑。最终 macOS 构建、指定模型绑定校准与完整喂食回归通过。画面：`artifacts/hand-swap/after/`。本轮未更新 iPhone。此前的左右手视觉判断由本次用户指定映射取代。

## 左右手与拇指结构纠正（2026-10-23）

用户再次否定手部自然度并指出拇指方向。移除运行时负缩放镜像；烘焙独立左右手皮肤、法线、绕序和绑定姿态；普通手指按局部关节轴屈曲，拇指单独对掌；完整上臂与前臂采用固定长度求解，取消最高 6 倍的拉伸。左右手掌心／手背／托粮／抓握校准、完整喂食回归及 macOS 构建通过，iOS 分支源码编译通过。证据与限制见 [HAND-REWORK.md](HAND-REWORK.md) 第二次纠正部分。

方向和结构检查通过不等同于自然度通过。手部资产与程序姿态仍粗糙，尚未真人接受，也未更新 iPhone。

## 手部模型替换（2026-10-23）

用户指出手部过假后，移除球体／胶囊拼接手、独立胶囊前臂和圆柱袖口，接入带皮肤贴图的连续手掌／腕部／前臂网格。来源为 para / MakeHuman 的 CC0 资源，已清除原 FPS 控制与动画，配置分节手指弯曲、前臂伸入、托粮比例和抚摸姿态。来源、修改与边界见 [HAND-REWORK.md](HAND-REWORK.md)。

最终 macOS 构建、iOS 分支源码编译及完整掌心喂食 Player 回归通过；已复查 390／320 宽实际双手画面。仍未更新 iPhone，连续动作自然度和真机表现尚待人工验收，不把模型替换视为写实手部成品。

## 视觉规范统一与整体复核（2026-10-23）

统一房间与小店的木色、湖蓝、薄荷布料、珊瑚点缀及中性日光；提高 UI 次级文字对比度，饱腹／亲密改为图标与数值分离的状态卡，统一面板关闭按钮、字体层级与阴影。喂食收粮按钮与提示卡分离，前臂减细并从侧面入画。

65 项核心检查、桌面／iOS C#、macOS 构建、四种尺寸房间与六类面板、掌心喂食与多猫回归通过；小店单独运行窗口检查。实际画面、规则、验证证据与开放门槛见 [VISUAL-QUALITY.md](VISUAL-QUALITY.md)。本轮没有更新 iPhone。小店实渲确认卡通与较写实模型混用，角色造型、近景贴图和动作自然度尚不满足整体视觉成品验收，不能以本轮 UI／配色完成代替该门槛。

## 多猫同住与空间约束（2026-10-22）

房间已支持逐只领养至五只猫，独立需要／性格／习惯／随机状态与回忆，名册关注切换，有方向的猫间关系，可接受／拒绝／中断的嗅闻、追逐和附近休息。家庭统一处理前台移动、身体扫掠、家具净空、动态绕行、窄门退让、观察者双向避让、共享食盆／水盆排队和红球占用。原单猫检查点显式迁移到独立的家庭 schema 2 文件，旧档保留。

65 项核心检查、最终五猫离屏 Player 场景、原厨房／后院／掌心喂食回归、九猫小店批处理回归通过；iPhone 0.3.0（11）Release 签名构建与签名校验通过，并已安装、成功启动。画面、完整结果与边界见 [MULTI-CAT.md](MULTI-CAT.md)。高级配对动画（蹭头、互舔）和扑跳尚未制作；身体包围范围无重叠不等同于完整皮肤逐顶点无穿透，真机自然度、触摸和长时间性能仍须单独验收。

# Momo 原型状态（2026-10-22）

## 四房后院与掌心喂食（2026-10-22）

四个室内区域连接可通行后院，猫和玩家共用扩大的导航；室内外粮袋、右手抓粮翻掌、左手摸头、进食暂停恢复，以及饱腹／亲密百分比图标与收益反馈已接入。40 项核心测试、实际 Player 的完整喂食与原有 HUD／探索回归通过；iPhone Release 签名构建通过，build 10 已覆盖安装，安装前后两份宠物存档逐字节一致。远程启动因锁屏被拒绝，实际手机互动和性能仍待体验。规则、截图和证据见 [FEEDING-GARDEN.md](FEEDING-GARDEN.md)。

## 本轮：操作界面改版（2026-10-22）

主界面收敛为猫咪卡、拍照／设置、圆形方向盘及「回忆／一起玩／找猫」。统一图标、圆角、配色与面板层级，视角细项收进设置，保留现有世界交互与存档。

三种竖屏尺寸的主界面与六类面板、长按行走与探索、四种比例的九猫档案与领养流程均通过实际桌面 Player 回归。iOS build 9 签名构建与校验通过，已覆盖安装到原 iPhone Momo 并启动成功；两份宠物存档安装前后逐字节一致。真机触控手感及持续性能未验收。实际画面与分层证据见 [HUD-REDESIGN.md](HUD-REDESIGN.md)。

## 本轮：走近猫、双区域探索与日常躲猫猫（2026-10-20）

源码已加入双区域空间、屏幕四向行走、原生 ARKit 位置追踪桥接、受限真实位移、日常邀请／躲藏／探头／被找到反馈，以及 8 段有来源记录的 CC0 实录声音。旧存档身份、关系与偏好继续保留，前台社交子状态不保存到离线世界。

验证与待完成项记录在 [EXPLORATION.md](EXPLORATION.md)。Unity 许可证恢复后，macOS 成品与 iPhone 工程均已更新；33 项核心测试、桌面与 iOS C# 编译、原生 ARKit 编译链接、8 段声音检查，以及 macOS Player 在 390×844、320×700 的长按行走、跨房间、找猫和完整躲藏／提示流程均通过。iPhone Release 未签名与本地签名构建通过，新版已安装到连接的实体 iPhone；远程启动被锁屏拒绝，真机位置追踪、触控、听感和孩子试玩仍待手动验收。

## 本轮：可连续玩的首次相处（2026-09-29）

房间内新增沿猫身慢慢滑动的连续抚摸反馈、可拖动的羽毛逗猫棒，以及猫在清醒且有精力、玩家暂时停手时主动拨来红球的邀请。房间提示会根据已尝试的互动给出下一种可探索的玩法；不同玩心的猫追羽毛的距离不同。首次领养后猫落在镜头可见的房间位置，再次选择外观时保留原位置。羽毛偏好和实际发生的邀请写入本地存档／回忆，旧存档缺少新增字段时可继续使用。

- `./scripts/test-core.sh`：25/25 通过，包含羽毛偏好防刷、性格差异、主动邀请前提与旧存档兼容。
- macOS Development Player 手机比例检查覆盖 390×844 和 320×700 的「一起玩」四项操作面板；连续抚摸、羽毛拖动和主动拨球均完成实际运行检查：`artifacts/mobile-hud-play-review.log`、`artifacts/mobile-hud/PASS.txt`。画面见 `artifacts/mobile-hud/wand-390.png`、`stroke-390.png`、`invitation-390.png`。
- 九宫格四种比例、九猫档案与首次领养回房间流程继续通过：`artifacts/boutique-play-review.log`。这证明功能和桌面比例布局可运行；首次使用者是否自愿玩满 10 分钟，仍需按 `docs/DEVICE-TEST.md` 在真机做无引导试玩验证。
- 最终版 iPhone 工程已重新导出，原有签名设置保留；Xcode `Debug / iphoneos` 签名编译成功并安装到连接的实体 iPhone。编译证据：`artifacts/xcode-ios-device-play-build.log`。远程启动因设备锁屏被拒绝，因此真机抚摸、逗猫棒触控与实际游玩时长尚未验收。

## 本轮：九只猫的档案（2026-09-28）

九宫格中选一只猫后，单猫页右上角新增「档案」。档案展示名称、品种、与模型设定相符的外观特征、性格概括，以及玩心、好奇心、亲近人、独立性四项初始倾向。关闭后返回同一只猫的互动与领养页面；档案不虚构生日、性别或经历。

- macOS Development Player 在 390×844、375×667、430×932、768×1024 下逐只打开并关闭九份档案，检查内容、文字截断、边界、返回、领养与重新打开流程，全部通过：`artifacts/boutique-profile-window-review.log`、`artifacts/boutique/PASS.txt`。390×844 的档案画面：`artifacts/boutique/profile-4-390.png`。
- `./scripts/test-core.sh`：22/22 通过。截图与窗口测试属于桌面 Player 验证，真机档案字形、触控区域和安全区仍需设备上确认。
- iPhone 工程已重新导出并保留既有签名设置；Xcode `Debug / iphoneos` 签名编译通过，新版已安装到连接的实体 iPhone。编译证据：`artifacts/xcode-ios-device-profile-build.log`。远程启动被设备锁屏拒绝，因此没有宣称真机档案视觉或触控验收。

## 本轮：可发现的互动与手机手势（2026-09-24）

房间底部新增「一起玩」入口，可直接试摸猫、滚球邀请、叫它过来三种真实互动；说明同时标明对应手势，并显示本次已试过几种。房间空闲提示改为可执行的玩法提示。单指滑动现在支持上下环视；双指缩放增益由每屏高 92° 提至 220°，视场缓动系数由 18 提至 36，仍限制在 48°–92°。九宫格中的猫保持稳定的头部与身体位置，仅保留眨眼、耳朵微动；选中单猫后继续使用较丰富的待机与互动动作。

- Unity 窗口手势检查通过：上下滑动改变俯仰、不误召唤、视角边界、快速捏合缩放和原有水平／陀螺仪逻辑；`artifacts/unity-build-mac.log`。
- macOS 实际 Player 手机尺寸检查通过：390×844、320×700 的房间／设置／互动面板无截字，三个互动按钮实际更新宠物状态；`artifacts/mobile-hud-review.log`、`artifacts/mobile-hud/play-390.png`、`play-320.png`。
- 九宫格四种窗口比例、36 次单猫预览与新增的九猫头部高度稳定检查通过；`artifacts/boutique-review.log`。
- iPhone 工程已重新导出，现有签名配置保留；Xcode `Debug / iphoneos` 签名编译通过，更新版已安装到连接的实体 iPhone。编译证据：`artifacts/xcode-ios-device-interactions-build.log`。远程启动被手机锁屏拒绝，因此真实双指速度、上下拖动方向、九宫格头部观感与互动乐趣仍待解锁后体验确认。

## 本轮：真机文字与行走节奏修复（2026-09-23）

针对使用者的 iPhone 截图，将房间 HUD、Settings 和 Boutique 文字统一改用项目内嵌的 Noto Sans CJK + TextMesh Pro，按手机逻辑尺寸重新布局面板、按钮和字号。中文标题与英文说明都在 320–430 点宽的竖屏预览中完整显示。导入猫的连续位置现在沿世界路线在两次模拟更新之间推进，再做短时平滑；行走周期按实际位移驱动，避免角色停顿后突然追上目标，以及脚步与前进速度脱节。

- 房间及 Settings 在 390×844、320×568 的实际 macOS Player 截图与文字边界检查通过：`artifacts/mobile-hud-review.log`、`artifacts/mobile-hud/`。
- Boutique 四种竖屏／平板比例、九猫文字边界和领养流程通过：`artifacts/boutique-review.log`、`artifacts/boutique/`。
- 导入猫实际 Player 连续行走检查：速度 1.078541–1.08 世界单位／模拟秒；前左爪支撑期最大滑移 0.009886 世界单位。记录在 `artifacts/imported-cat/player-review.log`；新版 90 帧视频是 `artifacts/imported-cat/walk-smooth.mp4`。
- macOS 构建、iPhone 工程导出、iPhone `Release / iphoneos` 未签名编译通过：`artifacts/unity-build-mac.log`、`artifacts/unity-build-ios-window.log`、`artifacts/xcode-ios-window-font-gait-build.log`。真机上的字体观感和走路自然程度仍待复装后确认。
- 最新模拟器工程也已通过 Xcode `Release / iphonesimulator` 编译，并在 iPhone 17 Pro Simulator 安装启动；中文 Boutique 标题和标签的实际渲染截图为 `artifacts/mobile-hud/ios-simulator-final.png`，编译记录为 `artifacts/xcode-ios-simulator-font-gait-final-build.log`。
- 使用工程现有 Team 和 Bundle ID 的 iPhone `Debug / iphoneos` 签名构建通过，已安装到连接的实体 iPhone；记录在 `artifacts/xcode-ios-device-font-gait-build.log`。首次远程启动因设备锁定被系统拒绝；该步骤不构成真机视觉验收。


## 本轮：混元站姿猫资源与运行接入（2026-09-23）

用户提供的第二个 GLB 已从 150 万面减到 4 万面，加入 27 个骨骼、每点最多四权重、八段基础动作与 2K 贴图，并接入房间自主行为、注视、抚摸与模型切换。默认显示灰虎斑外观，Settings 可恢复原品种；原有九猫 Boutique 与宠物存档保留。处理结果、运行方式与完整限制见 [IMPORTED-CAT.md](IMPORTED-CAT.md)。

- 独立 GLB 检查与实际 macOS Player 八种姿态、连续行走、停止、外观恢复及状态不变检查通过；原九猫 Boutique 四种窗口比例回归通过。
- Unity macOS 构建、iPhone 导出和 Xcode iPhone Release 未签名编译通过。已有本机签名设置保留并核对通过。
- 截图与行走视频：`artifacts/imported-cat/poses.png`、`artifacts/imported-cat/walk.mp4`。
- 动作是程序化原型；没有眼睑、嘴部形变或真实舔毛动作，Sleep 目前为睁眼趴卧。未进行本版真机安装、帧率或自然程度验收。

## 本轮：Boutique 手机视觉与取景修复（2026-09-22）

修复竖屏特写过近、UI 从中心向右溢出、固定九宫格标签错位、房间灯光叠加，以及打开按钮松手误选猫咪。改为完整九宫格 → 单猫互动 → 命名／带回家的布局，保留现有角色和单猫存档范围。细节与复验入口见 [BOUTIQUE.md](BOUTIQUE.md)。

- C# 核心规则：22/22 通过，含九个 Boutique 配置的完整性检查。
- macOS 实际运行：四种窗口比例、36 次预览、动画边界、UI 边界／文字高度、展示区比例、点选隔离、领养保存与返回房间；证据在 `artifacts/boutique-review.log`、`artifacts/boutique/`。
- iPhone 工程：`Builds/iOS-Window/Unity-iPhone.xcodeproj`；本机已有签名设置由导出脚本保留。未签名编译证据在 `artifacts/xcode-ios-window-boutique-build.log`。
- 本轮画面证据来自桌面 Player，尚未安装到真机验证 Boutique。


## 本轮：WINDOW 06 方向、捏合与触摸响应

依据真机反馈，将手机感应的水平变化改为与物理转向同向；单指水平滑动增益提高 30%；增加双指捏合，以 48°–92° 垂直视场调整远近。双指期间清除单指交互状态、暂停感应，结束后重新校准，因此不会误触猫、球或地面。界面标识为 WINDOW 06。

Unity 回归覆盖同向水平姿态、倾斜握持、完整水平角、单指增益、上下拖动不改变俯仰、捏合方向和缩放边界。真实双指手感和感应方向仍需在 iPhone 上确认。

## 本轮：WINDOW 05 走进并环视房间

房间由约 8 × 6.6 扩至 12 × 10 个世界单位，并同步更新家具位置、可走区域、球边界与寻路。镜头从窗外推进到房间中心，视野扩大，水平角度不再限制在 ±58°。默认关闭感应，房间左上 `Motion: off` 可直接开启；关闭时触摸只有左右环视，开启后手机转身负责 360° 水平环视、前后倾斜负责垂直视线，手指只作水平补偿。触摸猫后猫会向房间中心靠近，以强化亲昵的可见反馈。

| 本轮验证 | 证据 | 结果 |
| --- | --- | --- |
| 核心世界 | `./scripts/test-core.sh` | 21/21 通过；含扩大后家具避让、球被困/恢复、长时间离线、保存与性格差异。 |
| Unity 导出 | `artifacts/unity-build-ios-window.log` | WindowViewChecks 通过；含自动进入、退回、扩大房间找猫、无边界水平角度、仅水平触摸与原有姿态检查。 |
| iPhone 原生编译 | `artifacts/xcode-ios-window-v05-build.log` | Xcode `Release / iphoneos / CODE_SIGNING_ALLOWED=NO` 编译成功。 |

真机仍需确认感应方向与舒适度，特别是完整转身一圈、手机抬起/放下、关闭感应后的上下滑动不移动视线，以及猫在远角探索时的 Find cat。

## 本轮：WINDOW 04 补齐入口、感应与竖屏文字

检查真机反馈后确认此前代码确有三个未完成点：靠近窗口只由按钮触发；感应依赖可能延迟的能力标志；HUD 按横屏参考尺寸缩放。WINDOW 04 在启动或领养后 0.85 秒自动推进，单次运行内 Step back 后不再强制推进；iPhone 直接启用陀螺仪采样并继续过滤无效数据；HUD 改为按竖屏高度缩放。界面标识为 WINDOW 04。

| 本轮验证 | 证据 | 结果 |
| --- | --- | --- |
| 自动推进与交互回归 | `artifacts/unity-build-ios-simulator.log` | 自动推进阈值、进入观察状态、Step back 后不重复自动推进，以及原有感应、窗口、找猫和触点检查通过。 |
| 模拟器原生编译 | `artifacts/xcode-ios-simulator-v04-build.log` | Xcode `Release / iphonesimulator` 编译成功。当前 CoreSimulator 服务断开，未将本次构建重装进模拟器作画面检查。 |
| 真机工程导出与签名保留 | `artifacts/unity-build-ios-window.log` 与 `artifacts/ios-window-signing.json` | Unity 导出通过，现有本地签名配置恢复并核对通过。 |

真机仍需确认实际陀螺仪方向、延迟和文字观感；这三项不能由模拟器或代码回归替代。

## 本轮：WINDOW 03 横向感应修正

使用者真机反馈：竖屏向左移动时视线向右，而且不够灵敏。本轮反转横向感应输入，默认增益从 1.3 提高到 2.2，Settings → Look speed 改为 1.5 / 2.2 / 3.0 倍；感应缓动系数从 18 提高到 32。首次运行新版启用 2.2 倍，此后保存新版选择，宠物存档不受影响。界面标识 WINDOW 03。

继续使用 [`Builds/iOS-Window/Unity-iPhone.xcodeproj`](../Builds/iOS-Window/Unity-iPhone.xcodeproj)。导出脚本已恢复并核对使用者现有的签名选择。重新运行安装后，点击 Come closer，再回正并检查左右转动；新版真机方向与舒适度仍待使用者反馈。纯平移位置追踪尚未实现。

| 本轮验证 | 证据 | 结果 |
| --- | --- | --- |
| 回归及 Unity 导出 | `artifacts/unity-build-ios-window.log` | WINDOW VIEW CHECKS PASSED；覆盖反转后两侧观察射线、2.2 倍增益、三档切换、倾斜握持、角度跨界、滚转过滤、九点找猫、靠近/退回和触点路径。导出成功。 |
| iPhone 原生编译 | `artifacts/xcode-ios-window-v03-build.log` | Xcode `Release / iphoneos / CODE_SIGNING_ALLOWED=NO` 编译成功，Info.plist 校验通过。未替使用者安装到真机。 |
| 本地签名配置保留 | 构建脚本输出与 `artifacts/ios-window-signing.json` | 导出后恢复并比较通过；未操作 Apple 账号或真机。 |

## 上轮：WINDOW 02 外墙与窗口入口

增加外墙、木窗框、窗台和百叶，启动时从窗外点击 Come closer，用约 1.8 秒靠近窗口；Step back 退回。感应改为重力对齐的水平与俯仰，加入握持校准和灵敏度选择。WINDOW 03 在此基础上修正方向与响应。

上一版 Unity 导出及 Xcode iPhone 未签名编译、Simulator 编译均通过（`artifacts/xcode-ios-window-v02-build.log`、`artifacts/xcode-ios-simulator-v02-build.log`）。实际模拟器已检查外墙、靠近窗口、Find cat 和点猫反馈。截图见 [窗外画面](../artifacts/momo-window02-outside.png)。这些结果不能替代当前版本的真机感应验证。

## 历史：WINDOW 01 窗户视角（2026-09-15）

最新真机工程是 [`Builds/iOS-Window/Unity-iPhone.xcodeproj`](../Builds/iOS-Window/Unity-iPhone.xcodeproj)，使用独立导出目录。原 `Builds/iOS` 和 macOS 构建保留，为之前版本；下方历史验证不等于新版已通过真机验证。

新增局部透视镜头、竖屏陀螺仪姿态控制、空白区域拖动、Find cat、Recenter，以及运动控制开关。触摸猫或球时视角暂停，回前台重新校准。方案与未来 AR 边界见 [WINDOW-VIEW.md](WINDOW-VIEW.md)。

| 本轮范围 | 证据 | 结果 |
| --- | --- | --- |
| 镜头和手势回归 | Unity 构建中的 `WindowViewChecks` | 校准、竖屏轴向、滚转过滤、角度限位、房间九点找猫、拖动不召唤、松手位移与 UI 手势隔离通过。 |
| iPhone 工程 | `artifacts/unity-build-ios-window.log`、`artifacts/xcode-ios-window-build.log` | Unity 导出成功，Xcode `Release / iphoneos / CODE_SIGNING_ALLOWED=NO` 编译成功；新增运动用途说明，plist 校验通过。 |
| 模拟器工程 | `artifacts/unity-build-ios-simulator.log`、`artifacts/xcode-ios-simulator-build.log` | arm64 导出、Xcode 编译、安装、启动成功。 |
| 实际界面 | iPhone 17 Pro Simulator | 已确认局部房间、找猫、回正、近景点猫反馈、Journal、Settings 和原有宠物存档；最新实际截图见下方。 |
| 滑动输入限制 | 模拟器自动拖动日志 | 工具调用 drag 后应用收到的是 `floor / tap / 0px`；因此不能宣称模拟器实际滑动已通过。共享输入路径在编辑器的位移回归检查通过，真实滑动由使用者体验。 |
| 陀螺仪与真机 | 本轮未操作真实手机 | 只验证了姿态换算逻辑；真实方向、舒适度、延迟、恢复校准和触摸稳定性待使用者验证。 |

![窗户视角实际模拟器画面](../artifacts/momo-window-view.png)

## 当前可交付物

- 最新 iPhone 窗口原型：[`Builds/iOS-Window/Unity-iPhone.xcodeproj`](../Builds/iOS-Window/Unity-iPhone.xcodeproj)（0.2.0 / build 9，玩具家园风格与新版操作界面）。build 9 已覆盖更新到连接的实体 iPhone 并启动成功，原存档已核对保留；记录见 [`HUD-REDESIGN.md`](HUD-REDESIGN.md)。
- macOS 原型：[`Builds/macOS/Momo.app`](../Builds/macOS/Momo.app)。
- 历史 iOS Xcode 工程：[`Builds/iOS/Unity-iPhone.xcodeproj`](../Builds/iOS/Unity-iPhone.xcodeproj)。
- iOS Simulator Xcode 工程：[`Builds/iOS-Simulator/Unity-iPhone.xcodeproj`](../Builds/iOS-Simulator/Unity-iPhone.xcodeproj)（WINDOW 02）。
- 一张由运行中的原型实际保存的本地照片：[`artifacts/momo-desktop.png`](../artifacts/momo-desktop.png)。

工程采用 Unity `6000.3.23f1`。Unity Personal 已在本机激活；这个信息只说明编辑器可用，不代表游戏已经发布或有 iOS 分发资格。

## 已验证

| 范围 | 证据 | 结果 |
| --- | --- | --- |
| 规则和存档 | `./scripts/test-core.sh` | 21/21 通过；含确定性推进、离线补算、时间回拨、寻路、互动限额、双检查点、损坏恢复与一周关系/偏好保持。 |
| macOS Player | `./scripts/build.sh mac` | Unity 构建成功，输出 `Momo.app`。 |
| iOS 导出 | `./scripts/build.sh ios` | Unity IL2CPP/Xcode 工程导出成功。 |
| iPhone 编译 | Xcode `Release / iphoneos`，未签名 | 成功生成 `Momo.app`；这证明原生 iPhone 编译通过，不代表已装到手机。 |
| iPhone 签名 | Xcode 现有自动签名配置 | 成功使用现有 wildcard profile 和 Apple Development 证书签名，bundle ID 为 `com.momo.aipet.prototype`。 |
| iPhone 真机启动 | 使用者于 2026-09-15 报告 | 已在手机上正常运行；Xcode 签名和部署由使用者自行完成。 |
| iOS Simulator | Apple Silicon arm64 导出、Xcode 编译、iPhone 17 Pro Simulator | 成功安装并启动，已实际完成领养并进入宠物房间。 |
| 关系成长 | iPhone 17 Pro Simulator 小屏验证 | Journal 已呈现关系阶段、性格、偏好、时段习惯和真实事件；窗边偏好会显示窗边垫。 |
| 真实桌面运行 | 打开 `Momo.app` | 已完成领养、显示自主活动和回忆页；使用者确认 Journal 能在前台打开。 |
| 本地数据 | 退出后重新打开实际 Player | 名称和已领养状态保留，revision 从 9 到 12、世界时间从 2965 到 3295 秒；两个存档的 SHA-256 均有效。 |
| 本地拍照 | 按 `P` | 实际 PNG 已生成并保存在应用数据目录，副本见本页顶部链接。 |

## 真机部署状态

使用者已自行完成 Xcode 配置和部署，并于 2026-09-15 确认 Momo 能在手机正常运行。Apple 开发者账号、设备注册和签名配置仍由使用者控制。

后续如新增设备，用 Xcode 选择同一开发团队，并把模板 Bundle ID `com.momo.aipet.prototype` 换成该团队控制的唯一标识。下一轮真机验收应至少覆盖：触摸猫、地面和球，暂停恢复，30 分钟稳定运行，截图，以及重新启动后的球位置和回忆。模拟器可以覆盖布局、鼠标模拟触摸、存档和基础行为，不能替代这些真机验收。

可直接使用 [DEVICE-TEST.md](DEVICE-TEST.md) 执行本轮真机验收；它将每项操作、预期与异常记录格式固定下来。

## 还不能宣称的内容

- 尚未完成系统化的真机触摸、暂停恢复和 30 分钟性能验证。
- 尚未有 7–14 天目标用户留存或关系感测试。
- 没有云同步、换机恢复、完整历史、公开分发、商业化或隐私政策发布。
- 程序化猫与房间已在桌面实机画面中检查，但其生命感需要真实用户评审，不能由编译通过替代。
