# 结晶地表 `Crystal` 首版材质资产记录

## 记录状态

- 关联需求：`IDEA-0004`
- 生产状态：用户已批准 v2 概念方向；正式重建与制作方自动化验收完成，等待用户查看正式预览
- 概念批准：用户查看 `Crystal v2` 概念预览并回复“通过”
- 正式验收：待用户查看 4×4、默认倾斜正交和 PBR 检查图后确认
- 修订日期：2026-08-09
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Crystal` 地表材质；未接入 Unity 运行时，未创建能晶节点、晶簇模型、FBX、Prefab、Collider 或其他后续地形
- Git：本记录与正式候选资产位于同一提交，提交信息为 `art: add approved crystal terrain material`；若用户否决正式预览，以后续普通提交重制，不改写历史

## 来源与许可证

正式 BaseColor 以用户批准的 `Crystal_Approved_AI_Concept_v002.png` 作为颜色和局部表面结构输入。该概念由 Codex 内置图像生成工具生成，并只使用已批准的 `Wasteland`、`Rocky` 与 `Wetland` 概念作为项目风格、色系、尺度和完成度参考。第一稿因晶脉过亮、过长和类似科幻电路而被制作方淘汰；用户批准修订后的第二稿后才进入正式重建。

Height 由共享区域权重、独立周期宏观场、独立周期高频场、盆地边界和局部材料结构共同构建，不是概念图或 BaseColor 的灰度复制；Normal 由 16-bit Height 派生；Mask 四通道分别制作。没有使用照片、扫描、材质商店或第三方贴图。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Krita、Python、NumPy 和 Pillow 只作为制作工具，不提供外部正式像素。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线预览 |
| Blender MCP | 1.2，MCP SDK 1.3.0 | 制作机自动化桥接已安装；未提供像素来源 |
| Krita | 5.2.16，git `7d9aefc` | 将 6 层 OpenRaster 中间源保存为分层 `.kra` |
| Python | 3.11.9 | 周期重建与自动化验收 |
| NumPy | 2.2.4 | 频域周期分解、区域权重、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height 和 4×4 检查图 |

- 随机种子：`824219`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示
- 纹理尺度：约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G Ambient Occlusion / B Detail Mask / A Smoothness
- 自发光：当前交付没有 Emission 贴图或 Shader 接线；青灰晶脉只在 BaseColor 中保持克制辨识，不烘焙光晕
- 模型规则：Unity 模型主交付格式仍为 FBX；本地形资产没有模型、资源节点、晶簇或 FBX

## 视觉与 PBR 结果

- 构成比例：暖黄荒土 65%、深色玻化地表 18%、灰青晶壳 10%、细晶脉 4%、灼烧或玻化边缘 3%；二值像素误差小于 `0.00001` 个百分点。
- BaseColor：R `0.196078–0.560784`，G `0.184314–0.494118`，B `0.101961–0.388235`；平均 RGB `0.370520 / 0.304547 / 0.205996`。
- Height：`0.444984–0.540948`，平均 `0.498977`，包含 `6253` 个离散 16-bit 值。
- Normal：平均长度 `1.000208`，最小长度 `0.995020`，Z 最小 `0.929412`、平均 `0.999468`；整体向上且只表达贴地微起伏。
- Metallic：恒为 `0`，结晶地表不作为金属。
- AO：`0.886275–1.0`，平均 `0.996030`，没有重黑 AO 伪造深坑。
- Detail Mask：`0.192157–0.917647`，平均 `0.515890`。
- Smoothness：`0.129412–0.772549`，平均 `0.298218`；玻化区达到规格要求的高响应，暖黄荒土保持低值。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_BaseColor.png` | 2048² RGB 8-bit / sRGB | `d525b0aeef8dd4add2496bba8ce504278f8210ca90f16e97c970d73d3e5a4f5b` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Normal.png` | 2048² RGB 8-bit / Linear | `c12b7bb9e7c2690ed4564e140e9e3e50758b288b9059658aad7629c197d81179` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Mask.png` | 2048² RGBA 8-bit / Linear | `c8dd8b7d279accfd34fc27d11df0b1ac19475e5ac6835a1198d2ab80de4d7c52` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Crystal/T_Terrain_Crystal_Height.png` | 2048² Gray 16-bit / Linear | `5a48a0678d278be51f089239d6e465474c29a5554c9261a7cd6c813d7ee63d1a` |
| `ArtSource/FirstPass/Environment/Terrain/Crystal/References/Crystal_Approved_AI_Concept_v002.png` | 1254² RGB 8-bit / 已批准概念 | `921d53e77b4195ee1264a7439b75926b30790f402d715fbfcd47010cdef8b591` |
| `ArtSource/FirstPass/Environment/Terrain/Crystal/Crystal_Golden_Master.kra` | 6 个命名图层 | `a50c73410d2412f1a33d1ee5fb085aa896da6b7bd1fcb513790564cdb5485152` |
| `ArtSource/FirstPass/Environment/Terrain/Crystal/Crystal_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `a3e5ee1f599b7c8711340945a348c173a39aff7501e1f3b0b606915f1519a4be` |
| `ArtSource/FirstPass/Environment/Terrain/Crystal/Crystal_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `8a835403ed0983eeb07275469efce97c4d7958130c727be95ee1ab7b055c218a` |
| `ArtSource/FirstPass/Environment/Terrain/Crystal/References/Crystal_Golden_SourceNotes.md` | UTF-8 Markdown | `bd6a92ec8107ad7f22b23279ae73203775e57fbee81d5e66babafae5e87ed6b4` |
| `Docs/Art/FirstPass/Terrain/Crystal/QA_Terrain_Crystal_Tiling4x4.png` | 2048² RGB 8-bit | `f72110171cecf532f9e032f98023687c899ff326535d43ef9324457c404cf51e` |
| `Docs/Art/FirstPass/Terrain/Crystal/QA_Terrain_Crystal_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `3143e089be6a3b6ada22ba36d69a3adee0a0e313166c35c20af28b2ec331c5c6` |
| `Docs/Art/FirstPass/Terrain/Crystal/QA_Terrain_Crystal_PBRCheck.png` | 1920×1080 RGBA 8-bit | `e8faae8f3513202616a4628bf8bc6fa5fc5c3b5b178fe02aef65f73b3b994252` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 制作方验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.000594 / 0.000540`，低于内部相邻变化 `0.006066 / 0.007069`。
- Height 跨边界平均差 X/Y 为 `0.000014 / 0.000018`，低于内部相邻变化 `0.000350 / 0.000364`。
- Normal 跨边界平均差 X/Y 为 `0.000391 / 0.000504`，低于内部相邻变化 `0.000808 / 0.000874`。
- Mask 跨边界平均差 X/Y 为 `0.000181 / 0.000157`，低于内部相邻变化 `0.001523 / 0.001519`。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；晶脉已从连续网络收敛为近看可见的低对比矿痕。
- 默认倾斜正交预览仍先读作暖黄荒土，玻化区和灰青晶壳提供第二层辨识；没有资源节点、直立晶体或可采集物。
- PBR 平面和球体检查显示玻化区 Smoothness 高于荒土；高光来自独立 Mask Alpha，不存在 BaseColor 烘焙高光或 Emission 光晕。
- `.kra` 含最终 BaseColor 和五类覆盖遮罩；`.blend` 已由 Blender 5.2.0 LTS 无界面重开，场景元数据为 `gameplay_truth=none`，四张正式贴图全部打包。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未制作 `Ruins`、`DeepWater` 或 `Cliff` 正式贴图，也未制作可采集能晶节点或其他模型。
