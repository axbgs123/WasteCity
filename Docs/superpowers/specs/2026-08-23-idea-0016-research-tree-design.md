# IDEA-0016：43 节点正式科技树设计规格

> 需求 ID：`IDEA-0016`<br>
> 适用版本：Unity `2022.3.62f1`，正式 3D 入口，schema `31`<br>
> 权威依据：GDD A4.9、A16.4、B 卷 2.15；`IDEA-0005`、`IDEA-0011`、`IDEA-0015`、`IDEA-0016`<br>
> 状态边界：本文件是可执行设计，不代表代码、图标、自动化、构建或人工试玩已经完成。

## 1. 目标与非目标

本阶段把现有六节点 3D release profile 和长期 `ResearchCatalog` 收敛成一份正式目录：`1` 个通用根、四条路线各 `9` 个节点、`6` 个跨路线桥，共 `43` 个可见节点。玩家可以同时发展科技、修仙、血肉和灵能，不存在永久选路；桥节点必须同时满足两侧前置。

本规格同时固定：逐节点数值和语义、自下向上布局、搜索/筛选/定位、真实输入优先级、研究站与暂停、schema `31` 恢复以及性能门。

本阶段不新增敌人、炮塔、弹丸、跨城市运输、多城市共享运行时或 schema 字段。尚无真实消费者的战斗、单位、多城市和融合建筑效果必须显示为“仅预览”，不能收取研究成本后只改变一段文案。

## 2. 审计结论与冲突处理

### 2.1 当前双真值

- `DemoResearchCatalog` 持有六节点 3D 配置、发布状态和六个定义对象，`DemoResearchRuntime`、正式 3D 科技树与 schema `31` 恢复都直接依赖它。
- `ResearchCatalog` 另有长期路线目录。它当前含 B 卷的 `42` 个路线/桥节点，再额外加入 `core.research.legacy-analysis`，总数虽然也是 `43`，但没有通用根 `core.research.scrap-processing`。
- 两个目录对同一 ID 存在显示名、成本、时长、前置和对象引用差异；例如 `core.research.precision-assembly` 分别显示“弹药装配”和“精密装配”。UI、运行时和开发修改器因此可能读取不同真值。
- `ResearchDefinition` 当前把 Tier 夹在 `1–3`，没有通用根层、确定目录顺序、图标 ID、效果引用和正式/预览效果边界。

### 2.2 正式决策

1. `FormalResearchCatalog`（实现时可保留公开类型名 `ResearchCatalog`，但只能有这一份定义集合）成为唯一正式目录，精确包含本文件第 4 节的 `43` 个节点。
2. `core.research.scrap-processing` 是唯一通用根，初始完成；B 卷四路线各 `9` 节点和 `6` 桥全部保留原稳定 ID。
3. 六节点中与长期目录重合的 ID 继续沿用：
   - `core.research.automated-machinery`
   - `core.research.precision-assembly`
   - `core.research.automated-defense`
4. `core.research.reinforced-structures` 与 `core.research.legacy-analysis` 不挤入 43 节点，也不得静默改义成“合金装甲”或其他节点。它们进入只读的 `RetiredDemoResearchProfile` 兼容表：不显示、不允许新开研究、不计入 43 节点完成率；schema `31` 中已有的完成 ID 原样往返，已有活动 ID 按“已退役内容”暂停并保留剩余时间，不自动完成、映射、退款或丢弃。
5. “加固结构”和“遗产解析”的六节点数值继续作为 `IDEA-0011` 历史证据，不再是当前正式树节点。升阶仍要求的“遗产解析”必须在未来单独产品变更中重新安置；本阶段不得把任一新节点暗中当成升阶条件。
6. A16.4 已有可玩数值保持兼容：基础冶金 `铁矿 10 / 20 秒`，精密装配沿用六节点“弹药装配”的 `合金 10 / 30 秒`，自动防御沿用 `合金 12 + 生物质 10 / 35 秒`。正式显示统一采用 B 卷长期名称“精密装配”“自动防御架构”，不改稳定 ID及既有效果。

## 3. 正式数据合同

每个正式节点必须由一个不可变定义持有以下字段，UI 不得补写默认值或另建连接表：

```text
ResearchDefinition
  Id                    稳定研究 ID
  NameKey               本地化键；中文回退名仅供缺失本地化时显示
  Route                 Common / Technology / Cultivation / Biological / Psionics / Bridge
  Tier                  0 / 1 / 2 / 3；桥节点仍属 T3
  LayoutRow             0..4；桥节点布局行为 4
  CatalogOrder          全目录唯一 0..42
  RequiredResearchIds   0..2 个稳定 ID；数组中全部完成才满足
  Costs                 资源稳定 ID + 正整数数组
  DurationSeconds       非根节点大于 0
  ReleaseState          InitiallyCompleted / Researchable / PreviewOnly / RetiredCompatibility
  EffectReferences      0..n 个类型化稳定引用
  IconId                稳定二维资产 ID
  BriefKey              一句话背景简介本地化键
```

