using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveWorldCityTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxWorldCitySaveAdapter3D";
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private SimulationMode originalSimulationMode;

        [SetUp]
        public void SetUp()
        {
            originalSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                for (int index = cleanup.Count - 1; index >= 0; index--)
                {
                    if (cleanup[index] != null)
                        UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }
                cleanup.Clear();
            }
            finally
            {
                Physics.simulationMode = originalSimulationMode;
            }
        }

        [Test]
        public void AdapterContractExistsAndOwnsNoFilesOrSceneDiscovery()
        {
            Type adapterType = typeof(GrayboxBuildingSession3D).Assembly
                .GetType(AdapterTypeName);
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxWorldCitySaveAdapter3D.cs");
            bool sourceExists = File.Exists(sourcePath);
            string source = sourceExists
                ? File.ReadAllText(sourcePath)
                : string.Empty;

            Assert.That(
                adapterType,
                Is.Not.Null,
                "Task 4 requires one world/city DTO adapter in the " +
                "Graybox3D.Building assembly.");
            Assert.That(
                sourceExists,
                Is.True,
                "The adapter source must exist at its planned quality-" +
                "catalog path.");
            Assert.That(source, Does.Not.Contain("System.IO"));
            Assert.That(source, Does.Not.Contain("persistentDataPath"));
            Assert.That(source, Does.Not.Contain("FindObject"));
            Assert.That(source, Does.Not.Contain("File."));
        }

        [Test]
        public void AdapterExposesOnlyTypedWorldAndCityMappingBoundary()
        {
            Type adapterType = RequireAdapterType();

            ConstructorInfo constructor = adapterType.GetConstructor(
                new[]
                {
                    typeof(GrayboxSceneBootstrap),
                    typeof(GrayboxMobileCityController3D),
                    typeof(GrayboxBuildingSession3D),
                });
            MethodInfo captureWorld = FindPublicInstanceMethod(
                adapterType,
                "CaptureWorld",
                Type.EmptyTypes);
            MethodInfo captureCity = FindPublicInstanceMethod(
                adapterType,
                "CaptureCity",
                Type.EmptyTypes);
            MethodInfo restore = FindPublicInstanceMethod(
                adapterType,
                "TryRestore",
                new[]
                {
                    typeof(FormalThreeDWorldSaveData),
                    typeof(FormalThreeDCitySaveData),
                    typeof(string).MakeByRefType(),
                });

            Assert.That(
                constructor,
                Is.Not.Null,
                "The adapter must receive explicit authoritative owners; " +
                "it must not find them in the scene.");
            AssertMethodReturn(
                captureWorld,
                typeof(FormalThreeDWorldSaveData),
                "CaptureWorld");
            AssertMethodReturn(
                captureCity,
                typeof(FormalThreeDCitySaveData),
                "CaptureCity");
            AssertMethodReturn(restore, typeof(bool), "TryRestore");
        }

        [Test]
        public void WorldMapHasValidatedNonTruncatingResourceRestoreEntry()
        {
            MethodInfo restore = RequirePublicInstanceMethod(
                typeof(WorldMapModel),
                "TryRestoreResourceAmounts",
                typeof(bool),
                typeof(int[]),
                typeof(string).MakeByRefType());
            var map = new WorldMapModel(
                new[,]
                {
                    {
                        new WorldCell(
                            TerrainKind.Rocky,
                            ResourceIds.Iron,
                            10)
                    }
                });
            int[] invalid = { -1 };
            object[] arguments = { invalid, null };

            bool restored = (bool)restore.Invoke(map, arguments);

            Assert.That(restored, Is.False);
            Assert.That(arguments[1], Is.TypeOf<string>());
            Assert.That((string)arguments[1], Is.Not.Empty);
            Assert.That(
                map.Get(0, 0).ResourceAmount,
                Is.EqualTo(10),
                "Invalid resource state must be rejected atomically, not " +
                "silently clamped to zero.");
        }

        [TestCase(CityMode.Deploying, CityMode.Mobile, 2.5f)]
        [TestCase(CityMode.Packing, CityMode.Fortress, 4.25f)]
        public void DeploymentHasValidatedTransitionRestoreEntry(
            CityMode mode,
            CityMode returnMode,
            float remainingSeconds)
        {
            Type modelType = typeof(CityDeploymentModel);
            PropertyInfo transitionReturnMode = modelType.GetProperty(
                "TransitionReturnMode",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo restore = RequirePublicInstanceMethod(
                modelType,
                "TryRestore",
                typeof(bool),
                typeof(CityMode),
                typeof(CityMode),
                typeof(float),
                typeof(string).MakeByRefType());
            var model = new CityDeploymentModel(5f, 8f);
            object[] arguments =
            {
                mode,
                returnMode,
                remainingSeconds,
                null,
            };

            bool restored = (bool)restore.Invoke(model, arguments);

            Assert.That(
                transitionReturnMode,
                Is.Not.Null,
                "Active transitions must expose the stable mode restored " +
                "when the player cancels them.");
            Assert.That(restored, Is.True, arguments[3] as string);
            Assert.That(model.Mode, Is.EqualTo(mode));
            Assert.That(
                model.Remaining,
                Is.EqualTo(remainingSeconds).Within(.0001f));
            if (transitionReturnMode != null)
            {
                Assert.That(
                    transitionReturnMode.GetValue(model),
                    Is.EqualTo(returnMode));
            }
        }

        [Test]
        public void DeploymentRejectsMismatchedReturnModeWithoutMutation()
        {
            MethodInfo restore = RequirePublicInstanceMethod(
                typeof(CityDeploymentModel),
                "TryRestore",
                typeof(bool),
                typeof(CityMode),
                typeof(CityMode),
                typeof(float),
                typeof(string).MakeByRefType());
            var model = new CityDeploymentModel(5f, 8f);
            object[] arguments =
            {
                CityMode.Deploying,
                CityMode.Fortress,
                2f,
                null,
            };

            bool restored = (bool)restore.Invoke(model, arguments);

            Assert.That(restored, Is.False);
            Assert.That(arguments[3], Is.TypeOf<string>());
            Assert.That((string)arguments[3], Is.Not.Empty);
            Assert.That(model.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(model.Remaining, Is.Zero);
        }

        [Test]
        public void RuntimeOwnersExposeNarrowValidatedRestoreEntries()
        {
            MethodInfo restoreWorld = FindPublicInstanceMethod(
                typeof(GrayboxSceneBootstrap),
                "TryRestoreWorld",
                new[]
                {
                    typeof(WorldMapModel),
                    typeof(string).MakeByRefType(),
                });
            MethodInfo restoreCity = FindPublicInstanceMethod(
                typeof(GrayboxMobileCityController3D),
                "TryRestoreForPersistence",
                new[]
                {
                    typeof(Vector3),
                    typeof(CityMode),
                    typeof(CityMode),
                    typeof(float),
                    typeof(bool),
                    typeof(int),
                    typeof(int),
                    typeof(string).MakeByRefType(),
                });
            MethodInfo restorePopulation = FindPublicInstanceMethod(
                typeof(GrayboxBuildingSession3D),
                "TryRestorePopulation",
                new[]
                {
                    typeof(int),
                    typeof(int),
                    typeof(string).MakeByRefType(),
                });

            AssertMethodReturn(
                restoreWorld,
                typeof(bool),
                "GrayboxSceneBootstrap.TryRestoreWorld");
            AssertMethodReturn(
                restoreCity,
                typeof(bool),
                "GrayboxMobileCityController3D." +
                "TryRestoreForPersistence");
            AssertMethodReturn(
                restorePopulation,
                typeof(bool),
                "GrayboxBuildingSession3D.TryRestorePopulation");
        }

        [Test]
        public void CaptureWorldSortsStableNodesAndMatchesAuthoritativeTruth()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();

            FormalThreeDWorldSaveData captured =
                fixture.Adapter.CaptureWorld();

            Assert.That(
                captured.resourceNodes.Length,
                Is.EqualTo(fixture.Bootstrap.World.ResourceNodeCount));
            for (int index = 0; index < captured.resourceNodes.Length; index++)
            {
                FormalThreeDResourceNodeSaveData node =
                    captured.resourceNodes[index];
                WorldCell cell = fixture.Bootstrap.World.Get(node.x, node.y);
                Assert.That(
                    node.stableNodeId,
                    Is.EqualTo(
                        GrayboxResourceNodeIdentity3D.Create(
                            node.x,
                            node.y)));
                Assert.That(node.resourceId, Is.EqualTo(cell.ResourceId));
                Assert.That(
                    node.remainingAmount,
                    Is.EqualTo(cell.ResourceAmount));
                Assert.That(
                    node.isDepleted,
                    Is.EqualTo(cell.ResourceAmount == 0));
                if (index > 0)
                {
                    Assert.That(
                        string.CompareOrdinal(
                            captured.resourceNodes[index - 1].stableNodeId,
                            node.stableNodeId),
                        Is.LessThan(0),
                        "Resource nodes must be encoded in strict ordinal " +
                        "stable-ID order.");
                }
            }
        }

        [Test]
        public void IDEA0019_WorldIdentityUsesGenerationTwoWithoutSchemaChange()
        {
            Assert.That(
                GrayboxWorldCitySaveAdapter3D.WorldGenerationVersion,
                Is.EqualTo(2),
                "IDEA-0019 requires a distinct generation identity for " +
                "the authoritative v2 64x48 layout.");
            Assert.That(
                GrayboxWorldCitySaveAdapter3D
                    .WorldConfigurationSignature,
                Is.EqualTo("core.world.formal-3d.v2.64x48"),
                "The new layout must not reuse the v1 world signature.");
        }

        [Test]
        public void IDEA0019_FormalEnvelopeSchemaUsesApprovedSuccessor()
        {
            Assert.That(
                WasteCity.Persistence.FormalSaveEnvelope
                    .CurrentSchemaVersion,
                Is.EqualTo(34),
                "IDEA-0019 world generation remains v2 while IDEA-0022 " +
                "adds the later Formal3D schema 34 envelope.");
        }

        [Test]
        public void IDEA0019_CapturePublishesGenerationTwoIdentity()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();

            FormalThreeDWorldSaveData captured =
                fixture.Adapter.CaptureWorld();

            Assert.That(captured.worldGenerationVersion, Is.EqualTo(2));
            Assert.That(
                captured.configurationSignature,
                Is.EqualTo("core.world.formal-3d.v2.64x48"));
            Assert.That(captured.width, Is.EqualTo(64));
            Assert.That(captured.height, Is.EqualTo(48));
            Assert.That(captured.worldSeed, Is.EqualTo(8128));
        }

        [Test]
        public void IDEA0019_VersionOneWorldIsRejectedWithoutRuntimeMutation()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            FormalThreeDWorldSaveData versionOne =
                CreateVersionOneWorldSave();
            FormalThreeDCitySaveData city =
                CloneCity(fixture.Adapter.CaptureCity());
            RuntimeFingerprint before = RuntimeFingerprint.Capture(fixture);

            bool restored = fixture.Adapter.TryRestore(
                versionOne,
                city,
                out string error);

            Assert.That(restored, Is.False);
            Assert.That(
                error,
                Is.EqualTo("存档世界配置与当前正式世界不兼容"));
            before.AssertUnchanged(fixture);
        }

        [Test]
        public void CaptureDisturbRestoreRoundTripsWorldCityNavigationAndPopulation()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            ResourceCell resource = FindResourceCell(
                fixture.Bootstrap.World);
            Assert.That(
                fixture.Bootstrap.World.Harvest(
                    resource.X,
                    resource.Y,
                    7,
                    out _),
                Is.EqualTo(7));
            Vector3 savedPosition = fixture.StartPosition +
                                    new Vector3(.17f, 0f, .13f);
            Assert.That(
                fixture.City.TryRestoreForPersistence(
                    savedPosition,
                    CityMode.Mobile,
                    CityMode.Mobile,
                    0f,
                    true,
                    fixture.DestinationX,
                    fixture.DestinationY,
                    out string cityError),
                Is.True,
                cityError);
            Assert.That(
                fixture.Session.TryRestorePopulation(
                    137,
                    211,
                    out string populationError),
                Is.True,
                populationError);
            fixture.View.RefreshResourceNodeMarkers();
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            int expectedResourceAmount = FindNode(
                savedWorld,
                resource.X,
                resource.Y).remainingAmount;

            fixture.Bootstrap.World.Harvest(
                resource.X,
                resource.Y,
                int.MaxValue,
                out _);
            fixture.View.RefreshResourceNodeMarkers();
            fixture.City.ApplyManualInput(Vector2.right);
            fixture.Body.position = fixture.DestinationPosition;
            Assert.That(
                fixture.Session.TryRestorePopulation(3, 4, out _),
                Is.True);

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string restoreError),
                Is.True,
                restoreError);

            Assert.That(
                fixture.Bootstrap.World.Get(resource.X, resource.Y)
                    .ResourceAmount,
                Is.EqualTo(expectedResourceAmount));
            Assert.That(
                fixture.City.WorldPosition,
                Is.EqualTo(savedPosition).Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(fixture.City.TryGetCurrentCell(out int x, out int y), Is.True);
            Assert.That(x, Is.EqualTo(savedCity.cellX));
            Assert.That(y, Is.EqualTo(savedCity.cellY));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.City.AutopilotActive, Is.True);
            AssertDestination(
                fixture.City,
                fixture.DestinationX,
                fixture.DestinationY);
            Assert.That(fixture.Session.Population, Is.EqualTo(137));
            Assert.That(fixture.Session.PopulationCapacity, Is.EqualTo(211));
        }

        [Test]
        public void RestoredAutopilotRecomputesPathAndActuallyMoves()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            Assert.That(
                fixture.City.TryRestoreForPersistence(
                    fixture.StartPosition,
                    CityMode.Mobile,
                    CityMode.Mobile,
                    0f,
                    true,
                    fixture.DestinationX,
                    fixture.DestinationY,
                    out string setupError),
                Is.True,
                setupError);
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            fixture.City.ApplyManualInput(Vector2.left);
            Assert.That(fixture.City.AutopilotActive, Is.False);

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string restoreError),
                Is.True,
                restoreError);
            Vector3 before = fixture.Body.position;

            fixture.City.TickMovement(.1f);
            Physics.SyncTransforms();
            Physics.Simulate(.02f);

            Assert.That(
                Vector3.Distance(fixture.Body.position, before),
                Is.GreaterThan(.001f),
                "An active destination must rebuild a usable route; the " +
                "runtime path itself is not persisted.");
        }

        [TestCase(CityMode.Deploying, CityMode.Mobile, 1.25f)]
        [TestCase(CityMode.Packing, CityMode.Fortress, 2f)]
        public void ActiveTransitionRoundTripsRemainingReturnModeAndPresentation(
            CityMode mode,
            CityMode returnMode,
            float remainingSeconds)
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            Assert.That(
                fixture.City.TryRestoreForPersistence(
                    fixture.StartPosition,
                    mode,
                    returnMode,
                    remainingSeconds,
                    false,
                    0,
                    0,
                    out string setupError),
                Is.True,
                setupError);
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            Assert.That(
                fixture.City.TryRestoreForPersistence(
                    fixture.DestinationPosition,
                    CityMode.Mobile,
                    CityMode.Mobile,
                    0f,
                    false,
                    0,
                    0,
                    out _),
                Is.True);

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string restoreError),
                Is.True,
                restoreError);

            Assert.That(fixture.City.Mode, Is.EqualTo(mode));
            Assert.That(
                fixture.City.Deployment.TransitionReturnMode,
                Is.EqualTo(returnMode));
            Assert.That(
                fixture.City.Deployment.Remaining,
                Is.EqualTo(remainingSeconds).Within(.0001f));
            Assert.That(
                fixture.VisualTransform.localScale,
                Is.EqualTo(ExpectedCityVisualSize(mode, remainingSeconds))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                fixture.BodyCollider.size,
                Is.EqualTo(ExpectedCityColliderSize(mode, remainingSeconds))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));

            Assert.That(fixture.City.TryToggleDeployment(out _), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(returnMode));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidRestoreIsRepeatableAndLeavesAllRuntimeTruthUnchanged(
            bool positionCellMismatch)
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            ResourceCell resource = FindResourceCell(
                fixture.Bootstrap.World);
            FormalThreeDWorldSaveData candidateWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData candidateCity =
                CloneCity(fixture.Adapter.CaptureCity());
            if (positionCellMismatch)
            {
                candidateCity.cellX = candidateCity.cellX == 0 ? 1 : 0;
            }
            else
            {
                FindBlockedCell(
                    fixture.Bootstrap.World,
                    out candidateCity.destinationX,
                    out candidateCity.destinationY);
                candidateCity.autopilotActive = true;
                candidateCity.cityMode = (int)CityMode.Mobile;
                candidateCity.transitionReturnMode = (int)CityMode.Mobile;
                candidateCity.transitionRemainingSeconds = 0f;
            }

            fixture.Bootstrap.World.Harvest(
                resource.X,
                resource.Y,
                5,
                out _);
            fixture.View.RefreshResourceNodeMarkers();
            Assert.That(
                fixture.Session.TryRestorePopulation(173, 219, out _),
                Is.True);
            RuntimeFingerprint before = RuntimeFingerprint.Capture(fixture);

            Assert.That(
                fixture.Adapter.TryRestore(
                    candidateWorld,
                    candidateCity,
                    out string firstError),
                Is.False);
            before.AssertUnchanged(fixture);
            Assert.That(
                fixture.Adapter.TryRestore(
                    candidateWorld,
                    candidateCity,
                    out string secondError),
                Is.False);
            before.AssertUnchanged(fixture);
            Assert.That(secondError, Is.EqualTo(firstError));
        }

        [Test]
        public void RestoreReturnsWithMarkerRefreshedAndOneWorldModelInstalled()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            ResourceCell resource = FindResourceCell(
                fixture.Bootstrap.World);
            fixture.Bootstrap.World.Harvest(
                resource.X,
                resource.Y,
                9,
                out _);
            fixture.View.RefreshResourceNodeMarkers();
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            int savedAmount = FindNode(
                savedWorld,
                resource.X,
                resource.Y).remainingAmount;
            fixture.Bootstrap.World.Harvest(
                resource.X,
                resource.Y,
                int.MaxValue,
                out _);
            fixture.View.RefreshResourceNodeMarkers();

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string error),
                Is.True,
                error);

            Assert.That(
                fixture.View.TryGetResourceNodeMarker(
                    resource.X,
                    resource.Y,
                    out GrayboxResourceNodeMarker3D marker),
                Is.True);
            Assert.That(marker.DisplayedAmount, Is.EqualTo(savedAmount));
            Assert.That(
                fixture.Bootstrap.World,
                Is.SameAs(fixture.View.Model));
        }

        [Test]
        public void RestoreWithUnconfiguredCityFailsBeforeAnyRuntimeMutation()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                CloneCity(fixture.Adapter.CaptureCity());
            savedCity.population = 137;
            savedCity.populationCapacity = 211;
            RuntimeFingerprint before = RuntimeFingerprint.Capture(fixture);
            var cityObject = Track(new GameObject("UnconfiguredSaveCity"));
            GrayboxMobileCityController3D unconfiguredCity =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            var adapter = new GrayboxWorldCitySaveAdapter3D(
                fixture.Bootstrap,
                unconfiguredCity,
                fixture.Session);

            Assert.That(
                adapter.TryRestore(savedWorld, savedCity, out string error),
                Is.False);

            Assert.That(error, Is.Not.Empty);
            before.AssertUnchanged(fixture);
        }

        [Test]
        public void OrphanResourcesRoundTripWithoutCreatingUsableNodes()
        {
            WorldCityFixture fixture = CreateWorldCityFixture();
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            savedWorld.orphanResources = new[]
            {
                new FormalThreeDOrphanResourceSaveData
                {
                    resourceId = "mod.resource.unobtainium",
                    amount = 17,
                    ownerKind = "world",
                    ownerStableId = "world.orphan.000001",
                },
            };
            int nodeCount = fixture.Bootstrap.World.ResourceNodeCount;

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string error),
                Is.True,
                error);
            FormalThreeDWorldSaveData recaptured =
                fixture.Adapter.CaptureWorld();

            Assert.That(recaptured.orphanResources, Has.Length.EqualTo(1));
            Assert.That(
                recaptured.orphanResources[0].resourceId,
                Is.EqualTo("mod.resource.unobtainium"));
            Assert.That(recaptured.orphanResources[0].amount, Is.EqualTo(17));
            Assert.That(
                recaptured.orphanResources[0].ownerKind,
                Is.EqualTo("world"));
            Assert.That(
                recaptured.orphanResources[0].ownerStableId,
                Is.EqualTo("world.orphan.000001"));
            Assert.That(
                fixture.Bootstrap.World.ResourceNodeCount,
                Is.EqualTo(nodeCount));
        }

        [Test]
        public void TerrainPresentationFailureFallsBackWithoutSplittingWorldOwners()
        {
            var presenterObject = Track(
                new GameObject("FailingRestoreTerrainPresentation"));
            RecordingTerrainPresentation3D presenter =
                presenterObject.AddComponent<RecordingTerrainPresentation3D>();
            presenter.ParticipateInWorldLifecycle = true;
            WorldCityFixture fixture = CreateWorldCityFixture(presenter);
            Assert.That(fixture.View.HasActiveTerrainPresentation, Is.True);
            FormalThreeDWorldSaveData savedWorld =
                fixture.Adapter.CaptureWorld();
            FormalThreeDCitySaveData savedCity =
                fixture.Adapter.CaptureCity();
            presenter.ThrowOnPresent = true;
            LogAssert.Expect(
                LogType.Error,
                new Regex(
                    "Graybox terrain presentation failed: " +
                    "Injected presenter failure.*surface fallback " +
                    "restored"));

            Assert.That(
                fixture.Adapter.TryRestore(
                    savedWorld,
                    savedCity,
                    out string error),
                Is.True,
                error);

            Assert.That(
                fixture.Bootstrap.World,
                Is.SameAs(fixture.View.Model));
            Assert.That(fixture.View.SurfaceFallbackVisible, Is.True);
            Assert.That(fixture.View.HasActiveTerrainPresentation, Is.False);
        }

        private WorldCityFixture CreateWorldCityFixture(
            MonoBehaviour terrainPresentation = null)
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));
            var worldRoot = Track(new GameObject("FormalSaveWorld"));
            Transform terrain = NewChild(worldRoot.transform, "TerrainRoot");
            Transform resources = NewChild(worldRoot.transform, "ResourceRoot");
            Transform obstacles = NewChild(worldRoot.transform, "ObstacleRoot");
            GrayboxWorldView3D view =
                worldRoot.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);

            var bootstrapObject = Track(new GameObject("FormalSaveBootstrap"));
            GrayboxSceneBootstrap bootstrap =
                bootstrapObject.AddComponent<GrayboxSceneBootstrap>();
            bootstrap.Configure(null, view, terrainPresentation);
            Assert.That(
                bootstrap.TryRestoreWorld(
                    GrayboxWorldLayout3D.CreateDefault(),
                    GrayboxWorldLayout3D.DefaultSeed,
                    out string worldError),
                Is.True,
                worldError);
            FindReachablePair(
                bootstrap.World,
                out int startX,
                out int startY,
                out int destinationX,
                out int destinationY);
            Assert.That(
                view.Coordinates.TryCellToWorld(
                    startX,
                    startY,
                    .5f,
                    out Vector3 startPosition),
                Is.True);
            Assert.That(
                view.Coordinates.TryCellToWorld(
                    destinationX,
                    destinationY,
                    .5f,
                    out Vector3 destinationPosition),
                Is.True);

            var cityObject = Track(new GameObject("FormalSaveCity"));
            cityObject.transform.position = startPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider collider = cityObject.AddComponent<BoxCollider>();
            Transform visualTransform = NewChild(
                cityObject.transform,
                "Visual");
            MeshRenderer renderer =
                visualTransform.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            GrayboxVisualSlot visual =
                visualTransform.gameObject.AddComponent<GrayboxVisualSlot>();
            visual.Configure(
                "core.city.mobile",
                renderer,
                new Color(.9f, .48f, .1f));
            visual.ApplyFallback(material);
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(view, body, collider);

            var sessionObject = Track(new GameObject("FormalSaveSession"));
            GrayboxBuildingSession3D session =
                sessionObject.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureFormalSession();
            var adapter = new GrayboxWorldCitySaveAdapter3D(
                bootstrap,
                city,
                session);
            return new WorldCityFixture(
                bootstrap,
                view,
                city,
                body,
                collider,
                visualTransform,
                session,
                adapter,
                startPosition,
                destinationPosition,
                destinationX,
                destinationY);
        }

        private static FormalThreeDResourceNodeSaveData FindNode(
            FormalThreeDWorldSaveData world,
            int x,
            int y)
        {
            for (int index = 0; index < world.resourceNodes.Length; index++)
            {
                FormalThreeDResourceNodeSaveData node =
                    world.resourceNodes[index];
                if (node.x == x && node.y == y)
                    return node;
            }
            Assert.Fail("Expected a captured resource node at the selected cell.");
            return null;
        }

        private static FormalThreeDWorldSaveData CreateVersionOneWorldSave()
        {
            const int legacyWidth = 32;
            const int legacyHeight = 24;
            const int worldWidth = 64;
            const int worldHeight = 48;
            const int legacyOffsetX = 16;
            const int legacyOffsetY = 12;
            const int seedValue = 8128;
            var legacy = new WorldMapModel(
                legacyWidth,
                legacyHeight,
                new WorldSeed(seedValue));
            var cells = new WorldCell[worldWidth, worldHeight];
            var sparseCell = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (var x = 0; x < worldWidth; x++)
            for (var y = 0; y < worldHeight; y++)
                cells[x, y] = sparseCell;
            for (var x = 0; x < legacyWidth; x++)
            for (var y = 0; y < legacyHeight; y++)
                cells[x + legacyOffsetX, y + legacyOffsetY] =
                    legacy.Get(x, y);
            var versionOne = new WorldMapModel(cells);
            var nodes = new List<FormalThreeDResourceNodeSaveData>(
                versionOne.ResourceNodeCount);
            for (var x = 0; x < versionOne.Width; x++)
            for (var y = 0; y < versionOne.Height; y++)
            {
                WorldCell cell = versionOne.Get(x, y);
                if (!cell.HasResource) continue;
                nodes.Add(new FormalThreeDResourceNodeSaveData
                {
                    stableNodeId =
                        GrayboxResourceNodeIdentity3D.Create(x, y),
                    x = x,
                    y = y,
                    resourceId = cell.ResourceId,
                    remainingAmount = cell.ResourceAmount,
                    isDepleted = cell.ResourceAmount == 0,
                });
            }
            nodes.Sort((left, right) => string.CompareOrdinal(
                left.stableNodeId,
                right.stableNodeId));
            return new FormalThreeDWorldSaveData
            {
                worldDefinitionId =
                    GrayboxWorldCitySaveAdapter3D.WorldDefinitionId,
                worldGenerationVersion = 1,
                worldSeed = seedValue,
                width = worldWidth,
                height = worldHeight,
                configurationSignature =
                    "core.world.formal-3d.v1.64x48",
                resourceNodes = nodes.ToArray(),
                orphanResources =
                    Array.Empty<FormalThreeDOrphanResourceSaveData>(),
            };
        }

        private static ResourceCell FindResourceCell(WorldMapModel world)
        {
            for (int x = 0; x < world.Width; x++)
            for (int y = 0; y < world.Height; y++)
            {
                WorldCell cell = world.Get(x, y);
                if (cell.HasResource && cell.ResourceAmount >= 20)
                    return new ResourceCell(x, y);
            }
            Assert.Fail("The formal world fixture must contain a resource node.");
            return default;
        }

        private static void FindReachablePair(
            WorldMapModel world,
            out int startX,
            out int startY,
            out int destinationX,
            out int destinationY)
        {
            int[] offsetX = { 1, -1, 0, 0 };
            int[] offsetY = { 0, 0, 1, -1 };
            for (int radius = 0; radius < Math.Max(world.Width, world.Height); radius++)
            {
                int minimumX = Math.Max(0, world.Width / 2 - radius);
                int maximumX = Math.Min(world.Width - 1, world.Width / 2 + radius);
                int minimumY = Math.Max(0, world.Height / 2 - radius);
                int maximumY = Math.Min(world.Height - 1, world.Height / 2 + radius);
                for (int x = minimumX; x <= maximumX; x++)
                for (int y = minimumY; y <= maximumY; y++)
                {
                    if (!CityTerrainRules.IsPassable(world.Get(x, y)))
                        continue;
                    for (int offset = 0; offset < offsetX.Length; offset++)
                    {
                        int nextX = x + offsetX[offset];
                        int nextY = y + offsetY[offset];
                        if (nextX < 0 || nextY < 0 ||
                            nextX >= world.Width || nextY >= world.Height ||
                            !CityTerrainRules.IsPassable(
                                world.Get(nextX, nextY)))
                            continue;
                        if (CityPathfinder.TryFindPath(
                                world,
                                x,
                                y,
                                nextX,
                                nextY,
                                out WorldGridPoint[] route) &&
                            route.Length > 0)
                        {
                            startX = x;
                            startY = y;
                            destinationX = nextX;
                            destinationY = nextY;
                            return;
                        }
                    }
                }
            }
            Assert.Fail("The formal world must contain adjacent passable cells.");
            startX = startY = destinationX = destinationY = 0;
        }

        private static void FindBlockedCell(
            WorldMapModel world,
            out int x,
            out int y)
        {
            for (x = 0; x < world.Width; x++)
            for (y = 0; y < world.Height; y++)
            {
                if (!CityTerrainRules.IsPassable(world.Get(x, y)))
                    return;
            }
            Assert.Fail("The formal world fixture must contain a blocked cell.");
            x = y = 0;
        }

        private static FormalThreeDCitySaveData CloneCity(
            FormalThreeDCitySaveData source)
        {
            return new FormalThreeDCitySaveData
            {
                positionX = source.positionX,
                positionZ = source.positionZ,
                cellX = source.cellX,
                cellY = source.cellY,
                autopilotActive = source.autopilotActive,
                destinationX = source.destinationX,
                destinationY = source.destinationY,
                cityMode = source.cityMode,
                transitionReturnMode = source.transitionReturnMode,
                transitionRemainingSeconds =
                    source.transitionRemainingSeconds,
                population = source.population,
                populationCapacity = source.populationCapacity,
            };
        }

        private static Vector3 ExpectedCityVisualSize(
            CityMode mode,
            float remainingSeconds)
        {
            float fortressFactor = FortressFactor(mode, remainingSeconds);
            return Vector3.Lerp(
                new Vector3(8.6f, .65f, 6.6f),
                new Vector3(8.8f, .8f, 6.8f),
                fortressFactor);
        }

        private static Vector3 ExpectedCityColliderSize(
            CityMode mode,
            float remainingSeconds)
        {
            float fortressFactor = FortressFactor(mode, remainingSeconds);
            return Vector3.Lerp(
                new Vector3(3f, 1f, 2f),
                new Vector3(3f, 1.5f, 3f),
                fortressFactor);
        }

        private static float FortressFactor(
            CityMode mode,
            float remainingSeconds)
        {
            return mode == CityMode.Deploying
                ? 1f - remainingSeconds /
                  CityDeploymentRules.FormalDeployDurationSeconds
                : remainingSeconds /
                  CityDeploymentRules.FormalPackDurationSeconds;
        }

        private static void AssertDestination(
            GrayboxMobileCityController3D city,
            int expectedX,
            int expectedY)
        {
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(expectedX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(expectedY));
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private readonly struct ResourceCell
        {
            public ResourceCell(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private sealed class WorldCityFixture
        {
            public WorldCityFixture(
                GrayboxSceneBootstrap bootstrap,
                GrayboxWorldView3D view,
                GrayboxMobileCityController3D city,
                Rigidbody body,
                BoxCollider bodyCollider,
                Transform visualTransform,
                GrayboxBuildingSession3D session,
                GrayboxWorldCitySaveAdapter3D adapter,
                Vector3 startPosition,
                Vector3 destinationPosition,
                int destinationX,
                int destinationY)
            {
                Bootstrap = bootstrap;
                View = view;
                City = city;
                Body = body;
                BodyCollider = bodyCollider;
                VisualTransform = visualTransform;
                Session = session;
                Adapter = adapter;
                StartPosition = startPosition;
                DestinationPosition = destinationPosition;
                DestinationX = destinationX;
                DestinationY = destinationY;
            }

            public GrayboxSceneBootstrap Bootstrap { get; }
            public GrayboxWorldView3D View { get; }
            public GrayboxMobileCityController3D City { get; }
            public Rigidbody Body { get; }
            public BoxCollider BodyCollider { get; }
            public Transform VisualTransform { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxWorldCitySaveAdapter3D Adapter { get; }
            public Vector3 StartPosition { get; }
            public Vector3 DestinationPosition { get; }
            public int DestinationX { get; }
            public int DestinationY { get; }
        }

        private sealed class RuntimeFingerprint
        {
            private readonly WorldMapModel world;
            private readonly int[] resourceAmounts;
            private readonly Vector3 position;
            private readonly CityMode mode;
            private readonly CityMode returnMode;
            private readonly float remaining;
            private readonly bool autopilotActive;
            private readonly WorldGridPoint? destination;
            private readonly int population;
            private readonly int populationCapacity;

            private RuntimeFingerprint(WorldCityFixture fixture)
            {
                world = fixture.Bootstrap.World;
                resourceAmounts = world.CaptureResourceAmounts();
                position = fixture.City.WorldPosition;
                mode = fixture.City.Mode;
                returnMode = fixture.City.Deployment.TransitionReturnMode;
                remaining = fixture.City.Deployment.Remaining;
                autopilotActive = fixture.City.AutopilotActive;
                destination = fixture.City.Destination;
                population = fixture.Session.Population;
                populationCapacity = fixture.Session.PopulationCapacity;
            }

            public static RuntimeFingerprint Capture(WorldCityFixture fixture)
            {
                return new RuntimeFingerprint(fixture);
            }

            public void AssertUnchanged(WorldCityFixture fixture)
            {
                Assert.That(fixture.Bootstrap.World, Is.SameAs(world));
                Assert.That(
                    fixture.Bootstrap.World.CaptureResourceAmounts(),
                    Is.EqualTo(resourceAmounts));
                Assert.That(
                    fixture.City.WorldPosition,
                    Is.EqualTo(position)
                        .Using(Vector3ComparerWithEqualsOperator.Instance));
                Assert.That(fixture.City.Mode, Is.EqualTo(mode));
                Assert.That(
                    fixture.City.Deployment.TransitionReturnMode,
                    Is.EqualTo(returnMode));
                Assert.That(
                    fixture.City.Deployment.Remaining,
                    Is.EqualTo(remaining).Within(.0001f));
                Assert.That(
                    fixture.City.AutopilotActive,
                    Is.EqualTo(autopilotActive));
                Assert.That(fixture.City.Destination, Is.EqualTo(destination));
                Assert.That(fixture.Session.Population, Is.EqualTo(population));
                Assert.That(
                    fixture.Session.PopulationCapacity,
                    Is.EqualTo(populationCapacity));
            }
        }

        private static Type RequireAdapterType()
        {
            Type type = typeof(GrayboxBuildingSession3D).Assembly.GetType(
                AdapterTypeName);
            Assert.That(
                type,
                Is.Not.Null,
                "Expected Task 4 adapter type " + AdapterTypeName + ".");
            return type;
        }

        private static MethodInfo RequirePublicInstanceMethod(
            Type owner,
            string methodName,
            Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo method = FindPublicInstanceMethod(
                owner,
                methodName,
                parameterTypes);
            AssertMethodReturn(method, returnType, owner.FullName + "." + methodName);
            return method;
        }

        private static MethodInfo FindPublicInstanceMethod(
            Type owner,
            string methodName,
            Type[] parameterTypes)
        {
            return owner.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
        }

        private static void AssertMethodReturn(
            MethodInfo method,
            Type returnType,
            string contractName)
        {
            Assert.That(
                method,
                Is.Not.Null,
                contractName + " must be public with the required narrow " +
                "signature.");
            if (method != null)
                Assert.That(method.ReturnType, Is.EqualTo(returnType));
        }
    }
}
