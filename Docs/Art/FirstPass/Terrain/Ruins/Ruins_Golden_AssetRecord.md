# 大型废墟地表 `Ruins` 首版材质资产记录

## 记录状态

- 关联需求：`IDEA-0004`
- 生产状态：用户已批准 v1 概念方向；正式重建与制作方自动化验收完成，等待用户查看正式预览
- 概念批准：用户查看 `Ruins v1` 概念预览并回复“通过”
- 正式验收：待用户查看 4×4、默认倾斜正交和 PBR 检查图后确认
- 修订日期：2026-08-09
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Ruins` 地表材质；未接入 Unity 运行时，未创建 6–8 个低模废墟模块、FBX、Prefab、Collider 或其他后续地形
- Git：本记录与正式候选资产计划位于同一普通提交；若用户否决正式预览，以后续普通提交重制，不改写历史

## 来源与许可证

正式 BaseColor 以用户批准的 `Ruins_Approved_AI_Concept_v001.png` 作为颜色和局部表面结构输入。该概念由 Codex 内置图像生成工具生成，并只使用已批准的 `Wasteland`、`Rocky`、`Wetland` 与 `Crystal` 概念作为项目风格、色系、尺度和完成度参考。用户批准首稿概念后才进入正式重建。

Height 由共享区域权重、独立周期宏观场、独立周期高频场、盆地边界和局部材料结构共同构建，不是概念图或 BaseColor 的灰度复制；Normal 由 16-bit Height 派生；Mask 四通道分别制作。没有使用照片、扫描、材质商店或第三方贴图。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Krita、Python、NumPy 和 Pillow 只作为制作工具，不提供外部正式像素。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线预览 |
| Blender MCP | 1.2，MCP SDK 1.3.0 | 制作机自动化桥接已安装；未提供像素来源 |
| Krita | 5.2.16，git `7d9aefc` | 将 7 层 OpenRaster 中间源保存为分层 `.kra` |
| Python | 3.11.9 | 周期重建与自动化验收 |
| NumPy | 2.2.4 | 频域周期分解、区域权重、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height 和 4×4 检查图 |

固定程序随机种子为 `824401`；一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。

## 构成比例与 PBR 参数

| 构成 | 精确像素数 | 面积比例 | 主要视觉职责 |
|---|---:|---:|---|
| 风化混凝土 | 1,468,006 | 34.999990% | 灰色破裂板面与旧工业基础 |
| 破损工业地坪 | 838,861 | 20.000005% | 深色磨损底面 |
| 暖黄积尘 | 838,861 | 20.000005% | 接回 Wasteland 并柔化边缘 |
| 碎石和瓦砾 | 629,146 | 15.000010% | 中近景破损与颗粒轮廓 |
| 旧标线、排水或结构痕迹 | 293,601 | 6.999993% | 稀疏旧工业辨识，不形成网格 |
| 裸露金属 | 125,829 | 2.999997% | 贴地锈蚀碎片与少量金属响应 |

- BaseColor 主锚为 `#55514A`、`#393936`、`#756047`、`#8A682E`；无烘焙方向光、高光、AO 或阴影。
- Metallic 最小/最大/平均为 `0.000000 / 0.698039 / 0.016785`，只由 3% 裸露金属区域贡献。
- AO 最小/最大/平均为 `0.913725 / 1.000000 / 0.985206`。
- Detail Mask 最小/最大/平均为 `0.298039 / 0.925490 / 0.561747`。
- Smoothness 最小/最大/平均为 `0.098039 / 0.466667 / 0.205947`；积尘和瓦砾最低，氧化金属略高但不形成镜面。
- Height 最小/最大/平均为 `0.470649 / 0.575937 / 0.516684`，共有 `6,690` 个 16-bit 取值。
- Normal 平均长度 `1.000278`、最小长度 `0.994897`、最小 Z `0.945098`、平均 Z `0.996653`。

