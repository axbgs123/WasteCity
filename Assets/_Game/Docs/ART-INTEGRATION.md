# 美术资源接入接口

正式版规则层不直接引用具体美术文件。当前正式 3D 场景通过稳定 ID 和专用 3D 表现目录接入美术：

- `GrayboxVisualSlot`：为城市、建筑和地表表现保存稳定 ID、`MeshRenderer` 与可替换 fallback 颜色；
- `ResourceIconCatalog3D`：统一资源栏、矿点、仓库、背包、配方、科技和成本图标，正式资产在 `Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset`；
- `FirstArtTerrainProfile3D` 与 `FirstArtTerrainCatalog3D`：定义正式地表层、材质、数组和运行参数；
- `FirstArtRuinsCliffProfile3D` 与 `FirstArtRuinsCliffCatalog3D`：定义 Ruins/Cliff 稳定模块、Prefab、材质槽和分类回退。

后续替换美术时只修改相应 3D 目录、Profile、Prefab、材质或图标资产，不修改资源、战斗、世界生成或存档规则代码。表现组件不得持有生产、生命、攻击、资源节点或存档真值。

`VisualDefinition`、`VisualLibrary` 与 `Assets/_Game/ArtIntegration/VisualLibrary.asset` 继续保留为历史可替换数据结构和既有资产记录，但已经退役的 `VisualSlot`、`VisualLibraryProvider` 与 2D 场景适配器不再是当前接线。不得把历史 Library 当作正式 3D 的运行时所有者，也不得为恢复旧 2D 表现重新建立第二套场景入口。

当前正式 3D 已接入的稳定表现边界包括：

- `core.city.mobile`：移动城市；
- 所有建筑直接使用建筑稳定 ID，例如 `core.building.housing`；
- 全部正式资源使用 `ResourceIconCatalog3D` 的资源稳定 ID；
- 七类地表与 Ruins/Cliff 使用各自正式 3D Catalog/Profile 的稳定 ID。

接入步骤：先在对应正式目录确认或登记稳定 ID，再替换 3D Prefab、材质、Profile 或 `ResourceIconCatalog3D.asset` 引用，并保持既有灰盒 fallback。地形、Ruins/Cliff 和图标还必须遵守各自导入、构建和证据测试；不要重新引入 `VisualSlot`、`VisualLibraryProvider`、SpriteRenderer 场景适配器或 `FormalPrototype`。
