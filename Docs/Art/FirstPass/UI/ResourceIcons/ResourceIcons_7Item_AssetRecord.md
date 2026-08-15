# 七项常见资源图标与通用回退资产生产记录

## 状态与范围

- 关联需求：`IDEA-0011`；视觉方向同时参考 `IDEA-0004` 已批准的第一版废土工业美术语言。
- 当前状态：生产规范已建立；ImageGen 源图、透明母版、Unity Sprite、哈希和运行时接入均待生成或验证。
- 人工状态：尚未提交用户视觉确认，不得描述为已批准、已验收或正式美术。
- 本记录覆盖七项常见资源图标和一个缺图回退图标；不新增资源 ID，不改变资源数量、节点储量、生产配方、库存容量或放置兼容规则。
- 本轮只为正式 3D 背包、建筑缓存和资源提示提供可替换占位表现，不接线冻结 2D。

## 稳定映射

| 序号 | 资源 | 既有稳定 ID | 计划运行时文件 | 主要轮廓 |
|---:|---|---|---|---|
| 01 | 铁矿 | `core.resource.iron` | `I_Resource_Iron.png` | 带锈红矿脉的深褐黑矿块 |
| 02 | 合金 | `technology.resource.alloy` | `I_Resource_Alloy.png` | 枪灰色精炼金属锭组 |
| 03 | 弹药 | `technology.resource.ammunition` | `I_Resource_Ammunition.png` | 黄铜弹带与紧凑弹药盒 |
| 04 | 石材 | `core.resource.stone` | `I_Resource_Stone.png` | 灰褐色棱角石料堆 |
| 05 | 生物质 | `core.resource.biomass` | `I_Resource_Biomass.png` | 污绿色有机团块与旧金属容器 |
| 06 | 能晶 | `core.resource.energy-crystal` | `I_Resource_EnergyCrystal.png` | 铜锈夹具固定的灰蓝晶簇 |
| 07 | 水 | `core.resource.water` | `I_Resource_Water.png` | 磨损金属水罐与克制蓝色液体识别面 |
| — | Unknown 回退 | 仅为表现键 `ui.resource.unknown`，不是新资源 ID | `I_Resource_Unknown.png` | 无文字的封闭工业运输箱与问号形机械扣件轮廓 |

运行时代码必须继续使用既有资源稳定 ID 和正式显示名目录。`ui.resource.unknown` 只在图标映射缺失时回退，不得进入库存、配方、存档或世界资源节点真值。

## 与现有世界节点的边界

当前正式 3D 世界已经为五类基础采集节点提供合批 primitive 占位：

| 世界节点资源 | 当前表现职责 | 本记录的图标职责 |
|---|---|---|
| 铁矿 | 世界中的可采集节点占位 | 背包、缓存和资源提示中的二维识别 |
| 能晶 | 世界中的可采集节点占位 | 背包、缓存和资源提示中的二维识别 |
| 石材 | 世界中的可采集节点占位 | 背包、缓存和资源提示中的二维识别 |
| 生物质 | 世界中的可采集节点占位 | 背包、缓存和资源提示中的二维识别 |
| 水 | 世界中的可采集节点占位 | 背包、缓存和资源提示中的二维识别 |

- 合金和弹药是加工产物，本轮不创建世界采集节点。
- 图标不得保存节点坐标、剩余储量、枯竭状态或兼容性。
- 世界节点仍消费 `WorldMapModel` 和既有资源节点规则；图标只是 presenter 资源。
- 铁矿与能晶可参考已批准的 `ArtSource/FirstPass/Environment/ResourceNodes/References/` 概念，但不得把其中的白底参考 PNG 直接冒充 Unity Sprite。
- 世界节点的耗尽刷新、模型、材质、Collider 和批次重建不属于本图标资产文件的所有权。

## 视觉规范

- 风格：低饱和工业废土、深色氧化钢、旧铜、黄铜、暖黄积尘和受控锈蚀。
- 功能色：铁矿使用锈红；合金使用枪灰和少量冷白高光；弹药使用克制黄铜；石材使用灰褐；生物质使用污绿；能晶使用灰蓝或蓝绿；水使用低饱和深蓝。
- 每个图标依靠轮廓、材质和局部功能色共同区分，不能只靠颜色。
- 单个图标只保留一个连接主体，不出现人物、建筑、场景地面、漂浮碎片或独立散件。
- 禁止文字、数字、商标、品牌标志、霓虹背景、全息网格、魔法光环和过强辉光。
- 控制随机锈点、细碎划痕和颗粒噪声，保证 `64×64` 屏幕显示下仍可辨认。
- 图标不内置数量、容量、锁定或停工状态；这些信息由 UGUI 文本和状态层显示。

## ImageGen 生产计划

