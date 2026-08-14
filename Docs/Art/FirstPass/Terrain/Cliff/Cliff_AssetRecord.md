# 悬崖 `Cliff` 首版材质与模块包资产记录

## 状态与边界

- 关联需求：`IDEA-0004`
- 生产状态：参考板、正式 PBR 材质与六件模块均已于 2026-08-09 由用户验收通过
- 参考批准：用户查看 8 格材质/模块参考板后回复“通过”
- 正式验收：用户查看普通荒地组合图、模块组合图和 4×4 平铺图后再次回复“通过”
- 工具：Blender 5.2.0 LTS，hash `fbe6228777e7`；Blender MCP 1.2 已启用
- Unity 接入状态：已通过 `FirstArtRuinsCliffCatalog3D`、Profile、6 个运行时 Prefab、共享材质、确定性布局和 Cliff 类别合批接入默认 3D 场景；不扩展冻结 2D `VisualLibrary`，待用户视觉复验
- 玩法边界：没有 Collider、Rigidbody、WasteCity MonoBehaviour、资源节点、通行规则、阻挡真值或稳定 ID；资产只提供可替换视觉表现
- 坐标：Blender 内部 Z-up；FBX 使用 `-Z Forward / Y Up / Scale 1.0`
- Pivot：每件原点 `(0,0,0)`，底面在 Blender `Z=0`，导入 Unity 后对应底面 `Y=0`
- Git：本记录、资产和生产计划状态位于同一交付提交；记录不自引未知提交哈希

## Unity 运行时接入

