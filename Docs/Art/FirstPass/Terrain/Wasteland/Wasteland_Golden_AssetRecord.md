# 普通荒地 `Wasteland` 黄金样板资产记录

## 记录状态

- 生产状态：制作方输出与自动化验收完成，等待用户视觉确认
- 创建日期：2026-08-08
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Wasteland` 黄金样板；未接入 Unity 运行时，未创建其余六类正式地形
- Git：本记录与资产位于同一资产提交，提交信息为 `art: add wasteland terrain golden sample`

## 来源与许可证

所有正式像素均为本项目原创、确定性程序化输出。未使用照片、扫描、第三方纹理、下载素材、AI 概念图或 AI 生成贴图，因此没有第三方图像许可证依赖。资产供 WasteCity 项目所有者在项目中使用；Blender、Krita、Python、NumPy 和 Pillow 仅作为制作工具，不把工具本身的许可证内容复制进交付资产。

Blender MCP v1.2 已安装并完成场景读取验证，但本黄金样板的正式像素由本地确定性脚本生成，MCP、Poly Haven、Sketchfab 和 Hyper3D 均未作为像素或模型来源。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Blender | 5.2.0 LTS，hash `fbe6228777e7`，2026-07-14 build | 固定相机、PBR 平面/球体、贴图打包、离线预览；渲染器 EEVEE |
| Blender MCP | 1.2；MCP SDK 1.3.0 | 制作机自动化桥接验证；未提供像素来源 |
| Krita | 5.2.16 | 将 10 层 OpenRaster 中间源转换并保存为分层 `.kra` |
| Python | 3.12.13 | 确定性程序化生成与数据验收 |
| NumPy | 2.3.5 | 周期噪声、频域平滑、通道计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height、4×4 检查图 |

- 随机种子：`812804`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示，不烘焙回正式贴图
- 纹理尺度：一套纹理约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G AO / B Detail Mask / A Smoothness
- 模型交付规则：项目 Unity 模型主交付格式为 FBX；本地形样板没有正式模型或 FBX，仅含材质检查用 Blender 基础平面与球体

## 视觉与 PBR 结果

- 构成比例：压实土 65%、浮尘 18%、砾石 10%、浅裂纹 4%、工业痕迹 3%；实际二值像素比例误差小于 `0.00001` 个百分点。
- BaseColor 归一化范围：R `0.388235–0.556863`，G `0.313725–0.458824`，B `0.231373–0.341176`。
- Height：`0.451377–0.603220`，平均 `0.512809`，保留后续地表混合余量。
- Normal：平均长度 `1.000223`，Z 最小 `0.811765`、平均 `0.993780`，为向上、低起伏 Tangent Space Normal。
- Metallic：`0–0.050980`，平均 `0.000146`。
- AO：`0.819608–1.0`，未使用重黑 AO 伪造深度。
- Detail Mask：`0.2–0.945098`。
- Smoothness：`0.101961–0.231373`，平均 `0.172899`。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_BaseColor.png` | 2048² RGB 8-bit / sRGB | `ca5a21b0ab8d4a7344d07764867a34c0ce68027bea7396cb04efb27e0d4708e5` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Normal.png` | 2048² RGB 8-bit / Linear | `2fe85c5c11cdfc3c17c5c773c68c5815582f2d5b4fe1488d40c147fba2858147` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Mask.png` | 2048² RGBA 8-bit / Linear | `d6c6c50ead6ccb6c244887f53f6f45c648230af0effc5cbbe89beca88815ebed` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Height.png` | 2048² Gray 16-bit / Linear | `37eff23a342a2bcc52500f5f1e525fc49501c7176e12e594bd9676637260bc23` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/Wasteland_Golden_Master.kra` | 10 个命名图层载荷 | `9b131c0990ca915d311081ba0565be43bbb7d31ce71c5e43571d3393f2f5e42d` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/Wasteland_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `68a210dd8dc770bc127cb69022af5cacf7286329014258da5c65904561f1682d` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/References/Wasteland_Golden_SourceNotes.md` | UTF-8 Markdown | `03f7f84b33d959a4381a78e6087f926f74569a24ad05f70d0c19835cca67b1e8` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_Tiling4x4.png` | 2048² RGB 8-bit | `33caa99112318807444a7eb88ae03b3162d667b59d3efd83b9cb0933693ba881` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `cdc521a3359495ccff997cc165a3643fa8db85708f500ad1c7c2cb62121f542e` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_PBRCheck.png` | 1920×1080 RGBA 8-bit | `a85d4ce411dfc542ccd715fb64260a34c64806fcedca35bc990ca66f742a17ff` |

资产记录自身不登记自身 SHA-256，避免每次写入校验值都会改变同一文件；其完整性由包含它的 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 尺寸与 IHDR 位深/色型通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16。
- 四张贴图的跨边界平均差均不高于相邻像素内部平均差，未检测到水平、垂直或四角硬接缝。
- 4×4 图未出现边缘十字或棋盘接缝；默认倾斜正交镜头读取为暖黄干旱、低起伏普通荒地。
- PBR 平面和球体检查未显示金属化、湿泥或塑料表面；Normal 向上且无反转。
- `.kra` 含 10 个图层载荷和有效 `maindoc.xml`；`.blend` 可由 Blender 5.2.0 LTS 无界面打开，包含打包贴图、固定相机、灯光、平面和球体。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未生成 `Rocky`、`Wetland`、`Crystal`、`Ruins`、`DeepWater` 或 `Cliff` 正式贴图。
