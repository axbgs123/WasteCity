# IDEA-0001 F1A 建筑位置与移动运行规则设计

## 1. 目标与范围

本规格实现 `IDEA-0001` 在 F1A 的第一个独立基础里程碑：

- 为全部已登记建筑增加“内城、地面、两者皆可”位置标签；
- 为全部已登记建筑增加“移动可运行、仅展开运行、地形依赖”运行标签；
- 用统一纯规则判断建筑在城市状态和实际位置下能否施工、运行；
- 让现有运行时、生产、防御、维修、容量效果和单位制造服从该规则；
- 将实际位置写入存档，旧存档建筑安全迁移为地面建筑；
- 在现有建筑说明中显示位置与运行标签。

本里程碑不新增内城建造面板、不改变普通建造列表的可见性或筛选、不实现前哨独立库存与远程控制，也不修改 `BUG-0001`。

## 2. 现状差异

当前工程已经有 `Mobile / Deploying / Fortress / Packing` 城市状态，长周期研究和建筑施工也已有部分堡垒门控，但存在以下缺口：

1. `BuildingDefinition` 没有位置或运行标签；
2. 所有玩家放置建筑都被当成同一种城市相对网格对象；
3. 城市收起后，生产控制器仍可能继续统计已完成地面工厂；
4. 炮塔、护盾、自动维修、人口容量、仓储容量和友军制造没有统一的城市状态运行门；
5. 存档无法区分未来的内城建筑与当前地面建筑；
6. 建筑说明没有显示位置和移动运行限制。

## 3. 方案比较与选择

### 方案 A：只给建筑目录增加文字标签

优点是改动最小。缺点是标签不参与施工、生产、防御或存档，只是说明文字，不能阻止实际规则继续偏离 GDD。

### 方案 B：定义标签、实际位置和统一规则，并接入现有运行时

优点是标签成为玩法真值；当前地面建筑会正确服从展开门，未来内城放置和前哨系统可以直接复用；旧存档可明确迁移。代价是需要同步修改建筑实例、存档和多个运行入口。

### 方案 C：立即制作完整内城网格和双建造界面

优点是玩家立刻可以在移动中建造。缺点是会同时牵涉建造菜单、坐标系、相机、选择、放置预览、撤离和前哨，范围过大，并与尚未澄清的 `BUG-0001` 相交。

采用方案 B。它是可运行、可测试、可存档的最小真实基础，不用临时文字冒充实现，也不提前扩张到完整建造交互。

## 4. 领域模型

### 4.1 标签

```csharp
public enum BuildingPlacement
{
    Ground = 0,
    InnerCity = 1,
    Either = 2
}

public enum BuildingOperation
{
    MobileAllowed = 0,
    FortressOnly = 1,
    TerrainDependent = 2
}

public enum BuildingSite
{
    Ground = 0,
    InnerCity = 1
}
```

`BuildingPlacement` 和 `BuildingOperation` 属于建筑模板；`BuildingSite` 属于已放置建筑实例。`BuildingSite.Ground = 0` 保证旧 JSON 中缺失字段时默认迁移为地面建筑。

### 4.2 统一规则

新增纯 C# `BuildingMobilityRules`：

```csharp
public static bool SupportsSite(BuildingDefinition definition, BuildingSite site);
public static bool CanConstruct(BuildingDefinition definition, BuildingSite site, CityMode mode);
public static bool CanOperate(BuildingDefinition definition, BuildingSite site, CityMode mode);
public static string PlacementName(BuildingPlacement placement);
public static string OperationName(BuildingOperation operation);
```

规则矩阵：

| 实际位置 | 运行标签 | Mobile | Deploying / Packing | Fortress |
|---|---|---:|---:|---:|
| 内城 | MobileAllowed | 可施工、可运行 | 暂停 | 可施工、可运行 |
| 内城 | FortressOnly | 暂停 | 暂停 | 可施工、可运行 |
| 地面 | 任意合法标签 | 暂停 | 暂停 | 可施工、可运行 |
| 地面 | TerrainDependent | 暂停 | 暂停 | 可施工、可运行；具体资源或灵脉条件由放置合法性系统判断 |

移动可运行建筑在移动状态使用当前基础倍率 `1.0`；堡垒状态继续使用现有 `1.25` 生产倍率。该差值就是本里程碑的移动降效，不增加新的倍率常量。

