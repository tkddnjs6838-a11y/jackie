using UnityEngine;
using UnityEngine.Rendering;

// A bounded pool: no materials, colliders or particles are allocated on repeated shots.
[DefaultExecutionOrder(1000)]
public sealed class GunEffects : MonoBehaviour
{
    const int Capacity=48;
    sealed class Stroke { public LineRenderer line; public MeshRenderer pellet; public bool showPellet,smoke; public float tail; public float age,life,width; public Color color; public Vector3 velocity; public Transform anchor; public bool follow, tracer; public Vector3 from,to; }
    static GunEffects instance;
    readonly Stroke[] strokes=new Stroke[Capacity];
    MaterialPropertyBlock block;
    int cursor;
    Material pelletMaterial,smokeMaterial,lineMaterial;
    AnimationCurve flatWidth,flameWidth;
    public static int PoolCapacity => Capacity;
    static GunEffects Pool
    {
        get { if(instance==null)instance=new GameObject("GunEffectPool").AddComponent<GunEffects>();return instance; }
    }
    void Awake()
    {
        instance=this;block=new MaterialPropertyBlock();
        flatWidth=AnimationCurve.Constant(0,1,1);
        flameWidth=new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.22f,1f),new Keyframe(1,0));
        var material=Resources.Load<Material>("AdditiveFX");lineMaterial=material;
        smokeMaterial=new Material(Resources.Load<Shader>("GunSmoke"));
        pelletMaterial=new Material(Resources.Load<Shader>("PelletVisual"));
        for(int i=0;i<Capacity;i++)
        {
            var go=new GameObject("Stroke");go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.numCapVertices=1;line.enabled=false;
            var bead=new GameObject("Pellet");bead.transform.SetParent(transform,false);
            bead.AddComponent<MeshFilter>().sharedMesh=FxShapes.Sphere;
            var renderer=bead.AddComponent<MeshRenderer>();renderer.sharedMaterial=pelletMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
            strokes[i]=new Stroke{line=line,pellet=renderer};
        }
    }
    static Stroke Begin(Color color,float width,float life,Transform anchor=null)
    {
        var pool=Pool;var s=pool.strokes[pool.cursor++%Capacity];
        s.showPellet=false;s.smoke=false;s.tail=.45f;s.pellet.enabled=false;s.line.sharedMaterial=pool.lineMaterial;
        s.age=0;s.life=life;s.tracer=false;s.width=width;s.color=color;s.velocity=Vector3.zero;s.anchor=anchor;s.follow=anchor!=null;
        s.line.transform.SetPositionAndRotation(anchor!=null?anchor.position:Vector3.zero,anchor!=null?anchor.rotation:Quaternion.identity);
        s.line.transform.localScale=Vector3.one;s.line.useWorldSpace=anchor==null;s.line.loop=false;s.line.widthCurve=pool.flatWidth;s.line.widthMultiplier=width;s.line.enabled=true;
        pool.Tint(s,1);return s;
    }
    void Tint(Stroke s,float intensity){block.SetColor("_BaseColor",s.color*intensity);block.SetColor("_Color",s.color*intensity);s.line.SetPropertyBlock(block);}
    public static Color ColorFor(PlayerWeapons.Kind kind)=>kind==PlayerWeapons.Kind.LaserGun?new Color(.12f,.85f,1f):kind==PlayerWeapons.Kind.MachineGun?new Color(1f,.85f,.22f):new Color(1f,.43f,.08f);
    public static void Muzzle(PlayerWeapons.Kind kind,Transform muzzle)
    {
        if(muzzle==null)return;
        if(kind!=PlayerWeapons.Kind.LaserGun)
        {
            bool automatic=kind==PlayerWeapons.Kind.MachineGun;
            float flameLength=automatic?.23f:.78f,life=automatic?.06f:.15f;
            Flame(muzzle,new Color(1f,.30f,.035f),automatic?.12f:.48f,flameLength,life,Vector3.zero);
            Flame(muzzle,new Color(1f,.72f,.22f),automatic?.052f:.075f,flameLength*.60f,life*.8f,Vector3.zero);
            Flame(muzzle,new Color(.85f,.23f,.025f),.05f,flameLength*.65f,life,new Vector3(.075f,.025f,0));
            Flame(muzzle,new Color(.85f,.23f,.025f),.05f,flameLength*.55f,life,new Vector3(-.055f,-.025f,0));
            if(!automatic)Smoke(muzzle);
            return;
        }
        Color color=ColorFor(kind);
        float length=kind==PlayerWeapons.Kind.Shotgun?.31f:kind==PlayerWeapons.Kind.MachineGun?.17f:.38f;
        float width=kind==PlayerWeapons.Kind.MachineGun?.025f:.05f;
        var s=Begin(color,width,kind==PlayerWeapons.Kind.MachineGun?.075f:.12f,muzzle);
        s.line.positionCount=7;
        s.line.SetPosition(0,Vector3.zero);s.line.SetPosition(1,new Vector3(-length*.28f,0,length*.5f));
        s.line.SetPosition(2,Vector3.forward*length*.25f);s.line.SetPosition(3,Vector3.forward*length);
        s.line.SetPosition(4,Vector3.forward*length*.25f);s.line.SetPosition(5,new Vector3(length*.28f,0,length*.5f));s.line.SetPosition(6,Vector3.zero);
    }
    static void Smoke(Transform muzzle)
    {
        for(int i=0;i<3;i++)
        {
            var s=Begin(new Color(.30f,.25f,.21f,.40f),.24f,.30f+i*.035f);
            s.smoke=true;s.line.sharedMaterial=Pool.smokeMaterial;
            var at=muzzle.position+muzzle.forward*(.18f+i*.16f);
            s.line.positionCount=2;s.line.SetPosition(0,at);s.line.SetPosition(1,at+Vector3.up*.24f);
            s.velocity=muzzle.forward*(.6f+i*.2f)+Vector3.up*.7f;
        }
    }
    static void Flame(Transform muzzle,Color color,float width,float length,float life,Vector3 bend)
    {
        var s=Begin(color,width,life,muzzle);s.line.widthCurve=Pool.flameWidth;s.line.positionCount=3;
        s.line.SetPosition(0,Vector3.zero);
        s.line.SetPosition(1,bend*.6f+Vector3.forward*length*.35f);
        s.line.SetPosition(2,bend+Vector3.forward*length);
    }
    public static void Charge(Transform muzzle,float amount)
    {
        var s=Begin(ColorFor(PlayerWeapons.Kind.LaserGun),.018f,.065f,muzzle);s.line.positionCount=17;
        for(int i=0;i<17;i++){float a=i*Mathf.PI/8;s.line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*Mathf.Lerp(.16f,.025f,amount));}
    }
    public static void Beam(PlayerWeapons.Kind kind,Vector3 from,Vector3 to)
    {
        if(kind!=PlayerWeapons.Kind.LaserGun)
        {
            bool shotgun=kind==PlayerWeapons.Kind.Shotgun;
            Tracer(from,to,shotgun?new Color(1f,.72f,.26f):new Color(.9f,.55f,.16f),shotgun?.10f:.045f,shotgun?.24f:.13f,shotgun);
            return;
        }
        var s=Begin(ColorFor(kind),.16f,.28f);
        s.line.positionCount=2;s.line.SetPosition(0,from);s.line.SetPosition(1,to);
        var core=Begin(Color.white*1.4f,.065f,.22f);core.line.positionCount=2;core.line.SetPosition(0,from);core.line.SetPosition(1,to);
    }
    static void Tracer(Vector3 from,Vector3 to,Color color,float width,float life,bool pellet=false)
    {
        var s=Begin(color,width,life);s.showPellet=pellet;s.pellet.enabled=pellet;s.tracer=true;s.tail=pellet?1.05f:.45f;s.from=from;s.to=to;s.line.positionCount=2;PositionTracer(s);
    }
    static void PositionTracer(Stroke s)
    {
        var delta=s.to-s.from;float length=delta.magnitude;
        float head=Mathf.Lerp(Mathf.Min(.35f,length),length,Mathf.Clamp01(s.age/s.life));
        var direction=length>.001f?delta/length:Vector3.forward;
        s.line.SetPosition(0,s.from+direction*Mathf.Max(0,head-s.tail));s.line.SetPosition(1,s.from+direction*head);
        if(s.showPellet)
        {
            s.pellet.transform.position=s.from+direction*head;
            s.pellet.transform.rotation=Quaternion.LookRotation(direction);
            float fade=1-Mathf.InverseLerp(.85f,1,s.age/s.life);
            s.pellet.transform.localScale=new Vector3(.095f,.095f,.17f)*fade;
        }
    }
    public static void Impact(PlayerWeapons.Kind kind,Vector3 at,Vector3 normal)
    {
        var color=ColorFor(kind);var rotation=Quaternion.LookRotation(normal.sqrMagnitude>.01f?normal:Vector3.up);
        if(kind==PlayerWeapons.Kind.LaserGun)
        {
            var ring=Begin(color,.03f,.25f);ring.line.useWorldSpace=false;ring.line.transform.SetPositionAndRotation(at,rotation);ring.line.positionCount=25;
            for(int i=0;i<25;i++){float a=i*Mathf.PI/12;ring.line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.35f);}
        }
        int count=kind==PlayerWeapons.Kind.MachineGun?3:5;
        for(int i=0;i<count;i++)
        {
            var s=Begin(color,kind==PlayerWeapons.Kind.MachineGun?.018f:.028f,kind==PlayerWeapons.Kind.MachineGun?.10f:.18f);
            var direction=rotation*new Vector3(Mathf.Cos(i*2.4f),Mathf.Sin(i*2.4f),.55f).normalized;
            s.line.positionCount=2;s.line.SetPosition(0,at);s.line.SetPosition(1,at+direction*(kind==PlayerWeapons.Kind.MachineGun?.075f:.17f));
            s.velocity=direction*.8f;
        }
    }
    void Update()
    {
        if(Time.timeScale<=0)return;
        foreach(var s in strokes)
        {
            if(!s.line.enabled)continue;
            s.age+=Time.deltaTime;
            if(s.age>=s.life || (s.follow && s.anchor==null)){s.line.enabled=false;s.pellet.enabled=false;continue;}
            if(s.tracer)PositionTracer(s);
            if(s.follow)s.line.transform.SetPositionAndRotation(s.anchor.position,s.anchor.rotation);
            else if(s.velocity!=Vector3.zero)for(int i=0;i<s.line.positionCount;i++)s.line.SetPosition(i,s.line.GetPosition(i)+s.velocity*Time.deltaTime);
            if(!s.follow && !s.line.useWorldSpace)s.line.transform.localScale=Vector3.one*(1+s.age*3);
            float fade=s.tracer?1-Mathf.InverseLerp(.75f,1,s.age/s.life):1-s.age/s.life;
            s.line.widthMultiplier=s.width*(s.smoke?1+s.age*4:s.tracer?1:fade);Tint(s,fade);
        }
    }
    void LateUpdate()
    {
        // Arm IK runs in LateUpdate; follow the final rendered muzzle pose.
        foreach(var s in strokes)
            if(s.line.enabled && s.follow && s.anchor!=null)
                s.line.transform.SetPositionAndRotation(s.anchor.position,s.anchor.rotation);
    }
    void OnDestroy(){if(pelletMaterial!=null)Destroy(pelletMaterial);if(smokeMaterial!=null)Destroy(smokeMaterial);if(instance==this)instance=null;}
}
