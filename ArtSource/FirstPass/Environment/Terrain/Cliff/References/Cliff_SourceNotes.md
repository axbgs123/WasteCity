# 悬崖 `Cliff` 首版资产来源说明

- 资产：`Cliff` 首版地表材质与六件模块包
- 制作与批准日期：2026-08-09
- 材质随机种子：`813417`
- 模型随机种子：`813418`
- 已批准参考板：`Cliff_MaterialAndModules_Approved_AI_Reference_v001.png`
- 参考板 SHA-256：`e76d8d0e86b78c30475181aad0d99a58637b1d7b6dc756af76ffdf09c74be15d`
- 玩法真值：无；材质、几何高度、坡脚碎石和矿物污痕只表达视觉，不保存阻挡、通行、资源节点或稳定 ID
- Unity 接入：未进行；没有创建或修改场景、Prefab、Collider、Rigidbody、玩法代码、地形枚举或通行规则

## 来源、生成方法与批准记录

参考板由 Codex 内置图像生成工具创建，使用项目内已经批准的以下资产作为综合色温、纹理尺度、风格化 PBR 和废土世界一致性参考：

- `ArtSource/FirstPass/Environment/Terrain/Wasteland/References/Wasteland_Approved_AI_Concept_v002.png`
- `ArtSource/FirstPass/Environment/Terrain/Rocky/References/Rocky_Approved_AI_Concept_v001.png`
- `Docs/Art/FirstPass/Terrain/Ruins/QA_Ruins_ModuleKit_WastelandContext.png`

没有使用照片、扫描、材质商店、Poly Haven、Sketchfab、Hyper3D 或其他第三方纹理、网格与模型。参考板为项目内部生成概念，不含文字、标志、UI、玩法标记或第三方水印。

参考板生成要求的核心内容如下：

```text
Create one 4x2 WasteCity Cliff production reference board with exactly eight panels in the approved stylized-PBR warm wasteland style. Panel 1 is a close material study. The remaining panels show Straight A, Straight B, inner corner, outer corner, terminal/end cap, top cap/fill, and one assembled preview on the approved Wasteland ground. Use large horizontal weathered strata, dark fresh fracture interiors and deep cracks, warm yellow dry dust on upward faces, loose foot rubble that hides joins without forming ramps, and sparse subdued vertical mineral streaks. All modules share one height, thickness and modular endpoints. The wall is steep and clearly non-traversable. Use multiple visible material zones rather than a uniform brown surface. Avoid masonry blocks, clean bricks, sci-fi panels, lush vegetation, fantasy glow, gameplay markers, text, logos and watermarks.
```

用户查看该 8 格参考板后回复“通过”，批准参考板进入正式 PBR 重建和模块建模。正式候选完成后，用户查看普通荒地组合图、模块组合图与 4×4 平铺图，再次明确回复“通过”，批准当前材质和六件模块。

生成概念及重建资产用于 WasteCity 私有项目的商业制作；具体权利仍受生成时所用 OpenAI 服务条款与项目账户条款约束。Blender、Python、NumPy、Pillow 和 OpenRaster 仅作为制作工具与无损源格式。

## 正式材质重建方法

1. 从批准参考板提取暖褐岩层、暗色新鲜断面、暖黄积尘、坡脚碎石和低饱和矿痕的视觉方向；不把参考板灯光、相机阴影或模块轮廓直接烘入正式贴图。
2. 使用全周期分形噪声、周期 Voronoi 裂隙、横向地层波和独立细节场构建无缝材质。
3. 按 `55% / 20% / 15% / 7% / 3%` 精确建立风化岩层、深裂隙、暖黄积尘、坡脚碎石和矿物竖痕五类互斥区域，再周期柔化为共享权重。
4. BaseColor 只表达固有色与克制局部变化；不包含方向光、高光、环境阴影或玩法标记。
5. Height 由独立地层、裂隙、碎石和噪声场构建，不是 BaseColor 灰度复制；数值只服务视觉法线。
6. Tangent Space Normal 由最终 16-bit Height 的周期中心差分派生。
7. URP Mask 独立生成：R Metallic、G Ambient Occlusion、B Detail Mask、A Smoothness；没有从 BaseColor 伪造已完成数据通道。
8. 四张贴图共同滚动到周期场中梯度最低的合法切口 `1682,118`，降低边界处偶然出现的高对比裂隙；滚动不改变内容比例或通道对应关系。
9. `Cliff_Golden_Master.ora` 是无损 OpenRaster 分层源，包含最终 BaseColor 与五个命名区域遮罩，满足等价无损分层源合同。

