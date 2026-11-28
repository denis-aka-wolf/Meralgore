using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the peripheral perspective field of the METALGORE visor HUD.
/// The broken upper/lower arcs and converging traces imply depth without
/// enclosing the gameplay view in a frame.
/// </summary>
[AddComponentMenu("METALGORE/UI/HUD Perspective Graphic")]
public sealed class MeralgoreHUDPerspectiveGraphic : MaskableGraphic
{
    private const float LineWidth = 1.15f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;
        if (halfWidth <= 0f || halfHeight <= 0f)
            return;

        DrawBrokenArc(vh, halfWidth, halfHeight, true);
        DrawBrokenArc(vh, halfWidth, halfHeight, false);
        DrawPerspectiveRays(vh, halfWidth, halfHeight);
        DrawPeripheralTicks(vh, halfWidth, halfHeight);
    }

    private void DrawBrokenArc(VertexHelper vh, float halfWidth, float halfHeight, bool upper)
    {
        const int segments = 72;
        const int gapModulo = 9;
        const float gapWidth = 2.2f;
        float previousX = 0f;
        float previousY = 0f;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float normalized = t * 2f - 1f;
            float edge = Mathf.Pow(Mathf.Abs(normalized), 1.55f);
            float x = Mathf.Lerp(-halfWidth * 0.97f, halfWidth * 0.97f, t);
            float yMagnitude = halfHeight * (0.445f - 0.115f * edge);
            float y = upper ? yMagnitude : -yMagnitude;

            bool inGap = i % gapModulo < gapWidth || Mathf.Abs(normalized) < 0.13f;
            if (i > 0 && !inGap)
                AddLine(vh, new Vector2(previousX, previousY), new Vector2(x, y), LineWidth);

            previousX = x;
            previousY = y;
        }
    }

    private void DrawPerspectiveRays(VertexHelper vh, float halfWidth, float halfHeight)
    {
        Vector2[] outer =
        {
            new Vector2(-halfWidth * 0.985f, halfHeight * 0.24f),
            new Vector2(-halfWidth * 0.985f, -halfHeight * 0.24f),
            new Vector2(halfWidth * 0.985f, halfHeight * 0.24f),
            new Vector2(halfWidth * 0.985f, -halfHeight * 0.24f)
        };

        Vector2[] inner =
        {
            new Vector2(-halfWidth * 0.16f, halfHeight * 0.15f),
            new Vector2(-halfWidth * 0.16f, -halfHeight * 0.15f),
            new Vector2(halfWidth * 0.16f, halfHeight * 0.15f),
            new Vector2(halfWidth * 0.16f, -halfHeight * 0.15f)
        };

        for (int i = 0; i < outer.Length; i++)
        {
            Vector2 start = Vector2.Lerp(outer[i], inner[i], 0.08f);
            Vector2 end = Vector2.Lerp(outer[i], inner[i], 0.72f);
            AddLine(vh, start, end, LineWidth * 0.8f);
        }

        // Short diagonal vanishing marks sit outside the central combat area.
        AddLine(vh, new Vector2(-halfWidth * 0.23f, halfHeight * 0.205f),
            new Vector2(-halfWidth * 0.17f, halfHeight * 0.17f), LineWidth);
        AddLine(vh, new Vector2(halfWidth * 0.23f, halfHeight * 0.205f),
            new Vector2(halfWidth * 0.17f, halfHeight * 0.17f), LineWidth);
        AddLine(vh, new Vector2(-halfWidth * 0.23f, -halfHeight * 0.205f),
            new Vector2(-halfWidth * 0.17f, -halfHeight * 0.17f), LineWidth);
        AddLine(vh, new Vector2(halfWidth * 0.23f, -halfHeight * 0.205f),
            new Vector2(halfWidth * 0.17f, -halfHeight * 0.17f), LineWidth);
    }

    private void DrawPeripheralTicks(VertexHelper vh, float halfWidth, float halfHeight)
    {
        for (int i = 0; i < 13; i++)
        {
            float t = i / 12f;
            float x = Mathf.Lerp(-halfWidth * 0.91f, halfWidth * 0.91f, t);
            float normalized = t * 2f - 1f;
            float y = halfHeight * (0.445f - 0.115f * Mathf.Pow(Mathf.Abs(normalized), 1.55f));
            float tick = i % 3 == 0 ? 8f : 4f;

            AddLine(vh, new Vector2(x, y - tick), new Vector2(x, y + 1f), LineWidth);
            AddLine(vh, new Vector2(x, -y - 1f), new Vector2(x, -y + tick), LineWidth);
        }
    }

    private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width)
    {
        Vector2 direction = (end - start).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        int index = vh.currentVertCount;
        AddVertex(vh, start - normal);
        AddVertex(vh, start + normal);
        AddVertex(vh, end + normal);
        AddVertex(vh, end - normal);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private void AddVertex(VertexHelper vh, Vector2 position)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}