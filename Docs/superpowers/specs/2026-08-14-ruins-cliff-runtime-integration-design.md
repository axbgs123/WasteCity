# 《废土移动城市》Ruins / Cliff 模块运行时接入设计规格

> 日期：2026-08-14
> 关联需求：`IDEA-0004`
> 需求状态：已明确 / 已批准 / 开发中
> 父规格：`Docs/superpowers/specs/2026-08-08-first-art-pass-production-design.md`
> 地表运行时规格：`Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md`
> 资产记录：`Docs/Art/FirstPass/Terrain/Ruins/Ruins_ModuleKit_AssetRecord.md`、`Docs/Art/FirstPass/Terrain/Cliff/Cliff_AssetRecord.md`
> 设计基线：`9b8b533ea7b130a7a93c847c17085adc54f44cbd`
> 当前阶段：仅书面设计；尚未创建实施计划、Prefab、共享材质、运行时代码或场景引用

## 1. 目标与范围

本里程碑把已通过离线视觉和导入验收的 Ruins 八件、Cliff 六件 FBX 制作为 14 个 Unity Prefab，并作为七类正式连续地表之上的几何增强接入默认 `GrayboxPrototype3D`。

本规格只细化父规格中已经批准的 Ruins / Cliff 后续接入，不改变其余要求。未在本文重新定义的美术、坐标、LFS、许可证、命名、Windows 构建和人工视觉批准规则，继续引用父规格和地表运行时规格。

完成后应满足：

1. `WorldMapModel`、`WorldTraversalKind` 和现有坐标映射仍是唯一玩法真值；
2. 14 个 Prefab 只提供可替换表现，不携带碰撞、通行、放置或存档状态；
3. 相同规则地图产生字节级稳定的模块种类、朝向和组合顺序；
4. 长期运行只保留合批后的 Ruins / Cliff 表现，不保留逐格 Prefab 实例；
5. Ruins 与 Cliff 分别事务化接入；任一类失败时只恢复该类灰盒，不破坏已成功的连续正式地表或另一类模块；
6. 用户通过默认镜头、顶视和灰盒对照图确认边界可读性后，才可记为视觉通过。

## 2. 现有事实与本批差异

当前 `FirstArtTerrainRenderer3D` 成功后会通过 `GrayboxWorldView3D.SetSurfaceFallbackVisible(false)` 隐藏七类灰盒组，其中已经包含：

- `world.obstacle.ruins`；
- `world.obstacle.cliff`。

连续正式地表已经用控制图显示 Ruins / Cliff 材质，所以新几何是其上的增强层，不是第二张规则地图，也不替代连续地表。新模块失败时不能把已成功的正式地表一起销毁；必须按稳定 ID 选择性恢复 Ruins 或 Cliff 灰盒组。

本批与 2026-08-10 地表规格的唯一范围变化是：该规格第 17 节明确排除的 14 个 FBX / Prefab 现在进入独立里程碑。Texture2DArray、控制图、地表 Shader、DeepWater 和七层固定顺序不在本批修改范围。

## 3. 选定架构

采用“现有正式地表入口 + 两个几何子事务 + 合批输出”的方案：

```text
GrayboxWorldView3D 生成规则世界与全部灰盒
  -> FirstArtTerrainRenderer3D 原子生成连续正式地表
  -> Ruins 子事务：验证映射、布局、合批、验证 Renderer
  -> Cliff 子事务：验证映射、布局、合批、验证 Renderer
  -> 每类成功后才单独隐藏该类灰盒
  -> 每类失败时清理该类正式几何并单独恢复该类灰盒
```

`FirstArtTerrainRenderer3D` 仍是 `GrayboxSceneBootstrap` 唯一配置的 `IGrayboxTerrainPresentation3D`。新增几何实现放在 `WasteCity.ArtIntegration3D`，由该 presenter 组合调用；不新增第二个 Bootstrap 地表入口，不允许两个 presenter 争夺同一灰盒显隐权。

建议新增职责：

- `FirstArtRuinsCliffProfile3D`：保存 14 个 Prefab 和共享材质的批准引用；
- `FirstArtRuinsCliffCatalog3D`：冻结表现稳定 ID、Prefab 名称和材质槽语义；
- `FirstArtRuinsCliffLayout3D`：纯确定性地图投影，输出只读 placement 描述；
- `FirstArtRuinsCliffGeometry3D`：验证 Prefab、合批临时实例数据并拥有两个运行时 Mesh；
- 独立 Editor Builder：确定性创建或更新 14 个 Prefab、共享材质和 Profile，不改地形数组 Builder。

