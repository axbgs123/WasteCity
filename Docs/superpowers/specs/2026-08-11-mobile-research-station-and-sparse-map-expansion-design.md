# 移动研究站与 64×48 稀疏地图扩展设计规格

> 日期：2026-08-11
> 状态：书面设计已获用户批准，代码尚未实现
> 受控需求：`IDEA-0005`、`IDEA-0006`
> 目标场景：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`
> 目标分支基线：`codex/playtest-fixes`
> 关联正式文档：`Docs/01-Game-Design-Document-ZH.md`、`Docs/05-Formal-Development-Roadmap-ZH.md`、`Docs/06-User-Feedback-and-Change-Control-ZH.md`

## 1. 目的

本变更解决当前 3D 试玩中的两个独立问题：

1. `ResearchStation` 虽然声明为两种表面皆可，但运行标签仍为 `FortressOnly`，因此 Mobile 状态不能放入移动城市内城；
2. 当前默认 3D 世界只有 `32×24` 格，活动空间偏小，而直接用现有随机生成器扩大尺寸会同时增加大量资源、遗迹、深水、悬崖和障碍，不符合用户“扩展区域保持最原始地形”的要求。

本里程碑采用最小、确定、可回退的方案：只把研究站改为移动可建；地图扩大到 `64×48`，原 `32×24` seed 世界原样放在中央，新增外圈全部为空旷普通荒地。

## 2. 已批准产品规则

### 2.1 研究站

- `ResearchStation` 的放置标签继续为 `BuildingPlacement.Either`；
- 运行标签改为 `BuildingOperation.MobileAllowed`；
- Mobile 状态可在 `InnerCity` 建造、施工完成和携带研究站；
- Fortress 状态仍可在 `Ground` 或 `InnerCity` 建造研究站；
- Deploying 与 Packing 不开放新建造，沿用现有 `BuildingMobilityRules.CanConstruct` 的稳定状态限制；
- 未来研究任务在 Fortress 状态按 `100%` 速度推进，在 Mobile、Deploying、Packing 状态按 `50%` 速度推进；
- 城市状态切换不得取消研究、重置研究进度或切换当前科技。

最后两条是后续研究运行时的正式合同，不在本里程碑中加入无调用者的倍率 API。
现有通用 `BuildingMobilityRules.CanOperate` 在 Deploying/Packing 会返回 false；后续研究里程碑不得据此把过渡态研究倍率错误归零，而应以独立、受测的研究推进规则实现上述四状态表。是否同时扩展其他移动建筑在过渡态的运行行为不属于本设计。

### 2.2 地图

- 新世界尺寸固定为 `64×48`；
- 旧世界尺寸固定为 `32×24`；
- 旧世界在新世界中的逻辑偏移固定为 `LegacyOffsetX=16`、`LegacyOffsetY=12`；
- seed 固定沿用 `8128`；
- 中央区域使用现有 `WorldMapModel(32,24,new WorldSeed(8128))` 生成，并逐字段复制到新世界；
- 外围区域每格固定为：
  - `TerrainKind.Wasteland`；
  - `ResourceId=null`；
  - `ResourceAmount=0`；
  - `WorldTraversalKind.Open`；
- 外围不放资源、遗迹、深水、悬崖、障碍、敌人、事件或装饰；
- 外围允许城市通行、A* 寻路和普通地面建筑放置；
- 当前 `64×48` 是完整 Demo `96×64` 地图前的过渡里程碑，不能把主 GDD 的最终目标改小。

## 3. 选择方案

### 3.1 采用：集中式确定性布局工厂

新增一个位于 `WasteCity.Graybox3D` 程序集的纯创建边界，例如 `GrayboxWorldLayout3D`。它集中持有尺寸、偏移和默认 seed，并创建最终 `WorldMapModel`。

推荐公共合同：

```csharp
public static class GrayboxWorldLayout3D
{
    public const int DefaultSeed = 8128;
    public const int LegacyWidth = 32;
    public const int LegacyHeight = 24;
    public const int WorldWidth = 64;
    public const int WorldHeight = 48;
    public const int LegacyOffsetX = 16;
    public const int LegacyOffsetY = 12;

