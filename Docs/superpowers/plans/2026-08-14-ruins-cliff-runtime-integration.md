# Ruins / Cliff 模块运行时接入实施计划

> 日期：2026-08-14  
> 状态：待主代理审查后执行  
> 受控需求：`IDEA-0004`  
> 权威规格：`Docs/superpowers/specs/2026-08-14-ruins-cliff-runtime-integration-design.md`  
> 规格提交：`c27314c80a5c6cc1f30d175d44ccd0b527a2fd83`
> 前置验收基线：`9b8b533ea7b130a7a93c847c17085adc54f44cbd`  
> 工作分支：`codex/3d-usability-followup`

## Goal

把已验收的 Ruins 八件和 Cliff 六件 FBX 制成 14 个受控 Prefab，以 `WorldMapModel` 为唯一真值确定性布局、按类别合批，并由现有 `FirstArtTerrainRenderer3D` 事务化接入默认 3D 场景。失败时只恢复相应 Ruins 或 Cliff 灰盒，不回退已成功的连续地表或另一类别。

## Global Constraints

- 只在当前分支和工作树执行；每个任务开始、暂存前、提交后检查 `git status`。开始 Task 1 前必须由主代理确认所有既有脏路径的所有权；不得清理、覆盖、暂存或提交并发中的 `Docs/09`、Unity `.meta` 或临时场景噪声。
- 每个任务先写失败测试并保存 RED XML/log，再做最小 GREEN；每阶段结果交主代理审查后才进入下一任务。
- Unity 可执行文件固定为 `/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity`。
- `-runTests` 与 `-quit` 绝不同时使用；编译、Builder、authoring、构建命令可以使用 `-quit`。
- 不修改 14 个 FBX、14 个 `.fbx.meta`、地形 PNG/importer、`FirstArtTerrainAssetBuilder`、四个 Texture2DArray、现有地表 Shader、Packages、ProjectSettings、Build Settings、schema `30` 或冻结 2D 生产代码。
- 本批不运行 `TerrainAssetDeep`；日常 EditMode 必须显式使用 `-testCategory '!TerrainAssetDeep'`。
- 新模块 Shader 独立于 `WasteCityFirstPassTerrain.shader`；不得顺带修改现有地表 Shader。
- Windows 产物可在 macOS 构建和检查 PE 格式，但真实 Windows 10/11 启动冒烟必须记录为待补，不能用 macOS 检查冒充。
- 不创建 Release、不合并、不 force-push；每次只暂存任务列出的精确文件，禁止 `git add .`。
- 任一计划外生产文件、单格校准明显破坏已批准模型、需要 Collider/第二套规则、无法选择性回退或需要修改 FBX/meta 时立即停止。
- 开始 Unity 命令前建立并保存 SHA-256 保护清单：14 个 FBX、14 个 `.fbx.meta`、28 个地形 PNG `.meta`、四个 Texture2DArray、`WasteCityFirstPassTerrain.shader`、`FirstArtTerrainAssetBuilder.cs`、`MAT_Terrain_FirstPass.mat`、`FirstArtTerrainProfile3D.asset`、`Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset`、`ProjectSettings/GraphicsSettings.asset`、`ProjectSettings/QualitySettings.asset`、`ProjectSettings/ProjectSettings.asset`、`ProjectSettings/EditorBuildSettings.asset`、`Packages/manifest.json`、`Packages/packages-lock.json`。每次 Builder、authoring、测试和构建后重算；任何非批准变化立即停止。

统一环境：

```bash
export WASTECITY_PROJECT="/Users/baiyan1/Documents/WasteCity-3d-usability-followup"
export WASTECITY_UNITY="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
export WASTECITY_EVIDENCE="/private/tmp/wastecity-ruins-cliff"
mkdir -p "$WASTECITY_EVIDENCE"
```

新增 Unity 文件和文件夹均包含对应 `.meta`；下文“Create”默认包含该文件的 `.meta`，资产列表同理。

---

## Task 0: Formal Document, Workspace and Calibration Gate

**Files**

- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/superpowers/specs/2026-08-14-ruins-cliff-runtime-integration-design.md`
- Modify: `Docs/superpowers/plans/2026-08-14-ruins-cliff-runtime-integration.md`
- Evidence only outside repository: `$WASTECITY_EVIDENCE/calibration/`

**Gate A — formal documents and exact authority**

- `Docs/05` 把本子项从“经 `VisualLibrary` 接入”修正为“经 `FirstArtRuinsCliffCatalog3D` 冻结的稳定表现映射接入”，并说明不扩展冻结 2D 的一对一 `VisualLibrary`。
- `Docs/06` 的 `IDEA-0004` 双向引用本规格与本计划，状态保持 `开发中`；只记录书面进展，不写运行时、测试、构建或视觉已完成。
- 消除 SHA 自引用：先提交不含本计划的规格与正式文档为提交 A，再把 A 的真实 SHA 回填到本计划头并单独提交计划为提交 B。开始 Task 1 前必须证明 B 的 HEAD 与 `origin/codex/3d-usability-followup` 同步、A 是 B 的祖先且计划精确引用 A；不要求也不得声称 `HEAD=A`。

**Gate B — workspace and protected baseline**

```bash
git status --short --branch
git diff --name-only
git diff --cached --name-only
```

主代理确认所有既有脏路径归属；任务不拥有的 `Docs/09`、Unity `.meta`、Material/Profile 或临时场景不得被清理、覆盖或暂存。Unity 仍运行时出现的已知噪声由主代理在 Unity 结束后另行归属和处理，本计划不把“工作树已清”作为既成事实，也不要求为进入文档提交而擅自清除它。受保护路径必须生成路径排序稳定的 SHA-256 清单和 `git status --short -- <paths>` 快照。若不能得到主代理认可的安全基线，停止。

**Gate C — approved model calibration**

离线比例与视觉证据固定在 `$WASTECITY_EVIDENCE/calibration/README.md`、`calibration_matrix.json` 与 `renders/` 三张对照图；它们继续批准 root scale、目标 size 和视觉比例，但旧 JSON 不再拥有 ChildOffset 权威。Task 3.5 必须以 Unity raw Mesh 经 `SourceImportMatrix` 和 root scale 后的 bounds 推导纠偏 ChildOffset，并输出 `$WASTECITY_EVIDENCE/task3-5-corrected-calibration.json`。最终精确矩阵、Ruins 基础朝向和 Cliff mask/连接/旋转表以权威规格第 7.1–7.4 节为唯一实施输入。全过程只读 14 个 FBX/importer 和批准记录，不修改 FBX、`.meta`、Prefab 或正式场景。

校准目标与进入实现门槛为：root 位于 `1.0` 逻辑格中心，子 Mesh 最低 Y 贴 `0`；全部 X/Z extent `<=0.90`，Ruins 仅等比缩小，Cliff XZ 等比缩入单格且 Y 独立校准为 `0.90`。Ruins 无连接语义，以 FBX `0°` 为基础再叠加布局 `quarterTurns`；Cliff 冻结 `N=1,E=2,S=4,W=8`、标准 mask、四向旋转表，并以两臂间对角 Cliff 格的缺失/存在分别选择 Inner/Outer Corner。主代理已目视并接受首版比例与轮廓；最终居中/贴地仍必须由 Task 3.5 纠偏 JSON 闭合，不能把旧 offset 当成已通过。

Unity 接入仍保留视觉停止门：若默认倾斜正交镜头下出现拓扑不可读、triplanar 明显拉伸或任何 X/Z extent 超过 `0.90`，立即停止并提交“可控视觉重叠”或“多格模块”设计供用户另行批准；不能放宽玩法格、复制规则或用 Collider 绕过。

**RED / completion evidence**

Task 0 的 RED 是计划 B 尚未回填规格提交 A 的真实 SHA、失真的规格引用或未归属/不安全的工作树变化，不以代码测试代替。只有四份书面文档精确 diff 通过、计划 B 精确引用 A、B 的 HEAD 与 tracking ref 同步、保护清单已保存、规格第 7.1–7.3 节与校准证据一致且主代理明确放行，Task 0 才完成。

**Commit:** `docs: approve ruins cliff runtime integration plan`

---

## Task 1: Catalog / Profile Contract

**Files**

- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffCatalog3D.cs`
- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffProfile3D.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffCatalogProfileTests.cs`

**Interfaces**

- `FirstArtRuinsCliffCatalog3D` 固定 14 个表现稳定 ID、FBX 路径、Prefab 路径、类别、模块语义、root scale/child offset、14 件共同的 `SourceImportMatrix` 及每项按 Unity raw Mesh submesh 索引排列的 `MaterialRoles`；顺序不得来自目录或 Inspector。共同矩阵冻结为 quaternion `(-0.7071068,0,0,0.7071067)` 对应的规格第 4 节 row-major 矩阵；ChildOffset 冻结为 raw Mesh 先乘 import matrix、再乘 root scale 后 bounds 的 `(-center.x, -minY, -center.z)`，不能沿用旧离线表的 X/Z 符号。
- `FirstArtRuinsCliffProfile3D` 序列化 14 个 Prefab、13 个 Material 和模块 Shader，提供 `TryValidate(out string error)`；重复、缺失、未知槽、错误名称或跨类别引用必须失败。
- Profile 不保存地图、seed、规则格、Collider 或存档数据。

**RED**

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtRuinsCliffCatalogProfileTests \
  -testResults "$WASTECITY_EVIDENCE/task1-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task1-red.log"
```

优先用反射检查新类型/固定映射不存在，使 RED 仍能产出 NUnit XML；如果测试必须直接引用尚不存在的类型，则把 Unity 编译失败日志保存为 RED，并明确记录“无有效 XML”，不得伪造测试计数。随后实现最小常量表和纯验证。测试除固定表外，还必须用 `AssetDatabase` 只读加载 14 个 FBX，断言 imported root 矩阵、raw Mesh/submesh 数和 `renderer.sharedMaterials` 顺序与 Catalog 一致；其中七个 Ruins 槽序按规格第 4 节 Unity 预检真值纠正。GREEN 使用相同过滤器，输出 `task1-green.xml/log`。确认没有 FBX/meta/importer 差异。

**Commit:** `feat: define ruins cliff art catalog`

主代理审查：稳定 ID、数组顺序、Profile 边界及精确 diff。

---

## Task 2: Selective Graybox Fallback

**Files**

- Modify: `Assets/_Game/Scripts/Graybox3D/GrayboxWorldView3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxVisualAndWorldTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs`

**Interfaces**

- 保留 `SetSurfaceFallbackVisible(bool)` 的全量语义。
- 新增 `bool TrySetSurfaceFallbackVisible(string stableId, bool visible, out string error)` 和 `bool IsSurfaceFallbackVisible(string stableId)`。
- 内部维护七类逐 ID 真值；`SurfaceFallbackVisible` 冻结为“七类是否全部可见”，部分可见时为 `false`。全量入口同时重置七态，逐 ID 入口只改目标，Generate 前设置、Generate 后重建、Clear 和全量恢复语义一致。
- 只接受七类已登记 surface ID；未知 ID 不改变状态或任何 Renderer 并明确失败。
- `WasteCity.Graybox3D` 不得引用 `WasteCity.ArtIntegration3D`，避免 asmdef 循环；复用 `GrayboxWorldView3D` 现有 `IsSurfaceSlot` allowlist。若要把稳定 ID 下沉到 `WasteCity.Game`，视为计划外生产改动并先停止审批。

**RED / GREEN**

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxVisualAndWorldTests \
  -testResults "$WASTECITY_EVIDENCE/task2-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task2-red.log"
```

RED 覆盖只恢复 Ruins、只恢复 Cliff、部分状态下的聚合属性、未知 ID 原子失败、Generate 前设置/重建、Clear 和全量入口重置部分状态；最小实现后重跑上述类和 `FirstArtTerrainRendererTests`，保存 `task2-green.xml/log`。

**Commit:** `feat: select graybox surface fallback by id`

主代理审查：资源节点与其它五类 Renderer 未被触碰，旧 API 兼容。

---

## Task 3: Deterministic Layout

**Files**

- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffLayout3D.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffLayoutTests.cs`

**Interfaces**

