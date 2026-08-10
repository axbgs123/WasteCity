# 大型废墟 `Ruins` 低模模块包来源说明

- 关联需求：`IDEA-0004`
- 资产：`Ruins` 八件已批准低模环境模块
- 制作日期：2026-08-09
- 参考图：`Ruins_Modules_Approved_AI_Reference_v001.png`
- 参考图 SHA-256：`f72aa401942a0956f9d027486eb9639acc18825ef06f22776c5b0336f333458c`
- 批准记录：用户在查看 4×2 八件模块参考板后明确回复“通过”，随后才开始 Blender 建模；首版模型被否决后，按参考板逐件重建并增加模型内多材质分区与普通荒地组合预览，用户于 2026-08-09 明确回复“可以了”，批准当前版本
- 玩法真值：无；模型只提供环境视觉，不定义通行、资源、交互、碰撞或稳定 ID
- Unity 接入：当前已批准模型未接入正式场景、Prefab 或材质；八个 FBX 仅作为后续受控接入输入

## 输入来源与许可证

参考板由 Codex 内置图像生成工具生成，使用以下三项项目内已批准资产作为风格和材质参考：

| 输入 | 用途 | SHA-256 |
|---|---|---|
| `Ruins_Approved_AI_Concept_v001.png` | 工业废土造型与构成语言 | `f6c45cb0e9c8af0ce197c30080be6aa56ab74f3fd47eb165d8e7cb2a89de5834` |
| `T_Terrain_Ruins_BaseColor/Normal/Mask/Height.png` | 混凝土、沥青、积尘、锈蚀和骨料的共享 PBR 基础 | BaseColor `9bd361a9bb35deadda446bcd3a92daad67643b4c95b1e5b11bee007f56e17a5a`；Normal `4df13aa0877887065d4a892e0556d3cebd136b6084c2f4b2ae11b0f060569c5b`；Mask `34518e94df7a000ddf55749b84905fd33a7b94e61bae6539701c55c1aa5a88f7`；Height `15028f343750416c6b980e8e34664d69d9b07591fc2649982241abb7cac03404` |
| `QA_Terrain_Ruins_PBRCheck.png` | 粗糙度、对比度和默认镜头可读性 | `63d5013293eb6df6f3c529996e29dbde4a179e62926744bacfc3ed82d89da7d5` |
| `T_Terrain_Wasteland_BaseColor/Normal/Mask/Height.png` | 薄尘膜综合色温与普通荒地组合预览 | BaseColor `af95139005f9642fe209e5025fc126cf5a9fd514bc3846932cedf83dedea39ff`；Normal `48a8505e1c1a590356d2198836a4f463194cf8db9f2075c4e9b9e1e09e610742`；Mask `2e6906519e234b90abadf5deac531786950dfa2ab03bce80d2bacdbbb9c164f2`；Height `087b8c2fe636495761639d55b6e72521e6086b704c73aaa125b6e81bfaf83ad0` |

没有使用照片、扫描、资产商店模型、Sketchfab、Poly Haven、Hyper3D 或其他第三方模型/贴图。参考板的商业使用依据为项目用户委托并批准用于 WasteCity 私有项目；具体权利受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender 5.2.0 LTS 和 Python 只作为制作工具，不提供外部资产。

## 实际图像生成提示词

