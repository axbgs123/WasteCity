# 《废土移动城市》第一版地表材质接入开发参考

> 文档类型：工程经验与后续开发检查表<br>
> 建立日期：2026-08-11<br>
> 关联需求：`IDEA-0004`<br>
> 关联规格：`Docs/superpowers/specs/2026-08-10-first-terrain-runtime-integration-design.md`<br>
> 当前基线：七类连续地表和 Ruins/Cliff 首批运行时几何已经接入 `GrayboxPrototype3D`；Ruins/Cliff 已实现待用户视觉复验。本文不代表全部第一版美术完成，也不改变玩法规则或 schema `30`。

## 1. 文档目的

本文记录第一版七类地表从源文件交付、Unity 导入、运行资产生成、Shader 混合、场景接线、性能验证到视觉验收过程中实际遇到的问题和已验证的解决方式，供后续城市、建筑、废墟、悬崖、水体、UI、VFX 与其他材质接入复用。

本文是工程参考，不代替功能规格、实施计划、用户视觉批准或变更控制记录。新问题仍应在 `Docs/06-User-Feedback-and-Change-Control-ZH.md` 中按稳定 ID 单独登记。

## 2. 当前实现边界

当前正式地表表现包含：

- 七层固定顺序：`Wasteland`、`Rocky`、`Wetland`、`Crystal`、`Ruins`、`DeepWater`、`Cliff`；
- BaseColor、Normal、Mask、Height 四个 `Texture2DArray`；
- 一张共享 URP 材质和一个受版本控制的 Shader；
- 世界空间 XZ 取样；
- 两张确定性控制图；
- 柔和地表权重混合；
- 一个连续 Mesh、一个 MeshFilter、一个 MeshRenderer；
- 14 个可替换 Prefab（Ruins `8`、Cliff `6`），每个 Prefab 内嵌唯一可读运行时 Mesh；
- `FirstArtRuinsCliffCatalog3D` 与 Profile 提供稳定表现映射，运行时按类别确定性布局并合批为最多两个 MeshRenderer；
- 13 个共享几何材质和一个独立 URP 几何 Shader；
- Ruins/Cliff 类别级原子回退，复用同一个正式地形 presenter；
- 正式表现失败时恢复灰盒的原子回退；
- seed `8128` 下与二维规则地图相同的地形类别和资源节点布局。

材质和模型只负责表现。以下内容仍由规则层决定，禁止写入 Shader、材质、贴图或美术 Prefab：

- 深水、悬崖是否可通行；
- 湿地、岩石地、大型废墟的移动倍率；
- 城市能否展开；
- 建筑能否放置、占地、成本、施工时长和资源节点要求；
- 稳定 ID、存档和 schema。

## 3. 已遇到的主要问题与结论

### 3.1 大型源文件直接进入普通 Git 历史

问题：`.kra`、`.ora` 等分层源最初没有完整 LFS 规则，七个源文件以普通 Git blob 进入历史，总量约 129.6 MiB。即使后来删除文件，普通历史仍会持续膨胀。

已验证做法：

- `.png`、`.fbx`、`.blend`、`.kra`、`.ora`、`.psd`、`.wav` 统一使用 Git LFS；
- 在首次暂存大文件前先建立 `.gitattributes`；
- 交付验证器必须读取 Git 索引，确认内容是真实 LFS v1 pointer，而不只检查文件扩展名；
- 已经污染的来源分支不改写历史、不 force-push；从干净基线压入最终文件树。

后续要求：任何新增正式二进制美术目录，在第一件资源提交前先验证 LFS 属性和远端对象上传。

### 3.2 Unity 默认导入语义与 PBR 通道不一致

问题：Unity 默认会把 Normal、Mask、Height 当作普通 sRGB 纹理；静态 FBX 还可能错误导入动画、相机和灯光。

已验证导入合同：

| 类型 | 色彩空间/类型 | 关键设置 |
|---|---|---|
| BaseColor | sRGB、Default | Repeat、MipMap、最大 2048 |
| Normal | Linear、NormalMap | Repeat、MipMap、最大 2048 |
| Mask | Linear、RGBA | 保留 Alpha、Uncompressed 源合同 |
| Height | Linear、SingleChannel | 16-bit 源；运行副本可确定性降采样 |
| 静态 FBX | Model | 关闭 Animation、Camera、Light、自动 Collider 和 Read/Write |

