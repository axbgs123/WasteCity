# 《废土移动城市》Ruins / Cliff 模块运行时接入设计规格

> 日期：2026-08-14
> 关联需求：`IDEA-0004`
> 需求状态：已明确 / 已批准 / 开发中
> 父规格：`Docs/superpowers/specs/2026-08-08-first-art-pass-production-design.md`
> 地表运行时规格：`Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md`
> 正式路线图：`Docs/05-Formal-Development-Roadmap-ZH.md`
> 受控记录：`Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0004`
> 资产记录：`Docs/Art/FirstPass/Terrain/Ruins/Ruins_ModuleKit_AssetRecord.md`、`Docs/Art/FirstPass/Terrain/Cliff/Cliff_AssetRecord.md`
> 设计基线：正式文档提交 `5c3466b`
> 实施计划：`Docs/superpowers/plans/2026-08-14-ruins-cliff-runtime-integration.md`
> 当前阶段：书面设计、实施计划与只读模型校准已形成并经主代理目视接受；尚未创建 Prefab、共享材质、运行时代码或场景引用

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

Unity 2022.3.62f1 的只读导入预检进一步证明：14 个 FBX 的原始 Mesh subasset 不包含轴转换，轴转换保存在共同 imported root Transform。Catalog 因此必须为每项公开同一份冻结的 `SourceImportMatrix`，不能让 Builder 或 Geometry 猜测 Euler 角：

```text
SourceImportRotation quaternion = (-0.7071068, 0, 0, 0.7071067)
SourceImportMatrix row-major =
[ 1,  0,                  0,                  0 ]
[ 0, -0.00000011920929,   0.99999994,         0 ]
[ 0, -0.99999994,        -0.00000011920929,   0 ]
[ 0,  0,                  0,                  1 ]
```

Catalog 的每项 `MaterialRoles` 必须按 Unity `MeshRenderer.sharedMaterials` 与 raw Mesh submesh 的实际索引顺序冻结，而不是按材料名称或 Blender/文档展示顺序重排：

| 表现稳定 ID | Unity 导入后的 MaterialRoles / submesh 顺序 |
|---|---|
| `art.ruins.cracked-floor-slab` | `Aggregate, Concrete, DrainDark, Dust, DustFilm` |
| `art.ruins.rubble-pile-a` | `Dust, Aggregate, Concrete` |
| `art.ruins.rubble-pile-b` | `Aggregate, Concrete, Dust, Rust` |
| `art.ruins.rebar-concrete-block` | `Aggregate, Concrete, DustFilm, Dust, Rust` |
| `art.ruins.broken-pipe` | `Concrete, Aggregate, Rust, DrainDark, DustFilm, Dust` |
| `art.ruins.drainage-channel` | `DrainDark, Aggregate, Concrete, Dust, DustFilm` |
| `art.ruins.boundary-edge` | `Aggregate, DarkFloor, Concrete, DrainDark, Dust, DustFilm` |
| `art.ruins.worn-marking-plate` | `Aggregate, DarkFloor, Marking, DrainDark, Concrete, Dust, DustFilm` |
| Cliff 六件 | `Strata, Fracture, Dust, Rubble, Mineral` |

其中七件 Ruins 的旧 Catalog 顺序与 Unity 真值不同，必须按上表纠偏；`broken-pipe` 与 Cliff 六件原顺序已一致。此处只纠正表现映射，不改变 13 个共享材质角色集合。

## 5. Prefab 与共享材质合同

14 个 Prefab 放在各自 `Runtime/Prefabs/`，外部共享材质放在 `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/`。FBX 本身及其 `.meta` 保持批准内容和 GUID 不变。

Prefab 合同：

- 根对象名称与 Prefab 名一致；
- 只允许 Transform、MeshFilter、MeshRenderer；不得包含 Collider、Rigidbody、Animator、脚本、灯光、相机或粒子；
- 每个 Prefab 的 Mesh 必须由对应原始 FBX 确定性派生为唯一、可读的内嵌运行时 Mesh 子资源；原始 FBX 保持 Read/Write 关闭且不被修改，Prefab GUID 与 Mesh localFileID 在重建后保持稳定；
- Prefab 必须镜像 Catalog 的完整零格、零 `quarterTurns` 合成矩阵 `T(childOffset) * S(rootScale) * SourceImportMatrix`，测试比较最终 `localToWorldMatrix`，不能只检查或猜测 Euler/scale 字段；运行实例不得再随机缩放或镜像；
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

