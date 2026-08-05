# WasteCity 可操作 3D 灰盒基础设计

## 1. 文档状态与权威基线

本文定义冻结 2D 基线之后的首个可操作 3D 灰盒里程碑。本文只固定已经批准的产品与架构设计，不是实施计划。

权威开发基线：

- 分支基线：`origin/codex/camera-control`
- 提交：`e7911800491c75fdc33978982cfd3a52e11ab732`
- Unity：`2022.3.62f1`
- 目标平台：Windows 10/11 64 位
- 存档 schema：`30`
- 已验收自动化：EditMode `391/391`，PlayMode `50/50`
- 已验收构建：Windows Mono x86-64 构建成功
- 未完成验收：真实 Windows 10/11 独立程序运行冒烟

本里程碑最终只服务于 3D 产品。现有 `FormalPrototype`、二维规则、稳定 ID、schema 30 和自动化测试继续保留为只读回归基线，但不形成需要向玩家发布或长期维护的第二套可玩产品。

## 2. 目标

首个里程碑交付独立、可回退、可操作的俯视 3D 灰盒基础：

1. 新建独立 `GrayboxPrototype3D` 场景和 3D 适配程序集；
2. 使用同一世界种子生成相同的 `32×24` 地形、资源和障碍规则数据；
3. 把逻辑二维 `(x,y)` 映射到 Unity XZ 平面；
4. 提供可直接驾驶和自动寻路的 3D 移动城市；
5. 保留现有地形阻挡、减速、A* 路径和 `3×3` 展开合法性；
6. 提供 3D 领袖占位和既有直接控制切换；
7. 提供倾斜正交镜头，并保留既有 Following/Free、拖动、快速返回和目标切换语义；
8. 使用场景作用域 URP、Unity 原生几何体、基础材质和稳定视觉 ID；
9. 保留 2D 默认入口，同时提供只包含 3D 场景的独立 Windows 构建。

本里程碑把原先的“3D 场景/坐标/视觉基础”和“城市移动/展开/领袖/镜头”合并为一个验收里程碑。实现时仍须按独立职责拆分为可回滚的小提交，但提交拆分由后续实施计划定义。

## 3. 冻结设计决策

### 3.1 产品形态

- 最终玩家产品只交付 3D 版本。
- 3D 保持俯视操作，不采用第三人称。
- 未完成的建造菜单、内外城放置、物流、前哨、迷雾和领袖交互只在 3D 中实现一次。
- `FormalPrototype` 不原地转换为 3D，也不与 3D 共用带维度条件分支的运行时组件。

### 3.2 规则与坐标

- 世界规则继续使用二维 `(x,y)`、整数网格和平面距离。
- Unity 3D 表现使用 XZ 平面，Y 只表示视觉高度。
- Y 不进入地形通行、A*、展开合法性、直接控制规则或存档真值。
- 城市自动驾驶继续复用 `CityPathfinder`，不以 NavMesh 作为玩法真值。
- 深水、悬崖、废墟、湿地和岩地仍由 `WorldMapModel` 与 `CityTerrainRules` 判定。

### 3.3 表现与渲染

- 镜头采用倾斜正交俯视。
- 首阶段地形在玩法上保持平面；地形隆起、悬浮和障碍高度只作视觉。
- 只使用 Plane、Cube、Capsule、基础材质、程序化占位和稳定视觉 ID。
- 不下载、不购买资源，不新增 Unity 包，不制作正式模型、动画或复杂特效。
- 当前仓库只是安装了 URP `14.0.12`，`GraphicsSettings` 和所有 Quality 档尚未配置启用 SRP；首阶段不得把 URP 已启用视为现状。
- 首阶段通过场景作用域切换 URP，离开 3D 场景时恢复此前渲染管线引用。

### 3.4 存档与入口

- 首阶段禁止读取或写入正式 `formal-world.json`。
- schema 保持 `30`。
- 后续 3D 存档适配器继续使用同一二维数据语义，不保存表现 Y。
- 首阶段保留 2D 默认编辑/构建入口，并增加独立 3D 构建。
- 首阶段验收通过后，以单独变更尽早把默认编辑/构建入口切到 3D；最终玩家构建不包含 `FormalPrototype`。

## 4. 总体架构

