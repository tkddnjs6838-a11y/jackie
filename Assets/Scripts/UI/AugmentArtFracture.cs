using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// Break only the illustration into irregular triangular shards; copy and hit area remain intact.
public sealed class AugmentArtFracture : BaseMeshEffect
{
    sealed class Shard {public Vector2 a,b,c,center,motion;public float angle,delay,duration,bounce,arc;}
    readonly List<Shard> shards=new List<Shard>();
    readonly List<UIVertex> source=new List<UIVertex>();
    readonly List<UIVertex> polygon=new List<UIVertex>();
    readonly List<UIVertex> clipped=new List<UIVertex>();
    readonly List<UIVertex> result=new List<UIVertex>();
    readonly System.Random random=new System.Random(System.Guid.NewGuid().GetHashCode());
    float amount,age;
    Vector2 impact;
    float propagation;
    float Range(float a,float b)=>(float)(a+(b-a)*random.NextDouble());
    public void NewPattern()
    {
        impact=new Vector2(Range(-65,65),Range(-55,55));propagation=Range(.045f,.10f);
        shards.Clear();var r=((RectTransform)transform).rect;
        var points=new Vector2[4,3];
        for(int x=0;x<4;x++)for(int y=0;y<3;y++)points[x,y]=new Vector2(r.xMin+r.width*x/3f+(x>0&&x<3?Range(-12,12):0),r.yMin+r.height*y/2f+(y==1?Range(-17,17):0));
        for(int x=0;x<3;x++)for(int y=0;y<2;y++)
        {
            var a=points[x,y];var b=points[x+1,y];var c=points[x+1,y+1];var d=points[x,y+1];
            if(random.Next(2)==0){Add(a,b,c);Add(a,c,d);}else{Add(a,b,d);Add(b,c,d);}
        }
        amount=0;graphic.SetVerticesDirty();
    }
    void Add(Vector2 a,Vector2 b,Vector2 c)
    {
        var center=(a+b+c)/3;var outward=(center-impact+new Vector2(Range(-12,12),Range(-12,12))).normalized;
        shards.Add(new Shard{a=a,b=b,c=c,center=center,motion=outward*Range(17,31),angle=Range(-24,24),delay=Vector2.Distance(center,impact)/200f*propagation+Range(0,.025f),duration=Range(.075f,.14f),bounce=Range(.25f,.55f),arc=Range(-12,12)});
    }
    public void SetAmount(float value){if(Mathf.Approximately(value,amount))return;amount=value;graphic.SetVerticesDirty();}
    public void SetMotion(float time,float visibility)
    {
        age=time;amount=visibility;graphic.SetVerticesDirty();
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    void Clip(Vector2 a,Vector2 b)
    {
        clipped.Clear();if(polygon.Count==0)return;
        for(int i=0;i<polygon.Count;i++)
        {
            var p=polygon[i];var q=polygon[(i+1)%polygon.Count];float dp=Cross(b-a,(Vector2)p.position-a),dq=Cross(b-a,(Vector2)q.position-a);
            bool pin=dp>=-.0001f,qin=dq>=-.0001f;if(pin)clipped.Add(p);
            if(pin!=qin)
            {
                float t=dp/(dp-dq);var v=p;v.position=Vector3.Lerp(p.position,q.position,t);v.uv0=Vector4.Lerp(p.uv0,q.uv0,t);v.uv1=Vector4.Lerp(p.uv1,q.uv1,t);v.color=Color32.Lerp(p.color,q.color,t);clipped.Add(v);
            }
        }
        polygon.Clear();polygon.AddRange(clipped);
    }
    public override void ModifyMesh(VertexHelper vh)
    {
        if(!IsActive()||amount<=0||shards.Count==0)return;
        source.Clear();result.Clear();vh.GetUIVertexStream(source);
        foreach(var shard in shards)for(int i=0;i<source.Count;i+=3)
        {
            polygon.Clear();polygon.Add(source[i]);polygon.Add(source[i+1]);polygon.Add(source[i+2]);
            Clip(shard.a,shard.b);Clip(shard.b,shard.c);Clip(shard.c,shard.a);
            for(int j=1;j<polygon.Count-1;j++){result.Add(Move(polygon[0],shard));result.Add(Move(polygon[j],shard));result.Add(Move(polygon[j+1],shard));}
        }
        vh.Clear();vh.AddUIVertexTriangleStream(result);
    }
    UIVertex Move(UIVertex v,Shard shard)
    {
        float t=Mathf.Max(0,age-shard.delay);
        float launch=1-Mathf.Pow(1-Mathf.Clamp01(t/shard.duration),3);
        float tail=Mathf.Max(0,t-shard.duration);
        // Fast release, overshoot, then a damped rebound. Each shard breaks on its own beat.
        float spread=t<shard.duration?launch*(1+shard.bounce):1+shard.bounce*Mathf.Exp(-tail*12)*Mathf.Cos(tail*25);
        float kick=Mathf.Sin(Mathf.Clamp01(t/(shard.duration*2))*Mathf.PI);
        var tangent=new Vector2(-shard.motion.y,shard.motion.x).normalized;
        var p=(Vector2)v.position-shard.center;
        p=Quaternion.Euler(0,0,shard.angle*spread*amount)*p;
        v.position=p+shard.center+(shard.motion*spread+tangent*shard.arc*kick)*amount;return v;
    }
}
