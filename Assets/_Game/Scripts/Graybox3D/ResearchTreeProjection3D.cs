using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Usability
{
    /// <summary>
    /// Immutable IDEA-0016 graph-space projection for the formal research
    /// catalog. It owns no scene, input, research or inventory state.
    /// </summary>
    public sealed class ResearchTreeProjection3D
    {
        public static readonly Vector2 NodeSize =
            ResearchTreeVisualLayoutProfile3D.CompactNodeSize;

        private readonly ReadOnlyCollection<ResearchTreeNodeProjection3D>
            nodes;
        private readonly ReadOnlyCollection<ResearchTreeEdgeProjection3D>
            edges;
        private readonly ReadOnlyCollection<ResearchTreeJunctionProjection3D>
            junctions;
        private readonly ReadOnlyCollection<ResearchTreeTrunkProjection3D>
            trunks;
        private readonly Dictionary<string, ResearchTreeNodeProjection3D>
            nodesById;

        private ResearchTreeProjection3D(
            IList<ResearchTreeNodeProjection3D> nodes,
            IList<ResearchTreeEdgeProjection3D> edges,
            IList<ResearchTreeJunctionProjection3D> junctions,
            IList<ResearchTreeTrunkProjection3D> trunks,
            Rect bounds)
        {
            this.nodes = new ReadOnlyCollection<
                ResearchTreeNodeProjection3D>(
                new List<ResearchTreeNodeProjection3D>(nodes));
            this.edges = new ReadOnlyCollection<
                ResearchTreeEdgeProjection3D>(
                new List<ResearchTreeEdgeProjection3D>(edges));
            this.junctions = new ReadOnlyCollection<
                ResearchTreeJunctionProjection3D>(
                new List<ResearchTreeJunctionProjection3D>(junctions));
            this.trunks = new ReadOnlyCollection<
                ResearchTreeTrunkProjection3D>(
                new List<ResearchTreeTrunkProjection3D>(trunks));
            Bounds = bounds;
            nodesById = new Dictionary<
                string,
                ResearchTreeNodeProjection3D>(StringComparer.Ordinal);
            for (var index = 0; index < this.nodes.Count; index++)
            {
                ResearchTreeNodeProjection3D node = this.nodes[index];
                nodesById.Add(node.ResearchId, node);
            }
        }

        public IReadOnlyList<ResearchTreeNodeProjection3D> Nodes => nodes;
        public IReadOnlyList<ResearchTreeEdgeProjection3D> Edges => edges;
        public IReadOnlyList<ResearchTreeJunctionProjection3D> Junctions =>
            junctions;
        public IReadOnlyList<ResearchTreeTrunkProjection3D> Trunks => trunks;
        public Rect Bounds { get; }

        public static ResearchTreeProjection3D Create(
            IReadOnlyList<ResearchDefinition> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            var ordered = new List<ResearchDefinition>(definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                    ordered.Add(definitions[index]);
            }
            ordered.Sort(CompareDefinitions);

            var resultNodes = new List<ResearchTreeNodeProjection3D>(
                ordered.Count);
            for (var index = 0; index < ordered.Count; index++)
            {
                ResearchDefinition definition = ordered[index];
                resultNodes.Add(new ResearchTreeNodeProjection3D(
                    definition,
                    PositionFor(ordered, definition),
                    visible: true));
            }

            var projectedById = new Dictionary<string,
                ResearchTreeNodeProjection3D>(StringComparer.Ordinal);
            for (var index = 0; index < resultNodes.Count; index++)
                projectedById.Add(resultNodes[index].ResearchId,
                    resultNodes[index]);
            var outgoingCounts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (var index = 0; index < ordered.Count; index++)
            for (var requirementIndex = 0;
                 requirementIndex < ordered[index].RequiredResearchIds.Count;
                 requirementIndex++)
            {
                string prerequisite =
                    ordered[index].RequiredResearchIds[requirementIndex];
                outgoingCounts.TryGetValue(prerequisite, out int count);
                outgoingCounts[prerequisite] = count + 1;
            }

            var resultEdges = new List<ResearchTreeEdgeProjection3D>();
            var resultJunctions = new List<ResearchTreeJunctionProjection3D>();
            var resultTrunks = new List<ResearchTreeTrunkProjection3D>();
            var createdJunctions = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ordered.Count; index++)
            {
                ResearchDefinition dependent = ordered[index];
                for (var requirementIndex = 0;
                     requirementIndex <
                         dependent.RequiredResearchIds.Count;
                     requirementIndex++)
                {
                    string prerequisiteId =
                        dependent.RequiredResearchIds[requirementIndex];
                    ResearchTreeNodeProjection3D prerequisite =
                        projectedById[prerequisiteId];
                    ResearchTreeNodeProjection3D child =
                        projectedById[dependent.Id.Value];
                    bool shared = outgoingCounts[prerequisiteId] > 1;
                    string junctionId = shared
                        ? "junction:" + prerequisiteId
                        : string.Empty;
                    Vector2[] points = ConnectionPoints(
                        prerequisite.Position, child.Position, shared);
                    bool bridge = dependent.Route == DevelopmentRoute.Bridge ||
                        prerequisite.Definition.Route != dependent.Route &&
                        prerequisite.Definition.Route != DevelopmentRoute.Common;
                    Vector2[] branchPoints = shared
                        ? Slice(points, 1)
                        : points;
                    resultEdges.Add(new ResearchTreeEdgeProjection3D(
                        prerequisiteId,
                        dependent.Id.Value,
                        branchPoints,
                        junctionId,
                        bridge,
                        prerequisite.Definition.Route,
                        dependent.Route));
                    if (shared && createdJunctions.Add(junctionId))
                    {
                        resultJunctions.Add(
                            new ResearchTreeJunctionProjection3D(
                                junctionId,
                                prerequisiteId,
                                points[1],
                                prerequisite.Definition.Route));
                        resultTrunks.Add(new ResearchTreeTrunkProjection3D(
                            "trunk:" + prerequisiteId,
                            prerequisiteId,
                            new[] { points[0], points[1] },
                            prerequisite.Definition.Route));
                    }
                }
            }

            return new ResearchTreeProjection3D(
                resultNodes,
                resultEdges,
                resultJunctions,
                resultTrunks,
                CalculateBounds(resultNodes));
        }

        public ResearchTreeNodeProjection3D FindNode(string researchId)
        {
            if (string.IsNullOrEmpty(researchId)) return null;
            nodesById.TryGetValue(researchId, out var node);
            return node;
        }

        public ResearchTreeProjection3D WithVisibleResearchIds(
            IEnumerable<string> researchIds)
        {
            var visibleIds = new HashSet<string>(StringComparer.Ordinal);
            if (researchIds != null)
            {
                foreach (string researchId in researchIds)
                {
                    if (!string.IsNullOrEmpty(researchId))
                        visibleIds.Add(researchId);
                }
            }

            var filteredNodes = new List<ResearchTreeNodeProjection3D>(
                nodes.Count);
            for (var index = 0; index < nodes.Count; index++)
            {
                ResearchTreeNodeProjection3D node = nodes[index];
                filteredNodes.Add(new ResearchTreeNodeProjection3D(
                    node.Definition,
                    node.Position,
                    visibleIds.Contains(node.ResearchId)));
            }
            return new ResearchTreeProjection3D(
                filteredNodes,
                edges,
                junctions,
                trunks,
                Bounds);
        }

        private static Vector2[] Slice(Vector2[] source, int start)
        {
            var result = new Vector2[source.Length - start];
            Array.Copy(source, start, result, 0, result.Length);
            return result;
        }

        private static Vector2[] ConnectionPoints(
            Vector2 prerequisite,
            Vector2 dependent,
            bool shared)
        {
            Vector2 start = prerequisite + Vector2.up * (NodeSize.y * .5f);
            Vector2 end = dependent - Vector2.up * (NodeSize.y * .5f);
            float junctionY = Mathf.Min(end.y - 24f, start.y + 96f);
            Vector2 junction = new Vector2(prerequisite.x, junctionY);
            if (!shared &&
                Mathf.Approximately(prerequisite.x, dependent.x))
                return new[] { start, end };
            return shared
                ? new[]
                {
                    start,
                    junction,
                    new Vector2(dependent.x, junctionY),
                    end,
                }
                : new[]
                {
                    start,
                    new Vector2(prerequisite.x, junctionY),
                    new Vector2(dependent.x, junctionY),
                    end,
                };
        }

        public ResearchTreeNodeProjection3D SelectLatestResearchable(
            IEnumerable<string> researchIds)
        {
            if (researchIds == null) return null;
            ResearchTreeNodeProjection3D selected = null;
            foreach (string researchId in researchIds)
            {
                ResearchTreeNodeProjection3D candidate = FindNode(researchId);
                if (candidate == null) continue;
                if (selected == null ||
                    candidate.Definition.LayoutRow >
                        selected.Definition.LayoutRow ||
                    candidate.Definition.LayoutRow ==
                        selected.Definition.LayoutRow &&
                    candidate.Definition.CatalogOrder <
                        selected.Definition.CatalogOrder)
                {
                    selected = candidate;
                }
            }
            return selected;
        }

        public ResearchTreeViewportState3D FitAll(
            Vector2 viewportSize,
            float padding)
        {
            return ViewportForBounds(Bounds, viewportSize, padding);
        }

        public ResearchTreeViewportState3D Focus(
            IEnumerable<string> researchIds,
            Vector2 viewportSize,
            float padding)
        {
            var focused = new List<ResearchTreeNodeProjection3D>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (researchIds != null)
            {
                foreach (string researchId in researchIds)
                {
                    ResearchTreeNodeProjection3D node = FindNode(researchId);
                    if (node != null && seen.Add(node.ResearchId))
                        focused.Add(node);
                }
            }
            return focused.Count == 0
                ? FitAll(viewportSize, padding)
                : ViewportForBounds(
                    CalculateBounds(focused),
                    viewportSize,
                    padding);
        }

        public static float ClampZoom(float zoom)
        {
            return Mathf.Clamp(zoom, .34f, 1.45f);
        }

        public static Vector2 GraphToScreen(
            Vector2 graphPosition,
            Vector2 viewportSize,
            ResearchTreeViewportState3D state)
        {
            return (graphPosition - state.Center) * state.Zoom +
                viewportSize * .5f;
        }

        public static Vector2 ScreenToGraph(
            Vector2 screenPosition,
            Vector2 viewportSize,
            ResearchTreeViewportState3D state)
        {
            return state.Center +
                (screenPosition - viewportSize * .5f) / state.Zoom;
        }

        public static ResearchTreeViewportState3D ZoomAroundPointer(
            ResearchTreeViewportState3D state,
            float requestedZoom,
            Vector2 pointerPosition,
            Vector2 viewportSize)
        {
            Vector2 anchor = ScreenToGraph(
                pointerPosition,
                viewportSize,
                state);
            float zoom = ClampZoom(requestedZoom);
            Vector2 center = anchor -
                (pointerPosition - viewportSize * .5f) / zoom;
            return new ResearchTreeViewportState3D(center, zoom);
        }

        private static ResearchTreeViewportState3D ViewportForBounds(
            Rect bounds,
            Vector2 viewportSize,
            float padding)
        {
            float safePadding = Mathf.Max(0f, padding);
            float availableWidth = Mathf.Max(
                1f,
                viewportSize.x - safePadding * 2f);
            float availableHeight = Mathf.Max(
                1f,
                viewportSize.y - safePadding * 2f);
            float width = Mathf.Max(1f, bounds.width);
            float height = Mathf.Max(1f, bounds.height);
            float zoom = ClampZoom(Mathf.Min(
                availableWidth / width,
                availableHeight / height));
            return new ResearchTreeViewportState3D(bounds.center, zoom);
        }

        private static Rect CalculateBounds(
            IList<ResearchTreeNodeProjection3D> source)
        {
            if (source == null || source.Count == 0)
                return new Rect(Vector2.zero, Vector2.zero);
            Vector2 half = NodeSize * .5f;
            Vector2 minimum = source[0].Position - half;
            Vector2 maximum = source[0].Position + half;
            for (var index = 1; index < source.Count; index++)
            {
                Vector2 position = source[index].Position;
                minimum = Vector2.Min(minimum, position - half);
                maximum = Vector2.Max(maximum, position + half);
            }
            return Rect.MinMaxRect(
                minimum.x,
                minimum.y,
                maximum.x,
                maximum.y);
        }

        private static Vector2 PositionFor(
            IList<ResearchDefinition> ordered,
            ResearchDefinition definition)
        {
            int row = definition.LayoutRow;
            float[] rows = ResearchTreeVisualLayoutProfile3D.RowCenters;
            float y = rows[Mathf.Clamp(row, 0, rows.Length - 1)];
            if (definition.Route == DevelopmentRoute.Common)
                return new Vector2(0f, y);
            if (definition.Route == DevelopmentRoute.Bridge)
            {
                float bridgeCenter = BridgeCenterFor(ordered, definition);
                int bridgeIndex = BridgeOrdinalAtCenter(
                    ordered, definition, bridgeCenter);
                return new Vector2(
                    bridgeCenter,
                    y + bridgeIndex *
                    ResearchTreeVisualLayoutProfile3D.BridgeLevelStep);
            }

            float center = RouteCenter(definition.Route);
            if (row <= 1) return new Vector2(center, y);
            int ordinal = OrdinalInGroup(
                ordered,
                definition,
                definition.Route,
                row);
            float[] subcolumns =
                ResearchTreeVisualLayoutProfile3D.SubcolumnOffsets;
            return new Vector2(
                center + subcolumns[ordinal % subcolumns.Length],
                y + ordinal / subcolumns.Length *
                ResearchTreeVisualLayoutProfile3D.NodeSublaneStep);
        }

        private static int OrdinalInGroup(
            IList<ResearchDefinition> ordered,
            ResearchDefinition target,
            DevelopmentRoute route,
            int row)
        {
            var ordinal = 0;
            for (var index = 0; index < ordered.Count; index++)
            {
                ResearchDefinition candidate = ordered[index];
                if (candidate.Route != route || candidate.LayoutRow != row)
                    continue;
                if (ReferenceEquals(candidate, target) ||
                    string.Equals(
                        candidate.Id.Value,
                        target.Id.Value,
                        StringComparison.Ordinal))
                {
                    return ordinal;
                }
                ordinal++;
            }
            return 0;
        }

        private static float BridgeCenterFor(
            IList<ResearchDefinition> ordered,
            ResearchDefinition bridge)
        {
            var routes = new HashSet<DevelopmentRoute>();
            float sum = 0f;
            for (var index = 0;
                 index < bridge.RequiredResearchIds.Count;
                 index++)
            {
                ResearchDefinition prerequisite = FindDefinition(
                    ordered,
                    bridge.RequiredResearchIds[index]);
                if (prerequisite == null ||
                    prerequisite.Route == DevelopmentRoute.Common ||
                    prerequisite.Route == DevelopmentRoute.Bridge ||
                    !routes.Add(prerequisite.Route))
                {
                    continue;
                }
                sum += RouteCenter(prerequisite.Route);
            }
            return routes.Count == 0 ? 0f : sum / routes.Count;
        }

        private static int BridgeOrdinalAtCenter(
            IList<ResearchDefinition> ordered,
            ResearchDefinition target,
            float center)
        {
            var ordinal = 0;
            for (var index = 0; index < ordered.Count; index++)
            {
                ResearchDefinition candidate = ordered[index];
                if (candidate.Route != DevelopmentRoute.Bridge ||
                    candidate.LayoutRow != target.LayoutRow ||
                    !Mathf.Approximately(
                        BridgeCenterFor(ordered, candidate),
                        center))
                {
                    continue;
                }
                if (string.Equals(
                        candidate.Id.Value,
                        target.Id.Value,
                        StringComparison.Ordinal))
                {
                    return ordinal;
                }
                ordinal++;
            }
            return 0;
        }

        private static ResearchDefinition FindDefinition(
            IList<ResearchDefinition> ordered,
            string researchId)
        {
            for (var index = 0; index < ordered.Count; index++)
            {
                if (string.Equals(
                        ordered[index].Id.Value,
                        researchId,
                        StringComparison.Ordinal))
                {
                    return ordered[index];
                }
            }
            return null;
        }

        private static float RouteCenter(DevelopmentRoute route)
        {
            float[] lanes =
                ResearchTreeVisualLayoutProfile3D.RouteLaneCenters;
            switch (route)
            {
                case DevelopmentRoute.Technology: return lanes[0];
                case DevelopmentRoute.Cultivation: return lanes[1];
                case DevelopmentRoute.BiologicalAscension: return lanes[2];
                case DevelopmentRoute.Psionics: return lanes[3];
                default: return 0f;
            }
        }

        private static int CompareDefinitions(
            ResearchDefinition left,
            ResearchDefinition right)
        {
            int order = left.CatalogOrder.CompareTo(right.CatalogOrder);
            return order != 0
                ? order
                : string.Compare(
                    left.Id.Value,
                    right.Id.Value,
                    StringComparison.Ordinal);
        }
    }

    public sealed class ResearchTreeNodeProjection3D
    {
        internal ResearchTreeNodeProjection3D(
            ResearchDefinition definition,
            Vector2 position,
            bool visible)
        {
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            Position = position;
            Visible = visible;
        }

        public ResearchDefinition Definition { get; }
        public string ResearchId => Definition.Id.Value;
        public Vector2 Position { get; }
        public bool Visible { get; }
    }

    public sealed class ResearchTreeEdgeProjection3D
    {
        private readonly ReadOnlyCollection<Vector2> points;
        internal ResearchTreeEdgeProjection3D(
            string prerequisiteResearchId,
            string dependentResearchId,
            Vector2[] points,
            string junctionId,
            bool isBridge,
            DevelopmentRoute startRoute,
            DevelopmentRoute endRoute)
        {
            PrerequisiteResearchId = prerequisiteResearchId;
            DependentResearchId = dependentResearchId;
            this.points = Array.AsReadOnly(points);
            JunctionId = junctionId ?? string.Empty;
            IsBridge = isBridge;
            StartRoute = startRoute;
            EndRoute = endRoute;
        }

        public string PrerequisiteResearchId { get; }
        public string DependentResearchId { get; }
        public IReadOnlyList<Vector2> Points => points;
        public string JunctionId { get; }
        public bool IsBridge { get; }
        public DevelopmentRoute StartRoute { get; }
        public DevelopmentRoute EndRoute { get; }
    }

    public sealed class ResearchTreeJunctionProjection3D
    {
        internal ResearchTreeJunctionProjection3D(
            string stableId,
            string prerequisiteResearchId,
            Vector2 position,
            DevelopmentRoute route)
        {
            StableId = stableId;
            PrerequisiteResearchId = prerequisiteResearchId;
            Position = position;
            Route = route;
        }
        public string StableId { get; }
        public string PrerequisiteResearchId { get; }
        public Vector2 Position { get; }
        public DevelopmentRoute Route { get; }
    }

    public sealed class ResearchTreeTrunkProjection3D
    {
        private readonly ReadOnlyCollection<Vector2> points;
        internal ResearchTreeTrunkProjection3D(
            string stableId,
            string prerequisiteResearchId,
            Vector2[] points,
            DevelopmentRoute route)
        {
            StableId = stableId;
            PrerequisiteResearchId = prerequisiteResearchId;
            this.points = Array.AsReadOnly(points);
            Route = route;
        }
        public string StableId { get; }
        public string PrerequisiteResearchId { get; }
        public IReadOnlyList<Vector2> Points => points;
        public DevelopmentRoute Route { get; }
    }

    public readonly struct ResearchTreeViewportState3D :
        IEquatable<ResearchTreeViewportState3D>
    {
        public ResearchTreeViewportState3D(Vector2 center, float zoom)
        {
            Center = center;
            Zoom = ResearchTreeProjection3D.ClampZoom(zoom);
        }

        public Vector2 Center { get; }
        public float Zoom { get; }

        public bool Equals(ResearchTreeViewportState3D other)
        {
            return Center.Equals(other.Center) && Zoom.Equals(other.Zoom);
        }

        public override bool Equals(object obj)
        {
            return obj is ResearchTreeViewportState3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Center.GetHashCode() * 397) ^ Zoom.GetHashCode();
            }
        }

        public static bool operator ==(
            ResearchTreeViewportState3D left,
            ResearchTreeViewportState3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            ResearchTreeViewportState3D left,
            ResearchTreeViewportState3D right)
        {
            return !left.Equals(right);
        }
    }
}
