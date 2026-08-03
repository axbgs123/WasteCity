# 《废土移动城市》Demo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 14 天内从零制作一段可公开试玩的 Windows PC 纵向切片，首次体验 30-40 分钟，完整覆盖移动、展开、生产、防守、关注度、命轨、领袖、Boss 与文明升阶。

**Architecture:** Unity 2022.3.62f1，使用小型、职责单一的 MonoBehaviour 适配场景，核心规则尽量写成普通 C# 类并用 EditMode 测试。内容通过 ScriptableObject 配置，跨系统使用明确接口和领域事件；首版只抽象资源、建筑、敌人、科技、命轨、角色和事件等已有调用者，不提前实现多城市、跨星球、外交或政治架构。

**Tech Stack:** Unity 2022.3.62f1、C#、Unity Input System、Unity Test Framework/NUnit、TextMeshPro、URP 2D Renderer、Git、Git LFS。

## Global Constraints

- 当前版本唯一需求源：`游戏设计文档.md` 的 A 卷；B 卷禁止实现，除非任务明确引用。
- 目标平台：Windows PC；主要验收分辨率 1920×1080；键盘与鼠标。
- 目标性能：60 FPS；高压战斗 1% 低帧不低于 45 FPS；约 150 敌人、500 活跃弹丸。
- 内容必须数据驱动；稳定 ID 不使用显示名称；首版不写完整多城、外交、政治、跨星球抽象。
- 稳定 ID 必须使用至少三段的小写命名空间格式，例如 `core.resource.iron`；核心命名空间只允许官方内容。
- 所有玩家可见文字通过 `TextKey` 和本地化表读取；首版只提供简体中文，不在规则代码中写显示文本。
- 首版只实现 Mod-ready 内容基础，不扫描外部 Mods 目录，不加载外部脚本或 DLL。
- 所有任务遵循测试先行；纯规则使用 EditMode 测试，场景联动使用 PlayMode 测试。
- 每项功能同时验证编辑器和打包版本；第 10 天功能冻结。
- AI 不得自行增加资源、建筑、敌人、命轨、系统或第三方运行时依赖。
- 不使用来源或商用许可不清晰的美术、音乐、音效和字体。

---

## 0. 执行环境与命令

项目目标路径：

```bash
export WASTE_CITY_ROOT='/Users/baiyan1/Library/Containers/com.tencent.xinWeChat/Data/Documents/xwechat_files/wxid_m0pfm40gl5ek22_7fcc/msg/file/2026-07/废土城市'
export WASTE_CITY_PROJECT="$WASTE_CITY_ROOT/WasteCityDemo"
export WASTE_CITY_RESULTS="$WASTE_CITY_ROOT/TestResults"
export WASTE_CITY_UNITY="$(find /Applications/Unity/Hub/Editor -path '*/2022.3.*/*/Unity.app/Contents/MacOS/Unity' -type f 2>/dev/null | sort | tail -1)"
```

验证 Unity：

```bash
test -x "$WASTE_CITY_UNITY" && "$WASTE_CITY_UNITY" -version
```

EditMode 测试：

```bash
"$WASTE_CITY_UNITY" -batchmode -nographics -projectPath "$WASTE_CITY_PROJECT" -runTests -testPlatform EditMode -testResults "$WASTE_CITY_RESULTS/editmode.xml" -quit
```

PlayMode 测试：

```bash
"$WASTE_CITY_UNITY" -batchmode -nographics -projectPath "$WASTE_CITY_PROJECT" -runTests -testPlatform PlayMode -testResults "$WASTE_CITY_RESULTS/playmode.xml" -quit
```

Windows 构建通过 Unity Editor 的 `BuildTools.BuildWindows` 执行：

```bash
"$WASTE_CITY_UNITY" -batchmode -nographics -projectPath "$WASTE_CITY_PROJECT" -executeMethod WasteCity.Editor.BuildTools.BuildWindows -quit
```

---

## 1. 项目文件结构

```text
WasteCityDemo/
├── Assets/_Project/
│   ├── Art/
│   ├── Audio/
│   ├── Data/
│   │   ├── Resources/
│   │   ├── Buildings/
│   │   ├── Enemies/
│   │   ├── Tech/
│   │   ├── Fates/
│   │   ├── Waves/
│   │   └── Events/
│   ├── Prefabs/
│   ├── Scenes/
│   │   ├── Bootstrap.unity
│   │   └── Demo.unity
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── Content/
│   │   ├── Economy/
│   │   ├── City/
│   │   ├── World/
│   │   ├── Combat/
│   │   ├── Progression/
│   │   ├── Narrative/
│   │   ├── Localization/
│   │   ├── Persistence/
│   │   └── UI/
│   ├── Localization/
│   │   └── zh-CN.csv
│   ├── Docs/
│   │   └── ArchitectureDecisions.md
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
├── Packages/
├── ProjectSettings/
└── README.md
```

职责边界：

