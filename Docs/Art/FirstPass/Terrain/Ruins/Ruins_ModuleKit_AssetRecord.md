# 大型废墟 `Ruins` 八件低模模块包资产记录

## 状态与边界

- 关联需求：`IDEA-0004`
- 生产状态：参考板已由用户批准；八件模型候选已完成并等待用户视觉验收
- 制作日期：2026-08-09
- 工具：Blender 5.2.0 LTS，hash `fbe6228777e7`；Blender MCP 1.2 已启用
- 源文件：`ArtSource/FirstPass/Environment/Terrain/Ruins/Ruins_ModuleKit.blend`
- Unity 交换目录：`Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/`
- 坐标：Blender 内部 Z-up；FBX 使用 `-Z Forward / Y Up / Scale 1.0`
- Pivot：每件模型原点为 `(0,0,0)`，底面在 Blender `Z=0`，导入 Unity 后对应底面 `Y=0`
- 接入状态：未接入 Unity 场景、Prefab、材质或 VisualSlot
- 玩法边界：没有 Collider、Rigidbody、WasteCity MonoBehaviour、资源节点、通行规则或稳定 ID；模型仅为可替换视觉资产

## 逐件规格

尺寸为 Blender 源空间 `X×Y×Z`，单位与外城逻辑格合同一致；FBX 回读后尺寸和三角面数完全一致。

| FBX | 三角面 | 尺寸 | 视觉职责 |
|---|---:|---:|---|
| `SM_Ruins_CrackedFloorSlab.fbx` | 336 | `0.9900×0.9700×0.1114` | 破裂工业地坪板，低矮一格占地 |
| `SM_Ruins_RubblePile_A.fbx` | 448 | `0.9003×0.6596×0.2053` | 紧凑混凝土碎块堆 |
| `SM_Ruins_RubblePile_B.fbx` | 540 | `1.1537×0.5476×0.1806` | 细长板片与少量锈钢碎片混合堆 |
| `SM_Ruins_RebarConcreteBlock.fbx` | 272 | `0.9021×0.5368×0.3400` | 带三根短弯钢筋的破损混凝土块 |
| `SM_Ruins_BrokenPipe.fbx` | 308 | `0.7792×0.5399×0.5476` | 可见断口的短工业管段 |
| `SM_Ruins_DrainageChannel.fbx` | 324 | `1.0000×0.4813×0.1500` | 浅排水槽与破损混凝土唇边 |
| `SM_Ruins_BoundaryEdge.fbx` | 348 | `1.0400×0.5746×0.1400` | 废墟地表与积尘地面的低矮过渡边缘 |
| `SM_Ruins_WornMarkingPlate.fbx` | 288 | `0.9300×0.7400×0.0807` | 近贴地旧标线承载板 |

## 文件与 SHA-256

| 文件 | SHA-256 |
|---|---|
| `Ruins_Modules_Approved_AI_Reference_v001.png` | `f72aa401942a0956f9d027486eb9639acc18825ef06f22776c5b0336f333458c` |
| `Ruins_ModuleKit.blend` | `c690baccf245b8799abd6ad9ae713febd68d8874f3613c32bae2baf9916a5882` |
| `Ruins_ModuleKit_Generator.py` | `b8525827ae2ed231970053551491a2e7943dad00a812670b877f4ef79bd25f4c` |
| `SM_Ruins_BoundaryEdge.fbx` | `bdeb1dd657c30320d5006801db687b4d1a71a9274216fdf8f6f34cc50a81babe` |
| `SM_Ruins_BrokenPipe.fbx` | `e007bc148b385d1c5c823badbfe4221b3728ea3ef9c86f2415a22301fa1c352e` |
| `SM_Ruins_CrackedFloorSlab.fbx` | `47345e07172ee2ebf169a193d5397efdb05f903c82271c8a2b482efe32639222` |
| `SM_Ruins_DrainageChannel.fbx` | `a69b2989c360c75fee94cd1cafb63bbe9f5c73fb498bb9adb6538c07b2d8164b` |
| `SM_Ruins_RebarConcreteBlock.fbx` | `dd7f669f5324904d19d493f20b25c096372974b799eeb2da2808cb5a12943ee8` |
| `SM_Ruins_RubblePile_A.fbx` | `be3180e073dc4e17836869b82151e34359c347801f35cbe47d1af2da860818bd` |
| `SM_Ruins_RubblePile_B.fbx` | `cc3b8a0fbd60679c69e35eb104b561224a2dcdfe24956730a3686a61a38081f3` |
| `SM_Ruins_WornMarkingPlate.fbx` | `acabb0b7369929a33e8ece2ee3aeba8e0adcb0148eb46bb515f9e1ef93adda1d` |
| `QA_Ruins_ModuleKit_DefaultOrtho.png` | `0cd1df15ca1873e3f102a777075ae5bfa729ca33d32c04ea9df2479599c0c9a5` |
| `QA_Ruins_ModuleKit_Top.png` | `9807abfc1c6525a22b68228bf4001e9dad7451078e5f9893d38b93f102c8ae41` |
| `QA_Ruins_ModuleKit_Wireframe.png` | `5985dcdc1e169788dcc60d610e8472149056e50da871f24673e33f1bf94f2e9f` |

资产记录自身和来源说明不登记自身 SHA-256，以避免自引用；完整性由 Git 提交对象保证。

## 自动验证结果

- `Ruins_ModuleKit_Generator.py` 通过 Python AST 语法检查，并由 Blender 5.2.0 LTS 无界面完整执行，退出码为 `0`。
- `.blend` 无界面回读得到 `gameplay_truth=none`、`colliders=none`、`module_count=8`。
- 八件源 Mesh 均为独立对象，三角面范围 `272–540`，全部满足 `200–2,000` 要求；最低点均校正为 `Z=0`。
- 八个 FBX 已逐个导入全新 Blender 空场景，全部只有一个同名 Mesh；回读三角面、尺寸与源对象一致，位置均为 `(0,0,0)`。
- 默认倾斜正交图检查整体风格、轮廓与高度；顶视图检查占地和互不重复；线框图检查拓扑密度和独立轮廓。
- `.blend` 已打包批准参考图和生成脚本；没有把正式场景、玩法代码、地形枚举、通行规则、稳定 ID、Packages 或 ProjectSettings 纳入修改范围。

## 人工验收入口

用户需要查看：

1. `QA_Ruins_ModuleKit_DefaultOrtho.png`：八件套是否像同一套暖黄积尘工业废墟模块，剪影是否适合默认镜头；
2. `QA_Ruins_ModuleKit_Top.png`：八件职责是否清楚、占地是否紧凑、细长瓦砾堆是否仅轻微超格；
3. `QA_Ruins_ModuleKit_Wireframe.png`：低模轮廓是否清晰、没有隐藏高密度网格；
4. 批准后才允许把本候选记录为正式通过；本记录不等于 Unity 接入批准。