效果引用前缀固定为 `building:`、`recipe:`、`rule:`、`progression:`。实现必须有集中式效果目录检查引用是否存在；`PreviewOnly` 可以引用尚未接入的正式意图，但 UI 必须显示“本阶段仅预览”，研究命令必须拒绝启动。

本表成本只使用当前已有的 15 个稳定资源，避免科技树先于资源扩展规格制造第二批资源 ID：

| 简称 | 稳定 ID | 简称 | 稳定 ID |
|---|---|---|---|
| 铁矿 | `core.resource.iron` | 能晶 | `core.resource.energy-crystal` |
| 石料 | `core.resource.stone` | 生物质 | `core.resource.biomass` |
| 水 | `core.resource.water` | 合金 | `technology.resource.alloy` |
| 弹药 | `technology.resource.ammunition` | 灵铁 | `cultivation.resource.spirit-iron` |
| 飞剑 | `cultivation.resource.flying-sword` | 骨钢 | `biological.resource.bone-steel` |
| 生物质浓缩液 | `biological.resource.biomass-concentrate` | 生物武器 | `biological.resource.weapon` |
| 共振金属 | `psionics.resource.resonance-metal` | 灵能增幅器 | `psionics.resource.amplifier` |
| 丹药 | `cultivation.resource.elixir` |  |  |

## 4. 43 节点正式目录

### 4.1 通用根（1）

| 顺序 | 名称 / ID | 层级 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 0 | 废料加工<br>`core.research.scrap-processing` | 通用 T0 / 布局 0 | 无 | 无 / 初始完成 | `building:core.building.mining-station`、`building:core.building.research-station`、`building:core.building.housing`、`building:core.building.warehouse`、`building:core.building.wall`；**真实可用** | `art.icon.research.scrap-processing` | 城市从废墟中整理出可复用结构、基础工具与最初工业标准。 |

### 4.2 科技路线（9）

| 顺序 | 名称 / ID | 层级 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 1 | 基础冶金<br>`core.research.automated-machinery` | 科技 T1 / 布局 1 | 废料加工 | 铁矿×10 / 20 秒 | `building:core.building.smelter`、`recipe:core.production.smelt-alloy`、`recipe:core.crafting.field-alloy`；**真实可用** | `art.icon.research.automated-machinery` | 把不稳定废铁重新纳入可控温度和标准合金流程。 |
| 5 | 精密装配<br>`core.research.precision-assembly` | 科技 T2 / 布局 2 | 基础冶金 | 合金×10 / 30 秒 | `building:core.building.assembler`、`recipe:core.production.assemble-ammunition`、`recipe:core.crafting.field-ammunition`；**真实可用** | `art.icon.research.precision-assembly` | 统一公差、夹具与装配顺序，让弹药和机械件可以稳定量产。 |
| 6 | 自动防御架构<br>`core.research.automated-defense` | 科技 T2 / 布局 2 | 基础冶金 | 合金×12 + 生物质×10 / 35 秒 | `building:core.building.machine-gun-turret`；**真实可用** | `art.icon.research.automated-defense` | 以传感、供弹和射控回路组成无需持续人工瞄准的防线。 |
| 7 | 热能工程<br>`core.research.thermal-engineering` | 科技 T2 / 布局 2 | 基础冶金 | 铁矿×16 + 合金×8 / 40 秒 | `building:technology.building.power-plant`、`recipe:technology.production.energy-cell`；**发电站代理生产已存在，能量电池在新配方接线后可用** | `art.icon.research.thermal-engineering` | 回收炉温与废热，为重型工业建立稳定的热力循环。 |
| 8 | 弹道学<br>`core.research.ballistics` | 科技 T2 / 布局 2 | 基础冶金 | 铁矿×12 + 合金×10 / 40 秒 | `rule:core.effect.ballistics`；**仅预览**（战斗数值扩展不在本阶段） | `art.icon.research.ballistics` | 通过轨迹、膛压与弹体结构校正远程火力。 |
| 21 | 合金装甲<br>`core.research.alloy-armor` | 科技 T3 / 布局 3 | 精密装配 | 合金×24 + 石料×8 / 60 秒 | `rule:core.effect.alloy-armor`、`building:core.building.heavy-machine-gun-turret`；**仅预览** | `art.icon.research.alloy-armor` | 以层叠合金和缓冲结构把城市外壳改造成承压装甲。 |
| 22 | 无人系统<br>`core.research.unmanned-systems` | 科技 T3 / 布局 3 | 自动防御架构 | 合金×24 + 能晶×10 / 60 秒 | `building:core.building.automated-repair-bay`、`rule:core.effect.scout-drone`；**仅预览**（单位和维修接线不在本阶段） | `art.icon.research.unmanned-systems` | 将感知、路径和维修指令封装进可替代人员的自动平台。 |
| 23 | 轨道补给<br>`core.research.orbital-supply` | 科技 T3 / 布局 3 | 热能工程 | 合金×30 + 弹药×15 / 75 秒 | `rule:core.effect.logistics-range-24`；**真实可用**（只扩大既有城市物流，不实现跨城市运输） | `art.icon.research.orbital-supply` | 用高空测绘和远距调度把城市补给范围延伸到更远工位。 |
| 24 | 能量武器<br>`core.research.energy-weapons` | 科技 T3 / 布局 3 | 弹道学 | 合金×20 + 能晶×20 / 75 秒 | `building:core.building.laser-tower`、`rule:core.effect.technology-overload`；**仅预览** | `art.icon.research.energy-weapons` | 将高密度能量约束为可定向释放的穿甲束流。 |

