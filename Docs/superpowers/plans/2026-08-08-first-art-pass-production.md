# 第一版正式美术样板包生产实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在建造里程碑 Task 13 通过后，生产一套来源可追溯、格式统一、通过导入前验证的正式美术样板源资源，为后续独立 Unity 表现映射里程碑提供稳定交接包。

**Architecture:** 本计划只负责美术前期、源资源、导入资源、登记表和离线/Unity 导入前验证，不修改玩法、场景、VisualSlot 或正式运行时。资源以 `ArtSource/FirstPass/` 保存源工程，以 `Assets/_Game/Art/FirstPass/` 保存 Unity 可导入文件；采矿站先走完整黄金样板流程，模板冻结后再并行生产城市/角色/建筑、UI、环境与反馈三条轨道。

**Tech Stack:** Blender 5.2.0 LTS、Blender MCP v1.2、FBX、PNG、SVG、WAV 48 kHz/24-bit、Unity 2022.3.62f1、URP 14.0.12、Git LFS、Unity Test Framework、macOS 开发机与 Windows x86-64 构建验收。

## Global Constraints

- 关联需求固定为 `IDEA-0004`，设计规格固定为 `Docs/superpowers/specs/2026-08-08-first-art-pass-production-design.md`。
- Task 13 未通过全部测试、构建、性能与文档门时，本计划不得开始。
- 本计划不修改 `Assets/_Game/Scenes/`、`Assets/_Game/Scripts/`、`Assets/_Game/Editor/GrayboxSceneAuthoring.cs`、`Packages/`、`ProjectSettings/`、schema、存档或玩法定义。
- 本计划结束时不把正式资源接入运行时；Unity 表现映射必须在 Task 13 最终接口上另写设计规格和实施计划。
- 模型源文件使用 `.blend`；Unity 模型主交付与交换格式固定为 `.fbx`。glTF 2.0/GLB 只作为次要预览或跨工具交换格式，不得替代 FBX 正式交付；Unity 图片使用 PNG，矢量源使用 SVG，音频源使用 48 kHz、24-bit WAV。
- 制作机基线为 Blender 5.2.0 LTS 与 Blender MCP v1.2，并启用 FBX、glTF 2.0、UV Layout、SVG、BVH、Cycles 和 Pose Library；MCP 与这些扩展只服务于制作流程，不作为 Unity 运行时依赖。
- 外城模型尺寸读取 Task 13 最终 `BuildingDefinition`；内城平台固定 `8×6`、每格 `0.32`、完整表面 `2.56×1.92`。
- 模型导入比例 `1.0`，`Y` 向上，`+Z` 为正面，静态建筑根位于占地中心且底面为 `Y=0`。
- 五座样板建筑必须支持 `0°/90°/180°/270°`，第一版以一个主要 Renderer 为目标。
- 所有建模类资产必须先生成一张可执行参考图或参考板并取得用户明确批准，之后才能开始 Blender 白模或正式建模；参考图需登记来源、生成方法、许可证与批准日期。
- BaseColor 为 sRGB；Normal、Mask 和其他数据贴图为 Linear；URP Mask Map 为 R Metallic、G Occlusion、B Detail Mask、A Smoothness。
- 第一版地形固定为七类：`Wasteland`、`Rocky`、`Wetland`、`Crystal`、`Ruins`、`DeepWater`、`Cliff`；不得自行合并、改名或把资源节点类别混入地形类别。
- `Crystal` 是能晶化地表材质，`ResourceNodes/EnergyCrystal` 是可采集能晶节点，两者必须是独立资产；地表只表达视觉材质，不得持有资源节点、产量、储量、可采集状态或任何玩法真值。
- 地形采用风格化 PBR，以暖黄干旱废土为母底色，贴图为主、少量模型装饰为辅；`Wasteland`、`Rocky`、`Wetland`、`Crystal` 使用柔和混合，`Ruins` 以柔化碎边自然衔接但不模糊规则范围，`DeepWater` 和 `Cliff` 必须维持清晰可读的玩法边界。
- 每类正式地形的基础交付为 2048×2048 无缝 BaseColor、Tangent Space Normal、URP Mask 和 16-bit Height；不得只完成 BaseColor 后把其余数据贴图登记为完成。
- 资源不得持有成本、施工、生命、攻击、占地、解锁、城市状态或存档真值。
- 所有 `.png`、`.fbx`、`.blend`、`.kra`、`.ora`、`.psd`、`.wav` 必须使用 Git LFS。2026-08-10 的交付修复已补齐此前遗漏的 KRA/ORA/PSD 规则；后续新增二进制源文件必须先通过 `Tools/Art/validate_first_art_pass_delivery.py`，不得再次以普通 Git blob 提交。
- 所有外部资源必须记录作者、来源、许可证、商业使用权和证据路径；许可证不明确的资源不得进入交接包。
- Unity MCP/Package Manager 产生的 `Packages/manifest.json`、`Packages/packages-lock.json`、`ProjectSettings/PackageManagerSettings.asset`、`ProjectSettings/URPProjectSettings.asset` 始终视为受保护外部改动，除非另有独立批准。

