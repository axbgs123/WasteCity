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
                Has.Count.EqualTo(24));
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
        [TestCase("选择虚空债命轨", "core.legacy.void-debt")]
        [TestCase("选择回溯锚点命轨", "core.legacy.rewind-anchor")]
        public void IDEA0020_ThreeFateSelectionActionsBindCivilization(
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
                clearRequirements);
            modifier.ConfigureProgressionFacade(facade);
            return new Fixture(
                root, modifier, attention, fate, civilization, debt, pressure);
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
                AttentionPressureRuntime pressure)
            {
                Root = root;
                Modifier = modifier;
                Attention = attention;
                Fate = fate;
                Civilization = civilization;
                Debt = debt;
                Pressure = pressure;
            }

            public GameObject Root { get; }
            public GrayboxDeveloperModifier3D Modifier { get; }
            public FormalAttentionRuntime Attention { get; }
            public FormalFateRuntime Fate { get; }
            public FormalCivilizationAscensionRuntime Civilization { get; }
            public FormalVoidDebtRuntime Debt { get; }
            public AttentionPressureRuntime Pressure { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
#endif
