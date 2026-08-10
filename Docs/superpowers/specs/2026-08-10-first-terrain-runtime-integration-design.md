# 《废土移动城市》第一版七类地表运行时接入设计规格

> 日期：2026-08-10<br>
> 关联需求：`IDEA-0004`<br>
> 需求状态：已明确 / 已批准 / 开发中<br>
> 父规格：`Docs/superpowers/specs/2026-08-08-first-art-pass-production-design.md`<br>
> 地表源资源规格：`Docs/superpowers/specs/2026-08-08-first-terrain-materials-design.md`<br>
> 设计基线：`7b478b1269d3663e315561154482273b1e6620d4`<br>
> 当前阶段：书面设计已批准；尚未创建实施计划、Shader、Material、纹理数组、运行时代码或场景引用

## 1. 目标

把已经通过视觉验收和 Unity 导入合同的七类地表贴图接入默认 3D 场景，建立第一批可运行、可回退、可扩展的正式美术表现。

本里程碑必须同时达到以下结果：

1. 当前 `WorldMapModel` 仍是地形、通行、资源、建造和存档的唯一真值；
2. 七类地表通过一块连续网格和一个 URP 主材质显示；
3. 普通荒地、岩石地、湿地和结晶地表柔和、无缝衔接；
4. 废墟、深水和悬崖的视觉边缘自然，但玩法边界仍可读；
5. 深水具备缓慢法线流动和轻微高光变化，不增加水体模型；
6. 正式表现不可用时自动、完整恢复当前灰盒世界；
7. 运行时不创建逐格 Renderer、逐格材质实例或每帧 CPU 地表更新；
8. 用户通过固定截图和深水动态录屏确认最终视觉后，才可把本里程碑标记为已完成。

## 2. 已批准决策

本次对话已明确并批准：

- 首批只接入七类地表贴图；`Ruins` 八件模型和 `Cliff` 六件模型留给后续独立里程碑；
- 深水采用贴图材质表现，并使用两层缓慢错向法线与轻微高光变化；不创建水体网格、透明折射或真实波浪；
- Height 只参与材质细节与高度感知混合，不推动地表顶点，不改变 Collider、射线或玩法高度；
- 采用“连续地表 + 控制图 + Texture2DArray + 一个 URP 主材质”的方案；
- 原灰盒地表保留为安全回退，不删除、不改成玩法依赖；
- 正式地表成功后只隐藏灰盒地面及灰盒废墟、深水、悬崖表现；资源节点、城市、领袖、建筑与其他占位符继续显示；
- 一张 `2048×2048` 主贴图在世界中约覆盖 `4×4` 个外城逻辑格；
- 第一版不因美术接入修改地形枚举、移动倍率、阻挡、建造合法性、存档或 schema。

## 3. 与父规格的差异和解释

本规格不否定已经批准的美术方向，只细化首个运行时接入批次。若与父规格的执行细节不同，以本节为本批次边界：

| 父规格内容 | 本批处理 | 说明 |
|---|---|---|
| 地表最终可由贴图与少量几何增强 | 首批仅贴图 | 模型已验收但暂不接入，避免同时调试材质、拼接和模型分布 |
| 深水可使用简单共享水面网格 | 首批不使用水面网格 | 以主地表 Shader 的法线动画验证“像水”与边界可读性 |
| 悬崖应由最小模块几何表达高度差 | 首批只显示悬崖地表材质 | 逻辑阻挡保持有效；几何高度表现留给后续模型里程碑 |
| 软混合局部以两个主要地表为主 | 普通边界仍为两层，三岔点最多三层 | 三层只处理真实三类交汇，不允许七层同时采样 |
| Macro/Dust/Wetness 三张共享辅助图 | 不作为首批阻塞交付 | 首批使用确定性低频控制噪声和现有七套贴图；未来可作为独立视觉增强 |
| Height 源文件为 2048、16-bit | 源文件保持；运行数组使用 1024、R8 | Height 不参与几何，运行副本降采样用于控制显存，原始美术数据不改写 |