Mask 通道始终为：

```text
R = Metallic
G = Ambient Occlusion
B = Detail Mask / 该层法线细节强度
A = Smoothness
```

后续要求：导入策略只作用于批准目录，不应改变项目其他资源；重复重导入前后 `.meta` GUID 必须一致。

### 3.3 源贴图合格不等于已经可在场景中使用

问题：离线 4×4 平铺图合格，只能证明贴图本身无明显接缝；不能证明世界比例、光照、混合、远景可读性、运行格式、显存或交互投影正确。

已验证分层：

1. 源文件合同；
2. Unity importer 合同；
3. 生成数组合同；
4. Shader 与材质合同；
5. 正式场景运行合同；
6. 默认镜头视觉验收；
7. 性能和 Windows 构建验收。

文档状态必须明确区分“源资源完成”“Unity 可导入”“已经接线”“视觉通过”“用户接受已知偏差”。

### 3.4 逐格 UV 会放大重复和硬边

问题：旧灰盒世界每格使用独立 0–1 UV。直接替换正式贴图会让每格重复一次，并在格子边缘出现方向和法线断裂。

已验证做法：

- 使用世界 XZ 坐标采样，不使用单格 UV、相机空间或屏幕空间坐标；
- 一张 2048 贴图覆盖约 `4×4` 外城逻辑格；
- 所有地表层共用同一世界比例；
- 加入低幅度宏观色调变化抑制远景重复，但不得改变玩法分类；
- 材质边界用控制图权重混合，而不是切割出大量独立材质块。

### 3.5 七套独立材质会增加 Draw Call 和接缝管理成本

问题：按类别生成七个 Renderer/Material 容易产生交界重叠、硬缝、排序问题和额外 SetPass。

已验证架构：固定七层 `Texture2DArray` + 单一共享材质 + 单一连续 Mesh。普通区域只采样主要层，边界最多保留三个最高权重层。

后续要求：模型类资产可以使用少量共享材质槽，但大面积连续地表不要退回“每格或每类一个对象”的结构。

### 3.6 数组生成顺序和失败回滚容易留下半成品

问题：源引用缺失、数组尚未 finalization、格式不兼容或导入重入时，生成工具可能覆盖部分正式资产，造成 GUID 变化、损坏引用或粉色材质。

已验证做法：

```text
先验证全部源文件
  -> 在临时对象中生成四个数组
  -> 验证层数、尺寸、色彩空间、格式和 mip
  -> 生成/验证材质与 Profile
  -> 最后原子替换正式生成资产
  -> 保存、重导入并再次验证
```

生成失败时必须保留上一套有效正式资产，不允许把未完成数组提交为当前版本。

### 3.7 运行时 CPU 副本会造成内存重复

问题：数组生成和上传完成后若继续保持 readable，Editor 原生内存和运行时载荷会明显增加；磁盘压缩体积、Editor 原生内存和 Windows GPU 显存也容易被误当成同一个指标。

已验证做法：

- 完成数组构建后释放不再需要的 CPU 可读副本；
- 记录压缩载荷、Editor 原生内存和目标平台 GPU 显存三个独立指标；
- 不用 macOS Editor 内存冒充 Windows Player 显存；
- 运行时不重新解压七套 2048 源图，不创建材质实例。

当前证据：四个压缩数组载荷合计约 `127,227,779 B`；Editor 原生内存观察值约 `254,457,350 B`。后者不是 Windows GPU 显存结论。

### 3.8 光照和贴图亮度叠加导致综合色调过亮

当前场景合同使用：

```text
方向光颜色 = (1.0, 0.956, 0.85)
方向光强度 = 0.90
环境光强度 = 1.0
```

普通荒地 BaseColor 本身已经偏暖、偏亮；原合同 `1.25` 的方向光与环境光叠加后会压缩中间调，让荒地发白、不同类别更难区分。`BUG-0003` 已把主方向光降到 `0.90`，不修改已验收源贴图；当前状态仍是“已实现待用户视觉复验”，不是视觉问题已经人工通过。

后续优化顺序：

