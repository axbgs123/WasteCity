# 大型废墟 `Ruins` 八件低模模块包资产记录

## 状态与边界

- 关联需求：`IDEA-0004`
- 生产状态：参考板与八件重制模型均已由用户批准；Unity 运行时接入已实现，待用户视觉复验
- 制作日期：2026-08-09
- 工具：Blender 5.2.0 LTS，hash `fbe6228777e7`；Blender MCP 1.2 已启用
- 源文件：`ArtSource/FirstPass/Environment/Terrain/Ruins/Ruins_ModuleKit.blend`
- Unity 交换目录：`Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/`
- 坐标：Blender 内部 Z-up；FBX 使用 `-Z Forward / Y Up / Scale 1.0`
- Pivot：每件模型原点为 `(0,0,0)`，底面在 Blender `Z=0`，导入 Unity 后对应底面 `Y=0`
- 接入状态：已通过 `FirstArtRuinsCliffCatalog3D`、Profile、8 个运行时 Prefab、共享材质、确定性布局和 Ruins 类别合批接入默认 3D 场景；不扩展冻结 2D `VisualLibrary`
- 交付提交：`323f5e2`（已推送 `codex/first-art-pass-terrain`）
- 玩法边界：原始模型和运行时 Prefab 没有 Collider、Rigidbody、WasteCity MonoBehaviour、资源节点或通行规则；稳定 ID 仅由 Catalog 提供表现映射，不成为第二套玩法真值

## Unity 运行时接入

