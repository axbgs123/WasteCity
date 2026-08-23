# IDEA-0017 单城十波防御战役实施计划

> 日期：2026-08-24<br>
> 状态：已批准范围的可执行 TDD 计划，尚未实施<br>
> 受控需求：`IDEA-0017`<br>
> 权威规格：`Docs/superpowers/specs/2026-08-24-idea-0017-single-city-defense-campaign-design.md`<br>
> Unity：`2022.3.62f1`<br>
> 目标分支：沿用当前 `codex/idea-0017-combat-expansion` 工作分支；最终只普通 push，不创建 Release、不自动合并 PR

## 1. 交付目标与硬边界

交付一场正式 3D 单城十波战役：三座塔、三类敌人、统一 `0/1/2` 倍速度、建筑 HP/摧毁、胜负结算、正式统计和 schema `32`（兼容 `31`）。复用既有网格、物流、库存、生产、研究、`BuildingCatalog`、`DefenseTowerCatalog`、`EnemyCatalog`、真实输入和正式保存系统。

不可越过边界：

- 不进入 Boss、掘地者、掉落、前哨、战争迷雾、注意力驱动波次、多城市、跨城市运输或高级寻路。
- 不改变地图内容、地形源贴图、矿点、导入规则、Texture2DArray 或现有 seed。
- 不新增建模；只用现有可替换几何、材质和正式二维图标完成可观察化。
- 不复制放置合法性、资源节点、物流范围、建筑解锁、资源账本或科技前置判断。
- 不把“代码实现”“自动化通过”“本机运行”“真实 Windows 验证”“用户人工试玩”混写为同一状态。
- 发现第二真值、计划外地图改动或 schema `32` 表达缺口时，由主智能体选择最小兼容结构、同步正式文档后继续；只有与用户/其他任务未提交修改发生真实冲突，或需要超出用户授权的外部操作时才停止并报告。

## 2. 推荐开发顺序总览

```text
规格/登记 → RED 目录与纯模型 → GREEN 战斗模型 → 建筑生命
       → 十波导演 → 三塔物流/科技 → schema 32 → 3D UI/真实输入
       → 统计/结算 → 性能与独立审查 → 全量测试/构建/文档/普通推送
```

每个任务必须按 `RED → 最小 GREEN → REFACTOR → 聚焦验证 → diff 审查` 完成后再进入下一任务。不得先搭完整运行时代码再补测试。

## 3. Task 0：安全基线、需求登记与执行清单

**只读确认**

- `git status --short --branch`、精确 HEAD、当前分支和计划外文件。
- Unity `2022.3.62f1`、LFS 真实对象、唯一 WasteCity Editor 实例。
- 当前完整/聚焦自动化基线与 `Latest-Verification-ZH.md` 一致；旧电脑临时证据不视为本机证据。

**文档变更**

- 用户批准后登记 `Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0017`。
- 修正 `Docs/05-Formal-Development-Roadmap-ZH.md` 对 Boss、完整敌人和结算现状的过期描述，明确本阶段先做单城十波。
- 将本规格和计划列入受控文档目录；状态保持“已批准/未实现”。

**门**

- `git diff --check`；仅允许计划内文档。
- 不在本任务修改运行时代码。

## 4. Task 1：先冻结正式目录合同

**先写 RED**

- 新建 `SingleCityDefenseCampaignCatalogTests`：精确 `campaign.single-city-defense.v1`、10 波、组成、预警、生成时长、方向和稳定顺序。
- 扩展 `RouteDefenseTowerTests`：只冻结机枪/激光/孢子三塔的精确 HP、伤害、DPS、射程、耗材、消费秒数、本地容量和研究引用。
- 扩展 `EnemyCatalogTests`：三敌人的精确稳定 ID、生命、移速、DPS、射程、护甲和目标优先级。
- 失败断言：第 2–10 波生成时长都在 `45–75`；第 1 波恰为 `40`；目录不得出现 Boss、掘地者或未知引用。

**最小 GREEN**