## 文件、格式与 SHA-256

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_BaseColor.png` | 2048² RGB 8-bit / sRGB | `9bd361a9bb35deadda446bcd3a92daad67643b4c95b1e5b11bee007f56e17a5a` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Normal.png` | 2048² RGB 8-bit / Linear | `4df13aa0877887065d4a892e0556d3cebd136b6084c2f4b2ae11b0f060569c5b` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Mask.png` | 2048² RGBA 8-bit / Linear | `34518e94df7a000ddf55749b84905fd33a7b94e61bae6539701c55c1aa5a88f7` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/T_Terrain_Ruins_Height.png` | 2048² Gray 16-bit / Linear | `15028f343750416c6b980e8e34664d69d9b07591fc2649982241abb7cac03404` |
| `ArtSource/FirstPass/Environment/Terrain/Ruins/References/Ruins_Approved_AI_Concept_v001.png` | 1254² RGB 8-bit / 已批准概念 | `f6c45cb0e9c8af0ce197c30080be6aa56ab74f3fd47eb165d8e7cb2a89de5834` |
| `ArtSource/FirstPass/Environment/Terrain/Ruins/Ruins_Golden_Master.kra` | 7 个命名图层 | `c29d751a41dce6ca6bf4cb07ca4a437c5bf63e98d81f4c09196e83f6d8565a1d` |
| `ArtSource/FirstPass/Environment/Terrain/Ruins/Ruins_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `e5169c42046b8615d686c16fc7d95d447bf41baa6ff41a2e5a455aa63765ddf6` |
| `ArtSource/FirstPass/Environment/Terrain/Ruins/Ruins_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `82d0b60893f45fb673b1ffc2d7765f56a4fcdbb3c1294614c8504fcf7956db85` |
| `ArtSource/FirstPass/Environment/Terrain/Ruins/References/Ruins_Golden_SourceNotes.md` | UTF-8 Markdown | `b70bfdddb19949ceb4967f7c41ac123760a9602db4b7d044ba9984045a54ea9f` |
| `Docs/Art/FirstPass/Terrain/Ruins/QA_Terrain_Ruins_Tiling4x4.png` | 2048² RGB 8-bit | `730ea221c29c55eb0e2db1bb0451a82150c0b7f417225cd51a3fc7252601d621` |
| `Docs/Art/FirstPass/Terrain/Ruins/QA_Terrain_Ruins_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `e4e3e5aff2f4754e9c94db0ea858ea97210903a91440f83ade92dab3cd93baeb` |
| `Docs/Art/FirstPass/Terrain/Ruins/QA_Terrain_Ruins_PBRCheck.png` | 1920×1080 RGBA 8-bit | `63d5013293eb6df6f3c529996e29dbde4a179e62926744bacfc3ed82d89da7d5` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 制作方验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.000629 / 0.000578`，低于内部相邻变化 `0.006644 / 0.007321`。
- Height 跨边界平均差 X/Y 为 `0.000050 / 0.000049`，低于内部相邻变化 `0.000892 / 0.000892`。
- Mask 跨边界平均差 X/Y 为 `0.000540 / 0.000559`，低于内部相邻变化 `0.002653 / 0.002624`。
- Normal 跨边界平均差 X/Y 为 `0.001951 / 0.002598`，与内部相邻变化 `0.002123 / 0.002131` 同量级；4×4 和倾斜正交预览均无可见硬缝。
- 首次正式重建中连续红褐网状旧标线已被制作方否决；最终候选只保留稀疏、低对比、贴地的磨损痕迹。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；重复仍由单张材质周期决定，后续低模模块用于打破轮廓重复。
- 默认倾斜正交预览先读作暖黄积尘覆盖的灰黑工业废墟，混凝土、地坪和瓦砾层次清楚；没有建筑、资源节点或玩法标记。
- PBR 平面和球体检查显示裸露金属具有局部 Metallic，积尘与瓦砾保持低 Smoothness；不存在 BaseColor 烘焙高光或 Emission。
- `.kra` 含最终 BaseColor 和六类覆盖遮罩，共 7 个命名图层；`.blend` 已由 Blender 5.2.0 LTS 无界面重开，场景元数据为 `gameplay_truth=none`，四张正式贴图全部打包。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未制作 6–8 个低模废墟模块、FBX、Prefab、Collider、`DeepWater` 或 `Cliff` 正式资产。