- `Core`：稳定 ID、游戏时钟、领域事件、启动和生命周期；
- `Content`：唯一内容注册表、来源、类型和启动校验；
- `Economy`：资源、库存、生产配方、物流访问；
- `City`：移动、展开状态、生产力、建造与回收；
- `World`：网格、迷雾、矿脉、遭遇点和地图查询；
- `Combat`：生命、伤害、索敌、弹丸、敌人、波次和 Boss；
- `Progression`：关注度、科技、命轨、人口、领袖与文明升阶；
- `Narrative`：事件条件、选择、六幕流程和对话；
- `Localization`：TextKey、简体中文表和缺失回退；
- `Persistence`：存档、检查点、版本迁移；
- `UI`：只读取公开状态并发送玩家命令，不保存核心规则。

---

## 2. 公共接口合同

后续任务只能使用以下稳定名称；需要修改时先更新本节和所有调用任务。

```csharp
using System;
using System.Text.RegularExpressions;

public readonly struct StableId : IEquatable<StableId>
{
    public string Value { get; }

    private static readonly Regex Pattern =
        new Regex(@"^[a-z0-9_]+(?:\.[a-z0-9_]+){2,}$", RegexOptions.Compiled);

    public StableId(string value) => Value = value;

    public static bool TryParse(string raw, out StableId id)
    {
        var valid = !string.IsNullOrWhiteSpace(raw) && Pattern.IsMatch(raw);
        id = valid ? new StableId(raw) : default;
        return valid;
    }

    public bool Equals(StableId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj) =>
        obj is StableId other && Equals(other);

    public override int GetHashCode() =>
        Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value ?? string.Empty;
}

public readonly struct TextKey
{
    public string Value { get; }
    public TextKey(string value) => Value = value;
}

public readonly struct ResourceAmount
{
    public StableId ResourceId { get; }
    public int Amount { get; }
    public ResourceAmount(StableId resourceId, int amount)
    {
        ResourceId = resourceId;
        Amount = amount;
    }
}

public readonly struct ContentSource
{
    public StableId SourceId { get; }
    public string Version { get; }
    public ContentSource(StableId sourceId, string version)
    {
        SourceId = sourceId;
        Version = version;
    }
}

public interface IContentCatalog
{
    bool TryGet<TDefinition>(StableId id, out TDefinition definition);
    IReadOnlyList<string> Validate();
}

public interface ILocalizationService
{
    string Get(TextKey key);
}

public interface IInventory
{
    int Get(StableId resourceId);
    bool CanAfford(IReadOnlyList<ResourceAmount> cost);
    bool TrySpend(IReadOnlyList<ResourceAmount> cost);
    int Add(StableId resourceId, int amount);
}

public interface IGameClock
{
    float DeltaTime { get; }
    bool IsPaused { get; }
    float Speed { get; }
    void SetSpeed(float speed);
}

public interface ICityProductivity
{
    int EffectivePopulation { get; }
    float Multiplier { get; }
    float AdjustDuration(float baseSeconds);
}

public enum CityMode { Mobile, Deploying, Deployed, Packing, Destroyed }

public interface IAttentionService
{
    int Value { get; }
    bool Change(int delta, StableId sourceId, string reason);
    IReadOnlyList<AttentionChange> RecentChanges { get; }
}

public interface IResearchService
{
    StableId? ActiveTechId { get; }
    bool IsCompleted(StableId techId);
    bool TryStart(StableId techId);
    void Tick(float deltaTime);
}

public interface ISaveGameService
{
    void SaveCheckpoint(StableId checkpointId);
    bool LoadLatestCheckpoint();
    void SaveOnExit();
}
```

领域事件使用 Unity 2022.3 兼容的只读结构，每个结构提供同名只读属性和显式构造函数：

```csharp
public readonly struct BuildingCompleted
{
    public StableId BuildingId { get; }
    public Vector2Int Cell { get; }
    public BuildingCompleted(StableId buildingId, Vector2Int cell)
    {
        BuildingId = buildingId;
        Cell = cell;
    }
}

public readonly struct ProductionBlocked
{
    public StableId BuildingId { get; }
    public StableId ReasonId { get; }
    public ProductionBlocked(StableId buildingId, StableId reasonId)
    {
        BuildingId = buildingId;
        ReasonId = reasonId;
    }
}

public readonly struct AttentionThresholdReached
{
    public int Threshold { get; }
    public AttentionThresholdReached(int threshold) => Threshold = threshold;
}

public readonly struct WaveCompleted
{
    public StableId WaveId { get; }
    public WaveCompleted(StableId waveId) => WaveId = waveId;
}

public readonly struct TechCompleted
{
    public StableId TechId { get; }
    public TechCompleted(StableId techId) => TechId = techId;
}

public readonly struct DemoActChanged
{
    public int ActIndex { get; }
    public DemoActChanged(int actIndex) => ActIndex = actIndex;
}
```

---

## Task 1: Unity 工程、Git 与自动化测试骨架（第 1 天）

**Files:**
- Create: `WasteCityDemo/Assets/_Project/Scripts/Core/WasteCity.Core.asmdef`
- Create: `WasteCityDemo/Assets/_Project/Tests/EditMode/WasteCity.EditModeTests.asmdef`
- Create: `WasteCityDemo/Assets/_Project/Tests/EditMode/SmokeTests.cs`
- Create: `WasteCityDemo/Assets/_Project/Scenes/Bootstrap.unity`
- Create: `WasteCityDemo/Assets/_Project/Scenes/Demo.unity`
- Create: `WasteCityDemo/Assets/_Project/Scripts/Core/GameBootstrap.cs`
- Create: `WasteCityDemo/Assets/_Project/Editor/BuildTools.cs`
- Create: `WasteCityDemo/.gitignore`
- Create: `WasteCityDemo/.gitattributes`
- Create: `WasteCityDemo/README.md`
- Create: `WasteCityDemo/Assets/_Project/Docs/ArchitectureDecisions.md`

