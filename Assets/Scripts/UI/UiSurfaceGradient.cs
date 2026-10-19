using UnityEngine;
using UnityEngine.UI;

// Vertex tint only: shared UI material, no extra textures or shader passes.
public sealed class UiSurfaceGradient : BaseMeshEffect
{
    public float bottomMultiplier=.72f,topWhite=.28f;
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive())return;
        var rect=graphic.rectTransform.rect;var vertex=new UIVertex();
        for(int i=0;i<mesh.currentVertCount;i++)
        {
            mesh.PopulateUIVertex(ref vertex,i);
            float t=Mathf.InverseLerp(rect.yMin,rect.yMax,vertex.position.y);
            Color original=vertex.color;
            Color tint=Color.Lerp(original*bottomMultiplier,Color.Lerp(original,Color.white,topWhite),t);
            tint.a=original.a;vertex.color=tint;mesh.SetUIVertex(vertex,i);
        }
    }
}
