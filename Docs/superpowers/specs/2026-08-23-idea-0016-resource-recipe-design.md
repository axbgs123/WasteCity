# IDEA-0016：四路线资源与配方设计规格

> 需求 ID：`IDEA-0016`<br>
> 适用版本：Unity `2022.3.62f1`，正式 3D 入口，schema `31`<br>
> 权威依据：GDD A4.4、A6.2、A16.3、B 卷 2.13–2.15；`IDEA-0011`、`IDEA-0015`、`IDEA-0016`<br>
> 状态边界：本文件冻结可执行内容，不代表代码、图标、构建或人工试玩已经完成。

## 1. 目标和边界

本阶段把既有 `15` 种资源扩展为精确 `31` 种，统一登记来源、用途、发现条件、背景简介与图标语义；把 `3` 条机器配方和 `2` 条应急手工配方扩展为四路线加工和跨路线融合闭环。

固定边界：

- 原五种地图资源仍是铁矿、能晶、石料、生物质、水；seed `8128`、资源点位置、类型、储量及 `64×48` 外圈全部不变。
- 新增 `16` 种内容全部通过加工、精炼或融合获得，不新增地图矿物、采集点或第二套节点真值。
- 原 `15` 个 stable ID、中文名、正式初始数量、堆叠上限和旧存档含义保持兼容；schema 保持 `31`。
- 采矿 `3` 秒、冶炼合金 `6` 秒、装配弹药 `6` 秒以及两条 `12` 秒低效率手工配方保持不变。
- 高级路线材料只能由建筑生产，不能手工排队；首轮仍只保留“应急合金”和“应急弹药”两条手工配方。
- 新配方优先复用现有 `BuildingCatalog` 建筑。机器多输入/多输出和同建筑多配方必须进入统一生产运行时；不得新增平行生产模型。
- 敌人、炮塔、单位、多城市、跨城市运输和新建筑运行效果不因材料名称自动进入本轮；尚无消费端的终产物至少由正式科技成本或下游配方消费。

## 2. 正式资源数据合同

`ResourceDefinitionCatalog` 是唯一资源配置真值。每项不可变定义至少包含：

```text
Id / ChineseName / Route / Tier / StackLimit / FormalInitialCityAmount
SourceKinds / SourceSummary / UseKinds / UseSummary
DiscoveryRule / RequiredResearchIds / IconId
LoreBrief / VisualKeywords / ForbiddenVisualElements
DisplaySizesPx
```

路线枚举为 `Common / Technology / Cultivation / Biological / Psionics / Fusion`；层级为 `Raw / Intermediate / Product`。发现规则只允许从 schema `31` 已有真值派生：

1. `Always`：五种基础资源常驻资源栏；
2. `OwnedOrResearch`：城市核心、有效仓库、背包、建筑库存或合成预留中数量大于零，或任一关联研究已完成；
3. `OwnedOrRecipe`：已拥有，或其生产配方的研究前置已完成；
4. `OwnedOrAllRequirements`：已拥有，或融合配方的全部路线前置已完成。

物品耗尽后若没有研究/配方事实支撑，可以重新隐藏；本轮不增加独立永久发现位。若产品要求“曾见过一次后永久显示”，必须单独申请 schema 升级。

所有正式图标使用 `art.icon.item.<stable-id 去前缀后的语义段>`；实际显示覆盖 `20/24/32/40/64 px`，正式母版与 Unity 交付尺寸以二维美术管线规格为准。共通禁用项为：文字、数字、完整背景板、资源数量、路线状态和无法在 `20 px` 保留的细碎装饰。

## 3. 精确 31 种资源

### 3.1 通用与科技（12）