**Interfaces:**
- Produces: 可运行 Unity 工程、EditMode/PlayMode 命令、Windows 构建命令。

- [x] **Step 1: 创建 Unity 2022.3.62f1 2D URP 工程**

通过 Unity Hub 在 `$WASTE_CITY_PROJECT` 创建 2D URP 工程，打开后保存 `Bootstrap.unity` 和 `Demo.unity`。

- [ ] **Step 2: 写第一个失败的 EditMode 测试**

```csharp
[Test]
public void Project_UsesExpectedProductName()
{
    Assert.That(Application.productName, Is.EqualTo("废土移动城市 Demo"));
}
```

- [ ] **Step 3: 运行测试并确认失败**

运行 EditMode 命令。预期：产品名不是“废土移动城市 Demo”。

- [ ] **Step 4: 设置 PlayerSettings 与构建入口**

`BuildTools.BuildWindows()` 固定构建 `Bootstrap`、`Demo` 两个场景到：

```text
Builds/Windows/WasteCityDemo.exe
```

设置公司名 `Baiyan Indie`、产品名 `废土移动城市 Demo`、默认 1920×1080、允许窗口与全屏。

- [ ] **Step 5: 再次运行 EditMode 测试**

预期：`SmokeTests.Project_UsesExpectedProductName` 通过。

- [ ] **Step 6: 初始化 Git 与 LFS**

跟踪 Unity 必要目录；忽略 `Library/`、`Temp/`、`Logs/`、`Builds/`、`TestResults/`。LFS 跟踪 `*.psd`、`*.wav`、`*.mp3`、`*.mp4`。

- [ ] **Step 7: 创建首个提交**

```bash
git add .
git commit -m "chore: initialize Waste City Unity project"
```

---

## Task 2: 内容注册、稳定 ID、本地化与配置校验（第 1 天）

**Files:**
- Create: `Scripts/Content/StableId.cs`
- Create: `Scripts/Content/ContentSource.cs`
- Create: `Scripts/Content/ContentCatalog.cs`
- Create: `Scripts/Localization/TextKey.cs`
- Create: `Scripts/Localization/LocalizationService.cs`
- Create: `Localization/zh-CN.csv`
- Create: `Scripts/Content/GameIds.cs`
- Create: `Scripts/Economy/ResourceDefinition.cs`
- Create: `Scripts/Economy/RecipeDefinition.cs`
- Create: `Scripts/City/BuildingDefinition.cs`
- Create: `Scripts/Combat/EnemyDefinition.cs`
- Create: `Scripts/Progression/TechDefinition.cs`
- Create: `Scripts/Progression/FateDefinition.cs`
- Create: `Scripts/Combat/WaveDefinition.cs`
- Create: `Scripts/Narrative/GameEventDefinition.cs`
- Test: `Tests/EditMode/StableIdTests.cs`
- Test: `Tests/EditMode/ContentCatalogTests.cs`
- Test: `Tests/EditMode/LocalizationServiceTests.cs`

**Interfaces:**
- Produces: `StableId`、`TextKey`、`IContentCatalog`、`ILocalizationService`、全部 ScriptableObject 定义。

- [ ] **Step 1: 写命名空间 ID 测试**

```csharp
[Test]
public void TryParse_AcceptsCoreThreePartId()
{
    Assert.That(StableId.TryParse("core.resource.iron", out _), Is.True);
}

[TestCase("iron")]
[TestCase("core.iron")]
[TestCase("Core.Resource.Iron")]
[TestCase("core.resource.iron!")]
public void TryParse_RejectsInvalidIds(string raw)
{
    Assert.That(StableId.TryParse(raw, out _), Is.False);
}
```

- [ ] **Step 2: 写内容冲突、未知引用和本地化回退测试**

```csharp
[Test]
public void Validate_ReturnsError_WhenTwoDefinitionsShareId() { }

[Test]
public void Validate_ReturnsError_WhenRecipeReferencesUnknownResource() { }

[Test]
public void Get_ReturnsMissingMarker_WhenChineseKeyDoesNotExist() { }
```

- [ ] **Step 3: 运行并确认测试因类型不存在而失败**

运行 EditMode 测试，预期编译失败或测试失败。

- [ ] **Step 4: 实现内容与文本合同**

每个定义包含序列化 `StableId id`、`TextKey nameKey`、`TextKey descriptionKey` 和对应配置。`ContentCatalog.Validate()` 返回 `IReadOnlyList<string>`，检查 ID 格式、空 ID、重复 ID、未知引用、循环依赖、负数数值和缺失中文 Key。开发构建缺失文本显示 `[missing:key]`。

- [ ] **Step 5: 创建首版配置资产和简体中文表**

