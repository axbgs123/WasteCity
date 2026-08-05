# IDEA-0001 镜头跟随、自由拖动与快速返回设计

## 1. 目标与范围

本规格实现 `IDEA-0001` 的独立镜头控制里程碑：

- 镜头默认精确跟随当前直接控制对象；
- 鼠标中键拖动可暂时脱离目标，自由查看地图；
- `Home` 可在同一帧恢复跟随并立即对准当前控制对象；
- 当前直接控制对象切换时，无条件恢复跟随并立即对准新目标；
- 暂停时继续接受中键拖动与 `Home`；
- 全部实现继续使用现有 2D 占位场景。

本里程碑不实现平滑、惯性、震动、过渡动画、缩放、边缘滚动、镜头边界、Cinemachine、3D、暂停或倍速改造、内城放置、前哨、自动驾驶科技升级，也不迁移全项目输入系统。

本里程碑不保存镜头位置或自由拖动状态，存档 schema 保持 `30`。不得修改 `BUG-0001`、建造菜单的文字、显示、筛选、选择或放置逻辑。

## 2. 现有规则复用

现有 `FormalLeaderController.ControlTarget` 是直接控制对象的唯一真值，其内部已经通过 `DirectControlRules.Resolve` 得到：

| 城市/领袖状态 | `DirectControlTarget` | 镜头目标 |
|---|---|---|
| `Mobile` | `City` | 城市 |
| `Deploying` | `City` | 城市 |
| `Packing` | `City` | 城市 |
| `Fortress` 且领袖未招募 | `City` | 城市 |
| `Fortress` 且领袖已招募 | `Leader` | 领袖 |

镜头适配器只读取 `ControlTarget`，不得复制城市模式与招募状态的直接控制决策，也不得修改 `DirectControlRules`、`PlaceholderMobileCity` 或 `FormalLeaderController` 的玩法规则。

当 `FormalLeaderController` 引用、领袖目标 Transform 或目标 GameObject 不可用时，镜头把有效目标解析为城市。城市引用不可用时保持当前镜头位置；任何缺失引用都不得把镜头移动到世界原点。

## 3. 方案

采用“纯状态/规则模型 + 薄 Unity 适配器”：

- `CameraFollowModel` 只持有 `Following` / `Free` 状态和当前有效的 `DirectControlTarget`；
- `FormalCameraController` 读取 Unity 输入、引用与 Transform，把输入事件翻译给模型，并把模型结果应用到 `Main Camera`；
- 正式场景手工挂接 `FormalCameraController`，不运行会重建场景的 `FormalProjectSetup.Configure`。

不采用只在 MonoBehaviour 内堆叠布尔值的方案，以便纯状态转换可在 EditMode 中完整验证。也不引入 Cinemachine 或新包，避免扩大依赖与 3D 技术范围。

## 4. 纯状态模型

新增：

```csharp
public enum CameraFollowMode
{
    Following = 0,
    Free = 1
}

public sealed class CameraFollowModel
{
    public CameraFollowMode Mode { get; }
    public DirectControlTarget Target { get; }

    public void BeginFreeDrag();
    public void EndFreeDrag();
    public bool ObserveTarget(
        DirectControlTarget requestedTarget,
        bool leaderTargetAvailable);
    public void ReturnToTarget();
}
```

状态规则：

1. 新模型为 `Following`，有效目标为 `City`；
2. `BeginFreeDrag` 进入 `Free`；
3. `EndFreeDrag` 保持 `Free`；
4. `ReturnToTarget` 进入 `Following`；
5. 请求领袖但领袖目标不可用时，有效目标为 `City`；
6. 有效目标发生变化时，返回 `true`，并无条件进入 `Following`；
7. 有效目标未变化时不改变当前 `Following` / `Free` 状态。

模型不持有 Unity 对象、镜头坐标、时间、输入设备或存档数据。

## 5. Unity 适配器

新增 `FormalCameraController`，挂在正式场景 `Main Camera` 上，序列化引用：

```csharp
Camera controlledCamera;
PlaceholderMobileCity city;
FormalLeaderController leader;
Transform leaderTarget;
```

适配器公开与运行时输入共用的窄接口，供确定性测试调用：

```csharp
public CameraFollowMode Mode { get; }
public DirectControlTarget CurrentTarget { get; }
public bool ReferencesReady { get; }
public void BeginFreeDrag();
public void EndFreeDrag();
public void ApplyPointerDelta(Vector2 screenDelta, float screenHeight);
public void ReturnToTarget();
public void TickCamera();
```