1. 冻结固定相机、分辨率和参考画面；
2. 先校准方向光强度、颜色和环境光，再改贴图；
3. 保留 BaseColor 的 PBR 合理范围，避免为补偿场景灯光而永久压暗源贴图；
4. 用统一 Lighting Profile 或受控 authoring 常量管理，不在场景中手改后被 authoring 覆盖；
5. 同时观察荒地亮部、深水黑位、结晶地与资源节点可读性；
6. 记录直方图、平均亮度和固定截图，但最终仍由用户视觉批准。

建议第一轮只调整一个变量：降低主方向光或环境光；不要同时改灯光、全部 BaseColor、Shader tint 和后处理曝光，否则无法判断真正有效项。

### 3.9 柔和边缘与地形可读性存在冲突

问题：过窄混合会出现方格硬边；过宽混合会让 Rocky/Wetland/Crystal 失去类别辨识，DeepWater/Cliff 的不可通行边界也会被弱化。

已验证原则：

- Wasteland、Rocky、Wetland、Crystal 使用较柔和混合；
- Ruins、DeepWater、Cliff 边缘仍可使用柔和视觉过渡，但玩法边界必须一眼可读；
- BaseColor、Normal、Mask、Height 使用相同权重；
- Height 只微调混合权重，不做顶点位移、深度偏移或视差轮廓；
- 三岔点最多混合三个主要层；
- 资源节点独立显示，不烘焙进 Crystal 或其他地表。

### 3.10 动态材质必须用连续画面验收

问题：DeepWater 双法线移动在静态截图和代码检查中存在，但实际十秒变化量低于可感知目标。静态图不能证明动态效果。

后续要求：

- 动态水、风、发光脉冲、施工和 VFX 必须提交连续录屏；
- 固定 seed、相机、灯光和录制时长；
- 记录起始、中段、结束帧差；
- “用户接受当前偏差”与“技术视觉门通过”分开记录。

当前 DeepWater 动态不足已经被用户接受为第一版已知偏差，但仍是后续优化项。

### 3.11 原始 FBX 可导入不等于适合直接作为运行时合批源

问题：批准的 14 个原始 FBX 必须保持 Read/Write 关闭，但 Ruins/Cliff 运行时合批需要稳定读取完整顶点、索引、法线、切线和 UV。让 Prefab 直接引用 raw FBX Mesh 会把 importer 可读性与运行时实现耦合，也不利于以稳定 localFileID 管理重建。

已验证做法：

- Builder 在 Editor 中使用 `Mesh.AcquireReadOnlyMeshData` 从批准的 raw FBX 确定性复制数据；raw FBX 和 `.meta` 不修改，ModelImporter 的 Read/Write 始终关闭；
- 每个运行时 Prefab 内嵌且只内嵌一个命名为 `<StableId>_RuntimeMesh` 的可读 Mesh 子资源，Prefab 只含 Transform、MeshFilter、MeshRenderer；
- 首次发布固定 Prefab GUID，后续重建原位更新同一 Mesh 子资源，并以测试保护 Prefab GUID 和 Mesh localFileID 稳定；
- Geometry 只读取 Prefab 的 `sharedMesh` 与共享材质，明确忽略 Prefab Transform；完整 placement 矩阵是唯一空间变换来源；
- 输出合批 Mesh 在累计顶点数超过 `65,535` 时显式使用 `UInt32`；法线使用 inverse-transpose，切线使用线性变换后对新法线执行 Gram-Schmidt 正交化；
- 14 个 Prefab 是资产合同与 Mesh/材质来源，不实例化为逐格常驻对象。默认世界只保留 `RuntimeGeometry`、`RuinsGeometry`、`CliffGeometry` 三个长期对象和两个 Renderer。

## 4. 正式表现与灰盒回退

正确接入顺序：

```text
规则世界与灰盒先成功生成
  -> 验证 Profile / Shader / Material / arrays
  -> 创建连续 Mesh
  -> 生成并验证控制图
  -> 绑定共享材质
  -> 确认正式 Renderer 可用
  -> 最后隐藏对应灰盒 Renderer
```

连续地表的任何一步失败：

- 销毁本次未完成的运行对象；
- 恢复全部对应灰盒 Renderer；
- 保留资源节点和其他系统；
- 输出一次包含上下文的错误；
- 不阻止规则世界、城市、建造或存档初始化。

