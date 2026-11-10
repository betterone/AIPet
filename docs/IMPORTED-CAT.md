# 混元站姿猫接入（2026-09-23）

第二个用户模型 `580743ed2307e41a576a307c50143168.glb` 已转换为带蒙皮和基础动作、可在当前 Momo 中运行的角色。原下载文件未修改。第一个坐姿模型未替换到游戏中。

## 直接使用

- 桌面：打开 `Builds/macOS/Momo.app`，房间默认显示导入的灰色虎斑猫。已有宠物的名字、性格、关系、事件和存档保持不变。
- Settings → `Home model: imported tabby` 可切回 `original breed`；再次点击可恢复导入猫。
- Boutique 的九个原有品种仍保留。当前导入猫是房间的独立外观覆盖，不是九品种的重新建模；开启覆盖时，无论领养哪个品种，房间外观都是这只灰虎斑。
- iPhone：使用 `Builds/iOS-Window/Unity-iPhone.xcodeproj`；导出脚本已保留并核对本机原有签名设置。
- 通用模型：`artifacts/imported-cat/Tabby-rigged.glb` 内嵌贴图、骨骼、权重和八段动画。
- Unity 资源：`Assets/Momo/Resources/ImportedCat/Tabby.fbx` 与同目录贴图、材质和着色器。
- Blender 源文件：`artifacts/imported-cat/Tabby-editable.blend`。

## 处理结果

| 项目 | 原始模型 | 当前结果 |
| --- | --- | --- |
| 三角面 | 1,500,000 | 40,000 |
| 骨骼 | 无 | 27 个（包含根骨） |
| 顶点权重 | 无 | 每点最多 4 个、已归一化 |
| 贴图 | 3 张 4096 | 3 张 2048 |
| 动作 | 无 | Idle / Walk / Sit / Sleep / Eat / Paw / Groom / Stretch |
| GLB 文件 | 约 70 MiB | 约 12 MiB |

保留原始 UV 与花纹，减面后通过封闭体素代理计算热权重，再映射回带 UV 网格。四肢使用两段 IK 烘焙，脊柱、头颈与尾巴分别绑定。GLB/FBX 均可脱离本项目继续编辑。

Unity 使用 Legacy Animation 混合，行走相位按实际位移推进；停止后平滑回到行为对应的静止动作，附加有限的头颈注视与抚摸反应。FBX 默认动画列表在注册前清空，蒙皮固定四权重；尺寸在第一次实际采样后根据烘焙网格归一化，避免 FBX 单位转换覆盖造成模型缩小。

房间世界每 5 模拟秒更新一次宠物位置，表现层现在沿下一段路线连续预测中间位置，并用短时平滑吸收更新误差。导入猫和原程序化猫共用这条显示路径；存档、寻路目标和自主行为状态不改。脚步相位由实际位移驱动，速度变化时不再按固定时钟空踩。最新连续行走预览是 `artifacts/imported-cat/walk-smooth.mp4`。

原 GLB 的材质设置为 `doubleSided`。Unity 使用 `Momo/Imported Cat Fur` 保留双面渲染，并降低法线和高光强度。不能直接换回单面 Standard，否则生成网格的部分耳朵、腿和尾巴会出现可见空洞。iOS 贴图使用 ASTC 6×6。Unity 材质使用 BaseColor 和 Normal，金属度固定 0、光滑度 0.08；MetalRough 保留在交付资源内，供其他渲染器使用。

## 验证和画面证据

- `scripts/model-tools/validate_glb.py`：独立读取成品 GLB，检查索引范围、有限坐标、归一化四权重、骨骼引用、三张 2K 贴图和八段动画首尾一致；记录在 `artifacts/imported-cat/validation.log`。
- 实际 macOS Player：八种姿态截图、尺寸与镜头边界、蒙皮有限坐标、按位移行走、停止切待机、抚摸／注视、切回原猫，以及宠物状态保持不变。记录在 `artifacts/imported-cat/player-review.log` 和 `player/PASS.txt`。
- 八姿态实拍拼图：`artifacts/imported-cat/poses.png`。
- 行走录屏：`artifacts/imported-cat/walk.mp4`，90 个实际 Player 帧，30 fps，3 秒。测试使用固定时间步推进，不作为设备帧率数据。
- 平滑行走回归：`artifacts/imported-cat/walk-smooth.mp4`，90 帧；连续速度范围和支撑脚滑移记录在 `artifacts/imported-cat/player-review.log`。
- 原 Boutique 回归通过：四种窗口比例、36 次预览、点选隔离与领养返回；`artifacts/boutique-review.log`。
- macOS Unity 构建通过：`artifacts/unity-build-mac.log`。
- iPhone Unity 导出与 Xcode Release 未签名编译均通过：`artifacts/unity-build-ios-window.log`；原生未签名编译：`artifacts/xcode-ios-window-imported-cat-build.log`。

这些验证不能代表真机帧率、耗电、温度或自然程度已被用户接受。

## 明确的完成边界

这是可运行、可继续打磨的角色第一版，动作是本地编写并烘焙的程序化基础动作，不是动捕。Sleep 是趴卧姿态，眼睛仍然睁开；Eat / Groom 只实现低头、转头和抬爪，没有张嘴或真实舔毛接触。尚无眼睑、嘴部形变和毛发系统；近距离还能看出生成网格与贴图的局限。

减面并不等于人工四边面重拓扑。极端动作与近景表情仍需要后续动画／角色制作。真机视觉、触摸与性能需要在 iPhone 上继续验收。

## 复现

模型处理使用 Python 3.11、`bpy==4.5.3`、NumPy、SciPy、Pillow。本机依赖位于 `/private/tmp/momo-model-tools`；目录被清理时需要重新创建虚拟环境并安装这些依赖。处理过程不调用付费服务。

```sh
/private/tmp/momo-model-tools/bin/python -u scripts/model-tools/prepare_cat.py /path/to/source.glb
/private/tmp/momo-model-tools/bin/python scripts/model-tools/validate_glb.py
./scripts/build.sh mac
Builds/macOS/Momo.app/Contents/MacOS/Momo --imported-cat-review -logFile "$PWD/artifacts/imported-cat/player-review.log"
./scripts/build.sh ios-window
```

`prepare_cat.py` 的骨骼坐标按本次站姿猫定制，不是任意动物的一键绑定工具。新的模型需要重新确定骨骼位置。`--imported-cat-review` 使用临时独立存档，不覆盖用户宠物数据；需要图形桌面运行。