---

## File Structure

执行完成后只允许新增或修改以下资源根和本计划指定的测试/登记文件：

```text
ArtSource/FirstPass/
├── ArtBible/
├── MobileCity/
├── Characters/Leader_CenJin/
├── Buildings/{MiningStation,Housing,Warehouse,Wall,MachineGunTurret}/
├── Environment/{Terrain,ResourceNodes,Props}/
├── Construction/
├── UI/
├── VFX/
└── Audio/

Assets/_Game/Art/FirstPass/
├── ArtBible/
├── MobileCity/
├── Characters/Leader_CenJin/
├── Buildings/{MiningStation,Housing,Warehouse,Wall,MachineGunTurret}/
├── Environment/{Terrain,ResourceNodes,Props}/
├── Construction/
├── UI/{BuildingIcons,Categories,Routes,Actions,Panels}/
├── VFX/
├── Audio/
└── Shared/{Materials,Textures,Shaders}/

Docs/Art/FirstPass/
├── AssetRegister.csv
├── LicenseRegister.csv
├── Task13ArtContract.json
├── ReviewChecklist.md
└── AcceptanceShots.md

Assets/_Game/Editor/FirstArtPassAssetValidator.cs
Assets/_Game/Editor/FirstArtPassAssetValidator.cs.meta
Assets/_Game/Editor/FirstArtPassImportPolicy.cs
Assets/_Game/Editor/FirstArtPassImportPolicy.cs.meta
Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs
Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs.meta
Assets/_Game/Tests/EditMode/FirstArtPassImportPolicyTests.cs
Assets/_Game/Tests/EditMode/FirstArtPassImportPolicyTests.cs.meta
Tools/Art/validate_first_art_pass_delivery.py
```

`FirstArtPassAssetValidator` 只验证导入资产，不修改场景或运行时。它产生可读验证结果，不负责正式表现绑定。

### 2026-08-10 交付修复状态

- 已把来源分支最终文件树压入不含大普通 blob 的新交付分支，旧分支保留但不作为后续合并源；
- 已完成七类共 28 张地表 PNG、Ruins 八件与 Cliff 六件 FBX 的 LFS/数量/命名门；
- 已提交限定 `Assets/_Game/Art/FirstPass/` 的 Unity 导入策略与 43 项真实 importer 测试，并冻结完整 Art/FirstPass 层级共 55 个资源/目录 `.meta`；
- 本状态不替代本计划 Task 4 的完整尺寸、Bounds、Pivot、Renderer、音频和许可证验证器；该验证器仍未实现；
- 未创建 Material、Shader、Prefab、Sprite、场景映射或运行时美术接入，Task 12 仍未完成。

---

## Task 1: 通过 Task 13 门并冻结资产合同

**Files:**
- Create: `Docs/Art/FirstPass/Task13ArtContract.json`
- Create: `Docs/Art/FirstPass/AssetRegister.csv`
- Create: `Docs/Art/FirstPass/LicenseRegister.csv`
- Create: `Docs/Art/FirstPass/ReviewChecklist.md`

**Interfaces:**
- Consumes: Task 13 最终 HEAD、`BuildingCatalog.BuildMenu`、默认相机、`GrayboxPrototype3D`、稳定视觉 ID、现有 Git LFS 规则。
- Produces: 后续所有任务只读的尺寸/稳定 ID 合同与资产、许可证登记表。

- [ ] **Step 1: 建立隔离执行环境**

使用 `superpowers:using-git-worktrees` 从 Task 13 最终提交创建 `codex/first-art-pass-production`。确认新 worktree 除明确受保护的 MCP 文件外干净，不得复用仍在执行功能任务的共享目录。

- [ ] **Step 2: 验证 Task 13 硬门**

检查 `Docs/05-Formal-Development-Roadmap-ZH.md`、`Docs/06-User-Feedback-and-Change-Control-ZH.md` 和 Task 13 交付记录。必须同时出现完整 EditMode、PlayMode、无界面编译、默认 Release 3D、Development 3D、legacy 2D、性能和远端一致性证据。任一缺失则停止，不创建美术文件。

- [ ] **Step 3: 运行新鲜基线**

```bash
WASTECITY_UNITY_BIN="/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"
"$WASTECITY_UNITY_BIN" -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-first-art-pass-production \
  -runTests -testPlatform EditMode \
  -testResults /tmp/wastecity-first-art-pass/baseline-editmode.xml \
  -logFile /tmp/wastecity-first-art-pass/baseline-editmode.log
"$WASTECITY_UNITY_BIN" -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-first-art-pass-production \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/wastecity-first-art-pass/baseline-playmode.xml \
  -logFile /tmp/wastecity-first-art-pass/baseline-playmode.log
```

预期：两套完整基线 failed `0`；`-runTests` 命令不添加 `-quit`。

