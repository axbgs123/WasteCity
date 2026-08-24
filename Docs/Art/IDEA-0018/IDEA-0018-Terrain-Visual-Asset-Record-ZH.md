# IDEA-0018 地表视觉资产记录

日期：2026-08-24

状态：已生成并接入，待运行时固定证据与用户视觉验收

## 1. 范围

本批重制七类正式地表的 `BaseColor`，并从每类最终 `BaseColor` 确定性派生对应的 `Height`、`Normal`、`Mask`，不再沿用与新颜色纹理无关的旧通道。Texture2DArray 层顺序、控制图、连续 Mesh、Ruins/Cliff 几何和全部玩法真值保持不变。正式 `BaseColor`/`Normal` 为 `2048×2048` RGB PNG，`Mask` 为同尺寸 RGBA PNG，`Height` 为同尺寸 16-bit 灰度 PNG；源概念为 `1254×1254` RGB PNG。

地图仍为 `64×48` 方格、seed `8128`，资源节点、Traversal、建筑坐标、寻路、放置规则和 schema `32` 均未改变。该批不把地图改为六边形，也不复制任何《文明》或第三方游戏资产。

## 2. 工具、来源与许可

- 概念工具：OpenAI Codex 内置 `imagegen`；具体模型版本未暴露。
- 概念到正式图：七类各自独立生成一张原创、正交俯视、低频色块优先的地表概念。仓库 Editor 生成器只读取 `ArtSource/.../References/*_IDEA0018_Cartographic_Concept_v001.png` 这七份不可变输入，通过固定点 CPU 双线性重采样到 `2048×2048`，再做 96 像素 `smoothstep` 对边融合；正式 `BaseColor` 不再作为下一次重建的输入，也不依赖 GPU 或 `sips`。融合覆盖一段连续边缘带，不采用只修改首末像素的硬切接缝。
- 四通道生成器：`FirstArtTerrainAssetBuilder.RebuildCartographicSourceChannels` 从每次重新解码、重采样和融合的不可变概念输入生成正式 `BaseColor`，再确定性派生低振幅 16-bit `Height`、高度梯度对应的切线空间 `Normal` 及 R=Metallic、G=AO、B=Detail、A=Smoothness 的 `Mask`，随后按冻结层顺序重建四个正式 Texture2DArray。
- 身份保护：生成事务在覆盖前保存 28 张正式 PNG 及其 `.meta`，重导入后校验 GUID 和 `.meta` 字节；任一步失败即恢复源文件。概念源不被反写。
- 幂等保护：`TerrainAssetDeep` 连续执行两次纯生成，逐路径比较 28 张 PNG 的完整字节，并复核七份概念输入在生成前后字节不变；该门用于阻止“从已融合正式图继续融合”造成的重复重建漂移。
- 网络与 GitHub 参考：只提炼远距离轮廓、颜色家族、地图分层和模拟/表现分离原则；没有下载、复制或修改第三方代码与像素。
- 权利边界：为 WasteCity 项目按用户委托生成；使用权受项目账户对应 OpenAI 服务条款约束。发布前仍须由项目负责人完成最终商用与视觉复核。
- 人工结论：当前记录只证明生成、接入和自动合同，不代表用户已经完成视觉验收。

## 3. 统一提示词规则

七张图共享以下生产约束：原创的非写实手绘策略游戏地表；正交俯视；四边无缝；宽阔、低频、可远距离辨认的色块；中低频表面细节；中性漫反射光；无透视、地平线、边框、网格、六边形、建筑、资源图标、文字、水印或 Logo；避免照片噪声、高频杂乱、强方向阴影和明显重复图案。

各层差异如下：

