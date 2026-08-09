# 深水 `DeepWater` 首版材质资产记录

## 记录状态

- 生产状态：重制版完成并于 2026-08-09 通过用户视觉验收
- 概念批准：用户查看 `DeepWater_Approved_AI_Concept_v001.png` 后回复“通过”
- 正式验收：首轮正式结果因“不像水”被否决；用户查看重制后的默认倾斜正交与 PBR 检查图后回复“可以”
- 操作者：Codex，代表 WasteCity 项目执行受控资产生产
- 范围：仅 `DeepWater` 地表材质、分层源和离线 QA；未创建水面模型、FBX、Prefab、Collider、Shader Graph 或运行时材质
- 接入状态：未接入 Unity 场景、VisualSlot、地形映射或玩法系统
- 玩法边界：无玩法真值；不保存地形类型、阻挡、通行倍率、资源节点、稳定 ID 或存档数据
- 交付提交：`195364d`（批准材质与资产主体）

## 来源、许可证与工具

正式 BaseColor 以用户批准的 AI 概念作为色彩、污染程度和水面语言参考，但没有直接把概念图裁切为整套 PBR 通道。Height 由独立周期波场构建，Normal 由 Height 派生，Mask 四通道独立生成。没有使用照片、扫描、素材商店或第三方纹理。

生成概念的商业使用依据为项目用户委托并明确批准；具体权利受生成时所用 OpenAI 服务条款和项目账户条款约束。工具本身不提供像素版权来源。

| 工具 | 版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的 DeepWater 视觉概念，不直接生成其余 PBR 通道 |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 固定相机、PBR 平面/球体、贴图打包和 EEVEE 离线 QA |
| Blender MCP | 1.2，MCP SDK 1.3.0 | 项目建模自动化桥；本地形没有正式模型交付 |
| Python | 3.11.9 | 周期重建与自动化验证 |
| NumPy | 2.2.4 | 周期波场、区域遮罩、Height、Normal 和 Mask 计算 |
| Pillow | 12.2.0 | PNG、16-bit Height、4×4 检查图和 OpenRaster 图层编码 |
| OpenRaster | 0.0.1 | 五层无损分层源；属于合同允许的等价分层格式 |

## 固定设计与结果

- 随机种子：`813246`
- 正式贴图：2048×2048
- 纹理尺度：约覆盖 `4×4` 外城逻辑格，约 512 px/格
- 构成比例：蓝黑不透明水体 75%、缓慢涟漪 15%、油膜污染 7%、锈橙沉积 3%
- BaseColor 平均 RGB：`0.078921 / 0.130511 / 0.157920`
- BaseColor RGB 范围：`0.062745–0.149020 / 0.105882–0.207843 / 0.133333–0.247059`
- Height：16-bit 值 `30625–35159`，共 `4535` 个离散值；不是 BaseColor 灰度复制
- Normal：平均长度 `1.000200`，最小 `1.000015`，最大 `1.002964`，整体向上
- Metallic：恒为 `0`
- AO：`0.976471–1.0`
- Detail Mask：`0.101961–0.717647`
- Smoothness：`0.784314–0.941176`，符合设计目标 `0.78–0.94` 的 8-bit 量化误差
- Blender 源元数据：`gameplay_truth=none`、`coverage_ratios=75/15/7/3`、批准概念 v001、四张贴图全部打包

## 文件登记

