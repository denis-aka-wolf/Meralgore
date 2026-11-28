using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact optical reticle used by the METALGORE tactical layer.
/// Its interrupted brackets leave the target silhouette readable.
/// </summary>
[AddComponentMenu("METALGORE/UI/Tactical Reticle")]
public sealed class MeralgoreHUDReticleGraphic : MaskableGraphic
{
    [SerializeField] private float armLength = 14f;
    [SerializeField] private float gap = 9f;
    [SerializeField] private float stroke = 1.25f;
    [SerializeField] private float centerSize = 2.5f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;

        AddCorner(vh, new Vector2(c.x - halfW, c.y + halfH), 1f, -1f);
        AddCorner(vh, new Vector2(c.x + halfW, c.y + halfH), -1f, -1f);
        AddCorner(vh, new Vector2(c.x - halfW, c.y - halfH), 1f, 1f);
        AddCorner(vh, new Vector2(c.x + halfW, c.y - halfH), -1f, 1f);

        AddQuad(vh, new Vector2(c.x - centerSize * 0.5f, c.y - centerSize * 0.5f),
            new Vector2(c.x + centerSize * 0.5f, c.y + centerSize * 0.5f), color);
        AddQuad(vh, new Vector2(c.x - 1f, c.y + gap + 5f),
            new Vector2(c.x + 1f, c.y + gap + 9f), color);
        AddQuad(vh, new Vector2(c.x - 1f, c.y - gap - 9f),
            new Vector2(c.x + 1f, c.y - gap - 5f), color);
    }

    private void AddCorner(VertexHelper vh, Vector2 origin, float xDirection, float yDirection)
    {
        AddQuad(vh, origin + new Vector2(xDirection * gap, yDirection * (gap + armLength)),
            origin + new Vector2(xDirection * (gap + stroke), yDirection * gap), color);
        AddQuad(vh, origin + new Vector2(xDirection * gap, yDirection * (gap + stroke)),
            origin + new Vector2(xDirection * (gap + armLength), yDirection * gap), color);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Color tint)
    {
        float minX = Mathf.Min(a.x, b.x);
        float maxX = Mathf.Max(a.x, b.x);
        float minY = Mathf.Min(a.y, b.y);
        float maxY = Mathf.Max(a.y, b.y);
        int start = vh.currentVertCount;
        vh.AddVert(new Vector2(minX, minY), tint, Vector2.zero);
        vh.AddVert(new Vector2(minX, maxY), tint, Vector2.zero);
        vh.AddVert(new Vector2(maxX, maxY), tint, Vector2.zero);
        vh.AddVert(new Vector2(maxX, minY), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}