### 4.1 单向程序集依赖

```text
Unity / Input System / URP Runtime
                 │
                 ▼
        WasteCity.Graybox3D
                 │
                 ▼
          WasteCity.Game

WasteCity.EditModeTests ──► WasteCity.Game + WasteCity.Graybox3D
WasteCity.PlayModeTests ──► WasteCity.Game + WasteCity.Graybox3D
WasteCity.Editor ─────────► 按场景路径执行独立构建
```

约束：

- `WasteCity.Game` 不引用 `WasteCity.Graybox3D`。
- `WasteCity.Graybox3D` 引用既有 `WasteCity.Game`、`Unity.InputSystem`、`Unity.RenderPipelines.Core.Runtime` 和 `Unity.RenderPipelines.Universal.Runtime`。
- 现有 2D MonoBehaviour 不增加 `is3D`、场景类型或渲染器类型条件分支。
- 3D 场景不引用 `PlaceholderWorldView`、`PlaceholderMobileCity`、`FormalLeaderController` 或 `FormalCameraController`。
- 新适配器可以实例化和调用现有纯模型，但不得复制其决策规则。

### 4.2 分层

```text
输入转译
  └─ GrayboxInputRouter
       ├─ 城市手动输入 / 右键目的地 / F
       ├─ 领袖手动输入
       └─ 镜头中键 / Home

3D 适配
  ├─ PlanarCoordinateMapper3D
  ├─ GrayboxWorldView3D
  ├─ GrayboxMobileCityController3D
  ├─ GrayboxLeaderController3D
  ├─ GrayboxDirectControlCoordinator
  ├─ GrayboxCameraController3D
  ├─ GrayboxGroundProjector
  ├─ GrayboxVisualSlot
  └─ GrayboxUrpScope

既有规则
  ├─ WorldMapModel / WorldSeed
  ├─ CityTerrainRules / CityPathfinder
  ├─ CityDeploymentModel / CityDeploymentRules
  ├─ CityOperationalRules
  ├─ DirectControlRules
  └─ CameraFollowModel
```

### 4.3 规则唯一真值

| 决策 | 唯一真值 | 3D 适配器职责 |
|---|---|---|
| 地形和资源生成 | `WorldMapModel`、`WorldSeed` | 生成 Mesh 和颜色 |
| 城市能否通行 | `CityTerrainRules.IsPassable` | 把候选 XZ 转为逻辑平面后查询 |
| 城市速度 | `CityTerrainRules.SpeedMultiplier` | 把倍率应用到 3D 位移 |
| 自动驾驶路径 | `CityPathfinder` | 把 `WorldGridPoint` 映射成 XZ 路点 |
| 展开合法性 | `CityDeploymentRules.Validate` | 显示失败原因和启动状态转换 |
| 展开/收起状态 | `CityDeploymentModel` | 驱动灰盒形状，不自行改状态 |
| 当前直接控制对象 | `DirectControlRules.Resolve` | 把输入交给城市或领袖 |
| 镜头 Following/Free | `CameraFollowModel` | 移动 CameraRig 和转译输入 |

3D Mesh、材质、Collider、颜色、Transform Y 和视觉槽不得反向决定任何玩法状态。

## 5. XY ↔ XZ 坐标契约

### 5.1 单位与原点

- 一个逻辑格等于一个 Unity 世界单位。
- 逻辑 X 映射到 Unity X。
- 逻辑 Y 映射到 Unity Z。
- Unity Y 是表现高度。
- 世界继续以地图中心为原点。
- 与现有 2D 一致，格坐标锚点不额外增加 `0.5` 偏移。

对宽 `W`、高 `H` 的地图：

```text
cellToPlaneX = cellX - W * 0.5
cellToPlaneY = cellY - H * 0.5

cellToWorld3D = (
    cellToPlaneX,
    visualHeight,
    cellToPlaneY
)
```

从 Unity 世界点恢复逻辑格：

```text
cellX = floor(worldX + W * 0.5)
cellY = floor(worldZ + H * 0.5)
```

转换必须同时验证 `0 <= cellX < W` 和 `0 <= cellY < H`。越界返回失败，不钳制到边缘。

### 5.2 `32×24` 示例

