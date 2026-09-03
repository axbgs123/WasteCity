#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxDeveloperProgressionCommandTests
    {
        [Test]
        public void IDEA0020_ActionCatalogUsesStableChineseSearchableNames()
        {
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.ProgressionActionEntries,
                Has.Count.EqualTo(31));
            Assert.That(GrayboxDeveloperCatalogQuery3D.SearchProgressionActions(
                    "关注度").Select(value => value.DisplayName),
                Does.Contain("增加关注度").And.Contain("降低关注度")
                    .And.Contain("设置关注度"));
            Assert.That(GrayboxDeveloperCatalogQuery3D
                .TryResolveProgressionAction(
                    "查询进度配置签名",
                    out GrayboxDeveloperCatalogEntry3D query), Is.True);
            Assert.That(query.StableId,
                Is.EqualTo("developer.query.configuration-signature"));
        }

        [Test]
        public void IDEA0020_QueryFailureAndNoChangeNeverMarkUsage()
        {
            using (Fixture fixture = Create())
            {
                Assert.That(fixture.Modifier.QueryProgression(), Is.Not.Null);
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "查询关注度阈值").Code,
                    Is.EqualTo(GrayboxDeveloperCommandCode3D.NoChange));
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "设置关注度", amount: 10).Code,
                    Is.EqualTo(GrayboxDeveloperCommandCode3D.NoChange));
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "不存在的动作").Succeeded, Is.False);
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "增加关注度", amount: 0).Succeeded, Is.False);
                Assert.That(fixture.Modifier.HasModifiedGameState, Is.False);
            }
        }

        [Test]
        public void IDEA0028_FateDomainQueryReadsSevenLiveOwnerSnapshots()
        {
            using (Fixture fixture = Create())
            {
                GrayboxDeveloperProgressionQuery3D initial =
                    fixture.Modifier.QueryProgression();
                Assert.That(initial.FateDomainStates, Has.Count.EqualTo(7));
                Assert.That(initial.FateDomainStates[0],
                    Does.StartWith("量子纠缠：已连接")
                        .And.Contain("共享资源 1")
                        .And.Contain("同步记录 0"));
                Assert.That(initial.FateDomainStates[1],
                    Does.StartWith("空间模板：模板 0"));
                Assert.That(initial.FateDomainStates[2],
                    Does.StartWith("局部时加：未启动"));
                Assert.That(initial.FateDomainStates[3],
                    Does.StartWith("预知迟滞：周期 0").And.Contain("无预告"));
                Assert.That(initial.FateDomainStates[4],
                    Does.StartWith("因果透明：完整原因未开放"));
                Assert.That(initial.FateDomainStates[5],
                    Does.StartWith("虚空宝箱：评估 0")
                        .And.Contain("待领取 0").And.Contain("已领取 0"));
                Assert.That(initial.FateDomainStates[6],
                    Is.EqualTo("坐标锁定：未锁定"));

                Assert.That(fixture.Quantum.TrySetConnected(false), Is.True);
                Assert.That(fixture.Spatial.TryRestore(
                    new SpatialTemplateSnapshot(
                        1ul,
                        new[]
                        {
                            new SpatialTemplateDefinition(
                                "developer-template",
                                new[]
                                {
                                    new SpatialTemplateCell(
                                        0, 0, "core.building.wall", 0),
                                }),
                        }),
                    out string spatialError), Is.True, spatialError);
                Assert.That(fixture.Haste.TryEnterCycle(
                    3ul, out string hasteError), Is.True, hasteError);
                Assert.That(fixture.Haste.TrySelectTarget(
                    "production", out hasteError), Is.True, hasteError);
                Assert.That(fixture.Haste.TryStart(out hasteError), Is.True,
                    hasteError);
                Assert.That(fixture.Foresight.TryEnterCycle(
                    4ul, out string foresightError), Is.True, foresightError);
                Assert.That(fixture.Foresight.TryReveal(
                    4ul,
                    10f,
                    new[]
                    {
                        new ForesightAuthoritativePlan(
                            "event.raid", 25f, "突袭将至"),
                    },
                    out _,
                    out foresightError), Is.True, foresightError);
                Assert.That(fixture.Causal.TrySetFullReasonAccess(true),
                    Is.True);
                for (ulong sequence = 1ul; sequence <= 100ul; sequence++)
                {
                    Assert.That(fixture.Chest.TryEvaluateDeath(
                        "developer-enemy",
                        sequence,
                        out _,
                        out string chestError), Is.True, chestError);
                }
                Assert.That(fixture.Coordinate.TryRestore(
                    new CoordinateLockSnapshot(true, 1ul),
                    out string coordinateError), Is.True, coordinateError);

                GrayboxDeveloperProgressionQuery3D current =
                    fixture.Modifier.QueryProgression();
                Assert.That(current.FateDomainStates[0],
                    Does.StartWith("量子纠缠：已断开"));
                Assert.That(current.FateDomainStates[1],
                    Does.Contain("模板 1").And.Contain("格位 1"));
                Assert.That(current.FateDomainStates[2],
                    Does.StartWith("局部时加：运行中")
                        .And.Contain("目标 生产").And.Contain("周期 3"));
                Assert.That(current.FateDomainStates[3],
                    Does.StartWith("预知迟滞：周期 4")
                        .And.Contain("突袭将至"));
                Assert.That(current.FateDomainStates[4],
                    Does.StartWith("因果透明：完整原因已开放"));
                Assert.That(current.FateDomainStates[5],
                    Does.Contain("评估 100").And.Contain("待领取 1"));
                Assert.That(current.FateDomainStates[6],
                    Is.EqualTo("坐标锁定：已锁定"));

                GrayboxDeveloperCommandResult3D result =
                    fixture.Modifier.ExecuteProgressionAction(
                        "查询命轨领域状态");
                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Code,
                    Is.EqualTo(GrayboxDeveloperCommandCode3D.NoChange));
                Assert.That(result.Message,
                    Does.Contain("量子纠缠").And.Contain("坐标锁定"));
                Assert.That(fixture.Modifier.HasModifiedGameState, Is.False);
            }
        }

        [Test]
        public void IDEA0020_AttentionActionsUseFormalHistoryAndRemainRestorable()
        {
            using (Fixture fixture = Create())
            {
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "增加关注度", amount: 7).Succeeded, Is.True);
                Assert.That(fixture.Attention.Value, Is.EqualTo(17));
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "降低关注度", amount: 5).Succeeded, Is.True);
                Assert.That(fixture.Attention.Value, Is.EqualTo(12));
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "设置关注度", amount: 33).Succeeded, Is.True);
                Assert.That(fixture.Attention.Value, Is.EqualTo(33));
                Assert.That(fixture.Attention.Capture().ReachedThresholds,
                    Does.Contain(30));
                var restored = new FormalAttentionRuntime();
                Assert.That(restored.TryRestore(
                    fixture.Attention.Capture(), out string error), Is.True,
                    error);
                Assert.That(restored.Value, Is.EqualTo(33));
                Assert.That(fixture.Modifier.HasModifiedGameState, Is.True);
            }
        }

        [TestCase("选择袖珍宇宙命轨", "core.legacy.pocket-universe")]
        [TestCase("选择量子纠缠命轨", "core.legacy.quantum-entanglement")]
        [TestCase("选择空间模板命轨", "core.legacy.spatial-template")]
        [TestCase("选择回溯锚点命轨", "core.legacy.rewind-anchor")]
        [TestCase("选择局部时加命轨", "core.legacy.local-haste")]
        [TestCase("选择预知迟滞命轨", "core.legacy.foresight-delay")]
        [TestCase("选择虚空债命轨", "core.legacy.void-debt")]
        [TestCase("选择因果透明命轨", "core.legacy.causal-transparency")]
        [TestCase("选择虚空宝箱命轨", "core.legacy.void-chest")]
        public void IDEA0028_NineFateSelectionActionsBindCivilization(
            string action,
            string expectedFate)
        {
            using (Fixture fixture = Create())
            {
                Assert.That(fixture.Modifier.ExecuteProgressionAction(action)
                    .Succeeded, Is.True);
                Assert.That(fixture.Fate.Capture().SelectedId,
                    Is.EqualTo(expectedFate));
                Assert.That(fixture.Civilization.Capture().FateId,
                    Is.EqualTo(expectedFate));
                Assert.That(fixture.Attention.Value, Is.EqualTo(15));
                Assert.That(fixture.Fate.Capture().OfferedIds,
                    Does.Contain(expectedFate));
                Assert.That(fixture.Fate.Capture().OfferedIds,
                    Has.Count.EqualTo(3));
                Assert.That(fixture.Fate.Capture().OfferedIds,
                    Is.Unique);
            }
        }

        [Test]
        public void IDEA0020_DebtPressureBossAndAscensionCommandsMutateOwners()
        {
            using (Fixture debt = Create(
                       FormalFateCatalog.VoidDebtId))
            {
                Assert.That(debt.Modifier.ExecuteProgressionAction(
                    "增加指定资源虚空债", ResourceIds.Iron, 25).Succeeded,
                    Is.True);
                Assert.That(debt.Debt.GetDebt(ResourceIds.Iron), Is.EqualTo(25));
                Assert.That(debt.Modifier.ExecuteProgressionAction(
                    "偿还指定资源虚空债", ResourceIds.Iron, 10).Succeeded,
                    Is.True);
                Assert.That(debt.Debt.GetDebt(ResourceIds.Iron), Is.EqualTo(15));
            }

            using (Fixture pressure = Create())
            {
                Assert.That(pressure.Modifier.ExecuteProgressionAction(
                    "触发压力夹具", amount: 30).Succeeded, Is.True);
                Assert.That(pressure.Modifier.ExecuteProgressionAction(
                    "完成压力夹具", amount: 60).Succeeded, Is.True);
                Assert.That(pressure.Pressure.Capture().Entries.Last().State,
                    Is.EqualTo(AttentionPressureState.Completed));
                Assert.That(pressure.Modifier.ExecuteProgressionAction(
                    "设置晶壳母体已击败").Succeeded, Is.True);
                Assert.That(pressure.Pressure.Capture()
                    .CrystalBroodmotherDefeated, Is.True);
                GrayboxDeveloperProgressionQuery3D query =
                    pressure.Modifier.QueryProgression();
                Assert.That(query.PressureQueue,
                    Has.Some.Contains("已完成"));
                Assert.That(query.PressureQueue,
                    Has.None.Contains("Completed"));
                Assert.That(pressure.Modifier.ExecuteProgressionAction(
                    "清除晶壳母体击败状态").Succeeded, Is.True);
                Assert.That(pressure.Pressure.Capture()
                    .CrystalBroodmotherDefeated, Is.False);
                Assert.That(pressure.Modifier.ExecuteProgressionAction(
                    "重置压力夹具").Succeeded, Is.True);
                Assert.That(pressure.Pressure.Capture().Entries, Is.Empty);
            }

            using (Fixture ascension = Create(
                       FormalFateCatalog.PocketUniverseId))
            {
                Assert.That(ascension.Modifier.ExecuteProgressionAction(
                    "执行首次文明升阶").Succeeded, Is.True);
                Assert.That(ascension.Fate.Capture().Level, Is.EqualTo(2));
                Assert.That(ascension.Civilization.Capture()
                    .CivilizationLevel, Is.EqualTo(2));
                GrayboxDeveloperProgressionQuery3D query =
                    ascension.Modifier.QueryProgression();
                Assert.That(query.CommittedIds.Distinct().Count(),
                    Is.EqualTo(query.CommittedIds.Count));
                Assert.That(query.CommittedIds,
                    Does.Contain("first-civilization-ascension"));
                Assert.That(query.ConfigurationSignature,
                    Is.EqualTo(
                        FormalThreeDProgressionSaveData.ConfigurationSignature));
            }
        }

        [Test]
        public void IDEA0020_AnchorDelegatesMarkOnlySuccessfulChanges()
        {
            var createCount = 0;
            var readCount = 0;
            var clearCount = 0;
            using (Fixture fixture = Create(
                FormalFateCatalog.RewindAnchorId,
                () => ++createCount == 1,
                _ => ++readCount == 1,
                () => ++clearCount == 1))
            {
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "创建回溯锚点").Succeeded, Is.True);
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "读取指定回溯锚点",
                    GrayboxRewindAnchorService3D.StableAnchorId).Succeeded,
                    Is.True);
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "清理全部回溯锚点").Succeeded, Is.True);
                Assert.That(createCount, Is.EqualTo(1));
                Assert.That(readCount, Is.EqualTo(1));
                Assert.That(clearCount, Is.EqualTo(1));
                Assert.That(fixture.Modifier.HasModifiedGameState, Is.True);
            }
        }

        [Test]
        public void IDEA0020_AscensionRequirementFixtureDelegatesRespectNoChange()
        {
            bool satisfied = false;
            using (Fixture fixture = Create(
                satisfyRequirements: () =>
                {
                    if (satisfied) return false;
                    satisfied = true;
                    return true;
                },
                clearRequirements: () =>
                {
                    if (!satisfied) return false;
                    satisfied = false;
                    return true;
                }))
            {
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "满足升阶测试条件").Succeeded, Is.True);
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "满足升阶测试条件").Code,
                    Is.EqualTo(GrayboxDeveloperCommandCode3D.NoChange));
                Assert.That(fixture.Modifier.ExecuteProgressionAction(
                    "清除升阶测试条件").Succeeded, Is.True);
                Assert.That(satisfied, Is.False);
            }
        }

        [Test]
        public void IDEA0020_HostFixtureCommandsSynchronizeDefenseAndResetOverrides()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxFormalSaveRuntimeHost3D.cs"));
            Assert.That(source, Does.Contain(
                "SatisfyAscensionRequirementsForDevelopment"));
            Assert.That(source, Does.Contain(
                "ClearAscensionRequirementsForDevelopment"));
            string pressure = ExtractMethod(
                source,
                "private bool TryRestorePressureFixtureForDevelopment(");
            int clearDefense = pressure.IndexOf(
                "defense.Runtime.ClearActivePressure()",
                StringComparison.Ordinal);
            int restorePressure = pressure.IndexOf(
                "AttentionPressureRuntime.TryRestore(candidate",
                StringComparison.Ordinal);
            Assert.That(clearDefense, Is.GreaterThanOrEqualTo(0));
            Assert.That(restorePressure, Is.GreaterThan(clearDefense));
            Assert.That(source.Split(new[]
            {
                "developmentRequirementsOverride = null;",
            }, StringSplitOptions.None).Length - 1,
                Is.GreaterThanOrEqualTo(2),
                "New progress and Continue must clear session-only overrides.");
        }

        private static string ExtractMethod(string source, string signature)
        {
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
            int open = source.IndexOf('{', start);
            var depth = 0;
            for (var index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            Assert.Fail("Incomplete method: " + signature);
            return string.Empty;
        }

        private static Fixture Create(
            string selectedFate = null,
            Func<bool> createAnchor = null,
            Func<string, bool> readAnchor = null,
            Func<bool> clearAnchors = null,
            Func<bool> satisfyRequirements = null,
            Func<bool> clearRequirements = null)
        {
            var root = new GameObject("Developer.Progression.Test");
            root.SetActive(false);
            var session = root.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            var city = root.AddComponent<GrayboxMobileCityController3D>();
            var presentation = root.AddComponent<GrayboxBuildingWorldView3D>();
            var modifier = new GrayboxDeveloperModifier3D(
                session, city, presentation);
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            var civilization = new FormalCivilizationAscensionRuntime();
            if (!string.IsNullOrEmpty(selectedFate))
            {
                Assert.That(fate.TrySelect(
                    selectedFate, out _, out _, out string error), Is.True,
                    error);
                Assert.That(civilization.TryBindFate(
                    selectedFate, out error), Is.True, error);
            }
            var pocket = new PocketUniverseFateEffect();
            var debt = new FormalVoidDebtRuntime();
            var rewind = new FormalRewindAnchorMetadataRuntime();
            var pressure = new AttentionPressureRuntime();
            var sequence = new AdvancementSequenceModel();
            var quantum = new QuantumEntanglementRuntime(new[]
            {
                ResourceIds.Iron,
            });
            var spatial = new SpatialTemplateRuntime();
            var haste = new LocalHasteRuntime();
            var foresight = new ForesightDelayRuntime();
            var causal = new CausalTransparencyRuntime();
            var chest = new VoidChestRuntime();
            var coordinate = new CoordinateLockRuntime(attention, pressure);
            var facade = new GrayboxDeveloperProgressionFacade3D(
                attention,
                fate,
                civilization,
                pocket,
                debt,
                rewind,
                pressure,
                sequence,
                createAnchor,
                readAnchor,
                clearAnchors,
                null,
                null,
                null,
                null,
                null,
                satisfyRequirements,
                clearRequirements,
                quantum,
                spatial,
                haste,
                foresight,
                causal,
                chest,
                coordinate);
            modifier.ConfigureProgressionFacade(facade);
            return new Fixture(
                root,
                modifier,
                attention,
                fate,
                civilization,
                debt,
                pressure,
                quantum,
                spatial,
                haste,
                foresight,
                causal,
                chest,
                coordinate);
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(
                GameObject root,
                GrayboxDeveloperModifier3D modifier,
                FormalAttentionRuntime attention,
                FormalFateRuntime fate,
                FormalCivilizationAscensionRuntime civilization,
                FormalVoidDebtRuntime debt,
                AttentionPressureRuntime pressure,
                QuantumEntanglementRuntime quantum,
                SpatialTemplateRuntime spatial,
                LocalHasteRuntime haste,
                ForesightDelayRuntime foresight,
                CausalTransparencyRuntime causal,
                VoidChestRuntime chest,
                CoordinateLockRuntime coordinate)
            {
                Root = root;
                Modifier = modifier;
                Attention = attention;
                Fate = fate;
                Civilization = civilization;
                Debt = debt;
                Pressure = pressure;
                Quantum = quantum;
                Spatial = spatial;
                Haste = haste;
                Foresight = foresight;
                Causal = causal;
                Chest = chest;
                Coordinate = coordinate;
            }

            public GameObject Root { get; }
            public GrayboxDeveloperModifier3D Modifier { get; }
            public FormalAttentionRuntime Attention { get; }
            public FormalFateRuntime Fate { get; }
            public FormalCivilizationAscensionRuntime Civilization { get; }
            public FormalVoidDebtRuntime Debt { get; }
            public AttentionPressureRuntime Pressure { get; }
            public QuantumEntanglementRuntime Quantum { get; }
            public SpatialTemplateRuntime Spatial { get; }
            public LocalHasteRuntime Haste { get; }
            public ForesightDelayRuntime Foresight { get; }
            public CausalTransparencyRuntime Causal { get; }
            public VoidChestRuntime Chest { get; }
            public CoordinateLockRuntime Coordinate { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
#endif