| 文件 | 格式 / 色彩空间 | SHA-256 |
|---|---|---|
| `Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_BaseColor.png` | 2048² RGB 8-bit / sRGB | `769e252bb0cad150b1f99cb5dc929a0bc91756fb7033f143950e2bc0f3fdc561` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Normal.png` | 2048² RGB 8-bit Tangent Space / Linear | `5820a3d0eedec789aae53c3bc40a51d56b42fdfebf0ce62d28de841a03498e57` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Mask.png` | 2048² RGBA 8-bit / Linear | `bd646794954c6b5ea86e7f17014cd3f989ca2938e16d894e01909422706c0844` |
| `Assets/_Game/Art/FirstPass/Environment/Terrain/DeepWater/T_Terrain_DeepWater_Height.png` | 2048² Gray 16-bit / Linear | `61402497871ad571f13606462e3087735dbaa2ff9998fd14451731a4f34b10df` |
| `ArtSource/FirstPass/Environment/Terrain/DeepWater/References/DeepWater_Approved_AI_Concept_v001.png` | 1254² RGB 8-bit / 已批准概念 | `282ab23c61c45272d40fddaed56bef6282f36eb6f449b7e10e48f9d8f5a164d4` |
| `ArtSource/FirstPass/Environment/Terrain/DeepWater/DeepWater_Golden_Master.ora` | 五个命名图层的无损 OpenRaster 分层源 | `004887d1932f3b9d135c09ded2b6bbad66e54c9c4e0751afa6476345557227e2` |
| `ArtSource/FirstPass/Environment/Terrain/DeepWater/DeepWater_Golden_Generator.blend` | Blender 5.2.0 LTS，四张贴图已打包 | `ec9b799494e1f08efca60867be64c3d99af26bf01e0a89cc8083ff3167e084f7` |
| `ArtSource/FirstPass/Environment/Terrain/DeepWater/DeepWater_Golden_Generator.py` | UTF-8 Python，可重复生成源 | `8d28d1ea281a7b901db4e553246c43c30c9bb07acd61a030c6872cde206102f3` |
| `ArtSource/FirstPass/Environment/Terrain/DeepWater/References/DeepWater_Golden_SourceNotes.md` | UTF-8 Markdown | `5e41584f943d353be6452f0b7c7f473a0a602a44df467ea99f27a21cfe047ce0` |
| `Docs/Art/FirstPass/Terrain/DeepWater/QA_Terrain_DeepWater_Tiling4x4.png` | 2048² RGB 8-bit | `6f49bf4303b1e67db9da6ea997e6f6751f0613660106960387a446b168b98389` |
| `Docs/Art/FirstPass/Terrain/DeepWater/QA_Terrain_DeepWater_DefaultOrtho.png` | 1920×1080 RGBA 8-bit | `4c65a08e0ebb6af5cf00beb840749a311500d8e2fbbc6ac64c835f8a61c8304f` |
| `Docs/Art/FirstPass/Terrain/DeepWater/QA_Terrain_DeepWater_PBRCheck.png` | 1920×1080 RGBA 8-bit | `18eec8a013616ef2d3d0773d6b4a93a4b1b005d8d515262c27f5434c910948e7` |

资产记录自身不登记自己的 SHA-256，避免自引用改变文件；完整性由 Git 提交对象保证。

## 验收记录

- 四张正式 PNG 的 IHDR 通过：BaseColor RGB8、Normal RGB8、Mask RGBA8、Height Gray16，尺寸均为 2048×2048。
- BaseColor X/Y 边界平均差 `0.000001 / 0.000001`，低于内部相邻变化 `0.000154 / 0.000118`。
- Normal X/Y 边界平均差 `0.000097 / 0.000075`，低于或不高于内部变化 `0.000124 / 0.000113`。
- Mask X/Y 边界平均差 `0.000106 / 0.000091`，低于内部变化 `0.000764 / 0.000708`。
- Height X/Y 边界平均差 `0.000001 / 0.000001`，低于内部变化 `0.000144 / 0.000104`。
- 4×4 检查图没有硬接缝、黑线或棋盘边界；重制版不再把涟漪、油膜或沉积画成独立地面色块。
- 默认倾斜正交和 PBR 平面/球体检查能读出连续蓝黑水体、明显湿润高光与水面缓波；用户明确回复“可以”。
- 分层 OpenRaster 包含 `mimetype`、`stack.xml`、合并图、缩略图和 5 个图层文件，可无损回读。
- Blender 5.2.0 LTS 无界面重新打开成功，包含 PBR 平面/球体、固定相机、三盏灯、嵌入脚本、README 和四张打包贴图。
- 正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均未修改。