| 逻辑格 | Unity 世界坐标，Y=0 |
|---|---|
| `(0,0)` | `(-16,0,-12)` |
| `(8,7)` | `(-8,0,-5)` |
| `(16,12)` | `(0,0,0)` |
| `(31,23)` | `(15,0,11)` |

现有城市起点的 2D 平面位置 `(-8,-5)` 在 3D 中映射为 `(-8, cityVisualY, -5)`，仍对应逻辑格 `(8,7)`。

### 5.3 连续位置与存档语义

`cityX/cityY`、`leaderX/leaderY`、敌人和友军快照 `x/y`、集结点 `rallyX/rallyY` 都表示连续逻辑平面坐标，而非整数格。

后续 3D 存档恢复约定：

```text
saved (x,y) → Unity (x, heightProvider(x,y), y)
Unity (x,Y,z) → saved (x,z)
```

首阶段不接入正式存档。上述约定只用于保证未来适配不会要求 schema 变化。

### 5.4 表现高度

首阶段玩法平面恒为 `Y=0`：

- 城市和领袖的 Transform Y 只用于让几何体站在平面上；
- 资源标记、废墟和悬崖可以高于平面；
- 深水可以低于或贴近平面；
- 射线目的地投影、路径、距离和展开校验仍使用 `Y=0` 平面；
- 任何视觉 Y 都不写入模型或存档。

## 6. 3D 场景对象树

```text
GrayboxPrototype3D
├─ GrayboxRenderScope
│  └─ GrayboxUrpScope
├─ CameraRig
│  └─ Main Camera
│     ├─ Camera
│     └─ GrayboxCameraController3D
├─ GrayboxWorld
│  ├─ GrayboxWorldView3D
│  ├─ TerrainRoot
│  ├─ ResourceRoot
│  └─ ObstacleRoot
├─ GrayboxActors
│  ├─ MobileCity
│  │  ├─ Rigidbody
│  │  ├─ BoxCollider
│  │  ├─ GrayboxMobileCityController3D
│  │  └─ GrayboxVisualSlot
│  └─ Leader_CenJin
│     ├─ CapsuleCollider
│     ├─ GrayboxLeaderController3D
│     └─ GrayboxVisualSlot
└─ GrayboxSystems
   ├─ GrayboxSceneBootstrap
   ├─ GrayboxInputRouter
   ├─ GrayboxDirectControlCoordinator
   └─ GrayboxGroundProjector
```

场景约束：

- 3D 场景中恰好存在一个启用的 `GrayboxUrpScope`、一个 Main Camera、一个城市控制器和一个直接控制协调器。
- `GrayboxWorldView3D` 由 `GrayboxSceneBootstrap` 使用 seed `8128` 初始化。
- 3D 场景不包含正式存档控制器。
- 不把现有 `FormalGameBootstrap` 或整套 2D Systems 复制进 3D 场景。
- 城市 Rigidbody 使用 Kinematic、关闭重力、启用插值，并锁定 Y 和全部旋转；Collider 不参与通行裁决。

## 7. 新 3D 适配器职责

### 7.1 `PlanarCoordinateMapper3D`

负责：

- 整数格与 XZ 世界点互转；
- 连续逻辑平面坐标与 XZ 世界点互转；
- 地图边界验证；
- 把调用方显式传入的 `visualY` 应用到 Unity Y；首阶段不引入高度提供器。

不负责：

- 地形通行；
- 路径搜索；
- 高度玩法；
- 存档读写。

### 7.2 `GrayboxWorldView3D`

负责：

- 持有只读 `WorldMapModel` 引用；
- 从同一模型生成地形、资源和障碍灰盒；
- 为每个稳定 ID 生成一个合并网格和一个 Renderer；
- 提供地图尺寸和坐标映射器；
- 在场景销毁时释放运行时生成的 Mesh 和 MaterialPropertyBlock 状态。

不负责：

- 修改 `WorldCell`；
- 根据颜色或 Mesh 推断资源、阻挡或展开合法性；
- 迷雾、物流或建筑放置。

### 7.3 `GrayboxMobileCityController3D`

内部使用新的 `CityDeploymentModel(3f,5f)`，但所有状态规则仍来自既有模型和规则类。

负责：

