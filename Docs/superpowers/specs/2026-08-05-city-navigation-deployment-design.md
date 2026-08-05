# IDEA-0001 F1A 城市导航、直接控制与展开合法性设计

## 1. 目标与范围

本规格实现 `IDEA-0001` 在 F1A 的第二个独立基础里程碑：

- 移动态默认由玩家直接控制移动城市；
- 展开态在领袖已招募时默认由玩家直接控制领袖；
- 城市保留 WASD 直接驾驶，并支持在地图上指定目的地自动寻路；
- 基础障碍阻挡城市，恶劣地形降低移动速度；
- 展开前检查面积、障碍和地面稳定性，失败时显示明确原因；
- 自动驾驶目的地和已离城领袖位置进入存档；
- 全部新增表现继续使用程序化 2D 占位。

本里程碑不实现自由拖动镜头、内城网格、前哨、科技强化自动驾驶、展开/收起取消、受击中断，也不修改 `BUG-0001` 的建造菜单打开、可见性、筛选或选择规则。

## 2. 原文与当前实现差异

主 GDD、正式路线图和 `IDEA-0001` 已一致确认：

1. 移动态默认控制城市，展开态默认控制领袖；
2. 城市支持 WASD 与目的地自动寻路；
3. 普通地形可通行，悬崖、深水和大型障碍阻挡，泥地与废墟减速；
4. 展开需要满足面积、坡度和障碍条件。

当前代码没有与这些规则冲突，但只有以下基础：

- `PlaceholderMobileCity` 在 `Mobile` 状态读取 WASD，并直接移动 `Rigidbody2D`；
- `CityDeploymentModel.Toggle()` 不读取地图，任何位置都能进入展开；
- `WorldMapModel` 只有地形和资源，没有通行类型、寻路或展开判断；
- `FormalLeaderController` 的视觉始终贴着城市移动，没有直接控制；
- 存档只记录城市位置与展开状态，不记录自动驾驶和领袖位置。

因此本规格是对批准原文的补充实现，不改变既有需求。

## 3. 方案比较与选择

### 方案 A：只增加展开检查和提示

优点是改动小。缺点是没有自动寻路，障碍也不影响实际移动，无法满足本里程碑的核心规则。

### 方案 B：纯规则与路径模型，加薄运行时适配

新增可单测的通行、路径、展开和直接控制规则；现有城市、领袖、世界视图和存档只负责输入、表现和状态同步。优点是规则可以迁移到后续俯视 3D，测试不依赖物理帧，也不会把玩法真值放进占位表现。

### 方案 C：立即引入 NavMesh、Cinemachine 和 3D 导航代理

优点是接近最终表现。缺点是会提前绑定 F1B 的 3D 技术方案，引入新依赖，并让当前 2D 规则验证变得更难。

采用方案 B。

## 4. 世界通行模型

### 4.1 保留地形与资源兼容性

现有 `TerrainKind` 的数值和生成分布保持不变，避免同一种子旧存档恢复后资源类型或地形改变。障碍使用独立枚举：

```csharp
public enum WorldTraversalKind
{
    Open = 0,
    Ruins = 1,
    DeepWater = 2,
    Cliff = 3
}
```

`WorldCell` 增加不可变的 `Traversal`。生成时使用 `WorldSeed.Sample(x, y, 3)`：

- 已有资源节点强制为 `Open`，避免稳定资源点被障碍覆盖；
- 其余格子中 `0..3` 为 `Cliff`；
- `4..7` 为 `DeepWater`；
- `8..17` 为 `Ruins`；
- 其余为 `Open`。

障碍由世界种子重新生成，不增加存档数组；旧存档和新存档在同一种子下得到相同障碍。

### 4.2 通行与速度

新增纯规则 `CityTerrainRules`：

```csharp
public static bool IsPassable(WorldCell cell);
public static float SpeedMultiplier(WorldCell cell);
public static bool SupportsDeployment(WorldCell cell);
public static string TraversalName(WorldTraversalKind traversal);
```