本批新增独立 Shader `WasteCity/Terrain/FirstPassGeometry`。它使用 URP PBR 光照，并通过 object/world triplanar 或等价的垂直面安全映射采样现有四个地表 Texture2DArray：Ruins 固定读取层 `4`，Cliff 固定读取层 `6`。属性名冻结为 `_BaseColorArray`、`_NormalArray`、`_MaskArray`、`_HeightArray`、`_LayerIndex`、`_TriplanarScale` 及角色 tint/PBR 参数；材质必须把 `_LayerIndex` 固定为所属类别，不能由实例覆盖。三投影必须按世界/对象法线权重混合，并对 Tangent Space Normal 在 X/Y/Z 投影面执行轴重定向和符号修正，不能把三张法线样本直接相加。映射不得依赖地表控制图、逐格 UV、相机空间或 FBX 内嵌材质，尤其不能让 Cliff 垂直面发生 XZ 平面投影拉伸。

Shader 至少提供 `UniversalForward` 与 `ShadowCaster`，若当前 URP 深度路径需要则同时提供 `DepthOnly`；正向光照使用 URP 正式 PBR 输入和 `UniversalFragmentPBR`（或 Unity 2022.3 / URP 14.0.12 的等价受支持入口），不能以无光照颜色输出冒充 PBR。验证必须同时包含 Shader 编译、Material/数组引用合同和固定垂直面 RenderTexture 像素差；源码字符串检查只能作为补充，不能单独证明垂直面安全映射。

13 个共享材质只配置各角色的色调、Metallic、AO、Detail/Normal 强度和 Smoothness 等角色参数；它们共享上述 Shader 和既有数组引用，不复制纹理、不生成每-Prefab贴图，也不依赖 FBX 内嵌材质。运行时不得 `new Material` 或访问 `.material`。同名角色在全部 Prefab 中必须引用同一 Material GUID；Builder 遇到未知、遗漏或重复槽名时在修改正式资产前失败。

`WasteCityFirstPassTerrain.shader`、四个 Texture2DArray、其层顺序和 `FirstArtTerrainAssetBuilder` 均保持不变。新增几何 Shader 只消费既有数组，不获得生成、替换或重序列化这些数组的权限。

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
- 八件没有连接边语义；校准矩阵以 FBX 导入时 `0°` 为基础朝向，布局器只在此基础上叠加 placement 的 `quarterTurns`，不添加每资产隐藏旋转或镜像；
- Prefab 经过一次场景校准后，主体不得越出本格边界到足以误导相邻格通行；如批准模型无法在不明显变形的前提下满足，停止实现并回到设计审查。

### 7.2 Cliff

每个 Cliff 格先计算北、东、南、西四邻居的 Cliff 位掩码，再选模块：

| 正交邻居形态 | 模块 | 朝向规则 |
|---|---|---|
| 0 个 | `TopCap` | 标准 `0°`；批准套件没有孤立件 |
| 1 个 | `EndCap` | 标准连接 W=`0°`；N=`90°`、E=`180°`、S=`270°`，圆头背离连接边 |
| 2 个且相对 | `Straight_A/B` | 轴向由邻居决定，A/B 由哈希奇偶决定 |
| 2 个且相邻、两臂之间对角格不是 Cliff | `InnerCorner` | 标准 N+W=`0°`，按连接边旋转 |
| 2 个且相邻、两臂之间对角格是 Cliff | `OuterCorner` | 标准 N+W=`0°`，按连接边旋转 |
| 3 或 4 个 | `TopCap` | 标准 `0°`；批准套件没有 T/Cross 件 |

每个 Cliff 格同样恰好一个 placement。Prefab 根负责将已批准模块校准到一个逻辑格的可读占用；不修改玩法格、不生成多格占用，也不让模型 Collider 决定边界。若单格校准会严重破坏已批准视觉，必须停止并另行批准多格铺设方案，不能让几何越界冒充规则。