## 4. 稳定 ID 与 14 个 Prefab 映射

玩法类型只使用现有稳定 ID：

| 玩法来源 | 灰盒/范围稳定 ID | 正式几何类别 |
|---|---|---|
| `WorldTraversalKind.Ruins` | `world.obstacle.ruins` | Ruins 八件模块 |
| `WorldTraversalKind.Cliff` | `world.obstacle.cliff` | Cliff 六件模块 |

Prefab 需要独立的表现稳定 ID；这些 ID 只用于映射、诊断和测试，不写回 `WorldCell` 或存档：

| 表现稳定 ID | FBX | Prefab |
|---|---|---|
| `art.ruins.cracked-floor-slab` | `SM_Ruins_CrackedFloorSlab.fbx` | `PF_Ruins_CrackedFloorSlab.prefab` |
| `art.ruins.rubble-pile-a` | `SM_Ruins_RubblePile_A.fbx` | `PF_Ruins_RubblePile_A.prefab` |
| `art.ruins.rubble-pile-b` | `SM_Ruins_RubblePile_B.fbx` | `PF_Ruins_RubblePile_B.prefab` |
| `art.ruins.rebar-concrete-block` | `SM_Ruins_RebarConcreteBlock.fbx` | `PF_Ruins_RebarConcreteBlock.prefab` |
| `art.ruins.broken-pipe` | `SM_Ruins_BrokenPipe.fbx` | `PF_Ruins_BrokenPipe.prefab` |
| `art.ruins.drainage-channel` | `SM_Ruins_DrainageChannel.fbx` | `PF_Ruins_DrainageChannel.prefab` |
| `art.ruins.boundary-edge` | `SM_Ruins_BoundaryEdge.fbx` | `PF_Ruins_BoundaryEdge.prefab` |
| `art.ruins.worn-marking-plate` | `SM_Ruins_WornMarkingPlate.fbx` | `PF_Ruins_WornMarkingPlate.prefab` |
| `art.cliff.straight-a` | `SM_Cliff_Straight_A.fbx` | `PF_Cliff_Straight_A.prefab` |
| `art.cliff.straight-b` | `SM_Cliff_Straight_B.fbx` | `PF_Cliff_Straight_B.prefab` |
| `art.cliff.inner-corner` | `SM_Cliff_InnerCorner.fbx` | `PF_Cliff_InnerCorner.prefab` |
| `art.cliff.outer-corner` | `SM_Cliff_OuterCorner.fbx` | `PF_Cliff_OuterCorner.prefab` |
| `art.cliff.end-cap` | `SM_Cliff_EndCap.fbx` | `PF_Cliff_EndCap.prefab` |
| `art.cliff.top-cap` | `SM_Cliff_TopCap.fbx` | `PF_Cliff_TopCap.prefab` |

数组顺序必须由上述常量表固定，不得依赖 `AssetDatabase.FindAssets`、目录枚举、Inspector 拖拽顺序或 Prefab 文件名排序。ID 必须通过现有 `StableId` 格式验证。

## 5. Prefab 与共享材质合同

14 个 Prefab 放在各自 `Runtime/Prefabs/`，外部共享材质放在 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/`。FBX 本身及其 `.meta` 保持批准内容和 GUID 不变。

Prefab 合同：

- 根对象名称与 Prefab 名一致；
- 只允许 Transform、MeshFilter、MeshRenderer；不得包含 Collider、Rigidbody、Animator、脚本、灯光、相机或粒子；
- Mesh 必须直接来自对应 FBX，不能复制或运行时修改源 Mesh；
- Prefab 根可保存一次批准的贴地、朝向和视觉尺度校准；运行实例不得再随机缩放或镜像；
- `Y=0` 贴合数学地面，仅允许防 Z-fighting 的固定小正偏移；
- 材质槽必须全部映射到批准的外部共享材质，不能保留自动生成的每-FBX材质副本。

共享材质按 FBX 已验收槽名冻结为 13 个角色：

```text
Ruins: MAT_Ruins_Concrete, MAT_Ruins_Aggregate,
       MAT_Ruins_DustFilm, MAT_Ruins_Dust,
       MAT_Ruins_DarkFloor, MAT_Ruins_DrainDark,
       MAT_Ruins_Rust, MAT_Ruins_Marking
Cliff: MAT_Cliff_Strata, MAT_Cliff_Fracture,
       MAT_Cliff_Dust, MAT_Cliff_Rubble, MAT_Cliff_Mineral