- 接受标准化 WASD 平面输入；
- 接受逻辑格目的地；
- 调用 `CityPathfinder` 生成路径；
- 用 Rigidbody `MovePosition` 沿 XZ 路点移动；
- 在每个候选平面位置调用 `CityTerrainRules`；
- 应用当前格速度倍率；
- 手动输入时取消自动驾驶；
- 处理到达、路径失效和模式转换；
- 调用 `CityDeploymentRules.Validate` 后切换展开状态；
- 暴露 Mode、Autopilot、Destination 和最近结果。

城市控制器不得：

- 使用 Physics 或 Collider 判断深水、悬崖和废墟；
- 修改 `CityPathfinder`；
- 在非 `Mobile` 状态移动城市；
- 自动展开；
- 保存自身状态。

### 7.4 `GrayboxLeaderController3D`

负责：

- 持有既有 `LeaderModel`，不创建第二套领袖招募或过载状态；
- 当直接控制目标为 Leader 时接受标准化 WASD；
- 把候选 XZ 转回逻辑平面并使用现有通行规则；
- 在非 Leader 控制状态时停靠到城市平面偏移 `(+1.8,+1.2)`；
- 暴露领袖 Transform 作为镜头目标。

首个灰盒场景使用明确标记的开发验证夹具，通过 `LeaderModel.Restore` 使领袖处于已招募、健康、无冷却状态，以便人工和自动化验证 Fortress 后的控制切换。该夹具：

- 不提供玩家招募交互；
- 不写存档；
- 不改变正式开局“无领袖”规则；
- 在后续领袖交互里程碑由正式招募流程替代。

### 7.5 `GrayboxDirectControlCoordinator`

负责：

- 每帧读取城市 Mode 和领袖招募状态；
- 只调用 `DirectControlRules.Resolve` 得到 `DirectControlTarget`；
- 把当前目标暴露给输入路由和镜头；
- 在目标改变时发送一次目标变化通知。

不得复制 `Fortress && leaderRecruited` 判断。

### 7.6 `GrayboxGroundProjector`

负责：

- 使用 Main Camera 的屏幕射线与 `Y=0` 数学平面求交；
- 把命中 XZ 转换成逻辑平面或格坐标；
- 射线平行、位于相反方向或命中地图外时返回失败。

右键目的地不依赖地面 Collider。未来建筑和单位选择可以新增 `Physics.Raycast` 适配器，但不能改变本投影约定。

### 7.7 `GrayboxCameraController3D`

负责：

- 复用 `CameraFollowModel`；
- 读取 `GrayboxDirectControlCoordinator` 的现有目标；
- 移动 CameraRig 的 XZ，保持固定旋转、相机局部偏移和正交尺寸；
- 中键拖动、Home 返回和目标变化后的重新跟随；
- 缺失领袖目标时安全回退城市；
- 缺失全部目标时保持当前位置，不回世界原点。

不得：

- 复制直接控制规则；
- 平滑、惯性、震动、缩放、边缘滚动或边界限制；
- 依赖 `Time.deltaTime` 处理镜头输入。

## 8. 城市、领袖与镜头数据流

### 8.1 输入数据流

```text
Keyboard.current / Mouse.current
                │
                ▼
       GrayboxInputRouter
        │       │       │
        │       │       └─ 中键/Home ─► GrayboxCameraController3D
        │       └─ 右键屏幕点 ─► GrayboxGroundProjector ─► 目的地格
        ├─ WASD ─► 当前 DirectControlTarget 对应控制器
        └─ F ─► GrayboxMobileCityController3D
```

输入约定：

- 城市为当前目标且处于 Mobile 时，WASD 直接驾驶；
- 城市为当前目标但不处于 Mobile 时，WASD 不移动城市；
- Leader 为当前目标时，WASD 控制领袖；
- 右键仅在 Mobile 尝试设置城市目的地；
- F 在 Mobile 尝试展开，在 Fortress 尝试收起，在转换中返回稳定失败原因；
- 暂停时城市、领袖和展开计时停止；
- 暂停时镜头中键拖动和 Home 仍工作。

### 8.2 城市移动状态

```text
手动输入为零
  ├─ 无自动驾驶：保持
  └─ 有自动驾驶：沿当前路径点移动

手动输入非零
  ├─ 取消自动驾驶
  ├─ 归一化方向
  ├─ 查询当前格速度倍率
  ├─ 生成候选 XZ
  └─ 规则可通行才 MovePosition
```

