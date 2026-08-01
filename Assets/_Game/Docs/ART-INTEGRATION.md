# 美术资源接入接口

正式版规则层不直接引用具体美术文件。美术通过 `VisualDefinition` ScriptableObject 接入：

- `stableId`：稳定内容 ID，例如 `core.visual.mobile-city`；
- `sprite`：2D Sprite 资源；
- `prefab`：需要动画、粒子或复合结构时使用的 Prefab；
- `fallbackColor`：资源缺失时的程序化占位颜色。

后续替换美术时只修改 `VisualDefinition` 资产引用，不修改资源、战斗、世界生成或存档规则代码。
