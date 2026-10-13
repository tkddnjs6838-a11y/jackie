using UnityEngine;
using UnityEngine.UI;
public sealed class MenuButtonGradient : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive())return;
        var rect=graphic.rectTransform.rect;UIVertex v=new UIVertex();
        for(int i=0;i<mesh.currentVertCount;i++)
        {
            mesh.PopulateUIVertex(ref v,i);
            var tint=Color.Lerp(new Color(1,.5f,.06f),new Color(1,.76f,.29f),Mathf.InverseLerp(rect.yMin,rect.yMax,v.position.y));
            v.color=(Color)v.color*tint;mesh.SetUIVertex(v,i);
        }
    }
}
