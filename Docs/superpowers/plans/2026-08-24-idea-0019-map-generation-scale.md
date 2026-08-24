# IDEA-0019 正式地图内容重排与世界表现尺度统一实施计划

需求：`IDEA-0019`

设计：`Docs/superpowers/specs/2026-08-24-idea-0019-map-generation-scale-design.md`

## Task 1：需求、设计和 RED 基线

- 完成 Docs/06、GDD 和路线图批准同步并单独推送；
- 记录当前 HEAD、LFS、Unity `2022.3.62f1`、schema `32` 和 v1 world signature；
- 新增正式地图 v2、资源配额、出生保护、双通路与存档拒绝失败测试；
- 保存首轮 RED 结果，不以编译错误冒充行为 RED。

## Task 2：正式 v2 生成配置与宏区

- 新增不可变 `FormalWorldGenerationCatalog3D` 和纯 C# `FormalWorldGenerator3D`；
- 生成 `64×48` 低频地形宏区、出生保护和三格宽关键通路；
- 后置生成连续 DeepWater、Ruins 与 Cliff；
- 让 `GrayboxWorldLayout3D` 只作 facade，保持通用 `WorldMapModel` 构造器不变；
- 转绿确定性、占比、连通性、路径和部署测试。

## Task 3：24 个资源节点与真实采矿锚点

- 按正式 spot 配置生成铁8、石4、能晶4、水4、生物质4；
- 锁定两个安全铁矿、一个安全石点和三个裂谷铁矿的储量；
- 用统一放置评估验证每个可采节点的 `2×2` 锚点与安全矿站不重叠；
- 覆盖真实绑定、Harvest、枯竭和资源 Marker 数量。

## Task 4：world v2 存档与场景出生

- 先写 v2 capture/roundtrip、v1 拒绝且运行时不变的失败测试；
- generation version/signature 改为从正式配置读取，schema 保持 `32`；
- Scene Authoring 与正式场景改读 v2 StartCell；
- 更新默认展开、路径、控制图和固定场景合同。

## Task 5：Ruins/Cliff 与地表消费 v2 真值

- 更新旧 `76 Ruins / 32 Cliff` 等硬编码合同为 v2 配置合同；
- 保留 14 Prefab、稳定 GUID/localFileID、两类合批、13 共享材质和分类原子回退；
- 验证 control map 与 `WorldMapModel` 逐格一致、单地形 Renderer 和稳定重建；
- 不把布局变化反写到地形源资产或 Shader 真值。

## Task 6：建筑尺度 RED 与 Profile

- 先覆盖 1×1/2×2/3×2、Ground/Inner、四方向和全部建筑类别的 bounds；
- 新增纯表现 `FormalWorldPresentationScaleProfile3D`；
- 施工、完成、废墟与预览共享 footprint 中心和场地缩放；
- 炮塔仅保留低矮建筑基础座，防御视图拥有武器上层；
- 证明逻辑 footprint、旋转、放置、物流和存档坐标不变。

## Task 7：Marker 屏幕尺度与避让

- 先写 Near/Mid/Far 实际像素、默认名称密度、标签间距和稳定对象 RED；
- 扩展地图导航/尺度配置，以地面格屏幕相对高度驱动 LOD；
- 实现 Marker 尺寸、Renderer 显隐、优先级避让和采矿指引覆盖；
- 真实滚轮输入验证镜头与滚动 UI 不穿透。

## Task 8：正式 UI 图标语义尺寸

- 为 `FormalUiLayoutProfile3D` 增加 Inline/Compact/Row/Slot/Node/Hero；
- 逐步替换建造目录、成本、资源、仓库、背包、合成、科技和状态 View 的散落图标尺寸；
- 保持建造栏 `620×54` 及反馈位置不变；
- 覆盖 `1280×720`、`1920×1080`、`2560×1440` 与紧凑窗口比例。

## Task 9：证据、性能和文档收口

- 捕获 v2 总览、宏区/双通路、安全矿区、裂谷、湿地、Ruins/Cliff、建筑类别、Marker 三档和主要 UI；
- 验证单地形 Renderer、24 Marker 稳定复用、对象/材质/监听器无界增长和 300 次稳定分配；
- 更新 Docs/07、Docs/08、Docs/09、项目质量目录与复用目录；
- 运行 `TerrainAssetDeep`、完整 EditMode/PlayMode、质量门和三项现役构建；
- 运行 GenerateDocumentation、ValidateDocumentation、AnalyzeTestResults 与 `RecordVerification`；
- 普通提交并 push 当前分支，不创建 Release、不合并 PR、不 force-push；
- 交付时明确自动证据、用户视觉试玩与真实 Windows 验收的边界。