Cliff 位定义冻结为 `N=1, E=2, S=4, W=8`，Unity 本地轴为 `N=+Z, E=+X, Up=+Y`；从上往下看，正 `90° Y` 把本地 N 转到 E。标准 mask 为：`StraightA/B=10 (E+W)`、`InnerCorner/OuterCorner=9 (N+W)`、`EndCap=8 (W)`、`TopCap=15 (N+E+S+W)`。旋转查表冻结为：Straight `E|W=0°`, `N|S=90°`；Corner `N|W=0°`, `N|E=90°`, `E|S=180°`, `S|W=270°`；EndCap `W=0°`, `N=90°`, `E=180°`, `S=270°`；孤立或三/四邻接的 TopCap 为 `0°`。相邻两连接边之间的对角格只读取既有 `WorldTraversalKind.Cliff`：对角缺失选择 InnerCorner，对角存在选择 OuterCorner；不得创建第二套拓扑真值。

哈希算法及盐值在纯 C# 常量中冻结；混合步骤使用显式 `unchecked uint` 回绕，或使用不会溢出的宽整数后确定性截断，不能依赖编译器默认溢出设置，也不能让正常坐标因 `checked` 乘法抛异常。placement 先保存整数格、模块索引和 `quarterTurns`，再严格按 `T(cell) * Ry(quarterTurns) * T(childOffset) * S(rootScale) * SourceImportMatrix` 形成 `WorldMatrix`；共同 `SourceImportMatrix` 必须恰好消费一次。相同宽高与逐格规则内容必须产生完全相同的 placement 字段和矩阵。地图重建后只按新规则地图重算，不序列化布局。

### 7.3 代码前校准门

离线比例与视觉证据位于 `/private/tmp/wastecity-ruins-cliff/calibration/README.md`、`calibration_matrix.json` 及同目录 `renders/cliff_cell_fit_top.png`、`renders/cliff_cell_fit_ortho.png`、`renders/cliff_uniform_vs_nonuniform.png`。这些证据继续批准 root scale、目标 size 和视觉比例，但旧 `calibration_matrix.json` 不再是 ChildOffset 权威：它在接入 Unity `SourceImportMatrix` 后使用了相反的水平符号。ChildOffset 的唯一权威改为下表及 Task 3.5 从 Unity raw Mesh bounds 生成的纠偏证据。所有校准和纠偏都只读已提交的 14 个 FBX，没有修改 FBX、`.meta` 或正式资产。

下表的 root scale 与目标 size 沿用已批准的离线比例；ChildOffset 则从 Unity raw Mesh 先应用 `SourceImportMatrix`、再应用 `S(rootScale)` 后的 bounds 重新推导。水平分量严格为该 bounds center 的负值，Y 分量严格为使 min Y 回到 `0` 的正微偏移；因此 X/Z 是旧离线表数值取反，Y 保持并须由 raw bounds 复算验证。`scale` 与 `offset` 均按 Unity `X,Y,Z`；root 保持格中心。Ruins 只做不放大的等比缩放；Cliff 的 XZ 等比收进单格，Y 独立校准到 `0.90`，不修改源 Mesh。基础 Y 旋转均为 `0°`，最终 placement 再叠加上节冻结的 `quarterTurns`。

