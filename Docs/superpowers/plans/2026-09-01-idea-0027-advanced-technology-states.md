# IDEA-0027 高级科技状态实施计划

## 1. 目录与 RED

1. 新增 IDEA-0027 目录/发布状态测试，固定 44 节点、49 边、15 个原预览节点的正式可研究性和文明 Lv.2 双门。
2. 新增 `ResearchStatusCatalog` 完整性测试：稳定 ID、来源科技、允许目标、持续/周期、层数与策略。
3. 扩展 `ResearchEffectCatalog` 测试，要求所有已释放效果为 executable/active，UI 不再显示仅预览。

## 2. 状态纯规则

1. 复用并扩展过载、剑意、感染、共鸣、精神操控和护盾模型；补暂停、保存快照、确定性和非递归边界。
2. 新增目标领域窄状态快照与 restore plan；不得由 UI 直接写状态。
3. 增加傀儡容量、巨兽生命、军队再生、基因特质和集体意识研究继承测试。

## 3. schema 35

1. 新增 `researchEffectState` DTO、schema 35 envelope 和 schema 34 专用旧 hash 投影。
2. 先完成 `34→35` 清洁迁移 RED/GREEN，再扩展 validator、codec、store、wave retry 和 rewind anchor 回归。
3. 新增状态 save adapter，纳入正式 coordinator 的 capture/prepare/apply/rollback。
4. 覆盖重复/悬空/未知/越界/非法高水位、旧 hash 篡改、未来版本、重复奖励和全域回滚。

## 4. 正式 3D 消费者

1. Defense 接入过载、剑意、感染传播、共鸣、精神操控、酸液重甲倍率、护盾与自动维修；主战役、压力战役和存档使用同一消费者。
2. BuildingHealth 接入符箓承伤与护盾优先承伤，保存再生/维修周期。
3. Army 接入傀儡容量 4、巨兽 +10%、组织再生和受控单位投影。
4. Character 接入基因特质与首次完成幂等事务。
5. Research 接入集体意识 20% 起始进度；自然完成与 Development 完成走同一完成通知。

## 5. UI 与真实输入

1. 科技树显示所有节点正式状态与完整效果。
2. Defense 详情与核心 HUD 显示状态和护盾；选中激光塔按钮触发过载。
3. M/P 状态徽记接入军队和领袖真值。
4. 0 管理台增加状态列表、中文搜索和正式 facade 命令。
5. PlayMode 通过真实 T、世界选择、鼠标按钮、M/P、0 和 Esc 验证模态优先级与无输入穿透。

## 6. 收口

1. 设置精确 `WASTECITY_QUALITY_CHANGED_PATHS`，更新质量功能组和推荐复用目录。
2. 运行聚焦测试、日常完整 EditMode（排除 TerrainAssetDeep）、完整 PlayMode、AnalyzeTestResults。
3. 运行无界面编译、Windows Release 3D、Windows Development 3D、macOS universal 3D。
4. 运行 GenerateDocumentation、ValidateDocumentation、RecordVerification。
5. 独立静态审查，核对既存地形/美术改动未暂存；回写 IDEA-0027 为“已实现待验证”，提交并普通 push。