本批完成不能被描述为 `Ruins`、`DeepWater` 或 `Cliff` 的最终完整美术完成；它只证明七类地表贴图的首个正式运行时表现。

## 4. 方案比较与选择

### 4.1 方案 A：连续网格、控制图和纹理数组

- 一块连续网格；
- 两张小型控制图表达七类权重；
- 四组纹理数组保存 BaseColor、Normal、Mask、Height；
- 一个 URP 主材质完成 PBR、过渡和深水动画。

优点：无逐格 UV 缝，Renderer 数量最少，过渡质量和后续扩展最好。缺点：需要自定义 Shader、编辑器数组生成工具和清晰的回退边界。

### 4.2 方案 B：保留当前分类合并网格并分配独立材质

优点：接入代码少。缺点：当前内置 Plane 的 UV 在每格重复，分类边缘只能硬切；为了柔和过渡仍需增加额外边缘网格或 Shader，最终维护成本更高。

### 4.3 方案 C：转换为 Unity Terrain

优点：可使用 Terrain Layer 和内置地表绘制工具。缺点：引入新的高度、碰撞、坐标和地形生命周期，容易形成与 `WorldMapModel` 并存的第二套玩法真值，回退和测试成本最高。

### 4.4 结论

采用方案 A。方案 B、C 不进入实施计划。

## 5. 权威数据和稳定映射

### 5.1 玩法真值

以下内容继续只从现有规则读取：

- `TerrainKind.Wasteland`；
- `TerrainKind.Rocky`；
- `TerrainKind.Wetland`；
- `TerrainKind.Crystal`；
- `WorldTraversalKind.Ruins`；
- `WorldTraversalKind.DeepWater`；
- `WorldTraversalKind.Cliff`；
- 资源节点、通行、移动倍率、阻挡、建筑合法性和稳定节点 ID。

材质颜色、控制权重、Height、Normal、网格顶点和 Shader 参数不得反向写入上述规则。

### 5.2 七层固定顺序

纹理数组与 Profile 使用以下固定顺序：

| 层 | 类型 | 现有灰盒稳定 ID |
|---:|---|---|
| 0 | Wasteland | `world.terrain.wasteland` |
| 1 | Rocky | `world.terrain.rocky` |
| 2 | Wetland | `world.terrain.wetland` |
| 3 | Crystal | `world.terrain.crystal` |
| 4 | Ruins | `world.obstacle.ruins` |
| 5 | DeepWater | `world.obstacle.deep-water` |
| 6 | Cliff | `world.obstacle.cliff` |

数组层号不得依赖文件系统枚举顺序、中文名或 Inspector 手工拖拽顺序。编辑器生成器必须按上述常量表构建并验证。

## 6. 程序集与组件边界

### 6.1 新运行时程序集

新增独立程序集 `WasteCity.ArtIntegration3D`，建议位于：

```text
Assets/_Game/Scripts/ArtIntegration3D/
```

它只引用现有游戏规则、`WasteCity.Graybox3D` 和 Unity 运行时 API。它不得被纯规则程序集、Building 规则或 Persistence 反向引用。

### 6.2 现有 3D 边界中的最小接口

在 `WasteCity.Graybox3D` 中新增表现接口：

```csharp
public interface IGrayboxTerrainPresentation3D
{
    bool TryPresent(GrayboxWorldView3D worldView);
    void ClearPresentation();
}
```

`GrayboxSceneBootstrap` 只序列化一个可选 `MonoBehaviour`，运行时验证其是否实现该接口。正式地表缺失或配置失败不得使灰盒场景初始化失败。

### 6.3 正式地表组件

`FirstArtTerrainRenderer3D` 实现上述接口，职责限定为：

- 读取 `GrayboxWorldView3D.Model` 和 `Coordinates`；
- 创建一块连续地表网格；
- 生成两张控制图；
- 绑定 `FirstArtTerrainProfile3D` 和唯一正式材质；
- 完整成功后请求隐藏灰盒地表/障碍 Renderer；
- 禁用、销毁、重配或失败时清理自己的运行时对象并恢复灰盒。

