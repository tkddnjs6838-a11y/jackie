using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The final boss owns only two attacks: an aimed lava throw and a ground-following lava stream.
[RequireComponent(typeof(Enemy))]
public sealed class CraterBossAttacks : MonoBehaviour
{
    public float throwDamage=12f, flowDamage=10f;
    public float windUp=1.4f, blastRadius=2.8f, flowLength=18f, flowHalfWidth=1.45f;
    public string Phase { get; private set; } = "Idle";
    public int Throws { get; private set; }
    public int Flows { get; private set; }
    public int PlayerHits { get; private set; }
    public int ActiveEffects => effects.Count;
    Enemy enemy;
    BossAttackMotion motion;
    BossAttacks brain;
    PlayerHealth target;
    Material molten,crust,flowMaterial;
    readonly List<GameObject> effects=new List<GameObject>();
    bool busy;
    void Awake()
    {
        enemy=GetComponent<Enemy>();motion=GetComponent<BossAttackMotion>();brain=GetComponent<BossAttacks>();
        molten=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        molten.SetColor("_BaseColor",new Color(1,.24f,.015f));molten.SetFloat("_Smoothness",.6f);
        molten.EnableKeyword("_EMISSION");molten.SetColor("_EmissionColor",new Color(2.5f,.5f,.015f));
        flowMaterial=new Material(Resources.Load<Shader>("FX/CraterLava"));
        crust=new Material(molten);crust.SetColor("_BaseColor",new Color(.12f,.035f,.025f));
        crust.SetColor("_EmissionColor",Color.black);crust.SetFloat("_Smoothness",.25f);
    }
    bool Ready()
    {
        if(target==null && enemy.Player!=null)target=enemy.Player.GetComponent<PlayerHealth>();
        return isActiveAndEnabled && (brain==null || brain.isActiveAndEnabled) && !enemy.IsDying && RunStats.Instance!=null && RunStats.Instance.Running && target!=null && !target.IsDead;
    }
    void Update(){if(busy && !Ready())Cancel();}
    Vector3 Ground(Vector3 p){Vector3 q;return MapNavigation.Sample(p,3,out q)?q:p;}
    Vector3 Hand(){return motion!=null?motion.FistPoint("R"):transform.position+Vector3.up*3;}
    void Begin(string phase)
    {
        Cancel();busy=true;Phase=phase;enemy.externalMovement=true;
        Vector3 d=target.transform.position-transform.position;d.y=0;
        if(d.sqrMagnitude>.01f)transform.forward=d.normalized;
    }
    GameObject Orb(string label, Vector3 pos,float size,bool dark=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=label;go.layer=2;
        Destroy(go.GetComponent<Collider>());go.transform.position=pos;go.transform.localScale=Vector3.one*size;
        go.GetComponent<Renderer>().sharedMaterial=dark?crust:molten;effects.Add(go);return go;
    }
    GameObject Rock(Vector3 pos)
    {
        var root=Orb("LavaBoulder",pos,1.45f);root.GetComponent<Renderer>().sharedMaterial=flowMaterial;
        for(int i=0;i<7;i++)
        {
            float a=i*Mathf.PI*2/7;
            var piece=Orb("CoolingCrust",pos+new Vector3(Mathf.Cos(a)*.52f,(i%2==0?.28f:-.22f),Mathf.Sin(a)*.52f),.65f,true);
            piece.transform.SetParent(root.transform,true);piece.transform.localScale=new Vector3(.48f,.3f,.48f);
        }
        return root;
    }
    void Hit(Vector3 p,float radius,float damage)
    {
        Vector3 d=target.transform.position-p;d.y=0;
        if(d.sqrMagnitude<=radius*radius && Mathf.Abs(target.transform.position.y-p.y)<3f)
        {float before=target.currentHealth;target.TakeDamage(damage);if(target.currentHealth<before)PlayerHits++;}
    }
    void Impact(Vector3 p)
    {
        if(motion!=null)motion.MarkImpact();GameAudio.Play(GameAudio.Sfx.AxeHit);
        HitImpact.Spawn(p+Vector3.up*.25f,Vector3.up,1.65f);
        SwordSweep.Spawn(p+Vector3.up*.1f,Vector3.forward,blastRadius,360,.3f,new Color(1,.45f,.06f),.4f,1.2f);
        if(CameraShake.Instance!=null)CameraShake.Instance.Shake(.18f,.1f);
    }
    public IEnumerator Throw()
    {
        if(!Ready())yield break;Begin("ThrowTell");Throws++;
        Vector3 offset=target.transform.position-transform.position;offset.y=0;
        Vector3 aim=Ground(transform.position+Vector3.ClampMagnitude(offset,26));
        var rock=Rock(Hand());float t=0,nextTell=0;
        while(t<windUp)
        {
            if(!Ready()){Cancel();yield break;}
            if(Time.deltaTime<=0){yield return null;continue;}
            t=Mathf.Min(windUp,t+Time.deltaTime);
            if(motion!=null)motion.Pose("LavaThrowTell",t/windUp);
            rock.transform.position=Hand();rock.transform.localScale=Vector3.one*1.45f*Mathf.Lerp(.3f,1,Mathf.Clamp01(t/windUp*2));
            if(t>=nextTell){SwordSweep.Spawn(aim+Vector3.up*.12f,Vector3.forward,blastRadius,360,blastRadius*.88f,new Color(1,.6f,.08f),.22f,1);nextTell=t+.18f;}
            yield return null;
        }
        Phase="ThrowFlight";Vector3 start=Hand(),previous=start;
        float duration=Mathf.Clamp(Vector3.Distance(start,aim)/15,.55f,1.4f);t=0;
        while(t<duration)
        {
            if(!Ready()){Cancel();yield break;}
            if(Time.deltaTime<=0){yield return null;continue;}
            t=Mathf.Min(duration,t+Time.deltaTime);float u=t/duration;
            Vector3 point=Vector3.Lerp(start,aim+Vector3.up*.45f,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*4);
            Vector3 step=point-previous;bool blocked=false;
            if(step.sqrMagnitude>.00001f)
            {
                float nearest=float.MaxValue;Vector3 contact=point;
                foreach(var hit in Physics.SphereCastAll(previous,.4f,step.normalized,step.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))
                {
                    if(hit.collider.GetComponentInParent<Enemy>()!=null || hit.collider.GetComponentInParent<PlayerHealth>()!=null)continue;
                    if(hit.distance<nearest){nearest=hit.distance;contact=hit.point;blocked=true;}
                }
                if(blocked)point=contact;
            }
            rock.transform.position=point;rock.transform.Rotate(120*Time.deltaTime,65*Time.deltaTime,0);
            if(motion!=null)motion.Pose("LavaThrowRecover",u);
            previous=point;if(blocked)break;yield return null;
        }
        if(!Ready()){Cancel();yield break;}
        Impact(previous);Hit(previous,blastRadius,throwDamage);
        ClearEffects();yield return Recover("LavaThrowRecover",.9f);Cancel();
    }
    public IEnumerator Pour()
    {
        if(!Ready())yield break;Begin("PourTell");Flows++;
        Vector3 start=Ground(transform.position+transform.forward*(enemy.BodyRadiusWorld+.7f));
        var points=new List<Vector3>{start};
        for(int i=0;i<36;i++)
        {
            Vector3 next;
            if(!MapNavigation.MovePoint(points[points.Count-1],transform.forward*(flowLength/36),out next))break;
            if(Vector3.Distance(next,points[points.Count-1])<flowLength/72)break;
            points.Add(next);
        }
        if(points.Count<2){Cancel();yield break;}
        var warning=new GameObject("LavaFlowWarning");effects.Add(warning);
        var line=warning.AddComponent<LineRenderer>();line.sharedMaterial=molten;
        line.positionCount=points.Count*2;line.loop=true;line.useWorldSpace=true;line.startWidth=line.endWidth=.09f;
        line.numCapVertices=4;line.shadowCastingMode=ShadowCastingMode.Off;
        for(int i=0;i<points.Count;i++){line.SetPosition(i,points[i]-transform.right*flowHalfWidth+Vector3.up*.09f);line.SetPosition(points.Count*2-1-i,points[i]+transform.right*flowHalfWidth+Vector3.up*.09f);}
        float t=0;
        while(t<windUp)
        {
            if(!Ready()){Cancel();yield break;}
            if(Time.deltaTime<=0){yield return null;continue;}
            t+=Time.deltaTime;if(motion!=null)motion.Pose("LavaPourTell",Mathf.Clamp01(t/windUp));
            line.startWidth=line.endWidth=.09f+.03f*Mathf.Sin(t*14);yield return null;
        }
        while(Time.timeScale<=0){if(!Ready()){Cancel();yield break;}yield return null;}
        if(!Ready()){Cancel();yield break;}
        line.enabled=false;Phase="LavaFlow";
        var stream=new GameObject("GroundLavaStream");effects.Add(stream);
        var visual=stream.AddComponent<CraterLavaVisual>();
        visual.Build(points,transform.right,flowHalfWidth,flowMaterial);
        t=0;float nextDamage=0;GameAudio.Play(GameAudio.Sfx.AxeHit);
        if(motion!=null)motion.MarkImpact();
        while(t<5)
        {
            if(!Ready()){Cancel();yield break;}
            if(Time.deltaTime<=0){yield return null;continue;}
            t+=Time.deltaTime;float advance=Mathf.Clamp01(t/2.2f)*(points.Count-1);
            float fade=1-Mathf.Clamp01((t-4)/1);
            if(motion!=null)motion.Pose(t<2.2f?"LavaPourHold":"LavaPourRecover",Mathf.Clamp01((t-2.2f)/2.8f));
            visual.Tick(Hand(),t,advance,fade);
            if(t>=nextDamage && fade>.4f)
            {
                for(int i=1;i<points.Count && i<=advance;i++)
                {
                    Vector3 a=points[i-1],b=points[i],p=target.transform.position;
                    Vector3 ab=b-a;float u=Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.001f,ab.sqrMagnitude));
                    Vector3 closest=a+ab*u;Vector3 d=p-closest;d.y=0;
                    if(d.sqrMagnitude<flowHalfWidth*flowHalfWidth && Mathf.Abs(p.y-closest.y)<2.5f){Hit(closest,flowHalfWidth,flowDamage);nextDamage=t+1;break;}
                }
            }
            yield return null;
        }
        Cancel();
    }
    IEnumerator Recover(string pose,float duration)
    {
        Phase="Recover";float t=0;
        while(t<duration){if(!Ready())yield break;if(Time.deltaTime>0){t+=Time.deltaTime;if(motion!=null)motion.Pose(pose,1);}yield return null;}
    }
    void ClearEffects(){foreach(var go in effects)if(go!=null)Destroy(go);effects.Clear();}
    public void Cancel(){busy=false;Phase="Idle";ClearEffects();if(enemy!=null)enemy.externalMovement=false;if(motion!=null)motion.Release();}
    void OnDisable(){StopAllCoroutines();Cancel();}
    void OnDestroy(){if(molten!=null)Destroy(molten);if(crust!=null)Destroy(crust);if(flowMaterial!=null)Destroy(flowMaterial);}
}
