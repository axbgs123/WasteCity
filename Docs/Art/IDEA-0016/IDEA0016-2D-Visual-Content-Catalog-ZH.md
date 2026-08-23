# IDEA-0016 二维视觉内容目录

> 需求：`IDEA-0016`
> Unity：`2022.3.62f1`
> 目标场景：`Assets/_Game/Scenes/GrayboxPrototype3D.unity`
> 当前状态：开发中；六类基础风格已经用户确认，正式资产仍须完成自动化、场景证据和用户视觉复验。

## 1. 目录用途

本目录是 `IDEA-0016` 正式二维资产的人类可读入口。逐项稳定 ID、来源、用途、背景简介、视觉关键词、禁用项、母版路径、Unity 路径、GUID、摘要和审核状态由 `Docs/Engineering/idea-0016-visual-assets.json` 记录；玩法数值仍分别由资源、配方、科技和建筑正式目录拥有，视觉文件不得反向定义玩法。

本轮地图、seed、资源节点位置、建筑占地和存档 schema `31` 均不改变。新增材料来自加工、精炼、培育、共振或融合，不新增世界矿点。

## 2. 已确认风格

| 类别 | 风格键 | 方向 | 运行时主要用途 |
|---|---|---|---|
| 物品 | `idea0016.style.item.industrial-cel` | 工业手绘赛璐璐；强轮廓、少量高光、小尺寸可辨 | 资源状态栏、账本、背包、仓库、配方、生产详情、世界标记中心 |
| UI | `idea0016.style.ui.command-terminal` | 工业指挥终端；清晰层级、机械框架、低噪声可拉伸区域 | 科技树、资源/库存/合成面板、提示和状态框 |
| 科技 | `idea0016.style.technology.industrial-emblem` | 单一原理的工业手绘徽记 | 43 节点科技树、科技详情和 Development 修改器 |
| 建筑 | `idea0016.style.building.stylized-low-poly` | elevated 3/4 风格化低多边形建筑图 | 28 项建造目录、2 个升级目标、建筑和生产详情 |
| 人物 | `idea0016.style.character.cel-maintenance-commander` | 男性、遮脸、修长的赛璐璐维修指挥者岑烬 | 人物/指挥状态区和后续对话入口 |
| 世界标记 | `idea0016.style.world-marker.minimal-reticle` | 中央开孔、低遮挡的极简战术准星 | 既有资源节点标记外框；中心复用物品 Sprite |

风格批准只固定视觉方向，不等于逐项正式资产已经通过用户验收。

## 3. 内容规模与所有权

| 内容 | 精确数量 | 配置真值 | 视觉登记 |
|---|---:|---|---|
| 资源/物品 | 31 | `ResourceDefinitionCatalog` | 每个资源一张物品母版与一张 256×256 Unity Sprite |
| 配方 | 30 | `ResourceRecipeCatalog` | 每项持有主产物投影和机器/手工角标规则，不复制资源图 |
| 科技 | 43 | `ResearchCatalog` | 每个节点一张科技徽记；路线色和研究状态由运行时组合 |
| 建筑 | 30 | `BuildingCatalog` | 每个建筑一张 3/4 建筑图；28 项进入建造目录，2 项仅用于升级/详情 |
| 人物 | 1 | IDEA-0016 人物视觉登记 | `core.character.cen-jin` 唯一正式人物图 |
| 世界标记 | 1 | IDEA-0016 世界标记视觉登记 | `core.world-marker.resource-node` 外框，资源类型从物品目录解析 |
| UI | 以机器清单冻结值为准 | IDEA-0016 UI 视觉登记 | 只保留当前主循环需要的面板、节点、按钮、状态和分隔组件 |

完整 31 种资源的来源、用途和背景简介见 `Docs/superpowers/specs/2026-08-23-idea-0016-resource-recipe-design.md` 第 3 节；完整 30 条配方见同文第 5 节；43 个科技节点见 `Docs/superpowers/specs/2026-08-23-idea-0016-research-tree-design.md`。正式机器清单必须逐项复写这些语义，不能只保存文件名。

## 4. 文件合同

