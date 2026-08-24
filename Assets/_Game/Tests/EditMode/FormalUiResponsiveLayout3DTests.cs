using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class FormalUiResponsiveLayout3DTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2580f, 1080f)]
        public void IDEA0018_PersistentHudUsesNonOverlappingFormalSlots(
            float width,
            float height)
        {
            Canvas canvas = CreateCanvas(width, height);
            GrayboxOperationsView3D operations =
                canvas.gameObject.AddComponent<GrayboxOperationsView3D>();
            operations.Configure(canvas);
            EventSystem eventSystem = CreateEventSystem();
            var hudObject = Track(new GameObject("FormalDefenseHud"));
            GrayboxDefenseHudView3D hud =
                hudObject.AddComponent<GrayboxDefenseHudView3D>();
            hud.Configure(canvas, eventSystem);
            Canvas.ForceUpdateCanvases();

            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(0f, 0f, width, height));
            Rect resource = CanvasRect(
                canvas,
                Required(canvas.transform, "ResourceStatusBar"));
            Rect summary = CanvasRect(canvas, hud.SummaryRect);
            Rect speed = CanvasRect(canvas, hud.SpeedRect);
            Rect selection = CanvasRect(canvas, hud.SelectionRect);

            AssertInside(layout.ResourceStatusSlot, resource);
            Assert.That(summary.Overlaps(resource), Is.False);
            Assert.That(speed.Overlaps(resource), Is.False);
            Assert.That(selection.Overlaps(resource), Is.False);
            Assert.That(selection.Overlaps(speed), Is.False);
            AssertInside(layout.SafeArea, summary);
            AssertInside(layout.SafeArea, speed);
            AssertInside(layout.SafeArea, selection);
            Assert.That(
                Required(canvas.transform, "ProductionObservabilityUi.Root")
                    .localScale,
                Is.EqualTo(Vector3.one));
            Assert.That(hud.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2580f, 1080f)]
        public void IDEA0018_LargeOperationsPanelsFitSafeModalArea(
            float width,
            float height)
        {
            Canvas canvas = CreateCanvas(width, height);
            GrayboxOperationsView3D operations =
                canvas.gameObject.AddComponent<GrayboxOperationsView3D>();
            operations.Configure(canvas);
            Canvas.ForceUpdateCanvases();
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                new Rect(0f, 0f, width, height));

            string[] panelNames =
            {
                "FullResourceLedgerPanel",
                "InventoryCraftingPanel",
                "ResearchTreePanel",
            };
            foreach (string panelName in panelNames)
            {
                RectTransform panel = Required(canvas.transform, panelName);
                AssertInside(layout.MainModalArea, CanvasRect(canvas, panel));
                Assert.That(panel.localScale, Is.EqualTo(Vector3.one));
            }

            Text researchDetails = canvas
                .GetComponentsInChildren<Text>(true)
                .First(value => value.name.EndsWith(
                    ".Details",
                    StringComparison.Ordinal));
            Assert.That(
                researchDetails.fontSize,
                Is.GreaterThanOrEqualTo(
                    FormalUiLayoutProfile3D.Standard.FontDescription));
        }

        private Canvas CreateCanvas(float width, float height)
        {
            var canvasObject = Track(new GameObject(
                "FormalResponsiveCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster)));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().sizeDelta =
                new Vector2(width, height);
            return canvas;
        }

        private EventSystem CreateEventSystem()
        {
            return Track(new GameObject(
                    "FormalResponsiveEventSystem",
                    typeof(EventSystem)))
                .GetComponent<EventSystem>();
        }

        private GameObject Track(GameObject value)
        {
            cleanup.Add(value);
            return value;
        }

        private static RectTransform Required(
            Transform root,
            string objectName)
        {
            RectTransform match = root
                .GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(value => string.Equals(
                    value.name,
                    objectName,
                    StringComparison.Ordinal));
            Assert.That(match, Is.Not.Null, objectName);
            return match;
        }

        private static Rect CanvasRect(Canvas canvas, RectTransform target)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
            Vector3 topRight = canvasRect.InverseTransformPoint(corners[2]);
            Vector2 size = canvasRect.rect.size;
            return Rect.MinMaxRect(
                bottomLeft.x + size.x * .5f,
                bottomLeft.y + size.y * .5f,
                topRight.x + size.x * .5f,
                topRight.y + size.y * .5f);
        }

        private static void AssertInside(Rect outer, Rect inner)
        {
            const float tolerance = .5f;
            Assert.That(inner.xMin,
                Is.GreaterThanOrEqualTo(outer.xMin - tolerance));
            Assert.That(inner.yMin,
                Is.GreaterThanOrEqualTo(outer.yMin - tolerance));
            Assert.That(inner.xMax,
                Is.LessThanOrEqualTo(outer.xMax + tolerance));
            Assert.That(inner.yMax,
                Is.LessThanOrEqualTo(outer.yMax + tolerance));
        }
    }
}
