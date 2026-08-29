using UnityEngine;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    /// <summary>
    /// A single configuration-driven research-tree connection in local uGUI
    /// coordinates. The mesh is rebuilt only when Configure changes its data.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResearchTreeConnectionGraphic3D : MaskableGraphic
    {
        private Vector2[] points = System.Array.Empty<Vector2>();
        private float lineWidth;
        private Color endColor = Color.white;
        private bool dashed;
        private bool junction;
        private bool arrowCap;

        public bool HasOuterStroke =>
            !junction && points.Length >= 2 && lineWidth > 0f;
        public bool IsDashed => dashed;
        public bool HasArrowCap => arrowCap;

        public void Configure(
            Vector2 startPoint,
            Vector2 endPoint,
            Color lineColor,
            float width)
        {
            float resolvedWidth = Mathf.Max(0f, width);
            ConfigurePath(
                new[] { startPoint, endPoint },
                lineColor,
                lineColor,
                resolvedWidth,
                false,
                false);
        }

        public void ConfigurePath(
            System.Collections.Generic.IReadOnlyList<Vector2> path,
            Color startColor,
            Color targetColor,
            float width,
            bool useDashes,
            bool drawArrowCap)
        {
            var next = new Vector2[path?.Count ?? 0];
            for (var index = 0; index < next.Length; index++)
                next[index] = path[index];
            points = next;
            lineWidth = Mathf.Max(0f, width);
            color = startColor;
            endColor = targetColor;
            dashed = useDashes;
            junction = false;
            arrowCap = drawArrowCap;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void ConfigureJunction(
            Vector2 position,
            Color junctionColor,
            float diameter)
        {
            points = new[] { position };
            lineWidth = Mathf.Max(0f, diameter);
            color = junctionColor;
            endColor = junctionColor;
            dashed = false;
            junction = true;
            arrowCap = false;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (junction)
            {
                PopulateJunction(vertexHelper);
                return;
            }
            if (points.Length < 2 || lineWidth <= 0f) return;
            float innerWidth = lineWidth;
            Color innerStart = color;
            Color innerEnd = endColor;
            float outerWidth = innerWidth + Mathf.Max(3f, innerWidth * .8f);
            Color outerStart = new Color(
                .015f, .025f, .03f, innerStart.a * .9f);
            Color outerEnd = new Color(
                .015f, .025f, .03f, innerEnd.a * .9f);
            PopulatePath(
                vertexHelper,
                outerWidth,
                outerStart,
                outerEnd);
            PopulatePath(
                vertexHelper,
                innerWidth,
                innerStart,
                innerEnd);
        }

        private void PopulatePath(
            VertexHelper vertexHelper,
            float width,
            Color startColor,
            Color targetColor)
        {
            for (var index = 1; index < points.Length; index++)
            {
                float t0 = (index - 1f) / (points.Length - 1f);
                float t1 = index / (points.Length - 1f);
                if (dashed)
                {
                    AddDashedSegment(
                        vertexHelper,
                        points[index - 1],
                        points[index],
                        Color.Lerp(startColor, targetColor, t0),
                        Color.Lerp(startColor, targetColor, t1),
                        width);
                }
                else
                {
                    AddSegment(
                        vertexHelper,
                        points[index - 1],
                        points[index],
                        Color.Lerp(startColor, targetColor, t0),
                        Color.Lerp(startColor, targetColor, t1),
                        width);
                }
            }
            if (arrowCap)
                PopulateArrowCap(vertexHelper, width, targetColor);
        }

        private void PopulateArrowCap(
            VertexHelper helper,
            float width,
            Color targetColor)
        {
            if (points.Length < 2) return;
            Vector2 tip = points[points.Length - 1];
            Vector2 direction = tip - points[points.Length - 2];
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;
            direction.Normalize();
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float length = Mathf.Max(10f, width * 3f);
            float halfWidth = Mathf.Max(5f, width * 1.7f);
            Vector2 baseCenter = tip - direction * length;
            int first = helper.currentVertCount;
            Color32 capColor = targetColor;
            AddVertex(helper, tip, capColor);
            AddVertex(helper, baseCenter + normal * halfWidth, capColor);
            AddVertex(helper, baseCenter - normal * halfWidth, capColor);
            helper.AddTriangle(first, first + 1, first + 2);
        }

        private void AddDashedSegment(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            Color startColor,
            Color targetColor,
            float width)
        {
            const float dashLength = 26f;
            const float gapLength = 14f;
            float distance = Vector2.Distance(start, end);
            if (distance <= Mathf.Epsilon) return;
            Vector2 direction = (end - start) / distance;
            for (float offset = 0f; offset < distance;
                 offset += dashLength + gapLength)
            {
                float dashEnd = Mathf.Min(distance, offset + dashLength);
                float t0 = offset / distance;
                float t1 = dashEnd / distance;
                AddSegment(
                    helper,
                    start + direction * offset,
                    start + direction * dashEnd,
                    Color.Lerp(startColor, targetColor, t0),
                    Color.Lerp(startColor, targetColor, t1),
                    width);
            }
        }

        private void AddSegment(
            VertexHelper helper,
            Vector2 start,
            Vector2 end,
            Color32 startColor,
            Color32 targetColor,
            float width)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;
            Vector2 normal = new Vector2(-direction.y, direction.x)
                .normalized * (width * .5f);
            int first = helper.currentVertCount;
            AddVertex(helper, start - normal, startColor);
            AddVertex(helper, start + normal, startColor);
            AddVertex(helper, end + normal, targetColor);
            AddVertex(helper, end - normal, targetColor);
            helper.AddTriangle(first, first + 1, first + 2);
            helper.AddTriangle(first + 2, first + 3, first);
        }

        private void PopulateJunction(VertexHelper helper)
        {
            if (points.Length != 1 || lineWidth <= 0f) return;
            const int sides = 12;
            int center = helper.currentVertCount;
            AddVertex(helper, points[0], color);
            for (var index = 0; index <= sides; index++)
            {
                float angle = Mathf.PI * 2f * index / sides;
                AddVertex(helper, points[0] + new Vector2(
                    Mathf.Cos(angle), Mathf.Sin(angle)) * (lineWidth * .5f),
                    color);
                if (index > 0)
                    helper.AddTriangle(center, center + index,
                        center + index + 1);
            }
        }

        private static void AddVertex(
            VertexHelper vertexHelper,
            Vector2 position,
            Color32 vertexColor)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = vertexColor;
            vertexHelper.AddVert(vertex);
        }
    }
}
