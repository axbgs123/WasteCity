# IDEA-0028 九条正式命轨池实施计划

## 阶段 1：目录、抽取和候选恢复

1. 先扩展 `FormalFateCatalogTests`：九 ID、稳定顺序、唯一文案和 Lv.1/Lv.2 投影。
2. 新增 selector RED：相同输入同序、三个唯一、跨固定输入九条可达。
3. 扩展 `FormalFateRuntimeTests`：任意合法三候选、选择必须属于候选、恢复保序、坏快照原子拒绝。
4. 最小实现目录、selector 和 runtime；选择 Controller 只投影 snapshot 候选。

## 阶段 2：六条 Lv.1 纯规则 owner

1. 因果透明：完整历史访问与基础三原因隔离。
2. 量子纠缠：多聚落基础资源共享路由和断联边界。
3. 虚空宝箱：稳定死亡序号、判定、未领取/已领取幂等。
4. 空间模板：`3×3` 记录、预览、原子提交计划。
5. 预知迟滞：仅消费已安排 Pressure/Campaign 计划。
6. 局部时加：预算、目标、规则增量、暂停/销毁。

每项先建 EditMode RED，再做最小实现；随后增加真实运行消费者测试。不得引用 `WasteCity.Legacy`。

## 阶段 3：schema 36

1. 先写 schema `36` contract、round-trip、validator 和历史旧哈希 RED。
2. 为 `33/34/35` 建立精确旧 progression 投影。
3. 实现 `35→36`，保留旧候选/顺序/选择/等级/三效果，新增状态清洁默认。
4. 扩展 adapter、coordinator、检查点、内部波前重试和回溯锚点往返。

## 阶段 4：坐标锁定

1. RED 覆盖三条件、缺一不可、稳定键幂等、Attention 提升到至少 `90`、Pressure 失败回滚。
2. 接入 Research/Pressure/流程权威事实，不由 UI 轮询生成资格。
3. 增加状态保存与恢复中间阶段测试。

## 阶段 5：UI、真实输入和管理台

1. 三卡 UI 只渲染 offered IDs，并显示九条中实际抽到的三条。
2. 命轨详情改为通用投影；接入模板、时加、预知、箱领取动作。
3. 管理台由目录生成九条中文查询/选择项和状态操作。
4. PlayMode 真实输入覆盖新游戏抽卡、二次确认、详情动作、保存退出/继续、模态不穿透。

## 阶段 6：质量与交付

1. 聚焦 EditMode/PlayMode 与性能稳定门。
2. 日常完整 EditMode（排除 `TerrainAssetDeep`）和完整 PlayMode。
3. `WASTECITY_QUALITY_CHANGED_PATHS`、GenerateDocumentation、ValidateDocumentation、AnalyzeTestResults。
4. 无界面编译、Windows Release 3D、Windows Development 3D、macOS universal 3D。
5. 独立静态审查，修复阻断项。
6. RecordVerification、回写 IDEA-0028、精确暂存、提交和普通 push。

人工试玩与真实 Windows 10/11 视觉、GPU、显存和内存结论始终保留为待验收。
