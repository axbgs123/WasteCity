# User Feedback and Change Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立一份可在 GitHub 持续更新的用户反馈与变更控制文档，并强制所有开发者在开始任务前阅读。

**Architecture:** 使用单一动态补充文档保存 Bug、新构思和文档变更，审批状态与实现状态独立记录。根目录 `AGENTS.md` 提供仓库级必读门，README 和交接说明提供人类可见入口；所有更新通过普通 Git 提交和非强制推送同步。

**Tech Stack:** Markdown, Git, GitHub, repository-level `AGENTS.md`

## Global Constraints

- 新记录默认状态必须是 `待确认 + 未实现`。
- 只有审批状态为 `已批准` 的记录才能修改正式文档或进入实现。
- 实现状态固定为 `未实现`、`开发中`、`已实现待验证`、`已验证`、`不适用`。
- 补充文档不得把计划、批准或开发中内容描述成已实现事实。
- 每次新增记录或状态变化必须单独提交并推送当前开发分支，不强制推送。
- 不提交本机 Unity 离线包路径、生成缓存、凭据或许可证。
- 不自动合并默认分支，不创建公开 Release。

---

### Task 1: Create the Living Feedback Ledger

**Files:**
- Create: `Docs/06-User-Feedback-and-Change-Control-ZH.md`

**Interfaces:**
- Consumes: `Docs/superpowers/specs/2026-08-04-user-feedback-change-control-design.md`
- Produces: 所有后续开发者必须读取的统一记录入口与三类记录模板

- [x] **Step 1: Create the document header and authority rules**

写入项目名、最后更新时间、文档用途、先登记后生效规则，以及“登记不等于批准、批准不等于实现、实现不等于验证”声明。

- [x] **Step 2: Add exact status definitions**

审批状态写为：

```text
待确认 / 已批准 / 已拒绝 / 已撤回
```

实现状态写为：

```text
未实现 / 开发中 / 已实现待验证 / 已验证 / 不适用
```

明确新记录默认 `待确认 + 未实现`，只有 `已批准` 才能进入正式文档修改或开发。

- [x] **Step 3: Add the active-record summary**

使用以下固定列：

```markdown
| ID | 类型 | 标题 | 优先级 | 审批状态 | 实现状态 | 最近更新 | 关联内容 |
|---|---|---|---|---|---|---|---|
```

初始状态写明“当前没有待处理记录”，不得创建虚构 Bug 或需求。

- [x] **Step 4: Add detailed templates**

分别提供 `BUG-0001`、`IDEA-0001`、`DOC-0001` 模板。每个模板包含：

```text
提出日期
最近更新
用户原始描述
复现步骤或设计动机
预期结果
实际结果（Bug）
优先级
审批状态
实现状态
影响范围
关联正式文档
关联提交
验收条件
验证证据
决策原因或备注
```

- [x] **Step 5: Add update and GitHub rules**

明确编号不可复用、状态转换、原文档双向引用、独立提交、非强制推送、GitHub 不可用时先本地提交，以及禁止提交本机缓存和绝对路径。

- [x] **Step 6: Run document checks**

```bash
rg -n "待确认|已批准|已实现待验证|已验证|BUG-0001|IDEA-0001|DOC-0001" Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --check -- Docs/06-User-Feedback-and-Change-Control-ZH.md
```

要求所有固定状态和三类模板均可检索，且格式检查通过。

### Task 2: Enforce the Developer Read Gate

**Files:**
- Create: `AGENTS.md`
- Modify: `README.md`
- Modify: `Docs/00-README-ZH.md`

**Interfaces:**
- Consumes: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Produces: 仓库级强制阅读规则及两个人类可见文档入口

- [x] **Step 1: Create root AGENTS.md**

要求所有人类开发者和 AI 代理在分析、设计、计划、编码、测试或修改文档前：

1. 阅读补充文档；
2. 检查相关 `已批准`、`开发中`、`已实现待验证` 记录；
3. 在计划和提交中引用相关稳定 ID；
4. 禁止实现 `待确认`、`已拒绝` 或 `已撤回` 的内容；
5. 完成后回写实现状态、提交哈希和验证证据；
6. 用户提出反馈时先更新补充文档并推送 GitHub。

- [x] **Step 2: Update README document priority**

在“文档优先级”前加入开发启动入口：

```text
开发前先读 Docs/06-User-Feedback-and-Change-Control-ZH.md。
它记录用户最新反馈和已批准变更；登记不等于批准，批准不等于实现。
```

同时将补充文档列入仓库目录说明。

- [x] **Step 3: Update the handoff index**

在 `Docs/00-README-ZH.md` 的文件优先级顶部加入补充文档，并说明：

- 每次开发启动前必读；
- 已批准记录要求同步修改正式文档；
- 实现状态必须有提交和验证证据；
- 未批准记录不得扩大开发范围。

- [x] **Step 4: Verify all entry points**

```bash
rg -n "06-User-Feedback-and-Change-Control-ZH.md" AGENTS.md README.md Docs/00-README-ZH.md
rg -n "待确认|已批准|已实现待验证|已验证" AGENTS.md Docs/06-User-Feedback-and-Change-Control-ZH.md
git diff --check -- AGENTS.md README.md Docs/00-README-ZH.md
```

要求三个入口文件均指向同一补充文档，强制规则与状态名称一致。

### Task 3: Verify, Commit, and Push

**Files:**
- Create: `Docs/06-User-Feedback-and-Change-Control-ZH.md`
- Create: `AGENTS.md`
- Modify: `README.md`
- Modify: `Docs/00-README-ZH.md`
- Modify: `Docs/superpowers/plans/2026-08-04-user-feedback-change-control.md`

**Interfaces:**
- Consumes: Task 1-2 的完整文档变更
- Produces: GitHub 上可追溯、可供所有开发者读取的正式流程

- [x] **Step 1: Mark completed plan steps**

只将已经实际执行并验证的步骤从 `[ ]` 更新为 `[x]`。

- [x] **Step 2: Check repository scope**

```bash
git status --short
git diff --check
git diff --name-only
```

确认计划提交只包含：

```text
AGENTS.md
README.md
Docs/00-README-ZH.md
Docs/06-User-Feedback-and-Change-Control-ZH.md
Docs/superpowers/plans/2026-08-04-user-feedback-change-control.md
```

本机离线包和 `ProjectSettings/PackageManagerSettings.asset` 不得暂存。

- [x] **Step 3: Commit the implementation**

```bash
git add AGENTS.md README.md Docs/00-README-ZH.md Docs/06-User-Feedback-and-Change-Control-ZH.md Docs/superpowers/plans/2026-08-04-user-feedback-change-control.md
git diff --cached --check
git diff --cached --name-only
git commit -m "docs: add user feedback control"
```

- [ ] **Step 4: Push without force**

```bash
git push
```

- [ ] **Step 5: Verify remote parity**

```bash
local_head=$(git rev-parse HEAD)
remote_head=$(git ls-remote --heads origin refs/heads/codex/fix-foundation | awk '{print $1}')
test "$local_head" = "$remote_head"
```

要求本地与远端提交哈希完全一致。