它不负责输入、相机、建造、寻路、资源节点、存档或地形规则。

### 6.4 Profile

`FirstArtTerrainProfile3D : ScriptableObject` 保存：

- 四个纹理数组；
- 主材质或批准 Shader 的明确引用；
- 七层固定映射版本；
- 世界贴图比例；
- 各类过渡宽度；
- Height 混合强度；
- Normal 强度；
- 深水两层法线的方向、速度、比例和高光强度；
- 低频宏观明暗变化幅度；
- 控制图每格像素数。

Profile 不包含地图 seed、规则格、通行状态或存档数据。

## 7. 连续网格与坐标合同

### 7.1 网格范围

现有映射将逻辑格中心映射为：

```text
(x, y) -> (x - width / 2, visualY, y - height / 2)
```

因此连续地表边界覆盖首格中心外扩半格至末格中心外扩半格：

```text
minX = -width / 2 - 0.5
maxX =  width / 2 - 0.5
minZ = -height / 2 - 0.5
maxZ =  height / 2 - 0.5
```

地表视觉高度固定为当前数学地面 `Y=0` 的非玩法偏移允许值；第一版默认与数学平面重合。若为避免 Z-fighting 需要极小视觉偏移，只能作用于正式 Renderer，且不得修改投影平面、城市、领袖、建筑或 Collider。

### 7.2 网格结构

- 单一 Mesh；
- 单一 MeshRenderer；
- 法线统一向上；
- 无 Collider；
- 无逐格子对象；
- UV0 可保存 0–1 地图坐标，但材质主纹理必须使用世界 XZ 计算，不能依赖当前逐格 Plane UV；
- Bounds 必须覆盖整个地图，避免镜头边缘错误裁剪；
- 网格由表现组件拥有，并在 Clear/Destroy 时销毁，不保存进正式存档。

不做顶点位移，因此单一四边形即可满足首批渲染。若后续模型或地形变形需要细分，必须另行设计。

## 8. 控制图生成

### 8.1 分辨率和格式

每个逻辑格对应 `4×4` 个控制像素：

| 地图 | 控制图尺寸 |
|---|---|
| 32×24 | 128×96 |
| 96×64 | 384×256 |

控制图使用 Linear、Clamp、双线性采样和无 Mipmap，避免地图外侧采样回绕。它们是运行时临时 Texture，不写入存档。

### 8.2 通道

```text
ControlA.r = Wasteland
ControlA.g = Rocky
ControlA.b = Wetland
ControlA.a = Crystal

ControlB.r = Ruins
ControlB.g = DeepWater
ControlB.b = Cliff
ControlB.a = 保留，首批固定为 0
```

七个有效通道在进入 Shader 前必须归一化。任意像素的权重总和不能为 0；非法或缺失情况回退到 Wasteland，并记录一次明确错误。

### 8.3 基础地表

四类 `TerrainKind` 先生成 one-hot 权重，再在边界附近进行确定性平滑：

- Wasteland ↔ Rocky：1.0–1.5 格；
- Wasteland ↔ Wetland：0.8–1.5 格；
- Wasteland ↔ Crystal：0.8–1.2 格；
- 其他直接相邻组合使用两者中较窄的批准范围，不自行扩大至 1.5 格以上。

低频、可复现的边缘噪声只改变边缘形状，不改变分类中心。普通两类边界只保留两个主要权重；真实三岔点最多保留三个最高权重。

### 8.4 特殊遍历类型

当 `WorldTraversalKind` 不是 Open 时，对应特殊层覆盖基础地表，但边缘保留窄过渡：

- Ruins：0.50–1.00 格，使用积尘/碎屑语言；
- DeepWater：0.25–0.60 格，使用湿暗岸线；
- Cliff：0.20–0.50 格，使用碎石/断裂语言。

