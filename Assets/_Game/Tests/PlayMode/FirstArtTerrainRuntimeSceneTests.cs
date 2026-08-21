using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WasteCity.ArtIntegration3D;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainRuntimeSceneTests
    {
        private const string SceneName = "GrayboxPrototype3D";
        private const int ControlPixelsPerCell = 4;

        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.UpdateMode previousInputUpdateMode;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;
        private InputSettings.EditorInputBehaviorInPlayMode
            previousEditorInputBehavior;
        private float previousTimeScale;
        private RenderPipelineAsset previousGraphics;
        private RenderPipelineAsset previousQuality;

        [UnitySetUp]
        public IEnumerator LoadGrayboxScene()
        {
            previousTimeScale = Time.timeScale;
            previousGraphics = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            previousInputUpdateMode = InputSystem.settings.updateMode;
            previousBackgroundBehavior =
                InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior =
                InputSystem.settings.editorInputBehaviorInPlayMode;

            Time.timeScale = 1f;
            InputSystem.settings.updateMode =
                InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();

            GrayboxFormalPlayModeEntryFixture.BeginIsolatedStore();
            yield return SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return GrayboxFormalPlayModeEntryFixture
                .StartNewProgressThroughRealUi(mouse);
        }

        [UnityTearDown]
        public IEnumerator UnloadGrayboxScene()
        {
            try
            {
                Time.timeScale = 1f;
                yield return LoadEmptyScene();
            }
            finally
            {
                try
                {
                    GrayboxFormalPlayModeEntryFixture.CleanupIsolatedStore();
                }
                finally
                {
                try
                {
                    if (keyboard != null && keyboard.added)
                        InputSystem.RemoveDevice(keyboard);
                }
                finally
                {
                    try
                    {
                        if (mouse != null && mouse.added)
                            InputSystem.RemoveDevice(mouse);
                    }
                    finally
                    {
                        try
                        {
                            InputSystem.settings.updateMode =
                                previousInputUpdateMode;
                        }
                        finally
                        {
                            try
                            {
                                InputSystem.settings
                                    .editorInputBehaviorInPlayMode =
                                    previousEditorInputBehavior;
                            }
                            finally
                            {
                                try
                                {
                                    InputSystem.settings.backgroundBehavior =
                                        previousBackgroundBehavior;
                                }
                                finally
                                {
                                    try
                                    {
                                        GraphicsSettings
                                            .defaultRenderPipeline =
                                            previousGraphics;
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            QualitySettings.renderPipeline =
                                                previousQuality;
                                        }
                                        finally
                                        {
                                            Time.timeScale =
                                                previousTimeScale;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                    GrayboxFormalPlayModeEntryFixture
                        .AssertRealSaveFilesUnchanged();
                }

                Assert.That(keyboard == null || !keyboard.added, Is.True);
                Assert.That(mouse == null || !mouse.added, Is.True);
                Assert.That(
                    InputSystem.settings.updateMode,
                    Is.EqualTo(previousInputUpdateMode));
                Assert.That(
                    InputSystem.settings.editorInputBehaviorInPlayMode,
                    Is.EqualTo(previousEditorInputBehavior));
                Assert.That(
                    InputSystem.settings.backgroundBehavior,
                    Is.EqualTo(previousBackgroundBehavior));
                Assert.That(
                    GraphicsSettings.defaultRenderPipeline,
                    Is.SameAs(previousGraphics));
                Assert.That(
                    QualitySettings.renderPipeline,
                    Is.SameAs(previousQuality));
                Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
            }
        }

        [UnityTest]
        public IEnumerator Scene_AtomicallyShowsOneFormalTerrainAndKeepsResources()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxWorldView3D world = World();

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(presenter.SurfaceRenderer, Is.Not.Null);
            Assert.That(
                presenter.SurfaceRenderer.sharedMaterial,
                Is.EqualTo(presenter.Profile.Material));
            Assert.That(
                presenter.SurfaceRenderer.sharedMaterial.GetInstanceID(),
                Is.EqualTo(presenter.Profile.Material.GetInstanceID()));
            Assert.That(
                Object.FindObjectsOfType<FirstArtTerrainRenderer3D>().Length,
                Is.EqualTo(1));
            Assert.That(
                presenter.GetComponentsInChildren<MeshRenderer>().Length,
                Is.EqualTo(3));
            Assert.That(
                presenter.GetComponentsInChildren<MeshFilter>().Length,
                Is.EqualTo(3));
            Assert.That(
                presenter.GetComponentsInChildren<Collider>(),
                Is.Empty);
            Assert.That(
                presenter.SurfaceRenderer.transform.position.y,
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(world.SurfaceFallbackVisible, Is.False);
            AssertFallbackAndPlaceholderSlots(world, false);
            AssertRuinsCliffPresentation(presenter, world);

            FirstArtTerrainControlMap3D maps = presenter.ControlMaps;
            Assert.That(maps, Is.Not.Null);
            Assert.That(maps.ControlA, Is.Not.Null);
            Assert.That(maps.ControlB, Is.Not.Null);
            Assert.That(maps.ControlA, Is.Not.SameAs(maps.ControlB));
            var block = new MaterialPropertyBlock();
            presenter.SurfaceRenderer.GetPropertyBlock(block);
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_ControlA")),
                Is.SameAs(maps.ControlA));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_ControlB")),
                Is.SameAs(maps.ControlB));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PresenterLifecycle_RestoresFallbackRecreatesOnceAndRollsBackFailure()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxWorldView3D world = World();
            FirstArtTerrainProfile3D approvedProfile = presenter.Profile;

            presenter.enabled = false;
            yield return null;

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(
                presenter.transform.Find("RuntimeSurface"),
                Is.Null);
            Assert.That(RuntimeTerrainMeshCount(), Is.Zero);
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.Zero);
            Assert.That(RuntimeControlTextureCount(), Is.Zero);
            AssertFallbackAndPlaceholderSlots(world, true);

            presenter.enabled = true;
            yield return null;

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(presenter.Profile, Is.SameAs(approvedProfile));
            Assert.That(
                presenter.GetComponentsInChildren<MeshRenderer>().Length,
                Is.EqualTo(3));
            Assert.That(RuntimeTerrainMeshCount(), Is.EqualTo(1));
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.EqualTo(2));
            Assert.That(RuntimeControlTextureCount(), Is.EqualTo(2));
            AssertFallbackAndPlaceholderSlots(world, false);
            AssertRuinsCliffPresentation(presenter, world);

            presenter.enabled = false;
            yield return null;
            presenter.Configure(null);
            LogAssert.Expect(
                LogType.Error,
                "First-art terrain presentation failed: " +
                "Terrain profile is required.");
            presenter.enabled = true;
            yield return null;

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(
                presenter.transform.Find("RuntimeSurface"),
                Is.Null);
            Assert.That(RuntimeTerrainMeshCount(), Is.Zero);
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.Zero);
            Assert.That(RuntimeControlTextureCount(), Is.Zero);
            AssertFallbackAndPlaceholderSlots(world, true);
        }

        [UnityTest]
        public IEnumerator IDEA0004_CategoryFallbackDeactivatesPreviousGeometryInSameFrame()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxWorldView3D world = World();
            Transform previousRoot =
                presenter.transform.Find("RuntimeGeometry");
            Assert.That(previousRoot, Is.Not.Null);
            MeshRenderer[] previousRenderers =
                previousRoot.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(previousRenderers, Has.Length.EqualTo(2));
            Assert.That(
                previousRenderers.All(renderer =>
                    renderer != null && renderer.gameObject.activeInHierarchy),
                Is.True);

            using (FirstArtRuinsCliffGeometry3D.OverrideTestConfiguration(
                       true,
                       "AfterPreflight"))
            {
                Assert.That(presenter.TryPresent(world, false), Is.True);
            }

            Assert.That(
                presenter.RuinsStatus,
                Is.EqualTo(
                    FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(
                presenter.CliffStatus,
                Is.EqualTo(
                    FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(
                world.IsSurfaceFallbackVisible("world.obstacle.ruins"),
                Is.True);
            Assert.That(
                world.IsSurfaceFallbackVisible("world.obstacle.cliff"),
                Is.True);
            Assert.That(
                previousRenderers.All(renderer =>
                    renderer == null || !renderer.gameObject.activeInHierarchy),
                Is.True,
                "Fallback must hide the previous category geometry before " +
                "PlayMode's delayed Destroy is processed.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator RuinsCliffGeometry_ProjectsEveryRuleCellIntoTwoBatches()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxWorldView3D world = World();
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> placements =
                FirstArtRuinsCliffLayout3D.Project(
                    world.Model,
                    world.Coordinates);

            int ruinsCells = 0;
            int cliffCells = 0;
            for (int y = 0; y < world.Model.Height; y++)
            for (int x = 0; x < world.Model.Width; x++)
            {
                WorldTraversalKind traversal =
                    world.Model.Get(x, y).Traversal;
                if (traversal != WorldTraversalKind.Ruins &&
                    traversal != WorldTraversalKind.Cliff)
                    continue;

                FirstArtRuinsCliffFamily3D expectedFamily =
                    traversal == WorldTraversalKind.Ruins
                        ? FirstArtRuinsCliffFamily3D.Ruins
                        : FirstArtRuinsCliffFamily3D.Cliff;
                FirstArtRuinsCliffPlacement3D[] cellPlacements =
                    placements.Where(placement =>
                            placement.CellX == x &&
                            placement.CellY == y)
                        .ToArray();
                Assert.That(
                    cellPlacements,
                    Has.Length.EqualTo(1),
                    $"Rule cell {x},{y} must produce exactly one placement.");
                Assert.That(
                    cellPlacements[0].Family,
                    Is.EqualTo(expectedFamily),
                    $"Rule cell {x},{y} projected the wrong family.");
                Assert.That(
                    cellPlacements[0].CatalogIndex,
                    Is.InRange(0, FirstArtRuinsCliffCatalog3D.EntryCount - 1));
                Assert.That(
                    cellPlacements[0].WorldMatrix.ValidTRS(),
                    Is.True,
                    $"Rule cell {x},{y} projected an invalid matrix.");

                if (expectedFamily == FirstArtRuinsCliffFamily3D.Ruins)
                    ruinsCells++;
                else
                    cliffCells++;
            }

            Assert.That(ruinsCells, Is.GreaterThan(0));
            Assert.That(cliffCells, Is.GreaterThan(0));
            Assert.That(placements, Has.Count.EqualTo(ruinsCells + cliffCells));
            Assert.That(
                placements.Count(placement =>
                    placement.Family == FirstArtRuinsCliffFamily3D.Ruins),
                Is.EqualTo(ruinsCells));
            Assert.That(
                placements.Count(placement =>
                    placement.Family == FirstArtRuinsCliffFamily3D.Cliff),
                Is.EqualTo(cliffCells));
            AssertRuinsCliffPresentation(presenter, world);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorldRebuild_ReprojectsBothBatchesWithoutRuntimeResidue()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxWorldView3D world = World();
            Mesh[] geometryBefore = GeometryMeshes(presenter);
            Assert.That(geometryBefore, Has.Length.EqualTo(2));

            world.Generate(GrayboxWorldLayout3D.CreateDefault());
            yield return null;

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(world.HasActiveTerrainPresentation, Is.True);
            Assert.That(RuntimeTerrainMeshCount(), Is.EqualTo(1));
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.EqualTo(2));
            Assert.That(
                geometryBefore.All(mesh => mesh == null),
                Is.True,
                "World rebuild must release both previous category meshes.");
            AssertRuinsCliffPresentation(presenter, world);
        }

        [UnityTest]
        public IEnumerator ControlMaps_DeclareTheLayerOfEverySeed8128Cell()
        {
            FirstArtTerrainControlMap3D maps = Presenter().ControlMaps;
            WorldMapModel sceneModel = World().Model;
            WorldMapModel seed8128Model =
                GrayboxWorldLayout3D.CreateDefault();

            Assert.That(sceneModel.Width, Is.EqualTo(64));
            Assert.That(sceneModel.Height, Is.EqualTo(48));
            Assert.That(
                maps.Width,
                Is.EqualTo(seed8128Model.Width * ControlPixelsPerCell));
            Assert.That(
                maps.Height,
                Is.EqualTo(seed8128Model.Height * ControlPixelsPerCell));
            int checkedCells = 0;
            int sparseOuterCells = 0;
            for (int cellY = 0; cellY < seed8128Model.Height; cellY++)
            for (int cellX = 0; cellX < seed8128Model.Width; cellX++)
            {
                WorldCell expectedCell = seed8128Model.Get(cellX, cellY);
                WorldCell sceneCell = sceneModel.Get(cellX, cellY);
                Assert.That(
                    sceneCell.Terrain,
                    Is.EqualTo(expectedCell.Terrain),
                    $"Seed 8128 terrain mismatch at {cellX},{cellY}.");
                Assert.That(
                    sceneCell.Traversal,
                    Is.EqualTo(expectedCell.Traversal),
                    $"Seed 8128 traversal mismatch at {cellX},{cellY}.");
                Assert.That(
                    sceneCell.ResourceId,
                    Is.EqualTo(expectedCell.ResourceId),
                    $"Seed 8128 resource mismatch at {cellX},{cellY}.");
                Assert.That(
                    sceneCell.ResourceAmount,
                    Is.EqualTo(expectedCell.ResourceAmount),
                    $"Seed 8128 amount mismatch at {cellX},{cellY}.");
                bool isOuter =
                    cellX < GrayboxWorldLayout3D.LegacyOffsetX ||
                    cellX >= GrayboxWorldLayout3D.LegacyOffsetX +
                        GrayboxWorldLayout3D.LegacyWidth ||
                    cellY < GrayboxWorldLayout3D.LegacyOffsetY ||
                    cellY >= GrayboxWorldLayout3D.LegacyOffsetY +
                        GrayboxWorldLayout3D.LegacyHeight;
                if (isOuter)
                {
                    Assert.That(sceneCell.Terrain,
                        Is.EqualTo(TerrainKind.Wasteland));
                    Assert.That(sceneCell.Traversal,
                        Is.EqualTo(WorldTraversalKind.Open));
                    Assert.That(sceneCell.ResourceId, Is.Null);
                    Assert.That(sceneCell.ResourceAmount, Is.Zero);
                    sparseOuterCells++;
                }

                var totals = new int[FirstArtTerrainCatalog3D.LayerCount];
                for (int pixelY = 0;
                     pixelY < ControlPixelsPerCell;
                     pixelY++)
                for (int pixelX = 0;
                     pixelX < ControlPixelsPerCell;
                     pixelX++)
                {
                    int x = cellX * ControlPixelsPerCell + pixelX;
                    int y = cellY * ControlPixelsPerCell + pixelY;
                    int offset = (y * maps.Width + x) * 4;
                    for (int layer = 0; layer < 4; layer++)
                        totals[layer] += maps.ControlABytes[offset + layer];
                    for (int layer = 4;
                         layer < FirstArtTerrainCatalog3D.LayerCount;
                         layer++)
                    {
                        totals[layer] +=
                            maps.ControlBBytes[offset + layer - 4];
                    }
                }

                FirstArtTerrainLayer3D expected =
                    ExpectedLayer(expectedCell);
                int declaredTotal = totals[(int)expected];
                Assert.That(
                    declaredTotal,
                    Is.GreaterThan(0),
                    $"Seed 8128 cell {cellX},{cellY} must declare " +
                    $"{expected}; totals={string.Join(",", totals)}.");
                for (int layer = 0;
                     layer < FirstArtTerrainCatalog3D.LayerCount;
                     layer++)
                {
                    if (layer == (int)expected)
                        continue;
                    Assert.That(
                        declaredTotal,
                        Is.GreaterThan(totals[layer]),
                        $"Seed 8128 cell {cellX},{cellY} must declare " +
                        $"{expected} strictly above " +
                        $"{(FirstArtTerrainLayer3D)layer}; totals=" +
                        string.Join(",", totals) + ".");
                }
                checkedCells++;
            }

            Assert.That(checkedCells, Is.EqualTo(64 * 48));
            Assert.That(sparseOuterCells, Is.EqualTo(2304));
            Assert.That(
                sceneModel.ResourceNodeCount,
                Is.EqualTo(seed8128Model.ResourceNodeCount));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedMaterial_RemainsTheProfileAssetAcrossFrames()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            MeshRenderer renderer = presenter.SurfaceRenderer;
            Material sharedAsset = presenter.Profile.Material;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.That(presenter.SurfaceRenderer, Is.SameAs(renderer));
                Assert.That(
                    renderer.sharedMaterial,
                    Is.EqualTo(sharedAsset),
                    "Runtime terrain must not instantiate or replace its material.");
                Assert.That(
                    renderer.sharedMaterial.GetInstanceID(),
                    Is.EqualTo(sharedAsset.GetInstanceID()));
            }

            Assert.That(
                presenter.GetComponentsInChildren<MeshRenderer>().Length,
                Is.EqualTo(3));
            AssertRuinsCliffPresentation(presenter, World());
        }

        [UnityTest]
        public IEnumerator SceneUnload_ReleasesTheRuntimeMeshAndBothControlTextures()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            Mesh mesh = presenter.SurfaceRenderer
                .GetComponent<MeshFilter>().sharedMesh;
            Texture2D controlA = presenter.ControlMaps.ControlA;
            Texture2D controlB = presenter.ControlMaps.ControlB;
            Assert.That(RuntimeTerrainMeshCount(), Is.EqualTo(1));
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.EqualTo(2));
            Assert.That(RuntimeControlTextureCount(), Is.EqualTo(2));

            yield return LoadEmptyScene();
            yield return null;

            Assert.That(presenter == null, Is.True);
            Assert.That(mesh == null, Is.True);
            Assert.That(controlA == null, Is.True);
            Assert.That(controlB == null, Is.True);
            Assert.That(RuntimeTerrainMeshCount(), Is.Zero);
            Assert.That(RuntimeRuinsCliffMeshCount(), Is.Zero);
            Assert.That(RuntimeControlTextureCount(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator VirtualWASD_MovesTheMobileCityAboveColliderlessFormalTerrain()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxMobileCityController3D city = City();
            Vector3 cityBefore = city.transform.position;
            Vector3 terrainBefore =
                presenter.SurfaceRenderer.transform.position;

            yield return HoldKey(Key.W, 3);

            Assert.That(city.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(
                city.transform.position.z,
                Is.GreaterThan(cityBefore.z));
            Assert.That(
                city.transform.position.y,
                Is.EqualTo(cityBefore.y).Within(.0001f));
            Assert.That(
                presenter.SurfaceRenderer.transform.position,
                Is.EqualTo(terrainBefore));
            Assert.That(terrainBefore.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(
                presenter.GetComponentsInChildren<Collider>(),
                Is.Empty);
        }

        [UnityTest]
        public IEnumerator VirtualRightClick_AStarReducesDistanceAcrossFixedUpdates()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxMobileCityController3D city = City();
            GrayboxWorldView3D world = World();
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int startX,
                    out int startY),
                Is.True);
            FindReachableDestination(
                world.Model,
                startX,
                startY,
                out int targetX,
                out int targetY);
            Assert.That(
                world.Coordinates.TryCellToWorld(
                    targetX,
                    targetY,
                    0f,
                    out Vector3 destinationWorld),
                Is.True);
            Vector3 clickWorld =
                destinationWorld + new Vector3(.5f, 0f, .5f);

            yield return ClickMouse(
                MouseButton.Right,
                Camera.main.WorldToScreenPoint(clickWorld));

            Assert.That(city.AutopilotActive, Is.True);
            Assert.That(city.Destination.HasValue, Is.True);
            Assert.That(city.Destination.Value.X, Is.EqualTo(targetX));
            Assert.That(city.Destination.Value.Y, Is.EqualTo(targetY));
            Rigidbody body = city.GetComponent<Rigidbody>();
            float distanceBefore = Vector2.Distance(
                new Vector2(body.position.x, body.position.z),
                new Vector2(destinationWorld.x, destinationWorld.z));

            for (int step = 0; step < 3; step++)
                yield return new WaitForFixedUpdate();

            float distanceAfter = Vector2.Distance(
                new Vector2(body.position.x, body.position.z),
                new Vector2(destinationWorld.x, destinationWorld.z));
            Assert.That(distanceAfter, Is.LessThan(distanceBefore));
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(World().SurfaceFallbackVisible, Is.False);
            AssertRuinsCliffPresentation(presenter, world);
        }

        [UnityTest]
        public IEnumerator VirtualF_CompletesDeploymentThenPackingThroughRealFrames()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxMobileCityController3D city = City();
            GrayboxWorldView3D world = World();
            Rigidbody body = city.GetComponent<Rigidbody>();
            FindDeploymentCell(
                world.Model,
                out int validX,
                out int validY);
            MoveCityToCell(
                city,
                body,
                world.Coordinates,
                validX,
                validY);

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Deploying));
            city.Deployment.Restore(CityMode.Deploying, .001f);
            yield return WaitForCityMode(city, CityMode.Fortress);

            yield return TapKey(Key.F);
            Assert.That(city.Mode, Is.EqualTo(CityMode.Packing));
            city.Deployment.Restore(CityMode.Packing, .001f);
            yield return WaitForCityMode(city, CityMode.Mobile);

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(world.SurfaceFallbackVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator VirtualB_UsesMathematicalGroundAndRulesRejectWaterAndCliff()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxMobileCityController3D city = City();
            GrayboxWorldView3D world = World();
            GrayboxBuildingInteractionModel3D interaction =
                Object.FindObjectOfType<
                    GrayboxBuildingInteractionModel3D>();
            GrayboxBuildingMenuView3D menu =
                Object.FindObjectOfType<GrayboxBuildingMenuView3D>();
            GrayboxBuildingPlacementController3D placement =
                Object.FindObjectOfType<
                    GrayboxBuildingPlacementController3D>();
            Assert.That(interaction, Is.Not.Null);
            Assert.That(menu, Is.Not.Null);
            Assert.That(placement, Is.Not.Null);
            city.Deployment.Restore(CityMode.Fortress, 0f);
            yield return null;

            yield return TapKey(Key.B);
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(menu.CatalogVisible, Is.True);
            yield return TapKey(Key.Digit2);
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Housing));
            Assert.That(
                interaction.State,
                Is.EqualTo(GrayboxBuildingInteractionState.Previewing));

            yield return MoveToValidGroundPreview(city, world, placement);
            Assert.That(placement.CurrentHit.IsValid, Is.True);
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.Ground));
            Assert.That(
                placement.CurrentHit.WorldPoint.y,
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(placement.CurrentEvaluation.IsValid, Is.True);
            Assert.That(
                presenter.GetComponentsInChildren<Collider>(),
                Is.Empty);

            var deepWaterTarget = new TraversalTarget();
            yield return MoveToTraversal(
                world,
                placement,
                WorldTraversalKind.DeepWater,
                deepWaterTarget);
            AssertInvalidTerrain(
                world,
                placement,
                WorldTraversalKind.DeepWater,
                deepWaterTarget);
            var cliffTarget = new TraversalTarget();
            yield return MoveToTraversal(
                world,
                placement,
                WorldTraversalKind.Cliff,
                cliffTarget);
            AssertInvalidTerrain(
                world,
                placement,
                WorldTraversalKind.Cliff,
                cliffTarget);
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(world.SurfaceFallbackVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator VirtualMiddleDragAndHome_WorkWhileFormalTerrainIsActive()
        {
            FirstArtTerrainRenderer3D presenter = Presenter();
            GrayboxCameraController3D cameraController =
                Object.FindObjectOfType<GrayboxCameraController3D>();
            Assert.That(cameraController, Is.Not.Null);
            Vector2 start = new Vector2(
                Screen.width * .5f,
                Screen.height * .5f);
            Vector2 end = start + new Vector2(100f, 40f);

            yield return DragMouse(start, end);

            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Free));
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(World().SurfaceFallbackVisible, Is.False);

            yield return TapKey(Key.Home);

            Assert.That(
                cameraController.Mode,
                Is.EqualTo(CameraFollowMode.Following));
            Assert.That(presenter.IsPresented, Is.True);
        }

        private static FirstArtTerrainRenderer3D Presenter()
        {
            FirstArtTerrainRenderer3D presenter =
                Object.FindObjectOfType<FirstArtTerrainRenderer3D>();
            Assert.That(presenter, Is.Not.Null);
            return presenter;
        }

        private static GrayboxWorldView3D World()
        {
            GrayboxWorldView3D world =
                Object.FindObjectOfType<GrayboxWorldView3D>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.Model, Is.Not.Null);
            Assert.That(world.Coordinates, Is.Not.Null);
            return world;
        }

        private static GrayboxMobileCityController3D City()
        {
            GrayboxMobileCityController3D city =
                Object.FindObjectOfType<
                    GrayboxMobileCityController3D>();
            Assert.That(city, Is.Not.Null);
            return city;
        }

        private static void AssertFallbackAndPlaceholderSlots(
            GrayboxWorldView3D world,
            bool fallbackVisible)
        {
            GrayboxVisualSlot[] slots =
                world.GetComponentsInChildren<GrayboxVisualSlot>(true);
            GrayboxVisualSlot[] surfaces = slots
                .Where(slot =>
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(
                        slot.StableId))
                .ToArray();
            GrayboxVisualSlot[] placeholders = slots
                .Where(slot =>
                    !FirstArtTerrainCatalog3D.IsSurfaceStableId(
                        slot.StableId))
                .ToArray();

            Assert.That(surfaces, Has.Length.EqualTo(7));
            Assert.That(
                surfaces.Select(slot => slot.StableId).Distinct().Count(),
                Is.EqualTo(7));
            for (int index = 0; index < surfaces.Length; index++)
            {
                Assert.That(surfaces[index].Renderer, Is.Not.Null);
                Assert.That(
                    surfaces[index].gameObject.activeInHierarchy,
                    Is.True,
                    surfaces[index].StableId);
                Assert.That(
                    surfaces[index].Renderer.gameObject.activeInHierarchy,
                    Is.True,
                    surfaces[index].StableId);
                Assert.That(
                    surfaces[index].Renderer.enabled,
                    Is.EqualTo(fallbackVisible),
                    surfaces[index].StableId);
            }

            Assert.That(placeholders, Is.Not.Empty);
            for (int index = 0; index < placeholders.Length; index++)
            {
                Assert.That(placeholders[index].Renderer, Is.Not.Null);
                Assert.That(
                    placeholders[index].gameObject.activeInHierarchy,
                    Is.True,
                    placeholders[index].StableId);
                Assert.That(
                    placeholders[index].Renderer.gameObject.activeInHierarchy,
                    Is.True,
                    placeholders[index].StableId);
                Assert.That(
                    placeholders[index].Renderer.enabled,
                    Is.True,
                    placeholders[index].StableId);
            }
            Assert.That(
                world.SurfaceFallbackVisible,
                Is.EqualTo(fallbackVisible));
        }

        private static FirstArtTerrainLayer3D ExpectedLayer(WorldCell cell)
        {
            switch (cell.Traversal)
            {
                case WorldTraversalKind.Ruins:
                    return FirstArtTerrainLayer3D.Ruins;
                case WorldTraversalKind.DeepWater:
                    return FirstArtTerrainLayer3D.DeepWater;
                case WorldTraversalKind.Cliff:
                    return FirstArtTerrainLayer3D.Cliff;
                case WorldTraversalKind.Open:
                    break;
                default:
                    throw new AssertionException(
                        "Unexpected traversal " + cell.Traversal + ".");
            }

            switch (cell.Terrain)
            {
                case TerrainKind.Wasteland:
                    return FirstArtTerrainLayer3D.Wasteland;
                case TerrainKind.Rocky:
                    return FirstArtTerrainLayer3D.Rocky;
                case TerrainKind.Wetland:
                    return FirstArtTerrainLayer3D.Wetland;
                case TerrainKind.Crystal:
                    return FirstArtTerrainLayer3D.Crystal;
                default:
                    throw new AssertionException(
                        "Unexpected terrain " + cell.Terrain + ".");
            }
        }

        private IEnumerator HoldKey(Key key, int fixedSteps)
        {
            QueueKeyboard(key);
            yield return null;
            for (int step = 0; step < fixedSteps; step++)
                yield return new WaitForFixedUpdate();
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator TapKey(Key key)
        {
            QueueKeyboard(key);
            yield return null;
            QueueKeyboard();
            yield return null;
        }

        private IEnumerator ClickMouse(
            MouseButton button,
            Vector2 position)
        {
            QueueMouse(position, button);
            yield return null;
            QueueMouse(position);
            yield return null;
        }

        private IEnumerator DragMouse(Vector2 start, Vector2 end)
        {
            QueueMouse(start, MouseButton.Middle);
            yield return null;
            QueueMouse(
                end,
                MouseButton.Middle,
                expectPressedThisFrame: false);
            yield return null;
            QueueMouse(end);
            yield return null;
        }

        private IEnumerator MoveMouse(Vector2 position)
        {
            QueueMouse(position);
            yield return null;
        }

        private void QueueKeyboard(params Key[] keys)
        {
            InputSystem.QueueStateEvent(
                keyboard,
                new KeyboardState(keys));
            InputSystem.Update();
            Assert.That(Keyboard.current, Is.SameAs(keyboard));
            Assert.That(
                keyboard.anyKey.isPressed,
                Is.EqualTo(keys.Length > 0));
            for (int index = 0; index < keys.Length; index++)
            {
                Assert.That(
                    keyboard[keys[index]].isPressed,
                    Is.True,
                    keys[index].ToString());
                Assert.That(
                    keyboard[keys[index]].wasPressedThisFrame,
                    Is.True,
                    keys[index].ToString());
            }
        }

        private void QueueMouse(
            Vector2 position,
            MouseButton? button = null,
            bool expectPressedThisFrame = true)
        {
            var state = new MouseState { position = position };
            if (button.HasValue)
                state = state.WithButton(button.Value);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(mouse.position.ReadValue(), Is.EqualTo(position));
            if (button == MouseButton.Right)
            {
                Assert.That(mouse.rightButton.isPressed, Is.True);
                Assert.That(
                    mouse.rightButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else if (button == MouseButton.Middle)
            {
                Assert.That(mouse.middleButton.isPressed, Is.True);
                Assert.That(
                    mouse.middleButton.wasPressedThisFrame,
                    Is.EqualTo(expectPressedThisFrame));
            }
            else
            {
                Assert.That(mouse.rightButton.isPressed, Is.False);
                Assert.That(mouse.middleButton.isPressed, Is.False);
            }
        }

        private IEnumerator MoveToValidGroundPreview(
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement)
        {
            Assert.That(
                world.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY),
                Is.True);
            for (int radius = 2; radius <= 8; radius++)
            for (int x = cityX - radius; x <= cityX + radius; x++)
            for (int y = cityY - radius; y <= cityY + radius; y++)
            {
                if (!world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner))
                    continue;
                yield return MoveMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                if (placement.CurrentHit.Site == BuildingSite.Ground &&
                    placement.CurrentEvaluation.IsValid)
                    yield break;
            }

            Assert.Fail(
                "Seed 8128 must expose a valid mathematical-ground preview.");
        }

        private IEnumerator MoveToTraversal(
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            WorldTraversalKind traversal,
            TraversalTarget target)
        {
            Assert.That(target, Is.Not.Null);
            for (int x = 0; x < world.Model.Width; x++)
            for (int y = 0; y < world.Model.Height; y++)
            {
                if (world.Model.Get(x, y).Traversal != traversal)
                    continue;
                Assert.That(
                    world.Coordinates.TryCellToWorld(
                        x,
                        y,
                        0f,
                        out Vector3 corner),
                    Is.True);
                yield return MoveMouse(Camera.main.WorldToScreenPoint(
                    corner + new Vector3(.5f, 0f, .5f)));
                BuildingSurfaceHit hit = placement.CurrentHit;
                if (!hit.IsValid ||
                    hit.Site != BuildingSite.Ground ||
                    hit.X != x ||
                    hit.Y != y)
                    continue;
                target.Set(x, y);
                yield break;
            }

            Assert.Fail("Seed 8128 lacks ground projection for " +
                traversal + ".");
        }

        private static void AssertInvalidTerrain(
            GrayboxWorldView3D world,
            GrayboxBuildingPlacementController3D placement,
            WorldTraversalKind traversal,
            TraversalTarget target)
        {
            Assert.That(target, Is.Not.Null);
            Assert.That(target.IsAssigned, Is.True, traversal.ToString());
            Assert.That(
                placement.CurrentHit.IsValid,
                Is.True,
                traversal.ToString());
            Assert.That(
                placement.CurrentHit.Site,
                Is.EqualTo(BuildingSite.Ground),
                traversal.ToString());
            Assert.That(
                placement.CurrentHit.X,
                Is.EqualTo(target.X),
                traversal.ToString());
            Assert.That(
                placement.CurrentHit.Y,
                Is.EqualTo(target.Y),
                traversal.ToString());
            Assert.That(
                world.Model.Get(target.X, target.Y).Traversal,
                Is.EqualTo(traversal),
                traversal.ToString());
            Assert.That(
                placement.CurrentEvaluation.IsValid,
                Is.False,
                traversal.ToString());
            Assert.That(
                placement.CurrentEvaluation.Failures,
                Does.Contain(BuildingPlacementFailure.InvalidTerrain),
                traversal.ToString());
        }

        private static IEnumerator WaitForCityMode(
            GrayboxMobileCityController3D city,
            CityMode expected)
        {
            for (int frame = 0; frame < 60 && city.Mode != expected; frame++)
                yield return null;
            Assert.That(city.Mode, Is.EqualTo(expected));
        }

        private static void MoveCityToCell(
            GrayboxMobileCityController3D city,
            Rigidbody body,
            PlanarCoordinateMapper3D coordinates,
            int cellX,
            int cellY)
        {
            Assert.That(
                coordinates.TryCellToWorld(
                    cellX,
                    cellY,
                    city.transform.position.y,
                    out Vector3 world),
                Is.True);
            body.position = world;
            city.transform.position = world;
            Physics.SyncTransforms();
        }

        private static void FindReachableDestination(
            WorldMapModel map,
            int startX,
            int startY,
            out int targetX,
            out int targetY)
        {
            for (int radius = 1; radius <= 5; radius++)
            for (int x = Mathf.Max(0, startX - radius);
                 x <= Mathf.Min(map.Width - 1, startX + radius);
                 x++)
            for (int y = Mathf.Max(0, startY - radius);
                 y <= Mathf.Min(map.Height - 1, startY + radius);
                 y++)
            {
                if (!CityPathfinder.TryFindPath(
                        map,
                        startX,
                        startY,
                        x,
                        y,
                        out WorldGridPoint[] path) ||
                    path.Length == 0)
                    continue;
                targetX = x;
                targetY = y;
                return;
            }

            throw new AssertionException(
                "Seed 8128 must expose a nearby reachable cell.");
        }

        private static void FindDeploymentCell(
            WorldMapModel map,
            out int cellX,
            out int cellY)
        {
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                if (CityDeploymentRules.Validate(map, x, y) !=
                    CityDeploymentFailure.None)
                    continue;
                cellX = x;
                cellY = y;
                return;
            }

            throw new AssertionException(
                "Seed 8128 must expose a legal deployment cell.");
        }

        private static int RuntimeTerrainMeshCount()
        {
            return Resources.FindObjectsOfTypeAll<Mesh>()
                .Count(mesh =>
                    mesh != null &&
                    mesh.name == "first-art.terrain.surface");
        }

        private static int RuntimeRuinsCliffMeshCount()
        {
            return Resources.FindObjectsOfTypeAll<Mesh>()
                .Count(mesh =>
                    mesh != null &&
                    mesh.name == "FirstArtRuinsCliffCombinedGeometry");
        }

        private static Mesh[] GeometryMeshes(
            FirstArtTerrainRenderer3D presenter)
        {
            Transform root = presenter.transform.Find("RuntimeGeometry");
            if (root == null)
                return new Mesh[0];
            return root.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .ToArray();
        }

        private static void AssertRuinsCliffPresentation(
            FirstArtTerrainRenderer3D presenter,
            GrayboxWorldView3D world)
        {
            Assert.That(presenter.GeometryProfile, Is.Not.Null);
            Assert.That(
                presenter.RuinsStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Presented));
            Assert.That(
                presenter.CliffStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Presented));
            Assert.That(presenter.RuinsError, Is.Null);
            Assert.That(presenter.CliffError, Is.Null);
            Assert.That(
                world.IsSurfaceFallbackVisible("world.obstacle.ruins"),
                Is.False);
            Assert.That(
                world.IsSurfaceFallbackVisible("world.obstacle.cliff"),
                Is.False);

            Transform geometryRoot =
                presenter.transform.Find("RuntimeGeometry");
            Assert.That(geometryRoot, Is.Not.Null);
            Assert.That(geometryRoot.childCount, Is.EqualTo(2));
            MeshRenderer[] renderers =
                geometryRoot.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters =
                geometryRoot.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(renderers, Has.Length.EqualTo(2));
            Assert.That(filters, Has.Length.EqualTo(2));
            Assert.That(
                renderers.Select(renderer => renderer.name),
                Is.EquivalentTo(new[] { "RuinsGeometry", "CliffGeometry" }));
            Assert.That(
                filters.Select(filter => filter.sharedMesh),
                Has.All.Not.Null);
            Assert.That(
                filters.Select(filter => filter.sharedMesh.vertexCount),
                Has.All.GreaterThan(0));
            Assert.That(
                geometryRoot.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                geometryRoot.GetComponentsInChildren<MonoBehaviour>(true),
                Is.Empty);
        }

        private static int RuntimeControlTextureCount()
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>()
                .Count(texture =>
                    texture != null &&
                    (texture.name == "FirstArtTerrainControlA" ||
                     texture.name == "FirstArtTerrainControlB"));
        }

        private static IEnumerator LoadEmptyScene()
        {
            Scene graybox = SceneManager.GetSceneByName(SceneName);
            if (!graybox.IsValid() || !graybox.isLoaded)
            {
                yield return null;
                yield break;
            }

            Scene empty = SceneManager.CreateScene(
                "FirstArtTerrainRuntimeEmpty");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(graybox);
            yield return null;
        }

        private sealed class TraversalTarget
        {
            public bool IsAssigned { get; private set; }
            public int X { get; private set; } = -1;
            public int Y { get; private set; } = -1;

            public void Set(int x, int y)
            {
                X = x;
                Y = y;
                IsAssigned = true;
            }
        }
    }
}
