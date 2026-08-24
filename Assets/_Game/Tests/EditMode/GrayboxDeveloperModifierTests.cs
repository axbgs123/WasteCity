using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
                bootstrapType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly),
                Is.Empty);
            Assert.That(
                bootstrapType.GetEvents(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly),
                Is.Empty);
            PropertyInfo[] bootstrapProperties = bootstrapType.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly);
            Assert.That(
                bootstrapProperties.Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "IsRuntimeAvailable",
                    "IsPanelOpen",
                    "HasModifiedGameState"
                }));
            foreach (PropertyInfo property in bootstrapProperties)
            {
                Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
                Assert.That(property.CanRead, Is.True);
                Assert.That(property.GetMethod, Is.Not.Null);
                Assert.That(property.GetMethod.IsPublic, Is.True);
                Assert.That(property.GetMethod.IsStatic, Is.False);
                Assert.That(property.CanWrite, Is.False);
                Assert.That(property.SetMethod, Is.Null);
            }
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
            Assert.That(
                modifierType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly),
                Is.Empty);
            Assert.That(
                modifierType.GetEvents(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.DeclaredOnly),
                Is.Empty);
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
            PropertyInfo[] modifierProperties = modifierType.GetProperties(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly);
            Assert.That(
                modifierProperties.Select(property => property.Name),
                Is.EquivalentTo(new[] { "HasModifiedGameState" }));
            Assert.That(modifierProperties[0].PropertyType,
                Is.EqualTo(typeof(bool)));
            Assert.That(modifierProperties[0].CanRead, Is.True);
            Assert.That(modifierProperties[0].CanWrite, Is.False);
            Assert.That(
                PublicMethodSignatures(modifierType),
                Is.EquivalentTo(new[]
                {
                    "System.Boolean AddResource(System.String,System.Int32)",
                    "WasteCity.Graybox3D.Building.GrayboxDeveloperCommandResult3D AddResourceWithFeedback(System.String,System.Int32)",
                    "System.Boolean SetResource(System.String,System.Int32)",
                    "WasteCity.Graybox3D.Building.GrayboxDeveloperCommandResult3D SetResourceWithFeedback(System.String,System.Int32)",
                    "System.Boolean ClearResource(System.String)",
                    "System.Boolean UnlockResearch(System.String)",
                    "WasteCity.Graybox3D.Building.GrayboxDeveloperCommandResult3D UnlockResearchWithFeedback(System.String)",
                    "System.Boolean UnlockRoute(WasteCity.Content.ContentRoute)",
                    "WasteCity.Graybox3D.Building.GrayboxDeveloperCommandResult3D UnlockRouteWithFeedback(WasteCity.Content.ContentRoute)",
                    "System.Void UnlockAllResearch()",
                    "WasteCity.Graybox3D.Building.GrayboxDeveloperCommandResult3D UnlockAllResearchWithFeedback()",
                    "System.Boolean SetCityMode(WasteCity.City.CityMode)",
                    "System.Boolean CompleteCityTransition()",
                    "System.Boolean SetPopulation(System.Int32)",
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
        public void Bootstrap_ReleaseProjectionIsInertAndContainsNoRuntimeSurface()
        {
            string releaseSource = ProjectReleaseSource(ReadSource(
                "GrayboxDeveloperModifierBootstrap3D.cs"));

            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "public bool IsRuntimeAvailable")),
                Is.EqualTo("{ get { return false; } }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "public bool IsPanelOpen")),
                Is.EqualTo("{ get { return false; } }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "public bool HasModifiedGameState")),
                Is.EqualTo("{ get { return false; } }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "public bool TryTogglePanel()")),
                Is.EqualTo("{ return false; }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "private void Awake()")),
                Is.EqualTo("{ }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "private void OnEnable()")),
                Is.EqualTo("{ }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "private void OnDisable()")),
                Is.EqualTo("{ }"));
            Assert.That(
                NormalizeWhitespace(ExtractMemberBody(
                    releaseSource,
                    "private void OnDestroy()")),
                Is.EqualTo("{ }"));
            Assert.That(
                releaseSource,
                Does.Not.Contain("GrayboxDeveloperModifier3D"));
            Assert.That(
                releaseSource,
                Does.Not.Contain("TryCreateDevelopmentSurface"));
            Assert.That(
                releaseSource,
                Does.Not.Contain("CreatePanel"));
            Assert.That(
                releaseSource,
                Does.Not.Contain("DisposeDevelopmentSurface"));
            Assert.That(
                releaseSource,
                Does.Not.Contain("Graybox Developer Modifier"));
            Assert.That(releaseSource, Does.Not.Contain("new GameObject"));
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
                InputField resourceSearch = panel.transform
                    .Find("Resource Search")
                    .GetComponent<InputField>();
                InputField researchSearch = panel.transform
                    .Find("Research Search")
                    .GetComponent<InputField>();
                Assert.That(resourceSearch, Is.Not.Null);
                Assert.That(researchSearch, Is.Not.Null);
                Assert.That(panel.transform.Find("Resource Results")
                    .GetComponent<ScrollRect>(), Is.Not.Null);
                Assert.That(panel.transform.Find("Research Results")
                    .GetComponent<ScrollRect>(), Is.Not.Null);
                Assert.That(panel.transform.Find("Developer Feedback")
                    .GetComponentInChildren<Text>(true).text,
                    Is.EqualTo("请选择物品或科技"));
                Button[] catalogButtons =
                    panel.GetComponentsInChildren<Button>(true);
                Assert.That(catalogButtons.Count(button =>
                    button.name.StartsWith(
                        "Developer.Resource.",
                        StringComparison.Ordinal)), Is.EqualTo(31));
                Assert.That(catalogButtons.Count(button =>
                    button.name.StartsWith(
                        "Developer.Research.",
                        StringComparison.Ordinal)), Is.EqualTo(43));
                Assert.That(ButtonNamed(
                        panel,
                        "Developer.Resource." + ResourceIds.HybridCore)
                    .GetComponentInChildren<Text>(true).text,
                    Is.EqualTo("融合核心"));
                Assert.That(ButtonNamed(
                        panel,
                        "Developer.Research.core.research.spirit-sensing")
                    .GetComponentInChildren<Text>(true).text,
                    Is.EqualTo("灵火淬炼"));
                resourceSearch.text = "融合";
                Assert.That(catalogButtons.Count(button =>
                    button.name.StartsWith(
                        "Developer.Resource.",
                        StringComparison.Ordinal) &&
                    button.gameObject.activeSelf), Is.EqualTo(1));
                resourceSearch.text = string.Empty;
                ButtonNamed(
                    panel,
                    "Developer.Resource." + ResourceIds.HybridCore)
                    .onClick.Invoke();
                ButtonNamed(panel, "Resource +100").onClick.Invoke();
                Assert.That(fixture.Session.Inventory.Get(
                    ResourceIds.HybridCore), Is.EqualTo(100));
                Assert.That(panel.transform.Find("Developer Feedback")
                    .GetComponentInChildren<Text>(true).text,
                    Does.Contain("融合核心 已增加 100"));
                ButtonNamed(
                    panel,
                    "Developer.Resource." + ResourceIds.Iron)
                    .onClick.Invoke();
                researchSearch.text = "灵火";
                Assert.That(catalogButtons.Count(button =>
                    button.name.StartsWith(
                        "Developer.Research.",
                        StringComparison.Ordinal) &&
                    button.gameObject.activeSelf), Is.EqualTo(1));
                ButtonNamed(
                    panel,
                    "Developer.Research.core.research.spirit-sensing")
                    .onClick.Invoke();
                ButtonNamed(panel, "Unlock Research").onClick.Invoke();
                Assert.That(fixture.Session.IsResearchCompleted(
                    "core.research.spirit-sensing"), Is.True);
                researchSearch.text = string.Empty;
                Assert.That(new[]
                {
                    "Resource +100",
                    "Resource +1000",
                    "Clear Resource",
                    "Set Resource",
                    "Set Population",
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
                InputField population = panel.transform
                    .Find("Population Amount")
                    .GetComponent<InputField>();
                Assert.That(population, Is.Not.Null);
                population.text = "2000";
                ExecuteEvents.Execute(
                    ButtonNamed(panel, "Set Population").gameObject,
                    new PointerEventData(fixture.EventSystem)
                    {
                        button = PointerEventData.InputButton.Left
                    },
                    ExecuteEvents.pointerClickHandler);
                Assert.That(fixture.Session.Population, Is.EqualTo(2000));

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
                    {
                        nextPresentation = RuntimeTrack(
                            owned,
                            new GameObject("Replacement Presentation"))
                            .AddComponent<GrayboxBuildingWorldView3D>();
                        ConfigureRuntimePresentation(
                            owned,
                            nextPresentation,
                            nextCity,
                            "Replacement");
                    }
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
                    if (dependencyIndex == 1)
                    {
                        Assert.That(oldSession, Is.SameAs(nextSession));
                        Assert.That(fixture.City.Mode, Is.EqualTo(
                            CityMode.Mobile));
                        Assert.That(nextCity.Mode, Is.EqualTo(
                            CityMode.Mobile));
                        ButtonNamed(panel, "Set Fortress").onClick.Invoke();
                        Assert.That(fixture.City.Mode, Is.EqualTo(
                            CityMode.Mobile));
                        Assert.That(nextCity.Mode, Is.EqualTo(
                            CityMode.Fortress));
                    }
                    else if (dependencyIndex == 2)
                    {
                        GrayboxBuildingInstance3D construction = Begin(
                            nextSession,
                            20,
                            10,
                            new RecordingPresentation());
                        Assert.Throws<InvalidOperationException>(() =>
                            fixture.Presentation.UpdateInstance(construction));
                        Assert.DoesNotThrow(() =>
                            nextPresentation.UpdateInstance(construction));
                        Assert.DoesNotThrow(() =>
                            ButtonNamed(panel, "Complete Construction")
                                .onClick.Invoke());
                        Assert.That(
                            construction.State,
                            Is.EqualTo(
                                GrayboxBuildingInstanceState.Completed));
                    }

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
        public void UsageFlag_IgnoresRejectedAndSuccessfulNoChangeCommands()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Session.Inventory.Set(ResourceIds.Iron, 5000);
            fixture.Session.Inventory.Set(ResourceIds.Alloy, 0);
            fixture.Session.UnlockAllResearchForDevelopment();

            Assert.That(fixture.Modifier.HasModifiedGameState, Is.False);
            Assert.That(fixture.Modifier.AddResource("unknown.resource", 10),
                Is.False);
            Assert.That(fixture.Modifier.AddResource(ResourceIds.Iron, 10),
                Is.True);
            Assert.That(fixture.Modifier.SetResource(ResourceIds.Iron, 5000),
                Is.True);
            Assert.That(fixture.Modifier.ClearResource(ResourceIds.Alloy),
                Is.True);
            Assert.That(fixture.Modifier.UnlockResearch(
                "core.research.automated-machinery"), Is.True);
            Assert.That(fixture.Modifier.UnlockRoute(ContentRoute.Technology),
                Is.True);
            fixture.Modifier.UnlockAllResearch();
            Assert.That(fixture.Modifier.SetCityMode(CityMode.Mobile), Is.True);
            Assert.That(fixture.Modifier.CompleteCityTransition(), Is.False);
            Assert.That(fixture.Modifier.SetPopulation(
                fixture.Session.Population), Is.True);
            Assert.That(fixture.Modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Normal), Is.True);
            fixture.Modifier.CompleteAllConstruction();

            Assert.That(fixture.Modifier.HasModifiedGameState, Is.False);
        }

        [Test]
        public void UsageFlag_IsMonotonicAcrossEveryMutationFamily()
        {
            ModifierFixture resource = CreateFixture();
            Assert.That(resource.Modifier.AddResourceWithFeedback(
                "铁矿", 1).AppliedAmount, Is.EqualTo(1));
            Assert.That(resource.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture research = CreateFixture();
            Assert.That(research.Modifier.UnlockResearchWithFeedback(
                "基础冶金").AffectedCount, Is.EqualTo(1));
            Assert.That(research.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture route = CreateFixture();
            Assert.That(route.Modifier.UnlockRouteWithFeedback(
                ContentRoute.Technology).AffectedCount, Is.GreaterThan(0));
            Assert.That(route.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture allResearch = CreateFixture();
            Assert.That(allResearch.Modifier.UnlockAllResearchWithFeedback()
                .AffectedCount, Is.GreaterThan(0));
            Assert.That(allResearch.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture city = CreateFixture();
            Assert.That(city.Modifier.SetCityMode(CityMode.Fortress), Is.True);
            Assert.That(city.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture population = CreateFixture();
            Assert.That(population.Modifier.SetPopulation(201), Is.True);
            Assert.That(population.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture speed = CreateFixture();
            Assert.That(speed.Modifier.SetConstructionSpeed(
                DevelopmentConstructionSpeed.Fast10), Is.True);
            Assert.That(speed.Modifier.HasModifiedGameState, Is.True);

            ModifierFixture construction = CreateFixture();
            Begin(construction.Session, 10, 10, construction.Presentation);
            construction.Modifier.CompleteAllConstruction();
            Assert.That(construction.Modifier.HasModifiedGameState, Is.True);

            Assert.That(resource.Modifier.AddResource("unknown.resource", 1),
                Is.False);
            Assert.That(resource.Modifier.HasModifiedGameState, Is.True);
        }

        [UnityTest]
        public IEnumerator
            Bootstrap_SearchAndPanelToggleDoNotCountButUiMutationDoes()
        {
            yield return new EnterPlayMode();
            var owned = new List<UnityEngine.Object>();
            try
            {
                RuntimeBootstrapFixture fixture =
                    CreateRuntimeBootstrapFixture(owned);
                fixture.Bootstrap.Configure(
                    fixture.Session,
                    fixture.City,
                    fixture.Presentation,
                    fixture.Canvas);

                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.False);
                Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.True);
                GameObject panel = FindPanel(fixture.Canvas);
                InputNamed(panel, "Resource Search").text = "铁";
                InputNamed(panel, "Research Search").text = "自动";
                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.False);

                ButtonNamed(panel, "Resource +100").onClick.Invoke();

                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.True);
                Assert.That(fixture.Bootstrap.TryTogglePanel(), Is.True);
                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.True);

                fixture.Bootstrap.enabled = false;
                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.True);
                fixture.Bootstrap.enabled = true;
                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.True);

                GrayboxBuildingSession3D replacementSession =
                    CreateRuntimeSession(owned, "Replacement Session");
                fixture.Bootstrap.Configure(
                    replacementSession,
                    fixture.City,
                    fixture.Presentation,
                    fixture.Canvas);
                Assert.That(fixture.Bootstrap.HasModifiedGameState, Is.False);
            }
            finally
            {
                DestroyRuntimeObjects(owned);
            }
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
        public void FeedbackCommandsResolveChineseAndReportActualChanges()
        {
            ModifierFixture fixture = CreateFixture();
            fixture.Session.Inventory.Set(ResourceIds.HybridCore, 4995);

            GrayboxDeveloperCommandResult3D partial =
                fixture.Modifier.AddResourceWithFeedback("融合核心", 10);
            Assert.That(partial.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.PartialCapacity));
            Assert.That(partial.Succeeded, Is.True);
            Assert.That(partial.StableId, Is.EqualTo(ResourceIds.HybridCore));
            Assert.That(partial.DisplayName, Is.EqualTo("融合核心"));
            Assert.That(partial.RequestedAmount, Is.EqualTo(10));
            Assert.That(partial.AppliedAmount, Is.EqualTo(5));
            Assert.That(partial.Message, Does.Contain("实际增加 5"));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.HybridCore),
                Is.EqualTo(5000));

            GrayboxDeveloperCommandResult3D full =
                fixture.Modifier.AddResourceWithFeedback("融合核心", 10);
            Assert.That(full.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.CapacityFull));
            Assert.That(full.Succeeded, Is.False);
            Assert.That(full.AppliedAmount, Is.Zero);
            Assert.That(full.Message, Does.Contain("容量已满"));

            GrayboxDeveloperCommandResult3D invalid =
                fixture.Modifier.AddResourceWithFeedback("未知物品", 10);
            Assert.That(invalid.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.UnknownResource));
            Assert.That(invalid.Message, Does.Contain("未找到物品"));

            GrayboxDeveloperCommandResult3D cappedSet =
                fixture.Modifier.SetResourceWithFeedback("融合核心", 9999);
            Assert.That(cappedSet.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.PartialCapacity));
            Assert.That(cappedSet.Succeeded, Is.True);
            Assert.That(cappedSet.RequestedAmount, Is.EqualTo(9999));
            Assert.That(cappedSet.AppliedAmount, Is.EqualTo(5000));
            Assert.That(cappedSet.Message,
                Does.Contain("容量上限").And.Contain("实际设为 5000"));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.HybridCore),
                Is.EqualTo(5000));

            GrayboxDeveloperCommandResult3D unlocked =
                fixture.Modifier.UnlockResearchWithFeedback("灵火淬炼");
            Assert.That(unlocked.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.Success));
            Assert.That(unlocked.AffectedCount, Is.EqualTo(1));
            Assert.That(unlocked.Message, Does.Contain("已解锁科技：灵火淬炼"));
            Assert.That(fixture.Session.IsResearchCompleted(
                "core.research.spirit-sensing"), Is.True);

            GrayboxDeveloperCommandResult3D repeated =
                fixture.Modifier.UnlockResearchWithFeedback("灵火淬炼");
            Assert.That(repeated.Code,
                Is.EqualTo(GrayboxDeveloperCommandCode3D.AlreadyCompleted));
            Assert.That(repeated.AffectedCount, Is.Zero);
            Assert.That(repeated.Message, Does.Contain("已经解锁"));

            GrayboxDeveloperCommandResult3D route =
                fixture.Modifier.UnlockRouteWithFeedback(
                    ContentRoute.Technology);
            Assert.That(route.Succeeded, Is.True);
            Assert.That(route.AffectedCount, Is.GreaterThan(0));
            Assert.That(route.Message, Does.Contain("科技路线已解锁"));
            Assert.That(fixture.Session.HasContactedRoute(
                ContentRoute.Technology), Is.True);

            GrayboxDeveloperCommandResult3D all =
                fixture.Modifier.UnlockAllResearchWithFeedback();
            Assert.That(all.Succeeded, Is.True);
            Assert.That(all.AffectedCount, Is.GreaterThan(0));
            Assert.That(all.Message, Does.Contain("全部科技已解锁"));
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
        public void Commands_SetPopulationAndAdvanceCatalogOnlyOnChange()
        {
            ModifierFixture fixture = CreateFixture();
            uint revision = fixture.Session.CatalogRevision;

            Assert.That(fixture.Modifier.SetPopulation(-1), Is.False);
            Assert.That(fixture.Session.Population, Is.EqualTo(200));
            Assert.That(fixture.Session.CatalogRevision, Is.EqualTo(revision));
            Assert.That(fixture.Modifier.SetPopulation(2000), Is.True);
            Assert.That(fixture.Session.Population, Is.EqualTo(2000));
            Assert.That(
                fixture.Session.CatalogRevision,
                Is.EqualTo(revision + 1));
            Assert.That(fixture.Modifier.SetPopulation(2000), Is.True);
            Assert.That(
                fixture.Session.CatalogRevision,
                Is.EqualTo(revision + 1));
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
            float expectedRemaining =
                next.Progress.Remaining -
                .1f * fixture.Session.ProductivityMultiplier *
                fixture.Session.DevelopmentRuleTimeMultiplier;
            fixture.Session.TickConstruction(
                .1f,
                CityMode.Fortress,
                false,
                fixture.Presentation);
            Assert.That(
                next.Progress.Remaining,
                Is.EqualTo(expectedRemaining).Within(.0001f));
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

        private static string ProjectReleaseSource(string source)
        {
            var result = new StringBuilder();
            var parents = new Stack<bool>();
            bool active = true;
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                string directive = lines[index].Trim();
                if (directive == "#if UNITY_EDITOR || DEVELOPMENT_BUILD")
                {
                    parents.Push(active);
                    active = false;
                    continue;
                }
                if (directive == "#else")
                {
                    Assert.That(parents, Is.Not.Empty);
                    active = parents.Peek();
                    continue;
                }
                if (directive == "#endif")
                {
                    Assert.That(parents, Is.Not.Empty);
                    active = parents.Pop();
                    continue;
                }
                if (directive.StartsWith("#if", StringComparison.Ordinal))
                    Assert.Fail("Unexpected preprocessor condition: " +
                        directive);
                if (active)
                    result.AppendLine(lines[index]);
            }
            Assert.That(parents, Is.Empty);
            return result.ToString();
        }

        private static string ExtractMemberBody(
            string source,
            string declaration)
        {
            int declarationIndex = source.IndexOf(
                declaration,
                StringComparison.Ordinal);
            Assert.That(
                declarationIndex,
                Is.GreaterThanOrEqualTo(0),
                "Missing declaration " + declaration);
            int bodyStart = source.IndexOf('{', declarationIndex);
            Assert.That(bodyStart, Is.GreaterThan(declarationIndex));
            var depth = 0;
            for (var index = bodyStart; index < source.Length; index++)
            {
                if (source[index] == '{')
                    depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(
                        bodyStart,
                        index - bodyStart + 1);
            }
            Assert.Fail("Unclosed member body for " + declaration);
            return null;
        }

        private static string NormalizeWhitespace(string value)
        {
            return Regex.Replace(value, @"\s+", " ").Trim();
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

        private static void ConfigureRuntimePresentation(
            List<UnityEngine.Object> owned,
            GrayboxBuildingWorldView3D presentation,
            GrayboxMobileCityController3D city,
            string prefix)
        {
            Transform instanceRoot = RuntimeTrack(
                owned,
                new GameObject(prefix + " Instance Root")).transform;
            Transform infrastructureRoot = RuntimeTrack(
                owned,
                new GameObject(prefix + " Infrastructure Root")).transform;
            Shader shader = Shader.Find("Hidden/InternalErrorShader") ??
                Shader.Find("Unlit/Color");
            var material = new Material(shader);
            owned.Add(material);
            presentation.Configure(
                instanceRoot,
                infrastructureRoot,
                material,
                city);
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

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

    }
}