| # | 中文名 / stable ID | 路线·层级·发现 | 正式来源 | 正式用途 | 背景简介与视觉关键词 |
|---:|---|---|---|---|---|
| 1 | 铁矿<br>`core.resource.iron` | 通用·原料·Always | 铁矿节点采矿 | 合金、灵铁、骨钢、共振金属、研究 | 氧化废铁中仍可回收的金属矿；橙褐矿块、深色断面，禁用钢锭。 |
| 2 | 能晶<br>`core.resource.energy-crystal` | 通用·原料·Always | 能晶节点采矿 | 冷却液、能量电池、灵石、路线研究 | 储存不稳定能量的青色晶体；锐利晶簇、内发光，禁用宝石首饰。 |
| 3 | 石料<br>`core.resource.stone` | 通用·原料·Always | 石料节点采矿 | 精制石材、建筑、阵基、研究 | 废墟地层中的结构石；灰色层理块，禁用水泥砖文字。 |
| 4 | 生物质<br>`core.resource.biomass` | 通用·原料·Always | 既有尸体回收/正式初始库存 | 碳纤维、营养胶、生物路线、研究 | 可回收有机组织混合物；绿色纤维团、少量湿润高光，禁用血腥肢体。 |
| 5 | 水<br>`core.resource.water` | 通用·原料·Always | 既有水资源入口/库存 | 冷却、营养、炼丹、意识处理 | 经城市回收的工业水；密封水囊与蓝色液面，禁用自然风景。 |
| 6 | 精制石材<br>`core.resource.refined-stone` | 通用·中间品·OwnedOrRecipe | 冶炼厂精整石料 | 机械组件、阵法核心、建筑研究成本 | 压实并校准尺寸的结构材；分层灰板、整齐切边，禁用普通原石轮廓。 |
| 7 | 冷却液<br>`core.resource.coolant` | 通用·中间品·OwnedOrRecipe | 装配厂混合水与能晶 | 超导线圈、高负载科技成本 | 吸收废热的稳定蓝色介质；金属罐、青蓝液窗，禁用饮料瓶。 |
| 8 | 碳纤维<br>`core.resource.carbon-fiber` | 通用·中间品·OwnedOrRecipe | 装配厂碳化生物质 | 机械组件、轻质防护研究 | 从生物质中抽出的高强纤维；黑色编织卷、青灰夹扣，禁用布料花纹。 |
| 9 | 合金<br>`technology.resource.alloy` | 科技·中间品·OwnedOrResearch | 冶炼厂或应急手工 | 弹药、科技组件、建筑和研究 | 标准化耐热金属锭；冷灰锭、橙色炉痕，禁用黄金质感。 |
| 10 | 弹药<br>`technology.resource.ammunition` | 科技·终产物·OwnedOrResearch | 装配厂或应急手工 | 机枪塔供弹、弹道研究 | 封装为统一供弹规格的弹箱；紧凑弹匣与黄条，禁用散落实弹堆。 |
| 11 | 能量电池<br>`technology.resource.energy-cell` | 科技·中间品·OwnedOrRecipe | 装配厂封装能晶与合金 | 控制芯片、路线建筑和研究 | 可更换的工业储能单元；厚壳电芯、青色端口，禁用现代消费电池。 |
| 12 | 机械组件<br>`technology.resource.mechanical-component` | 科技·中间品·OwnedOrRecipe | 装配厂加工合金、石材与碳纤维 | 控制芯片、无人系统、融合核心 | 标准轴承、执行器和结构件组合；齿轮与框架强轮廓，禁用工具箱。 |

### 3.2 科技、修仙与血肉（11）