- [ ] **Step 4: 写入 Task13ArtContract.json**

文件必须保存：Task 13 HEAD、Unity/URP 版本、默认相机投影和 Transform、外城单格 `1.0`、内城单格 `0.32`、内城表面 `2.56×1.92`、城市/领袖稳定 ID、五座建筑的稳定 ID/Width/Height/Placement/Operation、28 个 BuildMenu 稳定 ID、两个升级目标排除项和默认 3D 场景 GUID。

五座建筑合同必须精确为：

```json
{
  "core.building.mining-station": {"width": 2, "height": 2},
  "core.building.housing": {"width": 2, "height": 2},
  "core.building.warehouse": {"width": 2, "height": 2},
  "core.building.wall": {"width": 1, "height": 1},
  "core.building.machine-gun-turret": {"width": 1, "height": 1}
}
```

若 Task 13 最终定义与上述批准规格不一致，停止并先修订 `IDEA-0004`，不得静默调整美术尺寸。

- [ ] **Step 5: 创建资产登记表**

`AssetRegister.csv` 表头固定为：

```text
asset_id,display_name,category,priority,source_path,unity_path,stable_visual_id,owner,version,concept_status,whitebox_status,final_status,integration_status,acceptance_status
```

写入规格总表中每个独立资源的行。初始版本 `0.1.0`，五种状态列均为 `not_started`，不得留空。

- [ ] **Step 6: 创建许可证登记表**

`LicenseRegister.csv` 表头固定为：

```text
asset_id,source_type,creator,source_url,license,commercial_use,attribution,proof_path,review_status
```

自制资源写 `source_type=original`、`license=project-owned`、`commercial_use=yes`。AI 辅助或外部资源必须填写实际提供方、条款链接和本地证据文件；未知项使用 `review_status=blocked`，且不得进入 Unity 交接目录。

- [ ] **Step 7: 提交资产合同**

```bash
git add Docs/Art/FirstPass/Task13ArtContract.json \
  Docs/Art/FirstPass/AssetRegister.csv \
  Docs/Art/FirstPass/LicenseRegister.csv \
  Docs/Art/FirstPass/ReviewChecklist.md
git commit -m "docs: freeze first art pass asset contract"
```

---

## Task 2: 制作并批准美术规范包

**Files:**
- Create: `ArtSource/FirstPass/ArtBible/*.kra`
- Create: `Assets/_Game/Art/FirstPass/ArtBible/*.png`
- Create: `Assets/_Game/Art/FirstPass/ArtBible/WasteCity_ColorPalette.ase`
- Create: `Assets/_Game/Art/FirstPass/ArtBible/WasteCity_ColorPalette.png`
- Create: `Assets/_Game/Art/FirstPass/ArtBible/WasteCity_ColorPalette.md`

**Interfaces:**
- Consumes: Task 1 尺寸合同和 `IDEA-0004` 风格方向。
- Produces: 三条后续生产轨道共同使用的色板、比例、材质和轮廓标准。

- [ ] **Step 1: 生成六张风格图草案**

分别绘制移动、展开、基础工业区、内城平台、外城施工、夜间灯光。每张固定 `3840×2160`、sRGB；不得混入其他作品的商标、UI或可识别模型。

- [ ] **Step 2: 生成六张无文字成图和六张标注图**

导出文件名固定为：

```text
ARTBIBLE_01_CityMobile_{Clean|Annotated}.png
ARTBIBLE_02_CityFortress_{Clean|Annotated}.png
ARTBIBLE_03_IndustrialDistrict_{Clean|Annotated}.png
ARTBIBLE_04_InnerCityGrid_{Clean|Annotated}.png
ARTBIBLE_05_GroundConstruction_{Clean|Annotated}.png
ARTBIBLE_06_NightLighting_{Clean|Annotated}.png
```

- [ ] **Step 3: 冻结色板**

记录基础工业、锈蚀、混凝土、警示色、四路线、合法、非法、选择和遗迹颜色的 sRGB Hex 与 Unity Linear 值。合法/非法颜色必须与现有灰盒语义一致。

- [ ] **Step 4: 制作比例图**

输出 `ARTBIBLE_ScaleSheet.png`，`4096×4096`，同时显示领袖、城市、平台、五建筑、1.0 外城格与 0.32 内城格。比例必须来自 Task 1 合同。

- [ ] **Step 5: 人工审核俯视可读性**

把六张图和比例图缩放到默认 1920×1080 游戏镜头占屏尺寸，确认城市、五建筑和领袖轮廓互不混淆。任何只能依靠细小文字辨认的设计退回修改。

- [ ] **Step 6: 更新登记并提交**

把规范资源的 concept/final 状态改为 `approved`，填写许可证记录。

```bash
git add ArtSource/FirstPass/ArtBible \
  Assets/_Game/Art/FirstPass/ArtBible \
  Docs/Art/FirstPass/AssetRegister.csv \
  Docs/Art/FirstPass/LicenseRegister.csv
git commit -m "art: establish first pass visual language"
```