```

它们使用批准的 URP Lit Shader、共享实例和已验收的颜色/PBR职责。运行时不得 `new Material` 或访问 `.material`。同名角色在全部 Prefab 中必须引用同一 Material GUID；Builder 遇到未知、遗漏或重复槽名时在修改正式资产前失败。

## 6. 为什么不直接复用旧 `VisualLibrary`

现有 `VisualLibrary.Resolve(stableId)` 只返回一个 `VisualDefinition`；一个定义最多保存一个 Sprite、一个 Prefab 和一个回退色。现有 `VisualSlot` 以 `SpriteRenderer` 为占位，并通过 `Instantiate` 创建单个 Prefab，适用于冻结 2D 场景中的一对一视觉替换。

本批需要同一 `world.obstacle.*` 稳定 ID 映射多个模块、读取邻接拓扑、确定朝向、按材质合批、按 Ruins/Cliff 分组回退，并且长期不能保留逐格实例。直接复用旧库会迫使它承担并不具备的多变体和 3D 生命周期职责，或产生大量逐格 GameObject。因此不修改、不扩展旧 2D `VisualLibrary`；新目录只在 `WasteCity.ArtIntegration3D` 内消费，冻结 2D 行为保持不变。

## 7. 确定性布局

布局器只接收 `WorldMapModel` 与 `PlanarCoordinateMapper3D`，按 `y` 后 `x` 的固定顺序扫描。它不得读取当前时间、`UnityEngine.Random`、相机、帧数、硬件、文件顺序或 PlayerPrefs。

### 7.1 Ruins

- 每个 `WorldTraversalKind.Ruins` 格恰好生成一个 placement，确保没有未覆盖格；
- 变体索引使用固定整数哈希 `Hash(width, height, x, y, ruinsSalt) % 8`；
- 朝向使用另一固定盐得到 `0/90/180/270`；
- 八件均匀进入候选表，不使用运行时密度滑杆；
- Prefab 经过一次场景校准后，主体不得越出本格边界到足以误导相邻格通行；如批准模型无法在不明显变形的前提下满足，停止实现并回到设计审查。

### 7.2 Cliff

每个 Cliff 格先计算北、东、南、西四邻居的 Cliff 位掩码，再选模块：

| 正交邻居形态 | 模块 | 朝向规则 |
|---|---|---|
| 0 个 | `TopCap` | 哈希四向旋转 |
| 1 个 | `EndCap` | 开口背向唯一邻居 |
| 2 个且相对 | `Straight_A/B` | 轴向由邻居决定，A/B 由哈希奇偶决定 |
| 2 个且相邻 | `OuterCorner` | 两个连接边决定旋转 |
| 3 个 | `InnerCorner` | 缺失边决定旋转 |
| 4 个 | `TopCap` | 哈希四向旋转 |

每个 Cliff 格同样恰好一个 placement。Prefab 根负责将已批准模块校准到一个逻辑格的可读占用；不修改玩法格、不生成多格占用，也不让模型 Collider 决定边界。若单格校准会严重破坏已批准视觉，必须停止并另行批准多格铺设方案，不能让几何越界冒充规则。

哈希算法及盐值在纯 C# 常量中冻结；相同宽高与逐格规则内容必须产生完全相同的 placement 序列和矩阵。地图重建后只按新规则地图重算，不序列化布局。

## 8. 表现与玩法真值隔离

模块系统只读取：

- `WorldCell.Traversal`；
- `WorldMapModel.Width/Height/Get`；
- `PlanarCoordinateMapper3D.TryCellToWorld`；
- 本规格冻结的表现 ID、Prefab、材质和视觉校准。

它不得：

- 修改 `WorldCell`、`WorldMapModel`、`TerrainKind` 或 `WorldTraversalKind`；
- 提供 Collider、NavMesh、射线表面或建筑投影面；
- 决定通行、移动倍率、城市展开、建造合法性、资源节点或寻路；
- 写入正式存档或改变 schema `30`；
- 根据模型 Bounds 扩大玩法阻挡范围；
- 隐藏资源节点、城市、建筑、UI 或其他稳定 ID。

## 9. 分组原子回退

`GrayboxWorldView3D` 增加按稳定 ID 控制 surface slot 的窄接口，同时保留现有“全部显示/隐藏”入口供正式地表使用。新接口只接受已存在的七类 surface 稳定 ID；未知 ID 必须失败，不得静默忽略。

状态规则：

| 连续地表 | Ruins 模块 | Cliff 模块 | 可见结果 |
|---|---|---|---|
| 失败 | 不运行/清理 | 不运行/清理 | 七类灰盒全部恢复 |
| 成功 | 成功 | 成功 | 正式地表 + 两类正式几何；两类灰盒隐藏 |
| 成功 | 失败 | 成功 | 正式地表 + Cliff 几何 + Ruins 灰盒 |
| 成功 | 成功 | 失败 | 正式地表 + Ruins 几何 + Cliff 灰盒 |
| 成功 | 失败 | 失败 | 正式地表 + 两类灰盒 |

每个类别按以下原子顺序执行：验证全部映射与共享材质 → 生成完整 placement → 在临时对象中完成合批 → 验证 Mesh/Renderer/材质 → 交换为当前有效几何 → 最后隐藏该稳定 ID 灰盒。任何一步失败都清理该类别临时与旧正式几何、恢复该稳定 ID 灰盒，并记录一次带类别和原因的错误；不得触碰另一类别、连续地表或资源节点。

`ClearPresentation`、禁用、销毁、世界重建和配置替换必须先恢复 Ruins/Cliff 灰盒，再销毁所拥有的运行时 Mesh。连续地表失败的原有全量回退优先级最高。

## 10. 合批与性能预算

14 个 Prefab 是资产合同和 Mesh/材质来源，不作为长期逐格对象实例化。运行时按类别与共享材质槽合批：

- 一个 `RuntimeGeometry` 根；
- 最多两个长期子对象：`RuinsGeometry`、`CliffGeometry`；
- 最多两个 MeshFilter、两个 MeshRenderer、两个运行时 Mesh；
- 合计最多 13 个共享材质槽/SetPass；
- 合批器必须先以检查溢出的整数累计每个最终 Mesh 的实际顶点数，再确定性选择索引格式：不超过 `65,535` 个顶点使用 `UnityEngine.Rendering.IndexFormat.UInt16`，超过该阈值必须在写入三角形和 submesh 前设置为 `IndexFormat.UInt32`；默认 `64×48` 地图只要任一类别超过阈值就必须使用 `UInt32`；
- 禁止依赖 Unity 的隐式索引格式、截断索引、回绕顶点，或为了规避 `UInt32` 而错误拆分材质 submesh；若目标平台不支持所需索引格式，必须让该类别事务失败并选择性恢复对应灰盒；
- 零 Collider、Rigidbody、Animator 和常驻 `Update/LateUpdate`；
- 不保留逐格 Prefab、临时 Mesh 或材质实例；
- 64×48 seed `8128` 的布局与合批五次中位数目标不超过 100 ms；
- 连续地表加本批几何的总初始化五次中位数目标不超过 250 ms；
- 预热后连续 300 帧，本模块后代托管分配为 `0 B`；
- 默认 1920×1080 目标仍为 60 FPS，并记录 Renderer、SetPass、三角面、CPU/GPU帧时和内存，不用 NUnit 耗时代替真实 Profiler。

若两 Mesh/十三材质预算无法满足，应先优化合批、材质复用或可见性，不得通过删除规则格、降低阻挡可读性或创建第二套简化地图解决。

## 11. 场景与 Authoring

场景继续只序列化一个正式表现 owner：

```text
GrayboxWorld
└── FirstArtTerrainPresentation
    ├── FirstArtTerrainRenderer3D          # 序列化组件
    ├── RuntimeSurface                     # 仅运行时
    └── RuntimeGeometry                    # 仅运行时
        ├── RuinsGeometry                  # 仅运行时
        └── CliffGeometry                  # 仅运行时