每帧流程：

1. `Update` 读取 `Mouse.current` 与 `Keyboard.current`；
2. 中键按下调用 `BeginFreeDrag`；
3. 中键保持时读取鼠标屏幕像素增量并调用 `ApplyPointerDelta`；
4. 中键松开调用 `EndFreeDrag`，状态继续为 `Free`；
5. `Home.wasPressedThisFrame` 调用 `ReturnToTarget`，同一帧立即对准；
6. `LateUpdate` 调用 `TickCamera`，观察 `leader.ControlTarget`；
7. 有效目标切换时恢复 `Following` 并立即对准；
8. 处于 `Following` 时每帧精确对准当前有效目标。

输入和镜头位置更新不读取 `Time.deltaTime`、`Time.unscaledDeltaTime` 或 `Time.timeScale`。

## 6. 坐标与拖动

跟随只复制目标的世界 X/Y，保留镜头当前 Z：

```text
camera = (target.x, target.y, camera.z)
```

自由拖动采用“抓住地图”方向。正交镜头下，每屏高度对应 `2 * orthographicSize` 个世界单位：

```text
worldUnitsPerPixel = 2 * orthographicSize / screenHeight
cameraDelta = -mouseScreenDelta * worldUnitsPerPixel
```

`screenHeight <= 0`、没有受控镜头或屏幕增量为零时安全忽略。拖动只改 X/Y，保留 Z。首版不做透视镜头换算，因为正式场景 `Main Camera` 是正交 2D 镜头。

## 7. 快速返回与目标切换

`ReturnToTarget` 不等待下一帧：

1. 重新读取 `FormalLeaderController.ControlTarget`；
2. 解析领袖目标是否可用；
3. 模型进入 `Following`；
4. 立即把镜头 X/Y 对准有效目标。

`TickCamera` 每帧观察有效目标。若城市/领袖控制对象发生切换，即使镜头此前处于 `Free`，也立即恢复 `Following` 并对准新目标。

领袖不可用的降级不是控制规则切换：镜头只把当前有效跟随对象安全解析为城市；领袖目标重新可用且 `ControlTarget` 仍为 `Leader` 时，有效目标变为领袖，因此恢复 `Following` 并对准领袖。

## 8. 场景接线与表现边界

只修改 `Assets/_Game/Scenes/FormalPrototype.unity` 的现有 `Main Camera`：

- 添加一个 `FormalCameraController`；
- 引用现有 `Camera`、`PlaceholderMobileCity`、`FormalLeaderController` 和领袖视觉 Transform；
- 不重建场景、不运行 `FormalProjectSetup.Configure`；
- 不添加美术资源、包或纯表现玩法真值。

本里程碑不要求修改 HUD。若后续单独批准提示文字，只能修改镜头帮助提示，不得触碰建造菜单文字。

## 9. 测试与验收

### EditMode 纯规则

- 默认 `Following`；
- 中键拖动事件进入 `Free`；
- 释放后仍为 `Free`；
- `Home` 语义恢复 `Following`；
- 有效目标切换自动恢复 `Following`；
- 城市/领袖目标选择；
- 领袖不可用安全回退城市。

### EditMode Unity 适配器

- 跟随只改 X/Y、保持 Z；
- 中键拖动方向与鼠标相反；
- `Time.timeScale == 0` 时拖动与快速返回仍有效；
- 缺领袖引用或领袖目标不可用时跟随城市，不回原点；
- `Home` 同一调用内立即对准；
- 直接控制目标切换时从 `Free` 恢复并立即对准。

### 场景与 PlayMode

- 正式场景接线完整；
- `Mobile` 跟随城市；
- `Fortress + 已招募` 跟随领袖；
- `Free` 后松开不吸回；
- `Home` 返回；
- 直接控制目标切换恢复跟随；
- 镜头操作不改变城市自动驾驶或部署状态。

### 完整验收门

- 完整 EditMode；
- 完整 PlayMode；
- 无界面脚本编译；
- Windows Mono x86-64 构建；
- `file Builds/Windows/WasteCity.exe`；
- `git diff --check`；
- 相对 `7d6a502` 的 Building 目录、指定建造控制器、`BUG-0001` 与三个受保护文件零差异；
- 真实 Windows 10/11 独立运行冒烟继续明确标记为待补。