- `Project(WorldMapModel, PlanarCoordinateMapper3D)` 按 `y` 后 `x` 返回只读 placement 序列；placement 含类别、Catalog 索引、格坐标、世界矩阵和四邻 mask。
- 固定 `unchecked uint` 回绕哈希及盐；或使用无溢出的宽整数并确定性截断。不得依赖编译器默认 overflow，也不得使用 `UnityEngine.Random`、时间、相机、文件顺序或 PlayerPrefs。
- Ruins 每格恰好一个、八变体均可达；Cliff 严格执行 0/1/相对2/相邻2/3/4 邻居模块表和四向旋转。
- placement 保存整数格、Catalog 索引、邻接 mask 和 `quarterTurns`，并严格组合 `T(cell) * Ry(quarterTurns) * T(childOffset) * S(rootScale) * SourceImportMatrix`；共同 import matrix 恰好消费一次，任何校准/导入矩阵占位未闭合时本任务立即停止。

**RED / GREEN**

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtRuinsCliffLayoutTests \
  -testResults "$WASTECITY_EVIDENCE/task3-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task3-red.log"
```

夹具冻结六种 Cliff 拓扑、Ruins 八变体、扫描顺序、旋转、同图字节一致、不同规则图变化，以及默认 seed `8128` 的 Ruins/Cliff 数量；矩阵断言必须包含共同 `SourceImportMatrix`，并用 raw Mesh 顶点/Bounds 证明缺乘和双乘都失败。GREEN 输出 `task3-green.xml/log`，并证明投影前后所有 `WorldCell` 逐字段一致。

**Commit:** `feat: project deterministic ruins cliff layout`

主代理审查：无第二套地图、无玩法写入、单格停止门可观测。

---

## Task 3.5: Corrective Unity Import Truth Gate

**触发与停止状态**

Task 4 首轮 RED 已真实执行并保存在 `$WASTECITY_EVIDENCE/task4-red.xml` 与 `task4-red.log`：5 个资产合同测试按预期全部失败。后续只读预检在 HEAD `756f4d3678d6de6df91819d795405e2b1ed12ac0` 暴露 imported root 轴转换未进入 Catalog/Layout，以及七个 Ruins 的 Catalog 槽序与 Unity submesh 顺序不一致。证据为：

- `$WASTECITY_EVIDENCE/task4-import-transform-preflight.md`
- `$WASTECITY_EVIDENCE/task4-import-transform-preflight.json`
- `$WASTECITY_EVIDENCE/task4-slot-preflight.md`
- `$WASTECITY_EVIDENCE/task4-slot-preflight.json`
- 对应 `task4-import-transform-preflight.log`、`task4-slot-preflight.log`

当次运行未发布 Material/Prefab/Profile，事务 marker 不存在。立即暂停 Task 4；已存在的 Task 4 RED、未跟踪 Builder/Shader/测试草稿和外部证据均不得删除、改写成 GREEN 或提前纳入纠偏提交。

**Files**

- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffCatalog3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffCatalogProfileTests.cs`
- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffLayout3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffLayoutTests.cs`
- Evidence only outside repository: 上述 preflight、新的 `task3-5-*.xml/log` 与 `$WASTECITY_EVIDENCE/task3-5-corrected-calibration.json`

**Corrective contract**

- Catalog 新增并冻结共同 `SourceImportMatrix`：quaternion `(-0.7071068,0,0,0.7071067)`，精确 row-major 数值引用权威规格第 4 节；禁止硬编码猜测 `-90°` Euler。
- 按权威规格第 4 节把七个 Ruins 的 `MaterialRoles` 改为 Unity `renderer.sharedMaterials` / raw Mesh submesh 实际顺序；`broken-pipe` 与 Cliff 六件保持已正确顺序。
- 对每件 raw Mesh 先应用 `SourceImportMatrix`、再应用 `S(rootScale)` 计算 bounds；Catalog ChildOffset 严格冻结为 `(-center.x, -minY, -center.z)`。这使规格 14 行的 X/Z 等于旧离线表数值取反，Y 保留使 min Y 为零的正微偏移并由 raw bounds 复算；旧 `calibration_matrix.json` 不再是 offset 权威。
- Layout 的唯一正式组合为 `T(cell) * Ry(quarterTurns) * T(childOffset) * S(rootScale) * SourceImportMatrix`，import matrix 只消费一次。
- Catalog EditMode 测试只读 14 个 Unity FBX imported roots，冻结共同位置/四元数/scale、raw Mesh/submesh 与实际 renderer 槽序；Layout 测试冻结完整矩阵和校准后 bounds，逐件断言 `abs(finalBounds.center.x/z)<=2e-7`、`abs(finalBounds.min.y)<=2e-7` 且 size 与规格目标逐轴误差 `<=2e-7`，并让遗漏 import matrix、双乘 import matrix、使用旧 ChildOffset 水平符号三类实现都失败。
- GREEN 测试原子写入 `$WASTECITY_EVIDENCE/task3-5-corrected-calibration.json`；JSON 顶层记录 Unity/HEAD/容差/共同 import quaternion 和 matrix，每个 entry 记录 stable ID、raw center/size、root scale、derived ChildOffset、final center/minY/size、expected size 与逐项 pass。缺少任一 entry、非有限数、阈值失败或写出半份文件均视为 GREEN 失败。
- 不修改 FBX、`.fbx.meta`、ModelImporter、schema `30`、冻结 2D、Prefab/Material/Profile 或任何玩法/存档真值。

**RED / GREEN / review**

先只改测试，保存能归因于旧 Catalog/Layout 的 RED；RED 阶段把输出指向独立临时路径，不能覆盖最终纠偏证据：

```bash
export WASTECITY_RUINS_CLIFF_CORRECTED_CALIBRATION="$WASTECITY_EVIDENCE/task3-5-corrected-calibration-red.json"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter 'WasteCity.Tests.FirstArtRuinsCliffCatalogProfileTests|WasteCity.Tests.FirstArtRuinsCliffLayoutTests' \
  -testResults "$WASTECITY_EVIDENCE/task3-5-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task3-5-red.log"