| 表现稳定 ID | root scale X,Y,Z | child offset X,Y,Z | 校准后 bounds X×Y×Z |
|---|---|---|---|
| `art.ruins.boundary-edge` | `0.7438418620039455, 0.7438418620039455, 0.7438418620039455` | `-0.011133320925701306, 0.00000004493711649645367, 0.005296984127656602` | `0.9×0.15491270551043693×0.541050134945314` |
| `art.ruins.broken-pipe` | `0.9755163303025417, 0.9755163303025417, 0.9755163303025417` | `-0.002470288718570525, 0.000000051493899180282064, -0.03244276854602628` | `0.9×0.6410873326965777×0.6970203244973145` |
| `art.ruins.cracked-floor-slab` | `0.7366124449717444, 0.7366124449717444, 0.7366124449717444` | `-0.005478553127634332, 0.00000005787199456683777, -0.03353046140802086` | `0.9×0.09877690067009585×0.7774924384932406` |
| `art.ruins.drainage-channel` | `0.8181818004482052, 0.8181818004482052, 0.8181818004482052` | `0, 0.00000004321330399777432, 0.0056144607475787775` | `0.8999999999999999×0.1825753668205225×0.5192538062170677` |
| `art.ruins.rebar-concrete-block` | `0.8568209086736785, 0.8568209086736785, 0.8568209086736785` | `0.0696553438815491, 0.00000005883560678828004, 0.017981657006396753` | `0.9×0.3988768404107975×0.6862975108916366` |
| `art.ruins.rubble-pile-a` | `0.9302806128327081, 0.9302806128327081, 0.9302806128327081` | `0.00454897037899669, 0.00000006284143144611042, 0.02610424617700395` | `0.9×0.2321418093319265×0.7192274617189032` |
| `art.ruins.rubble-pile-b` | `0.6434742705655297, 0.6434742705655297, 0.6434742705655297` | `0.0014350361567941057, 0.00000003308043718022879, 0.00010610649404046166` | `0.9000000000000001×0.15982392782516824×0.405880371314985` |
| `art.ruins.worn-marking-plate` | `0.8138665074093977, 0.8138665074093977, 0.8138665074093977` | `0.00327274226679653, 0.00000005188448785272019, -0.01237233860783203` | `0.9×0.05490496914480248×0.661674071662537` |
| `art.cliff.end-cap` | `0.36991012107980137, 0.6003284343832568, 0.36991012107980137` | `-0.010333405521301657, 0.000000050116234443793, -0.05523510290215231` | `0.9×0.9×0.48955793525425195` |
| `art.cliff.inner-corner` | `0.33295705133593456, 0.5994717484516436, 0.33295705133593456` | `-0.007434509565184104, 0.0000001335870600587246, 0.09329747471624714` | `0.9×0.9×0.724235948234779` |
| `art.cliff.outer-corner` | `0.39109889207039356, 0.59674880365643, 0.39109889207039356` | `-0.1157214599366119, 0.000000134027025689591, 0.0891520673972466` | `0.8677860067279551×0.9×0.9` |
| `art.cliff.straight-a` | `0.32663329905659955, 0.6002462920829474, 0.32663329905659955` | `-0.0016203349578728444, 0.00000005050754956264051, -0.0476678239679167` | `0.9×0.9×0.4327326771259738` |
| `art.cliff.straight-b` | `0.3322727185105163, 0.5965491125303924, 0.3322727185105163` | `-0.010047114768375274, 0.00000005090342182921911, -0.05238271282516175` | `0.9×0.9×0.45282165758321463` |
| `art.cliff.top-cap` | `0.3274945334777423, 0.6001636951606902, 0.3274945334777423` | `0.0049633219867498655, 0.00000011846357050606037, -0.053226165581228134` | `0.858200781085434×0.8999999999999999×0.9000000000000001` |

全部 14 件校准后 X/Z extent 都不超过 `0.90`，最终 `abs(bounds.center.x)` 与 `abs(bounds.center.z)` 均须 `<=2e-7`，最低 Y 与 `0` 的误差须 `<=2e-7`；Cliff 高度为 `0.90`。主代理已目视顶视、倾斜正交和等比/非等比对照图，接受其作为首版接入比例；纠偏后的居中数值仍须由 Unity 机器证据闭合。Cliff 的 Y/XZ 比相对源模型提高约 `1.53–1.84×`，因此 Unity 首版仍必须保留默认倾斜正交镜头视觉门：若出现拓扑不可读、triplanar 明显拉伸或任何 X/Z extent 超过 `0.90`，立即停止，不能放宽玩法格、复制规则或用 Collider 绕过；需要时另行批准“可控视觉重叠”或“多格模块”。

### 7.4 Unity 导入真值纠偏门

