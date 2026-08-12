# Task 5 fix 2 report — terrain expansion and multi-block rendering

关联记录：`DOC-0001`（已批准、开发中）。

## 修复范围

- `ProjectTestResultAnalyzer` 在每个 `PrimarySourceGlob` 内合并 snapshot 生产源码与受控只读的真实项目文件；物理文件展开限制为仓库相对 glob 的第一层目录，拒绝绝对路径、URI、父路径、通配根目录和无效递归 glob，跳过 `.meta` 与符号链接，只保留实际存在的仓库内相对文件。
- 保持既有顺序：失败测试源码、每个主 glob（组内 ordinal）、可复用资产、场景；使用 ordinal 去重。
- 新增真正非 C# `.asset` fixture 和提交目录的 world-terrain 集成，精确冻结 49 个现存非 meta terrain 文件的顺序与存在性。
- 将 known + unknown、entity、multiline message 与 stack 的完整普通中文报告冻结为逐字预期文本，覆盖两块各自 11 个标题、原始文本、相关文件、事实位置和 shell-safe filter。

## TDD 与验证

- RED：`/tmp/wastecity-project-quality/task-05-fix2/red.xml` 为 `21/18/3/0`，精确暴露非 C# 路径遗漏和越界 glob 未拒绝；renderer 的完整文本测试在修复前已证明现有渲染内容正确，保留为回归锁。
- focused GREEN：`/tmp/wastecity-project-quality/task-05-fix2/green-focused.xml` 为 `21/21/0/0`。
- 相关 Task 3/4 回归：`/tmp/wastecity-project-quality/task-05-fix2/related-green.xml` 为 `116/116/0/0`。
- 完整 PlayMode：`/tmp/wastecity-project-quality/task-05-fix2/playmode.xml` 为 `82/82/0/0`。
- 无界面编译：`/tmp/wastecity-project-quality/task-05-fix2/compile.log` 正常退出，无编译错误。
- 未重试完整 EditMode；此前该门会因受保护 terrain meta 导入循环而不产 XML，不能计为通过。
- 已运行现有 exact-four 生成器；两份结构目录与验证/关注目录均无字节变化。

## 保护与已知项

- 28 个 terrain meta 和两份未跟踪 `ProjectSettings` 均未修改或暂存。
- 按用户决定，未处理既定 Minor：不完整结果文案细分、summary 非中文长尾限制。
