using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingCombatLifecycleTests
    {
        private const string DestroyCommandName =
            "TryDestroyBuildingForCombat";

        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void IDEA0017_CombatDestroyCommandRejectsEveryIneligibleLifecycle()
        {
            AssertRejectedWithoutMutation(
                "building.instance.000001",
                GrayboxBuildingInstanceState.UnderConstruction,
                playerOwned: true,
                evacuationLocked: false,
                remainingSeconds: 1f);
            AssertRejectedWithoutMutation(
                "building.instance.000002",
                GrayboxBuildingInstanceState.Completed,
                playerOwned: false,
                evacuationLocked: false,
                remainingSeconds: 0f);
            AssertRejectedWithoutMutation(
                "building.instance.000003",
                GrayboxBuildingInstanceState.Completed,
                playerOwned: true,
                evacuationLocked: true,
                remainingSeconds: 0f);

            GrayboxBuildingSession3D missingSession = CreateSession();
            var missingPresentation = new RecordingPresentation();
            uint catalogBefore = missingSession.CatalogRevision;
            uint placementBefore = missingSession.PlacementRevision;

            Assert.That(
                InvokeCombatDestroy(
                    missingSession,
                    "building.instance.999999",
                    missingPresentation),
                Is.False,
                "IDEA-0017 combat destruction must reject a missing instance.");
            Assert.That(missingSession.CatalogRevision, Is.EqualTo(catalogBefore));
            Assert.That(missingSession.PlacementRevision,
                Is.EqualTo(placementBefore));
            Assert.That(missingPresentation.Updated, Is.Empty);
        }

        [Test]
        public void IDEA0017_CombatDestroyCommandCreatesOnePersistentRuinTransaction()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            const string stableId = "building.instance.000017";
            const int x = 10;
            const int y = 9;
            const BuildingOrientation orientation = BuildingOrientation.East;
            GrayboxBuildingInstance3D instance = RestoreSingle(
                session,
                presentation,
                stableId,
                BuildingCatalog.BehemothPen,
                GrayboxBuildingInstanceState.Completed,
                playerOwned: true,
                evacuationLocked: false,
                remainingSeconds: 0f,
                x,
                y,
                orientation);
            presentation.Reset();
            session.Inventory.Set(ResourceIds.BoneSteel, 41);

            PlacedBuilding placementBefore = instance.Placement;
            uint catalogBefore = session.CatalogRevision;
            uint placementRevisionBefore = session.PlacementRevision;
            int materialBefore = session.Inventory.Get(ResourceIds.BoneSteel);
            int gridCountBefore = session.GroundGrid.Count;
            List<BuildingCell> footprintBefore = CaptureFootprint(instance);

            Assert.That(
                InvokeCombatDestroy(session, stableId, presentation),
                Is.True,
                "IDEA-0017 must accept one completed, player-owned, unlocked building.");

            Assert.That(session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Placement, Is.SameAs(placementBefore));
            Assert.That(instance.Placement.Definition,
                Is.SameAs(BuildingCatalog.BehemothPen));
            Assert.That(instance.Placement.Site, Is.EqualTo(BuildingSite.Ground));
            Assert.That(instance.Placement.X, Is.EqualTo(x));
            Assert.That(instance.Placement.Y, Is.EqualTo(y));
            Assert.That(instance.Placement.Orientation, Is.EqualTo(orientation));
            Assert.That(instance.State.ToString(), Is.EqualTo("DestroyedRuin"));
            Assert.That(instance.IsPlayerOwned, Is.False);
            Assert.That(instance.IsEvacuationLocked, Is.False);
            Assert.That(session.CatalogRevision,
                Is.EqualTo(unchecked(catalogBefore + 1u)));
            Assert.That(session.PlacementRevision,
                Is.EqualTo(unchecked(placementRevisionBefore + 1u)));
            Assert.That(session.Inventory.Get(ResourceIds.BoneSteel),
                Is.EqualTo(materialBefore),
                "Combat destruction must not refund construction materials.");
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCountBefore));
            AssertFootprintStillOccupied(session.GroundGrid, footprintBefore);
            Assert.That(
                session.GroundGrid.CanPlace(
                    BuildingCatalog.BehemothPen,
                    x,
                    y,
                    BuildingSite.Ground,
                    orientation),
                Is.False,
                "The persistent ruin footprint must continue to reject overlap.");
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
            Assert.That(presentation.Created, Is.Empty);
            Assert.That(presentation.Removed, Is.Empty);

            var evacuationManifest = new List<GrayboxBuildingInstance3D>();
            session.CopyPlayerOwnedGroundInstances(evacuationManifest);
            Assert.That(evacuationManifest, Is.Empty,
                "Destroyed ruins must not enter the evacuation manifest.");
            Assert.That(session.HasPlayerOwnedGroundInstances, Is.False,
                "A ruin alone must not block deployment from finishing.");

            uint catalogAfter = session.CatalogRevision;
            uint placementAfter = session.PlacementRevision;
            Assert.That(
                InvokeCombatDestroy(session, stableId, presentation),
                Is.False,
                "Repeated destruction must be an idempotent no-op.");
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogAfter));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementAfter));
            Assert.That(session.Inventory.Get(ResourceIds.BoneSteel),
                Is.EqualTo(materialBefore));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCountBefore));
            AssertFootprintStillOccupied(session.GroundGrid, footprintBefore);
        }

        private void AssertRejectedWithoutMutation(
            string stableId,
            GrayboxBuildingInstanceState state,
            bool playerOwned,
            bool evacuationLocked,
            float remainingSeconds)
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = RestoreSingle(
                session,
                presentation,
                stableId,
                BuildingCatalog.Housing,
                state,
                playerOwned,
                evacuationLocked,
                remainingSeconds,
                10,
                10,
                BuildingOrientation.North);
            presentation.Reset();
            session.Inventory.Set(ResourceIds.Alloy, 37);
            uint catalogBefore = session.CatalogRevision;
            uint placementBefore = session.PlacementRevision;
            int materialBefore = session.Inventory.Get(ResourceIds.Alloy);
            int gridCountBefore = session.GroundGrid.Count;

            Assert.That(
                InvokeCombatDestroy(session, stableId, presentation),
                Is.False,
                "Only completed, player-owned, evacuation-unlocked buildings may be destroyed by combat.");
            Assert.That(instance.State, Is.EqualTo(state));
            Assert.That(instance.IsPlayerOwned, Is.EqualTo(playerOwned));
            Assert.That(instance.IsEvacuationLocked,
                Is.EqualTo(evacuationLocked));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogBefore));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementBefore));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(materialBefore));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCountBefore));
            Assert.That(presentation.Updated, Is.Empty);
            Assert.That(presentation.Created, Is.Empty);
            Assert.That(presentation.Removed, Is.Empty);
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var gameObject = new GameObject(
                "graybox-building-combat-lifecycle-test");
            cleanup.Add(gameObject);
            GrayboxBuildingSession3D session =
                gameObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static GrayboxBuildingInstance3D RestoreSingle(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation,
            string stableId,
            BuildingDefinition definition,
            GrayboxBuildingInstanceState state,
            bool playerOwned,
            bool evacuationLocked,
            float remainingSeconds,
            int x,
            int y,
            BuildingOrientation orientation)
        {
            var entries = new[]
            {
                new GrayboxBuildingRestoreEntry3D(
                    stableId,
                    definition,
                    BuildingSite.Ground,
                    x,
                    y,
                    orientation,
                    state,
                    remainingSeconds,
                    playerOwned,
                    evacuationLocked,
                    ResourceNodeBinding.None)
            };
            Assert.That(
                session.TryRestoreBuildings(
                    entries,
                    RestoredNextOrdinal(stableId),
                    presentation,
                    out string error),
                Is.True,
                error);
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            return session.Instances[0];
        }

        private static int RestoredNextOrdinal(string stableId)
        {
            int separator = stableId.LastIndexOf('.');
            Assert.That(separator, Is.GreaterThanOrEqualTo(0));
            Assert.That(int.TryParse(
                stableId.Substring(separator + 1),
                out int ordinal), Is.True);
            return ordinal + 1;
        }

        private static bool InvokeCombatDestroy(
            GrayboxBuildingSession3D session,
            string stableId,
            IGrayboxBuildingPresentation3D presentation)
        {
            MethodInfo command = typeof(GrayboxBuildingSession3D).GetMethod(
                DestroyCommandName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string),
                    typeof(IGrayboxBuildingPresentation3D)
                },
                null);
            Assert.That(
                command,
                Is.Not.Null,
                "IDEA-0017 requires public bool " + DestroyCommandName +
                "(string, IGrayboxBuildingPresentation3D).");
            Assert.That(command.ReturnType, Is.EqualTo(typeof(bool)));
            return (bool)command.Invoke(
                session,
                new object[] { stableId, presentation });
        }

        private static List<BuildingCell> CaptureFootprint(
            GrayboxBuildingInstance3D instance)
        {
            int width = BuildingOrientationRules.Width(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int height = BuildingOrientationRules.Height(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            var footprint = new List<BuildingCell>(width * height);
            for (var offsetX = 0; offsetX < width; offsetX++)
                for (var offsetY = 0; offsetY < height; offsetY++)
                    footprint.Add(new BuildingCell(
                        instance.Placement.X + offsetX,
                        instance.Placement.Y + offsetY));
            return footprint;
        }

        private static void AssertFootprintStillOccupied(
            BuildingGrid grid,
            IReadOnlyList<BuildingCell> footprint)
        {
            for (var index = 0; index < footprint.Count; index++)
            {
                Assert.That(
                    grid.IsOccupied(
                        footprint[index].X,
                        footprint[index].Y),
                    Is.True,
                    "Expected ruin footprint cell " +
                    footprint[index].X + "," + footprint[index].Y +
                    " to remain occupied.");
            }
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public List<GrayboxBuildingInstance3D> Created { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Updated { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Removed { get; } =
                new List<GrayboxBuildingInstance3D>();

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                Created.Add(instance);
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                Updated.Add(instance);
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                Removed.Add(instance);
            }

            public void Reset()
            {
                Created.Clear();
                Updated.Clear();
                Removed.Clear();
            }
        }
    }
}