特殊格中心的对应层必须保持最高权重；视觉边缘不得改变 `WorldTraversalKind` 的格范围。地图边缘使用 Clamp，不向地图外生成虚假邻居。

### 8.5 确定性

相同 seed、宽高和规则地图必须得到相同控制图字节。算法不得读取当前时间、Unity 随机全局状态、相机位置或硬件相关噪声。

## 9. 贴图资产和数组

### 9.1 原始资产合同

七套已经验收的原始 PNG 保持不改：

- BaseColor：2048×2048，RGB 8-bit，sRGB；
- Normal：2048×2048，RGB 8-bit，Tangent Space，Linear；
- Mask：2048×2048，RGBA 8-bit，Linear；
- Height：2048×2048，Gray 16-bit，Linear。

Mask 通道固定为：

```text
R = Metallic
G = Ambient Occlusion
B = Detail Mask
A = Smoothness
```

### 9.2 生成资产

编辑器工具确定性生成：

```text
TA_Terrain_BaseColor.asset
TA_Terrain_Normal.asset
TA_Terrain_Mask.asset
TA_Terrain_Height.asset
MAT_Terrain_FirstPass.mat
FirstArtTerrainProfile3D.asset
```

建议目录：

```text
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/
├── Generated/
├── Materials/
├── Profiles/
└── Shaders/
```

源 PNG 不被合并或重写。重复执行生成工具必须保持生成资产 GUID、数组层顺序和内容稳定；源引用缺失、格式错误或尺寸不符时在修改任何已生成正式资产前失败。

### 9.3 运行格式

- BaseColor：2048、sRGB、Mipmap、Windows 优先 BC7；
- Normal：2048、Linear、Mipmap、Windows 优先 BC5；
- Mask：2048、Linear、Mipmap、Windows 优先 BC7 或等价四通道格式；
- Height：由 2048/16-bit 源确定性降采样为 1024/R8、Linear、Mipmap；
- 所有数组启用 Repeat；控制图单独使用 Clamp；
- 不在运行时解压、重编码或创建七套 2048 副本。

平台不支持指定压缩格式时必须使用经验证的等价格式，而不是产生粉色材质或静默丢通道。实施计划必须包含 macOS 编辑器和 Windows 构建的格式验证。

## 10. Shader 合同

### 10.1 主 Shader

新增 URP Shader，建议文件与 Shader 名称：

```text
Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader
WasteCity/Terrain/FirstPassBlend
```

首批使用受版本控制的 ShaderLab/HLSL 实现，不依赖运行时生成 Shader Graph。Shader 必须由 Material/Profile 的直接引用进入构建，不修改全局 `GraphicsSettings` 或 `QualitySettings`。

### 10.2 世界空间取样

- 主纹理世界比例为每张 2048 贴图覆盖约 `4×4` 外城格；
- 使用世界 XZ 取样，连续跨越逻辑格边界；
- 可加入幅度不超过约 ±5% 的低频综合色调变化，以降低大地图重复；
- 不旋转单独格子 UV，不产生边缘方向断裂；
- 不使用相机空间或屏幕空间纹理坐标。

### 10.3 PBR 混合

BaseColor、Normal、Mask 和 Height 使用相同地表权重。Height 仅用于权重微调和材质交界的自然嵌合：

- 不推动顶点；
- 不写深度偏移；
- 不生成视差造成的明显轮廓错位；
- 不改变 AO、Smoothness 或 Normal 的分类边界；
- 权重重整后仍保持总和为 1。

普通边界采样两个主要层；三岔点最多采样三个最高权重层。区域中心不得为零权重层支付完整七层采样成本。

### 10.4 深水

DeepWater 使用同一 Normal 层进行两次采样：

- 两个方向不平行；
- 两个速度均缓慢，默认镜头下可察觉但不抢夺注意力；
- 两层比例略有差异，避免同步重复；
- 高光变化来自 Mask Smoothness 与移动法线，不烘焙进 BaseColor；
- 保持蓝黑、危险、浑浊、不透明，不实现透明、折射、泡沫模拟或可见水底；
- 动画使用 Shader 时间，CPU 不增加 Update 水面逻辑。