```

再最小修改 Catalog/Layout，把 `WASTECITY_RUINS_CLIFF_CORRECTED_CALIBRATION` 改为 `$WASTECITY_EVIDENCE/task3-5-corrected-calibration.json`，使用同一过滤器输出 `task3-5-green.xml/log`；验证最终 JSON 的 14 个 entry 和全部 pass 后，复跑各自完整 Task 1/Task 3 focused tests，比较完整受保护 SHA 清单。由独立代理复审 offset 推导和符号、矩阵乘法方向、七个槽序、旧符号/缺乘/双乘 fixture、机器证据、代码/测试 diff 和 FBX/meta/importer 零变化。

**Commit:** `fix: align ruins cliff catalog with unity import truth`

只暂存上述四个文件，普通 push 后证明本提交 HEAD 与 `origin/codex/3d-usability-followup` 同步；主代理确认独立审查和保护清单通过后，才能回到现有 Task 4 RED 继续实现。

---

## Task 4: Asset Builder, Shader, Materials and Prefabs

**Files**

- Create: `Assets/_Game/Editor/FirstArtRuinsCliffAssetBuilder.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffAssetBuilderTests.cs`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassGeometry.shader`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset`
- Create directory/assets: `Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/Geometry/`
- Create Ruins materials: `MAT_Ruins_Concrete.mat`, `MAT_Ruins_Aggregate.mat`, `MAT_Ruins_DustFilm.mat`, `MAT_Ruins_Dust.mat`, `MAT_Ruins_DarkFloor.mat`, `MAT_Ruins_DrainDark.mat`, `MAT_Ruins_Rust.mat`, `MAT_Ruins_Marking.mat`
- Create Cliff materials: `MAT_Cliff_Strata.mat`, `MAT_Cliff_Fracture.mat`, `MAT_Cliff_Dust.mat`, `MAT_Cliff_Rubble.mat`, `MAT_Cliff_Mineral.mat`
- Create directory/assets: `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Runtime/Prefabs/`
- Create Ruins Prefabs: `PF_Ruins_CrackedFloorSlab.prefab`, `PF_Ruins_RubblePile_A.prefab`, `PF_Ruins_RubblePile_B.prefab`, `PF_Ruins_RebarConcreteBlock.prefab`, `PF_Ruins_BrokenPipe.prefab`, `PF_Ruins_DrainageChannel.prefab`, `PF_Ruins_BoundaryEdge.prefab`, `PF_Ruins_WornMarkingPlate.prefab`
- Create directory/assets: `Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Runtime/Prefabs/`
- Create Cliff Prefabs: `PF_Cliff_Straight_A.prefab`, `PF_Cliff_Straight_B.prefab`, `PF_Cliff_InnerCorner.prefab`, `PF_Cliff_OuterCorner.prefab`, `PF_Cliff_EndCap.prefab`, `PF_Cliff_TopCap.prefab`

**Builder Contract**

- 先预检全部 FBX/槽名/Shader、输出路径冲突和 Task 0/3.5 校准真值，再在仓库内固定 staging 目录准备但不引用正式场景；验证全部 staging 资产成功后才按 Catalog 固定顺序发布。每个 FBX 必须验证唯一 imported root 的 position `(0,0,0)`、共同精确 quaternion/`SourceImportMatrix`、scale `(1,1,1)`、MeshFilter/Renderer 位于该 root、raw Mesh submesh 数与 `renderer.sharedMaterials` 顺序逐项匹配 Catalog；再从 raw bounds 独立复算 ChildOffset，并用完整组合逐项验证 `abs(finalBounds.center.x/z)<=2e-7`、`abs(finalBounds.min.y)<=2e-7`、size 与纠偏证据误差 `<=2e-7`。任一不符在 mutation 前失败。
- Builder 实现显式跨资产事务：首次创建通过 staging asset 连同 `.meta` 的 `AssetDatabase.MoveAsset` 发布；更新已有资产时先在 `Library/WasteCity.RuinsCliffAssetRestore/` 保存逐字节内容、GUID/路径清单和恢复 marker，再用保持既有 `.meta`/GUID 的序列化更新。任一 publish/save/reimport/最终验证失败时按逆序 rollback；编辑器下次初始化先恢复遗留 marker。成功后删除 marker/备份。连续重跑不得改变 Prefab/Material/Profile GUID 或字节。
- Prefab 仅 Transform/MeshFilter/MeshRenderer，零 Collider/Rigidbody/脚本；Builder 从对应原始 FBX 确定性复制 Mesh，并在每个 Prefab 内嵌唯一可读的 `<StableId>_RuntimeMesh` 子资源。原始 FBX importer 必须保持 Read/Write 关闭；重建时原位更新子资源，Prefab GUID 与 Mesh localFileID 必须稳定。全部槽按 Catalog 的 Unity 实际 submesh 顺序映射到 13 个共享 Material。Prefab 在 cell 原点、零 `quarterTurns` 时必须镜像完整 `T(childOffset) * S(rootScale) * SourceImportMatrix`，以最终 `localToWorldMatrix` 比较为准，不能只复制 imported root rotation 或只写 cell-fit scale。
- 新 Shader 名冻结为 `WasteCity/Terrain/FirstPassGeometry`，属性冻结为 `_BaseColorArray`、`_NormalArray`、`_MaskArray`、`_HeightArray`、`_LayerIndex`、`_TriplanarScale` 与角色 tint/PBR 参数。Ruins 材质 `_LayerIndex=4`，Cliff 材质 `_LayerIndex=6`，不得由实例覆盖。
- Shader 使用 URP 正式 PBR 路径，至少包含 `UniversalForward` 和 `ShadowCaster`，当前 URP 深度路径需要时增加 `DepthOnly`。三平面采样必须按法线权重混合，并对 Tangent Space Normal 的 X/Y/Z 投影做轴重定向与符号修正；不得使用逐格 UV、相机空间或地表控制图。
- 13 个共享 Material 只配置角色色调与 PBR 参数，共享新 Shader 和既有数组引用；不得复制纹理、生成每-Prefab贴图或引用 FBX 内嵌材质。
- `WasteCityFirstPassTerrain.shader`、四个 Texture2DArray、数组层顺序和 `FirstArtTerrainAssetBuilder` 只读且不得进入本任务 diff；Builder 只能消费其稳定引用。
- Builder 重跑两次后 Prefab/Material/Profile GUID 和字节稳定；FBX/meta SHA-256 前后完全一致。

**RED / GREEN**

先创建资产合同测试并运行，预期因 Builder/资产缺失失败：

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtRuinsCliffAssetBuilderTests \
  -testResults "$WASTECITY_EVIDENCE/task4-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task4-red.log"
```

实现 Builder 后执行：

```bash
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FirstArtRuinsCliffAssetBuilder.BuildRuntimeAssets \
  -logFile "$WASTECITY_EVIDENCE/task4-build-assets.log"
```

