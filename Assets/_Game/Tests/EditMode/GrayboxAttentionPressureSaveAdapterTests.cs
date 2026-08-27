using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxAttentionPressureSaveAdapterTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxAttentionPressureSaveAdapter3D";

        [Test]
        public void IDEA0020_EmptyCaptureOwnsExplicitPressureDto()
        {
            var pressure = new AttentionPressureRuntime();
            GrayboxDefenseRuntime3D defense = WonDefense();
            object adapter = CreateAdapter(pressure, defense);
            object data = Capture(adapter);
            Assert.That(Sequence(data, "entries"), Is.Empty);
            Assert.That(ReadField<string>(data, "activeEncounterId"), Is.Empty);
            Assert.That(ReadField<object>(data, "activeCampaign"), Is.Null);
        }

        [Test]
        public void IDEA0020_ActiveCampaignRoundTripsPressureAndDefenseAtomically()
        {
            CreateActive(out AttentionPressureRuntime sourcePressure,
                out GrayboxDefenseRuntime3D sourceDefense);
            InjectReinforcementAndAdvance(sourceDefense);
            object sourceAdapter = CreateAdapter(sourcePressure, sourceDefense);
            object data = Capture(sourceAdapter);
            object campaign = ReadField<object>(data, "activeCampaign");
            Assert.That(campaign, Is.Not.Null);
            Assert.That(ReadField<string>(data, "activeEncounterId"),
                Is.EqualTo(
                    "core.attention-encounter.directional-attack"));
            Assert.That(Sequence(campaign, "injectedReinforcements"),
                Has.Length.EqualTo(1));
            Assert.That(Sequence(campaign, "enemyStates"), Is.Not.Empty);
            Assert.That(ReadField<object>(campaign, "statistics"), Is.Not.Null);

            var targetPressure = new AttentionPressureRuntime();
            GrayboxDefenseRuntime3D targetDefense = WonDefense();
            object targetAdapter = CreateAdapter(targetPressure, targetDefense);
            AttentionPressureSnapshot pressureBefore = targetPressure.Capture();
            Assert.That(Prepare(
                targetAdapter,
                data,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(targetPressure.Capture(), Is.SameAs(pressureBefore));
            Assert.That(targetDefense.HasActivePressureCampaign, Is.False);
            Assert.That(Commit(
                targetAdapter,
                plan,
                out string commitError), Is.True, commitError);
            Assert.That(targetPressure.Capture().Entries.Single().State,
                Is.EqualTo(AttentionPressureState.Active));
            Assert.That(targetDefense.ActivePressureEncounterId,
                Is.EqualTo(sourceDefense.ActivePressureEncounterId));
            Assert.That(JsonUtility.ToJson(Capture(targetAdapter)),
                Is.EqualTo(JsonUtility.ToJson(data)));
        }

        [Test]
        public void IDEA0020_CrossDomainMismatchFailsPrepareWithoutWrites()
        {
            CreateActive(out AttentionPressureRuntime sourcePressure,
                out GrayboxDefenseRuntime3D sourceDefense);
            object data = Capture(CreateAdapter(sourcePressure, sourceDefense));
            WriteField(data, "activeEncounterId",
                "core.attention-encounter.high-risk-attack");

            var targetPressure = new AttentionPressureRuntime();
            GrayboxDefenseRuntime3D targetDefense = WonDefense();
            object target = CreateAdapter(targetPressure, targetDefense);
            AttentionPressureSnapshot before = targetPressure.Capture();
            Assert.That(Prepare(target, data, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(targetPressure.Capture(), Is.SameAs(before));
            Assert.That(targetDefense.HasActivePressureCampaign, Is.False);
        }

        [Test]
        public void IDEA0020_PlanIsOwnerRevisionBoundAndSingleUse()
        {
            CreateActive(out AttentionPressureRuntime sourcePressure,
                out GrayboxDefenseRuntime3D sourceDefense);
            object data = Capture(CreateAdapter(sourcePressure, sourceDefense));
            var pressure = new AttentionPressureRuntime();
            object adapter = CreateAdapter(pressure, WonDefense());
            Assert.That(Prepare(adapter, data, out object stale, out _), Is.True);
            pressure.TryQueueThreshold(60, out _);
            AttentionPressureSnapshot changed = pressure.Capture();
            Assert.That(Commit(adapter, stale, out _), Is.False);
            Assert.That(pressure.Capture(), Is.SameAs(changed));

            var ownerPressure = new AttentionPressureRuntime();
            object owner = CreateAdapter(ownerPressure, WonDefense());
            Assert.That(Prepare(owner, data, out object plan, out _), Is.True);
            object foreign = CreateAdapter(
                new AttentionPressureRuntime(), WonDefense());
            Assert.That(Commit(foreign, plan, out _), Is.False);
            Assert.That(Commit(owner, plan, out string error), Is.True, error);
            Assert.That(Commit(owner, plan, out _), Is.False);
        }

        private static object CreateAdapter(
            AttentionPressureRuntime pressure,
            GrayboxDefenseRuntime3D defense)
        {
            Type type = RequireType(AdapterTypeName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(AttentionPressureRuntime),
                typeof(GrayboxDefenseRuntime3D),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { pressure, defense });
        }

        private static object Capture(object adapter)
        {
            MethodInfo method = adapter.GetType().GetMethod("Capture");
            Assert.That(method, Is.Not.Null);
            return method.Invoke(adapter, null);
        }

        private static bool Prepare(
            object adapter,
            object data,
            out object plan,
            out string error)
        {
            MethodInfo method = adapter.GetType().GetMethods()
                .Single(value => value.Name == "TryPrepareRestore");
            object[] arguments = { data, null, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            plan = arguments[1];
            error = arguments[2] as string;
            return result;
        }

        private static bool Commit(
            object adapter,
            object plan,
            out string error)
        {
            MethodInfo method = adapter.GetType().GetMethods()
                .Single(value => value.Name == "TryCommitRestore");
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(adapter, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static void CreateActive(
            out AttentionPressureRuntime pressure,
            out GrayboxDefenseRuntime3D defense)
        {
            pressure = new AttentionPressureRuntime();
            pressure.TryQueueThreshold(30, out _);
            pressure.Tick(.1f, false, true, true, out _, out _);
            pressure.Tick(60f, false, true, true,
                out AttentionPressureCommand command, out _);
            defense = WonDefense();
            var controller = new GrayboxAttentionPressureDefenseController3D(
                pressure, defense);
            Assert.That(controller.TryHandle(command, out string error),
                Is.True, error);
        }

        private static void InjectReinforcementAndAdvance(
            GrayboxDefenseRuntime3D defense)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "activePressureCampaign",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var campaign = (SingleCityDefenseCampaignModel)field.GetValue(defense);
            Assert.That(campaign.TryInjectReinforcements(
                "test.injected.reinforcement",
                new[] { new WaveEntry(EnemyArchetype.Gnawer, 2) }), Is.True);
            campaign.Advance(61f, 1);
        }

        private static GrayboxDefenseRuntime3D WonDefense()
        {
            var defense = new GrayboxDefenseRuntime3D(0f, 0f, 20, 0f);
            var definition = new SingleCityDefenseCampaignDefinition(
                "test.pressure.persistence.main",
                new CampaignWaveDefinition(1, 0f, .1f,
                    new[] { CampaignSpawnDirection.East },
                    new WaveEntry(EnemyArchetype.Gnawer, 1)));
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f,
                definition);
            campaign.TryStartAfterExternalWarning();
            campaign.Advance(.2f, 1);
            campaign.DefeatEnemy(campaign.Snapshot.Enemies.Single().StableId,
                BuildingCatalog.MachineGunTurret.Id.Value);
            campaign.Advance(.1f, 1);
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                "campaign", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(defense, campaign);
            return defense;
        }

        private static object[] Sequence(object owner, string fieldName)
        {
            return ((IEnumerable)ReadField<object>(owner, fieldName))
                .Cast<object>().ToArray();
        }

        private static T ReadField<T>(object owner, string fieldName)
        {
            FieldInfo field = owner.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null,
                owner.GetType().FullName + "." + fieldName);
            return (T)field.GetValue(owner);
        }

        private static void WriteField(
            object owner,
            string fieldName,
            object value)
        {
            FieldInfo field = owner.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null);
            field.SetValue(owner, value);
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