### 4.3 修仙路线（9）

| 顺序 | 名称 / ID | 层级 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 2 | 灵火淬炼<br>`core.research.spirit-sensing` | 修仙 T1 / 布局 1 | 废料加工 | 能晶×8 + 铁矿×4 / 20 秒 | `building:cultivation.building.spirit-fire-furnace`、`recipe:cultivation.production.refine-spirit-iron`；**生产接线后真实可用** | `art.icon.research.spirit-sensing` | 从能晶共鸣中稳定灵火，使普通金属获得可控灵性。 |
| 9 | 炼器基础<br>`core.research.artifact-crafting` | 修仙 T2 / 布局 2 | 灵火淬炼 | 灵铁×12 + 能晶×8 / 40 秒 | `building:cultivation.building.artifact-workshop`、`recipe:cultivation.production.flying-sword`；**生产接线后真实可用** | `art.icon.research.artifact-crafting` | 以阵纹和灵铁统一器胚结构，建立法器批量制造方法。 |
| 10 | 剑阵初解<br>`core.research.sword-array` | 修仙 T2 / 布局 2 | 灵火淬炼 | 灵铁×14 + 飞剑×2 / 40 秒 | `building:cultivation.building.sword-array-tower`；**仅预览** | `art.icon.research.sword-array` | 让多柄飞剑按固定阵位循环，形成持续覆盖的杀伤区域。 |
| 11 | 聚灵术<br>`core.research.spirit-gathering` | 修仙 T2 / 布局 2 | 灵火淬炼 | 能晶×16 + 石料×8 / 40 秒 | `building:cultivation.building.spirit-gathering-array`、`recipe:cultivation.production.gather-spirit-stone`；**生产接线后真实可用** | `art.icon.research.spirit-gathering` | 以地脉节点和阵基汇聚环境中稀薄而游离的灵性。 |
| 12 | 符箓入门<br>`core.research.talisman-basics` | 修仙 T2 / 布局 2 | 灵火淬炼 | 灵铁×8 + 石料×12 / 40 秒 | `rule:core.effect.wall-talisman`；**仅预览** | `art.icon.research.talisman-basics` | 将短时防护规律压缩进可附着于城墙的基础符纹。 |
| 25 | 御剑术<br>`core.research.sword-riding` | 修仙 T3 / 布局 3 | 剑阵初解 | 灵铁×24 + 飞剑×8 / 60 秒 | `building:cultivation.building.sword-riding-platform`、`rule:core.effect.flying-sword-range`；**仅预览** | `art.icon.research.sword-riding` | 用连续神识校正高速飞剑，使其脱离固定阵台远距行动。 |
| 26 | 炼丹术<br>`core.research.alchemy` | 修仙 T3 / 布局 3 | 炼器基础 | 灵铁×16 + 生物质×20 / 60 秒 | `building:cultivation.building.alchemy-chamber`、`recipe:cultivation.production.elixir`；**生产接线后真实可用** | `art.icon.research.alchemy` | 以灵火提纯活性物质，把短暂药性封存为可携带丹剂。 |
| 27 | 阵法强化<br>`core.research.formation-reinforcement` | 修仙 T3 / 布局 3 | 聚灵术 | 能晶×24 + 石料×16 / 60 秒 | `rule:cultivation.effect.logistics-range-12`、`rule:cultivation.effect.spirit-output-150-percent`；**真实可用** | `art.icon.research.formation-reinforcement` | 扩展阵基回路，让聚灵与近域调度共享更稳定的能量边界。 |
| 28 | 傀儡术<br>`core.research.puppetry` | 修仙 T3 / 布局 3 | 符箓入门 | 灵铁×20 + 合金×12 / 60 秒 | `building:cultivation.building.puppet-workshop`、`rule:cultivation.effect.puppet-unit`；**仅预览** | `art.icon.research.puppetry` | 以符纹驱动机械关节，制造不依赖血肉疲劳的作战傀儡。 |

### 4.4 血肉路线（9）