- 原始 FBX 保持在 `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/`，ModelImporter 的 Read/Write 关闭；Builder 不修改 FBX 或 `.meta`。
- 6 个运行时 Prefab 位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/`。每个 Prefab 只含 Transform、MeshFilter、MeshRenderer，并内嵌唯一可读的 `<StableId>_RuntimeMesh` 子资源。
- Profile 位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset`；共享几何材质位于 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/`。
- Builder 从批准的 raw FBX 确定性复制运行时 Mesh；重复重建原位更新同一子资源，Prefab GUID 与 Mesh localFileID 由 focused 测试保护。
- 运行时只消费既有 `WorldMapModel` 地形分类和 Profile 映射，把 Cliff placement 合批为一个 owned Mesh/Renderer；不保留逐格 Prefab，不提供 Collider、阻挡、展开或建造真值。Cliff 失败时只恢复 Cliff 灰盒，不触碰 Ruins、连续地表或资源节点。
- 当前自动证据：Ruins/Cliff AssetBuilder focused EditMode `37/37`，固定捕获 `12/12` 张；最终日常 EditMode `1454/1454`（只排除 `TerrainAssetDeep`）、完整 PlayMode `91/91`，均为零失败、零跳过。两类合计布局与合批五次中位数 `59.1255 ms`、总初始化五次中位数 `95.8269 ms`、稳定观察 `300` 次托管分配 `0 B`。最终 v8 的 Windows Release 3D、Development 3D、legacy 2D 与 macOS universal 3D 四个构建均成功；三个 Windows Player 为 `PE32+` GUI x86-64，macOS 精确 binary 为 universal `x86_64 arm64`。每次完整退出后 `21` 个 ProjectSettings 与 `14` 个运行时 Prefab 哈希精确稳定，恢复标记和备份无残留。macOS 精确 binary 的 `45` 秒 NullGfx 冒烟只有 `31` 条预期 unsupported Shader 错误，脚本异常、空引用、未处理异常、Missing Script 与崩溃为 `0`；该无图形设备冒烟不证明真实渲染。真实 Windows 10/11 Player 的 GPU/显存/内存/视觉冒烟和本次运行时用户视觉复验仍待补。

## 材质合同

- 随机种子：`813417`
- 贴图尺寸：2048×2048
- 周期切口：`1682,118`
- 构成比例：大型风化岩层 55%、深裂隙 20%、暖黄积尘 15%、坡脚碎石 7%、矿物竖痕 3%
- BaseColor 平均 RGB：`0.29599534 / 0.23696370 / 0.18490896`
- Height：16-bit 值域 `0.34163424–0.61202411`，共 `17,708` 个离散值；不是 BaseColor 灰度复制
- Normal：平均长度 `1.00038587`，最小 `0.99347351`，最大 `1.00651473`
- Metallic：恒为 `0`
- AO：`0.58039216–1.0`
- Detail Mask：`0.20784314–0.98823529`
- Smoothness：`0.05098039–0.16470588`
- Blender 材质源元数据：`gameplay_truth=none`、`coverage_ratios=55/20/15/7/3`、`texture_resolution=2048`，四张正式贴图已打包

## 六件模块规格

尺寸为 Blender 源空间 `X×Y×Z`；六件均为一个独立 Mesh、一个 UV 集、五个真实材质槽。FBX 无界面回读后的三角面、尺寸、UV、材质槽、位置和底面与源对象一致。

| FBX | 三角面 | 尺寸 | 视觉职责 |
|---|---:|---:|---|
| `SM_Cliff_Straight_A.fbx` | 1,446 | `2.7554×1.3248×1.4994` | 标准横向分层直段，稀疏落石和贴面矿痕 |
| `SM_Cliff_Straight_B.fbx` | 1,472 | `2.7086×1.3628×1.5087` | 轮廓、裂层和顶部碎屑不同的第二直段 |
| `SM_Cliff_InnerCorner.fbx` | 1,644 | `2.7031×2.1752×1.5013` | 统一高度的内角衔接模块 |
| `SM_Cliff_OuterCorner.fbx` | 1,618 | `2.2188×2.3012×1.5082` | 陡直外角与双向坡脚落石 |
| `SM_Cliff_EndCap.fbx` | 1,418 | `2.4330×1.3235×1.4992` | 圆钝但不可通行的终端封口 |
| `SM_Cliff_TopCap.fbx` | 1,246 | `2.6205×2.7481×1.4996` | 大面积顶部填充与垂直侧壁封口 |

五个材质职责固定为：

1. `MAT_Cliff_Strata`：暖褐风化横向岩层；
2. `MAT_Cliff_Fracture`：暗色新鲜断面与深裂带；
3. `MAT_Cliff_Dust`：顶部暖黄尘壳；
4. `MAT_Cliff_Rubble`：脚部和顶部小尺度碎屑；
5. `MAT_Cliff_Mineral`：贴附岩面的克制纵向矿物污痕。

## 文件与 SHA-256

### 材质、分层源与批准参考

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_BaseColor.png` | 2048² RGB 8-bit / sRGB | `cf2c6eeb48245ba4e201298fba604300e9593ea1f668368fce6b1eb3a53b3b6b` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Normal.png` | 2048² RGB 8-bit Tangent Space / Linear | `f0a95e4ba080d0801f1201f7ac409c0bd1dd3f5df6ffde9faf3b9234b905d376` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Mask.png` | 2048² RGBA 8-bit / Linear | `18b557991e3b2e00eb8066cc8f347f0ed4a6a7a638f8e0bb8fd1a0e3ce78edcd` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/T_Terrain_Cliff_Height.png` | 2048² Gray 16-bit / Linear | `ba4b2da115ba16e6cce453b27c8edc41ad93dbb753e07fcc99de7631b7118783` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_Golden_Master.ora` | 六层无损 OpenRaster 分层源 | `10b0d14726ac7565c121d5fec0adb69ddd6e29dcee751976aa6a1a117faeba42` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_Golden_Generator.blend` | Blender 5.2.0 LTS，四图已打包 | `3f225f415636b37d04ee6c2e3efc1390d3a5dcd290df6d0409de94d825affd32` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `9bba03d9ea75d847bc21518bc801098744ce5dc23bf75d157f85d7a5ef67c5b0` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/References/Cliff_MaterialAndModules_Approved_AI_Reference_v001.png` | 1774×887 RGB 8-bit / 已批准参考板 | `e76d8d0e86b78c30475181aad0d99a58637b1d7b6dc756af76ffdf09c74be15d` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/References/Cliff_SourceNotes.md` | UTF-8 Markdown | `d95138ad06429c68492130b9bc6ef2e092e28d72e5864d9c743f01a32d0eb77d` |

### 模型源、验证器与 FBX

| 文件 | SHA-256 |
|---|---|
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_ModuleKit.blend` | `9ffebfb7ccd5695cff25faa25d9d693f1c8af202109a28ecac0bf00c93afa0f2` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_ModuleKit_Generator.py` | `bfb83e29d1c58c150d22163c87dea6f8507b91537ff784b4d4cb72ec1a831125` |
| `ArtSource/FirstPass/Environment/Terrain/Cliff/Cliff_Validation.py` | `53162debb5cfc836632132dea5d83e705562f2cf1394d169a39c0bf995b04b55` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_A.fbx` | `d4a7d40965708a7f21be961b3f2aabe93030adcd9cc197bdc41abc17612a6537` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_B.fbx` | `f47369d722e696fb2873e587ded1c154656ecdfef2c77b2a6fb6dfcacd43c587` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_InnerCorner.fbx` | `249b8f52ba3273197ddf1ce9deca639aad1f8b022d5652bb76795e2d65b5854a` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_OuterCorner.fbx` | `c4e024f304487fd6f1353088ec4972c47cb7142b87f07613321ca3614bc13e0c` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_EndCap.fbx` | `f05a74cf6dff1a40759c588542ecf376a6bad07ca88279a3519df1e2a6a7fb76` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_TopCap.fbx` | `bc5a4a2d0a0ddeb46f783dc9f74e6d045b2e281ed9ab971056f4a368fd9cb14c` |

### 固定 QA 图

| 文件 | SHA-256 |
|---|---|
| `QA_Terrain_Cliff_Tiling4x4.png` | `9d816507394440d4b7136fea806fc57716276945b75db638aa7a855dc06cb0f7` |
| `QA_Terrain_Cliff_DefaultOrtho.png` | `61c39553a6306920a5ca9309a522539743b12a59d716a82b0517d13cde5e3477` |
| `QA_Terrain_Cliff_PBRCheck.png` | `6817b9c8126b859c26746ea536dc3ba2232bf8e21750889c23c29b9f03693d03` |
| `QA_Cliff_ModuleKit_DefaultOrtho.png` | `545f6ea0f5a71c38ba9237e2a2f0bb7875418a5d2aca03cb5d8f5570ef7bff38` |
| `QA_Cliff_ModuleKit_WastelandContext.png` | `da983f2b789403d2dbfe67c76041bdb49b29e92750614db9b224c2ff99c2bd35` |
| `QA_Cliff_ModuleKit_Top.png` | `657a6e50b3ba040247a60e72733cb922d6bdf9b08215e0e86e52d43f40723b01` |
| `QA_Cliff_ModuleKit_Wireframe.png` | `de7406bf315ce0345ac1f2033212dcb12f64a555e9762a38a42b6d088c1dd902` |
| `QA_Cliff_ModuleKit_Assembled.png` | `6c91cf9d9c858434afac049d06ece6f85a079f257742ce6219d7ba0280633a42` |

资产记录自身不登记自己的 SHA-256，避免自引用；完整性由 Git 提交对象保证。

## 自动验证结果

- 三个 Python 文件通过 AST 语法检查；生成器由 Blender 5.2.0 LTS 无界面完整执行，退出码为 `0`。
- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor X/Y 边界平均差 `0.00040722 / 0.00047998`，低于内部相邻变化 `0.00175478 / 0.00189825`。
- Normal X/Y 边界平均差 `0.00270182 / 0.00276948`，低于内部相邻变化 `0.00745234 / 0.00755005`。
- Mask X/Y 边界平均差 `0.00088226 / 0.00096316`，低于内部相邻变化 `0.00265804 / 0.00271617`。
- Height X/Y 边界平均差 `0.00052046 / 0.00087797`，低于内部相邻变化 `0.00227436 / 0.00243320`。
- 4×4 检查图无硬接缝、黑线或棋盘边界；默认倾斜正交与 PBR 图用于检查纹理尺度、法线和粗糙度响应。
- OpenRaster 包含 `mimetype`、`stack.xml`、合并图、缩略图和 6 个图层文件，可无损回读。
- 材质 `.blend` 无界面回读得到 `gameplay_truth=none`、`coverage_ratios=55/20/15/7/3` 和 4 张打包贴图。
- 模块 `.blend` 无界面回读得到 `module_count=6`、`gameplay_truth=none`、`colliders=none`；六件源 Mesh 全部满足 `200–2,000` 三角面、一个 UV、五个材质槽、原点零位和底面 `Z=0`。
- 六个 FBX 已逐个导入全新 Blender 空场景，全部只有一个同名 Mesh；三角面、尺寸、UV、材质槽和底面与源对象一致。
- 普通荒地组合图验证综合色温与已批准 Wasteland 一致；组合图验证直段和转角可以形成连续、陡直、不可误读为坡道的视觉边界。
- 正式场景、Prefab、玩法代码、地形枚举、通行规则、稳定 ID、Packages 和 ProjectSettings 均未修改。

## 人工验收结论

用户已于 2026-08-09 查看正式候选并回复“通过”。该批准覆盖当前 PBR 材质、六件模型、FBX 与固定离线 QA；本次 Unity 运行时接入虽已实现并有自动证据，仍需用户另行视觉复验，不能沿用旧批准冒充运行时人工验收。
