using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Hangs back at a distance, telegraphs, then rushes in a straight line. If the rush
/// misses it overshoots and is briefly wide open, taking extra damage — which is what
/// makes the dash worth using instead of just backing away.
/// Drives its own movement, so Enemy.externalMovement is held on while it lives.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyCharger : MonoBehaviour
{
    public float preferredDistance = 9f;
    public float repositionSpeed = 4.5f;
    public float windUpTime = 0.75f;
    public float chargeSpeed = 16f;
    public float chargeDistance = 13f;
    public float recoverTime = 1.1f;
    public float restBetweenCharges = 1.6f;

    [Header("Vulnerability")]
    public float exposedDamageMultiplier = 2f;

    [Header("Look")]
    public Color windUpColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color exposedColor = new Color(1f, 0.4f, 0.4f, 1f);

    private enum State { Reposition, WindUp, Charge, Recover }

    private Enemy enemy;
    private Transform player;
    private State state = State.Reposition;
    private float timer;
    private NavMeshPath repositionPath;
    private readonly Vector3[] repositionCorners=new Vector3[64];
    private Vector3 repositionTarget;
    private float nextReposition;
    private Vector3 chargeDir;
    private float chargeTravelled;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    
    private LineRenderer warningLine;
    private Material warningMaterial;
private Color restColor;

void Awake()
    {
        repositionPath=new NavMeshPath();
        enemy = GetComponent<Enemy>();
        enemy.externalMovement = true;
        // White is a neutral material multiplier, so textured VARCO models keep
        // their original orange/red details while the warning flashes still work.
        restColor = enemy.preserveModelColors ? Color.white : enemy.variantColor;
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        timer = restBetweenCharges;
        CreateWarningLine();
    }void Update()
    {
        if (player == null || enemy.IsKnockedBack || Time.timeScale<=0) return;

        if (warningLine != null)
            warningLine.enabled = state == State.WindUp;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;

        switch (state)
        {
            case State.Reposition: Reposition(toPlayer, dist); break;
            case State.WindUp: WindUp(toPlayer); break;
            case State.Charge: Charge(); break;
            case State.Recover: Recover(); break;
        }

        Ground();
    }

    void Reposition(Vector3 toPlayer, float dist)
    {
        Tint(restColor);
        enemy.damageTakenMultiplier = 1f;

        // hold a ring around the player: close in when far, back off when too near
        Vector3 dir = toPlayer.normalized;
        float error = dist - preferredDistance;
        bool clearAttack=ClearAttackRoute(transform.position,player.position);
        if (Mathf.Abs(error) > 0.6f || !clearAttack)
        {
            // Reposition follows a route around props; only the attack is a straight rush.
            if(Time.time>=nextReposition)
            {
                nextReposition=Time.time+.6f;
                repositionTarget=FindRepositionTarget();
            }
            Vector3 destination=repositionTarget;
            Vector3 route=enemy.DirectionTo(destination);
            enemy.MoveAvoidingObstacles(route * repositionSpeed * PowerUpEffects.EnemySpeedScale * Time.deltaTime);
            transform.forward = dir;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f && clearAttack && (Mathf.Abs(error) <= 2.5f || dist<preferredDistance))
        {
            state = State.WindUp;
            timer = windUpTime;
        }
    }void WindUp(Vector3 toPlayer)
    {
        DrawWarningLine(toPlayer);
        transform.forward = toPlayer.normalized;
        timer -= Time.deltaTime;

        float rate = Mathf.Lerp(5f, 20f, 1f - Mathf.Clamp01(timer / windUpTime));
        Tint(Mathf.Repeat(Time.time * rate, 1f) > 0.5f ? windUpColor : restColor);

        if (timer <= 0f)
        {
            chargeDir = toPlayer.normalized;
            chargeTravelled = 0f;
            state = State.Charge;
        }
    }

    bool ClearAttackRoute(Vector3 from,Vector3 to)
    {
        return MapNavigation.Current==null || (MapNavigation.Sample(from,1,out var a)
            && MapNavigation.Sample(to,3,out var b) && !NavMesh.Raycast(a,b,out _,NavMesh.AllAreas));
    }

    Vector3 FindRepositionTarget()
    {
        Vector3 away=transform.position-player.position;away.y=0;
        if(away.sqrMagnitude<.01f)away=transform.forward;
        if(MapNavigation.Current==null)return player.position+away.normalized*preferredDistance;
        if(!MapNavigation.Sample(transform.position,1,out var from))return transform.position;
        Vector3 best=player.position;float bestLength=float.PositiveInfinity;
        // A straight backward retreat can end inside a pillar or cliff. Pick a
        // reachable firing position around the player, including sideways exits.
        for(int i=0;i<16;i++)
        {
            var candidate=player.position+Quaternion.Euler(0,i*22.5f,0)*away.normalized*preferredDistance;
            if(!MapNavigation.Sample(candidate,3,out candidate) || !MapNavigation.Clear(candidate,.6f)
                || !ClearAttackRoute(candidate,player.position))continue;
            if(!NavMesh.CalculatePath(from,candidate,NavMesh.AllAreas,repositionPath)
                || repositionPath.status!=NavMeshPathStatus.PathComplete)continue;
            int count=repositionPath.GetCornersNonAlloc(repositionCorners);float length=0;
            for(int j=1;j<count;j++)length+=Vector3.Distance(repositionCorners[j-1],repositionCorners[j]);
            if(length<bestLength){bestLength=length;best=candidate;}
        }
        return best;
    }

    void Charge()
    {
        Tint(windUpColor);
        float step = Mathf.Min(chargeSpeed * PowerUpEffects.EnemySpeedScale * Time.deltaTime, chargeDistance - chargeTravelled);
        Vector3 before=transform.position;
        enemy.MoveAvoidingObstacles(chargeDir * step);
        Vector3 moved=transform.position-before;moved.y=0;
        chargeTravelled += moved.magnitude;
        // Hitting a structure ends the rush instead of repeatedly pushing into it.
        if(moved.magnitude<step*.25f)chargeTravelled=chargeDistance;

        if (chargeTravelled >= chargeDistance)
        {
            state = State.Recover;
            timer = recoverTime;
        }
    }

    void Recover()
    {
        // overshot and winded: an opening for the player
        Tint(exposedColor);
        enemy.damageTakenMultiplier = exposedDamageMultiplier;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            enemy.damageTakenMultiplier = 1f;
            state = State.Reposition;
timer = restBetweenCharges;
        }
    }

    void Ground()
    {
        if(MapNavigation.Current!=null)return;
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        Vector3 p = transform.position;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        transform.position = p;
    }

    void Tint(Color color)
    {
        if (renderers == null) return;
        // created lazily as well as in Awake: some instances reached Tint with a null block
        // and threw ArgumentNullException every single frame, which both spammed the log and
        // left the wind-up colour tell dead on those chargers
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("baseColorFactor", color);
            r.SetPropertyBlock(block);
        }
    }

    public string StateName { get { return state.ToString(); } }
    public bool IsExposed { get { return state == State.Recover; } }


