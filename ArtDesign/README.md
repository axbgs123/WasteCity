# 《废土移动城市》Demo 美术交付目录

最终交付采用“分类独立文档、单项独立图片”，不把全部内容合并为一个文件。

## 文档拆分

```text
WasteCity-Art-Design/
├─ 00-Global/
│  ├─ Visual-Style.md
│  ├─ Color-Material.md
│  └─ Shared-Negative-Prompt.md
├─ 01-City-Deck/
│  ├─ City-Deck-Design.md
│  └─ City-Deck-Prompts.md
├─ 02-Buildings/
│  ├─ Buildings-Design.md
│  └─ Buildings-Prompts.md
├─ 03-Enemies-Boss/
│  ├─ Enemies-Boss-Design.md
│  └─ Enemies-Boss-Prompts.md
├─ 04-Characters-Narrative/
│  ├─ Characters-Narrative-Design.md
│  └─ Characters-Narrative-Prompts.md
├─ 05-Environment-Resources/
│  ├─ Environment-Resources-Design.md
│  └─ Environment-Resources-Prompts.md
├─ 06-UI/
│  ├─ UI-Design.md
│  └─ UI-Prompts.md
├─ 07-VFX/
│  ├─ VFX-Design.md
│  └─ VFX-Prompts.md
├─ 08-Asset-Register/
│  └─ Asset-Register.csv
└─ 09-Generation-Methods/
   └─ Generation-Method-Index.md
```

## 图片拆分

- 每个概念设计对象单独生成一张 PNG。
- 城市收起、城市展开、空甲板、结构拆分分别为独立图片。
- 每种建筑分别为独立图片；建筑家族阵列只用于风格校准。
- 每种普通敌人、精英、Boss 三阶段分别为独立图片。
- 岑烬健康与受伤状态分别为独立图片。
- 每个环境区域分别为独立图片。
- HUD、建造模式、建筑面板、命轨界面分别为独立图片。
- 每组特效分别为独立效果板。

阵列图和对比板不能替代单项图片，只用于统一比例、状态和风格。

## 文件命名

```text
分类_资产_状态_版本.png
```

示例：

```text
city_mobile_normal_v001.png
city_deployed_normal_v001.png
building_smelter_running_v001.png
enemy_crystal_shell_normal_v001.png
character_cenjin_injured_v001.png
environment_crystal_ravine_v001.png
ui_build_mode_v001.png
vfx_attention_locked_v001.png
```

当前根目录中的综合稿仅作为拆分前的工作底稿；最终交付以各分类目录中的独立文件为准。

## 双重索引

- `01`–`07` 按照游戏中的功能分类，方便查找城市、建筑、敌人、环境、UI 和特效。
- `09-Generation-Methods` 按照实际生成方式分类，方便决定哪些内容使用文生图、参考图生成、矢量工具、人工分层或 Unity 制作。

同一资产不复制两份实体文件。生成方式目录只记录资源 ID、推荐方法、输入依赖和最终文件位置。