| 顺序 | 名称 / ID | 层级 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 3 | 菌落培养<br>`core.research.adaptive-tissue` | 血肉 T1 / 布局 1 | 废料加工 | 生物质×10 + 水×5 / 20 秒 | `building:biological.building.colony-pool`、`recipe:biological.production.biomass-concentrate`、`recipe:biological.production.active-biomass`、`recipe:biological.production.bone-steel`；**生产接线后真实可用** | `art.icon.research.adaptive-tissue` | 在受控营养液中培养能适应污染环境的基础组织群。 |
| 13 | 生物培育<br>`core.research.bio-cultivation` | 血肉 T2 / 布局 2 | 菌落培养 | 骨钢×10 + 生物质浓缩液×10 / 40 秒 | `building:biological.building.breeding-chamber`、`recipe:biological.production.weapon`；**生产接线后真实可用** | `art.icon.research.bio-cultivation` | 将骨架和活体组织按用途定向生长为可控生物器件。 |
| 14 | 孢子散布<br>`core.research.spore-dispersal` | 血肉 T2 / 布局 2 | 菌落培养 | 生物质浓缩液×16 + 水×8 / 40 秒 | `building:biological.building.spore-tower`；**仅预览** | `art.icon.research.spore-dispersal` | 调节孢子的附着与休眠周期，使感染能在目标区域持续扩散。 |
| 15 | 代谢加速<br>`core.research.metabolic-acceleration` | 血肉 T2 / 布局 2 | 菌落培养 | 生物质×16 + 骨钢×8 / 40 秒 | `building:biological.building.metabolic-furnace`、`rule:biological.effect.corpse-recovery-150-percent`；**代谢炉现有能晶代理生产真实，掉落加成仅预览，不伪造 ResourceRecipe ID** | `art.icon.research.metabolic-acceleration` | 迫使培养体快速分解废弃组织，将其转成可调度的高能产物。 |
| 16 | 甲壳增生<br>`core.research.carapace-growth` | 血肉 T2 / 布局 2 | 菌落培养 | 骨钢×12 + 生物质×14 / 40 秒 | `rule:biological.effect.wall-carapace-regeneration`；**真实可用** | `art.icon.research.carapace-growth` | 让外墙表面附着可补生的甲壳层，以生物质交换缓慢修复。 |
| 29 | 巨兽培育<br>`core.research.behemoth-breeding` | 血肉 T3 / 布局 3 | 生物培育 | 骨钢×24 + 生物质浓缩液×18 / 60 秒 | `building:biological.building.behemoth-pen`、`rule:biological.effect.behemoth-unit`；**仅预览** | `art.icon.research.behemoth-breeding` | 通过长期定向选择培育能承担攻城和运输负荷的大型个体。 |
| 30 | 酸液喷吐<br>`core.research.acid-spit` | 血肉 T3 / 布局 3 | 孢子散布 | 生物质浓缩液×20 + 生物武器×10 / 60 秒 | `building:biological.building.acid-tower`、`rule:biological.effect.armor-corrosion`；**仅预览** | `art.icon.research.acid-spit` | 重构腺体压力和酸液配方，使喷吐物针对装甲持续腐蚀。 |
| 31 | 组织再生<br>`core.research.tissue-regeneration` | 血肉 T3 / 布局 3 | 代谢加速 | 生物质浓缩液×24 + 生物质×20 / 60 秒 | `rule:biological.effect.building-and-unit-regeneration`；**真实可用** | `art.icon.research.tissue-regeneration` | 让受损组织持续复制并替换坏死区域，形成低速自愈循环。 |
| 32 | 基因剪接<br>`core.research.gene-splicing` | 血肉 T3 / 布局 3 | 甲壳增生 | 骨钢×18 + 生物质浓缩液×18 / 60 秒 | `rule:biological.effect.leader-temporary-trait`；**仅预览** | `art.icon.research.gene-splicing` | 组合筛选后的基因片段，为活体赋予短期而强烈的适应特征。 |

### 4.5 灵能路线（9）

