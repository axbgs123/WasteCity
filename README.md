# Waste City / 《废土移动城市》

一款以移动城市为核心，融合资源采集、生产物流、科技发展、城市防御、探索与文明升阶的俯视角废土策略经营游戏。

> 项目正在持续开发，目前公开的是可运行、可测试的开发版本，不是正式发行版。大量建筑、角色和单位仍使用可替换的二维贴图或占位表现。

## 游戏简介

灾后世界中，幸存者依靠一座能够移动和展开的城市寻找资源。玩家需要选择落脚点，建立生产与仓储网络，研究不同文明路线，抵御逐渐增强的威胁，并在资源、关注度、命轨与文明发展之间作出取舍。

核心循环：

```text
驾驶移动城市探索世界
→ 选择位置并展开城市
→ 采集、生产、仓储与合成
→ 研究科技并建设防御
→ 应对敌袭、压力事件与资源危机
→ 保留前哨或处理外城资产后撤离
→ 扩张文明并继续探索
```

## 当前可体验内容

- `64×48` 确定性 3D 地图、七类地形、24 个固定资源节点与战争迷雾；
- 移动城市驾驶、自动寻路、展开/收起、内外城双网格建造和撤离；
- 建筑旋转、半透明预览、放置合法性反馈、采矿位置引导和精确缺料提示；
- 31 种资源、生产配方、真实仓库、单资源过滤、30 格背包与应急合成；
- 自下向上的 44 节点科技树、四条发展路线、六个跨路线桥节点与复合科技效果；
- 三类防御塔、三类敌人、十波单城防御战役、建筑受击、胜负结算和最近波前重试；
- 关注度、压力事件、九条正式命轨候选池和文明 `Lv.1 → Lv.2` 升阶；
- 单小队远征、主城/次城/前哨、实体运输、角色救援、外交与继承；
- 岑烬求救、领袖直接控制与手工采集、最后情报和前哨三级警报；
- schema `37` 正式 3D 单槽存档、自动检查点、主档校验和备份恢复；
- Editor/Development Build 专用中文验收管理台，支持资源与科技检索。

功能已经通过自动化和跨平台构建，不代表所有画面、节奏和手感均已完成人工验收。最新测试、构建和待验证边界请以[最近验证快照](Docs/Generated/Latest-Verification-ZH.md)为准。

## 当前开发重点

- 统一地图、移动城市、建筑、资源点与图标的视觉比例；
- 完善文明式地图层次和工业废土美术语言；
- 优化资源栏、建造、背包、仓库、合成、科技树及多城市界面；
- 逐步替换建筑、角色、单位、特效和音频占位资源；
- 持续验证生产、战斗、探索、前哨和文明进程的长期平衡。

现阶段不会把透明建筑贴图、公告板或程序化对象描述成正式三维模型。真实 Windows 10/11 的视觉、GPU、显存与内存验收，以及当前版本的完整用户试玩，仍等待实际执行。

## 技术信息

| 项目 | 当前配置 |
|---|---|
| 引擎 | Unity `2022.3.62f1`（revision `4af31df58517`） |
| 渲染 | URP，现役 3D 场景作用域管线 |
| 输入 | Unity Input System，键盘与鼠标 |
| 目标平台 | Windows 10/11 64 位；同时维护 macOS universal 构建 |
| 默认场景 | `Assets/_Game/Scenes/GrayboxPrototype3D.unity` |
| 版本控制 | Git + Git LFS |
| 当前存档 | Formal 3D schema `37` |

历史 2D `FormalPrototype` 场景及其专属构建入口已经退役。schema `1–30` 的识别、解码、迁移样本和仍被 3D 使用的共享规则继续保留，但不会被静默解释为当前 3D 世界。

## 获取并运行项目

### 环境要求

- Git 与 Git LFS；
- Unity Hub；
- Unity `2022.3.62f1`；
- 构建 Windows 版本时安装对应的 Windows Build Support 模块。

### 克隆

```bash
git lfs install
git clone https://github.com/axbgs123/WasteCity.git
cd WasteCity
git lfs pull
git lfs fsck
git status
```

请确认 Git LFS 已拉取真实资源，不要把 LFS pointer 文本当作图片、模型或音频文件。

### 在 Unity 中运行

