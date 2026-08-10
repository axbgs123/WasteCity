# 第一版美术资源交付阻塞修复实施计划

> 日期：2026-08-10<br>
> 关联需求：`IDEA-0004`<br>
> 基线：`57768b6925013908cbbc8f280a0023a46c6856c1`<br>
> 来源分支：`origin/codex/first-art-pass-terrain@31a9ba65bbe43b6fe05e318a9af53943e0d032c5`<br>
> 目标分支：`codex/first-art-pass-delivery-fixes`

## 1. 目标

把已经通过人工造型/材质审核的第一批美术文件整理成可安全推送、可在 Unity 2022.3.62f1 中稳定导入、状态描述真实的交付包，为后续独立“正式地形表现接入”里程碑提供干净基线。

本轮完成后只代表资源交付与导入合同合格，不代表资源已经进入正式场景、材质已完成、地形已柔和混合，也不把 `IDEA-0004` 标记为已实现或已验证。

## 2. 已确认根因

1. `.gitattributes` 没有覆盖 `.kra` 与 `.ora`，七个分层源文件以普通 Git blob 保存，总计约 129.6 MiB；继续合并原分支会永久放大正常 Git 历史。
2. `Assets/_Game/Art/FirstPass` 没有提交 Unity `.meta` 或导入策略；默认导入会把 Normal、Mask、Height 当作 sRGB 普通纹理，并让静态 FBX 导入动画、相机和灯光。
3. `Docs/05`、`Docs/06` 与原美术生产计划对“已完成源资源”“可导入”“已接线”的描述不完全一致。
4. 当前灰盒地面逐格重置 UV，且没有七类地表混合 Shader；这是后续表现接入问题，不属于本轮交付修复。

## 3. 保护边界

### 3.1 允许修改

- `.gitattributes`
- `ArtSource/FirstPass/**`
- `Assets/_Game/Art/FirstPass/**`
- `Assets/_Game/Editor/FirstArtPassImportPolicy.cs` 与 `.meta`
- `Assets/_Game/Tests/EditMode/FirstArtPassImportPolicyTests.cs` 与 `.meta`
- `Tools/Art/validate_first_art_pass_delivery.py`
- `Docs/Art/FirstPass/**`
- `Docs/05-Formal-Development-Roadmap-ZH.md`
- `Docs/06-User-Feedback-and-Change-Control-ZH.md` 中 `IDEA-0004` 段
- `Docs/superpowers/specs/2026-08-08-first-art-pass-production-design.md`
- `Docs/superpowers/specs/2026-08-08-first-terrain-materials-design.md`
- `Docs/superpowers/plans/2026-08-05-3d-building-placement-and-developer-modifier.md` 中来源分支已经完成的 Task 13 机器/Windows 事实勾选（只原样保留，不再编辑其执行合同）
- `Docs/superpowers/plans/2026-08-08-first-art-pass-production.md`
- 本计划文件

### 3.2 明确禁止

- `Assets/_Game/Scenes/**`
- `Assets/_Game/Scripts/**`
- `Assets/_Game/Rendering/**`
- `Packages/**`
- `ProjectSettings/**`
- schema、存档、玩法地形、通行、资源节点和建造规则
- Shader、Shader Graph、Material、Prefab、VisualLibrary、场景引用和 Build Settings

现有共享 worktree 中 Unity MCP 造成的 `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/PackageManagerSettings.asset` 与 `ProjectSettings/URPProjectSettings.asset` 不复制、不清理、不暂存。

## 4. 提交策略

原美术分支保留不改历史、不 force-push。本分支从 Task 13 完成基线开始，把来源分支最终文件树压成新的交付提交；在写入索引前补齐 LFS 规则，使 `.kra`、`.ora` 和未来 `.psd` 只以 LFS pointer 进入本分支历史。这样不会把来源分支的七个大普通 blob 带入后续集成历史。

## 5. Task A：建立可执行的 LFS 交付门

**Files**

- Create: `Tools/Art/validate_first_art_pass_delivery.py`
- Modify: `.gitattributes`
- Import final tree: `ArtSource/FirstPass/**`、`Assets/_Game/Art/FirstPass/**`、`Docs/Art/FirstPass/**` 与批准规格/计划记录

### A1. RED

先导入来源分支最终文件树，保持原 `.gitattributes`。验证器必须真实读取 Git 索引并失败，至少报告七个 `.kra/.ora` 没有 `filter=lfs` 或索引内容不是 LFS pointer。

验证器同时检查：

- `ArtSource/FirstPass` 与 `Assets/_Game/Art/FirstPass` 下 `.png/.fbx/.blend/.kra/.ora/.psd/.wav` 的属性必须为 LFS；
- 对已跟踪的上述二进制资源读取索引 blob，内容必须是合法 LFS v1 pointer；
- Unity 地形交付必须恰好包含七类，每类恰好 BaseColor、Normal、Mask、Height 四张 PNG；
- Ruins 恰好八个 FBX，Cliff 恰好六个 FBX；
- 校验失败返回非零，输出具体相对路径和原因。

