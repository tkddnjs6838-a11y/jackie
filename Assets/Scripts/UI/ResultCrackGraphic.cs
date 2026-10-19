using UnityEngine;
using UnityEngine.UI;

// Brief decorative fractures at the panel edges, clear of text and controls.
public sealed class ResultCrackGraphic : MaskableGraphic
{
    float elapsed;
    static readonly Vector2[] Path={new Vector2(-266,130),new Vector2(-246,117),new Vector2(-253,101),new Vector2(-231,85),new Vector2(-240,69),new Vector2(-221,54)};
    protected override void OnEnable(){base.OnEnable();elapsed=0;SetVerticesDirty();}
    void Update(){if(elapsed>=1.1f)return;elapsed+=Time.unscaledDeltaTime;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float reveal=Mathf.Clamp01(elapsed/.38f)*(Path.Length-1);
        float alpha=.7f*(1-Mathf.Clamp01((elapsed-.4f)/.7f));
        for(int side=0;side<2;side++)for(int i=0;i<Path.Length-1;i++)
        {
            float t=Mathf.Clamp01(reveal-i);if(t<=0)continue;
            var a=Path[i];var b=Vector2.Lerp(a,Path[i+1],t);
            if(side==1){a.x=-a.x;b.x=-b.x;a.y-=20;b.y-=20;}
            var n=new Vector2(-(b-a).y,(b-a).x).normalized*(1.2f+Mathf.Sin(Mathf.Clamp01(elapsed/.5f)*Mathf.PI));
            int k=vh.currentVertCount;var c=new Color(.46f,.83f,1,alpha);
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
        }
    }
}
