using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Tests
{
    /// <summary>
    /// IDEA-0021 contracts for the formal 44-node research-tree view.
    /// Pointer gestures and input ownership belong in PlayMode/real-input tests;
    /// this fixture only freezes the generated uGUI hierarchy and layout.
    /// </summary>
    public sealed class ResearchTreeUiContractTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void IDEA0016_BuildsFormalTreeViewportSearchAndRouteFilters()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);

            Transform panel = Required(root, "ResearchTreePanel");
            Text title = Required(panel, "Research.Title")
                .GetComponent<Text>();
            Transform viewport = Required(panel, "Research.Viewport");
            Transform content = Required(viewport, "Research.Content");
            Transform nodes = Required(content, "Research.Nodes");

            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Does.Not.Contain("首版"));
            Assert.That(
                Required(panel, "Research.Search").GetComponent<InputField>(),
                Is.Not.Null);

            string[] primaryRoutes =
            {
                "Technology",
                "Cultivation",
                "Biological",
                "Psionics",
            };
            foreach (string route in primaryRoutes)
            {
                Transform filter = Required(
                    panel,
                    "Research.Filter.Route." + route);
                Assert.That(
                    filter.GetComponent<Selectable>(),
                    Is.Not.Null,
                    route + " route filter must be an interactive uGUI control");
            }

            Button[] nodeButtons = nodes
                .GetComponentsInChildren<Button>(true);
            string[] expectedNames = ResearchCatalog.All
                .Select(value => "Research.Node." + value.Id.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualNames = nodeButtons
                .Select(value => value.name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(nodeButtons, Has.Length.EqualTo(44));
            Assert.That(actualNames, Is.EqualTo(expectedNames));
            Assert.That(view.IsResearchOpen, Is.True);
        }

        [Test]
        public void IDEA0016_DrawsEveryFormalPrerequisiteBehindNodesWithoutRaycasts()
        {
            CreateView(out Transform root);
            Transform content = Required(
                Required(root, "Research.Viewport"),
                "Research.Content");
            Transform connections = Required(
                content,
                "Research.Connections");
            Transform nodes = Required(content, "Research.Nodes");

            string[] expectedConnections = ResearchCatalog.All
                .SelectMany(child => child.RequiredResearchIds.Select(
                    parent => "Research.Connection." + parent + "->" +
                        child.Id.Value))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actualConnections = connections
                .GetComponentsInChildren<Transform>(true)
                .Where(value => value != connections)
                .Where(value => value.name.StartsWith(
                    "Research.Connection.",
                    StringComparison.Ordinal))
                .Select(value => value.name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actualConnections, Is.EqualTo(expectedConnections));
            Assert.That(
                connections.GetSiblingIndex(),
                Is.LessThan(nodes.GetSiblingIndex()),
                "connections must render before the node layer");

            Graphic[] lineGraphics = connections
                .GetComponentsInChildren<Graphic>(true);
            Assert.That(lineGraphics, Is.Not.Empty);
            foreach (Graphic graphic in lineGraphics)
            {
                Assert.That(
                    graphic.raycastTarget,
                    Is.False,
                    graphic.name + " must not block node or viewport input");
            }
            Transform[] junctions = connections
                .GetComponentsInChildren<Transform>(true)
                .Where(value => value.name.StartsWith(
                    "Research.Junction.", StringComparison.Ordinal))
                .ToArray();
            Assert.That(junctions, Is.Not.Empty);
            Assert.That(junctions.Select(value => value.name), Is.Unique);
            Transform[] trunks = connections
                .GetComponentsInChildren<Transform>(true)
                .Where(value => value.name.StartsWith(
                    "Research.Trunk.", StringComparison.Ordinal))
                .ToArray();
            ResearchTreeProjection3D projection =
                ResearchTreeProjection3D.Create(ResearchCatalog.All);
            Assert.That(trunks, Has.Length.EqualTo(projection.Trunks.Count));
            Assert.That(trunks.Select(value => value.name), Is.Unique);
        }

        [Test]
        public void IDEA0016_LayoutRowsRiseFromBottomAndNodeRectsDoNotOverlap()
        {
            CreateView(out Transform root);
            RectTransform content = (RectTransform)Required(
                Required(root, "Research.Viewport"),
                "Research.Content");
            Transform nodes = Required(content, "Research.Nodes");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();

            var placements = ResearchCatalog.All.Select(definition =>
            {
                RectTransform rect = (RectTransform)Required(
                    nodes,
                    "Research.Node." + definition.Id.Value);
                Bounds bounds = RectTransformUtility
                    .CalculateRelativeRectTransformBounds(content, rect);
                Assert.That(bounds.size.x, Is.GreaterThan(0f),
                    definition.Id.Value);
                Assert.That(bounds.size.y, Is.GreaterThan(0f),
                    definition.Id.Value);
                return new Placement(definition, bounds);
            }).ToArray();

            Placement[] rows = placements
                .GroupBy(value => value.Definition.LayoutRow)
                .OrderBy(value => value.Key)
                .Select(group =>
                {
                    Placement first = group.First();
                    foreach (Placement placement in group)
                    {
                        Assert.That(
                            placement.Bounds.center.y,
                            Is.EqualTo(first.Bounds.center.y).Within(.5f),
                            "all nodes in LayoutRow " + group.Key +
                            " must share one Y coordinate");
                    }
                    return first;
                })
                .ToArray();

            for (var index = 1; index < rows.Length; index++)
            {
                Assert.That(
                    rows[index].Bounds.center.y,
                    Is.GreaterThan(rows[index - 1].Bounds.center.y),
                    "higher LayoutRow values must appear above lower rows");
            }

            for (var left = 0; left < placements.Length; left++)
            {
                for (var right = left + 1;
                     right < placements.Length;
                     right++)
                {
                    Assert.That(
                        HasPositiveOverlap(
                            placements[left].Bounds,
                            placements[right].Bounds),
                        Is.False,
                        placements[left].Definition.Id.Value + " overlaps " +
                        placements[right].Definition.Id.Value);
                }
            }
        }

        [Test]
        public void IDEA0021_ConnectionGraphicBuildsDashedPathsAndJunctionMesh()
        {
            var owner = new GameObject(
                "Research.Connection.Geometry.Test",
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(ResearchTreeConnectionGraphic3D));
            cleanup.Add(owner);
            ResearchTreeConnectionGraphic3D graphic =
                owner.GetComponent<ResearchTreeConnectionGraphic3D>();
            MethodInfo populate = typeof(ResearchTreeConnectionGraphic3D)
                .GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
            Assert.That(populate, Is.Not.Null);

            graphic.ConfigurePath(
                new[]
                {
                    new Vector2(0f, 0f), new Vector2(0f, 96f),
                    new Vector2(300f, 96f), new Vector2(300f, 240f),
                },
                Color.cyan,
                Color.magenta,
                3f,
                true,
                true);
            using (var path = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { path });
                Assert.That(path.currentVertCount, Is.GreaterThan(16));
            }

            graphic.ConfigureJunction(
                new Vector2(20f, 30f), Color.yellow, 14f);
            using (var junction = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { junction });
                Assert.That(junction.currentVertCount, Is.EqualTo(14));
                Assert.That(junction.currentIndexCount, Is.EqualTo(36));
            }
            Assert.That(graphic.raycastTarget, Is.False);
        }

        [Test]
        public void IDEA0016_ReconfigureAndVisibilityTogglesRemainIdempotent()
        {
            GrayboxOperationsView3D view = CreateView(
                out Transform root,
                out Canvas canvas);
            Transform panel = Required(root, "ResearchTreePanel");
            Transform nodes = Required(panel, "Research.Nodes");
            Button selectedButton = nodes
                .GetComponentsInChildren<Button>(true)
                .Single(value => value.name ==
                    "Research.Node." + ResearchCatalog.All[0].Id.Value);
            int objectCount = panel
                .GetComponentsInChildren<Transform>(true).Length;
            int buttonCount = panel
                .GetComponentsInChildren<Button>(true).Length;
            var selectionCount = 0;
            string selectedId = null;
            view.ResearchSelected += id =>
            {
                selectionCount++;
                selectedId = id;
            };

            for (var index = 0; index < 4; index++)
            {
                view.Configure(canvas);
                view.SetResearchOpen(false);
                view.SetResearchOpen(true);
            }

            Transform currentPanel = Required(root, "ResearchTreePanel");
            Assert.That(currentPanel, Is.SameAs(panel));
            Assert.That(
                currentPanel.GetComponentsInChildren<Transform>(true).Length,
                Is.EqualTo(objectCount));
            Assert.That(
                currentPanel.GetComponentsInChildren<Button>(true).Length,
                Is.EqualTo(buttonCount));
            Assert.That(
                Required(
                    currentPanel,
                    selectedButton.name).GetComponent<Button>(),
                Is.SameAs(selectedButton));

            selectedButton.onClick.Invoke();
            Assert.That(selectionCount, Is.EqualTo(1));
            Assert.That(selectedId, Is.EqualTo(ResearchCatalog.All[0].Id.Value));
        }

        [Test]
        public void IDEA0016_SearchThatHidesSelectedNodeClearsSelection()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);
            string researchId = ResearchCatalog.AutomatedMachineryId;
            var selections = new List<string>();
            view.ResearchSelected += id => selections.Add(id);

            Button node = Required(
                    root,
                    "Research.Node." + researchId)
                .GetComponent<Button>();
            node.onClick.Invoke();
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                view.SetResearchNode(
                    definition,
                    string.Empty,
                    definition.Id.Value == researchId);
            }

            InputField search = Required(root, "Research.Search")
                .GetComponent<InputField>();
            search.text = "no-matching-formal-research";

            Assert.That(selections, Is.EqualTo(new[]
            {
                researchId,
                null,
            }), "hiding a selected node must clear the controller selection");
            Assert.That(node.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void IDEA0016_ViewportAcceptsPanZoomAndFitWithoutPerFramePolling()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);
            RectTransform viewport = (RectTransform)Required(
                root,
                "Research.Viewport");
            RectTransform content = (RectTransform)Required(
                viewport,
                "Research.Content");
            var eventSystemObject = new GameObject(
                "ResearchTreeUiContract.EventSystem",
                typeof(EventSystem));
            cleanup.Add(eventSystemObject);
            EventSystem eventSystem = eventSystemObject
                .GetComponent<EventSystem>();
            var drag = viewport.GetComponent(typeof(IDragHandler)) as
                IDragHandler;
            var scroll = viewport.GetComponent(typeof(IScrollHandler)) as
                IScrollHandler;

            Assert.That(drag, Is.Not.Null);
            Assert.That(scroll, Is.Not.Null);
            Vector2 positionBefore = content.anchoredPosition;
            drag.OnDrag(new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                delta = new Vector2(120f, -60f),
            });
            Assert.That(content.anchoredPosition,
                Is.Not.EqualTo(positionBefore));

            float zoomBefore = content.localScale.x;
            scroll.OnScroll(new PointerEventData(eventSystem)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    viewport.TransformPoint(viewport.rect.center)),
                scrollDelta = new Vector2(0f, 1f),
            });
            Assert.That(content.localScale.x, Is.GreaterThan(zoomBefore));

            view.FitResearchTree();
            Assert.That(content.localScale.x,
                Is.InRange(.4f, 1.45f));
            Assert.That(view.GetComponents<MonoBehaviour>()
                    .Count(component => component != null &&
                        component.GetType().GetMethod(
                            "Update",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.DeclaredOnly) !=
                        null),
                Is.Zero,
                "the research tree must not add an Update polling loop");
        }

        [Test]
        public void IDEA0016_FirstEscapeClearsResearchSearchFocusOnly()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);
            InputField search = Required(root, "Research.Search")
                .GetComponent<InputField>();
            var eventSystemObject = new GameObject(
                "ResearchTreeUiContract.FocusEventSystem",
                typeof(EventSystem));
            cleanup.Add(eventSystemObject);
            EventSystem eventSystem = eventSystemObject
                .GetComponent<EventSystem>();
            search.ActivateInputField();
            eventSystem.SetSelectedGameObject(search.gameObject);
            search.GetComponent<GrayboxResearchSearchFocus3D>()
                .OnSelect(new BaseEventData(eventSystem));

            Assert.That(view.ConsumeFocusedResearchEscape(), Is.True);
            Assert.That(view.HasResearchTextInputFocus, Is.False);
            Assert.That(view.IsResearchOpen, Is.True);
            Assert.That(view.ConsumeFocusedResearchEscape(), Is.False);
        }

        private GrayboxOperationsView3D CreateView(out Transform root)
        {
            return CreateView(out root, out _);
        }

        private GrayboxOperationsView3D CreateView(
            out Transform root,
            out Canvas canvas)
        {
            var canvasObject = new GameObject(
                "ResearchTreeUiContract.Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            cleanup.Add(canvasObject);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GrayboxOperationsView3D view =
                canvasObject.AddComponent<GrayboxOperationsView3D>();
            view.Configure(canvas);
            view.SetResearchOpen(true);
            Canvas.ForceUpdateCanvases();
            root = Required(
                canvasObject.transform,
                "ProductionObservabilityUi.Root");
            return view;
        }

        private static Transform Required(Transform root, string name)
        {
            Transform match = root
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => string.Equals(
                    value.name,
                    name,
                    StringComparison.Ordinal));
            Assert.That(match, Is.Not.Null, "Missing generated UI: " + name);
            return match;
        }

        private static bool HasPositiveOverlap(Bounds left, Bounds right)
        {
            const float tolerance = .5f;
            float horizontal = Math.Min(left.max.x, right.max.x) -
                Math.Max(left.min.x, right.min.x);
            float vertical = Math.Min(left.max.y, right.max.y) -
                Math.Max(left.min.y, right.min.y);
            return horizontal > tolerance && vertical > tolerance;
        }

        private readonly struct Placement
        {
            public Placement(ResearchDefinition definition, Bounds bounds)
            {
                Definition = definition;
                Bounds = bounds;
            }

            public ResearchDefinition Definition { get; }
            public Bounds Bounds { get; }
        }
    }
}
