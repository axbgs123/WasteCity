using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxPocketUniverseCollapseResolverTests
    {
        private const string ResolverTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxPocketUniverseCollapseResolver3D";

        [Test]
        public void IDEA0020_ResolverUsesFormalDamageWithoutSceneScanning()
        {
            FieldInfo damage = typeof(FormalFateCatalog).GetField(
                "PocketUniverseCollapseDamage",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(damage, Is.Not.Null);
            Assert.That(damage.IsLiteral, Is.True);
            Assert.That(damage.GetRawConstantValue(), Is.EqualTo(150));

            Type resolver = RequireResolverType();
            Assert.That(resolver.GetConstructors(), Has.Length.EqualTo(1));
            string source = File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxPocketUniverseCollapseResolver3D.cs")));
            StringAssert.DoesNotContain("FindObject", source);
            StringAssert.DoesNotContain("GameObject", source);
            StringAssert.Contains("BuildingOrientationRules.Width", source);
            StringAssert.Contains("BuildingOrientationRules.Height", source);
        }

        [Test]
        public void IDEA0020_IntersectingOthersTakeSortedDamageAndDestructionCommits()
        {
            using (Fixture fixture = new Fixture())
            {
                object resolver = fixture.CreateResolver();
                PocketUniverseCollapseCommand command =
                    fixture.CreateCommand(level: 1);

                object result = Resolve(resolver, command);

                AssertResult(result, true, "Applied");
                Assert.That(Strings(result, "DamagedStableInstanceIds"),
                    Is.EqualTo(new[]
                    {
                        fixture.FragileId,
                        fixture.WallId,
                    }),
                    "Affected buildings must be processed by stable ID.");
                Assert.That(Strings(result, "DestroyedStableInstanceIds"),
                    Is.EqualTo(new[] { fixture.FragileId }));
                Assert.That(Read<int>(result, "DamagePerBuilding"),
                    Is.EqualTo(150));
                AssertHealth(fixture.Health, fixture.WallId, 150, false);
                Assert.That(fixture.Find(fixture.FragileId).State,
                    Is.EqualTo(GrayboxBuildingInstanceState.DestroyedRuin));
                AssertHealth(fixture.Health, fixture.SourceId, 280, false);
                AssertHealth(fixture.Health, fixture.SafeId, 300, false);
                Assert.That(fixture.Find(fixture.LockedId).IsEvacuationLocked,
                    Is.True);
            }
        }

        [Test]
        public void IDEA0020_DuplicateCommandIsIdempotentAndLevelTwoUsesFourByFour()
        {
            using (Fixture fixture = new Fixture())
            {
                object resolver = fixture.CreateResolver();
                PocketUniverseCollapseCommand first =
                    fixture.CreateCommand(level: 1);
                AssertResult(Resolve(resolver, first), true, "Applied");
                AssertHealth(fixture.Health, fixture.WallId, 150, false);

                object duplicate = Resolve(resolver, first);

                AssertResult(duplicate, true, "AlreadyResolved");
                AssertHealth(fixture.Health, fixture.WallId, 150, false);
            }

            using (Fixture fixture = new Fixture())
            {
                object resolver = fixture.CreateResolver();
                PocketUniverseCollapseCommand levelTwo =
                    fixture.CreateCommand(level: 2);
                object result = Resolve(resolver, levelTwo);

                AssertResult(result, true, "Applied");
                Assert.That(Strings(result, "DamagedStableInstanceIds"),
                    Does.Contain(fixture.SafeId),
                    "The formal 4x4 region must include the next east cell.");
                AssertHealth(fixture.Health, fixture.SafeId, 150, false);
            }
        }

        [Test]
        public void IDEA0020_CommittedFlagshipDestructionTriggersCollapseOnce()
        {
            using (Fixture fixture = new Fixture())
            {
                var fate = new FormalFateRuntime();
                Assert.That(fate.TrySelect(
                    FormalFateCatalog.PocketUniverseId,
                    out _,
                    out _,
                    out string error), Is.True, error);
                var effect = new PocketUniverseFateEffect();
                var controller = new GrayboxPocketUniverseFateController3D(
                    fate,
                    new FormalAttentionRuntime(),
                    effect);
                var defenseObject = new GameObject("Collapse.Defense");
                try
                {
                    GrayboxDefenseController3D defense =
                        defenseObject.AddComponent<GrayboxDefenseController3D>();
                    SetPrivate(defense, "buildingHealth", fixture.Health);
                    SetPrivate(
                        defense,
                        "destructionCoordinator",
                        fixture.Destruction);
                    Bind(
                        controller,
                        fixture.Session,
                        new GrayboxProductionClock3D(),
                        defense);
                    Assert.That(effect.Capture().Flagships.Select(value =>
                            value.StableInstanceId),
                        Does.Contain(fixture.SourceId));

                    Assert.That(fixture.Health.TryApplyDamage(
                        fixture.SourceId,
                        1000,
                        out _,
                        out bool destroyed), Is.True);
                    Assert.That(destroyed, Is.True);
                    GrayboxCombatDestructionResult3D committed =
                        fixture.Destruction.Commit(fixture.SourceId);
                    Assert.That(committed.CommittedNow, Is.True);

                    AssertHealth(fixture.Health, fixture.WallId, 150, false);
                    Assert.That(fixture.Find(fixture.FragileId).State,
                        Is.EqualTo(
                            GrayboxBuildingInstanceState.DestroyedRuin));
                    Assert.That(effect.Capture().CollapsedFlagshipIds,
                        Is.EqualTo(new[] { fixture.SourceId }));
                    object collapse = controller.GetType().GetProperty(
                            "LastCollapseResult")
                        ?.GetValue(controller);
                    Assert.That(collapse, Is.Not.Null);

                    fixture.Destruction.Commit(fixture.SourceId);
                    AssertHealth(fixture.Health, fixture.WallId, 150, false);
                }
                finally
                {
                    controller.Dispose();
                    UnityEngine.Object.DestroyImmediate(defenseObject);
                }
            }
        }

        private static object Resolve(
            object resolver,
            PocketUniverseCollapseCommand command)
        {
            MethodInfo method = resolver.GetType().GetMethod(
                "Resolve",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(PocketUniverseCollapseCommand) },
                null);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(resolver, new object[] { command });
        }

        private static void Bind(
            GrayboxPocketUniverseFateController3D controller,
            GrayboxBuildingSession3D session,
            GrayboxProductionClock3D clock,
            GrayboxDefenseController3D defense)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Bind",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(GrayboxBuildingSession3D),
                    typeof(GrayboxProductionClock3D),
                    typeof(GrayboxDefenseController3D),
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { session, clock, defense });
        }

        private static void AssertResult(
            object result,
            bool success,
            string status)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(Read<bool>(result, "Success"), Is.EqualTo(success));
            Assert.That(Read<object>(result, "Status").ToString(),
                Is.EqualTo(status));
        }

        private static T Read<T>(object owner, string property)
        {
            PropertyInfo info = owner.GetType().GetProperty(
                property,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, property);
            return (T)info.GetValue(owner);
        }

        private static string[] Strings(object owner, string property)
        {
            return ((IEnumerable)Read<object>(owner, property))
                .Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
        }

        private static Type RequireResolverType()
        {
            Type type = typeof(GrayboxDefenseController3D).Assembly.GetType(
                ResolverTypeName,
                false);
            Assert.That(type, Is.Not.Null, ResolverTypeName);
            return type;
        }

        private static void AssertHealth(
            GrayboxBuildingHealthRuntime3D health,
            string stableId,
            int current,
            bool destroyed)
        {
            Assert.That(health.TryGetHealth(
                stableId,
                out int actual,
                out _,
                out bool actualDestroyed), Is.True, stableId);
            Assert.That(actual, Is.EqualTo(current), stableId);
            Assert.That(actualDestroyed, Is.EqualTo(destroyed), stableId);
        }

        private static void SetPrivate(
            object owner,
            string name,
            object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(owner, value);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly PocketUniverseFateEffect commandEffect =
                new PocketUniverseFateEffect();

            public readonly string SourceId = "building.instance.000001";
            public readonly string FragileId = "building.instance.000002";
            public readonly string WallId = "building.instance.000010";
            public readonly string LockedId = "building.instance.000020";
            public readonly string SafeId = "building.instance.000030";

            public Fixture()
            {
                root = new GameObject("Collapse.Resolver.Test");
                Session = root.AddComponent<GrayboxBuildingSession3D>();
                Session.ConfigureDevelopmentFixture();
                Presentation = new PresentationStub();
                var fragile = new BuildingDefinition(
                    "test.building.fragile",
                    "脆弱设施",
                    1,
                    1,
                    null,
                    0,
                    maximumHealth: 100);
                GrayboxBuildingRestoreEntry3D[] entries =
                {
                    Entry(SourceId, BuildingCatalog.Smelter, 10, 10),
                    Entry(WallId, BuildingCatalog.Wall, 9, 9),
                    Entry(FragileId, fragile, 9, 10),
                    Entry(LockedId, BuildingCatalog.Wall, 9, 11,
                        locked: true),
                    Entry(SafeId, BuildingCatalog.Wall, 12, 10),
                };
                Assert.That(Session.TryRestoreBuildings(
                    entries,
                    31,
                    Presentation,
                    out string error), Is.True, error);
                Health = new GrayboxBuildingHealthRuntime3D();
                Health.Synchronize(Session.Instances);
                Production = new GrayboxProductionRuntime3D();
                Defense = new GrayboxDefenseRuntime3D(0f, 0f, 9f, 0f);
                Campaign = new SingleCityDefenseCampaignModel(0f, 0f);
                Destruction = new GrayboxCombatDestructionCoordinator3D(
                    Session,
                    Health,
                    Production,
                    Defense,
                    Campaign,
                    Presentation);
                Assert.That(commandEffect.SelectFlagships(new[]
                {
                    new PocketUniverseBuildingCandidate(
                        SourceId,
                        BuildingCatalog.Smelter.Id.Value,
                        true,
                        true),
                }), Is.EqualTo(1));
            }

            public GrayboxBuildingSession3D Session { get; }
            public PresentationStub Presentation { get; }
            public GrayboxBuildingHealthRuntime3D Health { get; }
            public GrayboxProductionRuntime3D Production { get; }
            public GrayboxDefenseRuntime3D Defense { get; }
            public SingleCityDefenseCampaignModel Campaign { get; }
            public GrayboxCombatDestructionCoordinator3D Destruction { get; }

            public object CreateResolver()
            {
                return Activator.CreateInstance(
                    RequireResolverType(),
                    Session,
                    Health,
                    Destruction);
            }

            public PocketUniverseCollapseCommand CreateCommand(int level)
            {
                Assert.That(commandEffect.TrySetLevel(level, out _), Is.True);
                Assert.That(commandEffect.TryCreateCollapseCommand(
                    SourceId,
                    10,
                    10,
                    out PocketUniverseCollapseCommand command), Is.True);
                return command;
            }

            public GrayboxBuildingInstance3D Find(string stableId)
            {
                return Session.Instances.First(value =>
                    value.StableInstanceId == stableId);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            private static GrayboxBuildingRestoreEntry3D Entry(
                string stableId,
                BuildingDefinition definition,
                int x,
                int y,
                bool locked = false)
            {
                return new GrayboxBuildingRestoreEntry3D(
                    stableId,
                    definition,
                    BuildingSite.Ground,
                    x,
                    y,
                    BuildingOrientation.North,
                    GrayboxBuildingInstanceState.Completed,
                    0f,
                    isPlayerOwned: true,
                    isEvacuationLocked: locked,
                    ResourceNodeBinding.None);
            }
        }

        public sealed class PresentationStub :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void RemoveInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
