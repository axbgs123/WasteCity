# 七项常见资源图标与通用回退资产生产记录

## 状态与范围

- 关联需求：`IDEA-0011`；视觉方向同时参考 `IDEA-0004` 已批准的第一版废土工业美术语言。
- 当前状态：八次 ImageGen 已完成，色键源图、透明母版和 Unity Sprite 已生成；Importer 与稳定映射自动化为 `8/8`，正式 3D 背包运行时接入仍在开发。
- 人工状态：尚未提交用户视觉确认，不得描述为已批准、已验收或正式美术。
- 本记录覆盖七项常见资源图标和一个缺图回退图标；不新增资源 ID，不改变资源数量、节点储量、生产配方、库存容量或放置兼容规则。
- 本轮只为正式 3D 背包、建筑缓存和资源提示提供可替换占位表现，不接线冻结 2D。

## 稳定映射

| 序号 | 资源 | 既有稳定 ID | 计划运行时文件 | 主要轮廓 |
|---:|---|---|---|---|
| 01 | 铁矿 | `core.resource.iron` | `ResourceIcon_Iron.png` | 带锈红矿脉的深褐黑矿块 |
| 02 | 合金 | `technology.resource.alloy` | `ResourceIcon_Alloy.png` | 枪灰色精炼金属锭组 |
| 03 | 弹药 | `technology.resource.ammunition` | `ResourceIcon_Ammunition.png` | 黄铜弹药托盘 |
| 04 | 石材 | `core.resource.stone` | `ResourceIcon_Stone.png` | 灰褐色棱角石料堆 |
| 05 | 生物质 | `core.resource.biomass` | `ResourceIcon_Biomass.png` | 铜线捆扎的废土纤维与菌体 |
| 06 | 能晶 | `core.resource.energy-crystal` | `ResourceIcon_EnergyCrystal.png` | 旧铜夹具固定的蓝绿晶簇 |
| 07 | 水 | `core.resource.water` | `ResourceIcon_Water.png` | 磨损金属水罐与蓝色液位窗 |
| — | Unknown 回退 | 仅为表现键 `ui.resource.unknown`，不是新资源 ID | `ResourceIcon_Unknown.png` | 正面空白菱形铭牌的封闭工业运输箱 |

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
- 调用状态：已于 `2026-08-15` 执行八次独立调用；工具为 Codex 内置 OpenAI ImageGen，内部模型标识不对项目暴露。
- 计划源图目录：`ArtSource/FirstPass/UI/ResourceIcons/References/Generated/`。
- 实际源文件：八次独立 ImageGen 调用分别生成 `ResourceIcon_{Iron|Alloy|Ammunition|Stone|Biomass|EnergyCrystal|Water|Unknown}_Chroma_1254.png`；工具实际输出为 `1254×1254`，没有伪写成提示词请求的 `1024×1024`，也没有使用多格图板裁切。
- 计划透明母版目录：`ArtSource/FirstPass/UI/ResourceIcons/Masters/`。
- 计划 QA 总览：`Docs/Art/FirstPass/UI/ResourceIcons/QA_ResourceIcons_7Item_AndUnknown_v001.png`。
- 计划调用方式：由当前开发任务直接调用内置 ImageGen；不使用外部库存图片，不抓取网络素材，不把用户或第三方作品像素混入源图。

共享提示词骨架（每项必须把 `<subject>` 替换为该资源的唯一主体，并独立调用）：

> Use case: stylized-concept. Asset type: one inventory resource icon for a low-saturation industrial wasteland strategy game. Primary request: create one isolated `<subject>`. Clear distinct silhouette, oxidized gunmetal, old copper and brass, warm dusty amber accents, resource-specific restrained functional color, no text, no numbers, no logos, no people, no buildings, no scenery, no holograms, no loose disconnected pieces. Center one connected object, orthographic icon view, consistent neutral lighting and generous padding, readable at 64 pixels. Create the subject on a perfectly flat solid `#00ff00` chroma-key background for background removal. The background must have no shadows, gradients, texture, reflections, floor plane or lighting variation. Do not use `#00ff00` in the subject. No cast shadow, contact shadow, reflection or watermark.

实际八次调用均使用英文完整提示词。共同约束为：`one centered game inventory icon for WasteCity`、正交至轻微 3/4 俯视、64 像素可读、低饱和枪灰/锈色/旧铜废土工业配色、克制手绘 3D、无边框/文字/数字/Logo/水印/多余物体，以及完全平坦 `#00FF00` 背景；每次另加以下唯一主体句：

