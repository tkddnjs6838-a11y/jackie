#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Temporary trailer-capture driver. Plays a scripted run through the stages so the
// recorder can film it; it is not part of the game and is deleted after capture.
[DefaultExecutionOrder(400)]
public sealed class PromoDirector : MonoBehaviour
{
    public const string RunKey = "PromoCapture.Run", StagesKey = "PromoCapture.Stages", MarksKey = "PromoCapture.Marks", FpsKey = "PromoCapture.Fps";
    public static bool WantRecording, Finished;
    public static readonly List<string> Marks = new List<string>();
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    enum Mode { None, Monsters, Boss, Watch }
    enum Cam { Game, Intro, Wide, Chase }

    static readonly PlayerWeapons.Kind[] MonsterWeapon = { PlayerWeapons.Kind.Sword, PlayerWeapons.Kind.Axe, PlayerWeapons.Kind.Shotgun, PlayerWeapons.Kind.MachineGun, PlayerWeapons.Kind.LaserGun };
    static readonly int[] WideAt = { 3, 99, 1, 3, 3 }; // canyon walls block a side view; wax hops read best from the side
    static readonly PlayerWeapons.Kind[] BossWeapon = { PlayerWeapons.Kind.Sword, PlayerWeapons.Kind.Shotgun, PlayerWeapons.Kind.Sword, PlayerWeapons.Kind.MachineGun, PlayerWeapons.Kind.LaserGun };
    static readonly Vector3 BossOffset = new Vector3(.45f, 3.1f, -7.4f);