    public static WorldMapModel CreateDefault();
    public static WorldMapModel Create(int seed);
    public static int ToExpandedX(int legacyX);
    public static int ToExpandedY(int legacyY);
}
```

若实施时发现 `ToExpandedX/ToExpandedY` 不需要成为公共 API，可以保持内部方法；但尺寸、偏移和世界创建必须只有一个生产真值，禁止在 Bootstrap、Building Session、World View 和 Editor Authoring 中分别复制数字。

### 3.2 未采用：直接生成 64×48 随机世界

使用 `new WorldMapModel(64,48,seed)` 会改变旧区域内容并给外围增加资源与阻挡，破坏当前试玩位置、部署合同和已验收地形画面。

### 3.3 未采用：提前实现区块流送

区块流送会引入加载生命周期、跨区寻路、存档分区和表现卸载问题；当前 `64×48` 已明显小于地形系统验证过的 `96×64`，没有必要为本次扩图承担该复杂度。

## 4. 世界创建算法

创建顺序固定如下：

1. 使用旧尺寸和传入 seed 创建 `legacyWorld`；
2. 分配 `WorldCell[64,48]`；
3. 把全部 `3072` 格初始化为 Wasteland/Open、无资源；
4. 遍历旧世界全部 `768` 格；
5. 把旧格复制到 `[legacyX+16, legacyY+12]`；
6. 使用 `new WorldMapModel(cells)` 创建最终模型；
7. 不复制旧世界的 revealed 或已采集状态，因为当前 3D Bootstrap 每次只创建新开发夹具，且本里程碑不接正式存档。

复制必须保留以下四个字段：

- `Terrain`；
- `ResourceId`；
- `ResourceAmount`；
- `Traversal`。

不允许重新对中央区域采样 seed，也不允许按视觉近似重建中央区域。

## 5. 坐标与世界位置不变合同

现有映射为：

```text
worldX = logicalX - width / 2
worldZ = logicalY - height / 2
```

旧地图中 `(x,y)` 的世界位置是：

```text
(x - 16, y - 12)
```

新地图中旧格逻辑坐标变为 `(x+16,y+12)`，世界位置是：

```text
((x+16) - 32, (y+12) - 24)
= (x - 16, y - 12)
```

因此原城市、地形、资源节点和中央内容在 Unity XZ 平面中保持原位。

当前序列化移动城市世界位置 `(-9, .5, -4)` 必须保持不变。它在旧地图中的逻辑格为 `(7,8)`，扩图后对应 `(23,20)`。测试和 Editor authoring 应更新逻辑坐标断言，不得移动场景对象来保留旧逻辑数字。

## 6. 运行时接线

### 6.1 `GrayboxSceneBootstrap`

- `WorldSeedValue`、`WorldWidth`、`WorldHeight` 可保留为兼容别名，但必须引用集中布局常量；
- `Initialize()` 改为调用布局工厂，不再直接创建随机 `64×48` 世界；
- `worldView.Generate(World)` 与地形 presenter 调用顺序不变；
- 初始化幂等、表现失败回退和 URP scope 行为不变。

### 6.2 `GrayboxWorldView3D`

该组件已经按 `model.Width/model.Height` 创建 `PlanarCoordinateMapper3D`，继续以最终模型尺寸为准；不增加第二套坐标系统。

现有世界表现仍必须：

- 使用稳定 VisualSlot；
- 使用合批网格；
- 不为 `3072` 个格子创建逐格 GameObject；
- 把 Collider 只当选择/接触代理，不当玩法通行真值。

### 6.3 `GrayboxBuildingSession3D`

- `GroundGrid` 尺寸由集中布局常量得到 `64×48`；
- `InnerGrid` 继续为 `8×6`；
- `GroundBuildRadius` 继续为 `8`，不因地图变大自动扩张；
- 库存、人口、研究解锁夹具、稳定实例序号和 CatalogRevision 行为不变；
- 本次不增加存档或运行时地图替换 API。

### 6.4 `GrayboxBuildingWorldView3D`

- 地面网格线、地面建筑逻辑格到世界位置的换算统一使用 `64×48`；
- 优先从集中布局合同或配置引用读取尺寸，禁止继续保留独立 `32/24` 真值；
- 内城网格尺寸和跟随城市行为不变；
- 基础设施仍保持合并网格，不创建逐格对象。

### 6.5 地形运行时

现有 `FirstArtTerrain` presenter、控制图和单连续 Mesh 已支持任意模型尺寸，并曾在 `96×64` 测试世界通过结构与性能门。本变更只向其传入 `64×48` 最终模型：

- 中央仍显示原七类规则对应的材质；
- 外围控制图只选择 Wasteland 层；
- 不创建新贴图、不修改 Texture2DArray、不重绘当前已验收源材质；
- 不修改柔和边界、世界空间 UV、DeepWater 参数或 Shader；
- Terrain 只负责表现，WorldMapModel 继续是规则真值。

## 7. Editor authoring

`GrayboxSceneAuthoring` 必须与运行时使用同一布局工厂：

- 创建新场景城市时，以旧批准格 `(7,8)` 转换为扩展格 `(23,20)`，最终世界位置仍为 `(-9,.5,-4)`；
- `EnsurePlayableInitialDeployment` 使用最终 `64×48` 世界验证当前序列化世界位置映射出的 `(23,20)`；
- 不得再用 `new WorldMapModel(WorldWidth,WorldHeight,seed)` 创建全随机 64×48 世界；
- 两次 authoring 后场景内容、场景 GUID、渲染资产 GUID 和稳定 GlobalObjectId 必须保持一致；
- 如果运行时修改不需要序列化场景变化，最终场景 blob 应保持不变；若 Unity 因 authoring 更新写回场景，必须证明只有预期合同变化且两次运行幂等。

## 8. 研究站实现边界

本轮对研究站唯一生产行为修改为：

```csharp
operation: BuildingOperation.MobileAllowed
```

必须继续保留：

- 稳定 ID `core.building.research-station`；
- `2×2` 占地；
- `BuildingPlacement.Either`；
- 最低人口 `200`；
- 成本、施工时间、生命值与目录分类；
- 解锁、库存、占格、表面和城市模式的统一校验路径。

禁止为通过测试而：

- 把全部 FortressOnly 建筑改成 MobileAllowed；
- 绕过人口 200；
- 绕过 `BuildingMobilityRules`；
- 在放置控制器中写研究站特例；
- 新增 `ResearchController3D`、研究菜单、自动开始科技或每帧研究 Tick；
- 修改冻结 2D `ResearchController`。

## 9. 未来研究倍率合同

后续研究里程碑必须从城市部署模型读取状态并计算：

| CityMode | 研究倍率 |
|---|---:|
| Mobile | 0.5 |
| Deploying | 0.5 |
| Fortress | 1.0 |
| Packing | 0.5 |

该倍率只影响研究时间推进，不改变建造合法性、生产倍率、城市速度或施工速度。状态切换只改变下一次研究 Tick 的有效 delta，不重新创建 `ResearchModel`。

由于当前 3D 没有玩家可用的研究选择与推进运行时，本里程碑只通过文档固定该表，不创建未被消费的代码。

## 10. 测试设计

### 10.1 RED：研究站

先修改或新增 EditMode 断言：

- Catalog 标签期望 ResearchStation 为 `Either + MobileAllowed`；
- Mobile + InnerCity 的 `CanConstruct` 和 `CanOperate` 为 true；
- Mobile + Ground 仍为 false；
- Fortress + InnerCity、Fortress + Ground 均为 true；
- Deploying/Packing 不允许新建造；
- 人口 `199` 仍锁定，人口 `200` 才解锁；
- 正式开发夹具在人口满足、库存满足、内城空闲时能够原子创建 ResearchStation；
- 其他 29 个 BuildingCatalog 标签保持不变。

现有把 ResearchStation 当作 Mobile 反例的测试应改用真正的地面 FortressOnly 建筑作为反例，不得削弱整张 30 项标签矩阵。

### 10.2 RED：布局工厂

新增纯 EditMode 测试：

- 常量精确为 `64×48`、`32×24`、`16/12`、seed `8128`；
- 最终世界尺寸为 `64×48`；
- 对全部 `768` 个旧格比较 Terrain、ResourceId、ResourceAmount、Traversal；
- 对全部 `2304` 个外围格断言 Wasteland、Open、无资源、amount 0；
- 最终 `ResourceNodeCount` 与旧世界相同；
- 多次创建结果完全一致；
- 对全部旧格验证旧 mapper 世界位置等于新 mapper 偏移格世界位置；
- 旧城市世界位置映射到新格 `(23,20)`。

### 10.3 RED：运行时与 authoring

- Bootstrap 创建的是布局工厂世界，而不是全随机 64×48；
- WorldView mapper 为 `64×48`；
- GroundGrid 为 `64×48`，InnerGrid 仍为 `8×6`；
- 序列化城市世界位置仍为 `(-9,.5,-4)`；
- 新城市格 `(23,20)` 的 3×3 展开校验结果与旧 `(7,8)` 一致；
- 外围选取至少四个代表点，城市路径可达且普通地面建筑在其他条件满足时合法；
- 外围不生成资源 VisualSlot；
- authoring 两次运行保持场景和资产身份稳定。

### 10.4 回归与性能

必须运行：

- 受影响 focused EditMode；
- 3D 正式场景 PlayMode 输入与建造流程；
- 完整 EditMode；
- 完整 PlayMode；
- 无界面编译；
- 默认 3D Windows 构建；
- Development 3D Windows 构建；
- legacy 2D Windows 回归构建。

结构与性能门：

- 地形继续为单 Renderer、单长期地形表现根；
- 不出现 `3072` 个逐格对象；
- 现有 128 混合建筑结构门继续通过；
- 64×48 地形五次生成中位数不得差于既有 96×64 已验证预算；
- 预热后相关适配器 300 次调用维持 0 B 托管分配；
- 如生产路径新增逐帧逻辑，必须重新录制 GUI Profiler；本设计不要求新增逐帧逻辑。

## 11. 数据、存档与兼容

- schema 继续为 `30`；
- 不修改 `FormalSaveData`、`FormalSaveController` 或 Persistence；
- 不读写正式 `formal-world.json`；
- 当前 3D Building Session 是开发夹具，本次不迁移既有正式存档中的 32×24 数组；
- 冻结 2D `FormalPrototype`、`PlaceholderWorldView`、`PlaceholderBuildingController` 和 2D `ResearchController` 零差异；
- 如果未来正式存档接入 64×48 世界，必须另写坐标与数组迁移规格，不能复用本次“新开局中央嵌入”冒充存档迁移。

## 12. 文件边界候选

实施计划应从实际依赖审查中精确确认文件清单。设计允许的生产边界候选为：

- 新增 `Assets/_Game/Scripts/Graybox3D/GrayboxWorldLayout3D.cs` 及 `.meta`；
- 修改 `Assets/_Game/Scripts/Graybox3D/GrayboxSceneBootstrap.cs`；
- 修改 `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingSession3D.cs`；
- 修改 `Assets/_Game/Scripts/Graybox3D/Building/GrayboxBuildingWorldView3D.cs`；
- 修改 `Assets/_Game/Scripts/Building/BuildingGrid.cs`；
- 修改 `Assets/_Game/Editor/GrayboxSceneAuthoring.cs`；
- 按 TDD 需要修改或新增对应 EditMode/PlayMode 测试。

通常不应修改：

- `GrayboxWorldView3D.cs`，其现有动态 mapper 已能适配模型尺寸；
- FirstArt Terrain Shader、Material、Texture2DArray 和源 PNG；
- `PlanarCoordinateMapper3D.cs`；
- `BuildingMobilityRules.cs`；
- 场景中的序列化城市位置；
- Packages、GraphicsSettings、QualitySettings、正式存档和冻结 2D 文件。

若 RED 或编译证明需要计划外生产路径，必须先停止、解释根因并修订实施计划，不得现场扩散修改。

## 13. 错误处理

- 布局工厂只接受确定的整数 seed；尺寸和偏移使用编译期常量，不接收无效运行时尺寸；
- 复制前必须满足 `LegacyOffsetX + LegacyWidth <= WorldWidth` 与对应 Y 条件；测试冻结这些关系；
- Bootstrap 缺 URP scope 或 WorldView 时继续按现有合同返回 false，不生成半个世界；
- Terrain presenter 失败时继续恢复灰盒 surface fallback；
- Building Session 未配置时继续使用现有 development fixture 恢复路径；
- 研究站放置失败必须返回现有稳定合法性原因，不增加专用异常文本。

## 14. 回退

本变更可以通过以下最小范围回退：

1. Bootstrap 恢复旧 `32×24` 直接世界创建；
2. Building Session 与 Building World View 恢复旧地面尺寸；
3. ResearchStation 恢复 `FortressOnly`；
4. 删除新增布局工厂和对应测试；
5. authoring 恢复旧逻辑格合同。

因为不改 schema、不迁移正式存档、不改材质源、不移动序列化城市，也不修改冻结 2D，回退不需要清理玩家数据或重新制作美术资源。

## 15. 完成定义

只有同时满足以下条件，本轮代码和地图实现才算完成：

- 研究站可在 Mobile 内城真实放置并完成施工；
- 没有新增研究 UI 或研究推进运行时；
- 世界实际为 `64×48`；
- 中央旧区域全部字段逐格一致；
- 新外圈全部为空旷 Wasteland/Open；
- 原城市与旧内容世界位置不变；
- 外围城市通行、路径和普通地面建筑合法性通过；
- 地形仍为连续单 Mesh/Renderer，外围显示普通荒地材质；
- focused、完整测试、编译、三构建和性能门通过；
- 场景 authoring 幂等；
- 未提交受保护的地形 importer `.meta` 或机器生成 ProjectSettings；
- `Docs/06` 回写真实提交和验证证据，但不把未来研究系统记为已经实现。

状态回写必须区分两条需求：

- `IDEA-0006` 可在地图实现与自动化完成后标记为 `已实现待验证`；
- `IDEA-0005` 只能记录“移动建造子范围已实现”，总体继续保持 `开发中`，直到后续研究菜单与四状态 `100%/50%` 推进规则真实实现并通过验证。