```text
Use case: stylized-concept
Asset type: executable 3D modeling reference sheet for the WasteCity Ruins low-poly environment module kit
Input images: Image 1 is the user-approved Ruins visual concept and defines the industrial-wasteland forms; Image 2 is the approved formal Ruins terrain BaseColor and defines concrete, asphalt, dust, rust, and aggregate colors; Image 3 is the approved formal PBR material check and defines restrained roughness, contrast, and default game-camera readability. Use them only as project style and material references.
Primary request: create one clean landscape 3D asset reference sheet showing exactly eight separate low-poly ruin modules, arranged in a strict 4-column by 2-row grid. Every cell contains exactly one isolated module with generous space around it. The eight modules, in reading order from top-left to bottom-right, are:
1) one irregular cracked industrial floor slab, mostly flat, chipped perimeter, roughly one world-grid cell footprint;
2) one low compact rubble pile made of small angular concrete chunks;
3) one different low elongated rubble pile mixing broken slab fragments and two very small rusty metal scraps;
4) one broken reinforced-concrete block with exactly three short bent rebar ends, no tall spikes;
5) one short broken large industrial pipe section, open circular end visible, dented and partly dust-filled;
6) one straight shallow drainage-channel module with chipped concrete lips and a dark recessed groove;
7) one damaged edge/curb plate module for blending a ruins boundary into dusty ground, low asymmetric silhouette;
8) one paper-thin worn industrial-marking surface plate/decal carrier with a fragmented faded ochre stripe, almost flat.
Style/medium: production-ready stylized low-poly 3D concept renders, industrial wasteland, game asset turnaround quality, clear manufacturable forms, restrained bevels, no photogrammetry.
Composition/framing: exact 4x2 grid, identical three-quarter orthographic camera angle for every module, about 35 degrees downward, consistent scale cues, all silhouettes fully visible, no overlap between cells. No combined scene.
Scene/backdrop: clean neutral warm-gray studio background with very subtle cell separators only; each object rests on the same invisible ground plane with a soft compact contact shadow.
Lighting/mood: neutral soft three-point material-preview lighting, no dramatic sun, no fog, no cinematic atmosphere.
Color palette: weathered concrete #55514A, dark damaged floor #393936, warm dust #756047, restrained rust/old marking #8A682E. Dust should appear on upper surfaces and crevices, matching the approved terrain.
Materials/textures: shared weathered concrete, damaged asphalt, ochre dust, sparing oxidized steel; readable broad material blocks suitable for a shared atlas. All modules should look compatible as one kit.
Modeling constraints: target 200–2,000 triangles per module; Y-up manufacturing logic; base sits at Y=0; no single module exceeds about one grid cell in footprint except the elongated rubble pile; all pieces are low and traversable-looking visual dressing; shapes must be straightforward to reproduce in Blender and export as FBX. No gameplay collider or interaction should be implied.
Text constraints: no titles, labels, numbers, letters, logos, dimension text, watermark, or UI. The requested ordering must be communicated only through the grid position and distinct silhouettes.
Avoid: more or fewer than eight modules, duplicated module designs, joined diorama, full building, standing wall, tall tower, staircase, vehicle, furniture, loot, resource node, crystals, vegetation, bones, characters, deep hole, cliff, water, glowing parts, tall exposed rebar, large pipe network, realistic photo scan, excessive tiny debris, busy background, perspective distortion, cut-off objects.
```

## 参考板到模型的固定映射

| 参考板位置 | 模型 |
|---|---|
| 第一行第 1 件 | `SM_Ruins_CrackedFloorSlab` |
| 第一行第 2 件 | `SM_Ruins_RubblePile_A` |
| 第一行第 3 件 | `SM_Ruins_RubblePile_B` |
| 第一行第 4 件 | `SM_Ruins_RebarConcreteBlock` |
| 第二行第 1 件 | `SM_Ruins_BrokenPipe` |
| 第二行第 2 件 | `SM_Ruins_DrainageChannel` |
| 第二行第 3 件 | `SM_Ruins_BoundaryEdge` |
| 第二行第 4 件 | `SM_Ruins_WornMarkingPlate` |

## Blender 重建方法

1. 以参考板的剪影、相对高度和材料分区为约束，使用盒体、低边圆柱、折线钢筋和不规则碎块程序化搭建。
2. 每件模块合并为一个独立 Mesh，应用变换，生成可编辑 UV，并把原点放在世界原点、最低点校正到 Blender `Z=0`。
3. 使用完整混凝土表皮、裸露骨料、暗色地坪/沟槽、暖黄碎尘、薄尘膜、氧化钢和旧标线多材质分区预览；薄尘膜与组合地面直接引用已批准 `Wasteland` PBR 资产，但不把 Blender 预览材质声明为最终 Unity 材质。
4. 每件控制在 200–2,000 三角面；除细长瓦砾堆允许略超一格外，其余模型占地接近或小于一个外城逻辑格。
5. 以 `-Z Forward / Y Up / Scale 1.0` 单独导出 FBX，不导出灯光、相机、碰撞体或玩法组件。
6. 在 Blender 5.2.0 LTS 中回读 `.blend` 和全部八个 FBX，并输出默认倾斜正交、普通荒地组合、顶视和线框四张固定 QA 图。
