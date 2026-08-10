# 移动城市与内城平台三件套参考图来源记录

## 范围与批准

- 关联需求：第一版美术计划 Task 6。
- 用户于 2026-08-09 查看三件套白底 QA 总览后回复“继续”，批准当前参考并要求进入下一项。
- 批准内容：1 张白底源板、3 张 `512×512` RGB 单物体 PNG 和 1 张 QA 总览。
- 本批次仅提供未来美术制作的概念参考，不包含 Blender、建模、LOD、UV、贴图、动画、FBX、Prefab、碰撞体、玩法脚本或 Unity 接入。

## 固定顺序

1. 移动城市移动形态 `MobileCity Mobile`
2. 同一移动城市展开堡垒形态 `MobileCity Fortress`
3. 独立内城平台 `InnerCity Platform`

## 视觉与接口边界

- 移动和堡垒形态必须保留同一底盘、前部驾驶舱、四组履带、中央核心、双烟囱与装甲语言；展开形态只增加落地支撑、侧平台和可用顶面，不设计成另一座城市。
- 内城平台保持 `4:3` 外轮廓和清楚可读的 `8×6` 分区；边框、锁扣、灯和接口位于可建造分区之外。
- 参考图不修改城市稳定 ID、城市状态机、平台逻辑尺寸、建筑网格、移动规则或玩法根节点。
- 颜色和材质延续 WasteCity 已批准资产：约 50% 深色氧化钢、28% 暖赭黄积尘装甲、12% 旧铜或黄铜、7% 脏污混凝土/陶瓷、2% 琥珀状态灯、少于 1% 功能性暗青点缀。
- 禁止干净科幻、赛博霓虹、悬浮城市、魔法光环、大型主炮、动物腿结构和现代卡车外形。

## 生成提示词摘要

```text
Use case: stylized-concept
Asset type: WasteCity mobile-city production reference board
Primary request: one 3-column white-background board containing the same tracked mobile city in moving and deployed fortress forms, followed by a separate exact 8-by-6 inner-city platform.
Style/medium: stylized PBR, physically grounded industrial salvage, mid/low-poly production concept, matching the approved WasteCity building and environment references.
Materials: dark oxidized steel, dusty ochre armor, aged copper/brass, dirty concrete/ceramic, restrained amber lights, only tiny functional cyan accents.
Constraints: same city identity across the first two columns; four tracked bogies; deployed outriggers; platform with a readable 8-column by 6-row grid; pure white background; one isolated subject per panel; no people or text.
Avoid: clean sci-fi, cyberpunk neon, fantasy, holograms, floating city, train, ordinary truck, animal legs, oversized weapons, watermarks.
```

## 生成与后处理

- 生成工具：Codex 内置 OpenAI ImageGen。
- 风格参考：项目内已批准的 28 建筑白底 QA、十二种环境装饰白底 QA、七种施工状态白底 QA。
- 原始生成文件：`C:/Users/czc1/.codex/generated_images/019fbb9a-cf7b-7430-ad20-9a19d3309ed7/exec-62c76c4c-639e-48cc-a09a-382e7cf0293e.png`。
- 后处理工具：Python、Pillow、NumPy、SciPy。
- 后处理方式：按三栏拆分，移除生成面板边线与跨栏像素，提取主要物体，统一为纯白背景并居中放入 `512×512` RGB 画布。
- 自动检查：`3/3` 文件存在；尺寸均为 `512×512`；四角均为 `#FFFFFF`；每张只保留一个主体。

## 来源、版本与许可

- 外部库存素材：无。
- 批准版本：2026-08-09，`Approved v001`。
- 权利状态：项目委托生成内容；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
