# IDEA-0019 正式地图内容重排与世界表现尺度统一设计

状态：已批准，开发中

需求：`IDEA-0019`

Unity：`2022.3.62f1`

存档：schema `32` 不变；world generation/signature 升级 v2

## 1. 目标与非目标

本阶段同时解决四个已经由用户画面复验确认的问题：

1. `32×24` 逐格随机旧图嵌入 `64×48` 空荒地形成中央矩形；
2. seed `8128` 的 `156` 个资源节点与碎散 Traversal 淹没地图；
3. 建筑 XZ 随格尺寸变化而 Y 使用固定高度，炮塔又叠加两套完成体；
4. 世界 Marker 接近一整格宽，UI 图标缺少统一语义尺寸。

本阶段保持 `64×48`、seed `8128`、方格玩法、31 种资源目录、30 个建筑稳定 ID、玩法 footprint、生产/战斗规则和 schema `32`。不实现 `96×64`、迷雾、区块流送、新资源、新模型、复杂寻路或旧地图存档迁移。

## 2. 参考原则与版权边界

- Unciv：区域配额、出生影响区与资源间距；
- Factorio：starting patch、资源 spot、richness 与距离控制；
- Sovereign / ProceduralTerrainToolkit：低频地貌层、温湿/地形分类与连片边界；
- Unity 2022.3：以屏幕相对尺寸驱动 LOD，以逻辑像素与世界尺寸推导世界标记比例。

只提炼通用设计原则；不复制代码、地图、美术或数值，不下载或引入第三方依赖。

## 3. 地图所有权

```text
FormalWorldGenerationCatalog3D（v2 不可变配置）
  → FormalWorldGenerator3D（纯 C# 确定性生成）
  → GrayboxWorldLayout3D（默认世界 facade）
  → WorldMapModel（运行时格位/储量/揭示/采收真值）
  → ControlMap / Terrain / RuinsCliff / Marker（只读表现）
```

`WorldMapModel(int,int,WorldSeed)` 的旧通用随机构造器保持原语义，继续服务纯规则夹具；正式 v2 算法不能塞入该构造器。放置继续只经过 `BuildingPlacementEvaluation`，采矿兼容继续只经过 `BuildingResourceNodeCompatibilityRules`。

## 4. 正式 v2 配置

稳定身份：

- generation version：`2`；
- signature：`core.world.formal-3d.v2.64x48`；
- width/height：`64×48`；
- default seed：`8128`；
- start cell：`(10,9)`；
- start protection：切比雪夫半径 `4`，形成 `9×9` Open、无资源、可部署区；
- macro cell：`8`，逻辑模板 `8×6`；
- 边界扰动最大 `±2` 格，使用整数定点插值；
- 地形清理：一轮八邻域多数清理，平票保持原值。

目标地形占比：Wasteland `50%–60%`、Rocky `18%–26%`、Crystal `8%–15%`、Wetland `8%–15%`。非荒地必须形成宏观连片区；孤立单格特殊地形合计不超过验收门。

主区锚点：出生区 `(10,9)`、安全矿区约 `(16,15)`、结晶裂谷约 `(32,35)`、东南湿地区约 `(47,13)`、北部高地约 `(34,44)`、东部狭口约 `(52,25)`。seed 只允许在配置范围内稳定扰动非关键边界，不改变主区语义。

关键通路至少三格宽：出生至结晶裂谷存在南路和西北路两条独立路线；出生至东部狭口可达。保护 mask 在 Traversal 生成前建立，DeepWater/Cliff 不得覆盖。

Traversal 后置生成：

- DeepWater：1–2 个四邻域连续水体；
- Ruins：3–5 个半径受控的连续遗迹簇；
- Cliff：2–4 条连续折线或带状区域；
- 只使用稳定 frontier/折线栅格化，不用逐格独立 roll；
- 资源只落在 `Traversal.Open`。

## 5. 资源节点

总数精确为 `24`：

| 类型 | 数量 | 强制区域 |
|---|---:|---|
| 铁矿 | 8 | 安全区 2×240；裂谷 3×480；远端 3 |
| 石料 | 4 | 安全区 1×240；高地/遗迹边缘 3 |
| 能晶 | 4 | 结晶裂谷与南部晶地 |
| 水 | 4 | 湿地区可接近岸边 Open 格 |
| 生物质 | 4 | 湿地与遗迹边缘 Open 格 |

节点按 `ResourceSpotSpec` 生成：稳定 ID、资源 ID、区域中心、抖动半径、搜索半径、数量、地形亲和、节点间距、储量范围和 salt 全部来自正式配置。候选使用整数距离加 `WorldSeed.Sample` 稳定评分，贪心满足配额和间距；配置不足时显式失败，不能静默少放。

铁矿、石料和能晶节点必须有至少一个通过完整统一评估的 `2×2` 采矿站锚点；两个安全铁矿锚点不能重叠。水和生物质保持既有不可由采矿站直接提取的玩法边界。

GDD 中“裂谷采矿速度 +25%”当前没有区域速率真值字段，本阶段只实现裂谷位置与储量，不把未实现倍率写成已完成；如后续接入必须走独立生产配置变更。