## 11. 灰盒显隐与原子回退

### 11.1 需要隐藏的灰盒组

正式地表完整成功后，只隐藏以下 `GrayboxVisualSlot.Renderer`：

- `world.terrain.wasteland`；
- `world.terrain.rocky`；
- `world.terrain.wetland`；
- `world.terrain.crystal`；
- `world.obstacle.ruins`；
- `world.obstacle.deep-water`；
- `world.obstacle.cliff`。

资源节点和其他稳定 ID 不受影响。

### 11.2 原子顺序

```text
生成灰盒世界
  -> 正式表现验证 Profile / Shader / Material / arrays
  -> 创建连续网格
  -> 生成并验证控制图
  -> 绑定材质
  -> 正式 Renderer 可用
  -> 最后隐藏七个灰盒表现组
```

任何一步失败都必须销毁本次未完成的正式资源，并保持或恢复全部灰盒 Renderer。禁止先隐藏灰盒再尝试生成正式表现。

### 11.3 生命周期

- `TryPresent` 可重复调用，重建前先清理自己上一次生成对象；
- `OnDisable`、`OnDestroy`、配置替换和世界重建均恢复灰盒；
- 场景退出时不得把运行时 Mesh、Texture 或 Material 写回资产；
- 同一错误只输出一次带上下文的日志，不在 Update 中重复刷屏；
- 正式地表缺失属于可回退表现问题，不得阻止 `GrayboxSceneBootstrap.IsInitialized` 成功。

## 12. 场景与 Authoring

默认 `GrayboxPrototype3D` 增加一个正式表现对象，建议层级：

```text
World
├── TerrainRoot                 # 现有灰盒
├── ResourceRoot                # 现有灰盒资源节点
├── ObstacleRoot                # 现有灰盒遍历表现
└── FirstArtTerrainPresentation
    └── RuntimeSurface          # 仅运行时生成
```

场景只序列化：

- `FirstArtTerrainRenderer3D`；
- Profile 引用；
- 与 `GrayboxSceneBootstrap` 的可选表现接口引用。

运行时 Mesh、控制图和生成材质状态不序列化。场景 authoring 必须增量更新、连续运行幂等，并保持现有场景、URP、Renderer、GrayboxLit 和已导入美术资产 GUID 稳定。

本批不切换默认场景、不修改 Build Settings 顺序、不修改全局 URP 配置。

## 13. 性能预算

### 13.1 结构预算

- 正式地表：1 Mesh、1 MeshFilter、1 MeshRenderer、1 shared Material；
- 控制图：2 张；
- 不新增逐格 GameObject、逐格 Renderer 或逐格 Material；
- 现有灰盒对象可保持存在但 Renderer 隐藏，以支持回退；
- 32×24 和 96×64 都保持相同正式地表对象数量。

### 13.2 CPU 与分配

- 地表网格和控制图只在初始化或世界重建时生成；
- 正常稳定运行不执行 CPU 水面更新；
- 地表相关 Update/LateUpdate/渲染适配器预热后连续 300 帧托管分配为 0 B；
- 同 seed 的 96×64 控制图与网格生成需记录五次真实耗时，中位数目标不超过 250 ms；
- 性能探针不得用 NUnit 循环冒充真实 GUI Profiler 帧时。

### 13.3 GPU 与显存

- 首批七套运行纹理目标显存约 120 MB 以内，以真实 Profiler/平台导入结果记录；
- 目标为默认 1920×1080、60 FPS；
- 记录 GPU/CPU 帧时、Draw Call、SetPass、Renderer 数、纹理显存和 Shader 变体；
- 若 Texture2DArray 或三层混合导致目标硬件显著低于预算，应先保留原始资源并调整运行数组分辨率/格式，不改写源贴图。

## 14. 测试设计

### 14.1 纯数据和 EditMode

