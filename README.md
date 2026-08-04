# Waste City / 《废土移动城市》

《废土移动城市》Unity 正式版内部开发仓库。仓库为 Private，当前不公开发布。

## 固定环境

- Unity：`2022.3.62f1`（revision `4af31df58517`）
- 渲染：URP 2D Renderer
- 平台：Windows 10/11 64 位
- 输入：Unity Input System
- 版本控制：Git + Git LFS
- 当前分支：`master`

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

1. 打开 `Assets/_Game/Scenes/FormalPrototype.unity`；
2. 确认 Console 无持续错误；
3. 运行全部 EditMode 和 PlayMode 测试；
4. 通过菜单或批处理调用 `WasteCity.Editor.FormalBuildTools.BuildWindows`；
5. 启动 `Builds/Windows/WasteCity.exe` 做独立运行冒烟；
6. 开发前执行 `git status`，不要覆盖未提交改动。

当前自动化与构建基线（`6200703`）：

- EditMode：`252/252`；
- PlayMode：`34/34`；
- Unity 无界面编译：0 错误；
- Windows 构建：成功；
- 存档 schema：`27`；
- 统一友军集结点、随城回归、死亡统计和组织再生已经实现；
- 血肉路线感染闭环已经实现：生物伤害叠层、持续伤害、满层传播、精神控制清除、稳定美术替换槽和存档恢复；
- 科技路线过载与无人系统已经实现：2×攻速、能量伤害 +30%、过热停火、侦查无人机、自动维修机甲反馈和状态恢复；
- 修仙路线剑意、御剑台与傀儡维护已经实现：飞剑命中叠层、20 层真实斩杀、御剑术射程升级、能晶断供休眠/补给恢复、稳定美术替换槽和状态恢复；
- 当前基线的 Direct3D 11 独立运行冒烟待真实 Windows 10/11 电脑补验。上一正式基线曾完成 12 秒冒烟；当前基线补验前不作为候选发布版。

## 仓库目录

- `Assets/`：Unity 游戏代码、场景、测试和占位资源；
- `Packages/`：Unity 包清单和锁文件；
- `ProjectSettings/`：Unity 工程设置与精确编辑器版本；
- `Docs/`：主 GDD、历史计划、当前进度与正式版后续路线图；
- `ArtDesign/`：美术 Bible、风格规范、提示词、资产清单和基线参考图；
- `.gitattributes`：Unity YAML 合并和 Git LFS 规则；
- `.gitignore`：Unity 本地生成目录排除规则。

## 文档优先级

1. `Docs/05-Formal-Development-Roadmap-ZH.md`：当前正式版实现顺序与质量门；
2. `Docs/01-Game-Design-Document-ZH.md`：主 GDD 和正式版目标；
3. `Docs/00-README-ZH.md`：状态、交接与文档索引；
4. `Docs/04-Minimum-Releasable-Version-Plan-ZH.md`：历史 MRV；
5. `Docs/02-Demo-Implementation-Plan-ZH.md`：历史 Demo 实施计划；
6. `Docs/03-Legacy-Progress-Notes-ZH.md`：早期记录。

## 美术替换约定

当前大部分表现是占位符。正式资源通过稳定 ID、`VisualSlot`、`VisualDefinition` 和 `VisualLibrary` 替换，不应把碰撞、生命、攻击或存档状态放入纯美术 Prefab。详细要求见 `Docs/05-Formal-Development-Roadmap-ZH.md` 与 `ArtDesign/README.md`。

## GitHub 保密要求

- 仓库必须保持 **Private**；
- 不创建公开 Release、Pages 或商店页面；
- 不提交令牌、凭据、个人存档和许可证文件；
- PNG、FBX、Blend、WAV 等大型资产通过 Git LFS 管理。
