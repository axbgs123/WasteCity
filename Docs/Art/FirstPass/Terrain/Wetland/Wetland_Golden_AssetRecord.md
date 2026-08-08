# 湿地 `Wetland` 首版材质资产记录

## 记录状态

- 生产状态：用户已批准 v2 概念方向；正式重建与制作方自动化验收完成，等待用户查看正式预览
- 概念批准：用户查看 `Wetland v2` 概念预览并回复“可以”
- 修订日期：2026-08-09
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Wetland` 地表材质；未接入 Unity 运行时，未创建浅水网格或其他模型，也未制作后续地形
- Git：本记录与资产位于同一提交，提交信息为 `art: add approved wetland terrain material`

## 来源与许可证

正式 BaseColor 以用户批准的 `Wetland_Approved_AI_Concept_v002.png` 为颜色和表面结构输入。该概念由 Codex 内置图像生成工具生成，并仅使用已批准的 `Wasteland` 与 `Rocky` 概念作为项目风格、色系、尺度和完成度参考。第一稿因碎块过密被制作方淘汰，用户批准修订后的第二稿后才进入正式重建。

Height 由共享区域权重、带通材料结构、独立周期宏观场、独立周期高频场和独立盆地场共同构建，不是概念图或 BaseColor 的灰度复制；Normal 由 16-bit Height 派生；Mask 四通道分别制作。没有使用照片、扫描、材质商店或第三方贴图。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Krita、Python、NumPy 和 Pillow 仅作为制作工具。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线预览 |
| Blender MCP | 1.2；MCP SDK 1.3.0 | 制作机自动化桥接已安装；未提供像素来源 |
| Krita | 5.2.16 | 将 7 层 OpenRaster 中间源保存为分层 `.kra` |
| Python | 3.11.9 | 周期重建与自动化验收 |
| NumPy | 2.2.4 | 频域周期分解、区域权重、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height 和 4×4 检查图 |

- 随机种子：`813027`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示
- 纹理尺度：约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G Ambient Occlusion / B Detail Mask / A Smoothness
- 模型规则：Unity 模型主交付格式仍为 FBX；本地形资产没有模型、浅水网格或 FBX

## 视觉与 PBR 结果

- 构成比例：深色湿泥 45%、浅污水洼 20%、干土小岛 15%、黑色矿物污泥 10%、根系或枯草痕迹 5%、向荒地过渡的泥土 5%；二值像素误差小于 `0.00001` 个百分点。
- BaseColor：R `0.172549–0.560784`，G `0.164706–0.498039`，B `0.074510–0.419608`；平均 RGB `0.323797 / 0.285608 / 0.197253`。
- Height：`0.404166–0.572045`，平均 `0.481566`，包含 `10961` 个离散 16-bit 值。
- Normal：平均长度 `1.000336`，最小长度 `0.994464`，Z 最小 `0.850980`、平均 `0.994597`；整体向上且起伏低于岩石地。
- Metallic：恒为 `0`，湿地整体非金属。
- AO：`0.866667–1.0`，平均 `0.988358`。
- Detail Mask：`0.086275–0.913725`，平均 `0.424579`。
- Smoothness：`0.145098–0.843137`，平均 `0.501799`；干土保持低值，湿泥和浅污水达到规格要求的较高响应。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_BaseColor.png` | 2048² RGB 8-bit / sRGB | `1fde3be558bd9d84fa35c63160bd87cb1a02e53524bbaba5d7acd1142c3409a7` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Normal.png` | 2048² RGB 8-bit / Linear | `c080fba85ead513ee0ef611d5d125c8f0e8fe26d655ed48fc7f94cb96349249e` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Mask.png` | 2048² RGBA 8-bit / Linear | `4c3404da29423646f1e004b6f2b695a084a21ef2cfbaffa628a63e3ef7fe00fb` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Height.png` | 2048² Gray 16-bit / Linear | `6fdfb9c045dbd44129da5aada2f05e1be5a52bd938310bf7e8e008015d0a7068` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/References/Wetland_Approved_AI_Concept_v002.png` | 1254² RGB 8-bit / 已批准概念 | `15112788e3a33c3b7097dee91da8884a95794c1ffef9db0557526b2e3c4fcbc7` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Master.kra` | 7 个命名图层载荷 | `8a8b128e83aadcc79f2269b34d618fe683a7ce0b2e46dc00371b13639fe210a6` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `f19efc5a4da4f70ab1e4949d09134e5ec790c0460991ffc87274698b95e0afbc` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `983a1d073ddb9f734d1bcb4039a809cd7ac308b73f146a9d32bab25331b43296` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/References/Wetland_Golden_SourceNotes.md` | UTF-8 Markdown | `e9e0baed4f39e123ef6412b1a0da5eed3b652417a9d031f8b39f08e315475dd5` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_Tiling4x4.png` | 2048² RGB 8-bit | `a245613237f3e995aa68f36da6d07e84e98803fa459c4221896832c133f6440b` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `eccfc9b774e01228de9a4d5104f746bac1969994bd28d8a040ad5e54ad35c085` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_PBRCheck.png` | 1920×1080 RGBA 8-bit | `d98f9a915670988a1e49fb80f267693c5668a4da5381f0de694f9980dad1b3d1` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.001001 / 0.000844`，低于内部相邻变化 `0.005401 / 0.006287`。
- Height 跨边界平均差 X/Y 为 `0.000054 / 0.000074`，低于内部相邻变化 `0.001102 / 0.001193`；Normal 同样通过。
- 首次 Mask 验收发现 Y 边界 `0.004115` 高于内部 `0.003659`，未放宽标准；将六类共享混合权重执行周期重建后，最终 Mask X/Y 边界降至 `0.000639 / 0.000910`，低于内部 `0.003311 / 0.003657`。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；湿泥、浅污水、干土和污泥在缩小后仍可区分。
- PBR 平面和球体检查显示浅污水与湿泥的高光高于干土；高光来自独立 Smoothness 通道，不存在 BaseColor 烘焙高光。
- `.kra` 含最终 BaseColor 和六类覆盖遮罩；`.blend` 可由 Blender 5.2.0 LTS 无界面重新打开，包含生成脚本和四张打包贴图。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未制作 `Crystal`、`Ruins`、`DeepWater` 或 `Cliff` 正式贴图，也未制作浅水网格。