```

`FirstArtTerrainRenderer3D` 增加批准的 Ruins/Cliff Profile 引用。场景不得序列化 `RuntimeSurface`、`RuntimeGeometry` 或其子对象。

Authoring 必须：

1. 在场景 mutation 前验证 14 个 FBX、14 个 Prefab、13 个共享材质和 Profile 全部存在且 GUID/类型正确；
2. 使用独立 geometry asset builder，不能调用或扩写 Texture2DArray Builder 来顺带生成模型资产；
3. 只增量设置批准引用，不重建场景基础、URP、地表数组或灰盒对象；
4. 连续执行两次后场景字节、Prefab/Material/Profile GUID 和关键 GlobalObjectId 不变；
5. 破损场景或资产在 mutation 前失败，不生成第二个 presenter 或半套 Prefab；
6. 不修改 Build Settings、GraphicsSettings、QualitySettings、冻结 2D 或默认 schema。

## 12. TDD 与测试设计

实施计划必须按以下顺序先 RED、再最小 GREEN：

1. Catalog/Profile：14 个稳定 ID、Prefab、FBX 和 13 个材质角色一一对应，重复/缺失/未知槽失败；
2. 纯布局：Ruins 八变体、Cliff 六种邻接形态、旋转、扫描顺序和哈希确定性；
3. Prefab 合同：对应 Mesh、贴地/朝向、无脚本/Collider/逐资产材质副本；
4. 合批：placement 数与规则格一致，长期对象和 Renderer 不随格数增长，材质引用均为共享资产；合成夹具分别覆盖 `65,535` 顶点边界两侧，证明 `UInt16/UInt32` 选择发生在索引和 submesh 写入前，且顶点、索引和每个材质 submesh 均未截断或错位；
5. 分类回退：注入 Ruins 或 Cliff 单独失败，证明只恢复对应稳定 ID，正式地表和另一类保持；
6. 生命周期：重复 `TryPresent`、禁用/启用、世界重建、配置替换和销毁无残留、无重复对象；
7. 场景/authoring：唯一 owner、精确 Profile 引用、运行时子对象未序列化、重复 authoring 幂等；
8. PlayMode：真实 `GrayboxPrototype3D` 中两类几何与规则格逐项一致，城市移动/展开、A*、建造投影、资源节点和系统菜单不回归；
9. 性能与 Player 构建：结构、300 帧分配、Profiler、Shader/Material 保留和独立程序日志。

不得删除旧断言、放宽现有地表原子回退测试，或以直接调用内部可见性字段代替真实 presenter 生命周期测试。

## 13. 完整验收门

- 14 个既有 FBX 的 importer、GUID 和内容哈希保持；
- 新增 14 个 Prefab、13 个共享材质和一个 Profile 全部通过合同；
- focused EditMode 与正式场景 PlayMode 通过；
- 日常完整 EditMode（排除 `TerrainAssetDeep`）与完整 PlayMode 通过；
- Unity 无界面编译通过；
- 默认 Release 3D、Development 3D 和 legacy 2D Windows 构建成功；
- macOS Player 进行一次 3D 可见性冒烟，防止 `BUG-0005` 类 Shader 剥离回归；
- 能执行时补真实 Windows 10/11 独立程序至少 12 秒冒烟；不能执行时明确待补；
- 64×48 默认 seed 与合成邻接夹具均满足结构和性能预算；
- 文档生成、质量目录、复用目录和只读验证通过；
- 固定输出默认镜头、顶视、Ruins 近景、Cliff 直段/内外角/端头、灰盒回退和混合成功/失败对照图；
- 用户明确确认边界、比例、遮挡和综合色温后，才可把本子项记为视觉通过。

## 14. 本批不运行 `TerrainAssetDeep`

本里程碑不得修改地形源 PNG、其 importer 规则、`FirstArtTerrainAssetBuilder`、四个 Texture2DArray、数组层顺序、数组序列化或地表 Shader。新增的是 FBX 对应 Prefab、共享模型材质、确定性布局、合批和分组回退，因此只运行日常 EditMode 和相关美术/场景/PlayMode 测试，不运行 `TerrainAssetDeep`。

如果实现中发现必须修改上述任一深度地形范围，立即停止、报告计划外边界并重新批准；获批后才改变测试调度。

## 15. 明确排除与停止门

本批排除：

- 修改 14 个已验收 FBX 或其 `.meta`；
- 修改七类源贴图、数组、控制图权重或 DeepWater；
- 正式资源节点、城市、建筑、UI、VFX、SFX 或环境装饰接入；
- Collider、NavMesh、地形高度、阴影代理或遮挡剔除系统重做；
- 玩法、存档、schema `30`、冻结 2D、默认场景或 Build Settings 变化；
- 运行时随机密度、地图 seed 存档或模块布局存档；
- 把本批完成描述为整个 `IDEA-0004` 或第一版美术包完成。

出现以下情况立即停止并回到设计审查：

- 单格视觉校准只能通过明显破坏已批准模型比例完成；
- 模块需要 Collider 或第二套阻挡数据才能正确工作；
- 无法在保持正式地表的同时选择性恢复 Ruins/Cliff 灰盒；
- 需要长期逐格 GameObject/Renderer 或运行时材质实例；
- 需要修改源 FBX、稳定 ID、玩法坐标、地表数组或全局 URP；
- 性能超预算且只能靠减少规则格或削弱边界可读性解决。

## 16. 完成定义

本规格经主审后才能编写独立实施计划。实现、测试、构建、性能、固定视觉证据和用户验收全部完成前，状态只能是“开发中”或“已实现待验证”。

最终回写必须准确区分：14 个源模型已验收、Prefab/运行时接入已实现、自动验证已通过、用户视觉是否通过。任何一项不得替代另一项。
