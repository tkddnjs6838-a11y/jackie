using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    static readonly Unity.Profiling.ProfilerMarker CostMarker = new Unity.Profiling.ProfilerMarker("Combat.Damage");

    public static bool BossDefeated { get; private set; }
    public static event System.Action<Enemy> OnEnemyDied;
    public static event System.Action OnBossDefeated;
    public static event System.Action<Enemy> OnBossSpawned;

    public float CurrentHealth { get { return currentHealth; } }

    public bool IsDying { get { return isDying; } }
    public float HealthFraction { get { return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f; } }

    public static void ResetRunState()
    {
        BossDefeated = false;
    }

    public float moveSpeed = 3f;
    public float maxHealth = 40f;
    public float contactDamage = 20f;
    public float contactInterval = 1f;
    // Gap left between this creature's own body surface and the player before it stops
    // and starts hitting. Measured surface-to-player, not root-to-root, so a 7.5m boss
    // and a 0.9m blob both have to actually reach the player to land a hit.
    public float contactGap = 0.45f;
    // A creature stops the instant it is inside contactGap, so it ends up sitting exactly
    // on that boundary, where collision jitter nudges it a few millimetres in and out.
    // This slack keeps the hit check from failing on those frames.
    public float contactGrace = 0.15f;
    public bool isBoss = false;
    // variants set their own colour so the player can read the threat at a glance;
    // the plain monster keeps the old random hue
    public bool randomizeColor = true;
    public Color variantColor = Color.white;
    [Tooltip("Keep the imported model's textured material instead of replacing it with a flat tint.")]
    public bool preserveModelColors = false;

    public float corpseLifetime = 5f;
    public float squishDuration = 0.4f;
    public float hitFlashDuration = 0.18f;
    public float hitShakeAmount = 0.03f;

    // Hooks the variant behaviours drive:
    // damageTakenMultiplier lets a Warden shield its neighbours,
    // externalMovement hands the chase over to a Charger.
    [HideInInspector] public float damageTakenMultiplier = 1f;

    [HideInInspector] public bool contactDamageBalanced;
    [HideInInspector] public bool externalMovement;

    private readonly System.Collections.Generic.Dictionary<MonoBehaviour, float> shields = new System.Collections.Generic.Dictionary<MonoBehaviour, float>();
    public void SetShield(MonoBehaviour source, float reduction) { shields[source] = Mathf.Clamp01(reduction); }
    public void RemoveShield(MonoBehaviour source) { shields.Remove(source); }
    public float EffectiveDamageMultiplier
    {
        get
        {
            float protection = 0f;
            foreach (var pair in shields)
                if (pair.Key != null && pair.Key.isActiveAndEnabled)
                    protection = Mathf.Max(protection, pair.Value);
            return Mathf.Max(0f, damageTakenMultiplier) * (1f - protection);
        }
    }

    public Transform Player { get { return player; } }
    public float ContactGap { get { return contactGap; } }
    public float BodyRadiusWorld { get { return BodyRadius(); } }

    private float currentHealth;
    private Transform player;
    private float contactTimer;
    private bool isDying;
    private Color baseColor = Color.white;
    private bool hasBaseColor;
    private static readonly System.Collections.Generic.HashSet<Enemy> activeEnemies = new System.Collections.Generic.HashSet<Enemy>();
    public static System.Collections.Generic.IReadOnlyCollection<Enemy> ActiveEnemies => activeEnemies;

    public static int AliveRegularCount
    {
        get
        {
            int count = 0;
            foreach (var enemy in activeEnemies)
                if (enemy != null && !enemy.isBoss && !enemy.isDying) count++;
            return count;
        }
    }

    void OnEnable() { if (!isDying) activeEnemies.Add(this); }
    void OnDisable() { activeEnemies.Remove(this); }


    private WaxShardBurst shardBurst;
    private WaxCrackVisual crackVisual;
    private Coroutine flashRoutine;

    readonly RaycastHit[] obstacleHits=new RaycastHit[128];
    NavMeshPath chasePath;
    readonly Vector3[] chaseCorners=new Vector3[64];
    int cornerCount,cornerIndex;
    float nextPath;
    CapsuleCollider body;
    Vector3 ChaseDirection(Vector3 direct)
    {
        return DirectionTo(player.position);
    }

    public Vector3 DirectionTo(Vector3 destination)
    {
        Vector3 direct=destination-transform.position;direct.y=0;
        if(MapNavigation.Current==null)return direct.normalized;
        if(Time.time>=nextPath)
        {
            nextPath=Time.time+.45f+Mathf.Abs(GetInstanceID()%17)*.01f;
            cornerCount=0;cornerIndex=1;
            if(MapNavigation.Sample(transform.position,1,out var from) && MapNavigation.Sample(destination,3,out var to)
                && NavMesh.CalculatePath(from,to,NavMesh.AllAreas,chasePath))
            {
                cornerCount=chasePath.GetCornersNonAlloc(chaseCorners);
                // Pull boundary corners slightly onto clear floor. Otherwise a tiny
                // rounding error or early turn can aim across the obstacle edge forever.
                for(int c=1;c<cornerCount-1;c++)
                {
                    if(!NavMesh.FindClosestEdge(chaseCorners[c],out var edge,NavMesh.AllAreas))continue;
                    var near=chaseCorners[c]+edge.normal*.3f;
                    if(MapNavigation.Sample(near,.35f,out var inset)
                        && !NavMesh.Raycast(chaseCorners[c-1],inset,out _,NavMesh.AllAreas)
                        && !NavMesh.Raycast(inset,chaseCorners[c+1],out _,NavMesh.AllAreas))chaseCorners[c]=inset;
                }
            }
        }
        while(cornerIndex<cornerCount)
        {
            var delta=chaseCorners[cornerIndex]-transform.position;delta.y=0;
            // A path corner lies on the baked obstacle boundary. Requiring exact contact
            // can strand the walker a few centimetres short after NavMesh.Raycast clamps it.
            float tolerance=cornerIndex<cornerCount-1?.06f:.03f;
            if(delta.sqrMagnitude>tolerance*tolerance)return delta.normalized*Mathf.Min(1f,delta.magnitude/(Mathf.Max(moveSpeed,4.5f)*Mathf.Max(Time.deltaTime,.001f)));
            cornerIndex++;
        }
        return Vector3.zero;
    }
    void Awake()
    {
        chasePath=new NavMeshPath();
        body=GetComponent<CapsuleCollider>();
        nextPath=Time.time+Mathf.Abs(GetInstanceID()%23)*.015f;
        crackVisual = GetComponent<WaxCrackVisual>();
        if (crackVisual == null)
            crackVisual = gameObject.AddComponent<WaxCrackVisual>();

        crackVisual.ClearInheritedVisuals();

        shardBurst = GetComponent<WaxShardBurst>();
        if (shardBurst == null)
            shardBurst = gameObject.AddComponent<WaxShardBurst>();

        currentHealth = maxHealth;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        {
            if (preserveModelColors || isBoss)
            {
                baseColor = Color.white;
                hasBaseColor = true;
                ClearTint();
            }
            else if (randomizeColor) ApplyRandomColor();
            else ApplyVariantColor();
        }
        if (isBoss && OnBossSpawned != null)
            OnBossSpawned(this);
    }

    void ApplyVariantColor()
    {
        baseColor = variantColor;
        hasBaseColor = true;
        SetTint(baseColor);
    }

    // Soft candy colors that sit together on every map; fully random hues read as clashing toy plastic.
    static readonly Color[] RandomPalette =
    {
        new Color(1f, 0.54f, 0.48f),   // coral
        new Color(1f, 0.72f, 0.42f),   // apricot
        new Color(1f, 0.85f, 0.4f),    // butter
        new Color(0.5f, 0.85f, 0.69f), // mint
        new Color(0.49f, 0.77f, 0.95f),// sky
        new Color(0.7f, 0.62f, 0.95f), // lavender
        new Color(0.96f, 0.6f, 0.8f),  // pink
    };

    void ApplyRandomColor()
    {
        Color.RGBToHSV(RandomPalette[Random.Range(0, RandomPalette.Length)], out float hue, out float saturation, out float value);
        // slight per-monster drift so a crowd of the same color isn't identical
        baseColor = Color.HSVToRGB(Mathf.Repeat(hue + Random.Range(-0.015f, 0.015f), 1f), saturation * Random.Range(0.9f, 1.05f), value * Random.Range(0.93f, 1f));
        hasBaseColor = true;
        SetTint(baseColor);
    }

    void SetTint(Color color)
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r is LineRenderer) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor("baseColorFactor", color);
            r.SetPropertyBlock(block);
        }
    }

    void ClearTint()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r is LineRenderer) continue;
            r.SetPropertyBlock(block);
        }
    }

    Color InnerSlimeColor()
    {
        Color shell = hasBaseColor ? baseColor : new Color(0.95f, 0.3f, 0.75f, 1f);
        Color.RGBToHSV(shell, out float hue, out float saturation, out float value);
        return Color.HSVToRGB(hue, Mathf.Max(0.72f, saturation), 1f);
    }


    Vector3 knockbackVelocity;
    public bool IsKnockedBack => knockbackVelocity.sqrMagnitude>.01f;
    public int KnockbackApplications { get; private set; }
    public void ApplyKnockback(Vector3 direction,float speed)
    {
        if(isDying || speed<=0)return;
        direction.y=0;
        float resistance=isBoss?.12f:Mathf.Clamp(.55f/Mathf.Max(.55f,BodyRadius()),.25f,1f);
        knockbackVelocity=direction.normalized*speed*resistance;
        KnockbackApplications++;
    }
    void TickKnockback()
    {
        float speed=knockbackVelocity.magnitude;
        if(speed<=.001f)return;
        float next=Mathf.Max(0,speed-32f*Time.deltaTime);
        float duration=Mathf.Min(Time.deltaTime,speed/32f);
        MoveWithObstacles(knockbackVelocity/speed*((speed+next)*.5f*duration));
        knockbackVelocity=knockbackVelocity/speed*next;
    }

    void Update()
    {
        if (Time.timeScale <= 0f || player == null) return;
        bool pushed=IsKnockedBack;
        TickKnockback();
        if(isDying)return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        float reach = SurfaceDistanceToPlayer();

        if (reach > contactGap && !externalMovement && !pushed)
        {
            dir=ChaseDirection(dir);
            // PowerUpEffects.EnemySpeedScale is 1 unless the player is holding a SLOW pickup
            MoveWithObstacles(dir * moveSpeed * PowerUpEffects.EnemySpeedScale * Time.deltaTime);
            if(dir.sqrMagnitude>.001f)transform.forward = dir;
        }
        // snap to the ground every frame, not only while walking — otherwise anything
        // that stops next to the player keeps the height it happened to stop at
        var terrain = Terrain.activeTerrain;
        if (terrain != null && MapNavigation.Current==null)
        {
            Vector3 grounded = transform.position;
            grounded.y = terrain.SampleHeight(grounded) + terrain.transform.position.y;
            transform.position = grounded;
        }

        // contactTimer is this creature's own attack cooldown, not a wind-up: it charges
        // wherever the creature is and fires the moment it is touching the player. Charging
        // it only while in contact meant a full second of standing still was needed before
        // the first hit, so running into a monster and moving on never cost anything.
        // Boss patterns own damage throughout their tell, strike and recovery.
        // Reset the contact cooldown so the recovery remains a safe counterattack window.
        if (isBoss && externalMovement)
        {
            contactTimer = 0f;
            return;
        }
        contactTimer += Time.deltaTime;

        if (!pushed && contactTimer >= contactInterval && SurfaceDistanceToPlayer() <= contactGap + contactGrace)
        {
            contactTimer = 0f;
            var health = player.GetComponent<PlayerHealth>();
            if (health != null)
                health.TakeDamage(contactDamage);
        }
    }

    /// <summary>
    /// Horizontal gap between this creature's body edge and the player.
    /// Uses the capsule radius rather than Collider.ClosestPoint: the capsule's bottom
    /// cap pinches inward at the short player's height, so ClosestPoint let a 7.5m boss
    /// wander far closer on one frame and stop short on the next.
    /// </summary>
    float SurfaceDistanceToPlayer()
    {
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        return flat.magnitude - BodyRadius();
    }

    float BodyRadius()
    {
        var capsule = body!=null?body:(body=GetComponent<CapsuleCollider>());
        if (capsule != null)
        {
            var s = transform.lossyScale;
            return capsule.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        }
        return 0.5f;
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, player != null ? player.position + Vector3.up * 0.7f : transform.position - transform.forward);
    }

    public void TakeDamage(float amount, Vector3 contact, float dentStrength=1f)
    {
        using (CostMarker.Auto())
        {
            if (isDying || amount <= 0f) return;

            amount *= EffectiveDamageMultiplier;

            if (amount <= 0f) return;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            var dent = GetComponent<WaxDent>();
            if (dent == null) dent = gameObject.AddComponent<WaxDent>();
            dent.Punch(contact, 1f - HealthFraction, currentHealth <= 0f, dentStrength);
            if(!isBoss && currentHealth>0f)
            {
                var waddle=GetComponent<CreatureWaddle>();
                if(waddle!=null)waddle.PunchSquish(contact,dentStrength);
            }
            if (crackVisual != null)
                crackVisual.SetDamage(1f - HealthFraction);
            if (shardBurst != null && currentHealth > 0f)
                shardBurst.Burst(hasBaseColor ? baseColor : Color.white, false);

            // put the number at the body's mid-height so it is not down at a giant's ankles
            var col = GetComponent<Collider>();
            Vector3 numberPos = col != null ? col.bounds.center : transform.position + Vector3.up * 1f;
            DamageNumber.Create(numberPos, amount.ToString("F0"), Color.yellow);
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.08f, hitShakeAmount);

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlash());

            if (currentHealth <= 0f)
                Die();
        }
    }

    System.Collections.IEnumerator HitFlash()
    {
        // Imported textured bosses keep their authored material colors.
        if (preserveModelColors || isBoss) yield break;
        // Keep the shell hue readable even under repeated hits.
        SetTint(Color.Lerp(baseColor, Color.white, 0.22f));
        yield return new WaitForSecondsRealtime(Mathf.Min(hitFlashDuration, 0.08f));

        if (isDying) yield break;

        if (hasBaseColor)
            SetTint(baseColor);
        else
            ClearTint();
    }

    void Die()
    {

        if (!isBoss) SetTint(InnerSlimeColor());
        if (crackVisual != null) crackVisual.ShowCorpseCracks();
        isDying = true;
        activeEnemies.Remove(this);
        OnEnemyDied?.Invoke(this);

        if (isBoss)
        {
            BossDefeated = true;
            if (OnBossDefeated != null) OnBossDefeated();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.3f, 0.12f);
        }

        StartCoroutine(SquishAndDestroy());
    }

    System.Collections.IEnumerator SquishAndDestroy()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        foreach (var behaviour in GetComponents<MonoBehaviour>())
        {
            if (behaviour != this && !(behaviour is WaxCrackVisual) && !(behaviour is WaxDent))
                behaviour.enabled = false;
        }

        // Let the impact dent become visible before the shell breaks and the contents flatten.
        yield return new WaitForSeconds(0.07f);
        if (shardBurst != null) shardBurst.Burst(hasBaseColor ? baseColor : Color.white, true);
        Vector3 startScale = transform.localScale;
        float spread = isBoss ? 1.2f : 1.45f;
        float height = isBoss ? 0.28f : 0.1f;
        Vector3 endScale = new Vector3(startScale.x * spread, startScale.y * height, startScale.z * spread);
        float t = 0f;

        while (t < squishDuration)
        {
            t += Time.deltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / squishDuration), 3f);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.localScale = endScale;
        foreach (var animator in GetComponentsInChildren<Animator>()) animator.enabled = false;
        var dent = GetComponent<WaxDent>();
        if (dent != null) dent.FreezeSurface();
        yield return new WaitForSeconds(Application.isMobilePlatform ? Mathf.Min(corpseLifetime, .35f) : corpseLifetime);
        Destroy(gameObject);
    }


    void MoveWithObstacles(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= 0.0001f) return;

        if(MapNavigation.Current!=null)
        {
            if(!MapNavigation.MovePoint(transform.position,displacement,out var target))return;
            displacement=target-transform.position;
            // NavMesh supplies floor height; static obstacle casts still protect larger bodies.
            float floorY=target.y;displacement.y=0;distance=displacement.magnitude;
            if(distance<=.0001f)return;
            if(!FirstBlockingHit(displacement/distance,distance,out var wall))transform.position=new Vector3(target.x,floorY,target.z);
            else
            {
                var navSlide=Vector3.ProjectOnPlane(displacement,wall.normal);navSlide.y=0;
                if(navSlide.sqrMagnitude>.00001f && MapNavigation.MovePoint(transform.position,navSlide,out var around))
                {
                    var flat=around-transform.position;flat.y=0;
                    if(flat.sqrMagnitude>.00001f && !FirstBlockingHit(flat.normalized,flat.magnitude,out _))transform.position=around;
                }
            }
            return;
        }
        Vector3 direction = displacement / distance;
        if (!FirstBlockingHit(direction, distance, out RaycastHit hit))
        {
            transform.position += displacement;
            return;
        }

        float forwardDistance = Mathf.Max(0f, hit.distance - 0.04f);
        transform.position += direction * forwardDistance;

        Vector3 remaining = displacement - direction * forwardDistance;
        Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
        slide.y = 0f;
        float slideDistance = slide.magnitude;
        if (slideDistance <= 0.0001f) return;

        Vector3 slideDirection = slide / slideDistance;
        if (FirstBlockingHit(slideDirection, slideDistance, out RaycastHit slideHit))
            slideDistance = Mathf.Max(0f, slideHit.distance - 0.04f);

        transform.position += slideDirection * slideDistance;
    }

    public void MoveAvoidingObstacles(Vector3 displacement)
    {
        MoveWithObstacles(displacement);
    }

    public void RefreshConfiguredStatsAndColor()
    {

        if (!contactDamageBalanced && !isBoss && GetComponent<EnemyCharger>() == null)
        {
            contactDamage *= 0.75f;
            contactDamageBalanced = true;
        }
        currentHealth = maxHealth;
        if (preserveModelColors || isBoss)
        {
            baseColor = Color.white;
            hasBaseColor = true;
            ClearTint();
        }
        else if (!randomizeColor) ApplyVariantColor();
    }


    bool FirstBlockingHit(Vector3 direction, float distance, out RaycastHit nearest)
    {
        nearest = default;
        var capsule = body!=null?body:(body=GetComponent<CapsuleCollider>());
        if (capsule == null) return false;

        Bounds bounds = capsule.bounds;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.86f);
        float halfLine = Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 pointA = bounds.center + Vector3.up * halfLine;
        Vector3 pointB = bounds.center - Vector3.up * halfLine;
        if(MapNavigation.Current!=null)
        {
            // Several imported capsules extend below their foot pivot. Do not cast
            // that buried half into bridges/ramps and misclassify the floor as a wall.
            pointB.y=Mathf.Max(pointB.y,transform.position.y+radius+.08f);
            pointA.y=Mathf.Max(pointA.y,pointB.y);
        }

        int hitCount=Physics.CapsuleCastNonAlloc(pointA,pointB,radius,direction,obstacleHits,distance+.04f,~0,QueryTriggerInteraction.Ignore);
        if(hitCount==obstacleHits.Length){nearest.distance=0;return true;}
        float nearestDistance = float.MaxValue;
        bool found = false;

        for(int hitIndex=0;hitIndex<hitCount;hitIndex++)
        {
            var candidate=obstacleHits[hitIndex];
            Collider other = candidate.collider;
            if (other == null || other == capsule || other is TerrainCollider) continue;
            if(MapNavigation.Current!=null)
            {
                if(candidate.normal.y>=MapNavigation.WalkableNormalY)continue;
                // A baked, connected step is traversable even when the capsule
                // touches its vertical riser. Tall props still block and require a route.
                if(other.bounds.max.y<=transform.position.y+MapNavigation.StepHeight)continue;
            }
            if (other.transform.IsChildOf(transform)) continue;
            if (other.GetComponentInParent<Enemy>() != null) continue;
            if (player != null && (other.transform == player || other.transform.IsChildOf(player))) continue;

            if (candidate.distance < nearestDistance)
            {
                nearestDistance = candidate.distance;
                nearest = candidate;
                found = true;
            }
        }

        return found;
    }
}