- 七层固定顺序与七类源贴图一一对应；
- BaseColor/Normal/Mask/Height 的尺寸、sRGB、Linear、Normal 类型和 Mask 通道合同；
- Height 运行副本为确定性 1024/R8；
- 数组重复生成内容与 GUID 稳定；
- 32×24 和 96×64 网格范围、Bounds、法线与 Renderer 数量；
- 两张控制图尺寸与通道映射；
- 任意像素权重非负且总和为 1；
- 单一地表中心为正确主层；
- 三岔点最多保留三个主要层；
- 三类软边界和三类特殊边界宽度；
- 相同输入生成相同控制图字节；
- 地图边缘 Clamp，无黑边或回绕；
- 配置缺失、错误 Shader、错误数组层数和生成异常均完整回退灰盒；
- 资源节点 Renderer 不被灰盒地表显隐 API 影响；
- 无 Collider、无逐格对象、无运行时材质实例。

### 14.2 PlayMode 正式场景

- 加载 `GrayboxPrototype3D` 后正式地表存在且只有一个 Renderer；
- 七个灰盒地面/遍历 Renderer 隐藏，资源节点仍可见；
- 禁用正式表现组件后灰盒同帧恢复；
- 重新启用后正式表现重建且不重复对象；
- 城市 WASD、右键 A*、展开/收起、领袖、镜头、建造投影和放置结果与接入前一致；
- 地面数学投影继续命中 Y=0，不读取正式 Mesh Collider；
- 相同 seed 下控制图类别与 `WorldMapModel` 一致；
- Shader、Material 或 Profile 故障场景无粉色、空白和 Missing Script；
- 正式 Release 不依赖 Development 修改器才能生成地表。

### 14.3 完整验证

- 完整 EditMode；
- 完整 PlayMode；
- 无界面编译；
- 默认 Release 3D Windows 构建；
- 显式 Development 3D Windows 构建；
- legacy 2D Windows 回归构建；
- 默认 3D 场景 GUI Profiler 300 连续帧；
- 真实 Windows 10/11 独立程序冒烟，若当前机器不能执行则明确保留为待补，不能用 macOS PE 格式检查冒充。

## 15. 视觉验收

自动测试不能替代视觉批准。实现完成后固定 seed、相机、光照、分辨率和曝光，提交：

1. 整张地图俯视图；
2. 默认游戏镜头截图；
3. Wasteland ↔ Rocky 近景；
4. Wasteland ↔ Wetland 近景；
5. Wasteland ↔ Crystal 近景；
6. 一个真实三岔点近景；
7. Ruins 边界近景；
8. DeepWater 岸线近景；
9. Cliff 边界近景；
10. 同 seed 灰盒与正式地表对照图；
11. 约 10 秒 DeepWater 动态录屏。

视觉通过标准：

- 默认镜头下一眼可区分七类地表；
- 四类基础地表无方格硬线、色环、法线断层或明显棋盘重复；
- 三类特殊区域边缘自然，但深水和悬崖仍明确不可通行；
- 深水确实像蓝黑浑浊水体，流速缓慢，高光不过曝，不像塑料、油漆或普通地面；
- Height 不造成轮廓漂浮、建造预览穿插或视觉/碰撞错位；
- Crystal 不出现未经批准的自发光光晕；
- 资源节点保持独立可读，不被 Crystal 地表或正式地面覆盖；
- 用户明确批准截图和录屏后才回写“视觉验收通过”。

## 16. 错误处理

| 情况 | 行为 |
|---|---|
| Profile 缺失 | 不生成正式地表，保留灰盒，记录一次错误 |
| 任一数组缺失或层数不是 7 | 不生成正式地表，保留灰盒 |
| Shader/Material 不匹配 | 不隐藏灰盒；不得出现粉色替代层 |
| World/Coordinates 尚未生成 | `TryPresent` 返回 false，不创建残留对象 |
| 控制图权重非法 | 清理本次正式对象并回退；错误包含坐标和权重 |
| 运行时组件禁用/销毁 | 清理自己拥有的 Mesh/Texture，恢复灰盒 |
| 世界重新生成 | 清理旧正式表现，使用新 Model/Coordinates 原子重建 |
| 平台格式不支持 | 构建或导入验证失败，使用已批准等价格式后重建，不静默降级通道 |