## 6. 存档边界

`GrayboxWorldCitySaveAdapter3D` 从正式生成配置读取 version/signature。v2 capture、validate、restore 继续校验节点 ID、坐标、类型与剩余量。schema `32` DTO 不新增字段。

v1 旧地图节点和建筑绑定无法安全映射到 v2；尝试继续时通过既有中文路径返回“存档世界配置与当前正式世界不兼容”，且当前运行时保持原子不变。不得把 v1 签名伪装成 v2，也不得自动移动建筑或矿站绑定。

## 7. 建筑表现尺度

新增 `FormalWorldPresentationScaleProfile3D`，只拥有：

- Ground/Inner cell visual scale；
- footprint inset；
- 施工、完成、废墟、预览高度比例；
- 建筑类别高度和垂直强调上限；
- 炮塔 foundation/superstructure 边界；
- Marker 像素档、标签间距和选择框线宽。

它不得拥有 `BuildingDefinition`、放置合法性、库存、研究、节点、碰撞真值或存档字段。

全部建筑稳定 ID 映射到独立表现 archetype；不能直接复用建造菜单分类，因为路线分类不能表达建筑轮廓。正式 Profile 必须对 `BuildingCatalog.All` 30 项精确覆盖一次：

| Archetype | 建筑 |
|---|---|
| LowBarrier | 城墙 |
| ResidentialBlock | 住房 |
| StorageBlock | 仓库 |
| ExtractorRig | 采矿站 |
| ResearchHub | 研究站、意识网络 |
| DefenseFoundation | 机枪塔、激光塔、孢子塔 |
| Tower | 重型机枪塔、剑阵台、御剑台、酸液塔、心灵尖塔 |
| FieldArray | 聚灵阵、护盾发生器 |
| LargeEnclosure | 巨兽栏 |
| Processor | 冶炼厂、装配厂、发电站、灵火炉、菌落池、代谢炉、共振炉 |
| Workshop | 炼器坊、培育室、灵能工坊、自动维修机甲站、炼丹房、傀儡工坊 |

建议基线：

| 类别 | 横向占地比例 | 高度/格 |
|---|---:|---:|
| LowBarrier | 92% | `.38` |
| ResidentialBlock / StorageBlock | 86% / 90% | `.68` / `.58` |
| ExtractorRig / Processor / Workshop | 82% / 88% / 86% | `.86` / `.92` / `.78` |
| ResearchHub / Tower | 82% / 72% | `1.05` / `1.15` |
| DefenseFoundation | 74% | `.14` |
| FieldArray / LargeEnclosure | 84% / 88% | `.32` / `.72` |

所有轴从场地 cell size 推导；InnerCity 允许最多 `1.15` 的受控垂直强调，但单体高度不得超过 `.55` world unit。逻辑 footprint、选择和放置 collider 不从视觉 bounds 反推。

炮塔的 `GrayboxBuildingWorldView3D` 只显示不高于 `.12` 格的基础座；`GrayboxDefenseWorldView3D` 拥有武器上层。两者合成的 1×1 水平边界不超过 `.82×.82`，且不再叠加完整通用建筑。

## 8. Marker 与 UI 图标

Marker LOD 使用“一个地面格投影到屏幕的相对高度”而不是裸 orthographic size：

- Near：单格高度 ≥ `4.5%`，框 `28–36px`、名称+储量；
- Mid：`2.7%–4.5%`，框 `20–28px`、仅储量；
- Far：< `2.7%`，图标 `12–18px`、无文字或受控聚合。

普通标签屏幕矩形至少间隔 `6px`；默认 `1920×1080 / ortho 13` 不显示全图名称。悬停、选中与采矿指引可强制 Near，并抑制附近低优先级标签。Marker 稳定 ID、对象身份、资源量和采矿合法性不变。

扩展 `FormalUiLayoutProfile3D` 的图标语义 token：Inline `16`、Compact `20`、Row `24`、Slot `32`、Node `48`、Hero `64`。View 只选择语义级，不再写散落尺寸。建造栏保持 `620×54` 及反馈位置；目录 Hero 图标不超过卡片行高 `65%`，行内图标不超过行高 `60%`。

## 9. TDD 与完成门

第一批 RED：正式 v2 全图覆盖、24 节点配额/安全储量、宏区连通、出生保护、双通路、真实采矿锚点、v2 存档与 v1 原子拒绝。第二批 RED：全部建筑类别、Ground/Inner 同比、炮塔单一基础座、Marker 实际像素/避让、UI icon token 与真实输入。

实现后依次运行：地图/放置/存档/比例聚焦 EditMode → 真实输入 PlayMode → `TerrainAssetDeep` → 日常完整 EditMode → 完整 PlayMode → 项目质量门 → Windows Release 3D → Windows Development 3D → macOS universal 3D → 固定 GUI 证据 → 官方文档生成/校验/Analyze/RecordVerification。

自动化、开发机截图和构建只支持“已实现待验证”；用户视觉试玩和真实 Windows 10/11 GPU、显存、内存结论不能自动宣称完成。