Task 4 的首轮 RED 后，Unity 导入预检暴露了 Blender 离线校准没有表达的运行时边界。证据冻结在 `/private/tmp/wastecity-ruins-cliff/task4-import-transform-preflight.md`、`.json`、`task4-slot-preflight.md`、`.json` 及对应 raw logs；证据 HEAD 为 `756f4d3678d6de6df91819d795405e2b1ed12ac0`。当时 14 个 FBX 均为一个 imported root、一个 MeshFilter、一个 MeshRenderer，未发布 Material/Prefab/Profile，事务 marker 不存在；现有 Task 4 RED XML/log 必须保留，不能改写为通过。

纠偏权威如下：

1. 14 件共同 imported root 为 position `(0,0,0)`、上述精确 quaternion、scale `(1,1,1)`；raw Mesh bounds 仍是 Blender `XY` 地面/`Z` 高，轴转换不在 raw Mesh 内；
2. Catalog 同时冻结 `SourceImportMatrix`、每项 root scale/child offset 和按 Unity 实际 submesh 索引排序的 `MaterialRoles`；ChildOffset 必须由 raw Mesh 依次应用 `SourceImportMatrix`、`S(rootScale)` 后的 bounds 推导为 `(-center.x, -minY, -center.z)`，不得复用旧离线表的水平符号；
3. Layout 是正式运行时矩阵的唯一组合者，严格生成 `T(cell) * Ry * T(childOffset) * S(rootScale) * SourceImportMatrix`，不接受已含导入矩阵的输入；
4. Builder 只验证 imported root 矩阵、实际槽序和组合后 bounds，并把零格/零旋转的完整组合镜像进 mesh-only Prefab；
5. Geometry 从 Prefab 只读取内嵌可读的运行时 `MeshFilter.sharedMesh` 与 `MeshRenderer.sharedMaterials`，明确忽略 Prefab Transform，然后只应用已经完整的 placement `WorldMatrix`。原始 FBX 继续保持 Read/Write 关闭；若再乘 Prefab Transform 会重复消费校准和导入矩阵，必须由测试阻止。

纠偏前暂停 Task 4，不运行 Builder、不发布资产。Catalog/Layout 的纠偏必须先经历独立 RED、最小 GREEN并输出 `/private/tmp/wastecity-ruins-cliff/task3-5-corrected-calibration.json`、独立审查、提交和普通 push；JSON 必须逐件记录 raw bounds、SourceImportMatrix、root scale、推导出的 ChildOffset、最终 center/minY/size 和判定阈值。旧符号、缺乘或双乘 import matrix 的结果均必须失败。远端同步后才能从现有 Task 4 RED 恢复。本门不授权修改 FBX、`.fbx.meta`、ModelImporter、schema `30`、冻结 2D 或任何玩法真值。

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

`GrayboxWorldView3D` 增加按稳定 ID 控制 surface slot 的窄接口，同时保留现有“全部显示/隐藏”入口供正式地表使用。内部保存七个稳定 ID 的独立可见真值；现有 `SurfaceFallbackVisible` 明确定义为“七类 surface fallback 是否全部可见”，部分恢复时为 `false`。`SetSurfaceFallbackVisible(bool)` 原子重置七类状态，`TrySetSurfaceFallbackVisible` 只改目标 ID，`IsSurfaceFallbackVisible` 查询单类状态；Generate 前设置、Generate 后重建、Clear 和全量恢复都必须保留这一语义。新接口只接受已存在的七类 surface 稳定 ID；未知 ID 必须失败且不得改变任何状态或 Renderer。