禁止捕获异常后继续显示不完整地表；禁止吞掉错误；禁止通过修改玩法规则来掩盖视觉接入失败。

## 17. 明确排除

- `Ruins` 八件 FBX/Prefab 接入；
- `Cliff` 六件 FBX/Prefab 接入；
- Rocky 散石、岸线模型、泡沫模型或环境装饰；
- 透明水、折射、水下、浮力、波浪顶点动画或水体 Collider；
- 地形顶点位移、Unity Terrain、NavMesh 或 Mesh Collider；
- 正式资源节点、城市、领袖、建筑、UI、VFX、SFX；
- 修改 TerrainKind、WorldTraversalKind、WorldMapModel、寻路、移动倍率或建造规则；
- 修改 Persistence、schema `30` 或正式存档；
- 修改默认入口、Build Settings 顺序、全局 GraphicsSettings/QualitySettings；
- 删除冻结 2D 或现有 3D 灰盒；
- 修改七套已批准的原始贴图内容与 `.meta` GUID；
- 把本批结果描述为第一版全部美术完成。

## 18. 回退方案

全部新增正式内容限定在独立 ArtIntegration 程序集、Terrain Runtime 资产目录、一个场景表现对象和少量明确的灰盒表现接口接线中。

回退顺序：

1. 禁用或移除 `FirstArtTerrainRenderer3D` 场景引用；
2. `GrayboxWorldView3D` 恢复七个灰盒 Renderer；
3. 删除新增 ArtIntegration 运行时和生成资产；
4. 撤销 Bootstrap/authoring/测试的少量接线；
5. 保留全部原始美术资产、玩法规则、存档、场景基础和灰盒内容。

不得通过删除灰盒、迁移存档或重做地图规则完成回退。

## 19. 实施分段与停止门

后续实施计划应拆为至少以下串行阶段：

1. 固定数组层、Profile、源贴图验证和确定性资产生成；
2. 连续网格、世界空间比例和控制图生成；
3. URP 主 Shader、PBR 通道与基础地表混合；
4. Ruins/DeepWater/Cliff 边界与 DeepWater 动画；
5. 灰盒显隐、原子回退和场景 authoring；
6. PlayMode、完整回归、性能、三构建和视觉交付。

每段先 RED 再最小 GREEN，并独立提交。出现以下任一情况立即停止并先修订书面设计/计划：

- 需要修改玩法规则、存档、schema、坐标真值或数学地面；
- 需要新增 Collider、NavMesh、Unity Terrain 或全局 URP 设置；
- 需要改写七套批准源贴图；
- Texture2DArray 在目标平台无法保持批准的 PBR 通道；
- 正式地表不能在失败时完整恢复灰盒；
- 需要提前接入 Ruins/Cliff 模型或其他未批准美术；
- 96×64 下必须创建逐格对象才能工作；
- 性能或显存超过预算且只能通过明显降低批准视觉质量解决。

## 20. 完成定义

本规格只有在以下项目全部满足后才可从“开发中”推进：

- 书面实施计划另行完成并通过审阅；
- 七套正式纹理通过数组生成和层映射测试；
- 正式场景显示一块连续、无缝、可辨识的七类地表；
- 深水动态符合用户批准的 B 方案；
- 所有自动测试、编译、三构建和性能门通过；
- 灰盒原子回退通过；
- 玩法、Persistence、schema、资源节点和冻结 2D 零回归；
- 固定截图和 DeepWater 录屏获得用户明确视觉批准；
- `IDEA-0004`、路线图、关联提交和验证证据按真实状态回写并推送。

在此之前，不得把 Material、Shader、场景映射或第一版地形运行时描述为已经实现或已经验证。
