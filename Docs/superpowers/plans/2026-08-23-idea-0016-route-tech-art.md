# IDEA-0016 四路线资源、科技树与二维美术实施计划

> 日期：2026-08-23<br>
> 状态：已批准需求的可执行 TDD 计划，尚未实施<br>
> 受控需求：`IDEA-0016`<br>
> 权威规则：`Docs/01-Game-Design-Document-ZH.md` A4.4、A4.9、A6.2、A16.3、A16.4、A20、A21，`Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0016`，以及同日三份 IDEA-0016 设计规格<br>
> 精确计划基线：`9056957150978ec0e7147bea389fb733da98d091`（六类代表性视觉方向全部确认）<br>
> 工作分支：`codex/idea-0016-route-tech-art`<br>
> Unity：`2022.3.62f1`

## 1. 目标与不可越过边界

本阶段把既有生产、背包、仓库、研究和资源可观察化扩展为正式四路线内容：统一精确 `31` 种资源和配方真值，接入 `43` 节点自下向上科技树，补齐文明式资源状态、背包合成和 Development 修改器中文检索，并按用户已确认的六类风格生成、登记和接线透明二维资产。

固定边界：

- seed `8128` 的既有内容区和 `64×48` 外圈地图不变；新增材料只通过加工、精炼、融合、回收或既有战利品入口获得，不新增矿点。
- schema 保持 `31`。资源、配方、科技均使用稳定 ID；发现状态从已保存的研究、库存、配方和路线事实确定性派生。若必须保存独立且永久的发现位，立即停止并申请 schema 升级。
- 不进入敌人、炮塔、弹丸、跨城市运输、传送带、电力、工人单位、正式模型制作或新地图美术。
- 原 `15` 个资源、`3/6/6` 秒生产基线、30 格背包、仓库共享总容量 `150`、单活动研究和 `0` 键 Development 修改器保持兼容。
- `ResourceDefinitionCatalog`、`ResourceRecipeCatalog`、正式研究目录分别是资源、配方、科技的单一配置真值；UI、修改器、图标和说明不得维护平行名称、成本或解锁判断。
- 所有新增生产文件和资产都必须进入项目质量目录与可复用目录；TerrainAssetDeep 仍只在其既有触发条件或发布准备时运行。

## 2. 所有权与依赖方向

- 资源目录拥有稳定 ID、中文名、路线、层级、堆叠、来源、用途、发现规则、简介和图标 ID；`ResourceIds` 只保留常量，不再拥有第二份 `All` 清单。
- 配方目录拥有机器/手工类型、允许建筑、输入输出数组、周期、科技前置、默认顺序、简介和图标投影；生产运行时只持有建筑实例当前配方、内部库存、预留、进度和暂停。
- 正式研究目录拥有 `43` 节点配置；`ResearchModel` 只持有完成集合、活动项和剩余时间；树布局、筛选和定位是无存档的纯投影。
- 背包、城市核心、仓库和建筑库存继续由现有模型分别持有；所有转移、配方投入和完成产出继续通过原子事务。
- 二维资产 manifest 是视觉登记真值；运行时 catalog 只把稳定内容 ID 解析为 Sprite 或确定性 fallback，不反向定义玩法内容。
- 视图只渲染快照并发布命令；控制器不逐帧重建目录、不直接改模型私有集合，也不复制资源发现、研究可用性或配方合法性。

## 3. RED → GREEN → REFACTOR 顺序

### Task 1：书面规格、内容简介与机器清单

**文件**

- `Docs/superpowers/specs/2026-08-23-idea-0016-resource-recipe-design.md`
- `Docs/superpowers/specs/2026-08-23-idea-0016-research-tree-design.md`
- `Docs/superpowers/specs/2026-08-23-idea-0016-production-2d-art-pipeline-design.md`
- `Docs/Art/IDEA-0016/IDEA0016-2D-Visual-Content-Catalog-ZH.md`
- `Docs/Engineering/idea-0016-visual-assets.json`

先冻结逐项资源、配方、科技和视觉字段，再进入代码或正式图片生成。清单必须证明每种资源至少有一个真实来源和用途、每个引用存在、每个视觉项有目标尺寸/透明要求/路径/状态；拒绝样图和旧通用 10 图不得进入仓库。

### Task 2：资源与配方目录完整性

**先写 RED**

- 扩展 `ResourceDefinitionCatalogTests`：精确稳定顺序、字段完整、来源用途、发现条件、旧 15 项兼容。
- 新建 `ResourceRecipeCatalogIntegrityTests`：配方/资源/建筑/科技引用完整，机器与手工边界，多路线融合条件，非地图材料存在生产来源。
- 新建视觉 manifest 结构测试：stable/visual ID、目标路径和 catalog key 唯一。

**最小 GREEN**

扩展资源与配方定义，消除 `RouteContentDisplayCatalog`、`ResourceIds.All` 和 UI 中的平行名称/配方文案；暂不接 UI。

### Task 3：多输入、多输出与同建筑多配方

**先写 RED**

- 多输入在周期开始时一次性原子预留；任一项不足零扣除。
- 多输出在完成前做完整容量预检；任一项无空间则零产出且保持批次。
- 建筑允许多个有确定顺序的配方；合法切换、非空缓存/进行中切换拒绝原因、同步后保持当前配方。
- schema `31` 复用既有 `definitionId` 与资源数组往返非默认配方，不增 DTO 字段；未知配方继续保留。

**最小 GREEN**

