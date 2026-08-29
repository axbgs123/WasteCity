using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Usability;
using WasteCity.Research;

namespace WasteCity.Tests
{
    /// <summary>
    /// IDEA-0016 pure graph/layout contract for the formal bottom-up research
    /// tree. The projection must not depend on a scene, Canvas or input device.
    /// </summary>
    public sealed class ResearchTreeProjection3DTests
    {
        private const string RootId =
            "core.research.scrap-processing";
        private const string TechnologyT1Id =
            "core.research.automated-machinery";
        private const string CultivationT1Id =
            "core.research.spirit-sensing";
        private const string BiologicalT1Id =
            "core.research.adaptive-tissue";
        private const string PsionicsT1Id =
            "core.research.mind-resonance";
        private const string PrecisionAssemblyId =
            "core.research.precision-assembly";
        private const string AutomatedDefenseId =
            "core.research.automated-defense";
        private const string LegacyAnalysisId =
            "core.research.legacy-analysis";

        private static readonly float[] RowY =
            ResearchTreeVisualLayoutProfile3D.RowCenters;

        private static readonly Dictionary<DevelopmentRoute, float>
            RouteCenterX = new Dictionary<DevelopmentRoute, float>
        {
            { DevelopmentRoute.Technology,
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters[0] },
            { DevelopmentRoute.Cultivation,
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters[1] },
            { DevelopmentRoute.Biological,
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters[2] },
            { DevelopmentRoute.Psionics,
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters[3] },
        };

        private static readonly float[] BranchOffsets =
            ResearchTreeVisualLayoutProfile3D.SubcolumnOffsets;
        private static readonly float[] BridgeX =
            ResearchTreeVisualLayoutProfile3D.BridgeGutterCenters;

        [Test]
        public void FormalCatalog_ProjectsAllNodesAndAllPrerequisiteEdgesUpward()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            int expectedEdgeCount = ResearchCatalog.All.Sum(
                definition => definition.RequiredResearchIds.Count);

            Assert.That(projection.Nodes, Has.Count.EqualTo(44));
            Assert.That(projection.Edges,
                Has.Count.EqualTo(expectedEdgeCount));
            Assert.That(expectedEdgeCount, Is.EqualTo(49),
                "IDEA-0020 adds the automatic-defense to legacy-analysis edge.");

