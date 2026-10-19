using UnityEngine;

/// <summary>
/// Plays the game by itself so a run can be captured to video without a human at the
/// keyboard. Injected mouse and key events never reach an unfocused editor, so this
/// drives the CharacterController and the attack directly instead.
///
/// This is a capture tool, not part of the game: it lives on the player only while
/// recording footage, and each scenario below isolates the sounds a foley pass needs.
/// </summary>
public class DemoDriver : MonoBehaviour
{
    public enum Scenario
    {
        Combat,     // walk into the swarm and keep swinging — swings, hits, deaths, hurt
        Dash,       // repeated dashes with nothing else happening
        Boss,       // stand and fight the boss
        Idle        // walking only, for room tone
    }

    public Scenario scenario = Scenario.Combat;

    // Footage has to survive long enough to reach the boss, and a bot plays worse than a
    // person. These soften only the monsters already spawned in the scene, so the prefabs
    // and the balance that ships stay exactly as they are.
    public bool softenMonsters = true;
    public float damageCap = 4f;
    public float healPerSecond = 6f;
    public bool softenBoss = false;
    public float moveSpeed = 5f;
    public bool attack = true;
    // how often the walk direction is re-picked, so the footage does not look robotic
    public float turnInterval = 1.6f;

    private CharacterController cc;
    private PlayerAutoAttack attackComp;
    private PlayerDash dash;
    private System.Reflection.MethodInfo swingMethod;
    private System.Reflection.FieldInfo cooldownField;
    private System.Reflection.MethodInfo dashMethod;

    private float turnTimer;
    private Vector3 heading = Vector3.forward;
    private float dashTimer;
    private float softenTimer;
    private PlayerHealth health;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        attackComp = GetComponent<PlayerAutoAttack>();
        dash = GetComponent<PlayerDash>();

        swingMethod = typeof(PlayerAutoAttack).GetMethod("Swing",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, System.Type.EmptyTypes, null);
        cooldownField = typeof(PlayerAutoAttack).GetField("manualCooldownTimer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dashMethod = typeof(PlayerDash).GetMethod("StartDash",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        heading = transform.forward;
        health = GetComponent<PlayerHealth>();

        // These lookups break whenever PlayerAutoAttack.Swing changes shape, and a silent
        // return meant a measurement run quietly produced zero damage for 40 seconds.
        if (swingMethod == null || cooldownField == null)
            Debug.LogError("DemoDriver: attack reflection failed (swing=" + (swingMethod != null)
                + " cooldown=" + (cooldownField != null) + "). Measurements will read zero.");
    }

    void Update()
    {
        if (Time.timeScale <= 0f) return;

        if (softenMonsters) SoftenMonsters();
        PickHeading();
        Steer();
        Walk();

        if (scenario == Scenario.Dash) DriveDash();
        if (attack && scenario != Scenario.Dash && scenario != Scenario.Idle) DriveAttack();
    }

    /// <summary>Caps contact damage on live monsters and trickles health back.</summary>
    void SoftenMonsters()
    {
        if (health != null && healPerSecond > 0f) health.Heal(healPerSecond * Time.deltaTime);

        // every half second is plenty; monsters only change when new ones spawn
        softenTimer -= Time.deltaTime;
        if (softenTimer > 0f) return;
        softenTimer = 0.5f;

        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (e.isBoss && !softenBoss) continue;
            if (e.contactDamage > damageCap) e.contactDamage = damageCap;
        }
    }

    void PickHeading()
    {
        turnTimer -= Time.deltaTime;
        if (turnTimer > 0f) return;
        turnTimer = turnInterval;

        // head for the nearest monster in combat, wander otherwise
        if (scenario == Scenario.Combat || scenario == Scenario.Boss)
        {
            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (scenario == Scenario.Boss && !e.isBoss) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d < best) { best = d; nearest = e; }
            }
            if (nearest != null)
            {
                heading = nearest.transform.position - transform.position;
                heading.y = 0f;
                if (heading.sqrMagnitude > 0.001f) heading.Normalize();
                return;
            }
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        heading = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    void Steer()
    {
        if (heading.sqrMagnitude < 0.001f) return;
        // turn the body, so swings land where the camera is looking
        transform.forward = Vector3.Slerp(transform.forward, heading, 8f * Time.deltaTime);
    }

    void Walk()
    {
        Vector3 move = heading * moveSpeed;
        move.y = -4f;                    // stay pinned to the ground
        if (cc != null) cc.Move(move * Time.deltaTime);
        else transform.position += heading * moveSpeed * Time.deltaTime;
    }

    void DriveAttack()
    {
        if (attackComp == null || swingMethod == null || cooldownField == null) return;
        float cd = (float)cooldownField.GetValue(attackComp);
        if (cd > 0f) return;

        swingMethod.Invoke(attackComp, null);
    }

    void DriveDash()
    {
        if (dash == null || dashMethod == null) return;
        dashTimer -= Time.deltaTime;
        if (dashTimer > 0f) return;
        dashTimer = dash.cooldown + 0.4f;
        dashMethod.Invoke(dash, new object[] { heading });
    }
}