自动驾驶：

- 路径仍是四方向格路径；
- 路点映射为格锚点 XZ；
- 到达容差沿用 `0.08` 世界单位；
- 到达终点、切换出 Mobile 或路径无效时关闭；
- 不缓存到存档。

### 8.3 展开状态机

```text
Mobile
  └─ F + 3×3 合法 ─► Deploying（3 秒）
                         └─ 完成 ─► Fortress

Fortress
  └─ F ─► Packing（5 秒）
              └─ 完成 ─► Mobile
```

约束：

- Deploying、Fortress、Packing 时城市不移动；
- 展开前以城市所在逻辑格为中心调用既有 `3×3` 校验；
- 越界、阻挡和不稳定地面使用既有失败枚举和文字；
- 本里程碑不实现取消转换、受击中断或科技缩时。

### 8.4 直接控制状态

| 城市状态 | 领袖未招募 | 领袖已招募 |
|---|---|---|
| Mobile | City | City |
| Deploying | City | City |
| Fortress | City（不可移动） | Leader |
| Packing | City | City |

该表只用于规格说明；实现必须调用 `DirectControlRules.Resolve`，不能重新编码表内条件。

### 8.5 镜头状态机

```text
初始 ─► Following

Following
  ├─ 中键按下 ─► Free
  └─ 目标变化 ─► Following 并同帧对准新目标

Free
  ├─ 中键拖动 ─► Free，CameraRig 与鼠标方向相反
  ├─ 中键松开 ─► Free
  ├─ Home ─► Following 并同帧对准当前目标
  └─ 目标变化 ─► Following 并同帧对准新目标
```

倾斜正交拖动使用前一屏幕点和当前屏幕点分别与 `Y=0` 平面求交：

```text
cameraRigDelta = previousPlaneHit - currentPlaneHit
```

这保证“抓住地图”的反向拖动语义，并避免把 2D 正交每像素换算错误套用到倾斜平面。

## 9. 倾斜正交镜头契约

场景默认值：

- CameraRig 初始平面位置：城市起点；
- Main Camera 为 CameraRig 子对象；
- Main Camera 局部位置：`(0,18,-14)`；
- Main Camera 局部欧拉角：`(52,0,0)`；
- Projection：Orthographic；
- Orthographic Size：`13`；
- 不允许运行时修改旋转、局部偏移或正交尺寸。

Following 只把 CameraRig 的 X/Z 对准目标 X/Z，并保持 CameraRig Y。Free 只改 CameraRig X/Z。Main Camera 子对象的局部变换保持不变。

## 10. 场景作用域 URP

### 10.1 进入

`GrayboxUrpScope` 使用 `-10000` 脚本执行顺序，`GrayboxSceneBootstrap` 使用 `-9000`，从而保证在 3D 灰盒初始化前：

1. 记录进入时的 `GraphicsSettings.defaultRenderPipeline`；
2. 记录进入时的 `QualitySettings.renderPipeline`；
3. 把两者设置为灰盒专用 `UniversalRenderPipelineAsset`；
4. 确认灰盒 Pipeline 使用 Universal Renderer，而不是 2D Renderer；
5. 只在管线激活后允许 `GrayboxSceneBootstrap` 生成灰盒表现。

灰盒材质直接引用灰盒 URP Asset 可用的 URP Shader，确保 Windows 独立构建不会因场景作用域切换而剥离所需 Shader。

### 10.2 退出

`OnDisable` 或 `OnDestroy` 时：

1. 若当前 Quality 管线仍是本实例设置的灰盒管线，则恢复进入时记录的 Quality 管线；
2. 若当前 Graphics 默认管线仍是本实例设置的灰盒管线，则恢复进入时记录的 Graphics 默认管线；
3. 任一属性已被外部改为其他值时，保留外部新值；
4. 清除静态所有权，避免关闭场景后污染后续 2D 测试。

场景契约禁止同时启用多个 `GrayboxUrpScope`。恢复逻辑不得把外部在运行期间主动设置的新管线覆盖回旧值。

### 10.3 首阶段禁止事项