连续执行两次，比较 GUID/hash，再重跑 focused test 为 GREEN。测试冻结 Shader 名/属性/Pass、Ruins/Cliff 层号、13 个材质仅保存角色参数/既有数组引用，以及全部 Prefab 不引用 FBX 内嵌材质；逐项断言 imported root matrix、实际 slot/submesh 顺序、raw bounds、完整 Prefab 合成矩阵、最终 center/minY/size，防止旧 offset 符号、只乘 cell-fit 或只乘 import matrix。另在固定相机和光照下把水平面与两个正交垂直面渲染到 RenderTexture，断言垂直面存在非退化二维变化且不是 XZ 投影的常量拉伸。Shader 源字符串断言只作补充，不能代替编译和像素证据。注入每个 publish 阶段失败，证明 rollback 和下次启动 recovery 保持既有 GUID/字节且无半套资产。不得运行 `TerrainAssetDeep`。

**Commit:** `art: build ruins cliff runtime assets`

主代理审查：14 Prefab、13 Material、Shader/Profile、FBX/meta 零变化和单格视觉校准。

---

## Task 5: Combined Geometry and Index Boundary

**Files**

- Create: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtRuinsCliffGeometry3D.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffGeometryTests.cs`

**Interfaces**

- 类别级 `TryBuild(profile, placements, parent, out CategoryGeometry, out error)`；只在成功后返回拥有的 Mesh/GameObject。
- 从 Prefab 只取得内嵌可读的运行时 `MeshFilter.sharedMesh` 与 `MeshRenderer.sharedMaterials`，明确忽略 Prefab Transform；使用 `Mesh.AcquireReadOnlyMeshData` 读取该运行时 Mesh。输出使用 `Mesh.AllocateWritableMeshData`，不修改原始 FBX importer/readability。
- 每类先以 `checked long` 累计实际顶点/索引并验证能安全转换到 Unity 接受的 `int`。在 writable `MeshData` 上先调用 `SetVertexBufferParams` 和 `SetIndexBufferParams(indexCount, IndexFormat)`：`<= 65,535` 个顶点显式使用 `UInt16`，`> 65,535` 显式使用 `UInt32`；随后才写 buffer、设置 `subMeshCount` 并对每个角色调用 `SetSubMesh(SubMeshDescriptor)`，最后调用 `Mesh.ApplyAndDisposeWritableMeshData`。
- 明确复制和变换 Position、Normal、Tangent、UV0；Position 只使用已含共同 `SourceImportMatrix` 的完整 placement 矩阵，绝不能再乘 Prefab Transform。Normal 使用线性 `3×3` 的 inverse-transpose 后归一化，Tangent.xyz 使用线性 `3×3` 后相对新 Normal 做 Gram-Schmidt 正交化并归一化，Tangent.w 保留源 handedness。批准矩阵均为正行列式；遇到反射/负行列式矩阵原子失败。按 raw submesh 对应的 Catalog 材质角色归并，保持三角形 winding 和每角色范围，最终重算 Bounds。源缺少必需通道、属性格式不支持或材质槽不匹配时原子失败。
- 32 位索引支持通过默认读取 `SystemInfo.supports32bitsIndexBuffer`、测试可注入的只读 capability 提供；测试注入不得改变生产默认且必须在 teardown 恢复。平台不支持时在分配/写入 UInt32 buffer 前失败。
- 按 13 个固定材质角色归并 submesh；不能为规避 UInt32 错拆材质 submesh。
- 长期最多 `RuntimeGeometry/RuinsGeometry/CliffGeometry`、两个 MeshFilter、两个 MeshRenderer、两个 owned Mesh；无逐格实例和材质实例。
- 所有 `MeshDataArray`、`NativeArray`、临时 Mesh/GameObject 和失败后已创建的 Renderer 必须在嵌套 `try/finally` 中释放；成功路径只转移两个 owned Mesh 和两个长期子对象的所有权。

**RED / GREEN**

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtRuinsCliffGeometryTests \
  -testResults "$WASTECITY_EVIDENCE/task5-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task5-red.log"
```

合成夹具必须覆盖 `65,535` 顶点及其上一点，证明格式在 buffer/submesh 写入前选择，顶点、法线、切线、UV、索引、Bounds 和各材质 submesh 均未截断、回绕或错位；另用至少一件批准的非等比 Cliff scale 构造非轴对齐 Normal/Tangent，断言新 Normal 与 inverse-transpose 参考值一致且长度为 1、新 Tangent 长度为 1 且与 Normal 点积近零、Tangent.w 与源值一致，并断言错误的直接法线线性变换不能通过。加入带完整非 identity Prefab Transform 的 fixture：运行结果必须等于 raw Mesh 仅乘 placement 的参考值，并明确不等于额外再乘 Prefab Transform 的双乘结果。另测负行列式拒绝、`checked long` 到 `int` 边界、注入平台不支持、未知槽、缺失通道和每个中途异常点的原生/Unity 对象清理。GREEN 输出 `task5-green.xml/log`。

**Commit:** `feat: batch ruins cliff runtime geometry`

主代理审查：两 Mesh/十三材质预算、所有失败分支释放原生资源。

---

## Task 6: Presenter and Per-Category Transactions

**Files**

- Modify: `Assets/_Game/Scripts/ArtIntegration3D/FirstArtTerrainRenderer3D.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainRendererTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffPresentationTests.cs`

**Interfaces**

- Presenter 新增 `FirstArtRuinsCliffProfile3D` 引用，仍是唯一 `IGrayboxTerrainPresentation3D`。保留现有 `Configure(FirstArtTerrainProfile3D)` 重载及原语义；新增 `Configure(FirstArtTerrainProfile3D, FirstArtRuinsCliffProfile3D)`，旧重载等价于 geometry profile 为 `null` 并保持旧地表测试/调用可编译。
- 连续地表先按旧事务成功；Ruins、Cliff 分别在局部变量完成 profile/layout/geometry 验证，成功交换后才隐藏对应灰盒。
- 类别失败时 `TryPresent` 仍返回 `true`，表示连续地表已经呈现，不能让 Bootstrap 把正式地表整体清除；`LastPresentationError` 只表示会令整个 presenter 返回 `false` 的地表级失败。另保存独立 `RuinsStatus/RuinsError`、`CliffStatus/CliffError`，family 状态至少区分 `NotConfigured/Presented/Fallback`，每次尝试完整重置且每类失败只记录一次日志。
- `ClearPresentation`、重配、禁用、销毁和世界重建先恢复两个类别 fallback，再销毁 owned Mesh；旧全量地表失败优先。