            var actualEdges = new HashSet<string>(StringComparer.Ordinal);
            foreach (ResearchTreeEdgeProjection3D edge in projection.Edges)
            {
                ResearchTreeNodeProjection3D prerequisite =
                    projection.FindNode(edge.PrerequisiteResearchId);
                ResearchTreeNodeProjection3D dependent =
                    projection.FindNode(edge.DependentResearchId);
                Assert.That(prerequisite, Is.Not.Null,
                    edge.PrerequisiteResearchId);
                Assert.That(dependent, Is.Not.Null,
                    edge.DependentResearchId);
                Assert.That(prerequisite.Position.y,
                    Is.LessThan(dependent.Position.y),
                    edge.PrerequisiteResearchId + " -> " +
                    edge.DependentResearchId);
                Assert.That(actualEdges.Add(
                        edge.PrerequisiteResearchId + "\n" +
                        edge.DependentResearchId),
                    Is.True,
                    "Projected prerequisite edges must be unique.");
            }

            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                foreach (string prerequisiteId in
                         definition.RequiredResearchIds)
                {
                    Assert.That(actualEdges, Does.Contain(
                        prerequisiteId + "\n" + definition.Id.Value));
                }
            }
        }

        [Test]
        public void Layout_IsBottomUpAndUsesApprovedRouteColumnsWithoutOverlap()
        {
            ResearchTreeProjection3D projection = CreateProjection();

            Assert.That(projection.FindNode(RootId).Position,
                Is.EqualTo(new Vector2(0f, RowY[0])));
            Assert.That(projection.FindNode(TechnologyT1Id).Position.x,
                Is.EqualTo(RouteCenterX[DevelopmentRoute.Technology]));
            Assert.That(projection.FindNode(CultivationT1Id).Position.x,
                Is.EqualTo(RouteCenterX[DevelopmentRoute.Cultivation]));
            Assert.That(projection.FindNode(BiologicalT1Id).Position.x,
                Is.EqualTo(RouteCenterX[DevelopmentRoute.Biological]));
            Assert.That(projection.FindNode(PsionicsT1Id).Position.x,
                Is.EqualTo(RouteCenterX[DevelopmentRoute.Psionics]));

            foreach (DevelopmentRoute route in RouteCenterX.Keys)
            {
                for (var row = 2; row <= 3; row++)
                {
                    ResearchTreeNodeProjection3D[] nodes = projection.Nodes
                        .Where(node => node.Definition.Route == route &&
                            node.Definition.LayoutRow == row)
                        .OrderBy(node => node.Definition.CatalogOrder)
                        .ToArray();
                    Assert.That(nodes, Has.Length.EqualTo(4),
                        route + " row " + row);
                    for (var index = 0; index < nodes.Length; index++)
                    {
                        Assert.That(nodes[index].Position.x,
                            Is.EqualTo(RouteCenterX[route] +
                                BranchOffsets[index % BranchOffsets.Length]),
                            nodes[index].ResearchId);
                        Assert.That(nodes[index].Position.y,
                            Is.EqualTo(RowY[row] +
                                index / BranchOffsets.Length *
                                ResearchTreeVisualLayoutProfile3D
                                    .NodeSublaneStep),
                            nodes[index].ResearchId);
                    }
                }
            }

            ResearchTreeNodeProjection3D[] bridges = projection.Nodes
                .Where(node => node.Definition.Route ==
                    DevelopmentRoute.Bridge)
                .OrderBy(node => node.Definition.CatalogOrder)
                .ToArray();
            Assert.That(bridges,
                Has.Length.EqualTo(BridgeX.Length * 2));
            for (var index = 0; index < bridges.Length; index++)
            {
                ResearchDefinition bridge = bridges[index].Definition;
                float[] prerequisiteCenters = bridge.RequiredResearchIds
                    .Select(ResearchCatalog.Find)
                    .Where(value => value != null &&
                        RouteCenterX.ContainsKey(value.Route))
                    .Select(value => RouteCenterX[value.Route])
                    .Distinct()
                    .ToArray();
                Assert.That(prerequisiteCenters, Is.Not.Empty,
                    bridges[index].ResearchId);
                Assert.That(bridges[index].Position.x,
                    Is.EqualTo(prerequisiteCenters.Average()).Within(.001f),
                    bridges[index].ResearchId +
                    " must sit at the actual prerequisite convergence.");
                Assert.That(bridges[index].Position.y,
                    Is.GreaterThanOrEqualTo(RowY[4]),
                    bridges[index].ResearchId);
            }
            Assert.That(bridges.Select(value => value.Position).Distinct()
                .Count(), Is.EqualTo(bridges.Length));

            foreach (IGrouping<float, ResearchTreeNodeProjection3D> row in
                     projection.Nodes.GroupBy(node => node.Position.y))
            {
                ResearchTreeNodeProjection3D[] ordered = row
                    .OrderBy(node => node.Position.x)
                    .ToArray();
                for (var index = 1; index < ordered.Length; index++)
                {
                    Assert.That(
                        ordered[index].Position.x -
                        ordered[index - 1].Position.x,
                        Is.GreaterThanOrEqualTo(
                            ResearchTreeProjection3D.NodeSize.x),
                        ordered[index - 1].ResearchId + " overlaps " +
                        ordered[index].ResearchId);
                }
            }
        }

        [Test]
        public void Projection_IsStableWhenCatalogInputOrderChanges()
        {
            ResearchTreeProjection3D first = CreateProjection();
            ResearchTreeProjection3D second =
                ResearchTreeProjection3D.Create(
                    ResearchCatalog.All.Reverse().ToArray());

            CollectionAssert.AreEqual(
                first.Nodes.Select(NodeSignature).ToArray(),
                second.Nodes.Select(NodeSignature).ToArray());
            CollectionAssert.AreEqual(
                first.Edges.Select(EdgeSignature).ToArray(),
                second.Edges.Select(EdgeSignature).ToArray());
            Assert.That(second.Bounds, Is.EqualTo(first.Bounds));
        }

        [Test]
        public void BridgeNodes_ProjectBothFormalPrerequisites()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            ResearchDefinition[] bridges = ResearchCatalog.All
                .Where(definition => definition.Route ==
                    DevelopmentRoute.Bridge)
                .ToArray();

            Assert.That(bridges, Has.Length.EqualTo(6));
            foreach (ResearchDefinition bridge in bridges)
            {
                Assert.That(bridge.RequiredResearchIds,
                    Has.Count.EqualTo(2), bridge.Id.Value);
                string[] projectedPrerequisites = projection.Edges
                    .Where(edge => edge.DependentResearchId ==
                        bridge.Id.Value)
                    .Select(edge => edge.PrerequisiteResearchId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                CollectionAssert.AreEqual(
                    bridge.RequiredResearchIds
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray(),
                    projectedPrerequisites,
                    bridge.Id.Value);
            }
        }

        [Test]
        public void IDEA0021_SharedJunctionBranchesAndBridgesAreDeterministic()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            Assert.That(projection.Junctions, Is.Not.Empty);
            Assert.That(projection.Trunks, Has.Count.EqualTo(
                projection.Junctions.Count));
            Assert.That(projection.Trunks.Select(value => value.StableId),
                Is.Unique);
            foreach (ResearchTreeEdgeProjection3D edge in projection.Edges)
            {
                Assert.That(edge.Points.Count, Is.GreaterThanOrEqualTo(2));
                for (var index = 1; index < edge.Points.Count; index++)
                {
                    Assert.That(edge.Points[index].y,
                        Is.GreaterThanOrEqualTo(edge.Points[index - 1].y),
                        EdgeSignature(edge));
                }
                if (!string.IsNullOrEmpty(edge.JunctionId))
                {
                    ResearchTreeJunctionProjection3D junction =
                        projection.Junctions.Single(value =>
                            value.StableId == edge.JunctionId);
                    Assert.That(edge.Points, Does.Contain(junction.Position));
                }
            }
            ResearchTreeEdgeProjection3D[] bridges = projection.Edges
                .Where(value => value.IsBridge)
                .ToArray();
            Assert.That(bridges, Has.Length.EqualTo(12));
            Assert.That(bridges.All(value =>
                value.EndRoute == DevelopmentRoute.Bridge), Is.True);

            ResearchTreeProjection3D reversed =
                ResearchTreeProjection3D.Create(
                    ResearchCatalog.All.Reverse().ToArray());
            Assert.That(reversed.Junctions.Select(value =>
                    value.StableId + "|" + value.Position),
                Is.EqualTo(projection.Junctions.Select(value =>
                    value.StableId + "|" + value.Position)));
            Assert.That(reversed.Edges.Select(GeometrySignature),
                Is.EqualTo(projection.Edges.Select(GeometrySignature)));
            Assert.That(reversed.Trunks.Select(value =>
                    value.StableId + "|" + string.Join(";", value.Points)),
                Is.EqualTo(projection.Trunks.Select(value =>
                    value.StableId + "|" + string.Join(";", value.Points))));
        }

        [Test]
        public void LatestResearchable_UsesHighestLayoutRowThenCatalogOrder()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            ResearchTreeNodeProjection3D selected =
                projection.SelectLatestResearchable(new[]
                {
                    AutomatedDefenseId,
                    TechnologyT1Id,
                    PrecisionAssemblyId,
                });

            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.ResearchId,
                Is.EqualTo(PrecisionAssemblyId),
                "Both T2 candidates are current; the lower CatalogOrder " +
                "must be the deterministic main focus.");

            ResearchTreeNodeProjection3D legacy =
                projection.SelectLatestResearchable(new[]
                {
                    AutomatedDefenseId,
                    PrecisionAssemblyId,
                    LegacyAnalysisId,
                });
            Assert.That(legacy.ResearchId, Is.EqualTo(LegacyAnalysisId));
            var viewport = new Vector2(1476f, 644f);
            ResearchTreeViewportState3D focused = projection.Focus(
                new[] { legacy.ResearchId }, viewport, 28f);
            AssertNodesInsideViewport(
                new[] { legacy }, focused, viewport, 28f);
            Assert.That(projection.SelectLatestResearchable(
                Array.Empty<string>()), Is.Null);
        }

        [Test]
        public void Filtering_HidesNodesWithoutChangingGraphCoordinatesOrBounds()
        {
            ResearchTreeProjection3D original = CreateProjection();
            ResearchTreeProjection3D filtered =
                original.WithVisibleResearchIds(new[]
                {
                    RootId,
                    TechnologyT1Id,
                    PrecisionAssemblyId,
                });

            Assert.That(filtered.Nodes, Has.Count.EqualTo(44));
            Assert.That(filtered.Bounds, Is.EqualTo(original.Bounds));
            foreach (ResearchTreeNodeProjection3D originalNode in
                     original.Nodes)
            {
                ResearchTreeNodeProjection3D filteredNode =
                    filtered.FindNode(originalNode.ResearchId);
                Assert.That(filteredNode.Position,
                    Is.EqualTo(originalNode.Position),
                    originalNode.ResearchId);
            }
            CollectionAssert.AreEquivalent(
                new[] { RootId, TechnologyT1Id, PrecisionAssemblyId },
                filtered.Nodes.Where(node => node.Visible)
                    .Select(node => node.ResearchId)
                    .ToArray());
        }

        [Test]
        public void Zoom_ClampsAndKeepsThePointerGraphAnchorFixed()
        {
            Assert.That(ResearchTreeProjection3D.ClampZoom(-10f),
                Is.EqualTo(.34f));
            Assert.That(ResearchTreeProjection3D.ClampZoom(.9f),
                Is.EqualTo(.9f));
            Assert.That(ResearchTreeProjection3D.ClampZoom(10f),
                Is.EqualTo(1.45f));

            var viewportSize = new Vector2(1000f, 600f);
            var pointer = new Vector2(730f, 210f);
            var before = new ResearchTreeViewportState3D(
                new Vector2(120f, 310f),
                .8f);
            Vector2 anchorBefore = ResearchTreeProjection3D.ScreenToGraph(
                pointer,
                viewportSize,
                before);
            ResearchTreeViewportState3D after =
                ResearchTreeProjection3D.ZoomAroundPointer(
                    before,
                    requestedZoom: 1.3f,
                    pointer,
                    viewportSize);
            Vector2 anchorAfter = ResearchTreeProjection3D.ScreenToGraph(
                pointer,
                viewportSize,
                after);

            Assert.That(after.Zoom, Is.EqualTo(1.3f));
            Assert.That(anchorAfter.x,
                Is.EqualTo(anchorBefore.x).Within(.0001f));
            Assert.That(anchorAfter.y,
                Is.EqualTo(anchorBefore.y).Within(.0001f));
        }

        [Test]
        public void FitAndFocus_AreStableAndContainTheirRequestedBounds()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            var viewportSize = new Vector2(2000f, 1200f);
            const float padding = 48f;

            ResearchTreeViewportState3D firstFit = projection.FitAll(
                viewportSize,
                padding);
            ResearchTreeViewportState3D secondFit = projection.FitAll(
                viewportSize,
                padding);
            Assert.That(secondFit, Is.EqualTo(firstFit));
            AssertNodesInsideViewport(
                projection.Nodes,
                firstFit,
                viewportSize,
                padding);

            string[] focusIds =
            {
                TechnologyT1Id,
                PrecisionAssemblyId,
                AutomatedDefenseId,
            };
            ResearchTreeViewportState3D firstFocus = projection.Focus(
                focusIds,
                viewportSize,
                padding);
            ResearchTreeViewportState3D secondFocus = projection.Focus(
                focusIds.Reverse().ToArray(),
                viewportSize,
                padding);
            Assert.That(secondFocus, Is.EqualTo(firstFocus),
                "Focus must not depend on caller enumeration order.");
            AssertNodesInsideViewport(
                focusIds.Select(projection.FindNode),
                firstFocus,
                viewportSize,
                padding);
        }

        [Test]
        public void FitAll_ContainsEveryNodeAtDefaultResearchViewportSize()
        {
            ResearchTreeProjection3D projection = CreateProjection();
            var viewportSize = new Vector2(1476f, 644f);
            const float padding = 28f;

            ResearchTreeViewportState3D state = projection.FitAll(
                viewportSize,
                padding);

            AssertNodesInsideViewport(
                projection.Nodes,
                state,
                viewportSize,
                padding);
        }

        private static ResearchTreeProjection3D CreateProjection()
        {
            return ResearchTreeProjection3D.Create(ResearchCatalog.All);
        }

        private static string NodeSignature(
            ResearchTreeNodeProjection3D node)
        {
            return node.ResearchId + "|" + node.Position.x + "|" +
                node.Position.y + "|" + node.Visible;
        }

        private static string EdgeSignature(
            ResearchTreeEdgeProjection3D edge)
        {
            return edge.PrerequisiteResearchId + "|" +
                edge.DependentResearchId;
        }

        private static string GeometrySignature(
            ResearchTreeEdgeProjection3D edge)
        {
            return EdgeSignature(edge) + "|" + edge.JunctionId + "|" +
                edge.IsBridge + "|" + string.Join(";", edge.Points.Select(
                    value => value.x + "," + value.y));
        }

        private static void AssertNodesInsideViewport(
            IEnumerable<ResearchTreeNodeProjection3D> nodes,
            ResearchTreeViewportState3D state,
            Vector2 viewportSize,
            float padding)
        {
            Vector2 halfNode = ResearchTreeProjection3D.NodeSize *
                (.5f * state.Zoom);
            foreach (ResearchTreeNodeProjection3D node in nodes)
            {
                Vector2 screen = ResearchTreeProjection3D.GraphToScreen(
                    node.Position,
                    viewportSize,
                    state);
                Assert.That(screen.x - halfNode.x,
                    Is.GreaterThanOrEqualTo(padding - .01f),
                    node.ResearchId + " left");
                Assert.That(screen.x + halfNode.x,
                    Is.LessThanOrEqualTo(viewportSize.x - padding + .01f),
                    node.ResearchId + " right");
                Assert.That(screen.y - halfNode.y,
                    Is.GreaterThanOrEqualTo(padding - .01f),
                    node.ResearchId + " bottom");
                Assert.That(screen.y + halfNode.y,
                    Is.LessThanOrEqualTo(viewportSize.y - padding + .01f),
                    node.ResearchId + " top");
            }
        }
    }
}