| # | 中文名 / stable ID | 路线·层级·发现 | 正式来源 | 正式用途 | 背景简介与视觉关键词 |
|---:|---|---|---|---|---|
| 13 | 控制芯片<br>`technology.resource.control-chip` | 科技·终产物·OwnedOrRecipe | 装配厂加工机械组件与能量电池 | 无人系统、融合核心和高级研究 | 为废土机械封装确定控制逻辑；厚陶瓷芯片、粗引脚、青灯，禁用现代手机芯片。 |
| 14 | 超导线圈<br>`technology.resource.superconductive-coil` | 科技·终产物·OwnedOrRecipe | 装配厂以合金、能晶和冷却液绕制 | 能量武器、精神脉冲与高阶研究 | 在低温下输送高能量的绕组；铜灰线圈、青色冷凝，禁用巨大特斯拉塔。 |
| 15 | 灵铁<br>`cultivation.resource.spirit-iron` | 修仙·中间品·OwnedOrResearch | 灵火炉淬炼铁矿与能晶 | 飞剑、阵法核心、灵丹和研究 | 被灵火重排纹理的金属；暗银锭、细青金纹，禁用普通合金锭。 |
| 16 | 飞剑<br>`cultivation.resource.flying-sword` | 修仙·终产物·OwnedOrRecipe | 炼器坊加工灵铁与合金 | 剑阵、御剑研究与高周波桥节点 | 适于神识驱动的标准剑器；短而清楚的悬浮剑轮廓，禁用人物持剑场景。 |
| 17 | 灵丹<br>`cultivation.resource.elixir` | 修仙·终产物·OwnedOrRecipe | 炼丹房提炼浓缩液、灵石与水；融合配方增产 | 消耗品、血肉灵丹桥节点 | 封存短时活性的丹剂；密封丸与灵纹容器，禁用古风药瓶背景。 |
| 18 | 灵石<br>`cultivation.resource.spirit-stone` | 修仙·中间品·OwnedOrRecipe | 聚灵阵压缩能晶与精制石材 | 阵法核心、炼丹、修仙研究 | 能稳定储存灵性流动的阵材；石质晶核与环形纹路，禁用能晶同轮廓。 |
| 19 | 阵法核心<br>`cultivation.resource.formation-core` | 修仙·终产物·OwnedOrRecipe | 炼器坊组合灵石与灵铁 | 阵法强化、高周波桥接、融合核心 | 固化阵法拓扑的可替换核心；多边环与悬浮中心，禁用完整法阵背景。 |
| 20 | 骨钢<br>`biological.resource.bone-steel` | 血肉·中间品·OwnedOrResearch | 菌落池让活性生物质附着铁矿生长 | 生物武器、建筑和血肉研究 | 兼具韧性与自愈倾向的骨质金属；象牙灰骨架与铁芯，禁用人体骨骼。 |
| 21 | 生物质浓缩液<br>`biological.resource.biomass-concentrate` | 血肉·中间品·OwnedOrResearch | 菌落池浓缩水和生物质 | 活性生物质、酸腺、炼丹和研究 | 去除惰性组织后的高营养浆液；绿色密封罐与沉降层，禁用血液。 |
| 22 | 生物武器<br>`biological.resource.weapon` | 血肉·终产物·OwnedOrRecipe | 培育室组合骨钢与酸腺 | 孢子/酸液防御研究和融合消费 | 受控生长的攻击性器官模块；骨壳喷口、绿色囊体，禁用完整怪物。 |
| 23 | 活性生物质<br>`biological.resource.active-biomass` | 血肉·中间品·OwnedOrRecipe | 菌落池激活浓缩液与能晶 | 骨钢、变异基因和灵植精华 | 对刺激作出稳定反应的培养组织；紧凑组织团与脉冲青光，禁用肢体。 |
| 24 | 变异基因<br>`biological.resource.mutant-gene` | 血肉·终产物·OwnedOrRecipe | 培育室筛选活性生物质与水 | 酸腺、基因研究和融合核心 | 经筛选的适应性遗传片段；双螺旋胶囊与绿紫色带，禁用文字标签。 |
| 25 | 酸腺<br>`biological.resource.acid-gland` | 血肉·终产物·OwnedOrRecipe | 培育室培养浓缩液与变异基因 | 生物武器、酸液科技 | 可替换的高压腐蚀腺体；骨质接口与黄绿囊体，禁用写实内脏。 |

### 3.3 灵能与融合（6）

