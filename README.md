# Waste City / 《废土移动城市》

《废土移动城市》Unity 正式版内部开发仓库。仓库为 Private，当前不公开发布。

## 固定环境

- Unity：`2022.3.62f1`（revision `4af31df58517`）
- 渲染：默认 `GrayboxPrototype3D` 使用场景作用域 URP；冻结的 `FormalPrototype` 保留 2D Renderer 作为回归基线
- 平台：Windows 10/11 64 位
- 输入：Unity Input System
- 版本控制：Git + Git LFS
- 默认编辑/正式构建入口：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`

Unity Hub 必须安装上述精确编辑器版本以及 Windows Build Support。工程版本也记录在 `ProjectSettings/ProjectVersion.txt`。

## 新电脑接手

新电脑需要能够访问本 Private 仓库，并安装 Git、Git LFS、Unity Hub 和 Unity `2022.3.62f1`。

```powershell
git lfs install
git clone https://github.com/axbgs123/WasteCity.git
Set-Location WasteCity
git lfs pull
git status
```

然后在 Unity Hub 中选择“添加/打开”，指向克隆得到的 `WasteCity` 根目录。首次打开由 Unity 依据 `Packages/manifest.json` 和 `Packages/packages-lock.json` 恢复依赖；不要复制或提交其他电脑的 `Library/`、`Temp/`、`Logs/`、`Builds/`、`TestResults/` 或 `UserSettings/`。

## 首次验收

1. 打开默认场景 `Assets/_Game/Scenes/GrayboxPrototype3D.unity`；
2. 确认 Console 无持续错误；
3. 运行全部 EditMode 和 PlayMode 测试；
4. 通过菜单或批处理调用 `WasteCity.Editor.FormalBuildTools.BuildWindows` 构建默认 3D 版本；
5. 启动 `Builds/Windows/WasteCity.exe` 做独立运行冒烟；
6. 如需冻结 2D 回归产物，调用 `WasteCity.Editor.FormalBuildTools.BuildWindowsLegacy2D`，输出为 `Builds/Windows2D/WasteCity2D.exe`；
7. 开发前执行 `git status`，不要覆盖未提交改动。

当前自动化与构建基线（`8ce573a`）：

- 最新的 EditMode、PlayMode、编译、构建和人工试玩状态见[最新验证快照](Docs/Generated/Latest-Verification-ZH.md)；这里不手工维护会过期的测试数量。
- Unity 无界面编译：0 错误；
- 默认 3D Windows 构建和显式 2D 回归构建均成功，两个产物均为 `PE32+ executable (GUI) x86-64`；
- 存档 schema：`30`；首个 3D 基础阶段仍不读写正式存档；
- 统一友军集结点、随城回归、死亡统计和组织再生已经实现；
- 血肉路线感染闭环已经实现：生物伤害叠层、持续伤害、满层传播、精神控制清除、稳定美术替换槽和存档恢复；
- 科技路线过载与无人系统已经实现：2×攻速、能量伤害 +30%、过热停火、侦查无人机、自动维修机甲反馈和状态恢复；
- 修仙路线剑意、御剑台与傀儡维护已经实现：飞剑命中叠层、20 层真实斩杀、御剑术射程升级、能晶断供休眠/补给恢复、稳定美术替换槽和状态恢复；
- 灵能路线共鸣与集体意识单城效果已经实现：5 秒共鸣标记、最多 10 个目标、30% 灵能伤害同步、新研究继承 20% 进度、稳定美术替换槽和状态恢复；
- 四路线终端建筑已经补齐：发电站、聚灵阵、代谢炉、意识网络均具有人口/研究门槛、物流生产、占位美术槽和存档恢复；当前能源币/灵石继续以能晶代理，精神力结晶以灵能增幅器代理；
- 当前基线的 Direct3D 11 独立运行冒烟待真实 Windows 10/11 电脑补验。上一正式基线曾完成 12 秒冒烟；当前基线补验前不作为候选发布版。

## 仓库目录

- `Assets/`：Unity 游戏代码、场景、测试和占位资源；
- `Packages/`：Unity 包清单和锁文件；
- `ProjectSettings/`：Unity 工程设置与精确编辑器版本；
- `Docs/`：主 GDD、历史计划、当前进度与正式版后续路线图；
- `Docs/06-User-Feedback-and-Change-Control-ZH.md`：用户 Bug、新构思、文档变更及实现状态的开发前必读入口；
- `ArtDesign/`：美术 Bible、风格规范、提示词、资产清单和基线参考图；
- `.gitattributes`：Unity YAML 合并和 Git LFS 规则；
- `.gitignore`：Unity 本地生成目录排除规则。

## 文档优先级

开发前必须先阅读 `Docs/06-User-Feedback-and-Change-Control-ZH.md`。它记录用户最新反馈和已批准变更；登记不等于批准，批准不等于实现，实现不等于验证。

1. `Docs/05-Formal-Development-Roadmap-ZH.md`：当前正式版实现顺序与质量门；
2. `Docs/01-Game-Design-Document-ZH.md`：主 GDD 和正式版目标；
3. `Docs/00-README-ZH.md`：状态、交接与文档索引；
4. `Docs/04-Minimum-Releasable-Version-Plan-ZH.md`：历史 MRV；
5. `Docs/02-Demo-Implementation-Plan-ZH.md`：历史 Demo 实施计划；
6. `Docs/03-Legacy-Progress-Notes-ZH.md`：早期记录。

## 项目质量与复用入口

- [用户反馈与变更控制](Docs/06-User-Feedback-and-Change-Control-ZH.md)：确认批准状态和人工验收结论；
- [项目使用与开发入门](Docs/07-Project-Use-and-Development-Guide-ZH.md)：从默认场景和基本操作开始；
- [测试与 Bug 定位指南](Docs/08-Testing-and-Bug-Location-Guide-ZH.md)：按功能选择测试，阅读失败定位；
- [可复用项目目录](Docs/09-Reusable-Project-Catalog-ZH.md)：选择已有能力时先看用途和边界；
- [项目自动清单](Docs/Generated/Project-Inventory-ZH.md)、[测试自动清单](Docs/Generated/Test-Inventory-ZH.md)：当前文件、场景、组件与测试入口；
- [最新验证快照](Docs/Generated/Latest-Verification-ZH.md)、[文档关注提醒](Docs/Generated/Documentation-Attention-ZH.md)：最近证据和需要人工检查的文档提醒。

## 受控文档更新流程

完成一次有仓库改动的审查时，先在交付说明写明已经确认的比较基线和审查范围。`TASK_PATHS` 只写本任务已批准的精确路径；不要用全仓库 `git diff`，也不要把用户工作区里所有未提交内容当成本次改动。

### A. 已提交审查

当只审查已经提交的范围时，比较明确基线到 `HEAD`。这不会收集 index、工作区或未跟踪文件：

```sh
PROJECT_ROOT="$(git rev-parse --show-toplevel)"
REVIEW_BASE=<已明确的基线提交>
TASK_PATHS=("精确路径1" "精确路径2")
CHANGED_PATHS=/tmp/wastecity-project-quality/changed-paths.txt
mkdir -p /tmp/wastecity-project-quality
git diff --name-only "$REVIEW_BASE"...HEAD -- "${TASK_PATHS[@]}" > "$CHANGED_PATHS"
```

### B. 提交前正常开发

完成门通常在提交前运行。对同一份 `TASK_PATHS`，必须合并四类来源：基线后的已提交改动、index、已跟踪工作区改动和未跟踪文件。这样能收集本任务尚未提交的内容，却不会无差别收集用户的无关脏改：

```sh
PROJECT_ROOT="$(git rev-parse --show-toplevel)"
REVIEW_BASE=<已明确的基线提交>
TASK_PATHS=("精确路径1" "精确路径2")
CHANGED_PATHS=/tmp/wastecity-project-quality/changed-paths.txt
mkdir -p /tmp/wastecity-project-quality
{
  git diff --name-only "$REVIEW_BASE"...HEAD -- "${TASK_PATHS[@]}"
  git diff --cached --name-only -- "${TASK_PATHS[@]}"
  git diff --name-only -- "${TASK_PATHS[@]}"
  git ls-files --others --exclude-standard -- "${TASK_PATHS[@]}"
} | LC_ALL=C sort -u > "$CHANGED_PATHS"
```

确认 `CHANGED_PATHS` 只包含批准范围后，用它生成和验证：

```sh
WASTECITY_QUALITY_CHANGED_PATHS="$CHANGED_PATHS" \
  "$UNITY_BIN" -batchmode -quit -projectPath "$PROJECT_ROOT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.GenerateDocumentation