- 不修改 `ProjectSettings/GraphicsSettings.asset`；
- 不修改 `ProjectSettings/QualitySettings.asset`；
- 不把 URP 引用加入现有 `WasteCity.Game.asmdef`；
- 不重导入或改写 `FormalPrototype` 的 Sprite 材质；
- 不创建全项目渲染管线迁移。

## 11. 灰盒视觉规范

### 11.1 视觉槽

新增 `GrayboxVisualSlot`，只负责：

- 保存合法稳定 ID；
- 保存 MeshRenderer 和回退颜色；
- 暴露 `StableId`；
- 确保缺少正式 VisualDefinition 时仍显示程序化占位。

它不持有生命、移动、通行、招募、生产或存档状态。首阶段不修改现有硬绑定 `SpriteRenderer` 的 `VisualSlot`。

### 11.2 地形与障碍

| 对象 | 形状 | 颜色 | 稳定视觉 ID |
|---|---|---|---|
| 荒地 | Plane 顶面合并网格 | `(0.20,0.22,0.18)` | `world.terrain.wasteland` |
| 岩地 | Plane 顶面合并网格 | `(0.31,0.24,0.16)` | `world.terrain.rocky` |
| 结晶地 | Plane 顶面合并网格 | `(0.16,0.30,0.34)` | `world.terrain.crystal` |
| 湿地 | Plane 顶面合并网格 | `(0.13,0.28,0.22)` | `world.terrain.wetland` |
| 大型废墟 | Cube 组合 | `(0.20,0.20,0.20)` | `world.obstacle.ruins` |
| 深水 | 低位薄 Cube | `(0.03,0.12,0.28)` | `world.obstacle.deep-water` |
| 悬崖 | 高 Cube | `(0.12,0.08,0.05)` | `world.obstacle.cliff` |

地形、障碍和资源均按稳定 ID/颜色生成合并网格，不为每个格或资源点创建独立长期 GameObject。

### 11.3 资源

| 资源 | 形状 | 颜色 | 稳定视觉 ID |
|---|---|---|---|
| 铁矿 | 小 Cube | `(0.75,0.45,0.20)` | `core.resource.iron` |
| 能晶矿 | 小 Capsule | Cyan | `core.resource.energy-crystal` |
| 石材 | 小 Cube | Gray | `core.resource.stone` |
| 生物质 | 小 Capsule | Green | `core.resource.biomass` |
| 水源 | 低位小 Cube | Blue | `core.resource.water` |

### 11.4 城市与领袖

| 对象 | 形状与尺寸 | 颜色 | 稳定视觉 ID |
|---|---|---|---|
| 移动城市 | Cube，约 `(3,1,2)` | 橙色 `(0.90,0.48,0.10)` | `core.city.mobile` |
| 领袖岑烬 | Capsule，高约 `1.8` | 青色 `(0.20,0.85,0.95)` | `core.character.cen-jin` |

城市形态转换只通过程序化尺寸和颜色插值表达：

- Mobile：尺寸 `(3,1,2)`，颜色 `(0.90,0.48,0.10)`；
- Deploying：按现有 Progress 从 Mobile 插值到 Fortress；
- Fortress：尺寸 `(3,1.5,3)`，颜色 `(0.55,0.60,0.65)`；
- Packing：按现有 Progress 从 Fortress 插值回 Mobile。

缩放时同步调整 MeshRenderer 和 BoxCollider 的表现边界，并调整中心 Y 使底面保持在 `Y=0`。城市稳定视觉 ID 不随 Mode 改变。Collider、颜色和尺寸不作为状态真值。

## 12. 错误处理与安全降级

- 世界尚未生成：城市拒绝自动驾驶和展开，保持当前位置。
- 右键投影失败或地图外：不改变现有路径和状态，返回稳定原因。
- 目标格不可通行或不可达：不启动自动驾驶。
- 自动路径运行中失效：安全取消自动驾驶，不瞬移。
- 城市候选位置不可通行：拒绝本次位移。
- 领袖候选位置不可通行：拒绝本次位移。
- 领袖引用不可用：直接控制和镜头有效目标安全回退城市。
- 城市引用不可用：镜头保持当前位置，不回原点。
- 灰盒 URP Asset 缺失：停止灰盒表现初始化并报告明确错误，不修改项目全局资源。
- 场景退出：无条件释放运行时 Mesh；按第 10 节条件恢复管线。

