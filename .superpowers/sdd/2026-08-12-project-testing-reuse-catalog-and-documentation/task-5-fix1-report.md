# Task 5 fix 1 report — failure-location review fixes

关联记录：`DOC-0001`（已批准、开发中）。

## 修复范围

- 相关文件改为真实 snapshot 路径：失败测试源码优先，随后按 `PrimarySourceGlobs` 声明顺序展开生产源码、每组 ordinal 排序，再追加已登记的复用资产和场景；不再输出 glob。
- NUnit 3 的非空 `classname` 成为测试类身份的权威来源；缺失或空白时才使用兼容 fallback。复跑筛选保持单一 shell-safe 参数。
- 报告模型携带功能中文名、测试源码和方法；“问题区域”显示中文名，“失败位置”只显示可证实的测试源码/类/方法，摘要只出现在“优先检查”。
- 强化测试覆盖内存映射、unknown、参数化/嵌套/带点 TestName、中文多失败块、原始多行内容、完整 shell filter、loader 成功路径 equality，以及提交 catalog 的 13 项摘要与主路径顺序。

## TDD 与验证

- RED：`/tmp/wastecity-project-quality/task-05-fix/red.xml` 为 `50/44/6/0`；失败精确证明 glob 输出、`classname` 误映射与机器 ID/摘要滥用。
- focused GREEN：`/tmp/wastecity-project-quality/task-05-fix/final-focused.xml` 为 `74/74/0/0`。
- 相关 Task 3/4 回归：`/tmp/wastecity-project-quality/task-05-fix/related-green.xml` 为 `111/111/0/0`。
- 完整 PlayMode：`/tmp/wastecity-project-quality/task-05-fix/playmode.xml` 为 `82/82/0/0`。
- 无界面编译：`/tmp/wastecity-project-quality/task-05-fix/compile.log` 以
  `Exiting batchmode successfully now!` 结束，未发现编译错误。
- 完整 EditMode 由受保护 terrain meta 反复导入卡住，未生成 XML；为释放项目锁已终止本次无结果的批处理进程，不能计作通过。未修改 terrain meta。
- 既有 exact-four 生成接口已运行；两份结构目录内容未变化，验证/关注目录 SHA-256 保持不变。

## 已知低风险项

- 按用户决定保留：不完整结果的中文细分原因与 summary 的非中文长尾限制未在本修复处理。
- 28 个 terrain meta 和两份未跟踪 `ProjectSettings` SHA-256 已重新比较一致；未暂存它们。