规则：

| 条件 | 城市通行 | 速度倍率 | 可用于展开面积 |
|---|---:|---:|---:|
| 荒地 / 结晶地且开放 | 是 | `1.0` | 是 |
| 岩地且开放 | 是 | `0.8` | 是 |
| 湿地且开放 | 是 | `0.55` | 否 |
| 废墟 | 是 | `0.65` | 否 |
| 深水 / 悬崖 | 否 | `0` | 否 |

速度倍率读取当前位置的格子。移动候选位置落入地图外、深水或悬崖时拒绝该次位移。

### 4.3 世界坐标转换

`PlaceholderWorldView` 提供唯一坐标入口：

```csharp
public bool TryWorldToCell(Vector2 world, out int x, out int y);
public Vector2 CellToWorld(int x, int y);
public bool IsPassableWorld(Vector2 world);
```

转换继续使用当前格子中心约定：

```text
worldX = cellX - width * 0.5
worldY = cellY - height * 0.5
```

不改变建筑、资源、迷雾或旧存档使用的坐标。

## 5. 自动驾驶

### 5.1 路径模型

新增纯 C# `CityPathfinder` 和值类型 `WorldGridPoint`：

```csharp
public static bool TryFindPath(
    WorldMapModel map,
    int startX,
    int startY,
    int destinationX,
    int destinationY,
    out WorldGridPoint[] path);
```

路径使用四方向网格，不允许斜穿障碍。边成本为目标格 `1 / SpeedMultiplier`，因此在路程相近时优先避开慢速地形。启发值使用曼哈顿距离，地图最大为当前 `32×24`，不增加异步任务或第三方依赖。

以下情况返回 `false` 和空路径：

- 地图或起终点无效；
- 起点或终点不可通行；
- 不存在可达路径。

成功路径不包含起点，包含终点。

### 5.2 城市运行时

`PlaceholderMobileCity` 增加：

```csharp
public bool AutopilotActive { get; }
public int DestinationX { get; }
public int DestinationY { get; }
public float CurrentTerrainMultiplier { get; }
public string LastMobilityMessage { get; }
public bool NavigationReady { get; }

public void ConfigureWorld(PlaceholderWorldView world);
public bool TrySetDestination(Vector2 worldPosition, out string reason);
public bool TrySetDestinationCell(int x, int y, out string reason);
public void RestoreNavigation(bool active, int x, int y);
public bool TryToggleDeployment(out string reason);
```

输入规则：

- 仅 `Mobile` 接受自动驾驶目的地；
- 鼠标右键在地图上设置目的地；
- 有效路径启动自动驾驶并显示目的地；
- WASD 非零输入立即取消自动驾驶并改为直接驾驶；
- 到达终点、进入展开或路径失效时关闭自动驾驶；
- `Deploying`、`Fortress`、`Packing` 状态城市不移动；
- 自动驾驶不替玩家自动展开。

右键不会改变建造菜单或左键放置逻辑。

## 6. 展开合法性

新增纯规则：

```csharp
public enum CityDeploymentFailure
{
    None = 0,
    OutsideWorld = 1,
    Blocked = 2,
    UnstableGround = 3
}

public static class CityDeploymentRules
{
    public static CityDeploymentFailure Validate(
        WorldMapModel map,
        int centerX,
        int centerY,
        int radiusX = 1,
        int radiusY = 1);

    public static string FailureReason(CityDeploymentFailure failure);
}
```

本次用中心周围 `3×3` 格作为城市展开占地代理：

- 任一格超出地图：`OutsideWorld`，显示“展开失败：空间不足”；
- 任一格是深水或悬崖：`Blocked`，显示“展开失败：范围内存在深水或悬崖”；
- 任一格是湿地或废墟：`UnstableGround`，显示“展开失败：地面不稳定或有大型废墟”；
- 全部格合法：允许进入现有 `Deploying` 状态。

