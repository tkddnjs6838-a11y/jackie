using UnityEngine;

/// <summary>
/// The floor pickup that grants a timed effect. Deliberately built like the health orb —
/// same bob, spin, magnet and distance-check pickup — so players read it as "grab this"
/// without being taught anything new. The colour is what says which one it is.
/// </summary>
public class PowerUp : MonoBehaviour
{
    public PowerUpEffects.Kind kind = PowerUpEffects.Kind.Rage;
    public float lifetime = 22f;
    public float pickupRadius = 1f;

    [Header("Feel")]
    public float magnetRadius = 3.2f;
    public float magnetSpeed = 6.5f;
    public float bobHeight = 0.2f;
    public float bobSpeed = 2.2f;
    public float spinSpeed = 120f;
    public float blinkFrom = 4f;

    private Transform player;
    private Transform core;
    private Transform shell;
    private Material coreMat, shellMat;
    private float age;
    private float baseY;

    /// <summary>How many are lying around, so the dropper can keep the field readable.</summary>
    public static int ActiveCount { get; private set; }

    public static void Spawn(Vector3 position, PowerUpEffects.Kind kind)
    {
        if(!MapNavigation.DropPoint(position,out position))return ;
        var go = new GameObject("PowerUp_" + PowerUpEffects.NameOf(kind));
        go.SetActive(false);
        go.transform.position = position;
        go.AddComponent<PowerUp>().kind = kind;
        go.SetActive(true);
    }

    /// <summary>Picks one at random — every kind is equally worth having.</summary>
    public static PowerUpEffects.Kind RandomKind()
    {
        var values = System.Enum.GetValues(typeof(PowerUpEffects.Kind));
        return (PowerUpEffects.Kind)values.GetValue(Random.Range(0, values.Length));
    }

    void OnEnable()=>ActiveCount++;
    void OnDisable()=>ActiveCount--;

    void OnDestroy()
    {
        if (coreMat != null) Destroy(coreMat);
        if (shellMat != null) Destroy(shellMat);
    }

    void Awake()
    {

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        Color tint = PowerUpEffects.ColorOf(kind);
        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");

        shell = MakeBall("Shell", .34f, shader, tint * 0.7f, out shellMat);
        core = MakeBall("Core", .18f, shader, tint, out coreMat);

        core.localPosition=shell.localPosition=Vector3.up*.4f;
        PickupCrateVisual.Build(transform,PickupCrateVisual.Category.Timed,tint);
        core.gameObject.SetActive(false);shell.gameObject.SetActive(false);
        gameObject.AddComponent<TimedPickupMarker>().kind=kind;
        baseY = transform.position.y + 0.6f;
        var pos = transform.position;
        pos.y = baseY;
        transform.position = pos;

        DamageNumber.Create(transform.position + Vector3.up * 0.9f, PowerUpEffects.NameOf(kind), tint);
    }

    Transform MakeBall(string name, float scale, Shader shader, Color color, out Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * scale;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = FxShapes.Sphere;

        var mr = go.AddComponent<MeshRenderer>();
        var template=Resources.Load<Material>("AdditiveFX");
        mat = template!=null?new Material(template):new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 2.5f);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    void Update()
    {
        if (Time.timeScale <= 0f || RunStats.Instance==null || !RunStats.Instance.Running) return;
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(age * bobSpeed) * bobHeight;
        transform.position = pos;

        if (age > lifetime - blinkFrom)
        {
            // blink faster as it runs out, so a player can tell it is about to go
            float blink = Mathf.PingPong(age * 6f, 1f) > 0.5f ? 1f : 0.25f;
            SetAlpha(blink);
        }

        if (player == null) return;

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;

        if (dist < magnetRadius && MapNavigation.CanCollect(transform.position,player.position))
        {
            var next=transform.position+flat.normalized*Mathf.Min(dist,magnetSpeed*Time.deltaTime);
            if(MapNavigation.Current==null)transform.position=next;
            else if(MapNavigation.Sample(next,1.5f,out var floor)){baseY=floor.y+.75f;transform.position=new Vector3(floor.x,baseY,floor.z);}
        }

        if (dist <= pickupRadius && MapNavigation.CanCollect(transform.position,player.position))
        {
            var effects=player.GetComponent<PowerUpEffects>();
            var health=player.GetComponent<PlayerHealth>();
            if(effects==null || !effects.isActiveAndEnabled || health==null || health.IsDead)return;
            effects.Activate(kind);
            DamageNumber.Create(player.position+Vector3.up*1.5f,PowerUpEffects.DisplayName(kind)+" 획득",PowerUpEffects.ColorOf(kind));
            GameAudio.Play(GameAudio.Sfx.Pickup);
            HitImpact.Spawn(transform.position, Vector3.up, 1.2f);
            Destroy(gameObject);
        }
    }

    void SetAlpha(float a)
    {
        if (coreMat != null)
        {
            var c = PowerUpEffects.ColorOf(kind)*a; coreMat.color = c;
            if (coreMat.HasProperty("_BaseColor")) coreMat.SetColor("_BaseColor", c);
        }
        if (shellMat != null)
        {
            var c = PowerUpEffects.ColorOf(kind)*(a*.7f); shellMat.color = c;
            if (shellMat.HasProperty("_BaseColor")) shellMat.SetColor("_BaseColor", c);
        }
    }
}