| 顺序 | 名称 / ID | 层级 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 4 | 意识共振<br>`core.research.mind-resonance` | 灵能 T1 / 布局 1 | 废料加工 | 能晶×8 + 水×6 / 20 秒 | `building:psionics.building.resonance-furnace`、`recipe:psionics.production.resonance-metal`；**生产接线后真实可用** | `art.icon.research.mind-resonance` | 用稳定频率捕捉集体思维留下的微弱共鸣并写入金属。 |
| 17 | 灵能工坊<br>`core.research.psionic-workshop` | 灵能 T2 / 布局 2 | 意识共振 | 共振金属×12 + 能晶×8 / 40 秒 | `building:psionics.building.workshop`、`recipe:psionics.production.amplifier`；**生产接线后真实可用** | `art.icon.research.psionic-workshop` | 将共振结构加工为能放大、约束和转发精神信号的器件。 |
| 18 | 心灵尖塔<br>`core.research.mind-spire` | 灵能 T2 / 布局 2 | 意识共振 | 共振金属×14 + 灵能增幅器×4 / 40 秒 | `building:psionics.building.mind-spire`；**仅预览** | `art.icon.research.mind-spire` | 以高塔阵列集中精神灼烧，绕开目标的物理防护。 |
| 19 | 意识网络<br>`core.research.consciousness-network` | 灵能 T2 / 布局 2 | 意识共振 | 共振金属×16 + 水×10 / 40 秒 | `building:psionics.building.consciousness-network`、`recipe:psionics.production.consciousness-shard`；**生产接线后真实可用** | `art.icon.research.consciousness-network` | 把分散思维节点接入同一低延迟网络，沉淀可用精神资源。 |
| 20 | 思维加速<br>`core.research.thought-acceleration` | 灵能 T2 / 布局 2 | 意识共振 | 灵能增幅器×8 + 水×14 / 40 秒 | `rule:psionics.effect.research-speed-125-percent`；**真实可用** | `art.icon.research.thought-acceleration` | 通过并行认知分工压缩推演时间，让研究站更快完成同量工作。 |
| 33 | 心灵护盾<br>`core.research.mind-shield` | 灵能 T3 / 布局 3 | 灵能工坊 | 共振金属×24 + 灵能增幅器×12 / 60 秒 | `building:psionics.building.shield-generator`、`rule:psionics.effect.city-damage-shield`；**仅预览** | `art.icon.research.mind-shield` | 以同步意识维持范围屏障，削弱进入城市范围的攻击。 |
| 34 | 精神操控<br>`core.research.mind-control` | 灵能 T3 / 布局 3 | 心灵尖塔 | 灵能增幅器×20 + 水×20 / 60 秒 | `rule:psionics.effect.control-normal-enemy`；**仅预览** | `art.icon.research.mind-control` | 干扰低阶目标的判断回路，使其短暂服从外来指令。 |
| 35 | 预知感应<br>`core.research.precognitive-sense` | 灵能 T3 / 布局 3 | 意识网络 | 灵能增幅器×18 + 能晶×20 / 60 秒 | `rule:psionics.effect.warning-time-150-percent`；**真实可用** | `art.icon.research.precognitive-sense` | 从杂乱精神噪声中提取即将发生的敌意征兆，提前形成预警。 |
| 36 | 集体意识<br>`core.research.collective-consciousness` | 灵能 T3 / 布局 3 | 思维加速 | 灵能增幅器×24 + 水×24 / 60 秒 | `recipe:psionics.production.psionic-crystal`、`rule:psionics.effect.multi-city-shared-progress-20-percent`；**仅预览**（多城市不在本阶段） | `art.icon.research.collective-consciousness` | 让多座城市共享部分认知成果，但仍保留各自独立决策。 |

### 4.6 跨路线桥（6）

桥节点 `Tier=3`、`LayoutRow=4`，全部为 `PreviewOnly`。它们显示双路线前置、成本和未来效果，但本阶段不收取成本、不创建融合建筑。

| 顺序 | 名称 / ID | 路线 | 全部前置 | 成本 / 时间 | 效果引用与本轮状态 | 图标 ID | 背景简介 |
|---:|---|---|---|---|---|---|---|
| 37 | 灵能机甲<br>`core.research.bridge.psionic-mech` | 科技×灵能 | 精密装配 + 灵能工坊 | 合金×30 + 灵能增幅器×20 / 90 秒 | `building:bridge.building.psionic-mech-factory`；**仅预览** | `art.icon.research.bridge.psionic-mech` | 将机械骨架与灵能护盾耦合，形成兼具实体火力和精神防护的机体。 |
| 38 | 高周波飞剑<br>`core.research.bridge.high-frequency-sword` | 修仙×科技 | 炼器基础 + 精密装配 | 飞剑×12 + 合金×30 / 90 秒 | `building:bridge.building.high-frequency-sword-forge`；**仅预览** | `art.icon.research.bridge.high-frequency-sword` | 以精密振荡结构强化灵铁飞剑，使刃口在高速共振中切割装甲。 |
| 39 | 生物机库<br>`core.research.bridge.bio-hangar` | 血肉×科技 | 生物培育 + 精密装配 | 骨钢×25 + 合金×25 / 90 秒 | `building:bridge.building.bio-hangar`；**仅预览** | `art.icon.research.bridge.bio-hangar` | 让活体肌束和机械承力架共同生长，形成可维护的半机械巨兽平台。 |
| 40 | 灵植培育<br>`core.research.bridge.spirit-plant` | 修仙×血肉 | 炼器基础 + 生物培育 | 灵铁×20 + 生物质浓缩液×20 / 90 秒 | `building:bridge.building.spirit-plant-garden`、`recipe:fusion.production.spirit-plant-extract`；**仅预览** | `art.icon.research.bridge.spirit-plant` | 用灵性阵纹引导活体组织生长，稳定培育可炼制的灵植。 |
| 41 | 精神脉冲武器<br>`core.research.bridge.psionic-pulse` | 灵能×科技 | 灵能工坊 + 精密装配 | 灵能增幅器×20 + 弹药×30 / 90 秒 | `building:bridge.building.emp-tower`；**仅预览** | `art.icon.research.bridge.psionic-pulse` | 把精神共振编码成定向脉冲，扰乱机械目标的控制回路。 |
| 42 | 血肉灵丹<br>`core.research.bridge.flesh-elixir` | 血肉×修仙 | 生物培育 + 炼器基础 | 生物质浓缩液×25 + 能晶×25 / 90 秒 | `recipe:fusion.production.flesh-elixir`、`rule:bridge.effect.elixir-triple-with-mutation-risk`；**仅预览** | `art.icon.research.bridge.flesh-elixir` | 将高活性组织封入丹体，换取三倍效力并承担可见的突变风险。 |

