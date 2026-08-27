# IDEA-0022 实施计划

1. 提交受控需求、设计规格和 schema 34 边界。
2. F3 RED：四单位目录、制造/容量/维护、小队命令/路径/战斗/远征/战利品。
3. F3 GREEN：完成纯模型、主城库存/Defense/Leader 窄接口、Army 面板/地图标记和真实 `M` 输入。
4. F4A RED：settlement 身份/位置/库存/自治、聚焦/控制权、Convoy 原子装卸/风险/护送/恢复、前哨通信/补给/警报。
5. F4A GREEN：完成 WorldLayer/Settlement/Transport，主城引用与远程库存边界，World 面板/标记/真实 `N` 输入。
6. F5A RED：三角色、倒地/救援/中断/恢复/死亡/遗体，派系/忠诚/支持，议会/继承/政变，外交接触/报价/交易/条约。
7. F5A GREEN：完成 Character/Rescue/Leadership/Politics/Diplomacy，DirectControl/Leader/Attention/Statistics/Checkpoint 窄接口，Politics 面板/倒地 HUD/继承模态/真实 `P` 输入。
8. schema 34 RED→GREEN：DTO、codec/hash、validator、33→34、31/32/33 链、恢复计划/回滚、Rewind/WaveRetry、派生重建。
9. 组合 Host/Controller/View/Input/SceneAuthoring，用真实 Input System 跑 M/N/P 和保存退出/继续。
10. 独立静态审查，修复 P0–P2，更新质量目录/复用目录/测试指南。
11. 运行聚焦、日常完整 EditMode、完整 PlayMode、编译、三构建、Generate/Validate/Analyze/RecordVerification。
12. 分别回写 F3、F4A、F5A 真实状态，普通 push；不合并 PR、不创建 Release、不写人工/Windows 已验收。