WASTECITY_QUALITY_CHANGED_PATHS="$CHANGED_PATHS" \
  "$UNITY_BIN" -batchmode -quit -projectPath "$PROJECT_ROOT" \
  -executeMethod WasteCity.Editor.ProjectQuality.ProjectQualityTools.ValidateDocumentation
```

其中 `UNITY_BIN` 使用本机 Unity `2022.3.62f1` 的可执行文件。生成前输入清单只用于本轮文档关注提醒，不等于生成后最终暂存清单；步骤 9 仍要单独核对最终精确暂存清单和受保护文件。交付说明必须记录 `REVIEW_BASE`、`TASK_PATHS` 和 `CHANGED_PATHS` 所用清单，方便之后复查。只有用户明确确认“本次没有仓库变更”时，才可先创建空的 UTF-8 文件再运行上述命令；缺少 `WASTECITY_QUALITY_CHANGED_PATHS` 不是普通完成路径。生成和验证只更新或检查受控技术附录，不会替代人工试玩、审批或验收。

上面的命令块仅适用于 macOS/Linux shell。Windows 请在 PowerShell 中用同一份精确路径集合分别运行四类 Git 查询，并用 `Sort-Object -Unique | Set-Content -Encoding utf8` 合并为清单；随后按[测试与 Bug 定位指南](Docs/08-Testing-and-Bug-Location-Guide-ZH.md)的 Windows 入口运行 Unity，不要把 Bash 命令直接当成 Windows 通用命令。

## 美术替换约定

当前大部分表现是占位符。正式资源通过稳定 ID、`VisualSlot`、`VisualDefinition` 和 `VisualLibrary` 替换，不应把碰撞、生命、攻击或存档状态放入纯美术 Prefab。详细要求见 `Docs/05-Formal-Development-Roadmap-ZH.md` 与 `ArtDesign/README.md`。

## GitHub 保密要求

- 仓库必须保持 **Private**；
- 不创建公开 Release、Pages 或商店页面；
- 不提交令牌、凭据、个人存档和许可证文件；
- PNG、FBX、Blend、WAV 等大型资产通过 Git LFS 管理。
