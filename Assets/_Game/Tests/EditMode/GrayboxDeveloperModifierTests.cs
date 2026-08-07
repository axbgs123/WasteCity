using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxDeveloperModifierTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private bool originalEnterPlayModeOptionsEnabled;
        private EnterPlayModeOptions originalEnterPlayModeOptions;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalEnterPlayModeOptionsEnabled =
                EditorSettings.enterPlayModeOptionsEnabled;
            originalEnterPlayModeOptions =
                EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload |
                EnterPlayModeOptions.DisableSceneReload;
            yield break;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
            EditorSettings.enterPlayModeOptionsEnabled =
                originalEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions =
                originalEnterPlayModeOptions;
        }

        [Test]
        public void PublicSurface_MatchesFrozenBootstrapModifierAndSpeedContract()
        {
            Type bootstrapType = typeof(GrayboxDeveloperModifierBootstrap3D);
            Assert.That(
                bootstrapType.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly)
                    .Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "IsRuntimeAvailable",
                    "IsPanelOpen"
                }));
            Assert.That(
                PublicMethodSignatures(bootstrapType),
                Is.EquivalentTo(new[]
                {
                    "System.Boolean ResolveRuntimeAvailability(System.Boolean,System.Boolean)",
                    "System.Void Configure(WasteCity.Graybox3D.Building.GrayboxBuildingSession3D,WasteCity.Graybox3D.GrayboxMobileCityController3D,WasteCity.Graybox3D.Building.GrayboxBuildingWorldView3D,UnityEngine.Canvas)",
                    "System.Boolean TryTogglePanel()"
                }));

            Type speedType = bootstrapType.Assembly.GetType(
                "WasteCity.Graybox3D.Building.DevelopmentConstructionSpeed");
            Assert.That(speedType, Is.Not.Null);
            Assert.That(speedType.IsEnum, Is.True);
            Assert.That(Enum.GetNames(speedType), Is.EqualTo(new[]
            {
                "Normal",
                "Fast10",
                "Fast100"
            }));
            Assert.That(
                Enum.GetValues(speedType)
                    .Cast<object>()
                    .Select(Convert.ToInt32),
                Is.EqualTo(new[] { 1, 10, 100 }));

            Type modifierType = typeof(GrayboxDeveloperModifier3D);
            ConstructorInfo[] constructors = modifierType.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters()
                    .Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[]
                {
                    typeof(GrayboxBuildingSession3D),
                    typeof(GrayboxMobileCityController3D),
                    typeof(GrayboxBuildingWorldView3D)
                }));
            Assert.That(
                modifierType.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly),
                Is.Empty);
            Assert.That(
                PublicMethodSignatures(modifierType),
                Is.EquivalentTo(new[]
                {
                    "System.Boolean AddResource(System.String,System.Int32)",
                    "System.Boolean SetResource(System.String,System.Int32)",
                    "System.Boolean ClearResource(System.String)",
                    "System.Boolean UnlockResearch(System.String)",
                    "System.Boolean UnlockRoute(WasteCity.Content.ContentRoute)",
                    "System.Void UnlockAllResearch()",
                    "System.Boolean SetCityMode(WasteCity.City.CityMode)",
                    "System.Boolean CompleteCityTransition()",
                    "System.Boolean SetConstructionSpeed(WasteCity.Graybox3D.Building.DevelopmentConstructionSpeed)",
                    "System.Void CompleteAllConstruction()"
                }));
        }

        [Test]
        public void RuntimeAvailability_RequiresEditorOrDevelopmentBuild()
        {
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    false,
                    false),
                Is.False);
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    true,
                    false),
                Is.True);
            Assert.That(
                GrayboxDeveloperModifierBootstrap3D.ResolveRuntimeAvailability(
                    false,
                    true),
                Is.True);
        }

        [Test]
        public void Bootstrap_EditTimeAwakeAndConfigureCreateNoRuntimeObjects()
        {
            int canvasCount = UnityEngine.Object.FindObjectsOfType<Canvas>(
                true).Length;
            int eventSystemCount =
                UnityEngine.Object.FindObjectsOfType<EventSystem>(true).Length;
            int inputModuleCount = UnityEngine.Object.FindObjectsOfType<
                InputSystemUIInputModule>(true).Length;
            ModifierFixture fixture = CreateFixture();
            GrayboxBuildingWorldView3D presentation = fixture.Presentation;
            Canvas canvas = Track(new GameObject(
                "Shared Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster))).GetComponent<Canvas>();
            var bootstrapObject = Track(new GameObject("Modifier Bootstrap"));
            GrayboxDeveloperModifierBootstrap3D bootstrap =
                bootstrapObject.AddComponent<
                    GrayboxDeveloperModifierBootstrap3D>();

            Assert.That(Application.isPlaying, Is.False);
            typeof(GrayboxDeveloperModifierBootstrap3D).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bootstrap, null);
            bootstrap.Configure(
                fixture.Session,
                fixture.City,
                presentation,
                canvas);

            Assert.That(
                UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length,
                Is.EqualTo(canvasCount + 1));
            Assert.That(
                UnityEngine.Object.FindObjectsOfType<EventSystem>(true).Length,
                Is.EqualTo(eventSystemCount));
            Assert.That(
                UnityEngine.Object.FindObjectsOfType<
                    InputSystemUIInputModule>(true).Length,
                Is.EqualTo(inputModuleCount));
            Assert.That(
                canvas.transform.Find("Graybox Developer Modifier"),
                Is.Null);
            Assert.That(PrivateField(bootstrap, "panelRoot"), Is.Null);
            Assert.That(PrivateField(bootstrap, "modifier"), Is.Null);
            Assert.That(bootstrap.IsRuntimeAvailable, Is.False);
            Assert.That(bootstrap.IsPanelOpen, Is.False);
            Assert.That(bootstrap.TryTogglePanel(), Is.False);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    bootstrapObject),
                Is.Zero);
        }

        [Test]
        public void Bootstrap_IncompleteDependenciesRemainInert()
        {
            var bootstrapObject = Track(new GameObject("Modifier Bootstrap"));
            GrayboxDeveloperModifierBootstrap3D bootstrap =
                bootstrapObject.AddComponent<
                    GrayboxDeveloperModifierBootstrap3D>();
            int objectCount = UnityEngine.Object.FindObjectsOfType<
                Transform>(true).Length;

            bootstrap.Configure(null, null, null, null);

            Assert.That(bootstrap.IsRuntimeAvailable, Is.False);
            Assert.That(bootstrap.IsPanelOpen, Is.False);
            Assert.That(bootstrap.TryTogglePanel(), Is.False);
            Assert.That(
                UnityEngine.Object.FindObjectsOfType<Transform>(true).Length,
                Is.EqualTo(objectCount));
            Assert.That(PrivateField(bootstrap, "panelRoot"), Is.Null);
            Assert.That(PrivateField(bootstrap, "modifier"), Is.Null);
            Assert.That(
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    bootstrapObject),
                Is.Zero);
        }

        [Test]
        public void Bootstrap_OnlySerializesAlwaysCompiledComponentFields()
        {
            FieldInfo[] fields = typeof(GrayboxDeveloperModifierBootstrap3D)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                if (!field.IsDefined(typeof(SerializeField), true))
                    continue;
                Assert.That(field.FieldType, Is.Not.EqualTo(
                    typeof(GrayboxDeveloperModifier3D)));
            }
        }

        [Test]
        public void Bootstrap_DoesNotPollInputAndOnlyExposesTryTogglePanel()
        {
            string source = ReadSource(
                "GrayboxDeveloperModifierBootstrap3D.cs");

            Assert.That(source, Does.Not.Contain("Keyboard.current"));
            Assert.That(source, Does.Not.Contain("f10Key"));
            Assert.That(source, Does.Not.Match(@"void Update\s*\("));
            Assert.That(source, Does.Contain("TryTogglePanel"));
            Assert.That(
                typeof(GrayboxDeveloperModifierBootstrap3D).GetMethod(
                    "TryTogglePanel",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);
        }

        [Test]
        public void Bootstrap_LabelsTheConditionalPanelAsDevelopmentMode()
        {
            string source = ReadSource(
                "GrayboxDeveloperModifierBootstrap3D.cs");

            Assert.That(source, Does.Contain("开发模式"));
        }

        [UnityTest]
        public IEnumerator
            Bootstrap_RuntimeLifecycleUsesSharedCanvasAndCleansOldBindings()
        {
            yield return new EnterPlayMode();
            var owned = new List<UnityEngine.Object>();
            try
            {
                int canvasCountBefore =
                    UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length;
                int eventSystemCountBefore = UnityEngine.Object.FindObjectsOfType<
                    EventSystem>(true).Length;
                int inputModuleCountBefore =
                    UnityEngine.Object.FindObjectsOfType<
                        InputSystemUIInputModule>(true).Length;
                int panelCountBefore = PanelCount();
                RuntimeBootstrapFixture fixture =
                    CreateRuntimeBootstrapFixture(owned);

                fixture.Bootstrap.Configure(
                    fixture.Session,
                    null,
                    fixture.Presentation,
                    fixture.Canvas);
                Assert.That(fixture.Bootstrap.IsRuntimeAvailable, Is.False);
                Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.False);
                Assert.That(FindPanel(fixture.Canvas), Is.Null);

                fixture.Bootstrap.Configure(
                    fixture.Session,
                    fixture.City,
                    fixture.Presentation,
                    fixture.Canvas);
                GameObject panel = FindPanel(fixture.Canvas);
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.transform.parent, Is.EqualTo(
                    fixture.Canvas.transform));
                Assert.That(panel.GetComponentInParent<Canvas>(),
                    Is.SameAs(fixture.Canvas));
                Assert.That(panel.GetComponent<Canvas>(), Is.Null);
                Assert.That(fixture.Bootstrap.IsRuntimeAvailable, Is.True);
                Assert.That(fixture.Bootstrap.IsPanelOpen, Is.False);
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<Canvas>(true).Length,
                    Is.EqualTo(canvasCountBefore + 1));
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<EventSystem>(true).Length,
                    Is.EqualTo(eventSystemCountBefore + 1));
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<
                        InputSystemUIInputModule>(true).Length,
                    Is.EqualTo(inputModuleCountBefore + 1));

                Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.True);
                Assert.That(fixture.Bootstrap.IsPanelOpen, Is.True);
                Assert.That(new[]
                {
                    "Resource +100",
                    "Resource +1000",
                    "Clear Resource",
                    "Set Resource",
                    "Unlock Research",
                    "Unlock Technology",
                    "Unlock Cultivation",
                    "Unlock Biological Ascension",
                    "Unlock Psionics",
                    "Unlock All",
                    "Set Mobile",
                    "Set Fortress",
                    "Complete Transition",
                    "Multiplier 1x",
                    "Multiplier 10x",
                    "Multiplier 100x",
                    "Complete Construction"
                }, Has.All.Matches<string>(name =>
                    ButtonNamed(panel, name) != null));
                ExecuteEvents.Execute(
                    ButtonNamed(panel, "Resource +100").gameObject,
                    new PointerEventData(fixture.EventSystem)
                    {
                        button = PointerEventData.InputButton.Left
                    },
                    ExecuteEvents.pointerClickHandler);
                Assert.That(
                    fixture.Session.Inventory.Get(ResourceIds.Iron),
                    Is.EqualTo(130));

                fixture.Bootstrap.Configure(
                    fixture.Session,
                    fixture.City,
                    fixture.Presentation,
                    fixture.Canvas);
                Assert.That(FindPanel(fixture.Canvas), Is.SameAs(panel));
                Assert.That(
                    PanelCount(),
                    Is.EqualTo(panelCountBefore + 1));

                for (var dependencyIndex = 0;
                     dependencyIndex < 4;
                     dependencyIndex++)
                {
                    GrayboxBuildingSession3D oldSession = fixture.Session;
                    Button.ButtonClickedEvent oldClick =
                        ButtonNamed(panel, "Resource +100").onClick;
                    GrayboxBuildingSession3D nextSession = fixture.Session;
                    GrayboxMobileCityController3D nextCity = fixture.City;
                    GrayboxBuildingWorldView3D nextPresentation =
                        fixture.Presentation;
                    Canvas nextCanvas = fixture.Canvas;
                    if (dependencyIndex == 0)
                        nextSession = CreateRuntimeSession(
                            owned,
                            "Replacement Session");
                    else if (dependencyIndex == 1)
                        nextCity = RuntimeTrack(
                            owned,
                            new GameObject("Replacement City")).AddComponent<
                                GrayboxMobileCityController3D>();
                    else if (dependencyIndex == 2)
                        nextPresentation = RuntimeTrack(
                            owned,
                            new GameObject("Replacement Presentation"))
                            .AddComponent<GrayboxBuildingWorldView3D>();
                    else
                        nextCanvas = CreateRuntimeCanvas(
                            owned,
                            "Replacement Shared Canvas");

                    nextSession.Inventory.Set(ResourceIds.Iron, 0);
                    oldSession.Inventory.Set(ResourceIds.Iron, 0);
                    fixture.Bootstrap.Configure(
                        nextSession,
                        nextCity,
                        nextPresentation,
                        nextCanvas);

                    Assert.That(panel.activeSelf, Is.False);
                    oldClick.Invoke();
                    Assert.That(
                        oldSession.Inventory.Get(ResourceIds.Iron),
                        Is.Zero);
                    Assert.That(
                        nextSession.Inventory.Get(ResourceIds.Iron),
                        Is.Zero);
                    yield return null;
                    Assert.That(panel == null, Is.True);
                    panel = FindPanel(nextCanvas);
                    Assert.That(panel, Is.Not.Null);
                    Assert.That(panel.transform.parent, Is.EqualTo(
                        nextCanvas.transform));
                    Assert.That(
                        PanelCount(),
                        Is.EqualTo(panelCountBefore + 1));
                    ButtonNamed(panel, "Resource +100").onClick.Invoke();
                    Assert.That(
                        nextSession.Inventory.Get(ResourceIds.Iron),
                        Is.EqualTo(100));

                    fixture.Session = nextSession;
                    fixture.City = nextCity;
                    fixture.Presentation = nextPresentation;
                    fixture.Canvas = nextCanvas;
                }

                Button.ButtonClickedEvent disabledClick =
                    ButtonNamed(panel, "Resource +100").onClick;
                fixture.Session.Inventory.Set(ResourceIds.Iron, 0);
                fixture.Bootstrap.enabled = false;
                Assert.That(fixture.Bootstrap.IsRuntimeAvailable, Is.False);
                Assert.That(fixture.Bootstrap.IsPanelOpen, Is.False);
                Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.False);
                disabledClick.Invoke();
                Assert.That(
                    fixture.Session.Inventory.Get(ResourceIds.Iron),
                    Is.Zero);
                yield return null;
                Assert.That(panel == null, Is.True);

                fixture.Bootstrap.enabled = true;
                panel = FindPanel(fixture.Canvas);
                Assert.That(panel, Is.Not.Null);
                Assert.That(fixture.Bootstrap.IsRuntimeAvailable, Is.True);

                Button.ButtonClickedEvent destroyedClick =
                    ButtonNamed(panel, "Resource +100").onClick;
                fixture.Session.Inventory.Set(ResourceIds.Iron, 0);
                UnityEngine.Object.DestroyImmediate(
                    fixture.Bootstrap.gameObject);
                destroyedClick.Invoke();
                Assert.That(
                    fixture.Session.Inventory.Get(ResourceIds.Iron),
                    Is.Zero);
                yield return null;
                Assert.That(PanelCount(), Is.EqualTo(panelCountBefore));
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<EventSystem>(true).Length,
                    Is.EqualTo(eventSystemCountBefore + 1));
                Assert.That(
                    UnityEngine.Object.FindObjectsOfType<
                        InputSystemUIInputModule>(true).Length,
                    Is.EqualTo(inputModuleCountBefore + 1));
            }
            finally
            {
                DestroyRuntimeObjects(owned);
            }
        }

        [Test]
        public void Modifier_UsesOnlyApprovedModelAndDevelopmentAdapterPaths()
        {
            string source = ReadSource("GrayboxDeveloperModifier3D.cs");

            Assert.That(source, Does.Contain("session.Inventory.Add"));
            Assert.That(source, Does.Contain("session.Inventory.Set"));
            Assert.That(source, Does.Contain(
                "session.UnlockResearchForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.UnlockRouteForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.UnlockAllResearchForDevelopment"));
            Assert.That(source, Does.Contain(
                "city.RestoreDeploymentForDevelopment"));
            Assert.That(source, Does.Contain(
                "city.CompleteDeploymentTransitionForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.SetConstructionMultiplierForDevelopment"));
            Assert.That(source, Does.Contain(
                "session.CompleteAllConstructionForDevelopment"));
            Assert.That(source, Does.Not.Match(@"\btransform\s*\."));
            Assert.That(source, Does.Not.Match(
                @"\b(GroundGrid|InnerGrid)\s*\."));
            Assert.That(source, Does.Not.Match(
                @"BindingFlags|GetField|SetValue|FieldInfo"));
            Assert.That(source, Does.Not.Match(
                @"TryBeginConstruction|TryPlace|TryRestore"));
            Assert.That(source, Does.Not.Match(@"\bdeployment\s*\."));
        }

        [Test]
        public void Commands_ApplyResourceOperationsWithinCapacity()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Session.Inventory.Set(ResourceIds.Iron, 4900);

            Assert.That(
                fixture.Modifier.AddResource(ResourceIds.Iron, 100),
                Is.True);
            Assert.That(
                fixture.Modifier.AddResource(ResourceIds.Iron, 1000),
                Is.True);
            Assert.That(
                fixture.Modifier.ClearResource(ResourceIds.Iron),
                Is.True);
            Assert.That(
                fixture.Modifier.SetResource(ResourceIds.Iron, 1234),
                Is.True);
            Assert.That(
                fixture.Modifier.SetResource(ResourceIds.Iron, -1),
                Is.False);
            Assert.That(
                fixture.Modifier.AddResource("unknown.resource", 100),
                Is.False);
            Assert.That(
                fixture.Modifier.ClearResource("unknown.resource"),
                Is.False);

            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(1234));
            fixture.Modifier.AddResource(ResourceIds.Iron, 1000);
            fixture.Modifier.AddResource(ResourceIds.Iron, 1000);
            fixture.Modifier.AddResource(ResourceIds.Iron, 1000);
            fixture.Modifier.AddResource(ResourceIds.Iron, 1000);
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(5000));
        }

        [Test]
        public void Commands_UnlockWithoutRelocking()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(fixture.Modifier.UnlockResearch(
                "core.research.automated-machinery"), Is.True);
            Assert.That(
                fixture.Modifier.UnlockResearch("unknown.research"),
                Is.False);
            Assert.That(
                fixture.Modifier.UnlockRoute(ContentRoute.Technology),
                Is.True);
            Assert.That(
                fixture.Modifier.UnlockRoute(ContentRoute.Core),
                Is.False);
            fixture.Modifier.UnlockAllResearch();

            Assert.That(fixture.Session.IsResearchCompleted(
                "core.research.automated-machinery"), Is.True);
            Assert.That(fixture.Session.HasContactedRoute(
                ContentRoute.Technology), Is.True);
            Assert.That(fixture.Session.Research.CompletedCount,
                Is.EqualTo(ResearchCatalog.All.Length));
        }

        [Test]
        public void Commands_UseSafeCityDevelopmentAdapter()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(fixture.Modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Modifier.SetCityMode(CityMode.Mobile), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.Modifier.SetCityMode(CityMode.Deploying), Is.False);
            fixture.City.Deployment.Restore(CityMode.Packing, 1f);
            Assert.That(fixture.Modifier.CompleteCityTransition(), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
        }

        [Test]
        public void Modifier_ConstructorRejectsEveryMissingDependency()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.Throws<ArgumentNullException>(() =>
                new GrayboxDeveloperModifier3D(
                    null,
                    fixture.City,
                    fixture.Presentation));
            Assert.Throws<ArgumentNullException>(() =>
                new GrayboxDeveloperModifier3D(
                    fixture.Session,
                    null,
                    fixture.Presentation));
            Assert.Throws<ArgumentNullException>(() =>
                new GrayboxDeveloperModifier3D(
                    fixture.Session,
                    fixture.City,
                    null));
        }

        [TestCase(DevelopmentConstructionSpeed.Normal, 1f)]
        [TestCase(DevelopmentConstructionSpeed.Fast10, 10f)]
        [TestCase(DevelopmentConstructionSpeed.Fast100, 100f)]
        public void Commands_SetApprovedConstructionMultipliers(
            DevelopmentConstructionSpeed speed,
            float multiplier)
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(
                fixture.Modifier.SetConstructionSpeed(speed),
                Is.True);

            Assert.That(fixture.Session.ConstructionMultiplier,
                Is.EqualTo(multiplier));
        }

        [Test]
        public void Commands_RejectUnknownConstructionSpeed()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(
                fixture.Modifier.SetConstructionSpeed(
                    (DevelopmentConstructionSpeed)999),
                Is.False);
            Assert.That(
                fixture.Session.ConstructionMultiplier,
                Is.EqualTo(1f));
        }

        [Test]
        public void ImmediateCompletion_CompletesEveryExistingSiteAndPreservesMultiplier()
        {
            ModifierFixture fixture = CreateFixture();
            Begin(fixture.Session, 10, 10, fixture.Presentation);
            Begin(fixture.Session, 12, 10, fixture.Presentation);
            fixture.Modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast10);

            fixture.Modifier.CompleteAllConstruction();

            Assert.That(fixture.Session.Instances, Has.All.Matches<
                GrayboxBuildingInstance3D>(instance =>
                    instance.State == GrayboxBuildingInstanceState.Completed));
            Assert.That(fixture.Session.ConstructionMultiplier, Is.EqualTo(10f));
            GrayboxBuildingInstance3D next = Begin(
                fixture.Session, 14, 10, fixture.Presentation);
            fixture.Session.TickConstruction(
                .1f,
                CityMode.Fortress,
                false,
                fixture.Presentation);
            Assert.That(next.Progress.Remaining, Is.EqualTo(1f));
            Assert.That(next.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
        }

        [Test]
        public void DevelopmentChanges_AreDiscardedBySessionRecreation()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Modifier.SetResource(ResourceIds.Iron, 5000);
            fixture.Modifier.UnlockAllResearch();
            fixture.Modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast100);

            ModifierFixture freshFixture = CreateFixture();

            Assert.That(freshFixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(30));
            Assert.That(freshFixture.Session.Research.CompletedCount, Is.Zero);
            Assert.That(freshFixture.Session.ConstructionMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void CityDevelopmentAdapter_AcceptsOnlyStableModesAndTransitions()
        {
            ModifierFixture fixture = CreateFixture();

            Assert.That(fixture.City.RestoreDeploymentForDevelopment(
                CityMode.Deploying), Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Mobile));
            Assert.That(fixture.City.RestoreDeploymentForDevelopment(
                CityMode.Fortress), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.City.CompleteDeploymentTransitionForDevelopment(),
                Is.False);
            fixture.City.Deployment.Restore(CityMode.Deploying, 1f);
            Assert.That(fixture.City.CompleteDeploymentTransitionForDevelopment(),
                Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
        }

        private ModifierFixture CreateFixture()
        {
            var sessionObject = Track(new GameObject("Session"));
            GrayboxBuildingSession3D session = sessionObject.AddComponent<
                GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            var cityObject = Track(new GameObject("City"));
            GrayboxMobileCityController3D city = cityObject.AddComponent<
                GrayboxMobileCityController3D>();
            Transform instanceRoot =
                Track(new GameObject("Instance Root")).transform;
            Transform infrastructureRoot =
                Track(new GameObject("Infrastructure Root")).transform;
            Shader shader = Shader.Find("Hidden/InternalErrorShader") ??
                Shader.Find("Unlit/Color");
            Material material = Track(new Material(shader));
            GrayboxBuildingWorldView3D presentation =
                Track(new GameObject("Presentation")).AddComponent<
                    GrayboxBuildingWorldView3D>();
            presentation.Configure(
                instanceRoot,
                infrastructureRoot,
                material,
                city);
            return new ModifierFixture(
                session,
                city,
                new GrayboxDeveloperModifier3D(
                    session,
                    city,
                    presentation),
                presentation);
        }

        private static Button ButtonNamed(GameObject root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name)
                    return button;
            return null;
        }

        private static InputField InputNamed(GameObject root, string name)
        {
            foreach (InputField input in
                root.GetComponentsInChildren<InputField>(true))
                if (input.name == name)
                    return input;
            return null;
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            int x,
            int y,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingUnlockEvaluation unlock =
                BuildingUnlockModel.Evaluate(
                    BuildingCatalog.Wall,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                BuildingCatalog.Wall,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                x,
                y,
                session.GroundBuildRadius,
                CityMode.Fortress,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                unlock,
                session.Inventory.CanSpend(
                    BuildingCatalog.Wall.CostId,
                    BuildingCatalog.Wall.Cost));
            Assert.That(session.TryBeginConstruction(
                request, presentation, out GrayboxBuildingInstance3D instance,
                out _), Is.True);
            return instance;
        }

        private static string ReadSource(string name)
        {
            return File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Game/Scripts/Graybox3D/Building",
                name));
        }

        private static IEnumerable<string> PublicMethodSignatures(Type type)
        {
            return type.GetMethods(
                    BindingFlags.Static |
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method =>
                    method.ReturnType.FullName + " " +
                    method.Name + "(" +
                    string.Join(
                        ",",
                        method.GetParameters().Select(parameter =>
                            parameter.ParameterType.FullName)) +
                    ")");
        }

        private static object PrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
            return field.GetValue(target);
        }

        private static GameObject FindPanel(Canvas canvas)
        {
            Transform panel = canvas == null
                ? null
                : canvas.transform.Find("Graybox Developer Modifier");
            return panel == null ? null : panel.gameObject;
        }

        private static int PanelCount()
        {
            return UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .Count(transform =>
                    transform.name == "Graybox Developer Modifier");
        }

        private static RuntimeBootstrapFixture CreateRuntimeBootstrapFixture(
            List<UnityEngine.Object> owned)
        {
            GrayboxBuildingSession3D session =
                CreateRuntimeSession(owned, "Session");
            GrayboxMobileCityController3D city = RuntimeTrack(
                owned,
                new GameObject("City")).AddComponent<
                    GrayboxMobileCityController3D>();
            GrayboxBuildingWorldView3D presentation = RuntimeTrack(
                owned,
                new GameObject("Presentation")).AddComponent<
                    GrayboxBuildingWorldView3D>();
            Canvas canvas = CreateRuntimeCanvas(owned, "Shared Canvas");
            GameObject eventSystemObject = RuntimeTrack(
                owned,
                new GameObject(
                    "Shared EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule)));
            EventSystem eventSystem =
                eventSystemObject.GetComponent<EventSystem>();
            GrayboxDeveloperModifierBootstrap3D bootstrap = RuntimeTrack(
                owned,
                new GameObject("Modifier Bootstrap")).AddComponent<
                    GrayboxDeveloperModifierBootstrap3D>();
            return new RuntimeBootstrapFixture(
                bootstrap,
                session,
                city,
                presentation,
                canvas,
                eventSystem);
        }

        private static GrayboxBuildingSession3D CreateRuntimeSession(
            List<UnityEngine.Object> owned,
            string name)
        {
            GrayboxBuildingSession3D session = RuntimeTrack(
                owned,
                new GameObject(name)).AddComponent<
                    GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static Canvas CreateRuntimeCanvas(
            List<UnityEngine.Object> owned,
            string name)
        {
            Canvas canvas = RuntimeTrack(
                owned,
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster))).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        private static GameObject RuntimeTrack(
            List<UnityEngine.Object> owned,
            GameObject value)
        {
            owned.Add(value);
            return value;
        }

        private static void DestroyRuntimeObjects(
            List<UnityEngine.Object> owned)
        {
            for (var index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null)
                    UnityEngine.Object.DestroyImmediate(owned[index]);
            owned.Clear();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class ModifierFixture
        {
            public ModifierFixture(
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxDeveloperModifier3D modifier,
                GrayboxBuildingWorldView3D presentation)
            {
                Session = session;
                City = city;
                Modifier = modifier;
                Presentation = presentation;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxDeveloperModifier3D Modifier { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
        }

        private sealed class RuntimeBootstrapFixture
        {
            public RuntimeBootstrapFixture(
                GrayboxDeveloperModifierBootstrap3D bootstrap,
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                Canvas canvas,
                EventSystem eventSystem)
            {
                Bootstrap = bootstrap;
                Session = session;
                City = city;
                Presentation = presentation;
                Canvas = canvas;
                EventSystem = eventSystem;
            }

            public GrayboxDeveloperModifierBootstrap3D Bootstrap { get; }
            public GrayboxBuildingSession3D Session { get; set; }
            public GrayboxMobileCityController3D City { get; set; }
            public GrayboxBuildingWorldView3D Presentation { get; set; }
            public Canvas Canvas { get; set; }
            public EventSystem EventSystem { get; }
        }

    }
}