## 六件模块建模方法

1. 在 Blender 5.2.0 LTS 中程序化建立七层横向岩层，控制统一约 `1.50 m` 高度，并为直段、内角、外角、端头和顶部封口设置独立占地轮廓。
2. 使用受控破边、低强度位移、顶部尘壳、顶部薄碎片和坡脚角状碎石形成自然风化；坡脚碎石只隐藏视觉接缝，不形成可读为坡道的连续表面。
3. 每件模型固定五种真实材质槽：`Strata`、`Fracture`、`Dust`、`Rubble`、`Mineral`。矿痕使用贴附岩面的不规则薄片，不使用像钢筋一样的圆杆。
4. 每件为独立 Mesh、一个 UV 集、原点 `(0,0,0)`、底面 `Z=0`；Blender 内部 Z-up，FBX 使用 `-Z Forward / Y Up / Scale 1.0`。
5. FBX 是主要 Unity 交付格式；`.blend` 保存可重复生成脚本、批准参考图、五种材质和必要贴图。
6. 未添加 Collider、Rigidbody、WasteCity MonoBehaviour、资源真值、阻挡真值、稳定 ID 或场景引用。

## 工具与版本

| 工具 | 版本 | 用途 |
|---|---|---|
| Codex 内置图像生成 | 内部模型标识未暴露 | 生成用户批准的 8 格参考板，不直接生成其余 PBR 通道或 FBX |
| Blender | 5.2.0 LTS，hash `fbe6228777e7` | 模块建模、FBX 导出、贴图打包和 EEVEE 离线 QA |
| Blender MCP | 1.2，MCP SDK 1.3.0 | 项目建模自动化接口；本资产由可重复运行脚本执行 |
| Python | 3.11.9 | 周期材质重建、文件验证和自动化 |
| NumPy | 2.2.4 | 周期噪声、区域遮罩、Height、Normal 与 Mask 计算 |
| Pillow | 12.2.0 | PNG、16-bit Height、4×4 检查图与 OpenRaster 编码 |
| OpenRaster | 0.0.1 | 六层无损分层源 |

## 视觉与技术目标

| 成分 | 精确占比 | 表现 |
|---|---:|---|
| 大型风化岩层 | 55% | 暖褐、宽而钝的横向层理，主色靠近 `#4C3B2D` |
| 深裂隙与新鲜断面 | 20% | 接近 `#292521` 的低明度断裂带 |
| 暖黄积尘 | 15% | 接近 `#796047`，集中于顶部和向上表面 |
| 坡脚碎石 | 7% | 小尺度角状落石，隐藏接缝但不形成坡道 |
| 矿物竖痕 | 3% | 稀疏、克制、贴附岩面的纵向污痕 |

- 正式贴图：2048×2048；约覆盖 `4×4` 外城逻辑格，约 512 px/格。
- BaseColor：RGB 8-bit PNG / sRGB。
- Normal：RGB 8-bit Tangent Space PNG / Linear。
- Mask：RGBA 8-bit PNG / Linear；R Metallic / G AO / B Detail / A Smoothness。
- Height：Gray 16-bit PNG / Linear。
- 模块：直段 A、直段 B、内角、外角、端头、顶部封口，共六件独立 FBX。
- 清晰边界：所有模块保持陡直高度差；视觉落石不扩大、缩小或模糊未来玩法边界。
