using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxProgressionEventIntegrationTests
    {
        private const string RouterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxProgressionEventRouter3D";

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void IDEA0020_ControllerUsesExplicitOwnersAndAuthorityEvents()
        {
            Type controller = RequireRouterType();
            Assert.That(controller.GetConstructor(new[]
            {
                typeof(FormalAttentionRuntime),
                typeof(FormalFateRuntime),
            }), Is.Not.Null);
            MethodInfo bind = controller.GetMethod(
                "Bind",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(CityDeploymentModel),
                    typeof(GrayboxBuildingSession3D),
                },
                null);
            Assert.That(bind, Is.Not.Null);
            EventInfo buildingCompleted = typeof(GrayboxBuildingSession3D)
                .GetEvent(
                    "BuildingCompleted",
                    BindingFlags.Public | BindingFlags.Instance);
            Assert.That(buildingCompleted, Is.Not.Null,
                "The router must consume a settled building event, not poll " +
                "scene objects or infer completion from UI.");
            Assert.That(buildingCompleted.EventHandlerType,
                Is.EqualTo(typeof(Action<GrayboxBuildingInstance3D>)));
            Assert.That(typeof(ResearchModel).GetEvent("Completed"), Is.Not.Null);
            Assert.That(typeof(CityDeploymentModel).GetEvent(
                "CheckpointCommitted"), Is.Not.Null);
        }

        [Test]
        public void IDEA0020_FirstDeploymentAddsFiveExactlyOnce()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object controller = CreateController(attention, fate);
            var deployment = new CityDeploymentModel(3f, 5f);
            GrayboxBuildingSession3D session = CreateSession();
            Bind(controller, deployment, session);

            Assert.That(deployment.Toggle(), Is.True);
            deployment.Tick(3f);
            Assert.That(attention.Value, Is.EqualTo(15));
            AssertLatest(
                attention,
                "core.attention.city.first-deployment",
                5);

            Assert.That(deployment.Toggle(), Is.True);
            deployment.Tick(5f);
            Assert.That(deployment.Toggle(), Is.True);
            deployment.Tick(3f);
            Bind(controller, deployment, session);
            Assert.That(attention.Value, Is.EqualTo(15));
            Assert.That(attention.Capture().History, Has.Count.EqualTo(1));
        }

        [Test]
        public void IDEA0020_FirstProductionBuildingOfEachDefinitionAddsTwoThreeFour()
        {
            var attention = new FormalAttentionRuntime();
            object controller = CreateController(
                attention,
                new FormalFateRuntime());
            var deployment = new CityDeploymentModel(3f, 5f);
            GrayboxBuildingSession3D session = CreateSession();
            Bind(controller, deployment, session);
            var presentation = new NullPresentation();

            GrayboxBuildingRestoreEntry3D[] entries =
            {
                UnderConstruction(1, BuildingCatalog.MiningStation, 4, 4),
                UnderConstruction(2, BuildingCatalog.MiningStation, 8, 4),
                UnderConstruction(3, BuildingCatalog.Smelter, 12, 4),
                UnderConstruction(4, BuildingCatalog.Smelter, 16, 4),
                UnderConstruction(5, BuildingCatalog.Assembler, 20, 4),
                UnderConstruction(6, BuildingCatalog.Assembler, 24, 4),
            };
            Assert.That(session.TryRestoreBuildings(
                entries,
                7,
                presentation,
                out string restoreError), Is.True, restoreError);
            Assert.That(attention.Value, Is.EqualTo(10),
                "Loading under-construction truth is not a completion event.");

            session.CompleteAllConstructionForDevelopment(presentation);
            Assert.That(attention.Value, Is.EqualTo(19));
            Assert.That(attention.Capture().History.Select(value =>
                    value.ReasonId),
                Is.EqualTo(new[]
                {
                    "core.attention.building.first-mining-station",
                    "core.attention.building.first-smelter",
                    "core.attention.building.first-assembler",
                }));
            Assert.That(attention.Capture().History.Select(value =>
                    value.AppliedDelta),
                Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void IDEA0020_EachStableMachineGunAddsFiveButRestoreAndResyncDoNot()
        {
            var attention = new FormalAttentionRuntime();
            object controller = CreateController(
                attention,
                new FormalFateRuntime());
            var deployment = new CityDeploymentModel(3f, 5f);
            GrayboxBuildingSession3D session = CreateSession();
            Bind(controller, deployment, session);
            var presentation = new NullPresentation();
            GrayboxBuildingRestoreEntry3D[] construction =
            {
                UnderConstruction(
                    1,
                    BuildingCatalog.MachineGunTurret,
                    8,
                    8),
                UnderConstruction(
                    2,
                    BuildingCatalog.MachineGunTurret,
                    12,
                    8),
            };
            Assert.That(session.TryRestoreBuildings(
                construction,
                3,
                presentation,
                out string error), Is.True, error);
            session.CompleteAllConstructionForDevelopment(presentation);

            Assert.That(attention.Value, Is.EqualTo(20));
            Assert.That(attention.Capture().History.Select(value =>
                    value.StableEventKey),
                Is.EqualTo(new[]
                {
                    "building-completed:building.instance.000001",
                    "building-completed:building.instance.000002",
                }));

            GrayboxBuildingRestoreEntry3D[] restored = construction
                .Select(value => Completed(
                    value.StableInstanceId,
                    value.Definition,
                    value.X,
                    value.Y))
                .ToArray();
            Assert.That(session.TryRestoreBuildings(
                restored,
                3,
                presentation,
                out error), Is.True, error);
            Bind(controller, deployment, session);
            Assert.That(attention.Value, Is.EqualTo(20),
                "Restore and repeated authoring must not replay completion.");
            Assert.That(attention.Capture().History, Has.Count.EqualTo(2));
        }

        [Test]
        public void IDEA0020_FormalResearchCompletionAddsThreeFourFiveOnce()
        {
            var attention = new FormalAttentionRuntime();
            object controller = CreateController(
                attention,
                new FormalFateRuntime());
            GrayboxBuildingSession3D session = CreateSession();
            session.Research.GrantCompletedForDevelopment(
                ResearchCatalog.Find("core.research.scrap-processing"));
            Bind(controller, new CityDeploymentModel(3f, 5f), session);

            CompleteResearch(session, "core.research.automated-machinery");
            CompleteResearch(session, "core.research.precision-assembly");
            CompleteResearch(session, "core.research.automated-defense");
            Assert.That(attention.Value, Is.EqualTo(22));
            Assert.That(attention.Capture().History.Select(value =>
                    value.ReasonId),
                Is.EqualTo(new[]
                {
                    "core.attention.research.automated-machinery",
                    "core.attention.research.precision-assembly",
                    "core.attention.research.automated-defense",
                }));
            Assert.That(attention.Capture().History.Select(value =>
                    value.AppliedDelta),
                Is.EqualTo(new[] { 3, 4, 5 }));

            Bind(controller, new CityDeploymentModel(3f, 5f), session);
            Assert.That(attention.Value, Is.EqualTo(22));
            Assert.That(session.Research.Tick(1000f), Is.False,
                "Already completed research has no second completion event.");
            Assert.That(attention.Value, Is.EqualTo(22));
        }

        [Test]
        public void IDEA0020_FateSelectionUsesUnifiedAtomicAttentionCommit()
        {
            var attention = new FormalAttentionRuntime();
            var fate = new FormalFateRuntime();
            object controller = CreateController(attention, fate);
            Assert.That(TrySelectFate(
                controller,
                "core.legacy.void-debt",
                out string error), Is.True, error);
            Assert.That(attention.Value, Is.EqualTo(15));
            Assert.That(fate.Capture().SelectedId,
                Is.EqualTo("core.legacy.void-debt"));
            AssertLatest(
                attention,
                "core.attention.fate.first-activation",
                5);
            Assert.That(attention.Capture().History[0].StableEventKey,
                Is.EqualTo("fate-selection-complete"));

            var blockedAttention = new FormalAttentionRuntime();
            Assert.That(blockedAttention.TryApply(
                "core.attention.fate.first-activation",
                "preexisting.fate-selection",
                out _), Is.True);
            var pendingFate = new FormalFateRuntime();
            object blocked = CreateController(blockedAttention, pendingFate);
            string before = Fingerprint(blockedAttention, pendingFate);
            Assert.That(TrySelectFate(
                blocked,
                "core.legacy.pocket-universe",
                out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(Fingerprint(blockedAttention, pendingFate),
                Is.EqualTo(before),
                "Failed cross-domain commit cannot leave fate selected " +
                "without its attention event.");
            Assert.That(pendingFate.Capture().HasSelection, Is.False);
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var owner = new GameObject("ProgressionEventSession");
            created.Add(owner);
            GrayboxBuildingSession3D session =
                owner.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static void CompleteResearch(
            GrayboxBuildingSession3D session,
            string id)
        {
            ResearchDefinition definition = ResearchCatalog.Find(id);
            Assert.That(definition, Is.Not.Null, id);
            Assert.That(session.Research.Start(definition, session.Inventory),
                Is.True, id);
            Assert.That(session.Research.Tick(definition.Duration + 1f),
                Is.True, id);
        }

        private static GrayboxBuildingRestoreEntry3D UnderConstruction(
            int ordinal,
            BuildingDefinition definition,
            int x,
            int y)
        {
            return new GrayboxBuildingRestoreEntry3D(
                "building.instance." + ordinal.ToString("D6"),
                definition,
                BuildingSite.Ground,
                x,
                y,
                BuildingOrientation.North,
                GrayboxBuildingInstanceState.UnderConstruction,
                definition.BuildSeconds,
                isPlayerOwned: true,
                isEvacuationLocked: false,
                boundResourceNode: default(ResourceNodeBinding));
        }

        private static GrayboxBuildingRestoreEntry3D Completed(
            string stableId,
            BuildingDefinition definition,
            int x,
            int y)
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
                isEvacuationLocked: false,
                boundResourceNode: default(ResourceNodeBinding));
        }

        private static object CreateController(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            return Activator.CreateInstance(
                RequireRouterType(),
                attention,
                fate);
        }

        private static void Bind(
            object controller,
            CityDeploymentModel deployment,
            GrayboxBuildingSession3D session)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Bind",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(CityDeploymentModel),
                    typeof(GrayboxBuildingSession3D),
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { deployment, session });
        }

        private static bool TrySelectFate(
            object controller,
            string fateId,
            out string error)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "TrySelectFate",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { fateId, null };
            bool result = (bool)method.Invoke(controller, arguments);
            error = arguments[1] as string;
            Assert.That(error, Is.Not.Null);
            return result;
        }

        private static void AssertLatest(
            FormalAttentionRuntime attention,
            string reasonId,
            int delta)
        {
            FormalAttentionHistoryEntry latest =
                attention.Capture().History.Last();
            Assert.That(latest.ReasonId, Is.EqualTo(reasonId));
            Assert.That(latest.AppliedDelta, Is.EqualTo(delta));
        }

        private static string Fingerprint(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            return attentionState.Value + "|" + attentionState.Revision + "|" +
                attentionState.History.Count + "|" + fateState.SelectedId +
                "|" + fateState.Level + "|" + fateState.Revision;
        }

        private static Type RequireRouterType()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    RouterTypeName,
                    throwOnError: false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, RouterTypeName);
            return type;
        }

        private sealed class NullPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void RemoveInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