`WasteCity.Graybox3D` 不得反向引用 `WasteCity.ArtIntegration3D`，否则形成 asmdef 环。实现复用 `GrayboxWorldView3D` 现有 surface allowlist，或在另行批准后把公共稳定 ID 下沉到 `WasteCity.Game`；本批不得为了调用 `FirstArtTerrainCatalog3D` 修改程序集依赖方向。

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
- Geometry 从 Prefab 只读取内嵌可读的运行时 `MeshFilter.sharedMesh` 与 `MeshRenderer.sharedMaterials`，不读取或相乘 Prefab Transform；原始 FBX importer 必须保持 Read/Write 关闭。运行时 Mesh 由 Builder 在 Editor 中通过 `Mesh.AcquireReadOnlyMeshData` 从批准的原始 FBX 确定性复制，并作为 Prefab 子资源保存；更新时原位改写同一子资源，保持 Prefab GUID 与 Mesh localFileID 稳定。Geometry 对该内嵌运行时 Mesh 使用 `Mesh.AcquireReadOnlyMeshData`，输出固定使用 `Mesh.AllocateWritableMeshData`，先调用 `SetVertexBufferParams` 与 `SetIndexBufferParams(indexCount, IndexFormat)`，写完后设置 `subMeshCount` 并通过 `SetSubMesh(SubMeshDescriptor)` 冻结每个材质范围，最后调用 `Mesh.ApplyAndDisposeWritableMeshData`。必须复制 Position、Normal、Tangent 和 UV0：Position 用已包含 `SourceImportMatrix` 的完整 placement 矩阵变换；Normal 必须用该矩阵线性 `3×3` 部分的 inverse-transpose 变换并归一化，不能直接用非等比矩阵；Tangent.xyz 先用线性 `3×3` 变换，再相对新 Normal 做 Gram-Schmidt 正交化并归一化，Tangent.w 保留源 handedness。反射/负行列式矩阵不在批准校准内，遇到即失败，不能静默翻转 handedness。随后按源 submesh 的已批准材质角色归并并重算 Bounds；所有 `MeshDataArray`、`NativeArray`、临时 Mesh/GameObject 在成功、异常和平台拒绝分支均用 `finally` 释放；
- `SystemInfo.supports32bitsIndexBuffer` 通过默认读取真实平台、测试可注入的只读 capability 提供；生产默认不得被测试覆盖，测试结束必须恢复注入。平台不支持 UInt32 时不得尝试写 buffer；
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
4. 合批：placement 数与规则格一致，长期对象和 Renderer 不随格数增长，材质引用均为共享资产；合成夹具分别覆盖 `65,535` 顶点边界两侧，证明 `UInt16/UInt32` 选择发生在索引和 submesh 写入前，且顶点、索引和每个材质 submesh 均未截断或错位；另用批准的非等比 Cliff scale 夹具断言 Normal 等于 inverse-transpose 结果且单位化、Tangent 与新 Normal 正交且单位化、Tangent.w handedness 不变，并证明结果不同于错误的直接法线线性变换；用带非 identity Prefab Transform 的夹具证明 Geometry 只应用 placement，一旦额外相乘 Prefab Transform 测试必须失败；
5. 分类回退：注入 Ruins 或 Cliff 单独失败，证明只恢复对应稳定 ID，正式地表和另一类保持；
6. 生命周期：重复 `TryPresent`、禁用/启用、世界重建、配置替换和销毁无残留、无重复对象；
7. 场景/authoring：唯一 owner、精确 Profile 引用、运行时子对象未序列化、重复 authoring 幂等；
8. PlayMode：真实 `GrayboxPrototype3D` 中两类几何与规则格逐项一致，城市移动/展开、A*、建造投影、资源节点和系统菜单不回归；
9. 性能与 Player 构建：结构、300 帧分配、Profiler、Shader/Material 保留和独立程序日志。

新增类型尚不存在时允许把 Unity 编译失败日志作为第一条 RED，但不得虚构 NUnit XML；希望保留结构化 XML 时，应先用反射或资产缺失断言建立可运行 RED。不得删除旧断言、放宽现有地表原子回退测试，或以直接调用内部可见性字段代替真实 presenter 生命周期测试。A*、建造、真实输入和系统菜单回归应复跑现有测试类，不在新测试中复制其判断规则。

## 13. 完整验收门