1. Iron：`raw IRON ORE, a compact cluster of angular dark hematite rocks with rusty red metallic fracture faces and worn industrial dust`。
2. Alloy：`refined ALLOY, three compact stacked industrial ingots with beveled edges, gunmetal steel, old-brass highlights, heat discoloration and workshop wear`。
3. Ammunition：`AMMUNITION, rugged large-caliber cartridges in a small open dark-metal ammo tray, clearly inert inventory supplies, brass casings and dark gunmetal tips`；另加 `No weapons`。
4. Stone：`quarried STONE, rough slate-gray masonry chunks and one squared broken block, dusty chipped edges with warm rust soil stains`。
5. Biomass：`BIOMASS, hardy post-apocalyptic fibrous plant matter and fungal pods tied with worn copper wire, organic industrial feedstock, not food`。
6. EnergyCrystal：`ENERGY CRYSTAL, translucent cyan-teal mineral shards held in a battered gunmetal containment collar with old-brass clamps and restrained internal glow`。
7. Water：`clean WATER resource, a battered industrial canteen/jerrycan with an old-brass cap and a clear cyan water gauge, dusty but sanitary`。
8. Unknown：`UNKNOWN RESOURCE, a sealed battered industrial supply crate with a completely blank raised diamond metal plate, rusted corners and old-brass latches`；另加 `no question mark, no symbol`。

调用返回的原始文件保留在 Codex 生成缓存之外的本记录源图目录；仓库未保存或宣称不存在的内部模型标识、seed 或采样参数。

八项输出必须逐张检查主体、轮廓、统一比例和色键纯度。复制源图进入项目后，使用 imagegen 技能安装的 `remove_chroma_key.py` 进行确定性去背；验证 alpha、透明四角、主体覆盖和绿色 fringe。只有最终八张透明母版可以在本地组合 QA 总览，不能把总览反向作为运行时切图源。

## 尺寸、透明处理与 Unity 目标

### 源图与母版

- ImageGen 原始尺寸由工具实际结果记录，不预先伪造。
- 每项透明母版实际为 `1254×1254` RGBA 8-bit PNG，与 ImageGen 实际输出等尺寸，主体等比居中并保留安全边距。
- 去背只处理与画布边界连通的背景区域；不得删除资源主体内部的冷白高光、水面亮部或浅色金属。
- 透明边缘使用 straight alpha，清除白色 fringe，不预乘颜色，不保留阴影矩形或源板格线。

### Unity Sprite