- 新建不可变 `CampaignWaveCatalog`/`CampaignWaveDefinition`；波次不持有运行进度。
- 扩展 `DefenseTowerDefinition` 的本地容量字段并保留机枪容量兼容常量。
- 扩展 `EnemyTargetPriority`，让目录直接表达 `Core / Walls / Production`；不得在战役运行时给啃噬者写目录外特例。
- 只补目录缺失字段，不在 UI 或控制器写数值。

**聚焦验证**

- `SingleCityDefenseCampaignCatalogTests|RouteDefenseTowerTests|EnemyCatalogTests|BuildingGridTests`。

## 5. Task 2：纯战斗模型与确定性固定步

**先写 RED**

- 三类伤害全部只读取既有 `DamageMatrix`；重甲倍率不得在战役代码复制。
- 三塔 `20/48/18` DPS、`10/12/9` 射程和 `3/4/5` 秒耗材租约。
- 射程、目标死亡、目标出圈、稳定 ID 平局和目标重选。
- `0.1` 秒固定步在不同帧分块下得到相同 HP、耗材、目标和统计。
- `0` 冻结、`1×` 基线、`2×` 两倍规则推进；禁止双重乘速。
- 同一步最后敌人死亡和核心归零时判负。

**最小 GREEN**

- 新建或重构 `SingleCityDefenseCampaignModel`、敌人实例、塔战斗状态、伤害结算和不可变快照。
- 保留 `TutorialDefenseRuntimeModel` 的 schema 31 兼容解析入口；正式 3D 不再把它当十波真值。
- 规则模型不引用 Unity GameObject、粒子、LineRenderer 或本地化文本。

**REFACTOR**

- 消除 `FirstDefenseCombatModels`、`TurretWeaponModel` 和新模型之间的重复算法；可保留兼容 facade，但正式运行时只能有一条伤害路径。
- 快照数组按稳定实例 ID 排序并复用缓冲。

**聚焦验证**

- 新建纯模型测试，加跑 `FirstDefenseLoopTests|FirstDefenseWaveRuntimeTests|FormalCombatTests|GameSpeedTests`。

## 6. Task 3：建筑生命、目标优先和摧毁事务

**先写 RED**

- schema 31 既有建筑迁移为定义最大生命；新建/完工/撤离不复制或恢复 HP。
- 啃噬者只攻核心、晶壳兽城墙否则核心、啸叫者生产建筑否则核心；同类候选平局按距离、建筑实例 ID。
- 施工中、非玩家、已摧毁、撤离锁定建筑不可作为目标。
- 建筑生命首次到零只产生一次摧毁；塔、生产、研究站、物流和仓库贡献同一步失效。
- 内部库存、生产预留和本地耗材在首次摧毁事务中清除并记为损失；不退款、不转移、不生成掉落，保存恢复不得重复清除或重复计数。
- 残骸保持原占格；既有放置合法性拒绝覆盖。仓库容量收缩复用既有超容量语义。

**最小 GREEN**

- 新建随建筑实例 ID 持有的 `BuildingInstanceHealthStore` 和摧毁命令事务。
- 在既有建筑生命周期、生产、物流、研究资格和选择投影接入 `Destroyed`，不各自缓存一份生命。
- 世界视图先使用可替换残骸外观，不制作新模型。

**聚焦验证**

- 新建建筑战斗生命测试，加跑 `BuildingGridTests|BuildingPlacementEvaluationTests|ConstructionProgressTests|GrayboxEvacuationTests|GrayboxWarehouseStorageIntegrationTests|FormalProductionSimulationTests`。

## 7. Task 4：十波阶段机、入口冻结与检查点

**先写 RED**

