# 湿地 `Wetland` 首版材质来源说明

- 资产：`Wetland` 湿地首版正式材质
- 修订日期：2026-08-09
- 固定程序随机种子：`813118`
- 已批准概念：`Wetland_Approved_AI_Concept_v002.png`
- 概念 SHA-256：`15112788e3a33c3b7097dee91da8884a95794c1ffef9db0557526b2e3c4fcbc7`
- 玩法真值：无；颜色、遮罩、浅水表现和 Height 只表达视觉材质
- 模型交付：无；当前只制作地表贴图，不创建浅水网格、FBX、Prefab 或碰撞体

## 来源与批准记录

Codex 内置图像生成工具以项目中已批准的 `Wasteland_Approved_AI_Concept_v002.png` 和 `Rocky_Approved_AI_Concept_v001.png` 作为风格、色系、表面尺度和完成度参考，生成新的 `Wetland` 概念。第一稿因为碎块密度过高、容易误读成被水淹的岩石地，由制作方主动淘汰；第二稿扩大平滑湿泥区域、减少约六成碎块噪声并加入少量贴地枯根。

用户查看第二稿后回复“可以”，批准 `Wetland_Approved_AI_Concept_v002.png` 进入正式 PBR 重建。未使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。生成概念的商业使用依据为项目用户委托并批准用于 WasteCity 私有项目；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。

首版正式重建预览因湿泥仍像碎裂硬地、赭黄土与污水分布过碎、4×4 重复图案明显，以及 PBR 高光偏冷发白而被用户否决。重制时改用更连贯的低频区域分数，扩大湿泥、浅污水和矿物污泥的连续形状，保留更多已批准概念中的局部泥面细节，同时降低湿泥 Smoothness 并改为暖色、克制的预览灯光。用户查看重制后的 4×4 平铺、默认倾斜正交和 PBR 检查图后回复“通过”，正式批准本版资产。

## 最终概念修订提示词

```text
Use case: stylized-concept
Asset type: revised WasteCity Wetland terrain material concept preview
Input images: Image 1 is the Wetland draft to revise; Images 2 and 3 are approved Wasteland and Rocky style references. Preserve Image 1's top-down framing, dark olive-brown palette, shallow-puddle distribution, and overall WasteCity art direction. Use Images 2 and 3 only to preserve project style and texel scale.
Primary request: revise Image 1 so it reads unmistakably as broad pressed wet mud rather than flooded rocky ground.
Targeted changes only: reduce the dense angular chip/pebble microtexture by about 60%; merge it into larger smoother wet-mud plates and shallow compressed ruts. Keep approximately 20% shallow dirty-water puddles, 15% warm dry ochre soil islands, 10% near-black mineral sludge, 5% warm transition earth. Add a few subtle embedded dead-root or flattened straw traces totaling about 5%, lying flush with the mud and never forming upright vegetation. Maintain broad and medium shapes across roughly 70% of the surface.
Lighting/material: flat neutral material-preview light, restrained soft ambient occlusion, subtle wet sheen only on mud and puddles; no directional shadow or hard specular hotspot.
Constraints: perfectly top-down square ground surface, visually plausible for future tiling, no focal object, no deep water, no lake, no cliff, no resource node, no gameplay marker, no grid, no lush greenery, no reeds, no large stones, no ruins, no roads, no characters, no UI, no text, no watermark.
Avoid: excessive micro-noise, cobblestone appearance, rocky mosaic appearance, bright blue water, mirror reflections, dramatic cinematic lighting, large empty black patches, obvious repeating motifs.
```

## 正式重建方法

1. 将批准概念裁为正方形并重采样到 2048×2048。
2. 分离宽域亮度变化，保留湿泥、浅污水、干土岛、矿物污泥和枯根材料信息，不把概念照明或高光烘焙进 BaseColor。
3. 使用 periodic-plus-smooth 频域分解得到周期 BaseColor；所有后续噪声、模糊和中心差分均采用周期边界。
4. 按湿泥 45%、浅污水 20%、干土岛 15%、矿物污泥 10%、枯根 5%、过渡土 5% 精确选择互斥区域；用连贯低频区域分数抑制碎裂和显眼重复，再周期柔化并重建为共享混合权重。
5. BaseColor 使用 `#3C3829`、`#454934`、`#28302D`、`#69533A` 等规格色锚，并保留暖黄尘土成分以支持与普通荒地柔和混合。
6. Height 由共享区域权重、独立周期宏观场、独立周期高频场、独立盆地场和带通材料结构共同构建，不是 BaseColor 灰度复制；浅水只作为较低、较平的薄层视觉区域。
7. Normal 只从最终 16-bit Height 的周期中心差分派生。
8. URP Mask 独立制作：R Metallic 恒为非金属，G Ambient Occlusion 表达泥沟和枯根凹部，B Detail Mask 表达细节响应，A Smoothness 区分干土、湿泥和浅污水。
9. 分层 `.kra` 保存最终 BaseColor 和六个命名覆盖遮罩；`.blend` 保存生成脚本、固定种子、固定相机、暖色克制灯光、四张打包贴图和 PBR 检查平面/球体。

二值构成遮罩按 2048×2048 像素精确分配：

| 构成 | 像素数 | 面积比例 |
|---|---:|---:|
| 深色湿泥 | 1,887,437 | 45.000005% |
| 浅污水洼 | 838,861 | 20.000005% |
| 干土小岛 | 629,146 | 15.000010% |
| 黑色矿物污泥 | 419,430 | 9.999990% |
| 根系或枯草痕迹 | 209,715 | 4.999995% |
| 向荒地过渡的泥土 | 209,715 | 4.999995% |

一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。Blender 文件中的平面和球体只用于离线材质检查，不是正式模型；浅水网格属于后续独立模型/运行时任务，开始前必须先通知用户。