---

## Task 3: 完成全部白模和比例验收

**Files:**
- Create: `ArtSource/FirstPass/MobileCity/City_Mobile_Master.blend`
- Create: `ArtSource/FirstPass/Characters/Leader_CenJin/Leader_Master.blend`
- Create: `ArtSource/FirstPass/Buildings/*/*_Master.blend`
- Create: `Assets/_Game/Art/FirstPass/**/Whitebox/*.fbx`

**Interfaces:**
- Consumes: Task 1 尺寸合同和 Task 2 比例图。
- Produces: 进入精细建模前的城市、平台、领袖和五建筑白模。

- [ ] **Step 1: 创建 Blender 统一模板**

场景单位为 Metric、Unit Scale `1.0`，Y Up 导出 FBX；建立 `ROOT`、`VISUAL`、`SOCKETS` 三集合。建筑 ROOT 位于占地中心，底面 `Y=0`，前方 `+Z`。

- [ ] **Step 2: 制作城市 Mobile/Fortress 白模**

只验证底盘、展开支撑、内城平台和整体轮廓，不做 UV、贴图或细节。两个状态必须共享同一玩法根和可逆视觉层级。

- [ ] **Step 3: 制作领袖白模**

包含角色体块、工具/武器、背包/终端和选择环锚点；默认相机下不得与 1×1 建筑混淆。

- [ ] **Step 4: 制作五建筑白模**

采矿站/住宅/仓库固定 2×2，城墙/机枪塔固定 1×1。四方向旋转后底座投影不得超出合同占地。

- [ ] **Step 5: 导出 FBX**

应用 Scale/Rotation，不应用破坏动画层级的变换。文件名固定为 `SM_<Asset>_Whitebox.fbx`；不把 `.blend1`、自动备份或临时渲染提交。

- [ ] **Step 6: 在空白 Unity 验证场景中人工检查**

只创建未保存临时场景，将外城 1.0 格、内城 0.32 格和默认相机参数复制自合同。检查 Pivot、底面、方向、占地和屏幕轮廓；不得修改正式 `GrayboxPrototype3D`。

- [ ] **Step 7: 更新登记并提交**

```bash
git add ArtSource/FirstPass/MobileCity \
  ArtSource/FirstPass/Characters/Leader_CenJin \
  ArtSource/FirstPass/Buildings \
  Assets/_Game/Art/FirstPass/MobileCity/Whitebox \
  Assets/_Game/Art/FirstPass/Characters/Leader_CenJin/Whitebox \
  Assets/_Game/Art/FirstPass/Buildings \
  Docs/Art/FirstPass/AssetRegister.csv
git commit -m "art: validate first pass whitebox scale"
```

---

## Task 4: 建立资产自动验证器

**Files:**
- Create: `Assets/_Game/Editor/FirstArtPassAssetValidator.cs`
- Create: `Assets/_Game/Editor/FirstArtPassAssetValidator.cs.meta`
- Create: `Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs`
- Create: `Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs.meta`
- Modify: `Assets/_Game/Editor/WasteCity.Editor.asmdef`

**Interfaces:**
- Consumes: `Task13ArtContract.json` 和 `Assets/_Game/Art/FirstPass/`。
- Produces: `FirstArtPassAssetValidator.ValidateAll()` 返回只读验证问题列表，不修改场景和资产。

- [ ] **Step 1: 写 RED 测试**

测试必须覆盖：缺文件、错误命名、错误 FBX scale、建筑 Bounds 超出占地、Pivot 底面非零、非 `+Z` 方向标记、超过一个主要 Renderer、贴图导入色彩空间错误、Mask 不是 Linear、音频不是 48 kHz、许可证登记缺失。

- [ ] **Step 2: 运行 RED**

```bash
"$WASTECITY_UNITY_BIN" -batchmode -nographics \
  -projectPath /Users/baiyan1/Documents/WasteCity-first-art-pass-production \
  -runTests -testPlatform EditMode \
  -testFilter WasteCity.Tests.FirstArtPassAssetValidationTests \
  -testResults /tmp/wastecity-first-art-pass/validator-red.xml \
  -logFile /tmp/wastecity-first-art-pass/validator-red.log
```

预期：只因 `FirstArtPassAssetValidator` 缺失失败，不允许程序集错误。

- [ ] **Step 3: 实现只读验证器**

公开接口固定为：

```csharp
public readonly struct FirstArtPassValidationIssue
{
    public string AssetId { get; }
    public string Code { get; }
    public string Message { get; }
}

public static class FirstArtPassAssetValidator
{
    public static IReadOnlyList<FirstArtPassValidationIssue> ValidateAll();
}
```

验证器只用 AssetDatabase、ModelImporter、TextureImporter、AudioImporter、Renderer/Bounds 和 CSV/JSON 读取；不得保存场景、重写导入设置或自动修复资源。

- [ ] **Step 4: 运行 GREEN 和完整 EditMode**

