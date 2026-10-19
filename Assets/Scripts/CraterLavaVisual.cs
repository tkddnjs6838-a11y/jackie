using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: the attack coroutine retains the original damage lane and clock.
public sealed class CraterLavaVisual : MonoBehaviour
{
    const int Across=13, Rings=25, Sides=12;
    Mesh surface,tube;
    Vector3[] vertices,tubeVertices,bed;
    Vector2[] uv;
    List<Vector3> path;
    Transform jet;
    readonly Transform[] drops=new Transform[10];
    MaterialPropertyBlock block;
    Renderer surfaceRenderer;
    float halfWidth;
    Vector3 right;
    public void Build(List<Vector3> points,Vector3 lateral,float width,Material material)
    {
        path=points;right=lateral;halfWidth=width;block=new MaterialPropertyBlock();
        surface=new Mesh{name="RoundedLavaSurface"};surface.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh=surface;
        surfaceRenderer=gameObject.AddComponent<MeshRenderer>();surfaceRenderer.sharedMaterial=material;surfaceRenderer.shadowCastingMode=ShadowCastingMode.Off;
        vertices=new Vector3[path.Count*Across];bed=new Vector3[vertices.Length];uv=new Vector2[vertices.Length];
        var triangles=new int[(path.Count-1)*(Across-1)*6];int k=0;
        for(int i=0;i<path.Count;i++)for(int j=0;j<Across;j++)
        {
            int n=i*Across+j;float u=j/(float)(Across-1);Vector3 p=path[i]+right*((u*2-1)*width);
            Vector3 ground;if(MapNavigation.Sample(p,.8f,out ground) && Mathf.Abs(ground.y-p.y)<.75f)p.y=ground.y;
            bed[n]=p;uv[n]=new Vector2(u,i*.5f);
            if(i==path.Count-1||j==Across-1)continue;
            triangles[k++]=n;triangles[k++]=n+Across;triangles[k++]=n+1;
            triangles[k++]=n+1;triangles[k++]=n+Across;triangles[k++]=n+Across+1;
        }
        surface.vertices=vertices;surface.uv=uv;surface.triangles=triangles;
        var go=new GameObject("FallingMoltenLava");go.transform.SetParent(transform,false);jet=go.transform;
        tube=new Mesh{name="GravityLavaJet"};tube.MarkDynamic();go.AddComponent<MeshFilter>().sharedMesh=tube;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        var jetBlock=new MaterialPropertyBlock();jetBlock.SetFloat("_Stream",1);renderer.SetPropertyBlock(jetBlock);
        tubeVertices=new Vector3[Rings*Sides];var tuv=new Vector2[tubeVertices.Length];var ti=new int[(Rings-1)*Sides*6];k=0;
        for(int i=0;i<Rings;i++)for(int j=0;j<Sides;j++)
        {
            int n=i*Sides+j;tuv[n]=new Vector2(j/(float)Sides,i*.17f);
            if(i==Rings-1)continue;int next=i*Sides+(j+1)%Sides;
            ti[k++]=n;ti[k++]=next;ti[k++]=n+Sides;ti[k++]=next;ti[k++]=next+Sides;ti[k++]=n+Sides;
        }
        tube.vertices=tubeVertices;tube.uv=tuv;tube.triangles=ti;
        for(int i=0;i<drops.Length;i++)
        {
            var d=GameObject.CreatePrimitive(PrimitiveType.Sphere);d.name="MoltenSplash";d.layer=2;
            Destroy(d.GetComponent<Collider>());d.transform.SetParent(transform,false);
            var r=d.GetComponent<Renderer>();r.sharedMaterial=material;r.SetPropertyBlock(jetBlock);r.shadowCastingMode=ShadowCastingMode.Off;drops[i]=d.transform;
        }
    }
    public void Tick(Vector3 hand,float elapsed,float advance,float fade)
    {
        // The advancing end is a rounded lobe, not a long strip collapsing to a centre line.
        float head=Mathf.Clamp(advance,0,path.Count-1);
        int segment=Mathf.Min(path.Count-2,Mathf.FloorToInt(head));
        Vector3 tip=Vector3.Lerp(path[segment],path[segment+1],head-segment);
        for(int i=0;i<path.Count;i++)for(int j=0;j<Across;j++)
        {
            int n=i*Across+j;float x=j/(float)(Across-1)*2-1;
            float cap=Mathf.Sqrt(Mathf.Clamp01((head-i)*.6f));
            cap*=Mathf.Sqrt(Mathf.Clamp01((i+.25f)*.7f));
            float rim=1-.065f*(1+Mathf.Sin(i*1.65f+x*2));
            float w=halfWidth*cap*rim*(.8f+.2f*fade);
            Vector3 p=i>head?tip:path[i];p+=right*x*w;
            float profile=Mathf.Sqrt(Mathf.Max(0,1-x*x));
            float bulge=(.18f+.07f*Mathf.Sin(i*.9f-elapsed*4+x*3))*profile*cap;
            p.y=Mathf.Lerp(p.y,bed[n].y,cap)+.055f+bulge*fade;
            if(i>head)p=tip+Vector3.up*.055f;
            vertices[n]=p;
        }
        surface.vertices=vertices;surface.RecalculateNormals();surface.RecalculateBounds();
        block.SetFloat("_Cooling",Mathf.Clamp01((elapsed-2.7f)/2.3f));surfaceRenderer.SetPropertyBlock(block);
        jet.gameObject.SetActive(elapsed<2.2f);
        Vector3 landing=path[0]+Vector3.up*.22f;
        for(int i=0;i<Rings;i++)
        {
            float u=i/(float)(Rings-1);Vector3 center=Vector3.Lerp(hand,landing,u);
            // Downward acceleration produces a hanging, heavy stream instead of a rigid rod.
            center.y=Mathf.Lerp(hand.y,landing.y,u*u);
            Vector3 tangent=(landing-hand);tangent.y=2*u*(landing.y-hand.y);tangent.Normalize();
            Vector3 a=Vector3.Cross(tangent,right).normalized;Vector3 b=Vector3.Cross(tangent,a).normalized;
            float radius=Mathf.Lerp(.28f,.17f,u)*(1+.14f*Mathf.Sin(u*23-elapsed*11)+.07f*Mathf.Sin(u*41-elapsed*17));
            for(int j=0;j<Sides;j++){float angle=j*Mathf.PI*2/Sides;tubeVertices[i*Sides+j]=center+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*radius;}
        }
        tube.vertices=tubeVertices;tube.RecalculateNormals();tube.RecalculateBounds();
        for(int i=0;i<drops.Length;i++)
        {
            drops[i].gameObject.SetActive(elapsed<2.35f);
            float age=Mathf.Repeat(elapsed+i*.117f,.72f),angle=i*2.39996f;
            Vector3 velocity=new Vector3(Mathf.Cos(angle)*1.4f,2.6f+(i%3)*.35f,Mathf.Sin(angle)*1.4f);
            drops[i].position=landing+velocity*age+Vector3.down*(5.5f*age*age);
            float size=(.09f+(i%3)*.035f)*(1-age/.8f);drops[i].localScale=new Vector3(size,size*1.6f,size);
        }
    }
    void OnDestroy(){if(surface!=null)Destroy(surface);if(tube!=null)Destroy(tube);}
}
