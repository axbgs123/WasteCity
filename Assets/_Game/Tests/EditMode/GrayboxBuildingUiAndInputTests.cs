using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingUiAndInputTests
    {
        private static readonly Vector2 ScreenCenter =
            new Vector2(320f, 240f);

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = originalTimeScale;
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void Interaction_CatalogReturnsToItsOriginAndRetainsPreview()
        {
            GrayboxBuildingInteractionModel3D interaction =
                Create<GrayboxBuildingInteractionModel3D>("Interaction");

            interaction.ToggleCatalog();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CatalogOpen));
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            interaction.CloseCatalog();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));

            interaction.Select(BuildingCatalog.Wall);
            interaction.RotateClockwise();
            interaction.ToggleCatalog();
            Assert.That(interaction.CatalogReturnState, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            interaction.CloseCatalog();

            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(interaction.Selected, Is.SameAs(BuildingCatalog.Wall));
            Assert.That(interaction.Orientation, Is.EqualTo(
                BuildingOrientation.East));
            interaction.CancelPreview();
            Assert.That(interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
            Assert.That(interaction.Selected, Is.Null);
            Assert.That(interaction.Orientation, Is.EqualTo(
                BuildingOrientation.North));
        }

        [Test]
        public void Menu_CreatesTenQuickbarFiveCategoryAndFourRouteControls()
        {
            UiFixture fixture = CreateMenuFixture();

            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "QuickbarSlot.").Count,
                Is.EqualTo(10));
            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "Category.").Count,
                Is.EqualTo(5));
            Assert.That(
                NamedComponents<Button>(fixture.Canvas.transform, "Route.").Count,
                Is.EqualTo(4));
            Assert.That(
                FindComponent<InputField>(
                    fixture.Canvas.transform,
                    "Catalog.Search"),
                Is.Not.Null);
            Assert.That(fixture.Menu.CatalogVisible, Is.False);
            Assert.That(fixture.Menu.EvacuationVisible, Is.False);
        }

        [Test]
        public void Menu_CatalogDoesNotPauseWorldAndSearchesOnlyVisibleItems()
        {
            UiFixture fixture = CreateMenuFixture();
            float timeScaleBefore = Time.timeScale;

            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            fixture.Menu.SetSearchText("power-plant");

            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            Assert.That(fixture.Menu.SearchText, Is.EqualTo("power-plant"));
            Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));
            Assert.That(AllText(fixture.Canvas.transform), Does.Not.Contain(
                BuildingCatalog.PowerPlant.Name));
            Assert.That(AllText(fixture.Canvas.transform), Does.Not.Contain(
                BuildingCatalog.PowerPlant.Id.Value));
            Assert.That(FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value),
                Is.Null);
        }

        [Test]
        public void Menu_LockedCardsAreDisabledAndExposePrimaryAndAllReasons()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);

            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value);
            Assert.That(card, Is.Not.Null);
            Button button = card.GetComponent<Button>();
            Assert.That(button.interactable, Is.False);

            GrayboxBuildingCatalogItem3D item =
                new GrayboxBuildingCatalogPresenter3D().Describe(
                    fixture.Session,
                    BuildingCatalog.Smelter);
            string text = AllText(card);
            Assert.That(text, Does.Contain(item.PrimaryLockReason));
            foreach (string reason in item.LockReasons)
                Assert.That(text, Does.Contain(reason));
            Assert.That(text, Does.Contain(
                BuildingCatalog.Smelter.Cost.ToString()));
        }

        [Test]
        public void Menu_SelectionRejectsInvalidHiddenLockedAndFilteredItems()
        {
            UiFixture fixture = CreateMenuFixture();

            Assert.That(fixture.Menu.TrySelectQuickbarSlot(-1), Is.False);
            Assert.That(fixture.Menu.TrySelectQuickbarSlot(10), Is.False);
            Assert.That(fixture.Menu.TrySelectQuickbarSlot(0), Is.True);
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.MiningStation));

            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Basic);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.Smelter.Id.Value),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.PowerPlant.Id.Value),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    "missing.building"),
                Is.False);
            Assert.That(
                fixture.Menu.TrySelectCatalogItem(
                    BuildingCatalog.Housing.Id.Value),
                Is.True);
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Previewing));
            Assert.That(fixture.Interaction.Selected, Is.SameAs(
                BuildingCatalog.Housing));
            Assert.That(fixture.Menu.CatalogVisible, Is.False);
        }

        [Test]
        public void Menu_EmptyQuickbarSlotsAndHiddenCatalogItemsCreateNoText()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Session.SetRouteContact(ContentRoute.Technology, false);
            fixture.Menu.RefreshCatalog();

            Transform hiddenCard = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.PowerPlant.Id.Value);
            Assert.That(hiddenCard, Is.Null);

            string text = AllText(fixture.Canvas.transform);
            Assert.That(text, Does.Not.Contain(BuildingCatalog.PowerPlant.Name));
            Assert.That(text, Does.Not.Contain(
                BuildingCatalog.PowerPlant.Id.Value));
            Assert.That(
                NamedComponents<Button>(
                    fixture.Canvas.transform,
                    "Catalog.Card.").Count,
                Is.LessThan(GrayboxBuildingCatalogPresenter3D.BuildMenuCount));
        }

        [Test]
        public void UiGuard_RealSelectableFocusOwnsKeyboardUntilFollowingFrame()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.RefreshCatalog();
            InputField input = FindComponent<InputField>(
                fixture.Canvas.transform,
                "Catalog.Search");
            fixture.EventSystem.SetSelectedGameObject(input.gameObject);
            input.ActivateInputField();
            var guard = new GrayboxUiInputGuard3D();

            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
            input.text = "WASDBR1230";
            Assert.That(input.text, Is.EqualTo("WASDBR1230"));
            Assert.That(guard.ConsumeFocusedEscape(fixture.EventSystem), Is.True);
            Assert.That(fixture.EventSystem.currentSelectedGameObject, Is.Null);
            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
        }

        [Test]
        public void UiGuard_RealButtonAndRaycasterOwnKeyboardAndPointer()
        {
            UiFixture fixture = CreateMenuFixture();
            Camera uiCamera = Create<Camera>("UiCamera");
            uiCamera.orthographic = true;
            uiCamera.transform.position = new Vector3(0f, 0f, -10f);
            uiCamera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            fixture.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            fixture.Canvas.worldCamera = uiCamera;
            fixture.Canvas.planeDistance = 1f;
            GameObject pointerTarget = NewObject("PointerTarget");
            var rect = pointerTarget.AddComponent<RectTransform>();
            rect.SetParent(fixture.Canvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            pointerTarget.AddComponent<Image>();
            Button button = pointerTarget.AddComponent<Button>();
            fixture.EventSystem.SetSelectedGameObject(button.gameObject);
            var guard = new GrayboxUiInputGuard3D();

            Canvas.ForceUpdateCanvases();
            Assert.That(guard.HasKeyboardFocus(fixture.EventSystem), Is.True);
            Vector2 pointerPosition = RectTransformUtility.WorldToScreenPoint(
                uiCamera,
                rect.TransformPoint(rect.rect.center));
            Assert.That(
                guard.IsPointerOverUi(
                    fixture.EventSystem,
                    pointerPosition),
                Is.True);
            Assert.That(guard.ConsumeFocusedEscape(fixture.EventSystem), Is.True);
        }

        [Test]
        public void Menu_ButtonsRaiseOnlyTheirFrozenCallbacks()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                new RecordingPresentation());
            fixture.Menu.ShowEvacuation(new[] { instance });
            var cancelCount = 0;
            var cancelResolutionCount = 0;
            var itemCount = 0;
            var categoryCount = 0;
            var allCount = 0;
            var confirmationCount = 0;
            fixture.Menu.CancelSelectedConstructionRequested +=
                () => cancelCount++;
            fixture.Menu.CancelConstructionConfirmationResolved +=
                _ => cancelResolutionCount++;
            fixture.Menu.EvacuationItemTreatmentRequested +=
                (_, __) => itemCount++;
            fixture.Menu.EvacuationCategoryTreatmentRequested +=
                (_, __) => categoryCount++;
            fixture.Menu.EvacuationAllTreatmentRequested +=
                _ => allCount++;
            fixture.Menu.EvacuationConfirmationRequested +=
                () => confirmationCount++;

            int instanceCountBefore = fixture.Session.Instances.Count;
            Click(fixture.Canvas.transform, "Construction.Cancel");
            Click(fixture.Canvas.transform, "Construction.Confirm.Yes");
            Click(fixture.Canvas.transform, "Construction.Confirm.No");
            Click(
                fixture.Canvas.transform,
                "Evacuation.Item." + instance.StableInstanceId + ".Abandon");
            Click(
                fixture.Canvas.transform,
                "Evacuation.Category.Basic.FullDismantle");
            Click(
                fixture.Canvas.transform,
                "Evacuation.All.QuickDismantle");
            Click(fixture.Canvas.transform, "Evacuation.Confirm");

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(cancelResolutionCount, Is.EqualTo(2));
            Assert.That(itemCount, Is.EqualTo(1));
            Assert.That(categoryCount, Is.EqualTo(1));
            Assert.That(allCount, Is.EqualTo(1));
            Assert.That(confirmationCount, Is.EqualTo(1));
            Assert.That(fixture.Session.Instances.Count, Is.EqualTo(
                instanceCountBefore));
        }

        [Test]
        public void ConstructionController_ZeroProgressCancelsImmediatelyWithRefund()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            int alloyAfterSpend = fixture.Session.Inventory.Get(
                BuildingCatalog.Housing.CostId);
            Assert.That(
                fixture.Controller.SelectInstance(instance.StableInstanceId),
                Is.True);

            ConstructionCancelResult result =
                fixture.Controller.RequestCancelSelected();

            Assert.That(result, Is.EqualTo(
                ConstructionCancelResult.Cancelled));
            Assert.That(fixture.Session.Instances, Is.Empty);
            Assert.That(
                fixture.Session.Inventory.Get(BuildingCatalog.Housing.CostId),
                Is.EqualTo(alloyAfterSpend + BuildingCatalog.Housing.Cost));
        }

        [Test]
        public void ConstructionController_ProgressRequiresConfirmationAndMenuRoutesIt()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            fixture.Controller.TickConstruction(1f);
            Assert.That(instance.Progress.Normalized, Is.GreaterThan(0f));
            Assert.That(
                fixture.Controller.SelectInstance(instance.StableInstanceId),
                Is.True);

            Click(fixture.Canvas.transform, "Construction.Cancel");
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.CancelConfirmation));
            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Click(fixture.Canvas.transform, "Construction.Confirm.No");
            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));

            Click(fixture.Canvas.transform, "Construction.Cancel");
            Click(fixture.Canvas.transform, "Construction.Confirm.Yes");
            Assert.That(fixture.Session.Instances, Is.Empty);
        }

        [Test]
        public void ConstructionController_SelectAtResolvesColliderStableId()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            Transform visual = FindTransform(
                fixture.Presentation.transform,
                instance.StableInstanceId);
            fixture.Camera.transform.position =
                visual.position + Vector3.up * 10f;
            fixture.Camera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.That(fixture.Controller.SelectAt(ScreenCenter), Is.True);
            Assert.That(
                fixture.Controller.RequestCancelSelected(),
                Is.EqualTo(ConstructionCancelResult.Cancelled));
        }

        [Test]
        public void ConstructionController_TickDelegatesExactlyTheRequestedDelta()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            float before = instance.Progress.Remaining;

            fixture.Controller.TickConstruction(.75f);

            Assert.That(
                before - instance.Progress.Remaining,
                Is.EqualTo(.75f).Within(.0001f));
        }

        [Test]
        public void ConstructionController_DestroyRemovesMenuListeners()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                fixture.Presentation);
            fixture.Controller.TickConstruction(.5f);
            fixture.Controller.SelectInstance(instance.StableInstanceId);
            typeof(GrayboxConstructionController3D)
                .GetMethod(
                    "OnDestroy",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(fixture.Controller, null);
            UnityEngine.Object.DestroyImmediate(
                fixture.Controller.gameObject);

            Click(fixture.Canvas.transform, "Construction.Cancel");

            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Assert.That(fixture.Interaction.State, Is.EqualTo(
                GrayboxBuildingInteractionState.Inactive));
        }

        private UiFixture CreateMenuFixture()
        {
            EventSystem eventSystem = Create<EventSystem>("EventSystem");
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Canvas canvas = Create<Canvas>("Canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(640f, 480f);
            GrayboxBuildingSession3D session =
                Create<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            GrayboxBuildingInteractionModel3D interaction =
                Create<GrayboxBuildingInteractionModel3D>("Interaction");
            GrayboxBuildingMenuView3D menu =
                Create<GrayboxBuildingMenuView3D>("Menu");
            menu.Configure(canvas, eventSystem, session, interaction);
            return new UiFixture(
                canvas,
                eventSystem,
                session,
                interaction,
                menu);
        }

        private ControllerFixture CreateControllerFixture()
        {
            UiFixture ui = CreateMenuFixture();
            GrayboxMobileCityController3D city =
                Create<GrayboxMobileCityController3D>("City");
            GrayboxBuildingWorldView3D presentation =
                Create<GrayboxBuildingWorldView3D>("Presentation");
            var instanceRoot = NewObject("Instances");
            instanceRoot.transform.SetParent(presentation.transform, false);
            var infrastructureRoot = NewObject("Infrastructure");
            infrastructureRoot.transform.SetParent(
                presentation.transform,
                false);
            var material = new Material(Shader.Find(
                "Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color"));
            cleanup.Add(material);
            presentation.Configure(
                instanceRoot.transform,
                infrastructureRoot.transform,
                material,
                city);
            Camera camera = Create<Camera>("Camera");
            camera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            GrayboxConstructionController3D controller =
                Create<GrayboxConstructionController3D>("Construction");
            controller.Configure(
                ui.Session,
                city,
                presentation,
                ui.Interaction,
                camera,
                ui.Menu);
            return new ControllerFixture(
                ui,
                city,
                presentation,
                camera,
                controller);
        }

        private GrayboxBuildingInstance3D BeginInnerConstruction(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingDefinition definition = BuildingCatalog.Housing;
            var request = new BuildingPlacementRequest(
                definition,
                session.InnerGrid,
                BuildingSite.InnerCity,
                BuildingOrientation.North,
                0,
                0,
                0,
                0,
                session.GroundBuildRadius,
                CityMode.Mobile,
                true,
                false,
                true,
                true,
                true,
                null,
                true,
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount),
                true);
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private T Create<T>(string name) where T : Component
        {
            GameObject gameObject = NewObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            cleanup.Add(gameObject);
            return gameObject;
        }

        private static void Click(Transform root, string name)
        {
            Button button = FindComponent<Button>(root, name);
            Assert.That(button, Is.Not.Null, "Missing button " + name);
            button.onClick.Invoke();
        }

        private static List<T> NamedComponents<T>(
            Transform root,
            string prefix) where T : Component
        {
            return root.GetComponentsInChildren<T>(true)
                .Where(component => component.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
                .ToList();
        }

        private static T FindComponent<T>(
            Transform root,
            string name) where T : Component
        {
            Transform transform = FindTransform(root, name);
            return transform == null ? null : transform.GetComponent<T>();
        }

        private static Transform FindTransform(Transform root, string name)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name)
                    return transforms[index];
            return null;
        }

        private static string AllText(Transform root)
        {
            return string.Join(
                "\n",
                root.GetComponentsInChildren<Text>(true)
                    .Select(text => text.text)
                    .ToArray());
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
            }
        }

        private sealed class UiFixture
        {
            public UiFixture(
                Canvas canvas,
                EventSystem eventSystem,
                GrayboxBuildingSession3D session,
                GrayboxBuildingInteractionModel3D interaction,
                GrayboxBuildingMenuView3D menu)
            {
                Canvas = canvas;
                EventSystem = eventSystem;
                Session = session;
                Interaction = interaction;
                Menu = menu;
            }

            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingMenuView3D Menu { get; }
        }

        private sealed class ControllerFixture
        {
            public ControllerFixture(
                UiFixture ui,
                GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                Camera camera,
                GrayboxConstructionController3D controller)
            {
                Canvas = ui.Canvas;
                Session = ui.Session;
                Interaction = ui.Interaction;
                Menu = ui.Menu;
                City = city;
                Presentation = presentation;
                Camera = camera;
                Controller = controller;
            }

            public Canvas Canvas { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingMenuView3D Menu { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public Camera Camera { get; }
            public GrayboxConstructionController3D Controller { get; }
        }
    }
}