预期 focused failed `0`，完整 EditMode failed `0`。若基线测试发生与本文件无关的波动，单项复现并记录，不修改冻结测试。

- [ ] **Step 5: 提交**

```bash
git add Assets/_Game/Editor/FirstArtPassAssetValidator.cs \
  Assets/_Game/Editor/FirstArtPassAssetValidator.cs.meta \
  Assets/_Game/Editor/WasteCity.Editor.asmdef \
  Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs \
  Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs.meta
git commit -m "test: validate first art pass assets"
```

---

## Task 5: 完成采矿站黄金样板与铁矿节点

**Files:**
- Create: `ArtSource/FirstPass/Buildings/MiningStation/*`
- Create: `Assets/_Game/Art/FirstPass/Buildings/MiningStation/*`
- Create: `ArtSource/FirstPass/Environment/ResourceNodes/Iron/*`
- Create: `Assets/_Game/Art/FirstPass/Environment/ResourceNodes/Iron/*`
- Create: `Assets/_Game/Art/FirstPass/Shared/Materials/MAT_Building_Industrial.mat`

**Interfaces:**
- Consumes: 白模、色板、验证器和 `core.building.mining-station` 2×2 合同。
- Produces: 后续建筑复制的模型层级、材质、LOD、贴图和图标黄金模板。

- [ ] **Step 1: 完成三视概念**

正面、俯视、背面/功能结构必须表现资源输入端、加工主体、输出端和状态灯；禁止通过文字牌才能看出采矿功能。

- [ ] **Step 2: 完成 LOD0/1/2**

LOD0 4k–12k、LOD1 2k–6k、LOD2 0.5k–2k 三角面；三者 Pivot、底面和朝向一致。

- [ ] **Step 3: 完成 UV 与贴图**

输出 2048 BaseColor、Normal、Mask、Emission。Mask 通道按全局合同；不得在 BaseColor 烘焙强方向阴影。

- [ ] **Step 4: 完成铁矿节点**

制作 LOD0/1、普通、兼容高亮、不兼容和耗尽预留外观。高亮是表现层，不保存节点 ID 或兼容性规则。

- [ ] **Step 5: 完成黄金 Prefab**

`PF_Building_MiningStation.prefab` 只包含表现 Transform、一个主要 Renderer、LODGroup 和允许的 Animator/ParticleSystem；不得包含 Collider、Rigidbody 或 WasteCity 玩法 MonoBehaviour。

- [ ] **Step 6: 运行验证器**

focused 验证必须对采矿站和铁矿返回零 issue。用临时错误复制品证明错误 Pivot、Mask sRGB 和第二 Renderer 能打红，然后删除临时复制品。

- [ ] **Step 7: 冻结模板并提交**

把采矿站标记 `final_status=approved`、`integration_status=not_started`。提交源文件、导入资源、登记表与许可证证据。

```bash
git add ArtSource/FirstPass/Buildings/MiningStation \
  ArtSource/FirstPass/Environment/ResourceNodes/Iron \
  Assets/_Game/Art/FirstPass/Buildings/MiningStation \
  Assets/_Game/Art/FirstPass/Environment/ResourceNodes/Iron \
  Assets/_Game/Art/FirstPass/Shared/Materials \
  Docs/Art/FirstPass
git commit -m "art: complete mining station golden sample"
```

---

## Task 6: 完成移动城市与内城平台

**Files:**
- Create: `ArtSource/FirstPass/MobileCity/*`
- Create: `Assets/_Game/Art/FirstPass/MobileCity/*`

**Interfaces:**
- Consumes: 黄金材质模板、城市稳定 ID `core.city.mobile`、平台 `2.56×1.92` 合同。
- Produces: 城市 LOD、四状态动画和平台导入包。

- [ ] 完成城市 LOD0/1/2，预算分别为 60k–100k、25k–40k、8k–15k。
- [ ] 完成 `AN_City_MovingLoop`、`AN_City_Deploy`、`AN_City_IdleFortress`、`AN_City_Pack`；动画不移动玩法根。
- [ ] 完成 `SM_City_InnerPlatform.fbx`，可见顶面精确覆盖 `2.56×1.92`，网格刻线不遮挡预览。
- [ ] 完成不超过两套 4096 的城市纹理集，并记录实际显存。
- [ ] 创建 `PF_City_Mobile.prefab` 和 `PF_City_InnerPlatform.prefab`，不加入玩法脚本或玩法 Collider。
- [ ] 运行验证器和默认相机临时场景人工检查，确认移动/展开轮廓、平台格线和领袖比例。
- [x] 2026-08-09 已由用户批准移动形态、同城堡垒形态和 `8×6` 内城平台三张白底单物体概念参考；本项只代表造型参考完成，LOD、动画、FBX、Prefab 与 Unity 接入仍未开始。
- [x] 2026-08-09 已由用户批准 `Mobile → Deploy 33% → Deploy 66% → Fortress` 四帧白底机械顺序参考；`Pack` 使用反向顺序作视觉参考。正式动画层级、Pivot、接触事件、动画曲线、FBX 与 Animator 仍未开始。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete mobile city sample"`。

