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
        private Vector2 start;
        private Vector2 end;
        private float lineWidth;

        public void Configure(
            Vector2 startPoint,
            Vector2 endPoint,
            Color lineColor,
            float width)
        {
            float resolvedWidth = Mathf.Max(0f, width);
            bool geometryChanged = !start.Equals(startPoint) ||
                !end.Equals(endPoint) ||
                !lineWidth.Equals(resolvedWidth);

            start = startPoint;
            end = endPoint;
            lineWidth = resolvedWidth;
            raycastTarget = false;

            if (!color.Equals(lineColor))
                color = lineColor;
            if (geometryChanged)
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
            Vector2 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon || lineWidth <= 0f)
                return;

            Vector2 halfNormal = new Vector2(-direction.y, direction.x)
                .normalized * (lineWidth * .5f);
            Color32 vertexColor = color;

            AddVertex(vertexHelper, start - halfNormal, vertexColor);
            AddVertex(vertexHelper, start + halfNormal, vertexColor);
            AddVertex(vertexHelper, end + halfNormal, vertexColor);
            AddVertex(vertexHelper, end - halfNormal, vertexColor);
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
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
