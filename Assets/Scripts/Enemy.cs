using UnityEngine;

public class Enemy : MonoBehaviour
{
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

    public Transform Player { get { return player; } }
    public float ContactGap { get { return contactGap; } }
    public float BodyRadiusWorld { get { return BodyRadius(); } }

    private float currentHealth;
    private Transform player;
    private float contactTimer;
    private bool isDying;
    private Color baseColor = Color.white;
    private bool hasBaseColor;
    private float cachedRadius = -1f;
    
    
    private WaxShardBurst shardBurst;
private WaxCrackVisual crackVisual;
private Coroutine flashRoutine;

    void Awake()
    {
        
        crackVisual = GetComponent<WaxCrackVisual>();
        if (crackVisual == null)
            
        shardBurst = GetComponent<WaxShardBurst>();
        if (shardBurst == null)
            shardBurst = gameObject.AddComponent<WaxShardBurst>();
crackVisual = gameObject.AddComponent<WaxCrackVisual>();
currentHealth = maxHealth;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        if (!isBoss)
        {
            if (preserveModelColors)
            {
                baseColor = Color.white;
                hasBaseColor = true;
                ClearTint();
            }
            else if (randomizeColor) ApplyRandomColor();
            else ApplyVariantColor();
        }
        else if (OnBossSpawned != null)
            OnBossSpawned(this);
    }

    void ApplyVariantColor()
    {
        baseColor = variantColor;
        hasBaseColor = true;
        SetTint(baseColor);
    }

    void ApplyRandomColor()
    {
        baseColor = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.7f, 1f);
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


    void Update()
    {
        if (isDying || player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        float reach = SurfaceDistanceToPlayer();

        if (reach > contactGap && !externalMovement)
        {
            dir.Normalize();
            // PowerUpEffects.EnemySpeedScale is 1 unless the player is holding a SLOW pickup
            MoveWithObstacles(dir * moveSpeed * PowerUpEffects.EnemySpeedScale * Time.deltaTime);
            transform.forward = dir;
        }
        // snap to the ground every frame, not only while walking — otherwise anything
        // that stops next to the player keeps the height it happened to stop at
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Vector3 grounded = transform.position;
            grounded.y = terrain.SampleHeight(grounded) + terrain.transform.position.y;
            transform.position = grounded;
        }

        // contactTimer is this creature's own attack cooldown, not a wind-up: it charges
        // wherever the creature is and fires the moment it is touching the player. Charging
        // it only while in contact meant a full second of standing still was needed before
        // the first hit, so running into a monster and moving on never cost anything.
        contactTimer += Time.deltaTime;

        if (contactTimer >= contactInterval && SurfaceDistanceToPlayer() <= contactGap + contactGrace)
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
        if (cachedRadius >= 0f) return cachedRadius;

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            var s = transform.lossyScale;
            cachedRadius = capsule.radius * Mathf.Max(s.x, s.z);
        }
        else
        {
            cachedRadius = 0.5f;
        }
        return cachedRadius;
    }

    public void TakeDamage(float amount)
    {
        if (isDying) return;

        amount *= Mathf.Max(0f, damageTakenMultiplier);

        var waddle = GetComponent<CreatureWaddle>();
        if (waddle != null)
            waddle.PunchSquish();
        

        if (crackVisual != null)
            

        if (shardBurst != null)
            shardBurst.Burst(hasBaseColor ? baseColor : Color.white, currentHealth <= 0f);
crackVisual.SetDamage(1f - HealthFraction);
currentHealth -= amount;

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

    System.Collections.IEnumerator HitFlash()
    {
        SetTint(Color.white);
        yield return new WaitForSeconds(hitFlashDuration);

        if (isDying) yield break;

        if (hasBaseColor)
            SetTint(baseColor);
        else
            ClearTint();
    }

void Die()
    {
        
        SetTint(InnerSlimeColor());
        if (crackVisual != null) crackVisual.ShowCorpseCracks();
isDying = true;
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
            if (behaviour != this && !(behaviour is WaxCrackVisual))
                behaviour.enabled = false;
        }

        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(startScale.x * 1.85f, startScale.y * 0.025f, startScale.z * 1.85f);
        float t = 0f;

        while (t < squishDuration)
        {
            t += Time.deltaTime;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / squishDuration), 3f);
            transform.localScale = Vector3.Lerp(startScale, endScale, eased);
            yield return null;
        }

        transform.localScale = endScale;
        yield return new WaitForSeconds(corpseLifetime);
        Destroy(gameObject);
    }


void MoveWithObstacles(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= 0.0001f) return;

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
        if (preserveModelColors)
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
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return false;

        Bounds bounds = capsule.bounds;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.86f);
        float halfLine = Mathf.Max(0f, bounds.extents.y - radius);
        Vector3 pointA = bounds.center + Vector3.up * halfLine;
        Vector3 pointB = bounds.center - Vector3.up * halfLine;

        RaycastHit[] hits = Physics.CapsuleCastAll(pointA, pointB, radius, direction,
                                                   distance + 0.04f, ~0,
                                                   QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        bool found = false;

        foreach (var candidate in hits)
        {
            Collider other = candidate.collider;
            if (other == null || other == capsule || other is TerrainCollider) continue;
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