---

## Task 7: 完成领袖样板

**Files:**
- Create: `ArtSource/FirstPass/Characters/Leader_CenJin/*`
- Create: `Assets/_Game/Art/FirstPass/Characters/Leader_CenJin/*`

**Interfaces:**
- Consumes: 比例图和 Task 3 领袖白模。
- Produces: 岑烬模型、Rig、动画、Prefab 与头像。

- [ ] 完成 LOD0/1/2，预算分别为 15k–30k、7k–15k、2k–5k。
- [ ] 完成统一 Humanoid Rig，骨骼命名固定，不在动画文件复制网格。
- [ ] 完成待机、行走、奔跑、转向、操作、受击、倒地、等待救援、起身九组动画。
- [ ] 输出 `UI_Portrait_Leader_CenJin.png`，1024×1024 原图和 256×256 Unity 版。
- [ ] 创建只含表现组件的 `PF_Leader_CenJin.prefab`；SkinnedMeshRenderer 和 Animator 允许，WasteCity 玩法脚本、Collider 和 Rigidbody 禁止。
- [ ] 运行验证器，人工检查默认镜头下领袖与 1×1 建筑可区分、动作不移动根。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete first leader sample"`。

---

## Task 8: 完成四座剩余建筑与能晶节点

**Files:**
- Create: `ArtSource/FirstPass/Buildings/{Housing,Warehouse,Wall,MachineGunTurret}/*`
- Create: `Assets/_Game/Art/FirstPass/Buildings/{Housing,Warehouse,Wall,MachineGunTurret}/*`
- Create: `ArtSource/FirstPass/Environment/ResourceNodes/EnergyCrystal/*`
- Create: `Assets/_Game/Art/FirstPass/Environment/ResourceNodes/EnergyCrystal/*`

**Interfaces:**
- Consumes: 采矿站黄金模板和五建筑尺寸合同。
- Produces: 完整五建筑正式样板和第二种资源节点。

- [ ] 每座先完成三视概念并用默认俯视缩略图复核轮廓。
- [x] 2026-08-09 已由用户批准 `MiningStation`、`Housing`、`Warehouse`、`Wall`、`MachineGunTurret` 五张默认俯视白底单物体造型参考；正式三视图、模型、LOD、FBX、Prefab 与 Unity 接入仍未开始。批准的采矿站参考不包含资源节点。
- [ ] Housing/Warehouse 按普通建筑预算完成 LOD；Wall 允许更低预算；MachineGunTurret 按 8k–15k/3k–7k/0.8k–2.5k 完成 LOD。
- [ ] 机枪塔视觉炮头可以有动画层级，但不得包含瞄准、攻击或伤害脚本。
- [ ] 城墙四方向旋转后必须无空隙且不得越过 1×1 底座。
- [ ] 完成能晶 LOD0/1、普通/高亮/不兼容/耗尽预留表现。
- [ ] 运行验证器；五建筑和两节点必须零 issue。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete core building sample set"`。

---

## Task 9: 完成七种地形与十二种环境装饰

**Files:**
- Create: `ArtSource/FirstPass/Environment/Terrain/*`
- Create: `Assets/_Game/Art/FirstPass/Environment/Terrain/*`
- Create: `ArtSource/FirstPass/Environment/Props/*`
- Create: `Assets/_Game/Art/FirstPass/Environment/Props/*`

**Interfaces:**
- Consumes: 色板、URP Mask 合同和现有七类地形语义。
- Produces: 无缝地形材质与无玩法碰撞的装饰包。

- [x] 按 `Wasteland`、`Rocky`、`Wetland`、`Crystal`、`Ruins`、`DeepWater`、`Cliff` 七类逐一制作 2048×2048 无缝 BaseColor、Tangent Space Normal、URP Mask、16-bit Height、分层源文件和固定预览图；先以独立子计划完成 `Wasteland` 黄金样板，未批准前不得批量制作其余六类。
  - [x] `Wasteland`：黄金样板已完成并由用户验收通过。
  - [x] `Rocky`：正式贴图、分层源和固定预览已完成，并由用户验收通过。
  - [x] `Wetland`：首版正式预览被否决；重制后的正式贴图、分层源和固定预览已完成，并由用户验收通过。
  - [x] `Crystal`：概念、正式贴图、分层源和固定预览均已完成，并由用户验收通过。
  - [x] `Ruins`：正式地表贴图、分层源和固定预览已完成并由用户验收通过；八件低模模块按批准参考板完成重制，使用 `3–7` 个多材质分区，并通过默认倾斜正交、普通荒地组合、顶视和线框 QA。用户于 2026-08-09 明确批准当前模型；尚未接入 Unity。
  - [x] `DeepWater`：批准概念、2048×2048 BaseColor/Normal/URP Mask/16-bit Height、五层 OpenRaster 源、Blender 检查源和三张固定 QA 已完成；首轮正式结果因不像水被否决，连续水体重制版于 2026-08-09 由用户验收通过；尚未接入 Unity，也未制作正式水面模型。
  - [x] `Cliff`：批准参考板、2048×2048 BaseColor/Normal/URP Mask/16-bit Height、六层 OpenRaster 源、Blender 材质源、六件多材质模块、六个 FBX 和八张固定 QA 已完成；用户于 2026-08-09 验收通过。FBX 回读验证通过；尚未接入 Unity、Prefab、Collider 或玩法系统。