把正式生产定义改为输入/输出数组和建筑允许配方集合；旧采矿、冶炼、装配行为与数值保持不变，再逐批接入路线加工和融合配方。

### Task 4：正式 43 节点研究运行时

**先写 RED**

- 43 节点唯一、无环、顺序确定、前置/成本/图标/效果引用完整；六个桥节点都有双路线前置。
- 单活动研究、研究站资格、暂停、原子成本、取消 80% 退款和 schema `31` 已知/未知内容往返覆盖全部正式节点。
- 恢复解析器认识正式目录，而不是只认识六节点 Demo 投影。

**最小 GREEN**

建立唯一正式研究目录并让正式 3D 运行时、存档、建筑解锁和修改器共同读取；六节点目录仅保留历史兼容测试，不再参与正式 3D 会话。

### Task 5：自下向上树形投影与真实输入

**先写 RED**

- 纯逻辑布局验证层级向上、路线列稳定、桥节点双连线、无重叠和确定性重建。
- 搜索、路线/状态筛选、最新可研究范围、进行中定位和全部节点可见性。
- PlayMode 通过真实 Input System 覆盖 `T`、空白拖动、指针中心滚轮缩放、搜索输入、筛选点击、定位和 Esc；编辑框焦点不得穿透世界或快捷键。

**最小 GREEN**

把科技树从大型 Operations View/Controller 拆为独立视图与控制器；节点和连线只按目录/状态 revision 重建，静态帧不分配。

### Task 6：背包合成、资源状态栏与生产配方选择

**先写 RED**

- 背包和合成动态枚举目录，不再硬编码两条按钮刷新。
- 顶部资源条常驻五基础资源，其他资源按派生发现规则显示；完整账本覆盖全部正式资源并支持滚动/筛选。
- 配方选择显示输入、输出、周期、前置和阻塞原因，命令经正式生产门面提交。
- 所有点击和键盘测试走正式 3D 主循环。

**最小 GREEN**

保留 30 格背包和 20 次手工队列；通用应急配方可手工排队，高级路线配方只供机器生产。

### Task 7：Development 修改器中文双搜索

**先写 RED**

- `0` 键打开；Release 无入口、界面和行为。
- 资源与科技同时支持中文列表查找和输入搜索，显示游戏内中文名而不是函数名/stable ID。
- 资源增加报告实际增加量、部分容量和失败原因；科技支持单项、路线、全部解锁并给出明确中文反馈。

**最小 GREEN**

拆分目录查询、正式命令门面和条件编译 UI；复用统一资源/研究目录和输入焦点保护。

### Task 8：二维 Sprite 管线与首批物品资产

**先写 RED**

- Sprite 导入策略验证 RGBA、透明、Sprite、sRGB、Clamp、无 mipmap、不可读和稳定尺寸。
- manifest/catalog 验证内容全覆盖、未知/重复引用失败、fallback 可用。
- 图像检查验证透明角、安全区和 16/24/32px 联系表可辨认。

**最小 GREEN**

只把用户选中的风格参考和正式源图进入 LFS；先用 15 个既有物品打通生成、去背景、人工目检、导入、catalog 覆盖和 UI 复用，再分路线生成新增物品。

### Task 9：UI、科技、建筑、人物与世界标记正式资产

按 manifest 分批执行，每批都先登记简介、生成一个正式版本、透明/尺寸检查、缩小联系表目检、Unity 接线和聚焦回归：

1. 共用 UI 9-slice、科技树节点/连线/筛选/搜索/状态叠层；
2. 43 个科技图标；
3. 30 个建筑二维图；
4. 男性、遮脸、修长维修指挥者岑烬；
5. 一个通用“极简战术准星”资源节点框，中央插入统一物品图标，下方文本与状态由运行时生成。

资产完成只表示已生成并接入；用户人工视觉结论只能由用户确认。

### Task 10：性能、回归、构建与文档收尾

- 聚焦 EditMode/PlayMode 每个 GREEN 后运行；普通开发运行日常完整 EditMode 与完整 PlayMode。
- 性能门覆盖 43 节点树、全部资源状态投影、配方列表和修改器：重复打开关闭、搜索筛选和 300 帧静态状态下对象/监听器稳定，无逐帧目录重建、无界增长或持续托管分配。
- 更新 `Docs/09-Reusable-Project-Catalog-ZH.md`、`Docs/Engineering/project-quality-catalog.json` 和相关 UI/场景合同。
- 运行项目质量门、Windows Release 3D、Windows Development 3D、macOS universal 3D；发布准备阶段运行 TerrainAssetDeep。
- 运行官方文档生成、验证和 `RecordVerification`，更新 `Docs/Generated/Latest-Verification-ZH.md`。
- 独立静态审查与运行时验证完成后提交并普通 push；不创建 Release、不合并 PR、不 force-push。

## 4. 分批提交建议

1. `docs: design IDEA-0016 route technology and art expansion`
2. `test: specify IDEA-0016 content catalogs`
3. `feat: expand formal resources and recipes`
4. `feat: support selectable multi-input production`
5. `feat: run formal 43-node research catalog`
6. `feat: add bottom-up research tree interaction`
7. `feat: expand crafting resource hud and developer modifier`
8. `feat: add production 2d sprite pipeline`
9. `art: integrate IDEA-0016 production 2d assets`
10. `docs: record IDEA-0016 verification`

每个提交只在对应聚焦测试 GREEN、`git diff --check` 通过且工作区无计划外文件后创建；最终验证通过前，`IDEA-0016` 仍是“未实现”或按真实进度记为“部分实现”，不得写成“已验证”。
