# 美术资源接入接口

正式版规则层不直接引用具体美术文件。美术通过 `VisualDefinition` ScriptableObject 接入，并集中登记在 `Assets/_Game/ArtIntegration/VisualLibrary.asset`：

- `stableId`：稳定内容 ID，例如 `core.visual.mobile-city`；
- `sprite`：2D Sprite 资源；
- `prefab`：需要动画、粒子或复合结构时使用的 Prefab；
- `fallbackColor`：资源缺失时的程序化占位颜色。

后续替换美术时只修改 `VisualDefinition` 资产引用，不修改资源、战斗、世界生成或存档规则代码。

当前已接入的稳定槽位：

- `core.city.mobile`：移动城市；
- 所有建筑直接使用建筑稳定 ID，例如 `core.building.housing`；
- `core.enemy.light-placeholder` / `core.enemy.heavy-placeholder`：敌人；
- `core.world.rescue-ruin`：救援遗迹；
- `core.world.territory-cache`：领地缓存。

接入步骤：在 Unity 中创建 `Waste City/Presentation/Visual Definition`，填写与槽位一致的 `stableId`，指定 Sprite 或 Prefab，然后把该定义加入 `VisualLibrary.asset` 的 `definitions`。若指定 Prefab，占位 SpriteRenderer 自动隐藏；未提供定义时继续使用程序化占位符。
