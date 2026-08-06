using UnityEngine;
using UnityEngine.UI;

namespace ElectroOptics.UI.ComponentInfoCard
{
    /// <summary>
    /// 元件介绍卡左侧预览区的轻量网格。
    /// 使用 uGUI 顶点绘制，避免依赖透明度有误的网格切图。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComponentInfoCardGridGraphic : MaskableGraphic
    {
        [SerializeField, Min(4f)] private float spacing = 13f;
        [SerializeField, Range(0.5f, 4f)] private float lineThickness = 1f;

        public float Spacing => spacing;
        public float LineThickness => lineThickness;

        public void Configure(float gridSpacing, float thickness, Color gridColor)
        {
            spacing = Mathf.Max(4f, gridSpacing);
            lineThickness = Mathf.Clamp(thickness, 0.5f, 4f);
            color = gridColor;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            float safeSpacing = Mathf.Max(4f, spacing);
            float safeThickness = Mathf.Clamp(lineThickness, 0.5f, safeSpacing);
            Color32 vertexColor = color;

            int firstVertical = Mathf.CeilToInt((rect.xMin - rect.center.x) / safeSpacing);
            int lastVertical = Mathf.FloorToInt((rect.xMax - rect.center.x) / safeSpacing);
            for (int index = firstVertical; index <= lastVertical; index++)
            {
                float x = rect.center.x + index * safeSpacing;
                AddQuad(
                    vertexHelper,
                    new Rect(x - safeThickness * 0.5f, rect.yMin, safeThickness, rect.height),
                    vertexColor);
            }

            int firstHorizontal = Mathf.CeilToInt((rect.yMin - rect.center.y) / safeSpacing);
            int lastHorizontal = Mathf.FloorToInt((rect.yMax - rect.center.y) / safeSpacing);
            for (int index = firstHorizontal; index <= lastHorizontal; index++)
            {
                float y = rect.center.y + index * safeSpacing;
                AddQuad(
                    vertexHelper,
                    new Rect(rect.xMin, y - safeThickness * 0.5f, rect.width, safeThickness),
                    vertexColor);
            }
        }

        private static void AddQuad(VertexHelper vertexHelper, Rect rect, Color32 color)
        {
            int startIndex = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(rect.xMin, rect.yMin);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