错误处理不得创建第二套规则、静默改变 schema 或回退到 NavMesh。

## 13. 独立 3D 构建与入口

### 13.1 首阶段

- `EditorBuildSettings` 保持 `FormalPrototype` 为第一个启用场景；
- `GrayboxPrototype3D` 作为第二个启用场景加入，供 PlayMode 加载；
- 现有 `FormalBuildTools.BuildWindows()` 保持原行为，只构建 2D 冻结基线；
- 新增独立 `BuildWindowsGraybox3D()`，只构建 `GrayboxPrototype3D`；
- 3D 输出路径使用 `Builds/Windows3D/WasteCityGraybox.exe`，不得覆盖已验证 2D 产物；
- 两个构建分别执行格式检查。

### 13.2 首阶段之后

首里程碑验收通过后，用独立变更：

1. 把默认编辑入口切到 `GrayboxPrototype3D`；
2. 把正式 Windows 构建改为只包含 3D 场景；
3. 从最终玩家构建列表移除 `FormalPrototype`；
4. 保留 2D 场景资产和显式回归测试加载路径；
5. 单独评审是否从场景作用域 URP 转为全项目 URP。

默认入口切换不属于本里程碑。

## 14. 测试分层

### 14.1 既有 2D 回归

- 原 EditMode `391/391` 必须继续通过；
- 原 PlayMode `50/50` 必须继续通过；
- `FormalPrototype` 场景契约不变；
- 现有 2D Windows 构建继续成功；
- 任何新增测试后的总数必须从实际 XML 读取，不预填。

### 14.2 新增 EditMode 规则/坐标测试

覆盖：

- `32×24` 四个代表格的 XY↔XZ 精确映射；
- 连续平面坐标往返；
- 地图外转换失败且不钳制；
- Y 高度不影响反向平面坐标；
- 同 seed 下 3D 视图使用的 Terrain、Resource、Traversal 与 `WorldMapModel` 逐格一致；
- 3D 适配器调用既有地形、路径、展开和直接控制规则；
- 玩法判断不读取 Renderer、Collider 或 Transform Y。

### 14.3 新增 EditMode Unity 适配器测试

覆盖：

- 城市只改 X/Z，保持规定 Y；
- 手动输入取消自动驾驶；
- 右键目的地生成既有 A* 路径；
- 深水和悬崖阻挡；
- 岩地、湿地和废墟使用既有倍率；
- `3×3` 展开失败原因和成功状态；
- 领袖只在 Fortress+已招募时成为直接控制目标；
- 镜头缺失领袖时回退城市且不回原点；
- 场景作用域 URP 进入、退出和外部管线变更保护。

### 14.4 新增 PlayMode 场景测试

使用 Single 模式加载 `GrayboxPrototype3D`，覆盖：

- 场景对象树和序列化引用完整；
- URP Universal Renderer 已在场景作用域生效；
- 生成相同 `32×24` 地图、资源和障碍；
- 核心灰盒对象不使用 `SpriteRenderer`、`Rigidbody2D` 或 `Collider2D`；
- Mobile 下城市 WASD 移动；
- 可达右键目的地使城市沿路径接近终点；
- 非法目标和非法展开不改变模式；
- 合法位置完成 Deploying→Fortress；
- Fortress+已招募后 WASD 控制领袖，城市不移动；
- Packing 完成后恢复城市控制；
- Following、Free、松开保持、Home 返回和目标切换恢复；
- `Time.timeScale == 0` 时镜头仍可拖动和返回，玩法运动停止；
- 操作镜头不改变自动驾驶或展开状态；
- 卸载场景后恢复进入前渲染管线。

3D 与 2D 场景测试不得同时以 Additive 模式保留，避免现有全局对象查找命中重复组件。

### 14.5 构建验收

- 无界面脚本编译成功；
- 2D 冻结构建成功；
- 独立 3D Windows Mono x86-64 构建成功；
- `file Builds/Windows/WasteCity.exe` 为 Windows x86-64 GUI 可执行文件；
- `file Builds/Windows3D/WasteCityGraybox.exe` 为 Windows x86-64 GUI 可执行文件；
- 真实 Windows 10/11 独立程序运行冒烟继续标记待补。