| # | 中文名 / stable ID | 路线·层级·发现 | 正式来源 | 正式用途 | 背景简介与视觉关键词 |
|---:|---|---|---|---|---|
| 26 | 共振金属<br>`psionics.resource.resonance-metal` | 灵能·中间品·OwnedOrResearch | 共振炉加工铁矿与能晶 | 灵能增幅器、建筑和研究 | 能记录微弱精神频率的金属；银灰薄片与紫青波纹，禁用普通合金锭。 |
| 27 | 灵能增幅器<br>`psionics.resource.amplifier` | 灵能·终产物·OwnedOrRecipe | 灵能工坊组合共振金属与意识碎片 | 灵能结晶、护盾和灵能研究 | 放大并约束精神信号的器件；双环谐振器、紫色核心，禁用扬声器。 |
| 28 | 意识碎片<br>`psionics.resource.consciousness-shard` | 灵能·中间品·OwnedOrRecipe | 意识网络从水和能晶中沉淀信号 | 灵能增幅器、灵能结晶和研究 | 被介质捕获的残余认知片段；半透明碎片与断续波纹，禁用人脸。 |
| 29 | 灵能结晶<br>`psionics.resource.psionic-crystal` | 灵能·终产物·OwnedOrRecipe | 意识网络压缩意识碎片与增幅信号 | 心灵护盾、预知和融合核心 | 高密度稳定精神能量的结晶；紫青核心与同心波，禁用能晶同色同轮廓。 |
| 30 | 灵植精华<br>`fusion.resource.spirit-plant-extract` | 融合·中间品·OwnedOrAllRequirements | 培育室在灵植培育前置下融合灵石、活性生物质和水 | 血肉灵丹、恢复类研究 | 同时保持灵性与活性的植物提取物；青绿叶状液滴与阵纹，禁用自然植物场景。 |
| 31 | 融合核心<br>`fusion.resource.hybrid-core` | 融合·终产物·OwnedOrAllRequirements | 装配厂在全路线前置下组合控制芯片、阵法核心、变异基因和灵能结晶 | 四路线终局研究与后续融合建筑输入 | 四套规则在同一壳体内维持平衡的接口核心；四色受控分区、中心锁环，禁用彩虹光团。 |

## 4. 正式配方合同

`ResourceRecipeCatalog` 是唯一配方配置真值。每项必须包含：`Id / ChineseName / Kind / AllowedBuildingIds / Inputs[] / Outputs[] / DurationSeconds / RequiredResearchIds[] / DefaultForBuilding / IconProjection / LoreBrief`。机器配方的 `AllowedBuildingIds` 至少一项；手工配方不得含建筑。融合配方至少包含两条路线输入或两个路线前置。

机器周期开始时一次性从建筑内部输入或城市物流网络原子预留完整 `Inputs`；完成时原子检查并写入完整 `Outputs`。任一输入不足或任一输出无容量都不得部分扣除、部分生产、截断或丢弃。新产物在下一物流步才能卸入城市。配方切换仅在进度为零、没有预留且内部输入/输出为空时允许，否则显示具体拒绝原因。

## 5. 配方清单

### 5.1 既有兼容配方（数值不变）

| stable ID | 类型 / 建筑 | 输入 → 输出 | 周期 | 前置 |
|---|---|---|---:|---|
| `core.production.extract-node-resource` | 机器 / 采矿站 | 绑定节点 → 对应资源×1 | 3 秒 | 废料加工 |
| `core.production.smelt-alloy` | 机器 / 冶炼厂 | 铁矿×2 → 合金×1 | 6 秒 | 基础冶金 |
| `core.production.assemble-ammunition` | 机器 / 装配厂 | 合金×2 → 弹药×2 | 6 秒 | 精密装配 |
| `core.crafting.field-alloy` | 手工 | 铁矿×4 → 合金×1 | 12 秒 | 基础冶金 |
| `core.crafting.field-ammunition` | 手工 | 合金×4 → 弹药×2 | 12 秒 | 精密装配 |

### 5.2 通用与科技机器配方

