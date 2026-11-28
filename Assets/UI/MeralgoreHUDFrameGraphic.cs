using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the METALGORE instrument frame as one mesh instead of a stack of
/// rectangular Images. The clipped corners and offset signal rail establish
/// a consistent industrial display language while keeping hierarchy lean.
/// </summary>
[AddComponentMenu("METALGORE/UI/HUD Frame Graphic")]
public sealed class MeralgoreHUDFrameGraphic : MaskableGraphic
{
    [SerializeField] private float cornerCut = 13f;
    [SerializeField] private float borderWidth = 1.15f;
    [SerializeField] private float accentLength = 54f;
    [SerializeField] private float inset = 5f;

    private Color _surface = new Color32(16, 18, 18, 224);
    private Color _line = new Color32(69, 74, 71, 220);
    private Color _accent = new Color32(77, 122, 120, 255);

    public void SetPalette(Color surface, Color line, Color accent)
    {
        _surface = surface;
        _line = line;
        _accent = accent;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        float cut = Mathf.Min(cornerCut, Mathf.Min(r.width, r.height) * 0.12f);
        Vector2[] outer =
        {
            new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMax - cut, r.yMax),
            new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax, r.yMin + cut),
            new Vector2(r.xMax - cut, r.yMin), new Vector2(r.xMin + cut, r.yMin),
            new Vector2(r.xMin, r.yMin + cut), new Vector2(r.xMin, r.yMax - cut)
        };

        AddPolygon(vh, outer, _surface);

        Vector2[] inner =
        {
            new Vector2(r.xMin + cut + inset, r.yMax - borderWidth),
            new Vector2(r.xMax - cut - inset, r.yMax - borderWidth),
            new Vector2(r.xMax - borderWidth, r.yMax - cut - inset),
            new Vector2(r.xMax - borderWidth, r.yMin + cut + inset),
            new Vector2(r.xMax - cut - inset, r.yMin + borderWidth),
            new Vector2(r.xMin + cut + inset, r.yMin + borderWidth),
            new Vector2(r.xMin + borderWidth, r.yMin + cut + inset),
            new Vector2(r.xMin + borderWidth, r.yMax - cut - inset)
        };
        AddPolyline(vh, outer, inner, _line);

        float railY = r.yMax - 1.5f;
        AddQuad(vh, new Vector2(r.xMin + cut + 8f, railY - 1.1f),
            new Vector2(r.xMin + cut + 8f + accentLength, railY + 0.1f), _accent);

        float tickStart = r.xMax - cut - 42f;
        for (int i = 0; i < 3; i++)
        {
            float x = tickStart + i * 7f;
            AddQuad(vh, new Vector2(x, r.yMin + 6f),
                new Vector2(x + 2f, r.yMin + 12f + i * 2f), new Color(_line.r, _line.g, _line.b, 0.8f));
        }
    }

    private static void AddPolygon(VertexHelper vh, Vector2[] points, Color color)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < points.Length; i++)
            AddVert(vh, points[i], color);
        for (int i = 1; i < points.Length - 1; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }

    private static void AddPolyline(VertexHelper vh, Vector2[] outer, Vector2[] inner, Color color)
    {
        int start = vh.currentVertCount;
        for (int i = 0; i < outer.Length; i++)
        {
            AddVert(vh, outer[i], color);
            AddVert(vh, inner[i], color);
        }
        for (int i = 0; i < outer.Length; i++)
        {
            int next = (i + 1) % outer.Length;
            int a = start + i * 2;
            int b = start + next * 2;
            vh.AddTriangle(a, b, b + 1);
            vh.AddTriangle(a, b + 1, a + 1);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color color)
    {
        int start = vh.currentVertCount;
        AddVert(vh, new Vector2(min.x, min.y), color);
        AddVert(vh, new Vector2(min.x, max.y), color);
        AddVert(vh, new Vector2(max.x, max.y), color);
        AddVert(vh, new Vector2(max.x, min.y), color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddVert(VertexHelper vh, Vector2 position, Color color)
    {
        vh.AddVert(position, color, Vector2.zero);
    }
}