- 原始 FBX 保持在 `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/`，ModelImporter 的 Read/Write 关闭；Builder 不修改 FBX 或 `.meta`。
- 8 个运行时 Prefab 位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/`。每个 Prefab 只含 Transform、MeshFilter、MeshRenderer，并内嵌唯一可读的 `<StableId>_RuntimeMesh` 子资源。
- Profile 位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset`；共享几何材质位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/`。
- Builder 从批准的 raw FBX 确定性复制运行时 Mesh；重复重建原位更新同一子资源，Prefab GUID 与 Mesh localFileID 由 focused 测试保护。
- 运行时只消费既有 `WorldMapModel` 地形分类和 Profile 映射，把 Ruins placement 合批为一个 owned Mesh/Renderer；不保留逐格 Prefab，不提供 Collider、阻挡、展开或建造真值。Ruins 失败时只恢复 Ruins 灰盒，不触碰 Cliff、连续地表或资源节点。
- 当前自动证据：Ruins/Cliff AssetBuilder focused EditMode `37/37`，固定捕获 `12/12` 张；最终日常 EditMode `1454/1454`（只排除 `TerrainAssetDeep`）、完整 PlayMode `91/91`，均为零失败、零跳过。两类合计布局与合批五次中位数 `59.1255 ms`、总初始化五次中位数 `95.8269 ms`、稳定观察 `300` 次托管分配 `0 B`。最终 v8 的 Windows Release 3D、Development 3D、legacy 2D 与 macOS universal 3D 四个构建均成功；三个 Windows Player 为 `PE32+` GUI x86-64，macOS 精确 binary 为 universal `x86_64 arm64`。每次完整退出后 `21` 个 ProjectSettings 与 `14` 个运行时 Prefab 哈希精确稳定，恢复标记和备份无残留。macOS 精确 binary 的 `45` 秒 NullGfx 冒烟只有 `31` 条预期 unsupported Shader 错误，脚本异常、空引用、未处理异常、Missing Script 与崩溃为 `0`；该无图形设备冒烟不证明真实渲染。真实 Windows 10/11 Player 的 GPU/显存/内存/视觉冒烟和本次运行时用户视觉复验仍待补。

## 逐件规格

尺寸为 Blender 源空间 `X×Y×Z`，单位与外城逻辑格合同一致；FBX 回读后尺寸和三角面数完全一致。

| FBX | 三角面 | 尺寸 | 视觉职责 |
|---|---:|---:|---|
| `SM_Ruins_CrackedFloorSlab.fbx` | 1892 | `1.2218×1.0555×0.1341` | 破裂工业地坪板、密集崩边碎屑与局部薄尘膜 |
| `SM_Ruins_RubblePile_A.fbx` | 1636 | `0.9675×0.7731×0.2495` | 紧凑混凝土碎块堆，包含表皮、断面骨料和积尘 |
| `SM_Ruins_RubblePile_B.fbx` | 1900 | `1.3987×0.6308×0.2484` | 细长板片、边缘碎屑与少量锈钢碎片混合堆 |
| `SM_Ruins_RebarConcreteBlock.fbx` | 1084 | `1.0504×0.8010×0.4655` | 带三根短弯钢筋、顶部崩口和根部碎屑的破损混凝土块 |
| `SM_Ruins_BrokenPipe.fbx` | 1884 | `0.9226×0.7145×0.6572` | 可见断口、表面破洞、锈带和内部积尘的短工业管段 |
| `SM_Ruins_DrainageChannel.fbx` | 1816 | `1.1000×0.6346×0.2231` | 连续双侧破损沟沿、暗色槽底与局部积尘 |
| `SM_Ruins_BoundaryEdge.fbx` | 1680 | `1.2099×0.7274×0.2083` | 单侧分段破损路缘与积尘地面的低矮过渡边缘 |
| `SM_Ruins_WornMarkingPlate.fbx` | 1064 | `1.1058×0.8130×0.0675` | 近贴地旧标线承载板，两条长磨损赭黄标线 |

## 文件与 SHA-256

| 文件 | SHA-256 |
|---|---|
| `Ruins_Modules_Approved_AI_Reference_v001.png` | `f72aa401942a0956f9d027486eb9639acc18825ef06f22776c5b0336f333458c` |
| `Ruins_ModuleKit.blend` | `d675193ff238e817c3b6dc2b50661954bb44c50ad80f6dae6d0c3b592d87416e` |
| `Ruins_ModuleKit_Generator.py` | `6be8945b5178e32cfdfc6061062053b63c6118718269c73660c167662f469748` |
| `SM_Ruins_BoundaryEdge.fbx` | `ab1bd3659dc8c597157b0809d003cacf5249faff13467a2e8fab768f3578462d` |
| `SM_Ruins_BrokenPipe.fbx` | `9f0bdd5b6243116ef0ab4a03dbd42c4fdf0eba78f0064ece28c61092912c0aa2` |
| `SM_Ruins_CrackedFloorSlab.fbx` | `f1f56dfb5980a6ef130d9077cf5c2627e1d89c61bd22d570673cb3d9a1715abe` |
| `SM_Ruins_DrainageChannel.fbx` | `761106c806de9c59ef8ea1a03250c0c45bec9564cbc7c8977263cd5cc175336b` |
| `SM_Ruins_RebarConcreteBlock.fbx` | `c15b067a5085e8dee7d6bab440231d69a4a1fc0a2796a0e5418c4ef370ffc2d0` |
| `SM_Ruins_RubblePile_A.fbx` | `5bf95e96957334c32d6caa07f634f6f3ed397c5d791078922c740f2e00153a8c` |
| `SM_Ruins_RubblePile_B.fbx` | `aa8b820ac601916ce242bfa499344f13eef3930918eb53a90d8bd52fc4f2718d` |
| `SM_Ruins_WornMarkingPlate.fbx` | `8f5cc5150f64e8cf9f2ca3b288982dc48a135aa5dfb10c2256ee136a0f0517c2` |
| `QA_Ruins_ModuleKit_DefaultOrtho.png` | `9fc5300467ca99c67f7574c93f41dc4fcddddf6a6817acd1531fa084397e7d63` |
| `QA_Ruins_ModuleKit_WastelandContext.png` | `586bbc6993c77b0aef0bd7763a21d3a489f26372ed875826b986bc05ea3c579b` |
| `QA_Ruins_ModuleKit_Top.png` | `e41d246f2876fe556fde951220ad14e1868ca43ef1900007cef24a3d18e22c9c` |
| `QA_Ruins_ModuleKit_Wireframe.png` | `f79aac0e4c5f48e0259dd12e6f5884b336389d16c71cf0581617f2b353f82bb7` |

资产记录自身和来源说明不登记自身 SHA-256，以避免自引用；完整性由 Git 提交对象保证。

## 自动验证结果

- `Ruins_ModuleKit_Generator.py` 通过 Python AST 语法检查，并由 Blender 5.2.0 LTS 无界面完整执行，退出码为 `0`。
- `.blend` 无界面回读得到 `gameplay_truth=none`、`colliders=none`、`module_count=8`。
- 八件源 Mesh 均为独立对象，三角面范围 `1,064–1,900`，全部满足 `200–2,000` 要求；位置均为 `(0,0,0)`，最低点严格校正为 `Z=0`。
- 每件均有一套 UV 和 `3–7` 个真实材质槽；完整混凝土表皮、裸露骨料、积尘/薄尘膜、暗色地坪/沟槽、锈钢和残旧标线按职责分区，不以单一材质换色冒充。
- 八个 FBX 已逐个导入全新 Blender 空场景，全部只有一个同名 Mesh；回读三角面、尺寸、UV 数量、材质槽数量、位置和最低点与源对象一致。
- 默认倾斜正交图检查整体风格、轮廓与高度；普通荒地组合图检查物品与已批准地面的综合色温、积尘尺度和粗糙度连续性；顶视图检查占地和互不重复；线框图检查拓扑密度和独立轮廓。
- `.blend` 已打包批准参考图和生成脚本；没有把正式场景、玩法代码、地形枚举、通行规则、稳定 ID、Packages 或 ProjectSettings 纳入修改范围。

## 人工验收入口

用户已于 2026-08-09 查看重制结果并明确回复“可以了”，当前四张 QA 图作为批准版本的固定视觉证据：

1. `QA_Ruins_ModuleKit_DefaultOrtho.png`：八件套是否像同一套暖黄积尘工业废墟模块，剪影是否适合默认镜头；
2. `QA_Ruins_ModuleKit_WastelandContext.png`：物品与普通荒地是否属于同一套废土美术语言，同时仍能辨认混凝土、断面、沥青、锈钢和标线；
3. `QA_Ruins_ModuleKit_Top.png`：八件职责是否清楚、占地是否紧凑、细长瓦砾堆是否仅轻微超格；
4. `QA_Ruins_ModuleKit_Wireframe.png`：低模轮廓是否清晰、没有隐藏高密度网格。

2026-08-09 的“可以了”只批准离线模型视觉与 Blender/FBX 交付；本次 Unity 运行时接入虽已实现并有自动证据，仍需用户另行视觉复验，不能沿用旧批准冒充运行时人工验收。
