# 28 项普通建造目录建筑参考图来源记录

## 状态与边界

- 关联需求：`IDEA-0004`。
- 用户于 2026-08-09 查看 28 项白底单物件检查板后明确回复“通过”。
- 当前批准内容仅为二维建筑视觉参考：4 张路线源板、28 张独立 `512×512` RGB PNG 和 1 张 QA 总览。
- 不包含 Blender、FBX、Prefab、Unity Sprite、透明底正式图标、运行时映射或场景接入。
- 用户已明确后续不由当前开发者建模；这些概念图不得被解释为建模授权。

## 稳定目录映射

| 序号 | Stable ID | 单图文件 |
|---:|---|---|
| 01 | `core.building.mining-station` | `01_MiningStation_SingleObject_White.png` |
| 02 | `core.building.housing` | `02_Housing_SingleObject_White.png` |
| 03 | `core.building.warehouse` | `03_Warehouse_SingleObject_White.png` |
| 04 | `core.building.wall` | `04_Wall_SingleObject_White.png` |
| 05 | `core.building.research-station` | `05_ResearchStation_SingleObject_White.png` |
| 06 | `core.building.smelter` | `06_Smelter_SingleObject_White.png` |
| 07 | `core.building.assembler` | `07_Assembler_SingleObject_White.png` |
| 08 | `core.building.machine-gun-turret` | `08_MachineGunTurret_SingleObject_White.png` |
| 09 | `technology.building.power-plant` | `09_PowerPlant_SingleObject_White.png` |
| 10 | `cultivation.building.spirit-fire-furnace` | `10_SpiritFireFurnace_SingleObject_White.png` |
| 11 | `cultivation.building.artifact-workshop` | `11_ArtifactWorkshop_SingleObject_White.png` |
| 12 | `cultivation.building.sword-array-tower` | `12_SwordArrayTower_SingleObject_White.png` |
| 13 | `biological.building.colony-pool` | `13_ColonyPool_SingleObject_White.png` |
| 14 | `biological.building.breeding-chamber` | `14_BreedingChamber_SingleObject_White.png` |
| 15 | `biological.building.spore-tower` | `15_SporeTower_SingleObject_White.png` |
| 16 | `biological.building.metabolic-furnace` | `16_MetabolicFurnace_SingleObject_White.png` |
| 17 | `psionics.building.resonance-furnace` | `17_ResonanceFurnace_SingleObject_White.png` |
| 18 | `psionics.building.workshop` | `18_PsionicWorkshop_SingleObject_White.png` |
| 19 | `psionics.building.mind-spire` | `19_MindSpire_SingleObject_White.png` |
| 20 | `psionics.building.consciousness-network` | `20_ConsciousnessNetwork_SingleObject_White.png` |
| 21 | `core.building.laser-tower` | `21_LaserTower_SingleObject_White.png` |
| 22 | `biological.building.acid-tower` | `22_AcidTower_SingleObject_White.png` |
| 23 | `psionics.building.shield-generator` | `23_ShieldGenerator_SingleObject_White.png` |
| 24 | `cultivation.building.spirit-gathering-array` | `24_SpiritGatheringArray_SingleObject_White.png` |
| 25 | `core.building.automated-repair-bay` | `25_AutomatedRepairBay_SingleObject_White.png` |
| 26 | `cultivation.building.alchemy-chamber` | `26_AlchemyChamber_SingleObject_White.png` |
| 27 | `cultivation.building.puppet-workshop` | `27_PuppetWorkshop_SingleObject_White.png` |
| 28 | `biological.building.behemoth-pen` | `28_BehemothPen_SingleObject_White.png` |

明确排除仅可升级获得的 `core.building.heavy-machine-gun-turret` 与 `cultivation.building.sword-riding-platform`。

## 生成与裁剪方法

1. 使用 Codex 内置 OpenAI ImageGen 分别生成核心工业、修仙、生物、灵能四张白底源板。
2. 固定三分之四俯视正交相机、暖色棚拍光、白底、单主体、无人物、无独立散件，并统一暖黄干旱工业废土与多材质语言。
3. 使用 Python、Pillow、NumPy、SciPy 按固定网格拆分，以边缘连通近白像素归一化纯白背景，再按主体范围紧裁。
4. 每个主体等比放入 `512×512` RGB PNG；最终按 `BuildingCatalog.BuildMenu` 顺序生成 7×4 QA 板并自动验证数量、尺寸、白色角点和非空内容。

## 来源与许可证

- 外部库存素材：无。
- 生成日期与版本：2026-08-09，`Approved v001`。
- 权利状态：项目委托生成内容；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
