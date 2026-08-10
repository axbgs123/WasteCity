# 大型废墟地表 `Ruins` 首版材质来源说明

- 关联需求：`IDEA-0004`
- 资产：`Ruins` 大型废墟地表首版正式材质
- 修订日期：2026-08-09
- 固定程序随机种子：`824401`
- 已批准概念：`Ruins_Approved_AI_Concept_v001.png`
- 概念 SHA-256：`f6c45cb0e9c8af0ce197c30080be6aa56ab74f3fd47eb165d8e7cb2a89de5834`
- 玩法真值：无；颜色、遮罩、Height、旧标线和贴地金属痕迹只表达视觉材质
- 模型交付：本阶段无；规格中的 6–8 个低模废墟模块属于后续独立建模步骤，开始前需再次通知用户

## 来源与批准记录

Codex 内置图像生成工具以项目中已批准的 `Wasteland_Approved_AI_Concept_v002.png`、`Rocky_Approved_AI_Concept_v001.png`、`Wetland_Approved_AI_Concept_v002.png` 和 `Crystal_Approved_AI_Concept_v002.png` 作为风格、暖黄母底、纹理尺度和完成度参考，生成新的 `Ruins` 地表概念。四张参考图只提供项目内部风格约束，不复制其唯一构图。

用户于 2026-08-09 查看首张 `Ruins` 概念候选后回复“通过”，批准 `Ruins_Approved_AI_Concept_v001.png` 进入正式 PBR 重建。未使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。生成概念的商业使用依据为项目用户委托并批准用于 WasteCity 私有项目；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。

用户随后查看正式重建的 4×4 平铺检查图、默认倾斜正交预览图和 PBR 材质检查图，并于 2026-08-09 回复“通过”，正式批准本版 `Ruins` 地表材质。用户同时明确要求所有建模类资产先生成参考图、经用户确认后再依据参考图建模；因此本材质验收不授权直接开始低模废墟模块建模。

## 最终概念提示词

```text
Use case: stylized-concept
Asset type: WasteCity Ruins terrain material concept preview for later seamless PBR reconstruction
Input images: Image 1 is the approved Wasteland mother-material reference; Image 2 is the approved Rocky scale and fracture reference; Image 3 is the approved Wetland finish reference; Image 4 is the approved Crystal palette and broad-patch reference. Use all four only for the established WasteCity stylized PBR finish, top-down scale, restrained contrast, warm-dust continuity, and surface-detail density. Do not copy any unique layout.
Primary request: create one square, straight top-down, orthographic-looking, seamless/tileable concept of an abandoned industrial ruins ground surface buried in a warm dry wasteland. It must read as terrain material, not as a scene, building, road, or gameplay object.
Composition and exact visual balance: approximately 35% weathered gray concrete plates, 20% damaged charcoal industrial asphalt/flooring, 20% warm ochre windblown dust, 15% embedded small rubble and crushed aggregate, 7% faint worn markings/drainage/old structural traces, and 3% exposed rusty metal fragments. Use irregular broad patches with no center focal point and no obvious repeating motif. Warm dust must visibly bridge every hard material and soften the outer edges while the ruins area remains unmistakably distinct.
Color anchors: weathered concrete #55514A, damaged dark floor #393936, dusty brown #756047, restrained rust/old marking #8A682E. Keep the overall result muted, dusty, warm-neutral, and compatible with the four approved references.
Materials/textures: chipped concrete, spalled aggregate, eroded asphalt, thin dust deposits, scattered embedded gravel, a few short cracked drainage seams or faded industrial line fragments, and extremely sparse flush rusty rebar/metal slivers. Everything stays close to ground level for future texture reconstruction. Stylized PBR material-preview finish with believable roughness differences but no baked directional lighting.
Framing: exact square crop, direct overhead, uniform texel scale comparable to the approved references, no horizon, no perspective, no border, edge-to-edge material.
Constraints: terrain surface only; broad medium-frequency forms dominate; details remain readable at a default tilted orthographic game camera; visually tileable edges; no one-grid-square pattern; no resource node; no gameplay truth; no text or watermark.
Avoid: standing walls, intact buildings, room layouts, recognizable road lanes, vehicles, furniture, large pipes, tall rebar, large rubble piles, cliffs, water, crystals, vegetation, bones, loot, characters, UI, signs with readable text, central drain cover, square floor-tile grid, cobblestone mosaic, dense black crack network, dramatic shadows, sunlight direction, baked highlights, neon, photogrammetry look, excessive micro-noise, obvious repetition.
```

## 正式重建方法

1. 将批准概念裁为正方形并重采样到 2048×2048。
2. 分离宽域亮度变化，不把概念照明、阴影或高光烘焙进 BaseColor。
3. 使用 periodic-plus-smooth 频域分解建立周期边界；后续噪声、模糊、中心差分和区域重组均使用周期边界。
4. 将批准概念进行三个方向的周期重组，只保留局部混凝土、破损地坪、积尘和瓦砾材质语言，避免直接复制概念中的唯一宏观构图。
5. 按风化混凝土 35%、破损工业地坪 20%、暖黄积尘 20%、碎石和瓦砾 15%、旧标线/排水/结构痕迹 7%、裸露金属 3% 精确选择互斥区域，再生成共享软混合权重。
6. BaseColor 使用 `#55514A`、`#393936`、`#756047`、`#8A682E` 色锚；旧标线与金属保持低对比、短碎和贴地，不形成红褐网格。
7. Height 由六类共享区域权重、独立周期宏观场、独立周期高频场、盆地边界和局部材料起伏共同构建，不是 BaseColor 灰度复制；全部起伏保持贴地，不替代后续低模模块。
8. Tangent Space Normal 从 16-bit Height 的周期中心差分生成；URP Mask 四通道分别生成 Metallic、AO、Detail Mask 和 Smoothness。
9. 首次正式重建因旧标线被强化成连续红褐网状线而由制作方淘汰；最终版降低其连续性和显色，仅保留稀疏磨损痕迹。

## 固定构成与尺度

二值构成遮罩按 2048×2048 像素精确分配：

| 构成 | 像素数 | 面积比例 |
|---|---:|---:|
| 风化混凝土 | 1,468,006 | 34.999990% |
| 破损工业地坪 | 838,861 | 20.000005% |
| 暖黄积尘 | 838,861 | 20.000005% |
| 碎石和瓦砾 | 629,146 | 15.000010% |
| 旧标线、排水或结构痕迹 | 293,601 | 6.999993% |
| 裸露金属 | 125,829 | 2.999997% |

一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。Blender 文件中的平面和球体只用于离线材质检查，不是正式模型。后续 6–8 个低模废墟模块只负责打破平面与轮廓，不得改变 `Ruins` 的逻辑范围、通行规则、部署规则或任何稳定 ID。