| stable ID | 建筑 | 输入 → 输出 | 周期 | 前置 |
|---|---|---|---:|---|
| `core.production.refine-stone` | 冶炼厂 | 石料×3 → 精制石材×2 | 6 秒 | 基础冶金 |
| `core.production.mix-coolant` | 装配厂 | 水×2 + 能晶×1 → 冷却液×2 | 6 秒 | 精密装配 |
| `core.production.spin-carbon-fiber` | 装配厂 | 生物质×3 + 水×1 → 碳纤维×2 | 8 秒 | 精密装配 |
| `technology.production.energy-cell` | 装配厂 | 能晶×2 + 合金×1 → 能量电池×1 | 8 秒 | 热能工程 |
| `technology.production.mechanical-component` | 装配厂 | 合金×2 + 精制石材×1 + 碳纤维×1 → 机械组件×2 | 8 秒 | 精密装配 |
| `technology.production.control-chip` | 装配厂 | 机械组件×1 + 能量电池×1 → 控制芯片×1 | 10 秒 | 无人系统 |
| `technology.production.superconductive-coil` | 装配厂 | 合金×2 + 能晶×2 + 冷却液×1 → 超导线圈×1 | 10 秒 | 能量武器 |

### 5.3 修仙机器配方

| stable ID | 建筑 | 输入 → 输出 | 周期 | 前置 |
|---|---|---|---:|---|
| `cultivation.production.refine-spirit-iron` | 灵火炉 | 铁矿×2 + 能晶×1 → 灵铁×1 | 6 秒 | 灵火淬炼 |
| `cultivation.production.gather-spirit-stone` | 聚灵阵 | 能晶×2 + 精制石材×1 → 灵石×1 | 8 秒 | 聚灵术 |
| `cultivation.production.flying-sword` | 炼器坊 | 灵铁×2 + 合金×1 → 飞剑×1 | 8 秒 | 炼器基础 |
| `cultivation.production.formation-core` | 炼器坊 | 灵石×2 + 灵铁×1 → 阵法核心×1 | 10 秒 | 阵法强化 |
| `cultivation.production.elixir` | 炼丹房 | 生物质浓缩液×2 + 灵石×1 + 水×1 → 灵丹×1 | 10 秒 | 炼丹术 |

### 5.4 血肉机器配方

| stable ID | 建筑 | 输入 → 输出 | 周期 | 前置 |
|---|---|---|---:|---|
| `biological.production.biomass-concentrate` | 菌落池 | 生物质×3 + 水×1 → 生物质浓缩液×2 | 6 秒 | 菌落培养 |
| `biological.production.active-biomass` | 菌落池 | 生物质浓缩液×2 + 能晶×1 → 活性生物质×1 | 8 秒 | 菌落培养 |
| `biological.production.bone-steel` | 菌落池 | 铁矿×2 + 活性生物质×1 → 骨钢×1 | 8 秒 | 菌落培养 |
| `biological.production.mutant-gene` | 培育室 | 活性生物质×2 + 水×1 → 变异基因×1 | 10 秒 | 基因剪接 |
| `biological.production.acid-gland` | 培育室 | 生物质浓缩液×2 + 变异基因×1 → 酸腺×1 | 10 秒 | 酸液喷吐 |
| `biological.production.weapon` | 培育室 | 骨钢×2 + 酸腺×1 → 生物武器×1 | 10 秒 | 生物培育 |

### 5.5 灵能与融合机器配方

| stable ID | 建筑 | 输入 → 输出 | 周期 | 前置 |
|---|---|---|---:|---|
| `psionics.production.resonance-metal` | 共振炉 | 铁矿×2 + 能晶×1 → 共振金属×1 | 6 秒 | 意识共振 |
| `psionics.production.consciousness-shard` | 意识网络 | 水×2 + 能晶×1 → 意识碎片×1 | 8 秒 | 意识网络 |
| `psionics.production.amplifier` | 灵能工坊 | 共振金属×2 + 意识碎片×1 → 灵能增幅器×1 | 10 秒 | 灵能工坊 |
| `psionics.production.psionic-crystal` | 意识网络 | 意识碎片×2 + 灵能增幅器×1 → 灵能结晶×1 | 10 秒 | 集体意识 |
| `fusion.production.spirit-plant-extract` | 培育室 | 灵石×1 + 活性生物质×2 + 水×1 → 灵植精华×1 | 12 秒 | 灵植培育（修仙+血肉桥） |
| `fusion.production.flesh-elixir` | 炼丹房 | 灵植精华×1 + 变异基因×1 → 灵丹×3 | 12 秒 | 血肉灵丹（血肉+修仙桥） |
| `fusion.production.hybrid-core` | 装配厂 | 控制芯片×1 + 阵法核心×1 + 变异基因×1 + 灵能结晶×1 → 融合核心×1 | 18 秒 | 无人系统 + 阵法强化 + 基因剪接 + 集体意识；本轮仅在四项均为正式可研究节点时开放 |

