# IDEA-0022 F3–F5A 文明扩展设计

## 1. 总体边界

本规格实现 `IDEA-0022`，顺序固定为 F3 军队远征 → F4A 两城/前哨/运输 → F5A 外交/死亡/继承。现有 `64×48` v2 地图、seed `8128`、24 资源节点、建筑格与主城深度运行时不改。没有模型的单位、城市、前哨、运输队和角色使用清楚可替换的程序化标记。

一次升级 schema `33→34`，在 `FormalThreeDSaveData` 增加单一 `civilizationExpansion`。既有顶层主城域继续是 `core.city.000001` 的真值；扩展聚合只保存主城稳定引用、远程 settlement 轻量运行态、Army/Leader 和 Politics/Diplomacy，不复制主城库存/建筑。

## 2. F3 军队与远征

### 2.1 单位目录

| 单位 | 来源 | 制造 | 时间/每建筑容量 | 维护 | HP / 伤害 / 护甲 / 速度 |
|---|---|---|---|---|---|
| 战斗傀儡 | 傀儡工坊 | 合金1+灵铁1 | 20s / 3 | 能晶1/60s | 100 / 12真元 / 轻甲 / 2.5 |
| 培育巨兽 | 巨兽栏 | 骨钢2+生物质浓缩液3 | 35s / 1 | 生物质1/45s | 320 / 24生物 / 生物壳 / 1.2 |
| 灵能机甲 | 灵能机甲厂 | 控制芯片2+灵能增幅器1+能量电池1 | 30s / 2 | 能量电池1/60s | 220 / 20灵能 / 灵能盾 / 1.8 |
| 半机械巨兽 | 生物机库 | 生物武器2+机械组件2+活性生物质1 | 45s / 1 | 生物质1+能量电池1/60s | 380 / 28生物 / 重甲 / 1.1 |

首轮只给玩家一个默认小队 `core.squad.000001`，最多 12 单位，但单位/小队仍使用独立稳定 ID。制造必须经现有城市网络库存原子扣料和已完成/可运行建筑容量；维护失败使单位休眠，不删除它。

### 2.2 命令、战斗与远征

小队命令是 Rally、Guard、FollowLeader、Expedition、Retreat。出征目标必须是已揭示可通行格，路径复用 `CityPathfinder`；暂停允许下令但不推进。小队守卫主城时以窄接口对正式 Defense 敌人提交聚合伤害，伤害仍经 `DamageMatrix`；不复制第二套敌人。领袖健康且带队时小队伤害×1.2。

远征基础时间 45s + 路径成本×1.5s。遭遇由 sessionId + 目标格 + expeditionOrdinal 确定，只使用现有 EnemyCatalog。胜利给予 10–24 合金、8–20 生物质、4–12 能晶中的确定组合；战利品在返城时入库，途中为 pending cargo。撤退保留幸存单位并丢失当次 pending loot。

## 3. F4A 多城市、前哨与运输

世界层固定身份：主城 `core.city.000001`，次城 `core.city.000002`，前哨 `core.outpost.000001`。主城只在扩展聚合中保存引用。建立次城需 40 合金+30 精制石材+10 控制芯片+50 人口；建立前哨需 12 合金+12 石料。两者的目标必须已揭示、可通行、与既有 settlement 不重叠。

次城与前哨各有独立容量 150 的资源库存；次城人口 50/100。次城首轮选择 Industrial、Military、Research 一种自治模板，每 10s 分别产 1 合金、1 弹药或提供主研究×1.2 的集体意识贡献；前哨在通信/补给/维护都正常时每 12s 产 1 石料。这是自治 settlement 运行时，不冒充次城完整双网格建造。

Convoy 记录 source/destination/cargo/path/segment progress/escort squad/risk/status。发运时原子从源库存扣除；每世界格 1.5s，到达才卸货，满仓等待。无护送基础拦截概率 25%，有非休眠小队为 5%，由 convoyId/sessionId 确定性判定；损毁丢失 cargo，不返回源库存。

界面保存 FocusedSettlementId 与 ControlledCityId。可自由查看已通信 settlement；现有集体意识研究完成时视为远程指挥开放，否则领袖需调动 30s 到达次城后才可接管。通信中断保留最后情报并自治，不能发新命令。

