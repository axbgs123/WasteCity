# 普通荒地 `Wasteland` 黄金样板资产记录

## 记录状态

- 生产状态：用户已于 2026-08-08 验收通过 v2 正式预览；普通荒地黄金样板已批准
- 用户验收：已查看正式 4×4 平铺检查图与 PBR 材质检查图，并回复“可以”
- 修订日期：2026-08-08
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `Wasteland` 黄金样板；未接入 Unity 运行时，未创建其余六类正式地形
- Git：本记录与资产位于同一提交，提交信息为 `art: rebuild wasteland from approved style reference`

## 来源与许可证

正式 BaseColor 以用户批准的 `Wasteland_Approved_AI_Concept_v002.png` 为颜色和表面结构输入。该概念由 Codex 内置图像生成工具根据用户提供的风格截图生成，用户于 2026-08-08 明确回复“可以”后才进入正式重建。用户截图仅提供风格方向，未保存到仓库或直接复制像素。

Height 由带通材料结构、独立周期场和区域物理响应重新构建，不是概念图或 BaseColor 的灰度复制；Normal 由 16-bit Height 派生；Mask 四通道分别制作。没有使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方贴图。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Krita、Python、NumPy 和 Pillow 仅作为制作工具。

## 工具与固定设置

| 工具 | 完整版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线预览 |
| Blender MCP | 1.2；MCP SDK 1.3.0 | 制作机自动化桥接验证；未提供像素来源 |
| Krita | 5.2.16 | 将 6 层 OpenRaster 中间源保存为分层 `.kra` |
| Python | 3.12.13 | 周期重建与自动化验收 |
| NumPy | 2.3.5 | 频域周期分解、遮罩、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG 编码、16-bit Height 和 4×4 检查图 |

- 随机种子：`812804`
- 正式贴图：2048×2048
- 预览：1920×1080，AgX Medium High Contrast，仅用于离线展示
- 纹理尺度：约覆盖 4×4 外城逻辑格，约 512 px/格
- BaseColor：sRGB；Normal、Mask、Height：Linear 数据
- Mask：R Metallic / G AO / B Detail Mask / A Smoothness
- 模型规则：Unity 模型主交付格式仍为 FBX；本地形样板没有正式模型或 FBX

## 视觉与 PBR 结果

- 构成比例：压实土 65%、浮尘 18%、砾石 10%、浅裂纹 4%、工业痕迹 3%；二值像素误差小于 `0.00001` 个百分点。
- BaseColor：R `0.172549–0.776471`，G `0.062745–0.658824`，B `0.011765–0.545098`；平均 RGB `0.553757 / 0.405485 / 0.221319`。
- Height：`0.424994–0.636301`，平均 `0.495299`。
- Normal：平均长度 `1.000382`，Z 最小 `0.568627`、平均 `0.981281`；整体向上，局部嵌石允许更陡响应。
- Metallic：`0–0.047059`，平均 `0.000011`。
- AO：`0.721569–1.0`，平均 `0.984381`。
- Detail Mask：`0.184314–0.952941`。
- Smoothness：`0.070588–0.196078`，平均 `0.130662`。

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_BaseColor.png` | 2048² RGB 8-bit / sRGB | `af95139005f9642fe209e5025fc126cf5a9fd514bc3846932cedf83dedea39ff` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Normal.png` | 2048² RGB 8-bit / Linear | `48a8505e1c1a590356d2198836a4f463194cf8db9f2075c4e9b9e1e09e610742` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Mask.png` | 2048² RGBA 8-bit / Linear | `2e6906519e234b90abadf5deac531786950dfa2ab03bce80d2bacdbbb9c164f2` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/Wasteland/T_Terrain_Wasteland_Height.png` | 2048² Gray 16-bit / Linear | `087b8c2fe636495761639d55b6e72521e6086b704c73aaa125b6e81bfaf83ad0` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/References/Wasteland_Approved_AI_Concept_v002.png` | 1254² RGB 8-bit / 已批准概念 | `db9bbc2d6f0d81eb095018fe768c6c29e5505e82d425aa1db0e39261c92b4e5d` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/Wasteland_Golden_Master.kra` | 6 个命名图层载荷 | `3813e4ab6283a99c44783f069cbd0e800d3e73778a5fceb54012193919b20bd5` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/Wasteland_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `0ef955807c218aace663d01b0faea70cb138b7013676a2c96f51bfad90287e43` |
| `ArtSource/FirstPass/Environment/Terrain/Wasteland/References/Wasteland_Golden_SourceNotes.md` | UTF-8 Markdown | `0d8dfe2029e1c2040037d4ccea9bd44d11c74a96ff2942d851e1f4558b823c36` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_Tiling4x4.png` | 2048² RGB 8-bit | `d611a688c6f51ca4f1435e996997be601a1c7a079861c3e6909812aa16dd9e4a` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `67190b558dec31a800218ef65b63fff09f3d8b313d69fe6a849c2aadf5529912` |
| `Docs/Art/FirstPass/Terrain/Wasteland/QA_Terrain_Wasteland_PBRCheck.png` | 1920×1080 RGBA 8-bit | `aaca67f43bf8858ade73f2c3fec37e36313d35a18101014778d9529cdd0225af` |

资产记录自身不登记自身 SHA-256，避免自引用改变文件；其完整性由 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor 跨边界平均差 X/Y 为 `0.002411 / 0.002145`，显著低于内部相邻变化 `0.012316 / 0.014440`；Height、Normal 和 Mask 同样满足边界变化低于内部变化。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；默认镜头读取为参考风格的暖褐分层硬土和嵌入式碎石。
- PBR 平面和球体检查未显示金属化、湿泥或塑料表面；Normal 整体向上且无反相。
- `.kra` 含最终 BaseColor 和五类覆盖遮罩；`.blend` 可由 Blender 5.2.0 LTS 无界面重新打开并包含四张打包贴图。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
- 未生成 `Rocky`、`Wetland`、`Crystal`、`Ruins`、`DeepWater` 或 `Cliff` 正式贴图。