- 目标目录：`Assets/_Game/Art/FirstPass/UI/ResourceIcons/`。
- 每项运行时文件目标为 `256×256` RGBA 8-bit PNG。
- 计划 importer：`Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、sRGB 开启、Alpha Is Transparency 开启、Mip Map 关闭、Read/Write 关闭、最大尺寸 `256`。
- 图标映射应由稳定资源 ID 驱动；未知或尚无专属图标的 ID 回退到 `I_Resource_Unknown.png`，不得返回空 Sprite 导致界面缺口。
- PNG 受仓库 `.gitattributes` 的 Git LFS 规则管理；生成后必须确认本地为真实内容而非未物化 pointer。

## 实际处理与验证

- 去背工具：`C:/Users/czc1/.codex/skills/.system/imagegen/scripts/remove_chroma_key.py`；参数为 `--auto-key corners --soft-matte --transparent-threshold 12 --opaque-threshold 90 --edge-contract 1 --edge-feather 0.6 --despill`。
- 缩放工具：Pillow `12.2.0`，RGBA + Lanczos，输出 `256×256`；未覆盖 ImageGen 原始源图。
- 已验证：8 张 RGB 色键源图、8 张 `1254×1254` RGBA 母版、8 张 `256×256` RGBA Sprite 均存在；运行时 alpha 范围均为 `0–255`，主体 alpha bbox 非空且未触边。
- 已验证：Unity 聚焦 EditMode `GrayboxResourceIconAssets3DTests` 为 `8/8`；覆盖 Sprite/Single、sRGB、Alpha Is Transparency、mipmap 关闭、Read/Write 关闭、最大尺寸 256、七个稳定 ID 与 Unknown 回退。
- 已由开发者逐张检查透明小图：无文字、品牌、水印或源板格线；水罐液位窗含一个无文字水滴识别符号，保留为待用户视觉复验项。
- 待完成：正式 3D 背包/缓存运行时截图、`git lfs fsck --pointers`、32/64 像素场景内观感，以及用户对识别度、综合色温、噪声和一致性的人工确认。

## 资产清单与哈希

以下均为 SHA-256，文件名中的 `1254` 是工具实际输出尺寸：

```text
2b4af2387e09f83c6165ce540a3118d7de7d912e738f62cbb23098284a599c0d  References/Generated/ResourceIcon_Iron_Chroma_1254.png
7ea44c5a6638e3da89664c9f8cda594d2f313c2ad1270090af5765924bf703ce  References/Generated/ResourceIcon_Alloy_Chroma_1254.png
438d7ffd6a44c36eeb54be54787b383b12cbb4c1eb78ff08eaef29ec22372005  References/Generated/ResourceIcon_Ammunition_Chroma_1254.png
3edd8a3f9831bef9eeb4ae1245df527fe21ee5ab902946b81915f43380f722ee  References/Generated/ResourceIcon_Stone_Chroma_1254.png
b773f440d4ffe1d5ead677f77629c0e1952cdfab95a9341a4c2e22439479a881  References/Generated/ResourceIcon_Biomass_Chroma_1254.png
e0cc90bf8f8ebea0ce923bc64c158f042dd8eb84fcb84176cda45c3506dfde09  References/Generated/ResourceIcon_EnergyCrystal_Chroma_1254.png
47dd17ef0598ba0068a4eedb1d8b8f3235e0c845832b7cf6f6d396e1a7ef403d  References/Generated/ResourceIcon_Water_Chroma_1254.png
4bb0c22136ba0c748dfd5d6a722dce96609e057fadbeb0a836d09adc116fbaf2  References/Generated/ResourceIcon_Unknown_Chroma_1254.png
5162fbd072774ee213a6411c739ac9f4e98375d13492541a2926201edba010bc  Masters/ResourceIcon_Iron_Master_1254.png
5b576e3a5a8c2f25ffa120d7140b8d420bba0418d8b7bc5743e0744ed928b2b2  Masters/ResourceIcon_Alloy_Master_1254.png
b96acd05bfb3d24e68dc81c2e56609e748f3a267cafa1c030c3cc2df236fc2b0  Masters/ResourceIcon_Ammunition_Master_1254.png
c440c6e6b3729bf09e0a97f4ad342e8cbafeeb0f07524fe8738a2a3e14ecd09e  Masters/ResourceIcon_Stone_Master_1254.png
819e08d831fc83f6cf3c1b9bee5b33db15154b6ca1e3d94b28bc8774bf4cbcd9  Masters/ResourceIcon_Biomass_Master_1254.png
93466c29797c0b8c63f3afda4cb55a38f72620f6779c4f15d85d469404ab9311  Masters/ResourceIcon_EnergyCrystal_Master_1254.png
a8e23ed80cb8b12ef8c9d873c1d2871237dd0ae99e8901721929e6f8d23336c8  Masters/ResourceIcon_Water_Master_1254.png
c856fcd70eaf49dbc9b9180d224e3664e885d4970825ac64bc2701e4b0664ef4  Masters/ResourceIcon_Unknown_Master_1254.png
bab02fa50521705d6bb53dba47e463ffb2b0a7d3dec7590fd43189eaa1a6e170  Unity/ResourceIcon_Iron.png
445ee4bfdc8bb3ef51644369f9bc4a26df666d30590642b67f47e024754bfe21  Unity/ResourceIcon_Alloy.png
73707e3a8c952dbf5266a66bd5c8411e37d0db2566c0ceede84f33942fd56011  Unity/ResourceIcon_Ammunition.png
32a32a19b7af6ca27590c846bb7eef342ee4c75a00727032025607ef41766e7d  Unity/ResourceIcon_Stone.png
b85caab5ad2ac955eae99db8df15323755aafeddc3f6c25bffa43264a1fb307b  Unity/ResourceIcon_Biomass.png
e7431af14feda8dfb1f4cbdddcce89d7da16d648a680ab77c6813acd0a5d89b3  Unity/ResourceIcon_EnergyCrystal.png
44d5db96bbbbbdea2b8148438e89d4be0defc53c22068d92946c91f260b5611b  Unity/ResourceIcon_Water.png
1496685a8ddc1c649f1386e856e2f16360d4a8708b7fb008ed4a6fe5d20413e5  Unity/ResourceIcon_Unknown.png
```

## 来源与许可

- 外部库存素材：无；未抓取网络素材，未混入用户或第三方作品像素。
- 生成来源：Codex 内置 OpenAI ImageGen，八次独立调用，日期 `2026-08-15`；透明处理工具与实际参数见上文。
- 权利状态：生成后按项目委托生成内容记录；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
- 运行时背包接入、独立构建和用户视觉结论仍必须在真实发生后回写；当前仅声明源图、母版、Sprite 与聚焦导入测试已完成。