- [x] 使用 4×4 平铺图检查接缝与重复，并以默认倾斜正交视角和 PBR 球体或平面检查材质；BaseColor 使用 sRGB，Normal、Mask、Height 使用 Linear。
- [x] `Crystal` 地表只表现能晶污染或矿化痕迹，不生成可采集节点；可采集资产仍只属于 `Environment/ResourceNodes/EnergyCrystal`，任何地形文件不得保存资源或玩法真值。
- [x] `Wasteland`、`Rocky`、`Wetland`、`Crystal` 之间的色相、粗糙度和高度变化应支持柔和混合；`Ruins` 使用积尘、碎屑等柔化边缘但保持规则范围，`DeepWater` 和 `Cliff` 的轮廓、明度与材质响应保持清晰玩法边界。
- [x] 完成小石、大石、废钢板、管道、轮胎、路障、金属箱、路灯、混凝土碎块、机械残骸、干枯植物、能晶碎片十二种装饰的白底单物件参考图；十二张 `512×512` 独立 PNG 与总览板已于 2026-08-09 由用户批准。
- [ ] 十二种装饰的模型、FBX、Prefab 与 Unity 接入暂不制作：用户已明确取消由当前开发者继续建模；批准参考图只作为未来美术人员接手依据，不代表本项 3D 交付完成。
- [ ] 每种装饰 200–2,000 三角面，至少两种缩放或旋转变体，共享材质图集并允许 GPU Instancing。
- [ ] Prefab 不包含玩法 Collider、Rigidbody 或 WasteCity MonoBehaviour。
- [ ] 运行验证器与临时 32×24 展示场景人工检查，不修改正式场景。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete first environment kit"`。

---

## Task 10: 完成 28 建筑图标和建造 UI 包

**Files:**
- Create: `ArtSource/FirstPass/UI/*`
- Create: `Assets/_Game/Art/FirstPass/UI/BuildingIcons/*`
- Create: `Assets/_Game/Art/FirstPass/UI/Categories/*`
- Create: `Assets/_Game/Art/FirstPass/UI/Routes/*`
- Create: `Assets/_Game/Art/FirstPass/UI/Actions/*`
- Create: `Assets/_Game/Art/FirstPass/UI/Panels/*`

**Interfaces:**
- Consumes: 28 个 BuildMenu 稳定 ID、色板和固定图标相机规范。
- Produces: 完整目录视觉和后续 UGUI 映射所需 Sprite 源。

- [x] 为概念参考固定三分之四俯视正交相机、暖色棚拍光、纯白背景、单主体和统一占屏边距；4 张路线源板与 28 张独立图已通过用户验收。
- [x] 为 28 个普通目录稳定 ID 各输出一张已批准 `512×512` 白底概念参考，并生成按 `BuildingCatalog.BuildMenu` 顺序排列的 7×4 QA 板。
- [ ] 正式 `1024×1024` 透明原图、`256×256` Unity PNG、Sprite 导入和运行时映射不在本轮白底参考范围内，尚未完成。
- [x] 明确排除 `core.building.heavy-machine-gun-turret` 和 `cultivation.building.sword-riding-platform`。
- [x] 完成基础/生产/物流/防御/路线五分类与科技/修仙/生物/灵能四路线的 9 张 `512×512` 白底单徽章参考图；用户于 2026-08-09 批准。正式透明 Sprite 与 Unity 映射仍未开始。
- [x] 完成放置、旋转、取消、删除、施工、暂停、完成、遗弃、完整拆除、快速拆除、内城、外城十二张 `512×512` 白底单徽章参考图；用户于 2026-08-09 批准。透明 Sprite、UGUI 行为与 Unity 映射仍未开始。
- [ ] 制作十格快捷栏、完整目录、分类/路线标签、搜索、建筑卡、详情、成本、锁定原因、施工进度、取消确认、撤离清单、错误提示和按钮五态九宫格切图。
- [ ] 用脚本或清单验证 28 个稳定 ID 无缺失、无重复、两个升级目标不存在；人工检查 256 像素与默认游戏缩放下仍可读。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete first building UI package"`。

---

## Task 11: 完成施工、遗迹、特效和音效包

**Files:**
- Create: `ArtSource/FirstPass/Construction/*`
- Create: `Assets/_Game/Art/FirstPass/Construction/*`
- Create: `ArtSource/FirstPass/VFX/*`
- Create: `Assets/_Game/Art/FirstPass/VFX/*`
- Create: `ArtSource/FirstPass/Audio/*`
- Create: `Assets/_Game/Art/FirstPass/Audio/*`