| 层 | 中文视觉摘要 | 主色家族 |
|---|---|---|
| Wasteland | 风化干裂土、稀疏小石、宽阔尘土块 | 暖赭黄、沙色、少量焦褐 |
| Rocky | 大块磨损岩板、少量碎石 | 冷灰、灰褐、少量蓝灰 |
| Wetland | 湿土、克制浅湿斑、少量苔状笔触 | 灰橄榄、泥炭褐、去饱和绿 |
| Crystal | 浅色矿壳、细青色矿脉、小型内嵌晶面 | 青灰、浅石色、青绿 |
| Ruins | 磨损混凝土、稀疏锈迹和骨料 | 混凝土灰、炭灰、氧化锈色 |
| DeepWater | 深色连续水面、克制青灰波纹 | 蓝黑、深海军蓝、青灰 |
| Cliff | 宽阔断裂岩板、暖褐缝隙 | 石墨灰、冷赭褐、暖褐 |

## 4. 资产身份

概念源 SHA-256 未改变：Wasteland `949eeefbf69fc23c884a15b2ed4d8fa661caf7d98415e5eab20882aa83b6f49a`；Rocky `4ac1c15bedc0a6a51f0ec157aa6bbaf70b25e0f3a48b627084e51058d921eaaa`；Wetland `c1064dccbfdc709b9bd35e98bcd572a09b45322c529a24b5850de79b91a06fe5`；Crystal `c7039132e1c6f6e7140d4b38402d87fb652756e8f807d6827552c8a4f6dfa2cb`；Ruins `15cca40d068007b2657d2f8fc728517bbbba514c38c5a1e46959f13a6f6daf11`；DeepWater `d8f7a304e877b6bb330e266b98e8e0cd0dc4f3a896451b99a071a68887511da0`；Cliff `a3349afba4ce0d1e4be3fdc4967492d2add8cb36c842387a406e4ef9032dba40`。

正式四通道 SHA-256：

| 层 | BaseColor | Height | Normal | Mask |
|---|---|---|---|---|
| Wasteland | `a18192f3dc20c69cbb85526f6a82106f83d95d3b62a5b471854ad731a564170d` | `38e92079e27860d5d973e36257fc77f4f138e52218b26142dd5c6a958fbf4fd1` | `20e36171ab12c088678ccb58ebd8f42b5c2fbf8a4aae1a8a970f15f265e84012` | `e9a6fd36ee250e4ef785331c47423358107c901ea20fce26ca2dc8429edd5174` |
| Rocky | `685eb9ae719e5f6b0993f37db7275df5487bdfa45d0fe700ebc49aee532cb8ed` | `a5664d76e5f6a5c8b772baff8f2cdd55965d28d21e715af921bd9e90235d97e6` | `a77ca736e3ddd2219535c7af5d70bdb8b2555a81dd5482595f2930f656ef75e9` | `8c50cd0d48d18cdb66e4bcb9859e73bfdebb89a7627502b45960337ed5dacc3e` |
| Wetland | `40133ca4d1c2af9b798cce84de51052c33868281ffca7ef1352fe7bcebaa4230` | `77aa2908f47775a45530863e05177407ce06b7c3aa720a7e73cbc865c39d877a` | `332aec9ad7bb8d4c028d450599027370861a5e6536d76ce00e9564e00ee96e01` | `8b1a1512a4cba3c4fa37160c59979d93b71ac0865271a0c773d70c93411f22d2` |
| Crystal | `a5594472b0adadc0722377fccd9917f4485c300ff3a507bdf3e5ba46a4e95c3b` | `710ce54cf907b824968abf61d03fc714102b67ba326242708763f93690ffd302` | `bf64a92ee85739e08a6811c709b326e30bcf511caf07cafec6903d64f4ccdb40` | `db97dbb7693f5ab2f2b2ffec4b13177be4c055fb4b05734eb1c9b9409286737a` |
| Ruins | `d2faa332a32de1ec0c66df6cc55597b8ab83dcfd89b56870410ae340a1a5b55f` | `d032a36981675a0c926fa79acdb29d003d560531a97de53b72a314435239a203` | `44227745882983d9c9e554a23959f61cff91d5c2ebe682c74a9a5312a19f148a` | `c1fb87db42bff7a6d4d2571462ef8ff42fc1aa55080222f9464238915b8d6b7f` |
| DeepWater | `33595c49c93f580991f1d1eb621221564a43cd948a3d01f6c85d0e0737dc5934` | `c908938256309f4fc9f564a7324866a2211dadad9ec8a3754c8a9271f0dc61e0` | `bdd493418413bb41ee7668a1553e23a2536c053047132b748814b248ed1d4a8b` | `1f61541570506094e48766b3efc5289e88f8d1fb9741c647769b76e00ee1cfa3` |
| Cliff | `dd0c92d4ce92d3dddfe06225010152e64ef16a9c7d88c574d2811ba07d25a29e` | `4241a2edfb472cdbf57171ee549739ff3ff420a15d8851743c34cd619ace6f25` | `95734da128abd6f332da3901a012af1a52cffa29820770c4425cdcc3a9c23adf` | `7e4c86ba33e808cc8e45ca14d9b336a614f29202f5593bf94d2c04f5d9001972` |