**RED / GREEN**

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtRuinsCliffPresentationTests \
  -testResults "$WASTECITY_EVIDENCE/task6-red.xml" \
  -logFile "$WASTECITY_EVIDENCE/task6-red.log"
```

分别注入 Ruins 失败、Cliff 失败、双失败、地表失败，以及重复 TryPresent/禁用启用/重建/替换/销毁；断言 bool 返回值、全局错误和两个 family 状态彼此不混用。GREEN 后补跑 `FirstArtTerrainRendererTests` 与 `GrayboxSceneBootstrapTests`，旧地表断言和旧 Configure 调用必须原样通过。

**Commit:** `feat: present traversal geometry transactionally`

主代理审查：四种可见状态矩阵、一次性日志、无竞争 presenter。

---

## Task 7: Authoring and Scene Contract

**Files**

- Modify: `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`
- Modify: `Assets/_Game/Scenes/GrayboxPrototype3D.unity`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainSceneContractTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffSceneContractTests.cs`

**Contract**

- mutation 前精确验证 14 FBX、14 Prefab、13 Material、Shader、Profile 的路径/GUID/类型。
- 只给现有 `FirstArtTerrainRenderer3D` 增量设置 geometry Profile；场景仍只有一个 owner/presenter。
- `RuntimeSurface`、`RuntimeGeometry`、`RuinsGeometry`、`CliffGeometry` 不得序列化。
- 破损或重复 owner 在 mutation 前失败；连续 authoring 两次后场景 hash、关键 GlobalObjectId 和所有批准 GUID 稳定。

**RED / GREEN**

先新增合同测试并保存 RED；实现 authoring 后运行：

```bash
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile "$WASTECITY_EVIDENCE/task7-authoring-1.log"
shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.GrayboxSceneAuthoring.Configure \
  -logFile "$WASTECITY_EVIDENCE/task7-authoring-2.log"
shasum -a 256 Assets/_Game/Scenes/GrayboxPrototype3D.unity
```

然后 focused EditMode GREEN，且补跑旧 `FirstArtTerrainSceneContractTests`。

**Commit:** `feat: wire ruins cliff geometry into graybox scene`

主代理审查：场景精确 diff、两次 hash、ProjectSettings/FBX/meta 零变化。

---

## Task 8: Runtime, Performance, Builds and Evidence

**Files**

- Modify: `Assets/_Game/Tests/PlayMode/FirstArtTerrainRuntimeSceneTests.cs`
- Modify: `Assets/_Game/Tests/EditMode/FirstArtTerrainPerformanceTests.cs`
- Modify: `Assets/_Game/Editor/GrayboxPerformanceProbe.cs`
- Create: `Assets/_Game/Editor/FirstArtRuinsCliffEvidenceCapture.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtRuinsCliffEvidenceCaptureTests.cs`
- Modify: `Assets/_Game/Editor/FormalBuildTools.cs`
- Modify: `Assets/_Game/Tests/EditMode/GrayboxBuildAndPerformanceTests.cs`

**RED / GREEN and gates**

- PlayMode 用正式场景逐格核对 placement、两个 Renderer、真实禁用/恢复、世界重建。A* 复用 `FirstArtTerrainRuntimeSceneTests` 的真实右键路径测试；建造与菜单真实输入分别复跑现有 `GrayboxBuildingRuntimeSceneTests`、`GrayboxUsabilityRuntimeSceneTests`，不在新测试中复制导航、放置或菜单规则；完整 PlayMode 再统一兜底。
- 性能测试冻结结构；真实性能探针的 public、static、无参数 `WasteCity.Editor.GrayboxPerformanceProbe.MeasureRuinsCliffPerformance` 只从 `WASTECITY_RUINS_CLIFF_PERF_RESULT` 取得仓库外输出路径，记录布局+合批五次原始样本/中位数 `<=100 ms`、总初始化五次原始样本/中位数 `<=250 ms`、预热后 `300` 次稳定观察的当前线程托管分配 `0 B`，以及 Renderer/长期对象/顶点/三角形/`materialSlotCount` 统计，失败时抛异常且不得留下半份 JSON。NUnit 只冻结入口、结构和阈值，不以 NUnit stopwatch 冒充 GUI Profiler；真实连续 `300` 帧与 SetPass 由后文独立 GUI Profiler 门记录。
- 证据捕获的 public、static、无参数自动入口冻结为 `WasteCity.Editor.FirstArtRuinsCliffEvidenceCapture.StartAutomatedCapture`，只从 `WASTECITY_RUINS_CLIFF_EVIDENCE_DIR` 取得仓库外目录；输出 `manifest.json`、默认镜头、顶视、Ruins 近景、Cliff 六拓扑夹具、双成功及 Ruins/Cliff 单失败回退 PNG。manifest 必须记录场景、seed、相机矩阵、Profile/Material/Prefab GUID、每张图 SHA-256 与捕获结果；任何必需图缺失、全黑、粉色或 manifest 不完整时进程失败。
- 先在 `GrayboxBuildAndPerformanceTests` 写 RED：要求存在 `FormalBuildTools.BuildMacOSGraybox3D`，只构建 `Assets/_Game/Scenes/GrayboxPrototype3D.unity` 到 `Builds/macOS/WasteCity.app`，目标为 `BuildTarget.StandaloneOSX`，临时选择 universal 架构并在 build `finally`、构建后回调、Editor 退出和下次初始化恢复原设置；复用 `GrayboxRenderPipelineBuildScope`，不得创建第二套 URP 保护真值。实现后 focused GREEN。

先保存新增 PlayMode/性能断言的 RED，再最小完善探针/证据入口并运行 focused GREEN：

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter WasteCity.Tests.FirstArtTerrainRuntimeSceneTests \
  -testResults "$WASTECITY_EVIDENCE/task8-playmode.xml" \
  -logFile "$WASTECITY_EVIDENCE/task8-playmode.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtTerrainPerformanceTests \
  -testResults "$WASTECITY_EVIDENCE/task8-performance.xml" \
  -logFile "$WASTECITY_EVIDENCE/task8-performance.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.GrayboxBuildAndPerformanceTests \
  -testResults "$WASTECITY_EVIDENCE/task8-build-contract.xml" \
  -logFile "$WASTECITY_EVIDENCE/task8-build-contract.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter WasteCity.Tests.GrayboxBuildingRuntimeSceneTests \
  -testResults "$WASTECITY_EVIDENCE/task8-building-input.xml" \
  -logFile "$WASTECITY_EVIDENCE/task8-building-input.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testFilter WasteCity.Tests.GrayboxUsabilityRuntimeSceneTests \
  -testResults "$WASTECITY_EVIDENCE/task8-menu-input.xml" \
  -logFile "$WASTECITY_EVIDENCE/task8-menu-input.log"