**Interfaces:**
- Consumes: 五建筑实际唯一占地、共享材质、施工/撤离语义。
- Produces: 可供后续表现映射使用的状态资源，不包含状态机。

- [ ] 按五建筑唯一占地制作共享地基、框架、脚手架、焊接点、进度环、暂停和材料不足标记。
- [ ] 制作 Small/Medium/Large 三种遗迹覆盖、共享灰褐材质、裂纹/烧焦贴花。
- [ ] 制作合法预览、非法预览、选择描边、节点高亮、施工火花、完成、完整拆除、快速拆除八组 ParticleSystem/Shader Graph 资源。
- [ ] 制作菜单开/关、切分类、选建筑、旋转、合法/非法放置、施工开始/循环、完成、取消、遗弃、完整拆除、快速拆除十四组 48 kHz、24-bit WAV。
- [ ] 验证 VFX 不读取玩法状态、不实例化材质；音频源不是 MP3，循环点无爆音。
- [ ] 更新登记与许可证并提交：`git commit -m "art: complete construction feedback package"`。

---

## Task 12: 完成交接包验证并停止

**Files:**
- Modify: `Docs/Art/FirstPass/AssetRegister.csv`
- Modify: `Docs/Art/FirstPass/LicenseRegister.csv`
- Modify: `Docs/Art/FirstPass/ReviewChecklist.md`
- Create: `Docs/Art/FirstPass/AcceptanceShots.md`

**Interfaces:**
- Consumes: Tasks 1–11 全部资源。
- Produces: 可交给独立 Unity 表现映射设计的完整、未接线样板包。

- [ ] **Step 1: 运行完整资产验证**

`FirstArtPassAssetValidator.ValidateAll()` 必须返回零 issue。许可证登记中不得存在 `blocked`、空 commercial_use 或缺 proof_path。

- [ ] **Step 2: 运行完整 Unity 回归**

EditMode、PlayMode、无界面编译全部通过。因为本计划不接线，正式场景、VisualSlot、游戏运行截图和构建内容必须与 Task 13 基线一致。

- [ ] **Step 3: 证明冻结范围**

相对 Task 13 起点，以下路径必须零差异：

```text
Assets/_Game/Scenes/
Assets/_Game/Scripts/
Assets/_Game/Editor/GrayboxSceneAuthoring.cs
Packages/
ProjectSettings/
Assets/_Game/ArtIntegration/VisualLibrary.asset
```

允许的 C# 差异只有只读验证器、对应测试和 Editor asmdef 引用。

- [ ] **Step 4: 归档离线验收图**

`AcceptanceShots.md` 链接移动/展开城市、平台比例、领袖、五建筑四方向、两节点、七地形、十二装饰、28 图标总览、UI组件、施工、遗迹和八 VFX 的固定预览；不得把未接线预览描述成游戏运行截图。

- [ ] **Step 5: 最终登记状态**

所有本计划资源的 concept/whitebox/final 为 `approved`，integration/acceptance 为 `not_started`。后两列只有后续 Unity 表现映射和运行时验收完成后才能改变。

- [ ] **Step 6: 提交、推送并停止**

```bash
git add ArtSource/FirstPass \
  Assets/_Game/Art/FirstPass \
  Assets/_Game/Editor/FirstArtPassAssetValidator.cs \
  Assets/_Game/Editor/FirstArtPassAssetValidator.cs.meta \
  Assets/_Game/Editor/WasteCity.Editor.asmdef \
  Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs \
  Assets/_Game/Tests/EditMode/FirstArtPassAssetValidationTests.cs.meta \
  Docs/Art/FirstPass
git commit -m "art: deliver first formal sample source package"
git push origin codex/first-art-pass-production
```

最终报告必须列出 HEAD、所有提交、LFS 文件、验证结果、许可证状态、未接线边界和远端一致性。随后停止：不得修改运行时、场景或 VisualLibrary，不得开始剩余 23 座建筑，也不得把 `IDEA-0004` 标记为已实现或已验证。

---

## 后续独立里程碑

本计划通过后，基于 Task 13 最终代码和交接包另行设计“第一版正式美术 Unity 表现映射”。该里程碑必须逐项决定：

- 现有 `VisualDefinition`、`VisualLibrary`、`VisualLibraryProvider` 与 `GrayboxVisualSlot` 的复用边界；
- 动态建筑完成/预览/施工/遗迹如何解析建筑定义 ID，而不依赖实例 ID；
- MeshRenderer 与领袖 SkinnedMeshRenderer 的兼容；
- 城市四状态和领袖动画参数如何只读现有玩法状态；
- UI Sprite 如何进入运行时生成的 UGUI；
- 灰盒回退、Release 构建、128 个建筑/施工点/遗迹实例性能和真实 Windows 冒烟。

未完成该独立设计前，不得以临时脚本或场景手工引用接入本计划资源。
