# 岩石地 `Rocky` 首版材质资产记录

## 记录状态

- 生产状态：用户已于 2026-08-08 验收通过 v1 正式预览；`Rocky` 首版材质已批准
- 概念批准：用户于 2026-08-08 查看 `Rocky` 概念预览并回复“可以”
- 正式验收：用户已查看正式 4×4、倾斜正交和 PBR 检查图，并回复“通过”
- 修订日期：2026-08-08
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Rocky` 地表材质；未接入 Unity 运行时，未创建模型，也未制作其余五类未完成地形
- Git：本记录与资产位于同一提交，提交信息为 `art: add approved rocky terrain material`

## 来源与许可证

正式 BaseColor 以用户批准的 `Rocky_Approved_AI_Concept_v001.png` 为颜色和表面结构输入。该概念由 Codex 内置图像生成工具生成，并仅使用已批准的 `Wasteland` 概念作为项目风格、色系、尺度和完成度参考。用户明确批准后才进入正式重建。

Height 由共享区域权重、带通材料结构、独立周期宏观场、独立周期高频场和独立岩板断裂场共同构建，不是概念图或 BaseColor 的灰度复制；Normal 由 16-bit Height 派生；Mask 四通道分别制作。没有使用照片、扫描、材质商店或第三方贴图。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Krita、Python、NumPy 和 Pillow 仅作为制作工具。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线预览 |
| Blender MCP | 1.2；MCP SDK 1.3.0 | 制作机自动化桥接已安装；未提供像素来源 |
| Krita | 5.2.16 | 将 5 层 OpenRaster 中间源保存为分层 `.kra` |
| Python | 3.11.9 | 周期重建与自动化验收 |
| NumPy | 2.2.4 | 频域周期分解、区域权重、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height 和 4×4 检查图 |

- 随机种子：`812913`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示
- 纹理尺度：约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G Ambient Occlusion / B Detail Mask / A Smoothness
- 模型规则：Unity 模型主交付格式仍为 FBX；本地形资产没有模型或 FBX

## 视觉与 PBR 结果

- 构成比例：层状岩板 55%、裸露赭黄土 25%、裂隙碎石 15%、表面浮尘 5%；二值像素误差小于 `0.00001` 个百分点。
- BaseColor：R `0.278431–0.749020`，G `0.152941–0.650980`，B `0.039216–0.517647`；平均 RGB `0.517472 / 0.399488 / 0.251694`。
- Height：`0.437354–0.609812`，平均 `0.534772`，包含 `11076` 个离散 16-bit 值。
- Normal：平均长度 `1.000329`，最小长度 `0.993845`，Z 最小 `0.662745`、平均 `0.984359`；整体向上。
- Metallic：恒为 `0`，岩地整体非金属。
- AO：`0.835294–1.0`，平均 `0.974273`。
- Detail Mask：`0.196078–0.988235`，平均 `0.654266`。
- Smoothness：`0.058824–0.149020`，平均 `0.112313`，保持干燥粗糙表面。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_BaseColor.png` | 2048² RGB 8-bit / sRGB | `abacced339056ad3eb10855e8b04f7af325be88de367183d10c518aa637a466a` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Normal.png` | 2048² RGB 8-bit / Linear | `851d35813daad8f2650b5236564c959f3d15cbb352927a8b8d3986e0638be811` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Mask.png` | 2048² RGBA 8-bit / Linear | `bc55666df6093523d5f88e7c526fcef3063e05f4cec9c83488eed1de952bddcd` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Rocky/T_Terrain_Rocky_Height.png` | 2048² Gray 16-bit / Linear | `e7c8aa402211e377329e94936c0f5143e28aaf64b8bfc5a4f96b26205ed1b801` |
| `ArtSource/FirstPass/Environment/Terrain/Rocky/References/Rocky_Approved_AI_Concept_v001.png` | 1254² RGB 8-bit / 已批准概念 | `72dd8cd9806388d3a9d4eb073cc13d0997291a1efbfe2313c0f083819f0ad7de` |
| `ArtSource/FirstPass/Environment/Terrain/Rocky/Rocky_Golden_Master.kra` | 5 个命名图层载荷 | `84abd141543bd792a4e9b4613d9d65bd8ca2643333dd101c833d958f11f63d9c` |
| `ArtSource/FirstPass/Environment/Terrain/Rocky/Rocky_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `b6a284fed41995a5bceb693c6686ae3272c87dfdeadbdc9ab449ca2c696e1d41` |
| `ArtSource/FirstPass/Environment/Terrain/Rocky/Rocky_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `39e5874a1008afca2000d0f2826b6845bf9a6c8f558d36383de5c360180ba416` |
| `ArtSource/FirstPass/Environment/Terrain/Rocky/References/Rocky_Golden_SourceNotes.md` | UTF-8 Markdown | `148fac775159ab2efa623ff7e41a7a6e1c8e8d9bbaaaa06f9bda2809bdc04978` |
| `Docs/Art/FirstPass/Terrain/Rocky/QA_Terrain_Rocky_Tiling4x4.png` | 2048² RGB 8-bit | `c1597372066c8eda2ed26dbc2364439aa74e55527c63381de89a05c3433b1fd9` |
| `Docs/Art/FirstPass/Terrain/Rocky/QA_Terrain_Rocky_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `98b929a11e330b8cce77a12ac8d615266e31558ff3aff35ec4fd9b724f0d0a6d` |
| `Docs/Art/FirstPass/Terrain/Rocky/QA_Terrain_Rocky_PBRCheck.png` | 1920×1080 RGBA 8-bit | `02567946224666a244872dc0e66cf53b9720169fa031534acb1549065677dcb2` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.002016 / 0.001594`，低于内部相邻变化 `0.009656 / 0.012544`。
- Height 跨边界平均差 X/Y 为 `0.000079 / 0.000105`，低于内部相邻变化 `0.001320 / 0.001526`；Normal 和 Mask 同样满足边界变化低于内部变化。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；岩板面积和碎石通道在缩小后仍可读取。
- PBR 平面和球体检查显示低金属、低光滑度和向上的 Tangent Space Normal，没有湿泥或塑料感。
- `.kra` 含最终 BaseColor 和四类覆盖遮罩；`.blend` 可由 Blender 5.2.0 LTS 无界面重新打开，包含生成脚本和四张打包贴图。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未制作 `Wetland`、`Crystal`、`Ruins`、`DeepWater` 或 `Cliff` 正式贴图。
