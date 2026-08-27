using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Defense;

namespace WasteCity.Tests
{
    public sealed class CrystalBroodmotherEncounterTests
    {
        private const string EncounterTypeName =
            "WasteCity.Combat.CrystalBroodmotherEncounter";

        [Test]
        public void IDEA0020_AuthorityHealthObservationOwnsNoHealthAndNeverReplays()
        {
            object encounter = NewEncounter("boss.authority");
            Assert.That(Commands(Observe(encounter, "boss.authority", 4000, 4000)),
                Is.Empty);
            Assert.That(Commands(Observe(encounter, "boss.authority", 2801, 4000)),
                Is.Empty);
            Assert.That(Commands(Observe(encounter, "boss.authority", 3500, 4000)),
                Is.Empty, "An upward observation cannot rewrite authority.");
            object[] phase70 = Commands(Observe(
                encounter, "boss.authority", 2800, 4000));
            Assert.That(phase70, Has.Length.EqualTo(1));
            Assert.That(Read<string>(phase70[0], "StableCommandId"),
                Does.Contain("phase-70"));
            Assert.That(Commands(Observe(
                encounter, "boss.authority", 2800, 4000)), Is.Empty);
            Assert.That(Commands(Observe(
                encounter, "boss.authority", 3200, 4000)), Is.Empty);
            Assert.That(Commands(Observe(
                encounter, "boss.authority", 1400, 4000)),
                Has.Length.EqualTo(1));
            object[] defeated = Commands(Observe(
                encounter, "boss.authority", 0, 4000));
            Assert.That(defeated.Count(value =>
                Read<object>(value, "Kind").ToString() == "Defeated"),
                Is.EqualTo(1));
            Assert.That(Commands(Observe(
                encounter, "boss.authority", 0, 4000)), Is.Empty);
            Assert.That(Commands(Observe(
                encounter, "different-boss", 0, 4000)), Is.Empty);

            Type snapshot = Capture(encounter).GetType();
            Assert.That(snapshot.GetProperty("CurrentHealth"), Is.Null);
            Assert.That(snapshot.GetProperty("FixedStepAccumulatorSeconds"),
                Is.Null);
        }

        [Test]
        public void IDEA0020_FixedStepsEmitEachPhaseAndDefeatCommandOnce()
        {
            object encounter = NewEncounter("boss.instance.000001");
            Assert.That(Commands(Tick(encounter, .3f, false, 100)), Is.Empty);
            Assert.That(FixtureHealth(encounter), Is.EqualTo(3700));

            object[] phase70 = Commands(Tick(encounter, 1f, false, 100));
            Assert.That(phase70, Has.Length.EqualTo(1));
            Assert.That(Read<object>(phase70[0], "Kind").ToString(),
                Is.EqualTo("SpawnReinforcements"));
            Assert.That(Read<string>(phase70[0], "StableCommandId"),
                Does.Contain("phase-70"));

            object[] phase35 = Commands(Tick(encounter, 1.4f, false, 100));
            Assert.That(phase35, Has.Length.EqualTo(1));
            Assert.That(Read<string>(phase35[0], "StableCommandId"),
                Does.Contain("phase-35"));

            object[] defeated = Commands(Tick(encounter, 2f, false, 100));
            Assert.That(defeated.Count(value =>
                Read<object>(value, "Kind").ToString() == "Defeated"),
                Is.EqualTo(1));
            Assert.That(Commands(Tick(encounter, 10f, false, 100)), Is.Empty);
        }

        [Test]
        public void IDEA0020_PauseDoesNothingAndChunkingIsDeterministic()
        {
            object paused = NewEncounter("boss.pause");
            object before = Capture(paused);
            Assert.That(Commands(Tick(paused, 10f, true, 500)), Is.Empty);
            Assert.That(Capture(paused), Is.SameAs(before));

            object one = NewEncounter("boss.chunk.one");
            object many = NewEncounter("boss.chunk.many");
            Tick(one, .3f, false, 75);
            Tick(many, .1f, false, 75);
            Tick(many, .1f, false, 75);
            Tick(many, .1f, false, 75);
            Assert.That(FixtureHealth(one), Is.EqualTo(FixtureHealth(many)));
        }

