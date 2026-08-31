# IDEA-0026 复合科技效果实施计划

1. RED：新增目录完整性、稳定 ID、44 节点覆盖、可研究节点非纯解锁和御剑术自然可达测试。
2. GREEN：建立 `ResearchEffectCatalog`、`ResearchEffectResolver` 和不可变快照；保留旧摘要兼容。
3. RED→GREEN：生产周期/产量和研究速度接入；完成与 schema 34 恢复结果一致。
4. RED→GREEN：物流、建筑耐久、炮塔射程/伤害/间隔及既有路线规则接入。
5. RED→GREEN：科技树结构化效果、前后对比、预览诚实状态、完成反馈和管理台中文验证。
6. 聚焦 EditMode/PlayMode，随后日常完整 EditMode、完整 PlayMode、静态复审和长局重建检查。
7. 更新 GDD、路线图、变更控制、质量/复用目录；运行官方生成、验证和 `RecordVerification`。
8. 构建 Windows Release、Windows Development 和 macOS universal；普通提交和 push，不合并 PR、不创建 Release。

每一步只显式暂存本任务文件。既有 203 个 Unity 导入/地形改动不进入 IDEA-0026 提交。
