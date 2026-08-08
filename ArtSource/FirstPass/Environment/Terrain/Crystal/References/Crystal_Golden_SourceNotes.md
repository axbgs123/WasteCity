# 结晶地表 `Crystal` 首版材质来源说明

- 关联需求：`IDEA-0004`
- 资产：`Crystal` 结晶地表首版正式材质
- 修订日期：2026-08-09
- 固定程序随机种子：`824219`
- 已批准概念：`Crystal_Approved_AI_Concept_v002.png`
- 概念 SHA-256：`921d53e77b4195ee1264a7439b75926b30790f402d715fbfcd47010cdef8b591`
- 玩法真值：无；颜色、遮罩、晶壳、晶脉、Smoothness 和 Height 只表达视觉材质
- 模型交付：无；当前只制作地表贴图，不创建能晶节点、晶簇模型、FBX、Prefab 或 Collider

## 来源与批准记录

Codex 内置图像生成工具以项目中已批准的 `Wasteland_Approved_AI_Concept_v002.png`、`Rocky_Approved_AI_Concept_v001.png` 和 `Wetland_Approved_AI_Concept_v002.png` 作为风格、暖黄母底、纹理尺度和完成度参考，生成新的 `Crystal` 地表概念。第一稿的青色晶脉过亮、贯穿范围过大且局部碎裂纹像科幻电路，因此没有进入正式重建。

第二稿只降低晶脉亮度、饱和度、宽度和总长度，减少碎裂纹并恢复暖黄荒土主导比例。用户查看第二稿后回复“通过”，批准 `Crystal_Approved_AI_Concept_v002.png` 进入正式 PBR 重建。未使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。生成概念的商业使用依据为项目用户委托并批准用于 WasteCity 私有项目；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。

用户随后查看正式重建的 4×4 平铺检查图、默认倾斜正交预览图和 PBR 材质检查图，并于 2026-08-09 回复“通过”，正式批准本版 `Crystal` 地表材质。该正式验收不改变 `Crystal` 地表与 `ResourceNodes/EnergyCrystal` 可采集节点之间的资产及玩法边界。

## 最终概念修订提示词

```text
Use case: stylized-concept
Asset type: revised WasteCity Crystal terrain material concept preview
Input images: Image 1 is the Crystal draft to edit; Images 2–4 are approved WasteCity terrain style and scale references.
Primary request: revise only Image 1 so the Crystal surface reads as subtle geological contamination embedded in mostly warm dry wasteland soil, not sci-fi circuitry or a cracked paving mosaic.
Targeted changes: reduce the brightness, saturation, thickness, and total visible length of cyan veins by about 55%; break long connected vein paths into sparse short mineral traces with no glow halo. Reduce dense small polygonal crack/chip texture by about 40%, merging it into broader compacted soil and glassified plates. Slightly reduce the total gray-cyan crust area and return that area to warm ochre soil so warm ground remains visually dominant near 65%.
Preserve: Image 1's exact square top-down framing, broad distribution of dark glassified patches, muted gray-cyan crust character, project texel scale, and no central focal point. Preserve the approved references' restrained stylized PBR finish.
Color/material: warm soil #61503A remains dominant; dark glass #263234; desaturated dusty crust #526563; rare dim accents #4D8F92. Cyan must be a faint mineral stain, not emitted light.
Lighting: keep flat neutral material-preview lighting and restrained specular response; remove any impression of baked glow.
Constraints: change only the vein intensity/density, micro-crack density, and soil/crust balance; no resource node, no crystal cluster, no upright crystal, no collectible, no gameplay marker, no grid, no cliff, no water, no ruins, no plants, no road, no characters, no UI, no text, no logo, no watermark.
Avoid: neon, luminous cracks, circuitry, fantasy magic pattern, giant gemstone, geode, crystal spikes, repeated radial veins, cobblestone mosaic, dramatic shadows, black empty regions.
```

## 正式重建方法

1. 将批准概念裁为正方形并重采样到 2048×2048。
2. 分离宽域亮度变化，不把概念照明、阴影或高光烘焙进 BaseColor。
3. 使用 periodic-plus-smooth 频域分解建立周期边界；所有后续噪声、模糊、中心差分和区域重组均使用周期边界。
4. 将批准概念进行三个方向的周期重组，只保留其局部土壤、玻化表面和晶壳材质语言，避免直接复制概念中的唯一宏观构图。
5. 按暖黄荒土 65%、深色玻化地表 18%、灰青晶壳 10%、细晶脉 4%、灼烧或玻化边缘 3% 精确选择互斥区域，再生成共享软混合权重。
6. BaseColor 使用 `#61503A`、`#263234`、`#526563`、`#4D8F92` 色锚；细晶脉只使用低比例混色，不制作霓虹、光晕或烘焙自发光。
7. Height 由五类共享区域权重、独立周期宏观场、独立周期高频场、盆地边界和局部材料起伏共同构建，不是 BaseColor 灰度复制；所有表现保持贴地，不形成晶簇或玩法障碍。
8. Normal 只从最终 16-bit Height 的周期中心差分派生。
9. URP Mask 独立制作：R Metallic 恒为非金属，G Ambient Occlusion 表达低洼玻化边与细缝，B Detail Mask 控制材料细节响应，A Smoothness 使玻化区达到 `0.55–0.78`、荒土保持低值。
10. 分层 `.kra` 保存最终 BaseColor 和五个命名覆盖遮罩；`.blend` 保存生成脚本、固定种子、固定相机、灯光、四张打包贴图和 PBR 检查平面/球体。

二值构成遮罩按 2048×2048 像素精确分配：

| 构成 | 像素数 | 面积比例 |
|---|---:|---:|
| 暖黄荒土 | 2,726,298 | 65.000010% |
| 深色玻化地表 | 754,975 | 18.000007% |
| 灰青晶壳 | 419,430 | 9.999990% |
| 细晶脉 | 167,772 | 3.999996% |
| 灼烧或玻化边缘 | 125,829 | 2.999997% |

一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。Blender 文件中的平面和球体只用于离线材质检查，不是正式模型。`ResourceNodes/EnergyCrystal` 可采集能晶节点是后续独立模型任务，具有独立稳定 ID、独立高亮和独立玩法数据；开始该建模任务前必须先通知用户。