## 5. 研究状态与命令

节点状态由正式目录、研究模型、城市库存和研究站快照共同投影，优先级固定为：

1. `MissingContent`：存档引用的活动 ID 无定义；
2. `Completed`：已完成；
3. `ActiveRunning` / `ActivePaused`：当前活动项，并显示暂停原因；
4. `PreviewOnly`：目录明确只预览；
5. `MissingPrerequisite`：逐项列出所有未完成前置；
6. `InsufficientResources`：逐项列出拥有、需要和缺少；
7. `MissingResearchStation`：其它条件满足但无合格研究站；
8. `Researchable`。

同一节点不只靠颜色表达状态：图标蒙版、边框形状、短标签和可访问文本必须同时变化。桥节点的两个前置分别显示完成/未完成，不得合并成一个“前置不足”。

研究命令遵守既有原子规则：只允许一个活动任务；开始时从城市仓储一次性扣除全部成本；取消返还各成本的 `80%` 并清零进度，完整退款无法原子接收时拒绝取消；没有研究队列。`PreviewOnly`、已完成、活动中、前置不足、资源不足或缺少合格研究站都必须返回稳定原因码和中文说明。

## 6. 研究站、倍率与暂停

- 合格研究站必须同时满足：建筑已完成、归玩家所有、未进入撤离锁定。位置在内城或地面、城市是否处于转换中，不额外改变资格。
- 堡垒态规则速度 `1.0`；移动、展开中和收起中均为 `0.5`。完成“思维加速”后在上述倍率之后再乘 `1.25`。
- 失去最后一座合格研究站时，活动研究进入 `ActivePaused / NoEligibleResearchStation`；不取消、不退款、不清零。恢复资格后从原剩余规则秒继续。
- 战术暂停或系统菜单暂停冻结研究时间。暂停只冻结规则时间，不能改变节点完成、成本或研究站资格；解除暂停后不得补算现实时间。
- 同时存在多个暂停原因时，UI 主原因顺序为：缺失内容 → 系统暂停 → 战术暂停 → 无合格研究站；详情可列出全部原因。

## 7. 自下向上科技树界面

### 7.1 确定布局

图空间只从目录字段生成，不按当前完成状态重新排版：

- 根节点：`LayoutRow 0`，`Y=0`；
- 四路线 T1：`LayoutRow 1`，`Y=280`；
- T2：`LayoutRow 2`，`Y=600`；
- T3：`LayoutRow 3`，`Y=920`；
- 桥节点：`LayoutRow 4`，`Y=1260`。

四路线固定从左到右为科技、修仙、血肉、灵能，路线中心 `X=-1200/-400/400/1200`；T2/T3 的四个分支在路线中心使用 `-270/-90/+90/+270` 偏移。节点 `CatalogOrder` 决定同层从左到右的顺序，不得依赖哈希表、完成时间或本地化字符串排序。桥节点按 37–42 固定位置，并用两条来源路线颜色的独立连线进入同一节点。

节点建议逻辑尺寸 `220×112`，图标实际显示 `64×64`，使用用户确认的“工业手绘科技徽记”透明图；锁定、完成、进行中、选择和预览状态由 UI 叠加，不烘焙进基础图标。

精确边数为 `48`：根到四路线 `4`，T1 到 T2 `16`，T2 到 T3 `16`，六桥双前置 `12`。连线箭头朝上，先绘线后绘节点；正常线宽 `3`，高亮路径 `5`，缩放后不得低于屏幕 `1px`。交叉线使用小圆弧跨线，不改变前置语义。

### 7.2 拖动与缩放

- 左键从空白图面拖动；中键可从任意非滚动条位置拖动。按下节点只选择节点，不启动拖动；拖动超过 `6px` 后本次释放不得触发选择。
- 滚轮以指针所在图空间位置为缩放中心；范围 `0.55–1.45`，每刻度改变 `0.10`，缩放不重建节点或连线。
- 视图允许留出半屏边缘，但不能把全部 43 节点永久拖出可视区；窗口尺寸变化后重新夹取视图，不改变目录坐标。
- `Home`/“显示全树”把完整边界适配到视口；这只是视图命令，不写入存档。

### 7.3 搜索与筛选

- 搜索匹配中文名、稳定 ID 和简介；忽略首尾空白，英文不区分大小写。输入防抖 `100ms`，相同规范化查询不重复计算。
- 路线筛选支持全部、通用、科技、修仙、血肉、灵能、桥；状态筛选支持全部、可研究、进行中、已完成、锁定、仅预览。
- 筛选结果保留匹配节点及其全部前置作为低亮“路径上下文”，其余节点和连线隐藏；节点坐标不回流、不重排。搜索匹配项使用描边和结果序号，不只改变颜色。
- 清空搜索或筛选恢复原浏览中心和缩放，不更改选中节点、研究状态或目录。