- 第一座已完成且归玩家所有的任一正式防御塔只触发第一波一次，不依赖物流、耗材或玩家暂停。
- `Idle → Warning → SpawningAndCombat → CombatCleanup → 下一波 Warning/终局` 精确阶段转换。
- 生成结束但仍存活不能完成；击杀 90% 不能完成；全部计划敌人全灭才完成。
- 精确组成 `8G`、`10G`、`12G+2C`、`14G+3C`、`16G+4C+2H`、`18G+5C+3H`、`20G+6C+4H`、`22G+8C+5H`、`24G+9C+7H`、`28G+10C+8H`；预警 `15/20/20/25/25/30/30/35/40/45` 秒，生成 `40/45/50/50/55/55/60/60/65/75` 秒；稳定交错且最后一名不晚于配置时长出现。
- 入口在预警开始冻结，存取和城市移动不重选；同 seed/同状态结果一致。
- 第十波全灭胜利、核心归零失败、终局幂等；每波预警前检查点只创建一次。

**最小 GREEN**

- 将 `WaveDirectorModel` 的长期四段目录与正式十波目录分离；正式运行时只使用后者。
- 扩展 `GrayboxDefenseRuntime3D`/控制器的阶段命令和快照适配，移除正式路径的 90% 完成条件。
- 入口解析只读既有世界/建筑真值，不改地图资产。

**聚焦验证**

- `SingleCityDefenseCampaignRuntimeTests|FirstDefenseWaveRuntimeTests|GrayboxFirstDefenseRuntimeTests|GrayboxFormalSaveCheckpointTests|WorldMapTests`。

## 8. Task 5：三塔物流、正式科技和建造闭环

**先写 RED**

- 机枪塔本地 `30` 弹药、激光塔本地 `30` 能晶、孢子塔本地 `30` 生物武器。
- 在物流范围内经既有城市库存原子补给；离网保留并可用完，耗尽停火；恢复物流后继续补给。
- 暂停不消费租约；无目标不消费；射击跨耗材边界不多扣。
- `core.research.automated-defense` 继续解锁机枪塔；`core.research.ballistics`、`core.research.energy-weapons`、`core.research.spore-dispersal` 从 PreviewOnly 转为 Researchable。弹道学成为能量武器的正式进阶前置，后两项分别解锁激光塔和孢子塔。
- 建造仍经过研究、建筑前置、资源短缺、占格和物流正式原因；UI 不得绕过门面直接生成塔。

**最小 GREEN**

- 复用现有机枪供弹租约抽象，泛化为按塔定义的本地供给状态。
- 正式研究目录只把上述三个已接入进阶链的节点转正；其它 `IDEA-0016` 预览节点保持原状。
- 建造菜单继续使用 `BuildingCatalog` 稳定 ID；不改变现有建造栏位置和下方缺料提示。

**聚焦验证**

- 新建三塔供给/解锁测试，加跑 `LogisticsNetworkTests|GrayboxProductionClockTests|FormalResearchCatalogTests|FormalResearchRuntimeTests|BuildingUnlockTests|GrayboxBuildingUiAndInputTests`。

## 9. Task 6：schema 32、schema 31 迁移与文件安全

**先写 RED**

- envelope 当前版本升至 `32`，版本 `31` 全部固定样本继续可读；不破坏备份/事务校验。
- schema `1–30` 的历史身份、解码器、既有迁移、不得覆写和全部固定样本继续回归。
- 31 教程未触发、预警中、生成中、战斗中和已完成五类迁移。
- 三敌人、三塔缓存、入口、任意波阶段、建筑受伤/摧毁、统计、终局、`RequestedSpeed`、`LastNonZeroSpeed` 和固定步余量 round-trip；schema `31` 迁移默认两种速度均为 `1`。
- 未知稳定 ID 保留或明确拒绝，不按数组下标错配；编码顺序确定。
- 中断、校验失败、主文件损坏和备份恢复不覆盖可用旧存档。
- schema 31 迁移统计显示部分记录，不用零冒充完整历史。

**最小 GREEN**

- 扩展 `FormalSaveData`/codec/validator/envelope，新增单一 `31 → 32` 迁移器。
- `GrayboxDefenseSaveAdapter3D` 改接正式战役状态；旧教程 DTO 只供迁移读取。
- `FormalSaveCheckpointPolicy` 增加波前检查点事件，不改变既有手动/自动存档事务。