禁止先隐藏灰盒再尝试创建正式表现。

Ruins/Cliff 在连续地表成功后按类别独立事务：完整验证并生成某一类别的合批几何，成功后才隐藏该类别灰盒；失败时只清理该类别正式几何并恢复其灰盒，不触碰另一类别、连续地表或资源节点。`ClearPresentation`、禁用、销毁、世界重建和 Profile 替换都必须先恢复对应灰盒，再销毁 owned Mesh。

## 5. Authoring 与场景合同

后续美术接入不能只手工拖入场景，应由 authoring 和测试共同保护：

- 场景对象数量和名称唯一；
- 资产引用精确指向批准 GUID；
- Ruins/Cliff 的 14 个 Prefab、13 个共享材质、Profile 与唯一内嵌运行时 Mesh 均通过 Catalog/Builder 合同生成，不在场景中手工复制；
- 重复运行 authoring 后场景字节、关键 GlobalObjectId 和 `.meta` GUID 稳定；
- 破损场景在 mutation 前失败，而不是静默生成第二套对象；
- authoring 是最终灯光/材质参数的唯一写入者之一，避免手改被下一次生成覆盖；
- Build Settings、默认 3D 入口、冻结 2D 场景和全局 URP 设置不被表现接入顺带改变。

## 6. 性能经验

当前结构门：

- 正式地表长期对象：1；
- Renderer：1；
- 运行时控制图：2；
- 无逐格 GameObject、Renderer 或 Material；
- 地表 presenter 无常驻 `Update` / `LateUpdate`；
- 稳定运行预热后连续 300 帧托管分配 `0 B`。

Ruins/Cliff 子项的当前结构与性能证据：

- Ruins/Cliff 长期对象 `3`、Renderer `2`、运行时 owned Mesh `2`、材质槽 `13`，无逐格 Prefab 实例；
- seed `8128`、`64×48` 的 placement 数 `108`（Ruins `76`、Cliff `32`），布局与合批五次中位数 `59.1255 ms`，连续地表加几何总初始化五次中位数 `95.8269 ms`；
- 输出 `312,914` 顶点、`168,824` 三角形，稳定观察 `300` 次托管分配 `0 B`；
- GUI Profiler 连续 `300` 帧平均 `174.083 FPS / 5.7444 ms`，Rendering 观察为 SetPass `19`、Draw Calls/Batches `49/49`；GPU 帧时显示不可用，目标平台显存和真实 Windows 10/11 Player 冒烟仍待补。
- 2026-08-15 最终权威自动化为 AssetBuilder focused EditMode `37/37`、日常完整 EditMode `1454/1454`（只排除本批不运行的 `TerrainAssetDeep`）和完整 PlayMode `91/91`，全部零失败、零跳过；
- 最终 v8 的 Windows Release 3D、Development 3D、legacy 2D 与 macOS universal 3D 四个正式构建均成功；三个 Windows Player 均为 `PE32+` GUI x86-64，macOS 精确 binary 为 universal `x86_64 arm64`。每次完整退出后 `21` 个 ProjectSettings 与 `14` 个运行时 Prefab 哈希精确稳定，普通/最终退出恢复标记和备份均无残留；
- macOS 精确 binary 的 `45` 秒 NullGfx 启动冒烟中，`31` 条错误全部是无图形设备下预期的 unsupported Shader，脚本异常、空引用、未处理异常、Missing Script 与崩溃均为 `0`。该结果只证明 Player 启动/关闭路径，不证明真实渲染、GPU 或显存；真实 Windows 10/11 Player 的视觉/GPU/显存/内存冒烟和用户对 Ruins/Cliff 运行时画面的视觉复验仍待完成。

当前 seed `8128`、`96×64` 世界五次生成中位数约 `173.9275 ms`；连续 300 帧平均约 `469 FPS / 2.13 ms`。这些数据只能证明当前 Mac Editor 基线，不替代目标 Windows Player 的 GPU 和显存验证。

后续新增模型、Prefab、贴花、VFX 时应分别记录新增成本，不能把总成本都归因于地表。

## 7. 试玩暴露出的跨系统提醒

材质接入没有改变规则，但正式表现会显著影响玩家对规则的理解。本次试玩暴露出以下需要单独变更控制的问题：