### 7.4 打开定位与手动浏览

每次 `T` 打开时计算“最新可研究范围”：

1. 从 `Researchable` 节点中取最大 `Tier`；
2. 该层全部可研究节点形成定位范围；
3. 主焦点取其中 `CatalogOrder` 最小者，视口居中到这一组节点的包围盒；
4. 若没有可研究节点，则定位最大 `Tier`、再取最大 `CatalogOrder` 的已完成节点；仍没有时定位通用根。

存在活动研究时显示“定位进行中”按钮；点击才居中活动节点。玩家在本次打开后发生任何拖动、缩放或节点选择，即标记 `UserNavigated=true`，后续研究 revision 不得自动抢回视图。关闭再打开会重新执行上述默认定位。搜索结果另有“上一个/下一个”，顺序严格按 `CatalogOrder`。

## 8. 输入焦点与防穿透

- 科技树为模态面板。打开时停止向城市移动、领袖移动、建造、世界选择、世界缩放、`0` 修改器和快捷栏派发输入；战术暂停按既有规则仍可用。
- 面板、节点、滚动条、搜索框、筛选和提示层必须正确拦截指针；世界射线不得同时收到点击、滚轮或拖动。
- 搜索框获得焦点后，字符键（包括 `T`、`B`、`E`、`F`、`R`、数字键 `0`）只进入文本，不触发任何游戏命令。`Esc` 第一次清除输入焦点，第二次关闭科技树；无文本焦点时 `T` 关闭面板。
- IME 组合输入期间不得提交搜索、切换面板或触发世界命令；确认组合文本后才更新查询 revision。
- 关闭面板或场景卸载时注销全部监听器并释放焦点，不能留下吞键层。

## 9. schema 31 保存与恢复

schema 保持 `31`，仍只保存已完成稳定 ID 集合、活动稳定 ID和活动项剩余规则秒。目录顺序、前置、成本、时长、效果、研究站资格、城市倍率、搜索、筛选、选中、视图中心和缩放全部重算或重置，不增加字段。

恢复规则：

1. 新游戏自动完成 `core.research.scrap-processing`。
2. 旧 schema `31` 存档若没有通用根，恢复后按“初始完成根”不变量补齐；下一次正常保存时写入该 ID。这是由正式目录派生的兼容默认，不是新 schema 字段。
3. 三个重合旧 ID按同一稳定 ID恢复为正式节点，不复制完成项、不重复扣成本。
4. `core.research.reinforced-structures` 和 `core.research.legacy-analysis` 的已完成 ID原样保存在兼容集合中，但不计入 `CompletedCount/43`，不授予新节点效果；不得自动映射。
5. 其它未知完成 ID继续按 `IDEA-0015` 作为孤立 ID 往返且不授予效果。未知或已退役活动 ID暂停为缺失/退役内容，保留原 ID与剩余时间，不自动完成、不退款。
6. 活动正式节点恢复后重新计算研究站资格、城市模式倍率、暂停原因和 UI；恢复 API不能重新扣除研究成本。
7. 若实现发现路线接触、发现或某个新效果必须保存当前字段无法表达的权威状态，应停止并单独申请 schema 升级，不得挪用完成 ID或 UI 字段编码。

## 10. `DemoResearchCatalog` 迁移步骤

1. 先以失败测试冻结两份目录当前差异、六个 release profile ID及 schema `31` 往返。
2. 建立唯一正式 43 节点目录和按 ID 的只读索引；目录构造时验证数量、顺序、引用、DAG、成本、图标和效果引用。
3. `DemoResearchCatalog` 降级为历史兼容 facade：三个重合节点和通用根转发正式定义；两个退役节点只从 `RetiredDemoResearchProfile` 返回兼容元数据。正式运行时、正式 UI、开发修改器和新测试不得再枚举 `DemoResearchCatalog.All`。
4. `DemoResearchRuntime` 改为使用注入的正式目录解析器和 release state，不再用 `ReferenceEquals` 判断“是否属于目录”；归属以稳定 ID、正式定义存在及 `ReleaseState` 为准。
5. schema `31` restore 使用“正式目录 + 退役/未知保留策略”的唯一解析入口。退役完成 ID不能被丢弃，退役活动 ID不能被伪装成可继续研究的正式节点。
6. `RouteContentDisplayCatalog` 只消费正式目录的显示快照，不再提供研究名称、成本、效果或前置的第二真值。
7. 所有原来依赖六节点顺序的 UI、测试和端到端步骤改按稳定 ID定位；不得依赖数组下标。
8. 迁移完成后保留兼容 facade 至至少一个正式存档兼容周期；删除它须另有迁移证明和批准。

## 11. 性能与稳定性门

