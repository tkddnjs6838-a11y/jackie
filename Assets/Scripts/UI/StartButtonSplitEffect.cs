using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// Quiet hover feedback; the split plays once only after the start command is accepted.
public sealed class StartButtonSplitEffect : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    Button button;StartButtonSplitMesh mesh;bool hovered,pressed,launching;float age,scale=1;
    void Awake()
    {
        button=GetComponent<Button>();mesh=gameObject.AddComponent<StartButtonSplitMesh>();
        button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;colors.normalColor=new Color(.94f,.94f,.94f);colors.highlightedColor=new Color(1,1,.88f);colors.selectedColor=colors.normalColor;colors.pressedColor=new Color(.86f,.81f,.67f);colors.fadeDuration=.12f;button.colors=colors;
    }
    public void OnPointerEnter(PointerEventData e){if(button.IsInteractable())hovered=true;}
    public void OnPointerExit(PointerEventData e){hovered=pressed=false;}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button.IsInteractable())pressed=true;}
    public void OnPointerUp(PointerEventData e){pressed=false;}
    public void PlayLaunch()
    {
        launching=true;age=0;pressed=false;
        button.interactable=false;
        var view=GetComponentInParent<StartScreenView>();if(view!=null&&view.settingsButton!=null)view.settingsButton.interactable=false;
    }
    void Update()
    {
        if(launching){age+=Time.unscaledDeltaTime;mesh.Frame(age+.08f,1);return;}
        if(!button.IsInteractable())hovered=pressed=false;
        float target=pressed?.96f:hovered?1.04f:1;
        scale=Mathf.Lerp(scale,target,1-Mathf.Exp(-22*Time.unscaledDeltaTime));
        mesh.SetScale(scale);mesh.Frame(0,0);
    }
    void OnDisable(){hovered=pressed=launching=false;age=0;scale=1;if(mesh!=null){mesh.SetScale(1);mesh.Frame(0,0);}}
}
public sealed class StartButtonSplitMesh : BaseMeshEffect
{
    float time,weight,scale=1;
    public void SetScale(float s){if(Mathf.Approximately(scale,s))return;scale=s;graphic.SetVerticesDirty();}
    readonly List<UIVertex> source=new List<UIVertex>(),poly=new List<UIVertex>(),temp=new List<UIVertex>(),output=new List<UIVertex>();
    public void Frame(float t,float w){if(Mathf.Approximately(time,t)&&Mathf.Approximately(weight,w))return;time=t;weight=w;graphic.SetVerticesDirty();}
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    void Clip(Vector2 a,Vector2 b)
    {
        temp.Clear();for(int i=0;i<poly.Count;i++)
        {
            var p=poly[i];var q=poly[(i+1)%poly.Count];float dp=Cross(b-a,(Vector2)p.position-a),dq=Cross(b-a,(Vector2)q.position-a);
            if(dp>=-.0001f)temp.Add(p);
            if((dp>=0)!=(dq>=0)){float t=dp/(dp-dq);var v=p;v.position=Vector3.Lerp(p.position,q.position,t);v.uv0=Vector4.Lerp(p.uv0,q.uv0,t);v.uv1=Vector4.Lerp(p.uv1,q.uv1,t);v.color=Color32.Lerp(p.color,q.color,t);temp.Add(v);}
        }
        poly.Clear();poly.AddRange(temp);
    }
    static float Burst(float t,float delay,float hold)
    {t-=delay;if(t<=0)return 0;return (1-Mathf.Pow(1-Mathf.Clamp01(t/.075f),3))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-hold)/.32f)));}
    UIVertex Move(UIVertex v,Vector2 center,int index)
    {
        float age=time-.16f;float a=Burst(age,0,.24f);
        float side=center.x<0?-1:1;float ratio=((RectTransform)transform).rect.width/330f;var pivot=new Vector2(side*65*ratio,0);Vector2 p=v.position;
        float press=time<.16f?Mathf.Sin(time/.16f*Mathf.PI*.5f):0;
        p=Vector2.Scale(p,new Vector2(1-.025f*press,1+.03f*press));
        p=pivot+(Vector2)(Quaternion.Euler(0,0,side*12*a)*(p-pivot))+new Vector2(side*16*a,side*5*a)*ratio;
        p=Vector2.Lerp(v.position,p,weight);
        v.position=p*scale;return v;
    }
    void Piece(Vector2 a,Vector2 b,Vector2 c,int index)
    {
        var center=(a+b+c)/3;
        for(int i=0;i<source.Count;i+=3)
        {
            poly.Clear();poly.Add(source[i]);poly.Add(source[i+1]);poly.Add(source[i+2]);Clip(a,b);Clip(b,c);Clip(c,a);
            for(int j=1;j<poly.Count-1;j++){output.Add(Move(poly[0],center,index));output.Add(Move(poly[j],center,index));output.Add(Move(poly[j+1],center,index));}
        }
    }
    public override void ModifyMesh(VertexHelper vh)
    {
        if(!IsActive())return;
        if(weight<=0)
        {
            UIVertex v=default;for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref v,i);v.position*=scale;vh.SetUIVertex(v,i);}return;
        }
        source.Clear();output.Clear();vh.GetUIVertexStream(source);var r=((RectTransform)transform).rect;
        float[] seam={0,-8,7,-5,0};int index=0;
        for(int y=0;y<4;y++)for(int side=-1;side<=1;side+=2)
        {
            float bottom=Mathf.Lerp(r.yMin,r.yMax,y/4f),top=Mathf.Lerp(r.yMin,r.yMax,(y+1)/4f);
            var a=side<0?new Vector2(r.xMin,bottom):new Vector2(seam[y],bottom);
            var b=side<0?new Vector2(seam[y],bottom):new Vector2(r.xMax,bottom);
            var c=side<0?new Vector2(seam[y+1],top):new Vector2(r.xMax,top);
            var d=side<0?new Vector2(r.xMin,top):new Vector2(seam[y+1],top);
            Piece(a,b,c,index++);Piece(a,c,d,index++);
        }
        vh.Clear();vh.AddUIVertexTriangleStream(output);
    }
}