    MapSandbox map; GameFlowManager flow; EnemySpawner spawner; UpgradeSystem upgrades; PlayerController controller;
    Transform player; CharacterController cc; Animator animator; PlayerHealth health; PlayerAutoAttack melee;
    PlayerDash dash; PlayerWeapons weapons; PlayerGunCombat guns; CombatCamera combatCam; Transform cam;
    MethodInfo swing, startDash, spawnEnemy, pickPrefab, trySpawnBoss, changeMap, placePlayer;
    FieldInfo meleeCooldown, spawnerElapsed, spawnerKills, pitch;
    Mode mode; Cam camMode; float camTimer; Vector3 camPos, camVelocity; Quaternion camRot; bool camPlaced;
    Enemy boss; Vector3 moveDir; float moveSpeed; Vector3 faceTarget; bool hasFace;
    bool wasDanger; float strafeSign = 1; int attacksSeen; float pendingGameCam, rushTimer;
    Vector3 defaultOffset; float defaultSpawnRadius;
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    float step = 1f / 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!UnityEditor.SessionState.GetBool(RunKey, false) || FindFirstObjectByType<PromoDirector>() != null) return;
        Marks.Clear(); Finished = false; WantRecording = true;
        new GameObject("PromoDirector").AddComponent<PromoDirector>();
    }

    void Mark(string label)
    {
        Marks.Add(Time.frameCount + "\t" + label);
        UnityEditor.SessionState.SetString(MarksKey, string.Join("\n", Marks));
    }

    IEnumerator Start()
    {
        step = 1f / Mathf.Max(1, UnityEditor.SessionState.GetInt(FpsKey, 60));
        if (GameAudio.Instance != null) GameAudio.Instance.musicVolume = 0f; // music is laid in the edit
        while (flow == null || map == null || flow.startButton == null || !flow.startButton.interactable)
        {
            yield return null;
            if (flow == null) flow = FindFirstObjectByType<GameFlowManager>();
            if (map == null) map = FindFirstObjectByType<MapSandbox>();
        }
        spawner = FindFirstObjectByType<EnemySpawner>(); upgrades = FindFirstObjectByType<UpgradeSystem>();
        weapons = FindFirstObjectByType<PlayerWeapons>(); player = weapons.transform;
        cc = player.GetComponent<CharacterController>(); health = player.GetComponent<PlayerHealth>();
        melee = player.GetComponent<PlayerAutoAttack>(); dash = player.GetComponent<PlayerDash>(); guns = player.GetComponent<PlayerGunCombat>();
        controller = player.GetComponent<PlayerController>(); animator = controller.animator;
        combatCam = player.GetComponentInChildren<CombatCamera>(); cam = combatCam != null ? combatCam.transform : Camera.main.transform;
        defaultOffset = combatCam.offset; defaultSpawnRadius = spawner.spawnRadius;
        swing = typeof(PlayerAutoAttack).GetMethod("Swing", Any, null, System.Type.EmptyTypes, null);
        meleeCooldown = typeof(PlayerAutoAttack).GetField("manualCooldownTimer", Any);
        startDash = typeof(PlayerDash).GetMethod("StartDash", Any);
        spawnEnemy = typeof(EnemySpawner).GetMethod("SpawnEnemy", Any); pickPrefab = typeof(EnemySpawner).GetMethod("PickPrefab", Any);
        trySpawnBoss = typeof(EnemySpawner).GetMethod("TrySpawnBoss", Any);
        spawnerElapsed = typeof(EnemySpawner).GetField("elapsed", Any); spawnerKills = typeof(EnemySpawner).GetField("killCount", Any);
        changeMap = typeof(MapSandbox).GetMethod("ChangeMap", Any); placePlayer = typeof(MapSandbox).GetMethod("PlacePlayer", Any);
        pitch = typeof(PlayerController).GetField("pitch", Any);

        Mark("title");
        yield return Seconds(2.5f);
        Mark("start_click");
        flow.startButton.onClick.Invoke();
        while (Time.timeScale <= 0) yield return null;
        weapons.CollectAxe();
        weapons.CollectGun(PlayerWeapons.Kind.Shotgun); weapons.CollectGun(PlayerWeapons.Kind.MachineGun); weapons.CollectGun(PlayerWeapons.Kind.LaserGun);

        foreach (var s in UnityEditor.SessionState.GetString(StagesKey, "0,1,2,3,4").Split(','))
            yield return Stage(int.Parse(s.Trim()));
        mode = Mode.None; Mark("end");
        yield return Frames(15);
        WantRecording = false; Finished = true;
    }

    IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    // Recorder runs at a fixed capture rate; count frames so shot lengths match the footage.
    IEnumerator Seconds(float seconds) { int n = Mathf.RoundToInt(seconds / step); for (int i = 0; i < n; i++) { if (Choosing) yield return HandlePause(); yield return null; } }
    bool Choosing => upgrades != null && upgrades.Choosing;

    void SpawnMonster() { spawnEnemy.Invoke(spawner, new[] { pickPrefab.Invoke(spawner, null) }); }

    IEnumerator Stage(int index)
    {
        if (map.CurrentMap != index)
        {
            Mark("travel_" + index);
            yield return map.StartCoroutine((IEnumerator)changeMap.Invoke(map, new object[] { index }));
            yield return Frames(10);
        }
        while (map.Busy) yield return null;
        Mark("stage_" + index);
        combatCam.offset = defaultOffset; SetPitch(14f);
        yield return Equip(MonsterWeapon[index]);
        spawner.spawnRadius = 7.5f;
        for (int i = 0; i < 14; i++) SpawnMonster();
        mode = Mode.Monsters;
        int frames = Mathf.RoundToInt(12f / step);
        for (int f = 0; f < frames; f++)
        {
            if (f == Mathf.RoundToInt(3f / step) && (index == 0 || index == 2 || index == 4) && !Choosing)
            {
                // the same call a touched augment crate makes, so the pick and its glow play for real
                var growth = player.GetComponent<CharacterGrowth>();
                if (growth != null && upgrades.TryOffer(growth)) { Mark("offer_" + index); yield return HandlePause(45); Mark("glow_" + index); }
            }
            if (Choosing) yield return HandlePause();
            if (Enemy.AliveRegularCount < 11 && f % 6 == 0) SpawnMonster();
            yield return null;
        }
        spawner.spawnRadius = defaultSpawnRadius;
        // Boss: satisfy the stage's own spawn rule so the real warning and entrance play.
        mode = Mode.Watch; guns.SetTrigger(false);
        ClearMonsters();
        yield return Frames(20);
        placePlayer.Invoke(map, null); Physics.SyncTransforms(); // the open start area keeps the boss readable
        yield return Equip(BossWeapon[index]);
        combatCam.offset = BossOffset; SetPitch(11f);
        boss = null; Enemy.OnBossSpawned += OnBoss;
        for (int attempt = 0; attempt < 120 && !spawner.BossWarningActive && boss == null; attempt++)
        {
            spawnerElapsed.SetValue(spawner, 999f); spawnerKills.SetValue(spawner, 999);
            trySpawnBoss.Invoke(spawner, null); yield return null;
        }
        Mark("boss_warning_" + index);
        faceTarget = spawner.BossArrivalPosition; hasFace = true;
        while (boss == null) { if (Choosing) yield return HandlePause(); yield return null; }
        Enemy.OnBossSpawned -= OnBoss;
        spawner.enabled = false; ClearMonsters(); // the boss gets the stage to itself
        Mark("boss_spawn_" + index);
        yield return Frames(3);
        SetCam(Cam.Intro); Mark("intro_" + index);
        yield return Seconds(3f);
        float size = Mathf.Clamp(BossBounds().size.y / 4.5f, 1f, 1.55f);
        combatCam.offset = BossOffset * size; SetPitch(11f + (size - 1) * 4f);
        SetCam(index == 1 ? Cam.Chase : Cam.Game); Mark("fight_" + index + "_size" + size.ToString("F2")); // canyon: look over the walls
        mode = Mode.Boss; attacksSeen = 0; wasDanger = false; pendingGameCam = 0;
        float fight = 0;
        while (boss != null && !boss.IsDying && attacksSeen < 4 && fight < 36f) { if (Choosing) yield return HandlePause(); fight += step; yield return null; }
        // let the last pattern play out before the finishing blows
        float settle = 0;
        while (boss != null && !boss.IsDying && wasDanger && settle < 4f) { settle += step; yield return null; }
        Mark("finish_" + index);
        if (boss != null && !boss.IsDying) boss.damageTakenMultiplier = 14f;
        float finish = 0;
        while (boss != null && !boss.IsDying && finish < 8f) { if (Choosing) yield return HandlePause(); finish += step; yield return null; }
        if (boss != null && !boss.IsDying) boss.TakeDamage(999999f);
        Mark("boss_dead_" + index);
        if (camMode != Cam.Game) SetCam(Cam.Game);
        mode = Mode.Watch; guns.SetTrigger(false); hasFace = false;
        yield return Seconds(2.6f);
        Mark("stage_end_" + index);
    }

    void OnBoss(Enemy spawned) { boss = spawned; }
    void ClearMonsters() { foreach (var e in new List<Enemy>(Enemy.ActiveEnemies)) if (e != null && !e.isBoss && !e.IsDying) e.TakeDamage(99999f); }
    void SetPitch(float value) { if (pitch != null) pitch.SetValue(controller, value); }

    IEnumerator HandlePause(int hold = 24)
    {
        Mark("augment");
        yield return Frames(hold);
        upgrades.Choose(0);
    }

    IEnumerator Equip(PlayerWeapons.Kind kind)
    {
        for (int i = 0; i < 60 && weapons.Equipped != kind; i++) { weapons.RequestEquip(kind); yield return null; }
        if (kind >= PlayerWeapons.Kind.Shotgun) guns.AddAmmo(kind, false);
        Mark("weapon_" + kind);
    }

    void SetCam(Cam next)
    {
        camMode = next; camTimer = 0; camPlaced = false;
        combatCam.enabled = next == Cam.Game;
        if (next != Cam.Game)
        {
            if (hiddenCanvases.Count > 0) return;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.enabled = false; hiddenCanvases.Add(c); }
        }
        else { foreach (var c in hiddenCanvases) if (c != null) c.enabled = true; hiddenCanvases.Clear(); }
    }

    void Update()
    {
        if (player == null || Time.timeScale <= 0f) return;
        if (health.currentHealth < health.maxHealth * .7f) health.Heal(health.maxHealth);
        health.GrantInvincibility(.3f);
        moveDir = Vector3.zero; moveSpeed = 0;
        if (mode == Mode.Monsters) DriveMonsters();
        else if (mode == Mode.Boss && boss != null && !boss.IsDying) DriveBoss();
        else if (mode == Mode.Boss && boss != null) FaceToward(boss.transform.position);
        if (mode == Mode.Watch && hasFace) FaceToward(faceTarget);
        Move();
        if (weapons.IsGun) guns.AddAmmo(weapons.Equipped, false);
    }

    void DriveMonsters()
    {
        Enemy target = null; float best = float.MaxValue; int ahead = 0;
        foreach (var e in Enemy.ActiveEnemies)
        {
            if (e == null || e.isBoss || e.IsDying) continue;
            Vector3 d = e.transform.position - player.position; d.y = 0;
            float angle = Vector3.Angle(player.forward, d);
            if (angle < 35f && d.magnitude < 7f) ahead++;
            float score = d.magnitude + angle * .06f;
            if (score < best) { best = score; target = e; }
        }
        if (target == null) { guns.SetTrigger(false); return; }
        Vector3 to = target.transform.position - player.position; to.y = 0;
        float dist = to.magnitude - target.BodyRadiusWorld;
        FaceToward(target.transform.position);
        bool facing = Vector3.Angle(player.forward, to) < 25f;
        if (weapons.IsGun)
        {
            if (dist > 8f) { moveDir = to.normalized; moveSpeed = 5f; }
            else if (dist < 3.5f) { moveDir = -to.normalized; moveSpeed = 3f; }
            guns.SetTrigger(facing && dist < 14f);
            return;
        }
        // melee: dash into a pack every so often, otherwise close in and swing
        rushTimer -= Time.deltaTime;
        if (rushTimer <= 0 && ahead >= 3 && dist > 2.5f && dash.IsReady && facing)
        {
            startDash.Invoke(dash, new object[] { to.normalized }); rushTimer = 2.6f; Mark("rush");
            return;
        }
        if (dist > 1.4f) { moveDir = to.normalized; moveSpeed = 6f; }
        if (facing && dist < 2.4f) TrySwing();
    }

    bool Danger(out bool lavaLine)
    {
        lavaLine = false;
        var motion = boss.GetComponent<BossAttackMotion>();
        string phase = motion != null ? motion.Phase : "Idle";
        var crystals = boss.GetComponent<CrystalBossCrystals>();
        var lava = boss.GetComponent<CraterBossAttacks>();
        if (lava != null && (lava.Phase == "LavaFlow" || phase.StartsWith("LavaPour"))) lavaLine = true;
        if (crystals != null && (crystals.Phase == "Aim" || crystals.Phase == "Volley")) return true;
        if (lava != null && lava.Phase == "ThrowFlight") return true;
        return phase.EndsWith("Tell") || phase == "Charge" || phase == "HopAir" || phase == "LavaPourHold";
    }

    void DriveBoss()
    {
        Vector3 to = boss.transform.position - player.position; to.y = 0;
        float surface = to.magnitude - boss.BodyRadiusWorld;
        Vector3 dir = to.sqrMagnitude > .01f ? to.normalized : player.forward;
        FaceToward(boss.transform.position);
        bool danger = Danger(out bool lavaLine);
        if (danger && !wasDanger)
        {
            attacksSeen++; strafeSign = Random.value < .5f ? -1 : 1;
            Mark("attack_" + attacksSeen + "_" + (boss.GetComponent<BossAttacks>()?.lastPattern ?? "?"));
            if (attacksSeen == WideAt[map.CurrentMap]) { SetCam(Cam.Wide); Mark("wide_on"); }
            dodgeDelay = .22f; // read the tell first, then roll out
        }
        Vector3 side = Vector3.Cross(Vector3.up, dir) * strafeSign;
        if (dodgeDelay > 0) { dodgeDelay -= Time.deltaTime; if (dodgeDelay <= 0 && dash.IsReady) { startDash.Invoke(dash, new object[] { (side * .75f - dir * .65f).normalized }); Mark("dodge"); } }
        if (!danger && wasDanger && camMode == Cam.Wide) pendingGameCam = 1.6f;
        wasDanger = danger;
        if (pendingGameCam > 0) { pendingGameCam -= Time.deltaTime; if (pendingGameCam <= 0) { SetCam(Cam.Game); Mark("wide_off"); } }

        if (danger || lavaLine)
        {
            guns.SetTrigger(false);
            float keep = lavaLine ? 3f : 7.5f;
            moveDir = (side + (surface < keep ? -dir : Vector3.zero) * .8f).normalized; moveSpeed = 6f;
            return;
        }
        if (weapons.IsGun)
        {
            if (surface > 9f) { moveDir = dir; moveSpeed = 5f; } else if (surface < 5f) { moveDir = -dir; moveSpeed = 3f; }
            guns.SetTrigger(true);
            return;
        }
        // punish window: rush back in with a dash when far, then swing
        if (surface > 5f && dash.IsReady && pendingCounter) { startDash.Invoke(dash, new object[] { dir }); pendingCounter = false; Mark("counter_dash"); return; }
        if (surface > 1.5f) { moveDir = dir; moveSpeed = 6f; }
        if (surface < 2.4f) { TrySwing(); pendingCounter = true; }
    }
    float dodgeDelay; bool pendingCounter = true;

    void TrySwing()
    {
        if ((float)meleeCooldown.GetValue(melee) > 0f) return;
        swing.Invoke(melee, null);
    }

    void FaceToward(Vector3 point)
    {
        Vector3 d = point - player.position; d.y = 0;
        if (d.sqrMagnitude < .01f) return;
        player.rotation = Quaternion.RotateTowards(player.rotation, Quaternion.LookRotation(d), 240f * Time.deltaTime);
    }

    void Move()
    {
        bool dashing = dash != null && dash.IsDashing;
        if (!dashing && moveSpeed > 0 && cc.enabled) cc.Move(moveDir * moveSpeed * Time.deltaTime);
        if (animator != null) animator.SetFloat("Speed", dashing ? 0 : moveSpeed);
    }

    void LateUpdate()
    {
        if (camMode == Cam.Game || boss == null) return;
        camTimer += step;
        if (camMode == Cam.Intro) IntroShot(); else if (camMode == Cam.Chase) ChaseShot(); else WideShot();
        cam.SetPositionAndRotation(camPos, camRot);
    }

    Bounds BossBounds()
    {
        var b = new Bounds(boss.transform.position, Vector3.one); bool first = true;
        foreach (var r in boss.GetComponentsInChildren<Renderer>())
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void IntroShot()
    {
        var b = BossBounds(); float h = Mathf.Clamp(b.size.y, 1.5f, 8f);
        Vector3 toPlayer = player.position - boss.transform.position; toPlayer.y = 0;
        Vector3 front = toPlayer.sqrMagnitude > .01f ? toPlayer.normalized : boss.transform.forward;
        float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(camTimer / 3f));
        Vector3 orbit = Quaternion.Euler(0, Mathf.Lerp(-72f, -48f, k), 0) * front;
        Vector3 ground = new Vector3(boss.transform.position.x, b.min.y, boss.transform.position.z);
        camPos = ground + orbit * Mathf.Lerp(h * 1.9f, h * 1.35f, k) + Vector3.up * h * Mathf.Lerp(.1f, .18f, k);
        camRot = Quaternion.LookRotation(ground + Vector3.up * h * .6f - camPos);
    }

    void ChaseShot()
    {
        Vector3 bp = boss.transform.position, pp = player.position;
        Vector3 axis = bp - pp; axis.y = 0; if (axis.sqrMagnitude < .01f) axis = player.forward;
        Vector3 n = axis.normalized, perp = Vector3.Cross(Vector3.up, n);
        Vector3 desired = pp - n * 7.5f + perp * 2.5f + Vector3.up * 9f;
        Vector3 focus = Vector3.Lerp(pp, bp, .5f) + Vector3.up * 1.2f;
        if (!camPlaced) { camPos = desired; camRot = Quaternion.LookRotation(focus - desired); camVelocity = Vector3.zero; camPlaced = true; return; }
        camPos = Vector3.SmoothDamp(camPos, desired, ref camVelocity, .35f, 40f, step);
        camRot = Quaternion.Slerp(camRot, Quaternion.LookRotation(focus - camPos), 1 - Mathf.Exp(-6f * step));
    }

    void WideShot()
    {
        Vector3 bp = boss.transform.position, pp = player.position;
        Vector3 mid = Vector3.Lerp(bp, pp, .45f);
        Vector3 axis = pp - bp; axis.y = 0; if (axis.sqrMagnitude < .01f) axis = Vector3.forward;
        Vector3 n = axis.normalized, perp = Vector3.Cross(Vector3.up, n);
        float span = Mathf.Max(12f, axis.magnitude + 10f);
        Vector3 desired = mid + perp * span * .8f + n * span * .35f + Vector3.up * span * .5f;
        Quaternion look = Quaternion.LookRotation(mid + Vector3.up * 1.5f - desired);
        if (!camPlaced) { camPos = desired; camRot = look; camVelocity = Vector3.zero; camPlaced = true; return; }
        camPos = Vector3.SmoothDamp(camPos, desired, ref camVelocity, .6f, 30f, step);
        camRot = Quaternion.Slerp(camRot, Quaternion.LookRotation(mid + Vector3.up * 1.5f - camPos), 1 - Mathf.Exp(-5f * step));
    }

    void OnDestroy() { Enemy.OnBossSpawned -= OnBoss; }
}
#endif