**聚焦验证**

- `FormalSaveEnvelopeTests|FormalSaveTests|FormalSaveValidatorTests|FormalSaveFileTransactionTests|GrayboxFormalSaveDefenseTests|GrayboxFormalSaveRoundTripTests|GrayboxFormalSaveCheckpointTests`。

## 10. Task 7：统一速度、真实输入与焦点优先级

**先写 RED**

- 真实 Input System：`Space` 暂停/恢复最后非零速度，`1`/`2` 切速，屏幕按钮提交同一命令。
- 建造快捷栏、科技树搜索、修改器搜索和其他模态焦点获得数字键时，不穿透改速度。
- `0` 仍只打开 Development 修改器；Release 不出现修改器。
- 系统菜单叠加暂停原因，关闭后恢复请求速度；终局有效速度保持零。
- 暂停下镜头/UI/选择可用，战斗/生产/施工/研究/统计不推进。

**最小 GREEN**

- 让所有规则系统从统一速度快照取得一次规则增量；`Time.timeScale` 仅作经审计的兼容表现镜像。
- 在正式输入路由集中处理模态优先级，不在各 HUD 轮询键盘。

**聚焦验证**

- `GameSpeedTests|GrayboxDefenseRuntimeInputTests|GrayboxBuildingUiAndInputTests|GrayboxProductionObservabilityRuntimeInputTests|ResearchTreeUiContractTests|GrayboxDeveloperModifierRuntimeInputTests|GrayboxFormalSaveUiAndInputTests`。

## 11. Task 8：3D HUD、选择详情和池化表现

**先写 RED**

- HUD 投影精确显示波次/10、阶段、倒计时、组成、已生成/计划、存活、核心 HP 和请求/有效速度。
- 塔、普通建筑、敌人、残骸四种选择详情字段与稳定停火原因顺序。
- 真实点击选择三类对象；屏幕外敌人仍计数；HUD 不通过 GameObject 数量反推状态。
- 机枪示踪、激光束、孢子轨迹只消费已结算事件；禁用表现、重复消费或池耗尽不改伤害。
- 资源条、建造栏、下方缺料提示和新战役 HUD 在 `1920×1080` 不互相遮挡。

**最小 GREEN**

- 扩展 `GrayboxDefenseHud3D`、`GrayboxDefenseHudView3D`、`GrayboxDefenseWorldView3D` 和正式选择投影。
- 复用视觉 manifest/Sprite catalog 与现有建筑几何；新增占位必须登记项目质量和可复用目录。
- 所有文本由快照 revision 更新，静止帧不拼接。

**聚焦验证**

- `GrayboxDefenseObservabilityTests|GrayboxDefensePresentationTests|GrayboxDefenseRuntimeInputTests|GrayboxDefenseSnapshotStabilityTests|GrayboxSceneContractTests|Production2DVisualCatalogAtlasTests`。

## 12. Task 9：结算、统计与重试/继续

**先写 RED**

- 胜利/失败唯一事件、核心归零优先、终局冻结、重复 Restore 不重复结算。
- 全部统计字段的精确增量、过量伤害排除、同一实例只计一次、稳定 ID 分项顺序。
- 生产效率分子/分母边界，无分母显示无数据；暂停和终局不计时。
- schema 31 迁移部分统计标记；修改器使用标记。
- 胜利继续沙盒无新波并恢复最后非零速度；失败只允许读波前检查点或返回标题。
- 结算按钮通过真实输入，模态期间不穿透世界、建造和速度快捷键。

**最小 GREEN**

- 扩展 `SessionStatisticsModel` 为 schema 32 战役统计唯一所有者，消费战斗/生产/撤离已提交事件。
- 新建结算快照与视图；不根据 HUD 文本重新计算结果。

**聚焦验证**

- `SessionStatisticsTests|SingleCityDefenseSettlementTests|GrayboxDefenseRuntimeInputTests|GrayboxFormalSaveRoundTripTests|GuidanceFlowTests`。

## 13. Task 10：性能、稳定重建和独立审查