### A2. GREEN

在 `.gitattributes` 增加 `.kra/.ora/.psd` LFS 规则，重新暂存相关资源，让七个大源文件转换为 pointer。再次运行验证器，预期零问题。

### A3. 提交

提交最终资源树、LFS 规则和验证器；提交前运行 `git diff --cached --check`，并证明提交范围没有场景、脚本、Packages 或 ProjectSettings。

## 6. Task B：以真实导入结果冻结 Unity 导入合同

**Files**

- Create: `Assets/_Game/Tests/EditMode/FirstArtPassImportPolicyTests.cs` 与 `.meta`
- Create: `Assets/_Game/Editor/FirstArtPassImportPolicy.cs` 与 `.meta`
- Create: `Assets/_Game/Art/FirstPass/**/*.meta`

### B1. RED

下载 `Assets/_Game/Art/FirstPass/**` 的 LFS 内容，让 Unity 首次按默认设置导入。新增 EditMode 测试，直接读取真实 `TextureImporter` 与 `ModelImporter`：

- 七张 BaseColor：Default、sRGB、Repeat、MipMap、最大 2048；
- 七张 Normal：NormalMap、Linear、Repeat、MipMap、最大 2048；
- 七张 Mask：Default、Linear、RGBA 输入、非透明 Alpha、Repeat、MipMap、Uncompressed、最大 2048；
- 七张 Height：SingleChannel、Linear、Repeat、MipMap、Uncompressed、最大 2048；
- 十四个 FBX：scale 1.0、无动画、无相机、无灯光、无自动 Collider；
- policy 作用域外的现有项目贴图/模型不被修改。

在没有 policy 时先运行 focused 测试，预期只因默认 importer 合同错误而失败，不接受程序集或缺文件错误。

### B2. GREEN

实现仅作用于 `Assets/_Game/Art/FirstPass/` 的 `AssetPostprocessor`：

- BaseColor 使用高质量压缩并保留 sRGB；
- Normal 使用 NormalMap/Linear；
- Mask 和 Height 使用 Linear/Uncompressed；Height 使用 SingleChannel；
- 所有地表贴图 Repeat、Bilinear、MipMap、aniso 4、最大 2048；
- 静态 FBX 关闭 animation/camera/light/collider，保留模型材质槽名称供后续映射；
- 提供只重导入本目录的公开 Editor 命令，不保存场景、不创建材质或 Prefab。

执行重导入后重新运行 focused 测试，预期全部通过。提交 Unity 生成的稳定 `.meta`，第二次重导入前后 `.meta` GUID 列表必须一致。

### B3. 回归

运行完整 EditMode 和 PlayMode；两者 failed 为 0。运行无界面编译并扫描编译错误。测试造成的场景尾随空格只做机械规范化，最终正式场景必须与基线字节一致。

## 7. Task C：修正文档真实状态

**Files**

- Modify: `Docs/05-Formal-Development-Roadmap-ZH.md`
- Modify: `Docs/06-User-Feedback-and-Change-Control-ZH.md` 的 `IDEA-0004`
- Modify: `Docs/superpowers/plans/2026-08-08-first-art-pass-production.md`

文档只记录本轮可证事实：

- 七类地表共 28 张运行候选 PNG 已通过文件与 Unity 导入合同；
- Ruins 八件和 Cliff 六件 FBX 已通过静态导入合同；
- KRA/ORA/Blend/PNG/FBX 已由 LFS 管理；
- `.meta` 已提交且 GUID 稳定；
- 参考图很多，但正式透明 UI Sprite、其余模型、材质、Prefab、Shader、音效和运行时映射仍未完成；
- `IDEA-0004` 保持“已明确 / 已批准 / 开发中”。

旧计划不得把“用户批准参考图”改写成正式 Unity 资源完成，也不得把离线贴图验收改写成场景验收。

## 8. Task D：最终验收与推送

1. `Tools/Art/validate_first_art_pass_delivery.py --treeish HEAD` 零问题；
2. focused importer EditMode 全通过；
3. 完整 EditMode/PlayMode 全通过；
4. 无界面编译成功；
5. 第二次重导入后 importer 测试仍通过，`.meta` GUID 列表不变；
6. `git diff --check` 与提交检查通过；
7. 相对 `57768b6` 的禁止路径差异为空；
8. 本地 HEAD、tracking 与 `ls-remote` 一致后停止。

## 9. 后续独立任务

完成本修复后，才能设计并执行正式地形表现接入。该任务需要单独解决：

- 世界空间 UV：一套 2048 贴图覆盖约 `4×4` 外城格，避免当前逐格 0–1 UV 重复；
- 七类材质主 Shader 与 Mask/Normal/Height 通道；
- Wasteland/Rocky/Wetland/Crystal 的双层柔和混合；
- Ruins/DeepWater/Cliff 清晰但自然的边界；
- Ruins/Cliff 模块 Prefab、稳定表现映射、灰盒回退与性能；
- Release Windows 无粉色材质和真实 1080p Profiler 验收。

这些内容不在本轮顺带实现。
