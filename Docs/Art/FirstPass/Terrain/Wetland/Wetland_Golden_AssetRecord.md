# 湿地 `Wetland` 首版材质资产记录

## 记录状态

- 生产状态：首版正式预览被用户否决；重制版已于 2026-08-09 验收通过
- 概念批准：用户查看 `Wetland v2` 概念预览并回复“可以”
- 正式验收：用户否决首版正式预览；查看重制后的 4×4、倾斜正交和 PBR 检查图后回复“通过”
- 修订日期：2026-08-09
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Wetland` 地表材质；未接入 Unity 运行时，未创建浅水网格或其他模型，也未制作后续地形
- Git：本记录与重制资产位于同一提交，提交信息为 `art: refine wetland terrain material`

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

- 随机种子：`813118`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示
- 纹理尺度：约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G Ambient Occlusion / B Detail Mask / A Smoothness
- 模型规则：Unity 模型主交付格式仍为 FBX；本地形资产没有模型、浅水网格或 FBX

## 视觉与 PBR 结果

- 构成比例：深色湿泥 45%、浅污水洼 20%、干土小岛 15%、黑色矿物污泥 10%、根系或枯草痕迹 5%、向荒地过渡的泥土 5%；二值像素误差小于 `0.00001` 个百分点。
- BaseColor：R `0.141176–0.666667`，G `0.125490–0.619608`，B `0.054902–0.529412`；平均 RGB `0.322621 / 0.281468 / 0.193617`。
- Height：`0.402304–0.577737`，平均 `0.481831`，包含 `11331` 个离散 16-bit 值。
- Normal：平均长度 `1.000270`，最小长度 `0.994526`，Z 最小 `0.811765`、平均 `0.995620`；整体向上且起伏低于岩石地。
- Metallic：恒为 `0`，湿地整体非金属。
- AO：`0.890196–1.0`，平均 `0.989773`。
- Detail Mask：`0.090196–0.933333`，平均 `0.429523`。
- Smoothness：`0.121569–0.819608`，平均 `0.481557`；重制版降低湿泥高光，浅污水仍保持最高响应，干土保持低值。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_BaseColor.png` | 2048² RGB 8-bit / sRGB | `2c1f866ce1d6d4ea50263edab55cdede688302f49ac9c9f01f2275d9e24850f0` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Normal.png` | 2048² RGB 8-bit / Linear | `80ee58943034afdc1c406850c3b89c314728057de496a8fcdacd2b544606e788` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Mask.png` | 2048² RGBA 8-bit / Linear | `23891c12f890ded0b8cc6d980f66027fd362e21d2a0db097613901bb32cb8c23` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wetland/T_Terrain_Wetland_Height.png` | 2048² Gray 16-bit / Linear | `a9c2531dbbf888abd19b59865bf10cab2736f4b20c2acd56b2595f6fec9841b0` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/References/Wetland_Approved_AI_Concept_v002.png` | 1254² RGB 8-bit / 已批准概念 | `15112788e3a33c3b7097dee91da8884a95794c1ffef9db0557526b2e3c4fcbc7` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Master.kra` | 7 个命名图层载荷 | `3a758503d73d39148b2e31ba8b6f69635e3d66e4767793d1d8aee3dcece91ea5` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `288200fb049ce458a444850759916cf881e66d331745610e366c1dc2b8feb7e1` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/Wetland_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `bc33fc5275dc9f29c041163294b62be63fa1d02cdfe60b4e7a64534ae2f76011` |
| `ArtSource/FirstPass/Environment/Terrain/Wetland/References/Wetland_Golden_SourceNotes.md` | UTF-8 Markdown | `ad1682f714194909fc51231293c3e5b5247277f5ecae80b623f8f60a64458e06` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_Tiling4x4.png` | 2048² RGB 8-bit | `18dc518e544974c9d70a01e4ddc59ea4d30a090970d3ac0b87e2721380f8e1e8` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `eaccd49c2b5e0c883249d54e753d13f208b3eab940a01104445500cb86fcd88b` |
| `Docs/Art/FirstPass/Terrain/Wetland/QA_Terrain_Wetland_PBRCheck.png` | 1920×1080 RGBA 8-bit | `143e2bdd7c80ab0ca8f9c16245ea182599a042c5e933b6c79f7fddea868a644e` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.001287 / 0.001074`，低于内部相邻变化 `0.006958 / 0.008214`。
- Height 跨边界平均差 X/Y 为 `0.000049 / 0.000051`，低于内部相邻变化 `0.000898 / 0.000965`；Normal 边界 `0.001743 / 0.001779` 低于内部 `0.002931 / 0.003309`。
- 首次 Mask 验收发现 Y 边界 `0.004115` 高于内部 `0.003659`，未放宽标准；周期重建后通过。重制版最终 Mask X/Y 边界为 `0.000471 / 0.000590`，低于内部 `0.002967 / 0.003256`。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；湿泥、浅污水、干土和污泥在缩小后仍可区分。
- 首版正式预览被用户否决，原因包括湿泥像碎裂硬地、区域过碎、重复图案明显和 PBR 高光偏冷发白；重制版扩大连贯泥面区域、保留更多概念局部细节、降低 Smoothness 并改用暖色克制灯光。
- 重制后的 PBR 平面和球体检查显示浅污水高光高于湿泥与干土；高光来自独立 Smoothness 通道，不存在 BaseColor 烘焙高光。用户查看三张重制预览后回复“通过”。
- `.kra` 含最终 BaseColor 和六类覆盖遮罩；`.blend` 可由 Blender 5.2.0 LTS 无界面重新打开，包含生成脚本和四张打包贴图。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未制作 `Crystal`、`Ruins`、`DeepWater` 或 `Cliff` 正式贴图，也未制作浅水网格。
