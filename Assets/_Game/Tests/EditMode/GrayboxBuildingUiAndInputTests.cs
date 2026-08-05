using System;
using System.Collections.Generic;
using System.IO;
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
            for (var index = 0; index < cleanup.Count; index++)
                if (cleanup[index] != null &&
                    cleanup[index] is GameObject gameObject)
                {
                    Camera camera = gameObject.GetComponent<Camera>();
                    if (camera != null)
                        camera.targetTexture = null;
                }
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
        public void Menu_RealPointerDispatchReachesEveryConstructionAction()
        {
            UiFixture fixture = CreateMenuFixture();
            var cancelCount = 0;
            var confirmedCount = 0;
            var declinedCount = 0;
            fixture.Menu.CancelSelectedConstructionRequested +=
                () => cancelCount++;
            fixture.Menu.CancelConstructionConfirmationResolved +=
                confirmed =>
                {
                    if (confirmed) confirmedCount++;
                    else declinedCount++;
                };

            Button cancel = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Cancel");
            Button yes = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Confirm.Yes");
            Button no = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Confirm.No");

            AssertReadableAndSeparate(cancel, yes, no);
            PointerClick(fixture, cancel);
            PointerClick(fixture, yes);
            PointerClick(fixture, no);

            Assert.That(cancelCount, Is.EqualTo(1));
            Assert.That(confirmedCount, Is.EqualTo(1));
            Assert.That(declinedCount, Is.EqualTo(1));
        }

        [Test]
        public void Menu_RealPointerDispatchReachesAllEvacuationRowTreatments()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingInstance3D instance = BeginInnerConstruction(
                fixture.Session,
                new RecordingPresentation());
            fixture.Menu.ShowEvacuation(new[] { instance });
            var treatments = new List<BuildingEvacuationTreatment>();
            fixture.Menu.EvacuationItemTreatmentRequested +=
                (stableId, treatment) =>
                {
                    Assert.That(
                        stableId,
                        Is.EqualTo(instance.StableInstanceId));
                    treatments.Add(treatment);
                };
            string prefix =
                "Evacuation.Item." + instance.StableInstanceId + ".";
            Button abandon = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "Abandon");
            Button full = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "FullDismantle");
            Button quick = FindComponent<Button>(
                fixture.Canvas.transform,
                prefix + "QuickDismantle");

            AssertReadableAndSeparate(abandon, full, quick);
            PointerClick(fixture, abandon);
            PointerClick(fixture, full);
            PointerClick(fixture, quick);

            Assert.That(
                treatments,
                Is.EqualTo(new[]
                {
                    BuildingEvacuationTreatment.Abandon,
                    BuildingEvacuationTreatment.FullDismantle,
                    BuildingEvacuationTreatment.QuickDismantle
                }));
        }

        [Test]
        public void Menu_CatalogFieldsAndHoverDetailsHaveReadableNonOverlappingRects()
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Smelter.Id.Value);
            Transform name = FindTransform(card, "Name");
            Transform cost = FindTransform(card, "Cost");
            Transform reason = FindTransform(card, "PrimaryReason");
            Transform details = FindTransform(card, "Details");
            Assert.That(card, Is.Not.Null);
            Assert.That(name, Is.Not.Null);
            Assert.That(cost, Is.Not.Null);
            Assert.That(reason, Is.Not.Null);
            Assert.That(details, Is.Not.Null);

            var pointer = new PointerEventData(fixture.EventSystem);
            ExecuteEvents.Execute(
                card.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            ForceCanvasLayout(fixture.Canvas);

            Rect nameRect = ScreenRect(
                fixture,
                (RectTransform)name);
            Rect costRect = ScreenRect(
                fixture,
                (RectTransform)cost);
            Rect reasonRect = ScreenRect(
                fixture,
                (RectTransform)reason);
            Rect detailsRect = ScreenRect(
                fixture,
                (RectTransform)details);
            AssertReadable(nameRect, "name");
            AssertReadable(costRect, "cost");
            AssertReadable(reasonRect, "reason");
            AssertReadable(detailsRect, "details");
            AssertNoAreaOverlap(nameRect, costRect);
            AssertNoAreaOverlap(nameRect, reasonRect);
            AssertNoAreaOverlap(costRect, reasonRect);
            AssertNoAreaOverlap(detailsRect, nameRect);
            AssertNoAreaOverlap(detailsRect, costRect);
            AssertNoAreaOverlap(detailsRect, reasonRect);
        }

        [TestCase("core.building.smelter")]
        [TestCase("core.building.assembler")]
        public void Menu_HoverDetailsFitEveryPromisedFieldWithoutClipping(
            string stableBuildingId)
        {
            UiFixture fixture = CreateMenuFixture();
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Assert.That(fixture.Menu.CatalogVisible, Is.True);
            BuildingDefinition definition = BuildingCatalog.BuildMenu.Single(
                candidate => candidate.Id.Value == stableBuildingId);
            GrayboxBuildingCatalogItem3D item =
                new GrayboxBuildingCatalogPresenter3D().Describe(
                    fixture.Session,
                    definition);
            Transform card = FindTransform(
                fixture.Canvas.transform,
                "Catalog.Card." + stableBuildingId);
            Transform details = FindTransform(card, "Details");
            Text detailsText = FindComponent<Text>(
                details,
                "Details.Text");
            Assert.That(item.Visibility, Is.EqualTo(
                BuildingCatalogVisibility.Locked));

            var pointer = new PointerEventData(fixture.EventSystem);
            ExecuteEvents.Execute(
                card.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);
            ForceCanvasLayout(fixture.Canvas);

            Assert.That(details.gameObject.activeSelf, Is.True);
            Assert.That(
                detailsText.preferredHeight,
                Is.LessThanOrEqualTo(
                    detailsText.rectTransform.rect.height + .01f),
                detailsText.text);
            Assert.That(
                detailsText.verticalOverflow,
                Is.EqualTo(VerticalWrapMode.Truncate));
            Assert.That(detailsText.text, Does.Contain(definition.Name));
            Assert.That(detailsText.text, Does.Contain("类别 生产"));
            Assert.That(detailsText.text, Does.Contain("路线 核心"));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "占地 " + definition.Width + "×" +
                    definition.Height));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "位置 " + BuildingMobilityRules.PlacementName(
                        definition.Placement)));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "施工 " + definition.BuildSeconds + " 秒"));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "完整成本 " + definition.Cost + " " +
                    definition.CostId));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "研究 " +
                    (definition.RequiredResearchId ?? "无")));
            Assert.That(
                detailsText.text,
                Does.Contain(
                    "前置 " +
                    (definition.RequiredBuildingId ?? "无")));
            Assert.That(detailsText.text, Does.Contain("锁定原因 "));
            foreach (string lockReason in item.LockReasons)
                Assert.That(detailsText.text, Does.Contain(lockReason));

            Rect detailsRect = ScreenRect(
                fixture,
                (RectTransform)details);
            Rect cardRect = ScreenRect(
                fixture,
                (RectTransform)card);
            Assert.That(
                RectContains(cardRect, detailsRect),
                Is.True,
                "card " + cardRect + " details " + detailsRect);
            Transform sibling = card.parent.GetChild(
                card.GetSiblingIndex() == 0 ? 1 :
                card.GetSiblingIndex() - 1);
            AssertNoAreaOverlap(
                cardRect,
                ScreenRect(fixture, (RectTransform)sibling));
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

        [Test]
        public void Menu_DestroyAndRecreateRemovesOwnedRootAndOldCallbacks()
        {
            UiFixture fixture = CreateMenuFixture();
            GrayboxBuildingMenuView3D oldMenu = fixture.Menu;
            Button oldCancel = FindComponent<Button>(
                fixture.Canvas.transform,
                "Construction.Cancel");
            var oldCallbackCount = 0;
            oldMenu.CancelSelectedConstructionRequested +=
                () => oldCallbackCount++;

            InvokeLifecycle(oldMenu, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(oldMenu.gameObject);

            Assert.That(oldCancel == null, Is.True);
            Assert.That(
                NamedTransforms(
                    fixture.Canvas.transform,
                    "GrayboxBuildingUi.Root").Count,
                Is.Zero);

            GrayboxBuildingMenuView3D replacement =
                Create<GrayboxBuildingMenuView3D>("ReplacementMenu");
            replacement.Configure(
                fixture.Canvas,
                fixture.EventSystem,
                fixture.Session,
                fixture.Interaction);
            Assert.That(
                NamedComponents<Button>(
                    fixture.Canvas.transform,
                    "QuickbarSlot.").Count,
                Is.EqualTo(10));
            PointerClick(
                fixture.WithMenu(replacement),
                FindComponent<Button>(
                    fixture.Canvas.transform,
                    "Construction.Cancel"));
            Assert.That(oldCallbackCount, Is.Zero);
        }

        [Test]
        public void ConstructionController_ReconfigureAfterDestroyedViewKeepsOneReplacementDelegate()
        {
            ControllerFixture fixture = CreateControllerFixture();
            GrayboxBuildingMenuView3D oldMenu = fixture.Menu;
            UnityEngine.Object.DestroyImmediate(oldMenu.gameObject);
            GrayboxBuildingMenuView3D replacement =
                Create<GrayboxBuildingMenuView3D>("ReplacementMenu");
            replacement.Configure(
                fixture.Canvas,
                fixture.EventSystem,
                fixture.Session,
                fixture.Interaction);

            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation,
                fixture.Interaction,
                fixture.Camera,
                replacement);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.Presentation,
                fixture.Interaction,
                fixture.Camera,
                replacement);

            Assert.That(
                EventSubscriberCount(
                    replacement,
                    "CancelSelectedConstructionRequested"),
                Is.EqualTo(1));
            Assert.That(
                EventSubscriberCount(
                    replacement,
                    "CancelConstructionConfirmationResolved"),
                Is.EqualTo(1));
        }

        [Test]
        public void ConstructionController_UpdateUsesScaledDeltaTime()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxConstructionController3D.cs"));

            Assert.That(
                source,
                Does.Contain("TickConstruction(Time.deltaTime);"));
            Assert.That(
                source,
                Does.Not.Contain(
                    "TickConstruction(Time.unscaledDeltaTime);"));
        }

        private UiFixture CreateMenuFixture()
        {
            EventSystem eventSystem = Create<EventSystem>("EventSystem");
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            Canvas canvas = Create<Canvas>("Canvas");
            Camera uiCamera = Create<Camera>("MenuUiCamera");
            uiCamera.orthographic = true;
            uiCamera.transform.position = new Vector3(0f, 0f, -10f);
            uiCamera.pixelRect = new Rect(0f, 0f, 640f, 480f);
            var target = new RenderTexture(640, 480, 0);
            cleanup.Add(target);
            uiCamera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            GraphicRaycaster raycaster =
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            raycaster.enabled = true;
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
            canvas.enabled = false;
            canvas.enabled = true;
            raycaster.enabled = false;
            raycaster.enabled = true;
            RegisterHeadlessEditModeRaycaster(raycaster);
            ForceCanvasLayout(canvas);
            uiCamera.Render();
            return new UiFixture(
                canvas,
                eventSystem,
                uiCamera,
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

        private static void PointerClick(UiFixture fixture, Button button)
        {
            Assert.That(button, Is.Not.Null);
            ForceCanvasLayout(fixture.Canvas);
            Vector2 position = RectCenter(fixture, button);
            var pointer = new PointerEventData(fixture.EventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = position
            };
            var results = new List<RaycastResult>();
            var directResults = new List<RaycastResult>();
            button.GetComponentInParent<GraphicRaycaster>().Raycast(
                pointer,
                directResults);
            Assert.That(
                directResults,
                Is.Not.Empty,
                "Direct GraphicRaycaster missed " + position +
                " in " + fixture.UiCamera.pixelRect);
            fixture.EventSystem.RaycastAll(pointer, results);
            Assert.That(results, Is.Not.Empty);
            RaycastResult hit = results.First(result =>
                result.gameObject != null &&
                result.gameObject.activeInHierarchy);
            Assert.That(hit.gameObject, Is.Not.Null);
            GameObject resolved =
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    hit.gameObject);
            Assert.That(
                resolved,
                Is.EqualTo(button.gameObject),
                "The top real pointer hit must resolve to the expected button.");
            GameObject handled = ExecuteEvents.ExecuteHierarchy(
                hit.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            Assert.That(handled, Is.EqualTo(button.gameObject));
        }

        private static void AssertReadableAndSeparate(params Button[] buttons)
        {
            Assert.That(buttons, Has.All.Not.Null);
            Canvas canvas = buttons[0].GetComponentInParent<Canvas>();
            ForceCanvasLayout(canvas);
            var rects = buttons
                .Select(button => ScreenRect(
                    canvas,
                    (RectTransform)button.transform))
                .ToArray();
            for (var index = 0; index < rects.Length; index++)
            {
                AssertReadable(rects[index]);
                for (var other = index + 1;
                     other < rects.Length;
                     other++)
                    Assert.That(
                        OverlapArea(rects[index], rects[other]),
                        Is.EqualTo(0f).Within(.01f));
            }
        }

        private static void AssertReadable(Rect rect)
        {
            AssertReadable(rect, "control");
        }

        private static void AssertReadable(Rect rect, string label)
        {
            Assert.That(
                rect.width,
                Is.GreaterThanOrEqualTo(40f),
                label + " width " + rect);
            Assert.That(
                rect.height,
                Is.GreaterThanOrEqualTo(12f),
                label + " height " + rect);
        }

        private static void AssertNoAreaOverlap(Rect left, Rect right)
        {
            Assert.That(
                OverlapArea(left, right),
                Is.EqualTo(0f).Within(.01f));
        }

        private static float OverlapArea(Rect left, Rect right)
        {
            float width = Mathf.Max(
                0f,
                Mathf.Min(left.xMax, right.xMax) -
                Mathf.Max(left.xMin, right.xMin));
            float height = Mathf.Max(
                0f,
                Mathf.Min(left.yMax, right.yMax) -
                Mathf.Max(left.yMin, right.yMin));
            return width * height;
        }

        private static bool RectContains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - .01f &&
                   inner.yMin >= outer.yMin - .01f &&
                   inner.xMax <= outer.xMax + .01f &&
                   inner.yMax <= outer.yMax + .01f;
        }

        private static void ForceCanvasLayout(Canvas canvas)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutGroup[] groups =
                    canvas.GetComponentsInChildren<LayoutGroup>(true);
                for (var index = groups.Length - 1;
                     index >= 0;
                     index--)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(
                        (RectTransform)groups[index].transform);
            }
            Canvas.ForceUpdateCanvases();
            if (canvas.worldCamera != null &&
                canvas.worldCamera.targetTexture != null)
                canvas.worldCamera.Render();
        }

        private static Vector2 RectCenter(
            UiFixture fixture,
            Button button)
        {
            RectTransform rect = (RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(
                fixture.UiCamera,
                rect.TransformPoint(rect.rect.center));
        }

        private static Rect ScreenRect(
            UiFixture fixture,
            RectTransform rect)
        {
            return ScreenRect(fixture.Canvas, rect);
        }

        private static Rect ScreenRect(
            Canvas canvas,
            RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera camera = canvas.renderMode ==
                            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 minimum =
                RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum =
                RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(minimum.x, maximum.x),
                Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x),
                Mathf.Max(minimum.y, maximum.y));
        }

        private static int EventSubscriberCount(
            GrayboxBuildingMenuView3D menu,
            string eventName)
        {
            var field = typeof(GrayboxBuildingMenuView3D).GetField(
                eventName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            var callback = (Delegate)field.GetValue(menu);
            return callback == null
                ? 0
                : callback.GetInvocationList().Length;
        }

        private static void InvokeLifecycle(
            MonoBehaviour behaviour,
            string methodName)
        {
            typeof(GrayboxBuildingMenuView3D)
                .GetMethod(
                    methodName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(behaviour, null);
        }

        private static void RegisterHeadlessEditModeRaycaster(
            GraphicRaycaster raycaster)
        {
            Type manager = typeof(BaseRaycaster).Assembly.GetType(
                "UnityEngine.EventSystems.RaycasterManager");
            manager.GetMethod(
                    "AddRaycaster",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { raycaster });
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

        private static List<Transform> NamedTransforms(
            Transform root,
            string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == name)
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
                Camera uiCamera,
                GrayboxBuildingSession3D session,
                GrayboxBuildingInteractionModel3D interaction,
                GrayboxBuildingMenuView3D menu)
            {
                Canvas = canvas;
                EventSystem = eventSystem;
                UiCamera = uiCamera;
                Session = session;
                Interaction = interaction;
                Menu = menu;
            }

            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
            public Camera UiCamera { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
            public GrayboxBuildingMenuView3D Menu { get; }

            public UiFixture WithMenu(
                GrayboxBuildingMenuView3D replacement)
            {
                return new UiFixture(
                    Canvas,
                    EventSystem,
                    UiCamera,
                    Session,
                    Interaction,
                    replacement);
            }
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
                EventSystem = ui.EventSystem;
                Session = ui.Session;
                Interaction = ui.Interaction;
                Menu = ui.Menu;
                City = city;
                Presentation = presentation;
                Camera = camera;
                Controller = controller;
            }

            public Canvas Canvas { get; }
            public EventSystem EventSystem { get; }
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
