using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxAttentionPressureRuntimeControllerTests
    {
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxAttentionPressureRuntimeController3D";

        [Test]
        public void IDEA0020_ExplicitOwnersAndBindSeedsExistingSnapshotOnce()
        {
            var attention = new FormalAttentionRuntime();
            Assert.That(attention.TryApply(
                "core.attention.civilization.advanced",
                "pressure.prebound.30",
                out _), Is.True);
            var pressure = new AttentionPressureRuntime();
            using (ControllerFixture fixture = Create(attention, pressure))
            {
                Assert.That(Thresholds(pressure), Is.Empty);
                Bind(fixture.Controller);
                Assert.That(Thresholds(pressure), Is.EqualTo(new[] { 30 }));
                AttentionPressureSnapshot seeded = pressure.Capture();

                Bind(fixture.Controller);
                Assert.That(pressure.Capture(), Is.SameAs(seeded));
                FormalAttentionSnapshot restored = attention.Capture();
                Assert.That(attention.TryRestore(restored, out string error),
                    Is.True, error);
                Assert.That(pressure.Capture(), Is.SameAs(seeded),
                    "Attention restore does not publish or replay thresholds.");
            }
        }

        [Test]
        public void IDEA0020_LiveThresholdEventsQueueOnceAndDisposeUnsubscribes()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            ControllerFixture fixture = Create(attention, pressure);
            Bind(fixture.Controller);
            ApplyCivilization(attention, 1);
            ApplyCivilization(attention, 2);
            ApplyCivilization(attention, 3);
            ApplyCivilization(attention, 4);
            Assert.That(Thresholds(pressure), Is.EqualTo(new[] { 30, 60, 90 }));
            AttentionPressureSnapshot queued = pressure.Capture();
            ApplyCivilization(attention, 5);
            Assert.That(pressure.Capture(), Is.SameAs(queued));

            fixture.Dispose();
            var freshAttention = new FormalAttentionRuntime();
            var freshPressure = new AttentionPressureRuntime();
            ControllerFixture disposed = Create(freshAttention, freshPressure);
            Bind(disposed.Controller);
            disposed.Dispose();
            ApplyCivilization(freshAttention, 1);
            Assert.That(Thresholds(freshPressure), Is.Empty);
            disposed.Dispose();
        }

        [Test]
        public void IDEA0020_WarningIsolatedAndStartGoesOnlyToDefenseController()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = new AttentionPressureRuntime();
            pressure.TryQueueThreshold(30, out _);
            GrayboxDefenseRuntime3D defense = DefenseWithWonMainCampaign();
            using (ControllerFixture fixture = Create(
                       attention,
                       pressure,
                       defense))
            {
                Bind(fixture.Controller);
                var observed = 0;
                AddWarningHandler(fixture.Controller, command =>
                {
                    observed++;
                    throw new InvalidOperationException("subscriber failure");
                });
                AddWarningHandler(fixture.Controller, command => observed++);

                Assert.That(Tick(
                    fixture.Controller,
                    .1f,
                    mainCampaignActive: false,
                    tutorialCompleted: true,
                    firstTower: true,
                    out string error), Is.True, error);
                Assert.That(observed, Is.EqualTo(2));
                Assert.That(Read<Exception>(
                        fixture.Controller,
                        "LastWarningNotificationFailure"),
                    Is.Not.Null);
                Assert.That(pressure.Capture().Entries.Single().State,
                    Is.EqualTo(AttentionPressureState.Warning));

                Assert.That(Tick(
                    fixture.Controller,
                    60f,
                    false,
                    true,
                    true,
                    out error), Is.True, error);
                Assert.That(defense.HasActivePressureCampaign, Is.True,
                    "StartEncounterRequested is handed only to the defense " +
                    "adapter; this coordinator does not create enemies.");
                Assert.That(pressure.Capture().Entries.Single().State,
                    Is.EqualTo(AttentionPressureState.Active));
            }
        }

        [Test]
        public void IDEA0020_MainCampaignAndZeroDeltaLeavePressureSnapshotStatic()
        {
            var pressure = new AttentionPressureRuntime();
            pressure.TryQueueThreshold(30, out _);
            using (ControllerFixture fixture = Create(
                       new FormalAttentionRuntime(),
                       pressure))
            {
                Bind(fixture.Controller);
                AttentionPressureSnapshot before = pressure.Capture();
                Assert.That(Tick(
                    fixture.Controller,
                    100f,
                    true,
                    true,
                    true,
                    out _), Is.True);
                Assert.That(pressure.Capture(), Is.SameAs(before));
                Assert.That(Tick(
                    fixture.Controller,
                    0f,
                    false,
                    true,
                    true,
                    out _), Is.True);
                Assert.That(pressure.Capture(), Is.SameAs(before));
            }
        }

        private static ControllerFixture Create(
            FormalAttentionRuntime attention,
            AttentionPressureRuntime pressure,
            GrayboxDefenseRuntime3D defense = null)
        {
            Type type = RequireControllerType();
            defense ??= DefenseWithWonMainCampaign();
            var defenseController =
                new GrayboxAttentionPressureDefenseController3D(
                    pressure,
                    defense);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(AttentionPressureRuntime),
                typeof(GrayboxAttentionPressureDefenseController3D),
            });
            Assert.That(constructor, Is.Not.Null);
            object controller = constructor.Invoke(new object[]
            {
                attention,
                pressure,
                defenseController,
            });
            Assert.That(controller, Is.InstanceOf<IDisposable>());
            return new ControllerFixture(controller, defenseController);
        }

        private static void Bind(object controller)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Bind",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static bool Tick(
            object controller,
            float delta,
            bool mainCampaignActive,
            bool tutorialCompleted,
            bool firstTower,
            out string error)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Tick",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(float), typeof(bool), typeof(bool), typeof(bool),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments =
            {
                delta, mainCampaignActive, tutorialCompleted, firstTower, null,
            };
            bool result = (bool)method.Invoke(controller, arguments);
            error = arguments[4] as string;
            return result;
        }

        private static void AddWarningHandler(
            object controller,
            Action<AttentionPressureCommand> handler)
        {
            EventInfo warning = controller.GetType().GetEvent(
                "WarningStarted",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(warning, Is.Not.Null);
            warning.AddEventHandler(controller, handler);
        }

        private static void ApplyCivilization(
            FormalAttentionRuntime attention,
            int ordinal)
        {
            Assert.That(attention.TryApply(
                "core.attention.civilization.advanced",
                "pressure.civilization." + ordinal,
                out string error), Is.True, error);
        }

        private static int[] Thresholds(AttentionPressureRuntime pressure) =>
            pressure.Capture().Entries.Select(value => value.Threshold).ToArray();

        private static T Read<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(owner);
        }

        private static GrayboxDefenseRuntime3D DefenseWithWonMainCampaign()
        {
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f);
            var definition = new SingleCityDefenseCampaignDefinition(
                "test.pressure.main",
                new CampaignWaveDefinition(
                    1,
                    0f,
                    .1f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(WasteCity.Combat.EnemyArchetype.Gnawer, 1)));
            var campaign = new SingleCityDefenseCampaignModel(
                0f,
                0f,
                definition);
            campaign.TryStartAfterExternalWarning();
            campaign.Advance(.2f, 1);
            campaign.DefeatEnemy(
                campaign.Snapshot.Enemies.Single().StableId,
                BuildingCatalog.MachineGunTurret.Id.Value);
            campaign.Advance(.1f, 1);
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "campaign",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            field.SetValue(defense, campaign);
            return defense;
        }

        private static Type RequireControllerType()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    ControllerTypeName,
                    false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, ControllerTypeName);
            return type;
        }

        private sealed class ControllerFixture : IDisposable
        {
            private readonly GrayboxAttentionPressureDefenseController3D
                defenseController;
            private bool disposed;

            public ControllerFixture(
                object controller,
                GrayboxAttentionPressureDefenseController3D defenseController)
            {
                Controller = controller;
                this.defenseController = defenseController;
            }

            public object Controller { get; }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                ((IDisposable)Controller).Dispose();
                defenseController.Dispose();
            }
        }
    }
}
