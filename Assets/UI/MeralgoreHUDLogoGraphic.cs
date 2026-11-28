using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact angular insignia for the METALGORE combat operating system.
/// Rendered as a small vector mark so the brand remains crisp at every HUD scale.
/// </summary>
[AddComponentMenu("METALGORE/UI/HUD Logo Graphic")]
public sealed class MeralgoreHUDLogoGraphic : MaskableGraphic
{
    [SerializeField] private float strokeWidth = 2.2f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float sx = r.width / 48f;
        float sy = r.height / 54f;
        Vector2 p(float x, float y) => new Vector2(r.xMin + x * sx, r.yMin + y * sy);

        // Broken monolithic M: four rising/falling steel strokes.
        AddStroke(vh, p(5f, 5f), p(5f, 47f), strokeWidth, color);
        AddStroke(vh, p(5f, 47f), p(24f, 27f), strokeWidth, color);
        AddStroke(vh, p(24f, 27f), p(43f, 47f), strokeWidth, color);
        AddStroke(vh, p(43f, 47f), p(43f, 5f), strokeWidth, color);

        // Interrupted crossbar and lower signal notch.
        AddStroke(vh, p(13f, 20f), p(24f, 9f), strokeWidth * 0.72f, color);
        AddStroke(vh, p(24f, 9f), p(35f, 20f), strokeWidth * 0.72f, color);
        AddStroke(vh, p(20f, 27f), p(28f, 27f), strokeWidth * 0.72f,
            new Color(color.r, color.g, color.b, 0.55f));
        AddStroke(vh, p(23f, 3f), p(25f, 3f), strokeWidth * 1.5f,
            new Color32(179, 115, 20, 255));
    }

    private static void AddStroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero);
        vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero);
        vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}