当前地面建筑在城市离开后暂停，直到后续前哨里程碑为其提供独立补给、库存和通信状态；本里程碑不得把它们伪装成已经完成的前哨。

## 5. 当前建筑标签

| 建筑 | 放置位置 | 运行状态 |
|---|---|---|
| 采矿站 | 地面 | 地形依赖 |
| 住房 | 两者皆可 | 移动可运行 |
| 仓库 | 两者皆可 | 移动可运行 |
| 城墙 | 地面 | 仅展开运行 |
| 研究站 | 两者皆可 | 仅展开运行 |
| 冶炼厂 | 地面 | 仅展开运行 |
| 装配厂 | 两者皆可 | 移动可运行 |
| 机枪塔、重型机枪塔、激光塔 | 地面 | 仅展开运行 |
| 发电站 | 地面 | 仅展开运行 |
| 灵火炉、炼器坊 | 地面 | 仅展开运行 |
| 剑阵台、御剑台 | 地面 | 仅展开运行 |
| 聚灵阵 | 地面 | 地形依赖 |
| 炼丹房 | 两者皆可 | 移动可运行 |
| 傀儡工坊 | 两者皆可 | 移动可运行 |
| 菌落池、培育室、代谢炉 | 地面 | 仅展开运行 |
| 孢子塔、酸液塔 | 地面 | 仅展开运行 |
| 巨兽栏 | 地面 | 仅展开运行 |
| 共振炉 | 地面 | 仅展开运行 |
| 灵能工坊 | 两者皆可 | 移动可运行 |
| 心灵尖塔 | 地面 | 仅展开运行 |
| 意识网络 | 两者皆可 | 移动可运行 |
| 护盾发生器 | 两者皆可 | 移动可运行 |
| 自动维修机甲站 | 两者皆可 | 移动可运行 |

聚灵阵虽然当前没有资源节点判定，但按设计语义标记为地形依赖；本里程碑只要求堡垒地面运行，具体灵脉条件留给地形与放置合法性里程碑。

## 6. 运行时接入

`PlacedBuilding` 和 `BuildingRuntime` 保存 `BuildingSite`。现有鼠标放置、空间模板和旧存档恢复全部显式或默认创建 `Ground`，所以本次不会改变现有建造入口。

`BuildingRuntime.IsOperational` 同时要求：

- 建筑施工完成；
- 已连接物流；
- `BuildingMobilityRules.CanOperate` 为真。

人口容量、仓储容量等持续效果以 `IsOperational` 为开关。施工和维修推进使用 `CanConstruct` / `CanOperate`，从而允许未来的内城移动施工和维修，同时让当前地面建筑在移动、展开中、收起中暂停。

下列消费者必须读取同一运行状态：

- 路线生产和被动生产；
- 炮塔、护盾发生器、自动维修机甲站；
- `OperationalCount`；
- 傀儡制造和巨兽培育；
- 建筑持续效果。

研究仍沿用 `CityOperationalRules.LongWorkAllowed`，只在 `Fortress` 推进。

## 7. 存档

存档 schema 从 `28` 升为 `29`。`BuildingSnapshot` 增加：

```csharp
public int site;
```

schema 29 写入每个实例的 `BuildingSite`。schema 28 及更旧存档的 `site` 缺失，按枚举默认值恢复为 `Ground`。未知数值也回退为 `Ground`，避免坏档。

不改变稳定 ID、资源、研究或生产进度结构。

## 8. 显示

统一建筑说明新增：

```text
位置：两者皆可 · 运行：移动可运行
```

玩家可以在后续建造交互完成前先理解每个建筑的设计限制。现有建造菜单是否出现内容仍属于 `BUG-0001`，本次不改变其打开、筛选和选择规则。

## 9. 测试与验收

EditMode 测试必须覆盖：

1. 全部 `BuildingCatalog.All` 都有合法位置和运行标签；
2. 规则矩阵的 Mobile、Deploying、Fortress、Packing 行为；
3. 当前建筑网格默认创建地面实例；
4. 升级保留实际位置且拒绝不支持该位置的目标；
5. schema 29 往返保存实际位置；
6. schema 28 缺失位置字段时恢复为地面；
7. 建筑说明显示友好位置与运行名称，不泄露枚举或稳定 ID。

PlayMode 回归继续验证正式场景能够启动、建造、保存和读取。完成前运行全量 EditMode、PlayMode、无界面编译和 Windows 64 位构建。真实 Windows 10/11 独立程序冒烟继续保留为待验证门。
