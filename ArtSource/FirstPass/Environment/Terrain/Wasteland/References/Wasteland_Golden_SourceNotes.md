# 普通荒地黄金样板来源说明

- 资产：`Wasteland` 普通荒地黄金样板 v2
- 修订日期：2026-08-08
- 固定程序随机种子：`812804`
- 已批准概念：`Wasteland_Approved_AI_Concept_v002.png`
- 概念 SHA-256：`db9bbc2d6f0d81eb095018fe768c6c29e5505e82d425aa1db0e39261c92b4e5d`
- 玩法真值：无；纹理、遮罩和 Height 只表达视觉材质

## 来源与批准记录

用户提供了一张仅用于风格参考的截图，要求匹配其中暖褐硬土、嵌入式碎石、分层风化和风格化 PBR 视觉。截图没有作为正式像素输入保存到仓库，也没有直接复制其像素。

Codex 内置图像生成工具随后生成单张普通荒地概念图。用户于 2026-08-08 明确回复“可以”，批准该图作为正式重建的视觉与 BaseColor 输入。模型的内部标识未由内置工具暴露；生成文件、提示词、批准事实和后续加工方法均在本记录中保留。未使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。

商业使用依据为项目用户委托生成并明确批准用于 WasteCity 私有项目；生成输出的具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。

## 概念生成提示词

```text
Create one single square ordinary dry wasteland ground texture concept, closest to the upper-left terrain panel in the user-provided style reference. Match its rich sculpted relief, layered compacted earth, irregular gravel, warm brown-ochre palette and restrained cinematic material depth. Use broad compacted earth plates and dusty soil, sparse clusters of angular pebbles and embedded stones, occasional shallow fractured plates and erosion cracks, and subtle muted rust abrasion. Keep large quiet areas for buildings and units. Premium stylized PBR terrain material with a hand-sculpted and hand-painted look, strong readable forms without photoreal photographic noise. Output only terrain surface: no UI, grid, numbers, text, water, crystals, cliff, vegetation, buildings, resource nodes, vehicles or obvious road.
```

## 正式重建方法

1. 将已批准概念裁为正方形并重采样到 2048×2048。
2. 分离大尺度亮度变化，压低概念图中的宽域照明，只保留材料颜色和局部石块/硬土信息。
3. 使用 periodic-plus-smooth 频域分解把 BaseColor 转为周期分量；修正后的跨边界变化低于纹理内部相邻变化，不以镜像拼接制造硬缝。
4. 从周期 BaseColor 的低、中、高频材料结构计算工业痕迹、浅裂纹、砾石和浮尘候选分数，再按 65/18/10/4/3 精确选择互斥区域并周期柔化。
5. BaseColor 在批准概念的颜色与结构上，按五类区域进行受控暖黄、深土和锈色校正；不把 Blender 预览灯光烘焙回贴图。
6. Height 不是 BaseColor 灰度复制：它使用带通材料结构、独立周期宏观场、独立周期高频场和五类区域的物理高度响应重新构建，并保留 0.22–0.82 的混合空间。
7. Normal 只从最终 16-bit Height 的周期中心差分派生。
8. URP Mask 四通道分别计算：R Metallic、G Ambient Occlusion、B Detail Mask、A Smoothness；没有复制同一灰度图充当四个通道。
9. 分层 `.kra` 保存最终 BaseColor 与五个命名覆盖遮罩；`.blend` 保存可重复脚本、固定相机、灯光、打包贴图和 PBR 检查平面/球体。

二值构成遮罩按 2048×2048 像素精确分配：

| 构成 | 像素数 | 面积比例 |
|---|---:|---:|
| 压实赭黄土 | 2,726,298 | 65.0000095% |
| 浮尘与风积层 | 754,975 | 18.0000067% |
| 小砾石 | 419,430 | 9.9999905% |
| 浅裂纹 | 167,772 | 3.9999962% |
| 锈色磨损和工业痕迹 | 125,829 | 2.9999971% |

一套 2048 纹理按约 4×4 个外城逻辑格阅读，即约 512 px/格。Blender 文件中的平面和球体只用于离线材质检查，不是正式模型，也不产生 FBX 交付物。