岩地和结晶地继续允许展开，以保持资源格展开、采集和采矿主循环。`Fortress` 收起仍使用现有规则，不做地形复检。

`F` 输入改为调用 `TryToggleDeployment`。合法展开会清除自动驾驶；失败不改变 `CityDeploymentModel`。

## 7. 直接控制对象与领袖

新增纯规则：

```csharp
public enum DirectControlTarget
{
    City = 0,
    Leader = 1
}

public static DirectControlTarget Resolve(CityMode mode, bool leaderRecruited);
```

规则：

- `Mobile`、`Deploying`、`Packing`：控制城市；
- `Fortress` 且领袖已招募：控制领袖；
- `Fortress` 但领袖未招募：控制城市占位状态，不允许城市移动。

`FormalLeaderController` 在控制目标为领袖时读取 WASD，以基础速度 `5` 移动领袖视觉；候选位置不能进入地图外、深水或悬崖。其他状态下领袖回到城市内城占位位置。该控制只移动既有稳定 ID 为 `core.character.cen-jin` 的占位对象，不创建新角色系统。

镜头自由拖动、快速返回和跟随控制对象是路线图中的单独交互项，本里程碑不提前实现。

## 8. 存档 schema 30

存档从 schema `29` 升为 `30`，新增：

```csharp
public bool cityAutopilotActive;
public int cityDestinationX = -1;
public int cityDestinationY = -1;
public bool leaderPositionSaved;
public float leaderX;
public float leaderY;
```

保存时：

- 记录当前自动驾驶是否激活及目标格；
- 已招募领袖记录其世界坐标。

恢复时：

- schema `30` 在世界和城市位置恢复后重新计算路径；目标无效或不可达时安全关闭自动驾驶；
- schema `30` 且 `leaderPositionSaved` 恢复领袖位置；
- schema `29` 及更旧关闭自动驾驶，领袖回到城市占位位置；
- 不保存中间路径，避免地图规则变化后恢复过期路径。

不改变建筑位置、资源数量、迷雾、研究或战斗快照结构。

## 9. 占位表现与信息

`PlaceholderWorldView` 使用现有白色方块创建稳定、可替换的障碍占位：

- `world.obstacle.ruins`
- `world.obstacle.deep-water`
- `world.obstacle.cliff`

障碍颜色仅用于当前 2D 灰盒：

- 废墟：暗灰；
- 深水：深蓝；
- 悬崖：近黑棕。

HUD 增加：

- 当前直接控制对象；
- 当前地形速度倍率；
- 自动驾驶目标或最近一次展开失败原因；
- “WASD 直接驾驶 / 右键自动驾驶 / F 展开或收起”的提示。

所有规则来自模型，不从颜色、Sprite 或 `VisualSlot` 反推。

## 10. 测试与验收

EditMode 测试覆盖：

1. 旧地形数值与资源生成保持兼容，资源节点不生成障碍；
2. 深水/悬崖阻挡，湿地/废墟减速；
3. 路径绕开障碍、拒绝不可达目标，并在可选时偏好更快地形；
4. 展开 `3×3` 面积的越界、障碍、不稳定地面与合法资源地形；
5. 四个城市状态和领袖招募状态对应正确直接控制对象；
6. 城市 WASD 接管取消自动驾驶，合法/非法目的地返回稳定原因；
7. schema `30` 往返自动驾驶和领袖位置，schema `29` 使用安全默认值。

PlayMode 测试覆盖：

1. 正式场景生成障碍占位并保留稳定 `VisualSlot`；
2. 正式场景城市能够接收可达目的地并沿路径移动；
3. 非法展开不改变城市模式，合法位置进入 `Deploying`；
4. 展开且已招募时直接控制对象为领袖。

完成前运行全量 EditMode、PlayMode、无界面编译和 Windows 64 位构建。真实 Windows 10/11 独立程序冒烟继续保留为待验证门。