- 城市默认出生点的 `3×3` 范围包含湿地和大型废墟，规则拒绝展开；失败原因没有进入可见 UI，玩家感知为“按 F 没反应”；
- 当前光照合同会让暖黄荒地综合色调偏亮；
- 内城建筑是否可放置同时受 `Placement`、`Operation`、城市形态、研究、前置建筑、人口、库存和占地影响；如果 UI 没有持续显示主失败原因，玩家容易误判为平台或材质接入破坏了放置。

结论：每次表现接入回归不能只断言“规则结果没有变化”，还必须验证玩家能看到失败原因、当前控制对象、表面类型和合法性状态。

## 8. 后续材质/模型接入的最高效流程

1. 冻结稳定 ID、尺寸、Pivot、占地和默认相机；
2. 建立一件黄金样板，不批量制作；
3. 完成源文件、LFS、许可证和命名检查；
4. 建立目录限定 importer 合同；
5. 提交稳定 `.meta` 并验证重复重导入；
6. 创建共享 Material/Profile，不让 Prefab 持有玩法真值；
7. 通过稳定 `VisualSlot` 或批准映射接入，保留灰盒回退；
8. authoring 重复执行两次，比较 GUID、GlobalObjectId 和场景 hash；
9. 在默认镜头下验证比例、颜色、轮廓、遮挡和四方向旋转；
10. 验证交互：移动、展开、内外城投影、放置、施工、拆除和镜头；
11. 验证结构、300 帧 GC、1080p Profiler、Release/Development/legacy 构建；
12. 用户批准黄金样板后再批量生产同类资源。

## 9. 每次提交前检查表

### 资产与版本控制

- [ ] 二进制源和交付文件由 LFS 管理，索引内容为真实 pointer；
- [ ] 来源、许可证、负责人、版本和哈希已记录；
- [ ] `.meta` GUID 重导入稳定；
- [ ] raw FBX 保持 Read/Write 关闭，Prefab 内嵌运行时 Mesh 可读且 GUID/localFileID 重建稳定；
- [ ] 未提交 Library、Temp、Logs、本机 ProjectSettings 或账号信息。

### 贴图与材质

- [ ] BaseColor 为 sRGB；Normal/Mask/Height 为 Linear；
- [ ] Mask RGBA 通道符合冻结语义；
- [ ] Normal 是 Tangent Space，导入类型正确；
- [ ] 世界空间比例正确，无单格 0–1 重复；
- [ ] 相邻材质无硬缝、黑边、法线断层和明显棋盘重复；
- [ ] 灯光校准没有通过永久破坏源 BaseColor 来补偿；
- [ ] 没有运行时 `.material` 实例化。

### 玩法与交互

- [ ] 材质、模型和 Prefab 不持有玩法真值；
- [ ] 通行、减速、展开和放置结果与规则地图一致；
- [ ] 资源节点仍是独立稳定对象；
- [ ] 内外城投影仍使用批准的数学面/代理 Collider；
- [ ] 失败原因对玩家可见；
- [ ] 灰盒回退完整可用。

### 验证与证据

- [ ] focused EditMode/PlayMode 通过；
- [ ] 完整 EditMode/PlayMode 通过；
- [ ] 无界面编译和目标构建通过；
- [ ] 默认镜头、近景边界、三岔点和灰盒对照图齐全；
- [ ] 动态材质提供连续录屏；
- [ ] 结构、生成耗时、帧时、GC 和内存指标分别记录；
- [ ] 技术失败、用户接受偏差和最终通过使用不同状态描述。

## 10. 后续优先优化项

1. 建立可独立调节且由 authoring 保护的场景 Lighting Profile；
2. 在固定默认镜头下重新校准方向光、环境光和地表中间调；
3. 增强 DeepWater 蓝黑比、高光可读性和缓慢动态；
4. 提高 Rocky/Wetland/Crystal 的远景分类辨识，同时保持柔和过渡；
5. 在已接入的 Ruins/Cliff 模块上继续复验积尘、瓦砾、落石、连续地表接缝和俯视遮挡；当前自动证据已生成，仍待用户视觉复验；
6. 为玩家可见的展开/放置失败原因增加统一 HUD 反馈；
7. 在真实 Windows 10/11 Player 上补 GPU、显存和视觉冒烟。