```text
Docs/Art/IDEA-0016/
├── IDEA0016-2D-Visual-Content-Catalog-ZH.md
├── Manifests/
├── Source/
│   ├── Items/
│   ├── UI/
│   ├── Technologies/
│   ├── Buildings/
│   ├── Characters/
│   └── WorldMarkers/
└── QA/
    ├── ContactSheets/
    ├── ReducedSize/
    ├── Alpha/
    └── InGame/

Assets/_Game/Art/Production2D/
├── Items/
├── UI/
├── Technology/
├── Buildings/
├── Characters/
├── WorldMarkers/
├── Atlases/
└── Catalogs/
```

仓库只保留真 Alpha 正式母版和 Unity 交付图。色键、抠图、候选、联网参考下载和生成缓存不得进入上述正式目录。PNG 由 Git LFS 管理；工作树文件必须能被图像解码器读取，不能把 LFS pointer 当图片。

## 5. 尺寸与透明合同

| 类别 | 正式母版 | Unity 交付 | 关键检查 |
|---|---:|---:|---|
| 物品 | 1024×1024 RGBA | 256×256 RGBA | 四边至少 12.5% 透明安全区；20 px 仍可区分 |
| 科技 | 1024×1024 RGBA | 256×256 RGBA | 四边至少 12.5%；32 px 仍能识别核心原理 |
| 建筑 | 1024×1024 RGBA | 256×256 RGBA | 四边至少 10%；64 px 能识别功能轮廓与朝向 |
| 人物 | 2048×2048 RGBA | 512×512 RGBA | 关键轮廓在中央 80%；96 px 保留观察窗和长外套身份 |
| 世界标记 | 1024×1216 RGBA | 256×304 RGBA | 中央 176×176 像素必须逐像素全透明 |
| UI 图标/组件 | 机器清单指定 | 机器清单指定 | 9-slice Border、最小显示尺寸和拉伸安全区必须可执行验证 |

所有图均禁止文字、水印、数量、状态面板和完整场景背景。资源名、储量、锁定、停工、枯竭、采集中、悬停和选中状态由运行时文本、颜色或外部角标表达。

## 6. 运行时映射边界

- 物品由 `ResourceIconCatalog3D` 按资源稳定 ID 解析；资源栏、背包、仓库、合成、生产详情和世界节点共用同一张 Sprite。
- 配方由 `ResourceRecipeCatalog.IconProjection` 持有“主产物 + 机器/手工角标”组合规则，界面不得按输出数组临时发明另一套命名。
- 科技由正式科技图标目录按研究稳定 ID 解析；缺图保留确定性路线/层级回退。
- 建筑由正式建筑图标目录按建筑稳定 ID 解析；升级目标不因此重新进入普通建造目录。
- 世界标记外框不拥有节点位置、类型、储量或放置合法性；中心层继续消费物品目录。
- UI 和人物资产只负责表现，不拥有研究、库存、生产、输入焦点或场景状态。

## 7. 生产记录与已知偏差

第一批 31 张物品图在统一机器清单文件建立前已开始生产，属于流程次序偏差。当前处理方式是：依据已经冻结的 `ResourceDefinitionCatalog` 逐项补齐简介、路径和摘要，重新验证 31/31 一一对应，并把原色键源全部替换为 1024×1024 真 Alpha 母版。此记录不把次序偏差写成批准的常规流程；后续科技、建筑、UI、人物和世界标记均必须先有类别清单/brief 再生成。

程序化 fallback、已作废的十张通用样图、未入选代表图、错误女性/露脸/半写实岑烬候选、第三方参考下载和本机生成缓存均不是正式资产，不进入机器清单或验证证据。

## 8. 完成与验收门

自动完成门包括：清单完整性、尺寸/Alpha/安全区、TextureImporter、稳定 GUID、目录映射、SpriteAtlas、缩小联络表、真实输入主循环、静态帧对象/监听器/分配稳定、schema `31` 回归、完整 EditMode/PlayMode、项目质量工具和现役构建。

自动化通过后仍需用户在真实游戏中检查：小尺寸辨识、科技树相邻节点差异、建筑比例与朝向、UI 拉伸与层级、人物身份观感，以及密集矿点区世界标记的遮挡。用户尚未确认的项目不得写成“已验证”。真实 Windows 10/11 GPU、显存、内存和视觉结果同样必须保留为待验收，直到取得真实证据。