1. 在 Unity Hub 中选择“添加/打开”，指向仓库根目录；
2. 等待 Unity 根据 `Packages/manifest.json` 恢复依赖并完成资源导入；
3. 打开 `Assets/_Game/Scenes/GrayboxPrototype3D.unity`；
4. 确认 Console 没有持续错误后进入 Play Mode；
5. 从启动页选择“新游戏”或载入可兼容的正式 3D 存档。

不要提交本机生成的 `Library/`、`Temp/`、`Logs/`、`Builds/`、`TestResults/` 或 `UserSettings/`。

## 主要操作

| 按键 | 功能 |
|---|---|
| `WASD` | 驾驶当前城市或控制领袖 |
| 鼠标右键 | 设置城市自动驾驶目标；在特定操作中取消目标 |
| 鼠标中键 | 拖动镜头 |
| 鼠标滚轮 | 缩放镜头或科技树 |
| `Home` | 返回当前控制目标；科技树中恢复全览 |
| `B` | 打开建造目录 |
| `R` | 旋转待放置建筑 |
| `F` | 展开、收起或进入撤离处理 |
| `E` | 打开背包与合成 |
| `T` | 打开科技树 |
| `M` | 军队与远征 |
| `N` | 城市、前哨与运输 |
| `P` | 角色、内政与外交 |
| `U` | 条件满足时开始文明升阶 |
| `Space` | 战术暂停 |
| `Esc` | 逐级取消当前操作或打开系统菜单 |
| `0` | 仅在 Editor/Development Build 中打开验收管理台 |

界面输入具有优先级。搜索框或其他文本控件获得焦点时，键盘输入不应穿透到城市、建造和世界操作。

## 测试与构建

日常开发应先运行与改动对应的聚焦测试，再运行完整 EditMode 与 PlayMode。只有修改地形源贴图、导入规则、Texture2DArray Builder、数组生成，或准备发布候选时，才运行 `TerrainAssetDeep`。

现役正式构建入口：

- `WasteCity.Editor.FormalBuildTools.BuildWindows`：Windows Release 3D；
- `WasteCity.Editor.FormalBuildTools.BuildWindowsGraybox3DDevelopment`：Windows Development 3D；
- `WasteCity.Editor.FormalBuildTools.BuildMacOSGraybox3D`：macOS universal 3D。

测试选择、失败定位、批处理方式与证据要求详见[测试与 Bug 定位指南](Docs/08-Testing-and-Bug-Location-Guide-ZH.md)。

## 仓库结构

- `Assets/`：Unity 游戏代码、场景、测试和运行时资源；
- `Packages/`：Unity 包清单和锁文件；
- `ProjectSettings/`：工程设置和固定编辑器版本；
- `Docs/`：GDD、正式路线图、需求变更、测试与复用文档；
- `ArtDesign/`：美术 Bible、比例规范、提示词和当前视觉基线；
- `ArtSource/`：可追溯的美术源文件与参考资产；
- `.gitattributes`：Unity YAML 合并与 Git LFS 规则；
- `.gitignore`：Unity 本地生成目录排除规则。

## 文档导航

- [文档总索引](Docs/README.md)
- [游戏设计文档](Docs/01-Game-Design-Document-ZH.md)
- [正式开发路线图](Docs/05-Formal-Development-Roadmap-ZH.md)
- [用户反馈与变更控制](Docs/06-User-Feedback-and-Change-Control-ZH.md)
- [项目使用与开发入门](Docs/07-Project-Use-and-Development-Guide-ZH.md)
- [测试与 Bug 定位指南](Docs/08-Testing-and-Bug-Location-Guide-ZH.md)
- [可复用项目目录](Docs/09-Reusable-Project-Catalog-ZH.md)
- [最近验证快照](Docs/Generated/Latest-Verification-ZH.md)

## 参与开发

提交代码或文档前请完整阅读 [`AGENTS.md`](AGENTS.md) 和[用户反馈与变更控制](Docs/06-User-Feedback-and-Change-Control-ZH.md)。新功能、玩法变更和 Bug 修复需要对应稳定 `IDEA-`、`BUG-` 或 `DOC-` 编号，并遵守测试、文档生成、验证和普通 Git 提交流程。

请不要提交账号凭据、Unity 许可证、本机缓存、临时构建或机器专用绝对路径。大型 PNG、FBX、Blend、WAV 等资源应按仓库现有规则通过 Git LFS 管理。

## 许可证

本仓库目前尚未提供开源许可证。仓库公开可见不代表自动授权复制、修改、分发或商业使用；如需使用项目中的代码或美术资源，请先联系仓库所有者取得许可。