创建 4 资源、9 建筑、5 敌人、6 科技、3 命轨、4 波次和主线事件资产。全部核心 ID 以 `core.` 开头，文本只存 Key，不在资产中重复中文名称。

- [ ] **Step 6: 运行全部 EditMode 测试**

预期：配置校验通过，错误夹具能返回准确错误。

- [ ] **Step 7: 记录架构决策并提交**

在 `ArchitectureDecisions.md` 记录 ADR-001：选择单一 `ContentCatalog`、命名空间 ID、TextKey，以及首版不加载外部 Mod 的原因。

```bash
git add Assets/_Project
git commit -m "feat: add namespaced content and localization catalog"
```

---

## Task 3: 游戏时钟、事件与启动顺序（第 1 天）

**Files:**
- Create: `Scripts/Core/GameClock.cs`
- Create: `Scripts/Core/GameEventBus.cs`
- Modify: `Scripts/Core/GameBootstrap.cs`
- Test: `Tests/EditMode/GameClockTests.cs`
- Test: `Tests/EditMode/GameEventBusTests.cs`

**Interfaces:**
- Produces: `IGameClock`、类型安全 `GameEventBus.Publish<T>()/Subscribe<T>()`.

- [ ] **Step 1: 写暂停、倍速和取消订阅测试**

```csharp
[TestCase(0f, 0f)]
[TestCase(1f, 0.5f)]
[TestCase(2f, 1f)]
public void DeltaTime_UsesConfiguredSpeed(float speed, float expected) { }
```

- [ ] **Step 2: 运行测试并确认失败**

- [ ] **Step 3: 实现时钟与事件总线**

速度只允许 `0、1、2`；订阅返回 `IDisposable`，场景卸载时释放。

- [ ] **Step 4: Bootstrap 按固定顺序初始化**

```text
Content → Localization → Clock → EventBus → Save → Demo Scene
```

初始化失败显示阻断错误面板，不静默继续。

- [ ] **Step 5: 运行测试并提交**

```bash
git add Assets/_Project
git commit -m "feat: add game clock and domain events"
```

---

## Task 4: 库存、生产力与人口（第 2-3 天）

**Files:**
- Create: `Scripts/Economy/Inventory.cs`
- Create: `Scripts/Progression/PopulationService.cs`
- Create: `Scripts/City/CityProductivity.cs`
- Test: `Tests/EditMode/InventoryTests.cs`
- Test: `Tests/EditMode/CityProductivityTests.cs`

**Interfaces:**
- Produces: `IInventory`、`ICityProductivity`、人口容量和等待人口。

- [ ] **Step 1: 写库存原子扣款测试**

```csharp
[Test]
public void TrySpend_DoesNotChangeAnyResource_WhenOneCostIsMissing() { }
```

- [ ] **Step 2: 写生产力边界测试**

```csharp
[TestCase(100, 1.00f)]
[TestCase(140, 1.20f)]
[TestCase(160, 1.30f)]
[TestCase(400, 2.50f)]
[TestCase(500, 2.50f)]
public void Multiplier_FollowsPopulationFormula(int population, float expected) { }
```

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现库存、容量和生产力**

生产力公式严格使用 `Clamp(0.5f + effectivePopulation * 0.005f, 0f, 2.5f)`。超容量人口不计入 `EffectivePopulation`。

- [ ] **Step 5: 运行测试并提交**

```bash
git add Assets/_Project
git commit -m "feat: add inventory and population productivity"
```

---

## Task 5: 地图、相机、迷雾与移动城市（第 2 天）

**Files:**
- Create: `Scripts/World/WorldGrid.cs`
- Create: `Scripts/World/WorldNode.cs`
- Create: `Scripts/World/FogOfWarController.cs`
- Create: `Scripts/World/CameraController.cs`
- Create: `Scripts/City/CityController.cs`
- Create: `Scripts/City/CityStateMachine.cs`
- Create: `Scripts/City/CityInputController.cs`
- Test: `Tests/EditMode/CityStateMachineTests.cs`
- Test: `Tests/PlayMode/CityMovementPlayModeTests.cs`

**Interfaces:**
- Consumes: `IGameClock`、`ICityProductivity`.
- Produces: `CityMode`、`TryDeploy()`、`TryPack()`、城市位置事件。

- [ ] **Step 1: 写非法状态转换测试**

```csharp
[Test]
public void TryDeploy_ReturnsFalse_WhenAlreadyDeploying() { }

[Test]
public void PackDuration_UsesProductivity_AndDamagePenalty() { }
```

- [ ] **Step 2: 运行并确认失败**

- [ ] **Step 3: 实现 96×64 世界和城市状态**

移动速度 3 格/秒，展开 5 秒，收起 8 秒；受击收起时间除生产力后再乘 `1 / 0.7`。

- [ ] **Step 4: 实现场景输入和相机**

WASD、鼠标中键拖动、滚轮、X、Space、1/2；UI 获得焦点时阻止世界点击。

- [ ] **Step 5: PlayMode 验证**