        [Test]
        public void IDEA0020_RestoreIsAtomicAndCaptureIsStableZeroAlloc()
        {
            object source = NewEncounter("boss.restore");
            Tick(source, 1.3f, false, 100);
            object snapshot = Capture(source);
            object target = NewEncounter("boss.restore");
            Assert.That(TryRestore(target, snapshot, out string error),
                Is.True, error);
            Assert.That(Read<bool>(Capture(target), "Phase70Triggered"),
                Is.EqualTo(Read<bool>(snapshot, "Phase70Triggered")));

            object invalid = NewInvalidSnapshot(snapshot);
            object before = Capture(target);
            Assert.That(TryRestore(target, invalid, out error), Is.False);
            Assert.That(Capture(target), Is.SameAs(before));

            var typed = new CrystalBroodmotherEncounter("boss.capture");
            CrystalBroodmotherSnapshot stable = typed.Capture();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
            {
                if (!ReferenceEquals(typed.Capture(), stable))
                    Assert.Fail("Capture must remain cached without changes.");
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void IDEA0020_CampaignInjectionPersistsAndDefeatFactIsUnique()
        {
            var definition = new SingleCityDefenseCampaignDefinition(
                "pressure.broodmother.test",
                new CampaignWaveDefinition(
                    1, 0f, .1f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(EnemyArchetype.CrystalBroodmother, 1)));
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f,
                definition);
            Assert.That(campaign.TryStartAfterExternalWarning(), Is.True);
            campaign.Advance(1f, 1);
            string bossId = campaign.Snapshot.Enemies.Single().StableId;
            var encounter = new CrystalBroodmotherEncounter(bossId);
            CrystalBroodmotherCommand phase = encounter
                .ObserveAuthorityHealth(bossId, 2800, 4000).Single();
            var entries = phase.Reinforcements.Select(value =>
                new WaveEntry(value.Archetype, value.Count)).ToArray();

            Assert.That(campaign.TryInjectReinforcements(
                phase.StableCommandId, entries), Is.True);
            Assert.That(campaign.TryInjectReinforcements(
                phase.StableCommandId, entries), Is.False);
            Assert.That(campaign.Snapshot.PlannedEnemyCount, Is.EqualTo(5));
            SingleCityDefenseCampaignPersistenceState saved =
                campaign.CaptureForPersistence();
            Assert.That(saved.InjectedReinforcements, Has.Count.EqualTo(1));

            var restored = new SingleCityDefenseCampaignModel(0f, 0f,
                definition);
            Assert.That(restored.TryPrepareRestore(
                saved,
                out SingleCityDefenseCampaignRestorePlan restorePlan,
                out string error), Is.True, error);
            Assert.That(restored.TryCommitRestore(restorePlan, out error),
                Is.True, error);
            Assert.That(restored.Snapshot.PlannedEnemyCount, Is.EqualTo(5));
            Assert.That(restored.TryInjectReinforcements(
                phase.StableCommandId, entries), Is.False);

            var defeatedFacts = 0;
            restored.EnemyDefeated += (_, enemyId) =>
            {
                if (enemyId == CrystalBroodmotherCatalog.StableArchetypeId)
                    defeatedFacts++;
            };
            Assert.That(restored.DefeatEnemy(
                bossId, "core.building.machine-gun-turret"), Is.True);
            Assert.That(restored.DefeatEnemy(
                bossId, "core.building.machine-gun-turret"), Is.False);
            Assert.That(defeatedFacts, Is.EqualTo(1));
        }

        private static object NewEncounter(string stableId) =>
            Activator.CreateInstance(RequireType(EncounterTypeName), stableId);

        private static object Tick(
            object encounter,
            float delta,
            bool paused,
            int damage)
        {
            return encounter.GetType().GetMethod("Tick")?.Invoke(
                encounter,
                new object[] { delta, paused, damage });
        }

        private static object Capture(object encounter) =>
            encounter.GetType().GetMethod("Capture")?.Invoke(encounter, null);

        private static object Observe(
            object encounter,
            string stableBossId,
            int currentHealth,
            int maximumHealth)
        {
            MethodInfo method = encounter.GetType().GetMethod(
                "ObserveAuthorityHealth",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(encounter, new object[]
            {
                stableBossId,
                currentHealth,
                maximumHealth,
            });
        }

        private static object[] Commands(object value) =>
            ((IEnumerable)value).Cast<object>().ToArray();

        private static bool TryRestore(
            object encounter,
            object snapshot,
            out string error)
        {
            MethodInfo method = encounter.GetType().GetMethod("TryRestore");
            object[] arguments = { snapshot, null };
            bool result = (bool)method.Invoke(encounter, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static object NewInvalidSnapshot(object snapshot)
        {
            Type type = snapshot.GetType();
            ConstructorInfo constructor = type.GetConstructors().Single();
            ParameterInfo[] parameters = constructor.GetParameters();
            object[] values = new object[parameters.Length];
            for (var index = 0; index < values.Length; index++)
            {
                PropertyInfo property = type.GetProperty(
                    char.ToUpperInvariant(parameters[index].Name[0]) +
                    parameters[index].Name.Substring(1));
                values[index] = property?.GetValue(snapshot) ??
                    (parameters[index].ParameterType.IsValueType
                        ? Activator.CreateInstance(parameters[index].ParameterType)
                        : null);
            }
            int phase70 = Array.FindIndex(parameters, value =>
                value.Name == "phase70Triggered");
            int phase35 = Array.FindIndex(parameters, value =>
                value.Name == "phase35Triggered");
            values[phase70] = false;
            values[phase35] = true;
            return constructor.Invoke(values);
        }

        private static int FixtureHealth(object encounter)
        {
            FieldInfo field = encounter.GetType().GetField(
                "fixtureHealth",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (int)field.GetValue(encounter);
        }

        private static T Read<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(owner);
        }

        private static Type RequireType(string name)
        {
            Type type = typeof(EnemyCatalog).Assembly.GetType(name, false);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }
    }
}
