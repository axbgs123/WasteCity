using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0024ResearchTreePresentationTests
    {
        private const string ManifestPath =
            "Docs/Art/IDEA-0024/Manifests/" +
            "idea-0024-research-tree-background-visual-assets.json";
        private const string MasterPath =
            "Docs/Art/IDEA-0024/Source/UI/" +
            "ui-research-tree-background-master-v1.png";
        private const string DeliveryPath =
            "Assets/_Game/Art/Production2D/UI/" +
            "ui-research-tree-background.png";
        private const string BackgroundId =
            "core.ui.background.research-tree";
        private const string ProfileTypeName =
            "WasteCity.Graybox3D.Usability." +
            "ResearchTreeVisualLayoutProfile3D, WasteCity.Graybox3D";
        private const string PresentationStateTypeName =
            "WasteCity.Graybox3D.Building." +
            "ResearchNodePresentationState3D, " +
            "WasteCity.Graybox3D.Building";
        private const string PresentationTypeName =
            "WasteCity.Graybox3D.Building." +
            "ResearchNodePresentation3D, WasteCity.Graybox3D.Building";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void IDEA0024_BackgroundManifestOwnsOneOriginalFullHdSprite()
        {
            ResearchBackgroundManifest manifest = LoadManifest();

            Assert.That(manifest.requirementId, Is.EqualTo("IDEA-0024"));
            Assert.That(manifest.unityVersion, Is.EqualTo("2022.3.62f1"));
            Assert.That(manifest.entries, Has.Length.EqualTo(1));
            BackgroundEntry entry = manifest.entries[0];
            Assert.That(entry.contentId, Is.EqualTo(BackgroundId));
            Assert.That(entry.displayNameZh, Is.EqualTo("科技树全屏背景"));
            Assert.That(entry.visualClass, Is.EqualTo("ui"));
            Assert.That(entry.sourceAssetPath, Is.EqualTo(MasterPath));
            Assert.That(entry.unitySpritePath, Is.EqualTo(DeliveryPath));
            Assert.That(entry.masterSizePx, Is.EqualTo(new[] { 1920, 1080 }));
            Assert.That(entry.deliverySizePx,
                Is.EqualTo(new[] { 1920, 1080 }));
            Assert.That(entry.unityGuid,
                Is.EqualTo(AssetDatabase.AssetPathToGUID(DeliveryPath)));
            Assert.That(entry.sourceSha256, Is.EqualTo(Sha256(MasterPath)));
            Assert.That(entry.deliverySha256,
                Is.EqualTo(Sha256(DeliveryPath)));
            Assert.That(entry.loreBriefZh, Is.Not.Empty);
            Assert.That(entry.useSummaryZh, Is.Not.Empty);
            Assert.That(entry.promptSummaryZh, Is.Not.Empty);
            Assert.That(entry.forbiddenElementsZh,
                Does.Contain("文字").And.Contain("科技图标"));
            Assert.That(entry.reviewState, Is.EqualTo("integrated"));

            AssertFullHdPng(MasterPath, "master");
            AssertFullHdPng(DeliveryPath, "delivery");
            var importer = AssetImporter.GetAtPath(DeliveryPath) as
                TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType,
                Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void IDEA0024_UnifiedCatalogAndUiAtlasIncludeBackgroundExactlyOnce()
        {
            Type builder = Type.GetType(
                "WasteCity.Editor.Production2DVisualCatalogBuilder, " +
                "WasteCity.Editor");
            Assert.That(builder, Is.Not.Null);
            Assert.That(builder.GetField(
                    "ExpectedVisualCount",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null),
                Is.EqualTo(141));
            MethodInfo create = builder.GetMethod(
                "CreateExpectedVisualEntries",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(create, Is.Not.Null);
            var entries = ((IEnumerable)create.Invoke(null, null))
                .Cast<object>().ToArray();
            Assert.That(entries, Has.Length.EqualTo(141));
            object[] ui = entries.Where(value => string.Equals(
                    Property(value, "VisualClass").ToString(),
                    "Ui",
                    StringComparison.Ordinal))
                .ToArray();
            Assert.That(ui, Has.Length.EqualTo(19));
            Assert.That(ui.Count(value => string.Equals(
                    (string)Property(value, "ContentId"),
                    BackgroundId,
                    StringComparison.Ordinal)),
                Is.EqualTo(1));

            Type atlasBuilder = Type.GetType(
                "WasteCity.Editor.Production2DSpriteAtlasBuilder, " +
                "WasteCity.Editor");
            IEnumerable definitions = (IEnumerable)atlasBuilder.GetProperty(
                    "Definitions",
                    BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            object uiAtlas = definitions.Cast<object>().Single(value =>
                (string)Property(value, "Name") == "UI");
            Assert.That(Property(uiAtlas, "ExpectedPackableCount"),
                Is.EqualTo(19));
        }

        [Test]
        public void IDEA0024_LayoutProfileFreezesReferenceThreeRegionsAndLanes()
        {
            Type profile = Type.GetType(ProfileTypeName);
            Assert.That(profile, Is.Not.Null,
                "The reference layout must be one pure presentation profile.");
            Assert.That(Static<Vector2>(profile, "ReferenceResolution"),
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(Static<Rect>(profile, "HeaderRect"),
                Is.EqualTo(new Rect(0f, 968f, 1920f, 112f)));
            Assert.That(Static<Rect>(profile, "TreeRect"),
                Is.EqualTo(new Rect(0f, 216f, 1920f, 752f)));
            Assert.That(Static<Rect>(profile, "FooterRect"),
                Is.EqualTo(new Rect(0f, 0f, 1920f, 216f)));
            Assert.That(Static<Vector2>(profile, "CompactNodeSize"),
                Is.EqualTo(new Vector2(156f, 74f)));

            float[] lanes = Static<float[]>(profile, "RouteLaneCenters");
            float[] subcolumns = Static<float[]>(profile, "SubcolumnOffsets");
            float[] gutters = Static<float[]>(profile, "BridgeGutterCenters");
            Assert.That(lanes, Has.Length.EqualTo(4));
            Assert.That(lanes, Is.Ordered.Ascending);
            Assert.That(subcolumns, Is.EqualTo(new[] { -86f, 86f }));
            Assert.That(gutters, Has.Length.EqualTo(3));
            for (var index = 0; index < gutters.Length; index++)
                Assert.That(gutters[index],
                    Is.EqualTo((lanes[index] + lanes[index + 1]) * .5f));
        }

        [Test]
        public void IDEA0024_ProjectionKeepsTruthAndUsesTwoColumnRouteLanes()
        {
            ResearchTreeProjection3D projection =
                ResearchTreeProjection3D.Create(ResearchCatalog.All);
            Assert.That(projection.Nodes, Has.Count.EqualTo(44));
            Assert.That(projection.Edges, Has.Count.EqualTo(49));
            ResearchTreeNodeProjection3D[] bridges = projection.Nodes
                .Where(value => value.Definition.Route ==
                    DevelopmentRoute.Bridge)
                .ToArray();
            Assert.That(bridges, Has.Length.EqualTo(6));
            foreach (ResearchTreeNodeProjection3D bridge in bridges)
            {
                Assert.That(bridge.Definition.RequiredResearchIds,
                    Has.Count.EqualTo(2), bridge.ResearchId);
                Assert.That(projection.Edges.Count(edge => string.Equals(
                        edge.DependentResearchId,
                        bridge.ResearchId,
                        StringComparison.Ordinal)),
                    Is.EqualTo(2), bridge.ResearchId);
            }

            DevelopmentRoute[] routes =
            {
                DevelopmentRoute.Technology,
                DevelopmentRoute.Cultivation,
                DevelopmentRoute.BiologicalAscension,
                DevelopmentRoute.Psionics,
            };
            float[] lanes = Static<float[]>(
                Type.GetType(ProfileTypeName),
                "RouteLaneCenters");
            float[] offsets = Static<float[]>(
                Type.GetType(ProfileTypeName),
                "SubcolumnOffsets");
            for (var routeIndex = 0; routeIndex < routes.Length; routeIndex++)
            {
                float[] allowed = offsets.Select(value =>
                    lanes[routeIndex] + value).ToArray();
                foreach (ResearchTreeNodeProjection3D node in projection.Nodes
                             .Where(value => value.Definition.Route ==
                                 routes[routeIndex] &&
                                 value.Definition.LayoutRow >= 2))
                {
                    Assert.That(allowed.Any(value => Mathf.Approximately(
                            value, node.Position.x)), Is.True,
                        node.ResearchId + " must use one of two subcolumns.");
                }
            }

            Assert.That(bridges.Select(value => value.Position.y).Distinct()
                .Count(), Is.GreaterThan(1),
                "Bridge nodes must occupy route gutters at multiple levels, " +
                "not one top row.");
        }

        [Test]
        public void IDEA0024_ViewBuildsReferenceHeaderRoutesNodesAndFooter()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);
            Transform panel = Required(root, "ResearchTreePanel");
            Required(panel, "Research.Header");
            Required(panel, "Research.Viewport");
            Required(panel, "Research.Footer");
            Required(panel, "Research.Background");
            Required(panel, "Research.Search");

            foreach (string route in new[]
                     {
                         "All", "Technology", "Cultivation", "Biological",
                         "Psionics",
                     })
                Assert.That(Required(
                        panel,
                        "Research.Filter.Route." + route)
                    .GetComponent<Button>(), Is.Not.Null, route);
            foreach (string state in new[]
                     {
                         "All", "Researchable", "Active", "Completed",
                         "Locked",
                     })
                Assert.That(Required(
                        panel,
                        "Research.Filter.Status." + state)
                    .GetComponent<Button>(), Is.Not.Null, state);
            Assert.That(Required(panel, "Research.FocusCurrent")
                .GetComponent<Button>(), Is.Not.Null);
            Assert.That(Required(panel, "Research.FocusLatest")
                .GetComponent<Button>(), Is.Not.Null);

            foreach (string route in new[]
                     {
                         "Technology", "Cultivation", "Biological", "Psionics",
                     })
                Required(panel, "Research.RouteHeader." + route);

            Transform nodes = Required(panel, "Research.Nodes");
            Assert.That(nodes.GetComponentsInChildren<Button>(true)
                .Count(value => value.name.StartsWith(
                    "Research.Node.", StringComparison.Ordinal)),
                Is.EqualTo(44));
            Vector2 compact = Static<Vector2>(
                Type.GetType(ProfileTypeName),
                "CompactNodeSize");
            foreach (ResearchDefinition definition in ResearchCatalog.All)
                Assert.That(((RectTransform)Required(
                        nodes,
                        "Research.Node." + definition.Id.Value)).sizeDelta,
                    Is.EqualTo(compact), definition.Id.Value);
        }

        [Test]
        public void IDEA0024_FooterUsesSelectedTechnologyAndEveryMaterialTruth()
        {
            GrayboxOperationsView3D view = CreateView(out Transform root);
            ResearchDefinition definition = ResearchCatalog.All
                .OrderByDescending(value => value.Costs.Count)
                .ThenBy(value => value.CatalogOrder)
                .First();
            object presentation = CreatePresentation(
                definition,
                "Researchable",
                "可研究",
                true);
            InvokeSetNode(view, presentation);

            Required(root, "Research.Node." + definition.Id.Value)
                .GetComponent<Button>().onClick.Invoke();
            Transform footer = Required(root, "Research.Footer");
            Image icon = Required(footer, "Research.Detail.Icon")
                .GetComponent<Image>();
            Assert.That(icon.sprite,
                Is.SameAs(ResearchIconCatalog3D.Resolve(definition.Id.Value)));
            Assert.That(Required(footer, "Research.Detail.Name")
                    .GetComponent<Text>().text,
                Is.EqualTo(definition.Name));
            Assert.That(Required(footer, "Research.Detail.Duration")
                    .GetComponent<Text>().text,
                Does.Contain(definition.Duration.ToString("0.##")));
            Required(footer, "Research.Detail.Prerequisites");
            Required(footer, "Research.Detail.Description");
            Required(footer, "Research.StatusLegend");
            Assert.That(Required(footer, "Research.Start")
                .GetComponent<Button>(), Is.Not.Null);
            Assert.That(Required(footer, "Research.Cancel")
                .GetComponent<Button>(), Is.Not.Null);

            foreach (var cost in definition.Costs)
            {
                Transform row = Required(
                    footer,
                    "Research.Detail.Cost." + cost.ResourceId);
                Assert.That(Required(row, row.name + ".Icon")
                    .GetComponent<Image>().sprite, Is.Not.Null,
                    cost.ResourceId);
                Assert.That(Required(row, row.name + ".Amount")
                        .GetComponent<Text>().text,
                    Is.EqualTo(cost.Amount.ToString()), cost.ResourceId);
            }
        }

        [Test]
        public void IDEA0024_StateIsStructuredAndViewDoesNotParseChineseLabels()
        {
            Type state = Type.GetType(PresentationStateTypeName);
            Type presentation = Type.GetType(PresentationTypeName);
            Assert.That(state, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(Enum.GetNames(state), Is.EqualTo(new[]
            {
                "Locked", "Researchable", "Active", "Completed",
            }));
            MethodInfo setNode = typeof(GrayboxResearchTreeView3D).GetMethod(
                "SetNode",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { presentation },
                null);
            Assert.That(setNode, Is.Not.Null,
                "View must consume one structured presentation DTO.");

            string source = File.ReadAllText(Path.Combine(
                ProjectRoot(),
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxResearchTreeView3D.cs"));
            foreach (string forbidden in new[]
                     {
                         "Contains(\"可研究\")",
                         "Contains(\"研究中\")",
                         "Contains(\"已完成\")",
                         "Contains(\"锁定\")",
                     })
                Assert.That(source, Does.Not.Contain(forbidden));
        }

        [Test]
        public void IDEA0024_BackgroundSpriteIsActuallyBoundToTheTree()
        {
            CreateView(out Transform root);
            Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(
                DeliveryPath);
            Assert.That(expected, Is.Not.Null, DeliveryPath);
            Image background = Required(root, "Research.Background")
                .GetComponent<Image>();
            Assert.That(background.sprite, Is.SameAs(expected));
            Assert.That(background.preserveAspect, Is.False);
            Assert.That(background.raycastTarget, Is.False);
        }

        [Test]
        public void IDEA0024_ConnectionsExposeDoubleLayerDashArrowAndNoRaycast()
        {
            var owner = new GameObject(
                "IDEA0024.Connection",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ResearchTreeConnectionGraphic3D));
            cleanup.Add(owner);
            ResearchTreeConnectionGraphic3D graphic =
                owner.GetComponent<ResearchTreeConnectionGraphic3D>();
            graphic.ConfigurePath(
                new[]
                {
                    Vector2.zero,
                    new Vector2(0f, 100f),
                    new Vector2(120f, 100f),
                    new Vector2(120f, 200f),
                },
                Color.cyan,
                Color.yellow,
                5f,
                true,
                true);

            Assert.That(BoolProperty(graphic, "HasOuterStroke"), Is.True);
            Assert.That(BoolProperty(graphic, "IsDashed"), Is.True);
            Assert.That(BoolProperty(graphic, "HasArrowCap"), Is.True);
            Assert.That(graphic.raycastTarget, Is.False);
            MethodInfo populate = typeof(ResearchTreeConnectionGraphic3D)
                .GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
            using (var mesh = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThan(32),
                    "Outer and inner strokes must both contribute geometry.");
            }
        }

        private GrayboxOperationsView3D CreateView(out Transform root)
        {
            var canvasObject = new GameObject(
                "IDEA0024.Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            cleanup.Add(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GrayboxOperationsView3D view =
                canvasObject.AddComponent<GrayboxOperationsView3D>();
            view.Configure(canvas);
            view.SetResearchOpen(true);
            Canvas.ForceUpdateCanvases();
            root = canvasObject.transform;
            return view;
        }

        private static object CreatePresentation(
            ResearchDefinition definition,
            string stateName,
            string statusText,
            bool selected)
        {
            Type state = Type.GetType(PresentationStateTypeName);
            Type type = Type.GetType(PresentationTypeName);
            Assert.That(state, Is.Not.Null);
            Assert.That(type, Is.Not.Null);
            object stateValue = Enum.Parse(state, stateName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(ResearchDefinition), state, typeof(string), typeof(bool),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new[]
            {
                definition, stateValue, statusText, (object)selected,
            });
        }

        private static void InvokeSetNode(
            GrayboxOperationsView3D view,
            object presentation)
        {
            FieldInfo field = typeof(GrayboxOperationsView3D).GetField(
                "researchTreeView",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var tree = (GrayboxResearchTreeView3D)field.GetValue(view);
            MethodInfo method = tree.GetType().GetMethod(
                "SetNode",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { presentation.GetType() },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(tree, new[] { presentation });
        }

        private static bool BoolProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return (bool)property.GetValue(owner);
        }

        private static object Property(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(owner);
        }

        private static T Static<T>(Type owner, string name)
        {
            Assert.That(owner, Is.Not.Null, name);
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(null);
        }

        private static Transform Required(Transform root, string name)
        {
            Transform result = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => string.Equals(
                    value.name,
                    name,
                    StringComparison.Ordinal));
            Assert.That(result, Is.Not.Null, "Missing UI: " + name);
            return result;
        }

        private static ResearchBackgroundManifest LoadManifest()
        {
            Assert.That(File.Exists(Path.Combine(ProjectRoot(), ManifestPath)),
                Is.True, ManifestPath);
            ResearchBackgroundManifest manifest = JsonUtility.FromJson<
                ResearchBackgroundManifest>(File.ReadAllText(Path.Combine(
                    ProjectRoot(), ManifestPath)));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.entries, Is.Not.Null);
            return manifest;
        }

        private static void AssertFullHdPng(string path, string label)
        {
            string absolute = Path.Combine(ProjectRoot(), path);
            Assert.That(File.Exists(absolute), Is.True, path);
            byte[] bytes = File.ReadAllBytes(absolute);
            Assert.That(bytes.Length, Is.GreaterThan(25), label);
            Assert.That(bytes[25], Is.EqualTo(6), label + " must be RGBA PNG.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(bytes, false), Is.True, label);
                Assert.That(texture.width, Is.EqualTo(1920), label);
                Assert.That(texture.height, Is.EqualTo(1080), label);
                Color32[] pixels = texture.GetPixels32();
                Assert.That(pixels.Any(value => value.a >= 128), Is.True,
                    label + " must contain visible original artwork.");
                Assert.That(pixels.All(value => value.a == 255), Is.True,
                    label + " is a full-screen opaque background.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName
                .Replace('\\', '/');
        }

        private static string Sha256(string projectPath)
        {
            using SHA256 sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(File.ReadAllBytes(
                Path.Combine(ProjectRoot(), projectPath))).Select(
                    value => value.ToString("x2")));
        }

        [Serializable]
        private sealed class ResearchBackgroundManifest
        {
            public string requirementId;
            public string unityVersion;
            public BackgroundEntry[] entries;
        }

        [Serializable]
        private sealed class BackgroundEntry
        {
            public string contentId;
            public string displayNameZh;
            public string visualClass;
            public string sourceAssetPath;
            public string unitySpritePath;
            public int[] masterSizePx;
            public int[] deliverySizePx;
            public string unityGuid;
            public string sourceSha256;
            public string deliverySha256;
            public string loreBriefZh;
            public string useSummaryZh;
            public string forbiddenElementsZh;
            public string promptSummaryZh;
            public string reviewState;
        }
    }
}
