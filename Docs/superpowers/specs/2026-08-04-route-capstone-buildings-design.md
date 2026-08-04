# 四路线终端建筑补齐设计

## 目标

完成正式路线图 F1 的“发电站、代谢炉、意识网络等缺失路线建筑”里程碑，让主 GDD 2.14.4 表中的四路线终端建筑都能够建造、运行、保存和显示占位表现。

对照当前工程：

- 科技“发电站”缺失；
- 修仙“聚灵阵”已经存在并能生产；
- 血肉“代谢炉”缺失；
- 灵能“意识网络”缺失。

因此本里程碑新增三个建筑，并统一验证四个终端建筑。不会重复创建聚灵阵。

完成本里程碑后按用户要求停止功能开发，不进入四路线说明统一、跨路线桥节点、军队、多城市或正式美术。

## 共同规则

主 GDD 规定四个终端建筑都在人口达到 `1000` 后开放。工程同时已有对应研究节点，因此统一采用双门槛：

- 人口至少 `1000`；
- 对应研究已完成；
- 建成且物流在线时才产生效果；
- 建筑施工、维修、损毁、物流、占位表现和存档继续复用 `BuildingRuntime`、`PlaceholderBuildingController`、`VisualSlot` 与 `BuildingSnapshot`。

四个建筑都是 `2×2`，不要求资源矿点或前置建筑。断开物流后保留建筑和生产进度，但暂停效果。

## 建筑定义

### 科技：发电站

```text
稳定 ID：technology.building.power-plant
名称：发电站
研究：core.research.thermal-engineering
人口：1000
成本：14 合金
施工：12 秒
耐久：320
```

运行效果：每 `6` 秒被动产出 `1` 能晶代理，代表 GDD 的能源币产出。

### 修仙：聚灵阵

```text
稳定 ID：cultivation.building.spirit-gathering-array
名称：聚灵阵
研究：core.research.spirit-gathering
人口：1000
成本：12 石材
施工：10 秒
耐久：260
```

沿用现有每 `6` 秒被动产出 `1` 能晶代理。阵法强化继续提供 `1.5×` 产出速度。

### 血肉：代谢炉

```text
稳定 ID：biological.building.metabolic-furnace
名称：代谢炉
研究：core.research.metabolic-acceleration
人口：1000
成本：12 骨钢
施工：12 秒
耐久：360
```

运行效果：每 `8` 秒消耗 `2` 生物质，产出 `1` 能晶代理，代表 GDD 的生物质转能源币。

输入不足时保留到期进度并显示缺少输入；输出仓满时暂停，沿用 `ProductionProcess` 规则。

### 灵能：意识网络

```text
稳定 ID：psionics.building.consciousness-network
名称：意识网络
研究：core.research.consciousness-network
人口：1000
成本：12 共振金属
施工：14 秒
耐久：300
```

运行效果：

- 每 `10` 秒被动产出 `1` 灵能增幅器代理，代表 GDD 的精神力结晶；
- 只有研究完成且至少一座已完成、物流在线的意识网络存在时，已发现求救信号才能使用现有 `[J]` 免费远程延迟救援；
- 意识网络断网或全部损毁后，远程救援立即停用。

## 代理资源边界

当前正式资源 ID 中没有“能源币、灵石、精神力结晶”。为避免在建筑里程碑中提前进入下一项“资源、建筑和研究说明统一”，本次继续使用已存在的可存档资源代理：

| GDD 产物 | 当前代理 |
|----------|----------|
| 能源币 | `core.resource.energy-crystal` |
| 灵石 | `core.resource.energy-crystal` |
| 精神力结晶 | `psionics.resource.amplifier` |

这只是单城市灰盒阶段的实现映射。文档和代码不得把代理名称改写成最终正式资源名称；未来资源统一里程碑可以新增正式资源并迁移配方。

## 生产与存档

`TechnologyProductionController` 新增：

- 发电站被动生产进程；
- 代谢炉单输入生产进程；
- 意识网络被动生产进程。

`CaptureProgress()` 在现有 `11` 个进度后追加三个值：

```text
11 powerPlant
12 metabolicFurnace
13 consciousnessNetwork
```

`RestoreProgress()` 按数组长度恢复。旧存档数组较短时三个新进度默认 `0`，因此不需要提高 schema；当前 schema 保持 `28`。

建筑本身由既有 `BuildingSnapshot.definitionId` 自动往返，新 ID 必须加入 `BuildingCatalog.All` 和 `BuildMenu`。

## 表现

`PlaceholderBuildingController` 已按建筑 ID 自动附加 `VisualSlot`，三个新建筑直接使用各自稳定 ID 作为正式替换槽：

```text
technology.building.power-plant
biological.building.metabolic-furnace
psionics.building.consciousness-network
```

科技占位使用冷白/蓝色，血肉使用绿色，灵能使用紫色。表现 Prefab 不持有生产、物流或远程救援状态。

## 测试与验收

1. EditMode 证明三个新 ID 唯一地存在于 `All` 和 `BuildMenu`，四个终端建筑都是 `2×2`、人口门槛 `1000` 且需要正确研究。
2. EditMode 证明人口 `999` 时锁定、`1000` 且研究完成时解锁。
3. EditMode 证明意识网络远程链接同时需要研究和至少一座物流在线建筑。
4. PlayMode 通过正式场景恢复四座已完成建筑，证明稳定 `VisualSlot` ID 和物流状态正确。
5. PlayMode 证明运行 `10` 秒后发电站、聚灵阵、代谢炉和意识网络产生对应代理资源，代谢炉消耗生物质。
6. PlayMode 证明生产进度捕获数组长度为 `14`，旧长度 `11` 的进度数组仍能恢复。
7. 建筑快照往返测试证明三个新建筑 ID 不会在加载时丢失。
8. 运行全部 EditMode、PlayMode、无界面编译和 Windows 64 位构建。
9. 保留真实 Windows 10/11 独立程序冒烟为候选发布门。

## 非目标

- 不新增能源币、灵石或精神力结晶正式资源 ID。
- 不改变现有库存容量或资源 UI。
- 不实现跨路线桥建筑。
- 不实现多城市意识网络共享。
- 不进入四路线说明统一。
- 不导入正式美术、音频或付费资产。
