# 十二种环境装饰参考图来源记录

## 状态

- 视觉状态：用户已于 2026-08-09 明确回复“可以”，批准本套白底单物件参考图。
- 交付边界：本次只批准二维概念参考，不批准或要求继续制作 `.blend`、`.fbx`、Unity Prefab、Collider、LOD、材质图集或运行时接入。
- 用户后续约束：用户明确表示“不准备让你来建模了”；因此这些图片只供未来美术人员接手，不得被解释为当前开发者继续建模的授权。

## 批准资产

- `EnvironmentProps_12Item_WhiteBackground_Approved_v001.png`：4×3 总览参考板。
- `EnvironmentProps_12Item_SingleObject_White_QA.png`：十二张独立图片的无文字检查板。
- `WhiteBackground_SingleObject/01_SmallRock_SingleObject_White.png`
- `WhiteBackground_SingleObject/02_LargeRock_SingleObject_White.png`
- `WhiteBackground_SingleObject/03_ScrapSteelPlate_SingleObject_White.png`
- `WhiteBackground_SingleObject/04_BrokenPipe_SingleObject_White.png`
- `WhiteBackground_SingleObject/05_WornTire_SingleObject_White.png`
- `WhiteBackground_SingleObject/06_RoadBarricade_SingleObject_White.png`
- `WhiteBackground_SingleObject/07_MetalCrate_SingleObject_White.png`
- `WhiteBackground_SingleObject/08_BrokenStreetlamp_SingleObject_White.png`
- `WhiteBackground_SingleObject/09_ConcreteRubble_SingleObject_White.png`
- `WhiteBackground_SingleObject/10_MechanicalWreckage_SingleObject_White.png`
- `WhiteBackground_SingleObject/11_DeadPlant_SingleObject_White.png`
- `WhiteBackground_SingleObject/12_EnergyCrystalFragment_SingleObject_White.png`

十二张单图均为 `512×512` RGB PNG、纯白背景、单一居中主体。主体周围与该主体连续的破碎边缘、根系、接触碎屑或结构残件属于同一资产造型，不代表额外独立道具。

## 生成与裁剪方法

1. 使用 OpenAI ImageGen 依据 WasteCity 已批准的暖黄干旱废土、风格化 PBR、多材质工业残破语言生成 4×3 环境装饰参考板。
2. 根据用户反馈将背景改为纯白，保持固定倾斜正交观察角度和统一光照。
3. 使用确定性图像处理按 4×3 网格拆分，再以非白像素颜色距离和连通区域识别主体。
4. 保留最大连续主体及紧邻的抗锯齿边缘、接触阴影，清除其他格子和分离残留。
5. 将主体按边界紧裁、等比缩放并居中放入 `512×512` 纯白画布；最终逐张及总览复核。

## 来源、许可证与版本

- 外部库存素材：无。
- 生成工具：Codex 内置 OpenAI ImageGen，2026-08-09 会话版本。
- 后处理：Python、Pillow、NumPy、SciPy 连通区域分析。
- 权利状态：项目委托生成内容；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
- 版本：`Approved v001`。

## 非交付中间文件

早期等格裁剪只属于本地处理历史，未纳入仓库交付；正式接手应只使用 `WhiteBackground_SingleObject/`。