- 14 个既有 FBX 的 importer、GUID 和内容哈希保持；
- 新增 14 个 Prefab、13 个共享材质和一个 Profile 全部通过合同；
- focused EditMode 与正式场景 PlayMode 通过；
- 日常完整 EditMode（排除 `TerrainAssetDeep`）与完整 PlayMode 通过；
- Unity 无界面编译通过；
- 本子项最终 Release 3D 通过显式正式入口 `WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D`，Development 3D 通过 `BuildWindowsGraybox3DDevelopment`，legacy 2D 通过 `BuildWindowsLegacy2D`；三者必须构建各自独立产物并验证为 Windows GUI x86-64；
- 先以 TDD 为 `FormalBuildTools` 增加 `BuildMacOSGraybox3D`，固定只构建 `GrayboxPrototype3D` 到 `Builds/macOS/WasteCity.app`，临时选择 universal 架构并在成功、失败和下次恢复路径还原受保护设置；随后构建并启动 macOS Player 进行一次 3D 可见性冒烟，验证产物为 arm64+x86_64，防止 `BUG-0005` 类 Shader 剥离回归；
- 能执行时补真实 Windows 10/11 独立程序至少 12 秒冒烟；不能执行时明确待补；
- 64×48 默认 seed 与合成邻接夹具均满足结构和性能预算；
- 文档生成、质量目录、复用目录和只读验证通过；
- 固定输出默认镜头、顶视、Ruins 近景、Cliff 直段/内外角/端头、灰盒回退和混合成功/失败对照图；
- 用户明确确认边界、比例、遮挡和综合色温后，才可把本子项记为视觉通过。

## 14. 本批不运行 `TerrainAssetDeep`

本里程碑不得修改地形源 PNG、其 importer 规则、`FirstArtTerrainAssetBuilder`、四个 Texture2DArray、数组层顺序、数组序列化或地表 Shader。新增的是 FBX 对应 Prefab、共享模型材质、确定性布局、合批和分组回退，因此只运行日常 EditMode 和相关美术/场景/PlayMode 测试，不运行 `TerrainAssetDeep`。

如果实现中发现必须修改上述任一深度地形范围，立即停止、报告计划外边界并重新批准；获批后才改变测试调度。

进入实现前建立 SHA-256 保护清单，完成后逐字节比较：14 个 FBX 与 14 个 `.fbx.meta`、28 个地形 PNG `.meta`、四个 Texture2DArray、`WasteCityFirstPassTerrain.shader`、`FirstArtTerrainAssetBuilder.cs`、`MAT_Terrain_FirstPass.mat`、`FirstArtTerrainProfile3D.asset`、`GrayboxURP.asset`、`ProjectSettings/GraphicsSettings.asset`、`ProjectSettings/QualitySettings.asset`、`ProjectSettings/ProjectSettings.asset`、`ProjectSettings/EditorBuildSettings.asset`、`Packages/manifest.json` 和 `Packages/packages-lock.json`。任何非本计划批准的变化都必须停止；现有工作区噪声不能被新提交吸收。

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

## 17. 2026-08-15 当前执行证据

- Ruins/Cliff AssetBuilder focused EditMode 为 `37/37`；日常完整 EditMode 为 `1454/1454`，唯一排除类别为本批不运行的 `TerrainAssetDeep`；完整 PlayMode 为 `91/91`。以上均为零失败、零跳过。
- 最终 v8 依次通过 `BuildWindowsGraybox3D`、`BuildWindowsGraybox3DDevelopment`、`BuildWindowsLegacy2D` 和 `BuildMacOSGraybox3D`。三个 Windows Player 均确认为 `PE32+` GUI x86-64；macOS 精确 binary 确认为 universal `x86_64 arm64`。
- 每次构建带 `-batchmode -nographics -quit` 完整退出后，`21` 个 ProjectSettings 和 `14` 个运行时 Prefab 的哈希均精确稳定，普通/最终退出恢复标记和备份均为 `0`。
- macOS 精确 binary 已运行 `45` 秒 NullGfx 启动冒烟；`31` 条错误全部为无图形设备下预期的 unsupported Shader，脚本异常、空引用、未处理异常、Missing Script 与崩溃均为 `0`。该结果只覆盖启动和关闭生命周期，不满足真实画面、GPU 或显存门。
- 真实 Windows 10/11 Player 的视觉、GPU、显存和内存冒烟以及用户对 Ruins/Cliff 运行时画面的视觉复验仍待完成。因此本子项只能写“已实现待用户视觉复验”，整个 `IDEA-0004` 继续保持 `开发中`，不得写成“已验证”。