void CreateWarningLine()
    {
        var go = new GameObject("ChargeWarning");
        go.transform.SetParent(transform, false);
        warningLine = go.AddComponent<LineRenderer>();
        warningLine.positionCount = 2;
        warningLine.useWorldSpace = true;
        warningLine.startWidth = 0.16f;
        warningLine.endWidth = 0.05f;
        warningLine.numCapVertices = 2;
        warningLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var template = Resources.Load<Material>("AdditiveFX");
        var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
        warningMaterial = new Material(shader);
        warningMaterial.color = windUpColor;
        if (warningMaterial.HasProperty("_BaseColor")) warningMaterial.SetColor("_BaseColor", windUpColor);
        warningLine.sharedMaterial = warningMaterial;
        warningLine.enabled = false;
    }

    void DrawWarningLine(Vector3 toPlayer)
    {
        if (warningLine == null) return;
        Vector3 direction = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : transform.forward;
        Vector3 from = transform.position + Vector3.up * 0.08f;
        warningLine.SetPosition(0, from);
        warningLine.SetPosition(1, from + direction * chargeDistance);
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 24f);
        warningLine.startWidth = Mathf.Lerp(0.10f, 0.24f, pulse);
    }

    void OnDisable()
    {
        if (enemy != null)
        {
            enemy.externalMovement = false;
            enemy.damageTakenMultiplier = 1f;
        }
        if (warningLine != null) warningLine.enabled = false;
    }

    void OnEnable()
    {
        if (enemy != null) enemy.externalMovement = true;
    }

    void OnDestroy()
    {
        if (warningMaterial != null) Destroy(warningMaterial);
    }
}