- 正式目录在启动时构造一次不可变数组和字典；UI 打开、搜索、筛选或每帧更新不得重建目录。
- 第一次打开最多实例化 `43` 个节点和 `48` 条连线；之后关闭/重开、搜索、筛选、定位、拖动和缩放只复用对象。连续 `100` 次开关和 `100` 次筛选后，节点、连线、GameObject、材质和监听器数量必须回到同一稳定值。
- 预热后静止打开 `300` 帧，以及连续 `300` 次拖动/缩放采样，科技树自身的稳定路径托管分配为 `0 B`；不得出现逐帧 LINQ、字符串拼接、目录遍历建图或布局重算。
- 搜索文字或筛选 revision 改变时允许有界快照分配，但单次提交不得实例化/销毁节点或连线，且托管分配上限 `64 KB`；相同规范化查询重复提交必须 `0 B`。
- 研究状态更新只刷新受影响节点、相邻连线和详情；不得每帧刷新全部 43 节点。空闲时没有 `Update/LateUpdate` 轮询，使用目录/研究/库存/研究站 revision 驱动。
- 在 `1920×1080`、完整 43 节点、48 连线、全部状态混合时保存 300 帧 Profiler 证据；对象数、监听器、GC 和最慢搜索/筛选提交必须进入验证记录。自动化结果不得写成用户视觉验收或真实 Windows GPU/显存验收。

## 12. TDD 与验收矩阵

必须先建立当前实现失败的测试，再写最小实现。

### 12.1 EditMode

- 精确 `43 = 1 + 4×9 + 6`，ID、`CatalogOrder 0..42`、图标 ID唯一；四路线各 9，桥 6，根 1。
- 所有前置存在、无自引用、无环、从根可达；桥节点恰有两个不同路线的 T2 前置，单侧完成仍不可研究。
- 所有成本资源存在于统一资源目录；成本、时长、A16.4 三项兼容数值及表中 release state 精确一致。
- `PreviewOnly` 不扣款、不启动；研究开始多资源原子扣除、单活动、取消 80%、退款容量拒绝、完成效果命令幂等。
- 研究站四项资格、堡垒 `1.0`、其它形态 `0.5`、思维加速 `1.25`、战术/系统暂停、失站暂停恢复。
- 新旧 schema `31`：补根、三个共享 ID、两个退役 ID、未知完成、未知/退役活动、剩余时间、恢复不重复扣款及再次保存稳定。
- `DemoResearchCatalog` 不再是正式枚举源；正式 UI/运行时代码静态审查不得新增对其 `All/ReleaseState` 的依赖。
- 最新可研究定位、同层确定顺序、搜索规范化、路线/状态筛选、路径上下文和恢复浏览中心为纯状态测试。

### 12.2 PlayMode 真实输入

- `T` 打开 43 节点树；真实鼠标在空白拖动、节点点击、滚轮指针中心缩放、Home 全树定位。
- 搜索中文名和稳定 ID，真实点击路线/状态筛选，上一个/下一个结果与清空恢复。
- 打开自动定位最新可研究范围；点击“定位进行中”；手动浏览后 revision 不抢回。
- 搜索框聚焦时输入 `T/B/E/F/R/0` 不触发关闭、建造、背包、部署、旋转、修改器或世界操作；两次 Esc 顺序正确。
- 点击、拖动和滚轮在科技树上不穿透世界；关闭后世界输入恢复且没有重复监听。
- 通过真实研究站建造、真实库存和研究命令完成至少一条可用路线；拆除/撤离锁定最后研究站后暂停，恢复资格后继续。
- 保存活动研究、退出/重载正式场景、继续游戏，验证完成项、活动项、剩余时间、倍率和打开定位正确重建。

### 12.3 完成门

聚焦测试通过后，运行日常完整 EditMode（本阶段不触发 `TerrainAssetDeep`）、完整 PlayMode、项目质量门、Windows Release 3D、Windows Development 3D、macOS universal 3D、官方文档生成/验证与 `RecordVerification`。新增目录、运行时、UI、测试和图标必须登记项目质量目录及 `Docs/09-Reusable-Project-Catalog-ZH.md`。

人工试玩重点是：自下向上关系是否一眼可读、43 节点缩放后是否仍能辨认、桥节点双前置是否清楚、搜索和定位是否符合预期、锁定原因是否可理解。只有用户亲自确认后才能记录为人工验收完成。

## 13. 实施顺序

1. RED：正式目录合同、43 节点逐项数值、DAG、双前置和退役兼容测试。
2. GREEN：唯一 `FormalResearchCatalog`、效果引用目录与 `DemoResearchCatalog` 兼容 facade。
3. RED/GREEN：统一研究运行时、研究站/暂停/效果命令与 schema `31` 恢复。
4. RED/GREEN：确定性图布局、43 节点/48 连线对象池和状态投影。
5. RED/GREEN：真实拖动、缩放、搜索、筛选、打开定位和输入防穿透。
6. 接入已确认的科技图标；缺图使用同一稳定占位，不以文件名或 UI 私有字典建第二映射。
7. 完成聚焦、全量、性能、构建、质量目录和正式文档收尾；人工试玩与 Windows 真机结论保持待验收。
