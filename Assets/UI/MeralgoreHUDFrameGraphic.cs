using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Empty frame layer retained as a layout anchor for HUD modules.
/// METALGORE panels intentionally have no rectangular surface or outline.
/// Their visual structure is built from independent curved traces, ticks,
/// labels and status marks.
/// </summary>
[AddComponentMenu("METALGORE/UI/HUD Frame Graphic")]
public sealed class MeralgoreHUDFrameGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
    }
}
