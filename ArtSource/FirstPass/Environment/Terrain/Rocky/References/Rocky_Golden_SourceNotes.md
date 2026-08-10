# 岩石地 `Rocky` 首版材质来源说明

- 资产：`Rocky` 岩石地首版正式材质
- 修订日期：2026-08-08
- 固定程序随机种子：`812913`
- 已批准概念：`Rocky_Approved_AI_Concept_v001.png`
- 概念 SHA-256：`72dd8cd9806388d3a9d4eb073cc13d0997291a1efbfe2313c0f083819f0ad7de`
- 玩法真值：无；颜色、遮罩和 Height 只表达视觉材质
- 模型交付：无；当前只制作地表贴图，不创建 FBX、Prefab 或碰撞体

## 来源与批准记录

Codex 内置图像生成工具以项目中已批准的 `Wasteland_Approved_AI_Concept_v002.png` 作为风格、色系、表面尺度和完成度参考，生成一张新的 `Rocky` 岩石地概念。普通荒地概念只作为风格参考；正式 `Rocky` 没有复制普通荒地资产，也没有把其他地形、资源节点或玩法数据混入地表。

用户于 2026-08-08 查看概念预览并回复“可以”，批准该图进入正式 PBR 重建。未使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。生成概念的商业使用依据为项目用户委托并批准用于 WasteCity 私有项目；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。

## 概念生成提示词

```text
Use case: stylized-concept
Asset type: WasteCity terrain material concept preview for the Rocky ground category
Input image: use the supplied approved Wasteland concept only as the exact project style, palette-family, surface scale, and rendering-quality reference; create a distinct Rocky terrain, not a copy.
Primary request: create a square, straight top-down orthographic view of continuous warm arid rocky wasteland ground, suitable as the visual concept for a future seamless game terrain material.
Subject: roughly 55% broad layered stone plates and fractured bedrock shelves, 25% compact ochre soil between the plates, 15% angular gravel and small chips gathered along cracks, 5% fine dust. The stone plates should be visibly larger and denser than in the Wasteland reference, with readable stratification, chipped edges, branching cracks, shallow erosion and controlled variety. This is traversable rocky ground, not an impassable cliff.
Style/medium: polished stylized PBR game-material concept, believable geology with gently simplified shapes, matching the approved reference's warm, grounded, non-cartoon realism.
Composition/framing: perfectly top-down, evenly distributed surface detail, no focal object, no horizon, no perspective, no border, no grid, no obvious directional streak, no isolated landmark.
Lighting/mood: flat neutral material-preview illumination with very soft ambient occlusion only; avoid baked directional sunlight and deep cast shadows.
Color palette: warm yellow-ochre and dusty umber base; rock plates slightly cooler muted brown-gray for separation; very restrained desaturated rust accents below 4%; no blue glow and no saturated colors.
Constraints: ground surface only; visually plausible for tiling; consistent texel scale; edges should not contain conspicuous unique features; no vegetation, water, crystals, ore nodes, ruins, roads, tracks, bones, buildings, machines, characters, UI, text, symbols, decals or watermark.
Avoid: cliff faces, vertical walls, giant boulders, resource deposits, gameplay markers, photogrammetry look, dramatic cinematic lighting, excessive micro-noise, overly dark image, obvious repeating motifs.
```

## 正式重建方法

1. 将批准概念裁为正方形并重采样到 2048×2048。
2. 分离宽域亮度变化，保留岩板、裂隙和碎石的材料信息，不把概念照明烘焙进 BaseColor。
3. 使用 periodic-plus-smooth 频域分解得到周期 BaseColor；所有后续噪声、模糊和中心差分均采用周期边界。
4. 按岩板 55%、裸土 25%、碎石 15%、浮尘 5% 精确选择互斥区域，再以周期高斯柔化成共享混合权重。
5. 岩板使用偏冷的低饱和棕灰，裸土和浮尘保持暖黄赭色，使其与普通荒地柔和兼容但保持结构差异。
6. Height 由共享区域权重、独立周期宏观场、独立周期高频场、独立周期岩板断裂场和带通材料结构共同构建，不是 BaseColor 灰度复制。
7. Normal 只从最终 16-bit Height 的周期中心差分派生。
8. URP Mask 独立制作：R Metallic 恒为非金属，G Ambient Occlusion 表达裂隙和碎石凹部，B Detail Mask 表达细节响应，A Smoothness 保持干燥低光滑度。
9. 分层 `.kra` 保存最终 BaseColor 和四个命名覆盖遮罩；`.blend` 保存生成脚本、固定种子、固定相机、灯光、四张打包贴图和 PBR 检查平面/球体。

二值构成遮罩按 2048×2048 像素精确分配：

| 构成 | 像素数 | 面积比例 |
|---|---:|---:|
| 层状岩板 | 2,306,867 | 54.999995% |
| 裸露赭黄土 | 1,048,576 | 25.000000% |
| 裂隙碎石 | 629,146 | 15.000010% |
| 表面浮尘 | 209,715 | 4.999995% |

一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。Blender 文件中的平面和球体只用于离线材质检查，不是正式模型，也不产生 FBX 交付物。
