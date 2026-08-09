# UI 面板与控件参考图来源记录

## 状态与范围

- 关联需求：`IDEA-0004`、第一版美术计划 Task 10。
- 用户于 2026-08-09 明确回复“算了。用第一版吧”，批准当前第一版 UI 面板与控件参考图。
- 批准内容：3 张白底源板、18 张 `512×512` RGB 单物体白底 PNG、1 张 QA 总览。
- 本批次只作为视觉参考与后续 UI 切图依据，不包含透明正式 Sprite、UGUI 行为、九宫格参数、Prefab、Unity 接入、模型或 FBX。

## 固定顺序

1. 十格快捷栏 `Quickbar10`
2. 完整建造目录 `FullCatalog`
3. 建筑卡片 `BuildingCard`
4. 建筑详情 `BuildingDetail`
5. 取消建造确认框 `CancelConfirmation`
6. 疏散列表 `EvacuationList`
7. 建造分类页签 `CategoryTab`
8. 路线页签 `RouteTab`
9. 搜索框 `SearchField`
10. 费用标签 `CostChip`
11. 锁定原因提示 `LockedReason`
12. 建造进度条 `ConstructionProgress`
13. 错误提示条 `ErrorToast`
14. 按钮普通态 `Button Normal`
15. 按钮悬停态 `Button Hover`
16. 按钮按下态 `Button Pressed`
17. 按钮禁用态 `Button Disabled`
18. 按钮选中态 `Button Selected`

## 视觉约束

- 与已批准的建筑、环境道具、分类徽章和操作徽章共用同一套废土工业语言。
- 主材质为深色氧化钢、旧铜与黄铜，辅以暖黄积尘、受控锈蚀和少量橙色或青色功能光。
- 保留分层装甲、倒角、铆钉和机械连接结构；以清晰的功能分区和状态差异保证可读性。
- 后续正式 UI 制作应控制随机锈点、细碎划痕和颗粒噪点，避免继续增加高频表面噪声。

## 生成与后处理

- 生成工具：Codex 内置 OpenAI ImageGen。
- 生成约束：纯白背景、单一连接主体、无文字、无标志、统一正交展示、统一废土工业材质。
- 后处理工具：Python、Pillow、NumPy、SciPy。
- 后处理方式：按源板网格拆分，移除浅灰网格线和外围近中性色线，按主体紧裁并居中放入 `512×512` 纯白画布。
- 自动检查：`18/18` 文件存在；尺寸均为 `512×512`；四角均为 `#FFFFFF`；内容非空。

## 来源、版本与许可证

- 外部库存素材：无。
- 批准版本：2026-08-09，`Approved v001`。
- 权利状态：项目委托生成内容；使用与再分发仍须遵守适用的 OpenAI 服务条款和仓库政策。本记录不替代法律意见。
