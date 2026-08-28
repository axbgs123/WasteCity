# IDEA-0024 科技树、验收入口与世界比例设计

## 目标

按用户图二重构正式科技树表现，同时解决启动验收入口、M/N/P 鼠标切换、建筑 Sprite 不可见和移动城市过小/内外格比例不一致。所有变化保持 schema `34`、64×48 v2 地图和领域真值不变。

## 科技树

- 继续由 `ResearchCatalog`、`FormalResearchRuntime` 和城市库存拥有 44 科技、49 前置边、6 桥节点、成本、时间、状态和材料真值。
- `ResearchTreeProjection3D` 是唯一图布局：底部公共根，科技/修仙/生物飞升/灵能四条纵向路线，每路线两子列，桥科技位于相邻路线 gutter 并按前置层级错层。
- Header 提供标题、搜索、5 路线筛选、5 结构化状态筛选、当前研究和最新可研究定位。
- Tree 使用紧凑节点、路线标题、双层连线、圆接点、箭头、金色虚线桥和非颜色状态符号。
- Footer 固定显示选中科技大图、说明/解锁、全部真实材料图标与数量、时间、前置、开始/取消和状态图例。
- 新背景为原创无文字工业终端纹理；科技和材料图标不变。

## 启动验收管理台

- 仅 Editor/Development 创建“验收管理台”页面；Release 不创建控件。
- 页面提供继续并打开管理台、新游戏并打开管理台、返回。
- 继续/新游戏仍走现有正式入口及覆盖确认；只在 `EnterGameplay` 后调用既有 `GrayboxDeveloperModifierBootstrap3D.TryTogglePanel()`。
- 不绕过未来 schema 拒绝，不直接写存档 DTO，不自动判定用户通过。

## 移动城市与建筑

- 唯一表现 Profile 提供 inner cell=ground cell=`1`、8×6、派生 anchor `(-4,-3)`、平台与 deck 高度、城市视觉约 `8.6×0.65×6.6`。
- 原 gameplay Rigidbody/BoxCollider、路径、部署和战斗边界保持旧值；视觉与 collider 解耦。
- Platform、SurfaceProjector、BuildingWorldView 和 SceneAuthoring 消费同一坐标策略，不复制常量。
- 完成建筑 Sprite 的底边位于 Mesh 屋顶上方，保持 world-up 并水平朝向相机；施工/遗迹继续按状态隐藏。

## TDD 与回归

先写失败测试冻结科技树结构、背景/详情/筛选、可点击页签、Dev/Release 启动器差异、城市视觉/物理解耦、8×6 甲板和建筑公告板可见性；再做最小实现。最终运行聚焦、日常完整 EditMode、完整 PlayMode、质量门和三项构建。用户视觉与真实 Windows 仍由实际试玩确认。