## 6. 来源、用途与可达性门

- 精确 `31` 项均必须被目录完整性测试证明至少有一个来源和一个用途；“以后可能用”不算用途。
- 五基础资源的来源是冻结地图/既有入口；其余 `26` 项至少被一条正式配方产出。
- 终产物用途可为已接入消费、正式配方输入或可研究节点成本。实现目录时必须把本规格使用的新材料成本回写到正式研究配置，不能继续让所有高阶研究只消耗五基础资源。
- `fusion.production.hybrid-core` 使用 `core.research.unmanned-systems`、`core.research.formation-reinforcement`、`core.research.gene-splicing`、`core.research.collective-consciousness` 四个显式前置；不得新增隐含计数器或从 UI 筛选状态猜测。
- 任一配方前置为 `PreviewOnly` 时，该配方也不可运行；UI 必须显示其前置为本阶段预览，不能生产无真实解锁来源的内容。

## 7. 生产、存档和 UI 兼容

- `FormalProductionDefinition` 改为 `Inputs/Outputs` 数组；旧单输入属性只可作为只读兼容投影，不能继续成为新配方真值。
- 建筑 ID 映射为有稳定顺序的允许配方列表；新建筑使用唯一 `DefaultForBuilding`，已有建筑保持当前 `definitionId`。
- schema `31` 现有生产 `definitionId` 和输入/预留/输出数组足以往返当前配方及多资源库存，不新增字段。恢复时验证“存档配方属于建筑允许列表”，而不是必须等于唯一默认配方。
- 未知但语法有效的资源、配方和生产定义继续按既有惰性内容规则保留；重新安装内容后可恢复，不静默删除或改名。
- 资源状态栏、完整账本、背包、仓库、生产详情、科技成本、建造成本、世界标记和修改器只通过 stable ID 读取资源目录；`RouteContentDisplayCatalog` 不再持有平行中文名或配方文案。
- 顶部状态栏常驻五基础资源；其他资源服从第 2 节派生发现规则。完整账本显示已发现项并提供路线/层级筛选；不得把 31 项全部硬塞进一行。

## 8. RED → GREEN 门

1. `ResourceDefinitionCatalogTests` 先固定精确 31 ID、原 15 顺序兼容、字段完整、来源用途和发现规则。
2. `ResourceRecipeCatalogIntegrityTests` 先固定配方 ID 唯一、资源/建筑/研究引用完整、非地图资源来源、终产物用途和融合前置。
3. `FormalProductionSimulationTests` 先固定多输入原子预留、多输出容量、暂停/脱网/输出满与旧 `3/6/6` 回归。
4. `GrayboxProductionRuntimeTests` 先固定同建筑多配方、默认、切换拒绝、生命周期与撤离锁。
5. `GrayboxFormalSaveProductionTests` 先固定 schema `31` 非默认配方、多输入/输出和未知内容往返，DTO 字段集合不变。
6. `PlayerBackpackModelTests`、`CityResourceStorageModelTests`、仓库和合成测试证明 31 种资源的堆叠、过滤、转移、容量和恢复。
7. PlayMode 从正式 3D Input System 验证配方选择、资源栏滚动/筛选、背包合成和修改器中文搜索；不得只调用内部方法。

出现必须新增地图资源、修改 seed、增加独立永久发现状态、升级 schema、复制库存/物流真值或创建计划外生产文件时停止并报告。除此之外，同类数值命名、图标留白和列表排版等小型决策按本规格与长期维护性处理，不为极低概率情形扩大系统。
