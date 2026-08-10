# 深水 `DeepWater` 首版材质来源说明

- 资产：`DeepWater` 深水首版正式材质
- 制作与批准日期：2026-08-09
- 固定程序随机种子：`813246`
- 已批准概念：`DeepWater_Approved_AI_Concept_v001.png`
- 玩法真值：无；颜色、涟漪、油膜、沉积、Height 和 PBR 响应只表达视觉材质
- 模型交付：无；本次不创建水面 FBX、Prefab、Collider、Shader Graph 或 Unity 运行时材质

## 来源、生成方法与批准记录

概念图由 Codex 内置图像生成工具创建，并使用项目内已经批准的 `Wasteland_Approved_AI_Concept_v002.png`、`Wetland_Approved_AI_Concept_v002.png` 与 `QA_Ruins_ModuleKit_WastelandContext.png` 作为风格、综合色温、纹理尺度和废土世界一致性参考。没有使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理。

最终概念提示词的核心要求如下：

```text
Create one square WasteCity DeepWater terrain concept in the approved stylized-PBR wasteland style. Show approximately 70% opaque near-black blue-green contaminated deep water and 30% warm ochre dry bank, viewed from a near-top-down tilted orthographic game camera. Preserve the approved Wasteland granular scale and warm dusty identity. Use broad slow ripples, faint sediment below the surface, tiny restrained oily traces, a narrow wet transition band and a clearly readable non-traversable shoreline. Avoid bright blue tropical water, white surf, clean lake water, lush vegetation, fantasy glow, resource nodes, gameplay markers, text, logos and watermarks.
```

用户查看概念图后回复“通过”，批准 `DeepWater_Approved_AI_Concept_v001.png` 进入正式 PBR 重建。首轮正式重建虽然满足文件格式和比例，但把涟漪、油膜与沉积错误表现成模糊色块；用户明确评价“这个一点都不像水啊”。该版本被制作方判定为不合格，未提交、未登记为完成。重制时删除孤立色块式构成，把四种比例改为共享连续水体上的覆盖遮罩，以蓝黑不透明水面、连续缓波、真实高 Smoothness 和克制污染为主体。用户查看重制后的默认倾斜正交与 PBR 检查图后回复“可以”，批准当前版本。

概念及生成资产用于 WasteCity 私有项目的商业制作；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Python、NumPy、Pillow 和 OpenRaster 仅作为制作工具与无损源格式。

## 正式重建方法

1. 从批准概念的深水区域提取低饱和蓝黑、炭青与微量暖褐色彩，不把岸线、光照或场景阴影直接烘入正式地表。
2. 使用 periodic-plus-smooth 频域分解及全周期噪声，保证 BaseColor、Height、Normal 和 Mask 四边连续。
3. 按 `75% / 15% / 7% / 3%` 精确建立蓝黑水体、缓慢涟漪、油膜污染和锈橙沉积四个互斥二值覆盖遮罩，再周期柔化为共享混合权重。
4. BaseColor 以统一不透明水体为母底；涟漪、油膜和沉积只进行局部低强度调制，不形成漂浮石块或独立地面色块。
5. Height 使用独立的多向周期波场、宏观起伏和细节场构建，不是 BaseColor 灰度复制；数值只服务视觉法线，不保存玩法高度。
6. Tangent Space Normal 仅从最终 16-bit Height 的周期中心差分派生。
7. URP Mask 独立生成：R Metallic 恒为 0，G Ambient Occlusion 保持水面高值，B Detail Mask 表达涟漪与污染细节，A Smoothness 限定在 `0.78–0.94`。
8. `DeepWater_Golden_Master.ora` 是无损分层 OpenRaster 源，包含最终 BaseColor 和四个命名覆盖遮罩；它满足“`.kra`、`.psd`、`.sbs` 或等价无损分层源”的合同。
9. Blender 5.2.0 LTS 文件保存固定相机、三点灯光、PBR 平面/球体、生成脚本、四张打包贴图和 `gameplay_truth=none` 元数据；预览平面与球体仅用于离线材质检查，不是正式模型交付。

## 视觉与技术目标

| 成分 | 精确面积占比 | 表现 |
|---|---:|---|
| 蓝黑不透明水体 | 75% | `#101F29`、`#09131B` 一带的危险深水母底 |
| 宽而缓慢的涟漪 | 15% | 连续、低频、多方向水面波纹 |
| 油膜污染 | 7% | 极低饱和、低对比的局部色差与更高 Smoothness |
| 锈橙污染沉积 | 3% | 水下克制暖褐沉积，不形成资源节点或玩法标记 |

- 正式贴图：2048×2048；一套约覆盖 `4×4` 外城逻辑格，约 512 px/格。
- BaseColor：RGB 8-bit PNG / sRGB。
- Normal：RGB 8-bit Tangent Space PNG / Linear。
- Mask：RGBA 8-bit PNG / Linear，R Metallic / G AO / B Detail / A Smoothness。
- Height：Gray 16-bit PNG / Linear。
- 视觉边界：DeepWater 本体保持明显低明度和高 Smoothness；岸线属于后续独立过渡表现，不能模糊不可通行的逻辑边界。
- Unity 接入：未进行；正式场景、玩法代码、地形枚举、通行规则、稳定 ID、资源节点、Packages 和 ProjectSettings 均不修改。