## 15. 性能预算

首阶段使用固定 seed、`32×24` 地图、一个城市、一个领袖和一个相机。

结构预算：

- 地形按四类合并，障碍按三类合并，资源按五类合并；
- 生成后的世界表现 Renderer 数量不超过 `16`；
- 不为 768 个地形格保留 768 个独立 GameObject；
- 世界、城市、领袖和镜头稳定运行后，适配器 Update/LateUpdate 不产生托管堆分配；
- 不在每帧调用 `FindObjectOfType`、`FindObjectsOfType`、运行时 Mesh 重建或材质实例化；
- 仅在世界生成或场景销毁时创建/释放程序化 Mesh。

时间预算：

- 在当前开发机上，固定地图连续 5 次生成的中位数不超过 `250 ms`；
- 完成两帧预热后，空闲灰盒场景连续 300 帧不得出现适配器产生的 GC Alloc；
- 首阶段开发机目标为 1920×1080 下稳定 60 FPS；
- GDD 的正式 Windows 目标仍是 60 FPS、1% low 不低于 45 FPS，最终在完整垂直切片和真实 Windows 设备上验收，不能由本阶段 macOS 数据代替。

性能测试失败时优先减少 GameObject/Renderer/分配，不得通过降低规则更新正确性规避。

## 16. 回退机制

本里程碑的回退边界是：

- 独立 `GrayboxPrototype3D` 场景；
- 独立 `WasteCity.Graybox3D` 程序集；
- 独立灰盒 URP Asset、Universal Renderer 和材质；
- 独立 3D 构建入口；
- 少量 `EditorBuildSettings` 和测试 asmdef 接线；
- 新增 3D 测试。

失败回退时可删除上述新增内容并撤销少量接线。以下内容不应需要回退，因为本里程碑不得修改：

- `FormalPrototype.unity`
- 既有规则与模型
- 现有 2D MonoBehaviour
- schema 30
- `FormalSaveController`
- `PlaceholderBuildingController`
- `GraphicsSettings.asset`
- `QualitySettings.asset`
- 包管理器文件
- `BUG-0001`

每个实现提交必须只包含一个可验证职责。任何提交失败都能在不改写 2D 冻结提交的前提下单独撤销。

## 17. 明确排除项

本里程碑不实现：

- 建造菜单显示、筛选、选择或交互改造；
- 内城或地面建筑放置；
- 建筑施工、生产、物流和物流观察；
- 前哨、迷雾正式玩法或领袖招募交互；
- 敌人、炮塔、防御、伤害表现或波次；
- 撤离和完整垂直切片；
- 正式存档读取、写入或 schema 修改；
- NavMesh 玩法导航；
- 地形高度、坡度、悬浮对通行或展开的影响；
- 第三人称镜头；
- 镜头平滑、惯性、缩放、边缘滚动、边界、震动或 Cinemachine；
- 正式模型、绑定、动画、纹理、复杂材质和特效；
- 全项目输入系统迁移或 `.inputactions`；
- 默认入口切换；
- 现有 2D 运行时重构；
- 新 Unity 包；
- `BUG-0001` 的澄清、诊断或修复。

`BUG-0001` 必须继续保持：

- 需求明确状态：待澄清
- 审批状态：待确认
- 实现状态：未实现

进入 3D 建造里程碑前，由用户单独澄清。

## 18. 后续里程碑顺序

1. **默认入口切换**：本里程碑验收后，以独立变更把默认编辑和正式构建入口切到 3D；最终构建移除 2D 场景。
2. **3D 建造与生产**：先单独澄清 `BUG-0001`，再实现 3D 选择、网格预览、内外城放置和最小生产链。
3. **3D 敌人与防御**：实现一种敌人、一种炮塔、伤害、弹药、掉落和防御闭环。
4. **撤离、存档与完整垂直切片**：接入独立 3D 存档适配器，验证 schema 30 二维语义，完成移动→展开→建造→生产→防御→撤离，并执行性能与 Windows 验收。
5. **剩余 3D F1A 交互**：物流观察、前哨、迷雾和正式领袖交互继续只在 3D 基础上实现。

任何后续里程碑都不得删除 2D 规则、稳定 ID、存档兼容测试或冻结回归场景，也不得未经审批开始正式美术生产。