```

运行冻结的真实探针与自动证据入口；两个环境变量都必须是绝对路径，命令不得加 `-nographics`。性能入口完成后退出；证据入口通过 Editor update 驱动 PlayMode 捕获并自行退出，因此不得添加 `-quit`：

```bash
export WASTECITY_RUINS_CLIFF_PERF_RESULT="$WASTECITY_EVIDENCE/task8-performance-probe.json"
export WASTECITY_RUINS_CLIFF_EVIDENCE_DIR="$WASTECITY_EVIDENCE/task8-captures"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.GrayboxPerformanceProbe.MeasureRuinsCliffPerformance \
  -logFile "$WASTECITY_EVIDENCE/task8-performance-probe.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FirstArtRuinsCliffEvidenceCapture.StartAutomatedCapture \
  -logFile "$WASTECITY_EVIDENCE/task8-evidence-capture.log"
test -s "$WASTECITY_EVIDENCE/task8-performance-probe.json"
test -s "$WASTECITY_EVIDENCE/task8-captures/manifest.json"
```

GUI Profiler 是独立人工技术门，不由上述 JSON 或 NUnit 代替。在主代理锁定的正确 Unity 工程实例中打开 `GrayboxPrototype3D`，Game View 固定 `1920×1080`，进入 Play；打开 `Window > Analysis > Profiler`，Target 选择当前 Editor PlayMode，关闭 Deep Profile，启用 CPU Usage、Rendering、Memory，预热后清空历史并连续录制至少 300 帧。保存原始会话为 `$WASTECITY_EVIDENCE/task8-profiler-300frames.data`，并把 CPU Timeline（包含布局/合批标记）、Rendering、Memory 三个模块截图分别保存为 `task8-profiler-cpu.png`、`task8-profiler-rendering.png`、`task8-profiler-memory.png`；在 `$WASTECITY_EVIDENCE/task8-profiler-notes.md` 记录 Unity 版本、目标、分辨率、帧范围、平均/最差 CPU/GPU 帧时、FPS、Renderer、SetPass、三角形和内存。任一原始 `.data`、三图或 notes 缺失时性能门未完成。

完整回归与编译：

```bash
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform EditMode -testCategory '!TerrainAssetDeep' \
  -testResults "$WASTECITY_EVIDENCE/editmode-final.xml" \
  -logFile "$WASTECITY_EVIDENCE/editmode-final.log"
"$WASTECITY_UNITY" -batchmode -projectPath "$WASTECITY_PROJECT" \
  -runTests -testPlatform PlayMode \
  -testResults "$WASTECITY_EVIDENCE/playmode-final.xml" \
  -logFile "$WASTECITY_EVIDENCE/playmode-final.log"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -logFile "$WASTECITY_EVIDENCE/compile-final.log"
```

构建：

```bash
"$WASTECITY_UNITY" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3D \
  -logFile "$WASTECITY_EVIDENCE/build-release-3d.log"
"$WASTECITY_UNITY" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment \
  -logFile "$WASTECITY_EVIDENCE/build-development-3d.log"
"$WASTECITY_UNITY" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D \
  -logFile "$WASTECITY_EVIDENCE/build-legacy-2d.log"
"$WASTECITY_UNITY" -batchmode -nographics -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D \
  -logFile "$WASTECITY_EVIDENCE/build-macos-3d.log"