## 4. F5A 角色、外交与继承

### 4.1 角色与救援

| ID | 名称 | 专精 | 威望 | 起始忠诚 | 路线倾向 |
|---|---|---|---:|---:|---|
| `core.character.cen-jin` | 岑烬 | 工程/维修 | 70 | 80 | 科技 |
| `core.character.lin-xi` | 林溪 | 研究/管理 | 55 | 75 | 灵能 |
| `core.character.han-gu` | 韩骨 | 军事/远征 | 65 | 55 | 血肉 |

角色状态是 Active、Downed、Recovering、Dead。倒地时限 60s；角色救援距离不超过 1.5 格并持续 8s，城市医疗救援需城市在 3 格内并持续 4s，离开/受击中断。开始时预留 2 生物质，成功时消费，取消返回。剩余时间 <30s 时成功救援增加一个“迟缓反应”永久伤势，恢复期 30s；超时死亡产生遗体与装备记录。

### 4.2 继承与内政

领袖死亡后立即进入 Council，文明生产/研究效率×0.75。玩家可预先指定继承人。候选支持 = 威望 + (忠诚-50)/2 + 支持派系影响力/2 + 城市委派加成 10，夹在 0..100。支持≥60 可直接继承；支持<60的强推产生 CoupCrisis，可选 Concession（两非支持派系影响力+10，库存合金-10）或 Suppression（全派系忠诚-15，指定城市忠诚-20）解决。继承成功只更改 currentLeaderId 和状态，不清空科技、文明、城市、库存或军队。

内部派系：工程议会 45/65、守备团 35/60、迁徙民团 20/70（影响力/忠诚）。任一 settlement 忠诚<20 且守备团忠诚<35时发布一次“割据风险”警报；本轮不生成实体内战或自动转移城市归属。

### 4.3 外交

灰烬商团初始关系 +10，晶律协定会初始 -5。状态是 Unknown、Contacted、TradeAgreement、DefensePact、Hostile。世界层运输队首次接近势力节点或 Development 命令可建立 Contacted。报价按 factionId + sessionId + offerOrdinal 确定，每 60s 可刷新；首轮三类报价是：10 合金换 20 石料、12 生物质换 8 能晶、15 弹药换一次 Convoy 拦截免疫。接受原子结算并关系+5，拒绝不改库存且关系-1。关系≥40 可签 TradeAgreement，≥70 可签 DefensePact，<-40 为 Hostile。

## 5. 运行时与 UI

- `CivilizationExpansionRuntime` 是 Army/WorldLayer/Politics/Diplomacy 的组合根；子域各自拥有 snapshot/revision/prepare/commit，UI 只读快照和发命令。
- `GrayboxCivilizationExpansionController3D` 注入主城 Session/Storage/Research/Defense/Leader/World/Clock，不用 FindObject 重建真值。
- `M` Army、`N` WorldLayer、`P` Character/Politics/Diplomacy；三面板互斥。Succession/Coup 是高优先模态；倒地 HUD 为非模态警报。
- 世界标记只为 settlement/squad/convoy 表现，不拥有路径、库存、HP 或归属真值。

## 6. 存档与验收

schema `34` DTO 保存 next ordinals、Army units/squad/command/position/maintenance/expedition/loot/leader assignment；settlements/outpost/inventories/templates/communication/loyalty、convoys/cargo/path/progress/escort/risk；characters/life/rescue/corpse/wounds/assignment、leadership/candidates/council/coup、internal factions、external diplomacy/offers/cooldowns。Validator 检查稳定 ID 语法、唯一性、目录 ID、数值范围与全部跨引用。schema `33→34` 迁移不反推历史；旧回溯锚点和波前档保持可读。

完成前运行三阶段聚焦 Edit/Play、schema `31→32→33→34` 与 `34` 往返、主城旧回归、日常完整 EditMode、完整 PlayMode、项目质量门、Windows Release/Development、macOS universal、文档生成/校验和 `RecordVerification`。本轮不修改地形源或数组，日常不运行 `TerrainAssetDeep`。自动化不替代用户或真实 Windows 验收。