预期：移动、展开、收起、受击惩罚、迷雾揭示均通过。

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add world navigation and mobile city states"
```

---

## Task 6: 建造、回收、物流与生产链（第 3 天）

**Files:**
- Create: `Scripts/City/ConstructionService.cs`
- Create: `Scripts/City/BuildGrid.cs`
- Create: `Scripts/City/BuildingInstance.cs`
- Create: `Scripts/Economy/LogisticsNetwork.cs`
- Create: `Scripts/Economy/ProductionBuilding.cs`
- Create: `Scripts/Economy/MineNode.cs`
- Test: `Tests/EditMode/ConstructionServiceTests.cs`
- Test: `Tests/EditMode/ProductionBuildingTests.cs`
- Test: `Tests/PlayMode/ProductionChainPlayModeTests.cs`

**Interfaces:**
- Consumes: `IInventory`、`ICityProductivity`、`BuildingDefinition`.
- Produces: `TryQueueBuild()`、`TryCancel()`、`TryRecycle()`、`ProductionBlocked`.

- [ ] **Step 1: 写建造原子扣款和取消返还测试**

- [ ] **Step 2: 写 2 矿→2 冶炼→1 装配的 60 秒产量测试**

预期稳定状态下每分钟生产 20 弹药；输入不足时发布准确 `ProductionBlocked`。

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现单施工队列和物流范围**

施工开始扣款；取消 100%；非战斗拆除 80%；战斗拆除 60% 且基础 5 秒。范围外建筑保留内部库存。

- [ ] **Step 5: 实现矿脉储量**

安全矿 240、裂谷矿 480；枯竭后停机并发布 `mine_depleted` 原因。

- [ ] **Step 6: PlayMode 验证完整生产链**

- [ ] **Step 7: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add construction logistics and production"
```

---

## Task 7: 生命、伤害、碾压、弹丸与炮塔（第 4 天）

**Files:**
- Create: `Scripts/Combat/Health.cs`
- Create: `Scripts/Combat/DamagePacket.cs`
- Create: `Scripts/Combat/TargetingService.cs`
- Create: `Scripts/Combat/ProjectilePool.cs`
- Create: `Scripts/Combat/TowerController.cs`
- Create: `Scripts/Combat/CityCrushDamage.cs`
- Test: `Tests/EditMode/HealthTests.cs`
- Test: `Tests/EditMode/TargetingServiceTests.cs`
- Test: `Tests/PlayMode/TowerCombatPlayModeTests.cs`

**Interfaces:**
- Produces: `ApplyDamage(DamagePacket)`、`Died`、对象池 `Rent/Return`.

- [ ] **Step 1: 写重甲减伤与死亡只触发一次测试**

- [ ] **Step 2: 写塔无弹停火、补弹恢复测试**

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现最小战斗规则**

无甲、重甲、特殊三标签；机枪塔 20 DPS、每 3 秒消耗 1 弹药；城市碾压 100 DPS；核心自卫武器 8 DPS 且不耗弹。

- [ ] **Step 5: PlayMode 压力测试**

生成 150 个占位敌人和 500 个弹丸，记录帧率与池扩容次数；运行中不得实例化超过池预热预算。

- [ ] **Step 6: 第 4 天核心门**

连续玩 20 分钟，记录 A17.2 五项答案。三项以上失败则停止新增功能并修核心闭环。

- [ ] **Step 7: 提交**

```bash
git add Assets/_Project
git commit -m "feat: complete playable city production combat loop"
```

---

## Task 8: 敌人 AI、波次与关注度（第 5 天）

**Files:**
- Create: `Scripts/Combat/EnemyAgent.cs`
- Create: `Scripts/Combat/EnemyTargetPolicy.cs`
- Create: `Scripts/Combat/WaveDirector.cs`
- Create: `Scripts/Progression/AttentionService.cs`
- Test: `Tests/EditMode/AttentionServiceTests.cs`
- Test: `Tests/EditMode/WaveDirectorTests.cs`
- Test: `Tests/PlayMode/WaveSequencePlayModeTests.cs`

**Interfaces:**
- Consumes: `IAttentionService`、`WaveDefinition`.
- Produces: 阈值事件、波次预警、`WaveCompleted`.

- [ ] **Step 1: 写阈值只触发一次和最近三条记录测试**

- [ ] **Step 2: 写波次依赖测试**

定向攻击必须等待教学波完成和首塔建成；高危攻击必须等待定向攻击完成。

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现三类目标策略**

啃噬者最近目标；晶壳兽优先城墙；啸叫者保持 7 格并优先生产建筑；掘地者跳过首层阻挡。

- [ ] **Step 5: 配置四波内容**

严格使用 A16.7 的数量、预警和分批时间。

- [ ] **Step 6: PlayMode 验证阈值和波次不重叠**