正式 Texture2DArray SHA-256：BaseColor `f3e129a54a3ced4864d8fb7ca940a96fcd47846f2d8cb7466a6c242e485aabe9`；Height `e893be59eabee21fcf284d70378032cab44a3cb584d552f3e857a81a5be38a1f`；Normal `36420017eb23c0aa86b9520b509b5e5ede356c34b95e0f5a77e0a61bb40e9871`；Mask `e20dfcb07c2cd1cf7b82c445f445be617a60b35dd3e317aacb88880d50d0d30e`。

概念源位于 `ArtSource/FirstPass/Environment/Terrain/<Layer>/References/<Layer>_IDEA0018_Cartographic_Concept_v001.png`；正式图继续使用既有稳定 Unity 路径 `Assets/_Game/Art/FirstPass/Environment/Terrain/<Layer>/T_Terrain_<Layer>_<Channel>.png`。静态核验显示 28 个正式 PNG 的 GUID 均与交接基线一致；Texture2DArray 仍为 BaseColor、Normal、Mask、Height 四个既有稳定路径。

## 5. 验证门

已完成的聚焦证据：

- RED：旧 Height 与新 BaseColor 的相关性失败；修正真实 R16 测试读取后，旧 BaseColor 的 Repeat 接缝失败。
- 正式重建：从不可变概念输入执行 `RebuildCartographicSourceChannels`，`/tmp/wastecity-idea18/terrain-idempotence/rebuild.log`，退出码 0。
- GREEN：`IDEA0018_ConceptGeneration_IsByteIdenticalAcrossConsecutiveRuns`、`IDEA0018_SourceChannelsMatchTheRebuiltCartographicBaseColors` 与 `BuildTextureArrays_UsesFrozenLayerOrderAndFormats` 共 `3/3` 通过；前者同时逐字节核对两次生成、仓库 28 张正式 PNG 和七份未改写 concept。记录为 `/tmp/wastecity-idea18/terrain-idempotence/final-focused-with-delivery.xml`。
- 合同覆盖七层 BaseColor↔Height 相关性、Height↔Normal 梯度相关性、低振幅高频范围、四通道 Repeat 接缝、BaseColor 96 像素边缘带梯度、Mask RGBA 语义、Texture2DArray 冻结层顺序和格式。

最终自动化与构建已经完成：完整 `TerrainAssetDeep` `20/20`、日常 EditMode `2515/2515`、完整 PlayMode `88/88`、项目质量分析与无界面编译通过；Windows Release 3D、Windows Development 3D、macOS universal 3D 三项构建成功。真实 Unity GUI 在 `/tmp/wastecity-first-terrain/visual-review` 生成 15 张固定截图、10 帧缩放轨迹、Near/Mid/Far 三档、300 连续水面帧、MP4 和 manifest；技术视觉门、DeepWater 蓝黑颜色门和动态门通过。准确实现提交为 `2d340d8844dae00ff7760ed44709e441ad877d72`。

上述自动化、开发机截图和跨平台构建不能替代用户对比例、色差、接缝、地貌辨认和遮挡的人工结论，也不能替代真实 Windows 10/11 的视觉、GPU、显存和内存复验；状态继续是“已生成并接入，待用户视觉验收”。
