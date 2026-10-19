using System.Collections;
using UnityEngine;

// Four reusable crystals: idle orbit, readable aimed volley, then a temporary guard.
[RequireComponent(typeof(Enemy))]
public sealed class CrystalBossCrystals : MonoBehaviour
{
    public float projectileDamage = 18f;
    public float projectileSpeed = 13f;
    public float guardDuration = 2.2f;
    [Range(0, .9f)] public float guardReduction = .65f;
    public string Phase { get; private set; } = "Orbit";
    public int ShotsFired { get; private set; }
    public int PlayerHits { get; private set; }
    public bool Guarding { get; private set; }
    readonly Transform[] crystals = new Transform[4];
    readonly LineRenderer[] trails = new LineRenderer[4];
    readonly bool[] flying = new bool[4];
    readonly Vector3[] velocity = new Vector3[4];
    readonly float[] life = new float[4];
    readonly RaycastHit[] hits = new RaycastHit[64];
    Enemy enemy;
    BossAttackMotion motion;
    PlayerHealth target;
    Mesh mesh;
    Material[] materials;
    LineRenderer shield;
    Mesh guardMesh;
    MeshRenderer guardRenderer;
    Material guardMaterial;
    readonly Vector3[] guardVertices = new Vector3[5];
    float lastHealth;
    float guardFlash;
    float clock;
    bool busy;