- 生成工具：Codex 内置 OpenAI ImageGen；具体内部模型标识不对项目暴露。
- 调用状态：待执行。当前仓库没有本轮 ImageGen 输出。
- 计划源图目录：`ArtSource/FirstPass/UI/ResourceIcons/References/Generated/`。
- 计划源文件：八次独立 ImageGen 调用分别生成 `Iron`、`Alloy`、`Ammunition`、`Stone`、`Biomass`、`EnergyCrystal`、`Water` 和 `Unknown` 源图；不同资源不得用一张多格图板裁切代替独立生成。
- 计划透明母版目录：`ArtSource/FirstPass/UI/ResourceIcons/Masters/`。
- 计划 QA 总览：`Docs/Art/FirstPass/UI/ResourceIcons/QA_ResourceIcons_7Item_AndUnknown_v001.png`。
- 计划调用方式：由当前开发任务直接调用内置 ImageGen；不使用外部库存图片，不抓取网络素材，不把用户或第三方作品像素混入源图。

共享提示词骨架（每项必须把 `<subject>` 替换为该资源的唯一主体，并独立调用）：

> Use case: stylized-concept. Asset type: one inventory resource icon for a low-saturation industrial wasteland strategy game. Primary request: create one isolated `<subject>`. Clear distinct silhouette, oxidized gunmetal, old copper and brass, warm dusty amber accents, resource-specific restrained functional color, no text, no numbers, no logos, no people, no buildings, no scenery, no holograms, no loose disconnected pieces. Center one connected object, orthographic icon view, consistent neutral lighting and generous padding, readable at 64 pixels. Create the subject on a perfectly flat solid `#00ff00` chroma-key background for background removal. The background must have no shadows, gradients, texture, reflections, floor plane or lighting variation. Do not use `#00ff00` in the subject. No cast shadow, contact shadow, reflection or watermark.

八项输出必须逐张检查主体、轮廓、统一比例和色键纯度。复制源图进入项目后，使用 imagegen 技能安装的 `remove_chroma_key.py` 进行确定性去背；验证 alpha、透明四角、主体覆盖和绿色 fringe。只有最终八张透明母版可以在本地组合 QA 总览，不能把总览反向作为运行时切图源。

## 尺寸、透明处理与 Unity 目标

### 源图与母版

- ImageGen 原始尺寸由工具实际结果记录，不预先伪造。
- 每项透明母版目标为 `1024×1024` RGBA 8-bit PNG，主体等比居中并保留一致安全边距。
- 去背只处理与画布边界连通的背景区域；不得删除资源主体内部的冷白高光、水面亮部或浅色金属。
- 透明边缘使用 straight alpha，清除白色 fringe，不预乘颜色，不保留阴影矩形或源板格线。

### Unity Sprite

- 目标目录：`Assets/_Game/Art/FirstPass/UI/ResourceIcons/`。
- 每项运行时文件目标为 `256×256` RGBA 8-bit PNG。
- 计划 importer：`Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、sRGB 开启、Alpha Is Transparency 开启、Mip Map 关闭、Read/Write 关闭、最大尺寸 `256`。
- 图标映射应由稳定资源 ID 驱动；未知或尚无专属图标的 ID 回退到 `I_Resource_Unknown.png`，不得返回空 Sprite 导致界面缺口。
- PNG 受仓库 `.gitattributes` 的 Git LFS 规则管理；生成后必须确认本地为真实内容而非未物化 pointer。

## 计划验证

以下结果目前全部待执行，不得提前填为通过：

1. 八张母版和八张 Unity Sprite 的文件存在性、数量与固定映射；
2. 母版 `1024×1024`、运行时 `256×256`、RGBA 8-bit 与有效 alpha；
3. 四角透明、主体非空、无白边、无源板格线、无文字或标志；
4. `64×64` 和 `32×32` 缩放总览中的轮廓可读性；
5. Unity importer 的 Sprite、sRGB、alpha、mipmap、Read/Write 和最大尺寸合同；
6. 七个既有资源 ID 命中专属图标，任意未知 ID 稳定回退 Unknown；
7. 图标替换不改变库存、生产、资源节点、物流、存档 schema 或冻结 2D；
8. `git lfs fsck --pointers` 与目标 PNG 的 SHA-256；
9. 正式 3D 背包和建筑缓存中的运行时截图与真实输入检查；
10. 用户对图标识别、综合色温、噪声、尺寸和风格一致性的人工视觉确认。

## 资产清单与哈希

| 产物 | 状态 | SHA-256 |
|---|---|---|
| 8 张 ImageGen 独立色键源图 | 待生成 | 待生成后逐项记录 |
| 8 张透明母版 | 待生成 | 待生成后逐项记录 |
| 8 张 Unity Sprite | 待生成 | 待生成后逐项记录 |
| QA 总览 | 待生成 | 待生成后记录 |

## 来源与许可

- 计划外部库存素材：无。
- 计划生成来源：Codex 内置 OpenAI ImageGen；确定性透明处理工具和版本待实际执行后记录。
- 权利状态：生成后按项目委托生成内容记录；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
- 任何哈希、工具版本、机器验证、运行时接入和用户视觉结论都必须在真实发生后回写，当前不作完成声明。