- [ ] **Step 7: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add attention-driven enemy waves"
```

---

## Task 9: 科技、人口事件与文明升阶（第 6 天）

**Files:**
- Create: `Scripts/Progression/ResearchService.cs`
- Create: `Scripts/Progression/CivilizationService.cs`
- Create: `Scripts/Progression/PopulationEventService.cs`
- Test: `Tests/EditMode/ResearchServiceTests.cs`
- Test: `Tests/EditMode/CivilizationServiceTests.cs`

**Interfaces:**
- Consumes: `IInventory`、`TechDefinition`、`IAttentionService`.
- Produces: `IResearchService`、`CanAscend()`、`Ascend()`.

- [ ] **Step 1: 写研究扣款、取消返还 80% 和单队列测试**

- [ ] **Step 2: 写升阶四条件必须同时满足测试**

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现六项科技和人口容量**

使用 A16.4 数值；人口超容量进入等待，不贡献生产力。

- [ ] **Step 5: 实现升阶**

完成遗产解析、两塔、Boss 击败、至少一座生产建筑运行。升阶发布事件并使关注度 +25。

- [ ] **Step 6: 运行测试并提交**

```bash
git add Assets/_Project
git commit -m "feat: add research population and civilization ascension"
```

---

## Task 10: 三条命轨（第 8 天）

**Files:**
- Create: `Scripts/Progression/FateService.cs`
- Create: `Scripts/Progression/Effects/FirstOfTypeProductionEffect.cs`
- Create: `Scripts/Progression/Effects/VoidDebtEffect.cs`
- Create: `Scripts/Progression/Effects/RewindAnchorEffect.cs`
- Test: `Tests/EditMode/FateServiceTests.cs`
- Test: `Tests/PlayMode/FateEffectsPlayModeTests.cs`

**Interfaces:**
- Produces: `SelectFate(StableId)`、规则效果订阅和命轨等级。

- [ ] **Step 1: 写只能选择一次和未选择不可生效测试**

- [ ] **Step 2: 分别写三条命轨规则测试**

袖珍宇宙只强化每类首座；虚空债按每 10 债务/30 秒增加关注；回溯后关注度不回退并 +12。

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现效果为独立策略**

禁止在 `ConstructionService`、`ProductionBuilding` 或 `SaveGameService` 中写命轨名称分支。

- [ ] **Step 5: PlayMode 分别完成核心闭环**

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add three rule-changing fate paths"
```

---

## Task 11: 通用事件、六幕导演与目标提示（第 7 天）

**Files:**
- Create: `Scripts/Narrative/EventCondition.cs`
- Create: `Scripts/Narrative/EventOutcome.cs`
- Create: `Scripts/Narrative/GameEventRunner.cs`
- Create: `Scripts/Narrative/DemoActDirector.cs`
- Create: `Scripts/Narrative/ObjectiveService.cs`
- Test: `Tests/EditMode/GameEventRunnerTests.cs`
- Test: `Tests/PlayMode/DemoFlowPlayModeTests.cs`

**Interfaces:**
- Consumes: 资源、建筑、科技、击杀、位置、关注度、人口、前置事件。
- Produces: `DemoActChanged`、当前目标、选择结果。

- [ ] **Step 1: 写组合条件与事件幂等测试**

- [ ] **Step 2: 运行并确认失败**

- [ ] **Step 3: 实现条件和结果配置**

事件结果支持资源、人口、关注度、科技、角色状态、对话和后续事件。事件完成 ID 写入存档。

- [ ] **Step 4: 配置六幕**

时间只作最早触发保护，不作为唯一条件。使用 A3 的验收条件推进。

- [ ] **Step 5: PlayMode 从新游戏走到占位结算**

使用测试辅助命令加速资源与战斗，但不能绕过事件条件。

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: connect six-act demo flow"
```

---

## Task 12: 岑烬、救援分支与领袖技能（第 9 天）

**Files:**
- Create: `Scripts/Progression/LeaderDefinition.cs`
- Create: `Scripts/Progression/LeaderController.cs`
- Create: `Scripts/Progression/LeaderSkillService.cs`
- Test: `Tests/EditMode/LeaderChoiceTests.cs`
- Test: `Tests/PlayMode/LeaderOverloadPlayModeTests.cs`

**Interfaces:**
- Produces: `RecruitLeader()`、`ActivateSkill()`、装配厂效率修正。

- [ ] **Step 1: 写立即/延迟救援结果测试**

立即救援为完整技能；延迟救援降低 Boss 战技能效果；无永久丢失。

- [ ] **Step 2: 写过载结束后停火测试**

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现角色、主动和被动**

立即救援版本使附近炮塔攻速×1.75，持续 5 秒；延迟救援版本为×1.35。效果结束后炮塔停火 3 秒，主动技能冷却 30 秒。被动使城市范围内装配厂效率×1.25。倍率、持续时间和冷却都写入 `LeaderDefinition`，效果不能永久残留。

- [ ] **Step 5: PlayMode 验证两分支**

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add Cen Jin rescue and leader gameplay"
```

---

## Task 13: Boss 与坚守/撤离路线（第 9 天）

**Files:**
- Create: `Scripts/Combat/Boss/CrystalMotherController.cs`
- Create: `Scripts/Combat/Boss/BossPhase.cs`
- Create: `Scripts/Combat/Boss/BossArenaRules.cs`
- Test: `Tests/EditMode/CrystalMotherPhaseTests.cs`
- Test: `Tests/PlayMode/BossRoutesPlayModeTests.cs`

**Interfaces:**
- Consumes: 城市状态、波次、关注度。
- Produces: Boss 阶段事件、击败状态、升阶条件。

- [ ] **Step 1: 写 70%/40% 阶段只切换一次测试**

- [ ] **Step 2: 写撤离后 Boss 寻路与重新展开测试**

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现 A16.8 三阶段**

所有攻击使用明确预警；Boss 不穿越结晶障碍；撤离路线可使用东部狭口。