    void Awake()
    {
        enemy = GetComponent<Enemy>(); motion = GetComponent<BossAttackMotion>();
        mesh = BuildPrism();
        materials = new Material[4];
        Color[] colors = {new Color(.95f,.4f,.72f),new Color(.35f,1f,.83f),new Color(1f,.65f,.55f),new Color(.65f,.5f,1f)};
        for (int i=0;i<4;i++)
        {
            var go = new GameObject("FloatingCrystal_"+i); go.transform.SetParent(transform,false);
            crystals[i]=go.transform;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor",colors[i]); mat.SetFloat("_Smoothness",.78f);
            mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",colors[i]*.55f);
            materials[i]=mat;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            trails[i]=MakeLine("CrystalTrail_"+i,mat,.08f);trails[i].positionCount=2;trails[i].enabled=false;
            crystals[i].position=Orbit(i);
        }
        shield=MakeLine("CrystalGuard",materials[1],.13f);shield.loop=true;shield.positionCount=4;shield.enabled=false;
        shield.startWidth=shield.endWidth=.16f;
        var panel=new GameObject("ConnectedCrystalShield");panel.transform.SetParent(transform,false);
        guardMesh=new Mesh();guardMesh.name="FourCrystalShield";guardMesh.MarkDynamic();
        guardMesh.vertices=guardVertices;
        guardMesh.uv=new[]{new Vector2(.5f,.5f),new Vector2(1,.5f),new Vector2(.5f,1),new Vector2(0,.5f),new Vector2(.5f,0)};
        guardMesh.triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,1};
        panel.AddComponent<MeshFilter>().sharedMesh=guardMesh;
        guardMaterial=new Material(Resources.Load<Shader>("FX/CrystalGuard"));
        guardMaterial.SetColor("_Color",new Color(.3f,.8f,.95f,.24f));
        guardRenderer=panel.AddComponent<MeshRenderer>();guardRenderer.sharedMaterial=guardMaterial;
        guardRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        guardRenderer.receiveShadows=false;guardRenderer.enabled=false;
        lastHealth=enemy.CurrentHealth;
    }
    LineRenderer MakeLine(string label,Material material,float width)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
        line.startWidth=width;line.endWidth=width*.3f;line.numCapVertices=3;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
    }
    Vector3 Orbit(int i)
    {
        float a=clock*.35f+i*Mathf.PI*.5f;
        return transform.position+new Vector3(Mathf.Cos(a)*3.3f,4.6f+Mathf.Sin(a*2f)*.3f,Mathf.Sin(a)*2.1f);
    }
    bool Ready()
    {
        if(target==null && enemy.Player!=null)target=enemy.Player.GetComponent<PlayerHealth>();
        return isActiveAndEnabled && !enemy.IsDying && RunStats.Instance!=null && RunStats.Instance.Running && target!=null && !target.IsDead;
    }
    void Update()
    {
        if(!Ready()){if(busy)Cancel();return;}
        if(Time.deltaTime<=0)return;
        clock+=Time.deltaTime;
        for(int i=0;i<4;i++)
        {
            if(flying[i]){StepFlight(i,Time.deltaTime);continue;}
            Vector3 wanted=Orbit(i);
            if(Guarding)
            {
                float a=i*Mathf.PI*.5f;
                wanted=transform.position+Vector3.up*2.7f+transform.forward*2.6f+transform.right*Mathf.Cos(a)*2.8f+Vector3.up*Mathf.Sin(a)*2.4f;
            }
            crystals[i].position=Vector3.Lerp(crystals[i].position,wanted,1-Mathf.Exp(-Time.deltaTime*7));
            crystals[i].rotation=Quaternion.Euler(12*Mathf.Sin(clock+i),clock*30+i*45,Guarding?45:12);
            if(Guarding)shield.SetPosition(i,crystals[i].position);
        }
        if(Guarding)
        {
            Vector3 center=Vector3.zero;
            for(int i=0;i<4;i++){guardVertices[i+1]=transform.InverseTransformPoint(crystals[i].position);center+=guardVertices[i+1];}
            guardVertices[0]=center*.25f;
            guardMesh.vertices=guardVertices;guardMesh.RecalculateBounds();
            if(enemy.CurrentHealth<lastHealth)guardFlash=1;
            guardFlash=Mathf.MoveTowards(guardFlash,0,Time.deltaTime*5);
            guardMaterial.SetFloat("_Flash",guardFlash);
        }
        lastHealth=enemy.CurrentHealth;
    }
    void StepFlight(int i,float dt)
    {
        Vector3 from=crystals[i].position;
        Vector3 delta=velocity[i]*dt;
        float nearest=delta.magnitude;Collider collider=null;
        int count=Physics.SphereCastNonAlloc(from,.32f,delta.normalized,hits,nearest,~0,QueryTriggerInteraction.Ignore);
        // A full buffer is treated as blocked, never as permission to shoot through geometry.
        if(count==hits.Length){ReturnCrystal(i);return;}
        for(int h=0;h<count;h++)
        {
            var c=hits[h].collider;
            if(c.transform.IsChildOf(transform) || c.GetComponentInParent<Enemy>()!=null)continue;
            if(hits[h].distance<=nearest){nearest=hits[h].distance;collider=c;}
        }
        crystals[i].position=from+delta.normalized*nearest;
        trails[i].SetPosition(0,crystals[i].position);trails[i].SetPosition(1,crystals[i].position-velocity[i].normalized*1.6f);
        life[i]-=dt;
        if(collider!=null)
        {
            var hp=collider.GetComponentInParent<PlayerHealth>();
            if(hp!=null){float before=hp.currentHealth;hp.TakeDamage(projectileDamage);if(hp.currentHealth<before)PlayerHits++;}
            HitImpact.Spawn(crystals[i].position,-velocity[i].normalized,.65f);
            ReturnCrystal(i);
        }
        else if(life[i]<=0)ReturnCrystal(i);
    }
    void ReturnCrystal(int i){flying[i]=false;trails[i].enabled=false;}
    public IEnumerator Volley()
    {
        if(!Ready())yield break;
        busy=true;enemy.externalMovement=true;Phase="Aim";
        Vector3 aim=target.transform.position+Vector3.up*.9f;
        var direction=aim-transform.position;direction.y=0;
        if(direction.sqrMagnitude>.01f)transform.forward=direction.normalized;
        float t=0;
        while(t<.9f)
        {
            if(!Ready()){Cancel();yield break;}
            t+=Time.deltaTime;if(motion!=null)motion.Pose("ChargeTell",t/.9f);
            for(int i=0;i<4;i++){trails[i].enabled=true;trails[i].SetPosition(0,crystals[i].position);trails[i].SetPosition(1,aim);}
            yield return null;
        }
        for(int i=0;i<4;i++)trails[i].enabled=false;
        Phase="Volley";
        for(int i=0;i<4;i++)
        {
            if(!Ready()){Cancel();yield break;}
            while(Time.timeScale<=0){if(!Ready()){Cancel();yield break;}yield return null;}
            velocity[i]=(aim-crystals[i].position).normalized*projectileSpeed;
            crystals[i].rotation=Quaternion.FromToRotation(Vector3.up,velocity[i]);
            flying[i]=true;life[i]=2;trails[i].enabled=true;ShotsFired++;
            t=0;while(t<.22f){if(!Ready()){Cancel();yield break;}t+=Time.deltaTime;yield return null;}
        }
        while(flying[0]||flying[1]||flying[2]||flying[3]){if(!Ready()){Cancel();yield break;}yield return null;}
        Phase="Recover";t=0;
        while(t<.7f){if(!Ready()){Cancel();yield break;}t+=Time.deltaTime;if(motion!=null)motion.Pose("ChargeRecover",t/.7f);yield return null;}
        Cancel();
    }
    public IEnumerator Defend()
    {
        if(!Ready())yield break;
        busy=true;enemy.externalMovement=true;Phase="Guard";Guarding=true;shield.enabled=true;guardRenderer.enabled=true;lastHealth=enemy.CurrentHealth;
        // The visual assembles before protection activates.
        float t=0;
        while(t<guardDuration)
        {
            if(!Ready()){Cancel();yield break;}
            t+=Time.deltaTime;
            if(t>=.35f)enemy.SetShield(this,guardReduction);
            if(motion!=null)motion.Pose("ChargeTell",Mathf.Clamp01(t/.5f));
            yield return null;
        }
        Cancel();
    }
    public void Cancel()
    {
        Guarding=false;busy=false;Phase="Orbit";
        if(enemy!=null){enemy.RemoveShield(this);enemy.externalMovement=false;}
        if(motion!=null)motion.Release();
        if(shield!=null)shield.enabled=false;
        if(guardRenderer!=null)guardRenderer.enabled=false;guardFlash=0;
        for(int i=0;i<4;i++)if(trails[i]!=null)ReturnCrystal(i);
    }
    void OnDisable(){StopAllCoroutines();Cancel();}
    void OnDestroy(){if(guardMesh!=null)Destroy(guardMesh);if(guardMaterial!=null)Destroy(guardMaterial);if(mesh!=null)Destroy(mesh);if(materials!=null)foreach(var m in materials)if(m!=null)Destroy(m);}
    static Mesh BuildPrism()
    {
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        Vector3[] ring=new Vector3[12];
        for(int i=0;i<6;i++){float a=i*Mathf.PI/3;ring[i]=new Vector3(Mathf.Cos(a)*.38f,-.45f,Mathf.Sin(a)*.38f);ring[i+6]=new Vector3(Mathf.Cos(a)*.38f,.45f,Mathf.Sin(a)*.38f);}
        for(int i=0;i<6;i++)
        {
            int j=(i+1)%6;Vector3[] face={Vector3.up*1.05f,ring[i+6],ring[j+6],ring[i],ring[j],ring[i+6],ring[j],ring[j+6],ring[i+6],Vector3.down*.9f,ring[j],ring[i]};
            foreach(var v in face){triangles.Add(vertices.Count);vertices.Add(v);}
        }
        for(int t=0;t<triangles.Count;t+=3){int swap=triangles[t+1];triangles[t+1]=triangles[t+2];triangles[t+2]=swap;}
        var result=new Mesh();result.name="FloatingQuartzPrism";result.SetVertices(vertices);result.SetTriangles(triangles,0);result.RecalculateNormals();result.RecalculateBounds();return result;
    }
}