```

用 `lipo -info Builds/macOS/WasteCity.app/Contents/MacOS/*` 确认产物同时包含 `arm64` 与 `x86_64`，再启动该精确 app 做至少一次 3D 可见性冒烟，检查无粉色、Missing Shader、Missing Script 或运行时异常。真实 Windows 10/11 至少 12 秒冒烟标记待补。捕获证据前由主代理检查正确 Unity 实例和固定相机。四次构建后重算完整保护清单，尤其确认 `GrayboxURP.asset`、GraphicsSettings、QualitySettings 和 macOS 架构相关 ProjectSettings 逐字节恢复。

**Commit:** `test: verify ruins cliff runtime integration`

主代理审查：测试 XML、探针 JSON、GUI Profiler 原始 `.data`/三图/notes、自动视觉 manifest/PNG、四次构建、macOS 日志/截图和 Windows 待补声明。

### Task 8 最终执行记录（2026-08-15）

- AssetBuilder focused EditMode `37/37`；日常完整 EditMode `1454/1454`，唯一排除类别为 `TerrainAssetDeep`；完整 PlayMode `91/91`。全部零失败、零跳过。
- 最终 v8 按上方四个显式入口顺序执行，四份日志均记录 `Build Finished, Result: Success.` 和正常 batchmode 退出。三个 Windows Player 均为 `PE32+` GUI x86-64，macOS 精确 binary 为 universal `x86_64 arm64`。
- 每次构建完整退出后，`21` 个 ProjectSettings 与 `14` 个运行时 Prefab 哈希精确稳定，普通/最终退出恢复标记和备份均无残留。
- macOS 精确 binary 的 `45` 秒 NullGfx 冒烟中，`31` 条错误全部为无图形设备下预期的 unsupported Shader，脚本异常、空引用、未处理异常、Missing Script 与崩溃为 `0`。该结果不能作为画面、GPU 或显存验收。
- 真实 Windows 10/11 Player 的视觉/GPU/显存/内存冒烟与用户 Ruins/Cliff 运行时视觉复验仍待完成；不得把本 Task 8 自动证据写成整个 `IDEA-0004` 已验证。

---

## Task 9: Documentation, Quality Catalog and Final Review

**Files**

- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/09-Reusable-Project-Catalog-ZH.md`
- Modify: `Docs/Art/FirstPass/Terrain/FirstTerrainRuntimeIntegration-DevelopmentReference-ZH.md`
- Modify: `Docs/Art/FirstPass/Terrain/Ruins/Ruins_ModuleKit_AssetRecord.md`
- Modify: `Docs/Art/FirstPass/Terrain/Cliff/Cliff_AssetRecord.md`
- Modify: `Docs/Engineering/project-quality-catalog.json`
- Modify generated as required: `Docs/Generated/Project-Inventory-ZH.md`, `Docs/Generated/Test-Inventory-ZH.md`, `Docs/Generated/Documentation-Attention-ZH.md`, `Docs/Generated/Latest-Verification-ZH.md`

**Procedure**

1. 从精确审查范围生成 UTF-8 相对路径清单并设置 `WASTECITY_QUALITY_CHANGED_PATHS`。
2. 更新质量目录和复用目录，映射 `IDEA-0004`、组件、场景、Prefab/Shader、测试与最小复跑入口。
3. Docs/06 只写真实提交和自动证据；在用户查看最终视觉前保持本子项“已实现待验证”，不得写成用户视觉已通过或整个 `IDEA-0004` 已完成。
4. 为质量工具显式设置全部必需环境变量；`AnalyzeTestResults` 分别分析最终 EditMode 与 PlayMode XML。构建摘要使用严格 JSON，四项均引用真实绝对日志；不能把真实执行的构建写成 `NotRequired`。
5. 先 GenerateDocumentation → ValidateDocumentation，确认目录映射可接受；再以 Task 8 的实现提交 SHA 和“等待用户复验”调用 RecordVerification。记录后连续运行 GenerateDocumentation 两次并比较四个 Generated 文件 SHA，最后再只读 ValidateDocumentation。

在调用质量工具前，用 `apply_patch` 在 `$WASTECITY_EVIDENCE/build-summary.json` 写入以下结构，把示例日志路径展开为真实绝对路径；四个日志均必须存在且内容证明成功：

```json
{"Builds":[
  {"Name":"Windows Release 3D","Status":"Succeeded","EvidenceLogPath":"/private/tmp/wastecity-ruins-cliff/task8-builds-final8/windows-release3d.log"},
  {"Name":"Windows Development 3D","Status":"Succeeded","EvidenceLogPath":"/private/tmp/wastecity-ruins-cliff/task8-builds-final8/windows-development3d.log"},
  {"Name":"Windows legacy 2D","Status":"Succeeded","EvidenceLogPath":"/private/tmp/wastecity-ruins-cliff/task8-builds-final8/windows-legacy2d.log"},
  {"Name":"macOS universal 3D","Status":"Succeeded","EvidenceLogPath":"/private/tmp/wastecity-ruins-cliff/task8-builds-final8/macos.log"}
]}
```

设置真实证据环境：

```bash
export WASTECITY_QUALITY_CHANGED_PATHS="$WASTECITY_EVIDENCE/changed-paths.txt"
export WASTECITY_QUALITY_TEST_RESULTS="$WASTECITY_EVIDENCE/editmode-final.xml"
export WASTECITY_QUALITY_ANALYSIS_OUTPUT="$WASTECITY_EVIDENCE/editmode-analysis.txt"
export WASTECITY_QUALITY_VERIFIED_SHA="$(git rev-parse HEAD)"
export WASTECITY_QUALITY_VERIFIED_AT="$(date '+%Y-%m-%dT%H:%M:%S%z' | sed -E 's/([+-][0-9]{2})([0-9]{2})$/\1:\2/')"
export WASTECITY_QUALITY_EDITMODE_RESULTS="$WASTECITY_EVIDENCE/editmode-final.xml"
export WASTECITY_QUALITY_PLAYMODE_RESULTS="$WASTECITY_EVIDENCE/playmode-final.xml"
export WASTECITY_QUALITY_COMPILE_LOG="$WASTECITY_EVIDENCE/compile-final.log"
export WASTECITY_QUALITY_BUILD_SUMMARY="$WASTECITY_EVIDENCE/build-summary.json"
export WASTECITY_QUALITY_HUMAN_PLAYTEST="等待用户复验"
```

`changed-paths.txt` 必须由本里程碑明确审查基线到当前 Task 8 实现 HEAD 的仓库相对路径加 Task 9 文档路径生成，UTF-8、排序去重，不能使用空清单。先分析 EditMode；随后只改变以下两个变量再分析 PlayMode：

```bash
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.AnalyzeTestResults \
  -logFile "$WASTECITY_EVIDENCE/analyze-editmode.log"
export WASTECITY_QUALITY_TEST_RESULTS="$WASTECITY_EVIDENCE/playmode-final.xml"
export WASTECITY_QUALITY_ANALYSIS_OUTPUT="$WASTECITY_EVIDENCE/playmode-analysis.txt"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.AnalyzeTestResults \
  -logFile "$WASTECITY_EVIDENCE/analyze-playmode.log"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/generate-docs-before-record.log"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/validate-docs-before-record.log"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.RecordVerification \
  -logFile "$WASTECITY_EVIDENCE/record-verification.log"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/generate-docs-after-record-1.log"
shasum -a 256 Docs/Generated/*.md > "$WASTECITY_EVIDENCE/generated-1.sha256"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/generate-docs-after-record-2.log"
shasum -a 256 Docs/Generated/*.md > "$WASTECITY_EVIDENCE/generated-2.sha256"
diff -u "$WASTECITY_EVIDENCE/generated-1.sha256" "$WASTECITY_EVIDENCE/generated-2.sha256"
"$WASTECITY_UNITY" -batchmode -quit -projectPath "$WASTECITY_PROJECT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation \
  -logFile "$WASTECITY_EVIDENCE/validate-docs-final.log"
```

执行前核对真实命名空间；若工具现有全名不同，只修正文档命令，不新增替代工具。最后检查：

```bash
git status --short
git diff --check
git diff --name-only
git diff -- Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models \
  Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models \
  Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated \
  Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat \
  Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset \
  Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader \
  Assets/_Game/Editor/FirstArtTerrainAssetBuilder.cs \
  Assets/_Game/Rendering/Graybox3D/GrayboxURP.asset \
  ProjectSettings Packages
```

最后把完整保护清单与 Task 0 基线作 `diff -u`，不能只依赖 Git diff；确认 index 只包含 Task 9 精确文件，并证明没有把并发 `Docs/09` 或 Unity `.meta` 噪声吸收到提交中。

**Commit:** `docs: record ruins cliff integration verification`

主代理独立终审全部提交、受保护路径、场景/资产身份和验证证据后，普通 push 当前分支。等待用户观看固定证据或试玩；用户确认视觉后再单独更新验收状态并提交，不自动合并或创建 Release。
