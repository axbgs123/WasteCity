# IDEA-0018 文明式地图视觉与正式 3D UI 比例统一实施计划

需求：`IDEA-0018`
设计：`Docs/superpowers/specs/2026-08-24-idea-0018-civilization-map-ui-design.md`

## Task 1：需求、基线与失败测试

- 更新 Docs/06、GDD、路线图并保持 schema `32`；
- 记录当前 HEAD、LFS、Unity `2022.3.62f1`、受保护文件与七类资产身份；
- 先写世界/存档冻结测试，证明视觉重制不会改变地图真值；
- 先写 UI Canvas/Policy 失败测试和地形视觉合同失败测试；
- 跑最小过滤器，保存 RED 证据。

## Task 2：共享 UI 比例边界

- 新增纯表现 `FormalUiLayoutProfile3D` 与纯计算 `FormalUiLayoutPolicy3D`；
- Scene Authoring 让三个 Canvas 使用同一 `1920×1080 + Expand` 配置；
- 保留唯一 EventSystem、排序层和输入模块；
- 先让 Canvas/Policy/场景合同测试转绿。

## Task 3：常驻 HUD

- 按安全槽位整理资源栏、防御/核心 HUD、速度区、选择详情、建造栏与固定反馈；
- 保持建造栏本身和“反馈在其正上方”的用户已验收关系；
- 资源栏支持受控滚动，不改发现/账本真值；
- 以真实 Pointer/Keyboard 输入验证首末项和不穿透。

## Task 4：主面板与长内容

- 资源账本、背包/合成、科技树使用安全区内最大尺寸；
- 撤离、系统菜单和结算统一安全区，结算统计滚动、动作固定；
- 修改器改为右侧可滚动抽屉，移除整体缩小到不可读的策略；
- 覆盖紧凑窗口与超宽窗口测试。

## Task 5：镜头缩放与资源标记层级

- 先写正式 Input System 滚轮失败测试与模态不穿透测试；
- 在唯一镜头控制器中实现配置化缩放；
- 资源 Marker 实现 Near/Mid/Far 与 Mining Guidance 覆盖；
- 证明节点身份/储量不变、重复切换无对象和监听器增长。

## Task 6：七类地表重制

- 在稳定路径下制作七类低频、手绘/风格化源贴图；
- 保持 BaseColor/Normal/Mask/Height 尺寸、色彩空间、压缩与 LFS 合同；
- 更新七份 AssetRecord、来源说明、QA 与哈希；
- 先运行源资产聚焦测试，再由 Builder 生成四个 Texture2DArray。

## Task 7：同一 Shader/Profile 的宏观可读性

- 先写 Shader/Profile 参数合同失败测试；
- 增加确定性宏观色区、受控层内色阶、去重复和特殊边缘/岸线参数；
- 保留世界空间采样、top-3 混合、单 Renderer、双水法线和原子回退；
- 第一轮不改控制图算法，只有固定证据失败才单独调整。

## Task 8：视觉证据、性能与稳定重建

- 扩展证据捕获为地图总览、交汇、岸线、接缝、资源三档和主要 UI；
- 录制动态水与滚轮缩放连续帧证据；
- 验证 Renderer/材质/对象/监听器数量、300 帧稳定和反复重建；
- 独立只读审查地图真值、UI 输入和版权/许可证边界。

## Task 9：完整验证与交付

- 运行 `TerrainAssetDeep`；
- 运行日常完整 EditMode、完整 PlayMode与项目质量门；
- 构建 Windows Release 3D、Windows Development 3D、macOS universal 3D；
- 更新项目质量目录、可复用目录、使用/测试指南和正式记录；
- 运行 GenerateDocumentation、ValidateDocumentation、AnalyzeTestResults 和 `RecordVerification`；
- 提交并普通 push 当前分支，不创建 Release、不合并 PR、不 force-push；
- 交付时明确自动化/构建与用户视觉试玩、真实 Windows 验收的区别。