**性能 RED/门**

- 第 10 波 46 敌人、三类塔各 8 座、混合建筑受伤、完整 HUD，预热后 300 帧无持续托管分配。
- 单次波次切换快照分配 `<=64 KB`；目标查询无每帧场景搜索/LINQ/临时列表。
- 连续十波、20 次读档、20 次结算开关、20 次失败重试，对象、池、监听器和稳定 ID 无界增长为零。
- 不同帧分块、存取断点和 1×/2×切换产生相同战斗结果。

**独立静态审查**

- 搜索平行塔/敌人/波次数值、90% 完成残留、直接库存修改、第二生命真值、直接 UI 调模型私有状态、逐帧分配和输入穿透。
- 审查 schema `1–30` 与 `31` 所有 fixture、未知内容策略、原子事务和备份安全。
- 审查未引入 Boss、掉落、前哨、迷雾、高级寻路、地图改动或新模型。

**运行时验证**

- 自动推进新游戏到三塔/三敌人混合波，验证读档、摧毁、胜负和结算。
- 从至少三个 schema `31` fixture 进入正式 3D，验证教程各阶段迁移；运行 schema `1–30` 全部固定样本的历史身份与只读兼容回归。
- 捕获性能与 UI 证据；只标记本机自动化结论。

## 14. Task 11：全量回归、构建与文档收尾

按顺序执行，并保存精确命令、Unity 版本、提交、开始/结束时间和结果：

1. 聚焦 IDEA-0017 EditMode 与 PlayMode。
2. 日常完整 EditMode；`TerrainAssetDeep` 按规则排除。
3. 完整 PlayMode。
4. 项目质量扫描、编译/程序集/场景合同和文档引用验证。
5. Windows Release 3D。
6. Windows Development 3D。
7. macOS universal 3D。
8. 约定的 Player smoke、启动、存取和战役结算验证。
9. 官方文档生成、验证和 `RecordVerification`。

需要更新：

- `Docs/01-Game-Design-Document-ZH.md`：只写已批准、与本规格一致的十波首版边界。
- `Docs/05-Formal-Development-Roadmap-ZH.md`：校准实际现状与下一阶段。
- `Docs/06-User-Feedback-and-Change-Control-ZH.md`：按真实证据推进 `IDEA-0017` 状态。
- `Docs/07-Project-Use-and-Development-Guide-ZH.md`：三塔、速度、战役、结算的玩家操作。
- `Docs/08-Testing-and-Bug-Location-Guide-ZH.md`：波次、建筑伤害、迁移和性能定位。
- `Docs/09-Reusable-Project-Catalog-ZH.md`：战役模型、健康存储、结算、迁移器和表现池。
- `Docs/Engineering/project-quality-catalog.json` 及相关场景/UI/资产质量登记。
- `Docs/Generated/Latest-Verification-ZH.md`：只由正式记录流程写入真实结果。

发布准备阶段若项目规则要求运行 `TerrainAssetDeep`，单独执行并记录；普通代码迭代不运行。真实 Windows 10/11 的视觉、GPU、显存和内存验证未执行时必须继续列为待验。

## 15. 分批提交建议

1. `docs: design IDEA-0017 single-city defense campaign`
2. `test: specify defense campaign catalogs`
3. `feat: add deterministic three-tower combat`
4. `feat: add building combat health and destruction`
5. `feat: run ten-wave single-city campaign`
6. `feat: connect defense logistics and research unlocks`
7. `feat: migrate formal saves to schema 32`
8. `feat: add defense speed input and observability`
9. `feat: add campaign settlement and statistics`
10. `perf: stabilize full defense campaign runtime`
11. `docs: record IDEA-0017 verification`

每个提交前都必须满足对应聚焦测试 GREEN、`git diff --check`、计划内文件审查和状态措辞核对。最终全量验证前只能写“已实现待验证”；用户实际试玩前不能写“人工验收通过”。所有远端操作只用普通 push，不创建 Release、不合并 PR、不 force-push。