- [ ] **Step 5: PlayMode 完成坚守与撤离各一次**

记录时间、消耗弹药、建筑损失和失败原因。

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add crystal mother boss and two viable routes"
```

---

## Task 14: 存档、检查点与版本迁移（第 10 天）

**Files:**
- Create: `Scripts/Persistence/SaveGameData.cs`
- Create: `Scripts/Persistence/SaveGameService.cs`
- Create: `Scripts/Persistence/SaveMigration.cs`
- Create: `Scripts/Persistence/ContentSourceRecord.cs`
- Create: `Scripts/Persistence/MissingContentProxy.cs`
- Create: `Scripts/Persistence/OrphanedResourceRecord.cs`
- Test: `Tests/EditMode/SaveRoundTripTests.cs`
- Test: `Tests/EditMode/SaveMigrationTests.cs`
- Test: `Tests/PlayMode/CheckpointRecoveryPlayModeTests.cs`

**Interfaces:**
- Produces: `ISaveGameService`，保存版本 `1`.

- [ ] **Step 1: 写保存往返测试**

必须覆盖 `gameVersion`、`saveSchemaVersion`、内容源列表、库存、城市、建筑、生产库存、敌人/波次、关注度、科技、命轨、人口、领袖、事件和当前幕。

- [ ] **Step 2: 写损坏文件回退测试**

损坏当前存档时加载最近有效检查点并显示提示，不覆盖损坏文件。

- [ ] **Step 3: 写未知内容和 V1→V2 迁移测试**

未知建筑恢复为保留原始 ID、位置和占格的 `MissingContentProxy`；未知资源进入孤立资源表；迁移前创建备份；迁移失败不覆盖原存档。

- [ ] **Step 4: 运行并确认失败**

- [ ] **Step 5: 实现原子写入和版本字段**

先写临时文件，校验后替换正式存档；保留上一份备份。

- [ ] **Step 6: 实现单向迁移链**

迁移类按 `V1ToV2Migration` 命名；旧迁移发布后不得修改。当前首版保存为 Schema 1，同时提供测试夹具证明未来迁移入口可工作。

- [ ] **Step 7: 配置四个检查点**

命轨完成、首次展开、首塔完成、Boss 开始。

- [ ] **Step 8: 运行测试并提交**

```bash
git add Assets/_Project
git commit -m "feat: add versioned saves and recovery checkpoints"
```

---

## Task 15: HUD、面板、设置与结算（第 10 天）

**Files:**
- Create: `Scripts/UI/HudPresenter.cs`
- Create: `Scripts/UI/ResourceBarPresenter.cs`
- Create: `Scripts/UI/BuildingPanelPresenter.cs`
- Create: `Scripts/UI/ResearchPanelPresenter.cs`
- Create: `Scripts/UI/AttentionPanelPresenter.cs`
- Create: `Scripts/UI/FatePanelPresenter.cs`
- Create: `Scripts/UI/LeaderPanelPresenter.cs`
- Create: `Scripts/UI/SettingsPresenter.cs`
- Create: `Scripts/UI/DemoSummaryPresenter.cs`
- Test: `Tests/PlayMode/HudPlayModeTests.cs`

**Interfaces:**
- UI 只调用公开服务，不直接修改库存或系统字段；所有可见文字通过 `ILocalizationService` 获取。

- [ ] **Step 1: 写 HUD 状态映射测试**

验证人口/容量/生产力、四资源、关注度、目标和城市状态。

- [ ] **Step 2: 写暂停时仍可阅读说明测试**

- [ ] **Step 3: 运行并确认失败**

- [ ] **Step 4: 实现 A7 所有 D1 界面**

悬停资源显示收入、消耗、预计耗尽；建筑显示阻塞；关注度显示最近三条。代码和场景中不得保存最终中文显示文本，只保存 TextKey。

- [ ] **Step 5: 实现设置**

音乐、音效、全屏、分辨率、震动；保存设置独立于游戏存档。

- [ ] **Step 6: 实现结算**

显示完成时间、击杀、最高关注度、生产效率、建筑损失和关键选择。

- [ ] **Step 7: PlayMode 验证并提交**

```bash
git add Assets/_Project
git commit -m "feat: add complete demo interface and summary"
```

---

## Task 16: 本地遥测、试玩日志与版本记录（第 10 天）

**Files:**
- Create: `Scripts/Core/LocalTelemetry.cs`
- Create: `Scripts/Core/TelemetryEvent.cs`
- Create: `Assets/_Project/Docs/PlaytestReportTemplate.md`
- Test: `Tests/EditMode/LocalTelemetryTests.cs`

**Interfaces:**
- Produces: JSON Lines 本地日志；不联网、不包含个人信息。

- [ ] **Step 1: 写日志字段测试**

每条事件包含运行 ID、版本、游戏时间、事件名、参数和关键资源。

- [ ] **Step 2: 运行并确认失败**

- [ ] **Step 3: 实现 A18.3 事件列表**

日志写入 `Application.persistentDataPath/Telemetry/`，写入失败不阻断游戏。

- [ ] **Step 4: 使用模板完成一次自测记录**

- [ ] **Step 5: 提交**

```bash
git add Assets/_Project
git commit -m "feat: add local playtest telemetry"
```

---

## Task 17: 视觉、动画、特效与音频替换（第 11-12 天）

**Files:**
- Create/Modify: `Art/**`
- Create/Modify: `Audio/**`
- Create/Modify: `Prefabs/**`
- Create: `Assets/_Project/Docs/AssetRegister.csv`
- Test: `Tests/PlayMode/ContentReferencePlayModeTests.cs`

**Interfaces:**
- 所有素材通过预制体和配置引用；规则代码不直接引用具体图片或音频文件名。

- [ ] **Step 1: 建立资产登记表**

列包含资产 ID、用途、工具/模型、提示词版本、日期、参考来源、后期修改、许可证、Unity 路径。

- [ ] **Step 2: 写缺失引用测试**

加载所有首版配置和预制体，确认 Sprite、Animator、AudioClip、Prefab 不为空。

- [ ] **Step 3: 按优先级替换占位**

城市与状态 → 建筑与阻塞 → 敌人与危险 → 战斗反馈 → UI → 环境装饰。

- [ ] **Step 4: 添加最低音频**

主菜单、探索、经营、战斗、Boss、结尾音乐；关键操作和战斗音效。

- [ ] **Step 5: 运行内容引用测试和场景可读性检查**

同时运行 `ContentCatalog.Validate()`，确认全部首版资产具有命名空间 ID、中文 Key、许可记录和有效资源引用。

- [ ] **Step 6: 提交**

```bash
git add Assets/_Project
git commit -m "feat: replace placeholders with licensed audiovisual content"
```

---

## Task 18: 回归、性能、Windows 构建与作品集（第 13-14 天）

**Files:**
- Create: `Assets/_Project/Docs/ReleaseChecklist.md`
- Create: `Assets/_Project/Docs/KnownIssues.md`
- Create: `Assets/_Project/Docs/PortfolioCaseStudy.md`
- Modify: `Scripts/Core/LocalTelemetry.cs`
- Modify: `Editor/BuildTools.cs`

**Interfaces:**
- Produces: 可分发 Windows 构建、测试证据、作品集材料。

- [ ] **Step 1: 运行完整 EditMode 和 PlayMode 测试**

预期：零失败。失败时先修复，不跳过或删除测试。

- [ ] **Step 2: 执行三命轨回归矩阵**

每条命轨至少完成一次；坚守和撤离路线各完成一次；加载四个检查点。

- [ ] **Step 3: 运行 60 分钟稳定性和压力测试**

记录平均 FPS、1% 低帧、峰值敌人、峰值弹丸、内存起止值和阻断报错。

- [ ] **Step 4: 运行 R2/R3 试玩并应用决策阈值**

任何改动只允许修复、调数值和改善反馈，不新增系统。

- [ ] **Step 5: 构建 Windows 版本**

运行 `BuildTools.BuildWindows`；在干净目录解压并从主菜单走到结算。

- [ ] **Step 6: 在真实 Windows 设备完成候选版检查**

检查中文字体、DPI、窗口/全屏、键鼠焦点、输入法、路径权限、退出保存、资源引用、杀毒软件误报、30-60 分钟性能和绝对路径泄漏。没有 Windows 实机结果不得标记为发布候选版。

- [ ] **Step 7: 完成发布检查**

确认版本号、许可证、存档路径、音量、分辨率、退出保存、失败重试、无个人路径泄漏。

- [ ] **Step 8: 制作作品集案例**

包含产品定位、核心循环、范围取舍、数值调整、试玩证据、AI 任务卡、已知问题和正式版路线。

- [ ] **Step 9: 最终提交和标签**

```bash
git add .
git commit -m "release: complete Waste City demo vertical slice"
git tag demo-v1.0.0
```

---

## 3. 需求覆盖检查表

| GDD 要求 | 实施任务 |
|----------|----------|
| 移动、展开、收起、碾压 | Task 5、7 |
| 人口决定生产力 | Task 4 |
| 资源、建造、物流、生产链 | Task 2、4、6 |
| 三普通敌人、精英、Boss | Task 8、13 |
| 关注度与三压力阶段 | Task 8 |
| 五项研究与升阶 | Task 9 |
| 三条命轨 | Task 10 |
| 岑烬与救援选择 | Task 12 |
| 六幕流程与结尾 | Task 11、13、15 |
| 存档与检查点 | Task 14 |
| UI、设置和结算 | Task 15 |
| 本地试玩数据 | Task 16 |
| 视觉、音频和授权记录 | Task 17 |
| 性能、构建和作品集 | Task 18 |
| 命名空间 ID 与唯一内容注册 | Task 2 |
| 本地化 Key 与中文表 | Task 2、15、17 |
| 存档版本、迁移和未知内容 | Task 14 |
| Windows 实机发布验收 | Task 18 |

## 4. 每日结束统一检查

- [ ] 当前分支无未解释的编译错误；
- [ ] 当日新增测试全部通过；
- [ ] 从主场景运行当日交付；
- [ ] 检查 Console 无持续报错；
- [ ] 更新开发进度与已知问题；
- [ ] 提交只包含本任务文件；
- [ ] 次日任务依赖已经满足；
- [ ] 新想法进入正式版待办，不扩大首版范围。
