using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run in Play Mode at the title screen; all temporary objects and input devices are removed.
public static class CombatRegressionChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Prefix = "__CombatRegression_";
    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static GameObject New(string name)
    {
        var go = new GameObject(Prefix + name);
        go.SetActive(false);
        go.transform.position = new Vector3(10000, 1000, 10000);
        return go;
    }

    [MenuItem("Tools/Validation/Run Combat Regression Checks")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || (RunStats.Instance != null && RunStats.Instance.Running))
        {
            Debug.LogWarning("Enter Play Mode at the title screen before running combat checks.");
            return;
        }
        float savedScale = Time.timeScale;
        var savedLock = Cursor.lockState;
        bool savedVisible = Cursor.visible;
        var stats = RunStats.Instance;
        bool wasRunning = stats != null && stats.Running;
        int savedClicks = stats != null ? stats.Clicks : 0;
        int savedHits = stats != null ? stats.Hits : 0;
        var originalEffects = PowerUpEffects.Instance;
        float originalSlow = PowerUpEffects.EnemySpeedScale;
        Mouse mouse = null;
        var checks = new List<string>();
        try
        {
            // Avoid changing persistent records or sending synthetic deaths to the live scene.
            if (stats != null) Set(stats, "<Running>k__BackingField", true);
            Time.timeScale = 0f;

            var go = New("Enemy");
            go.AddComponent<CapsuleCollider>().radius = 0.5f;
            go.AddComponent<WaxCrackVisual>();
            go.AddComponent<WaxShardBurst>();
            var enemy = go.AddComponent<Enemy>();
            go.SetActive(true);
            Call(enemy, "Awake");
            Check(go.GetComponents<WaxCrackVisual>().Length == 1 &&
                  go.GetComponents<WaxShardBurst>().Length == 1, "Duplicate hit-effect components");
            checks.Add("effect initialization is idempotent");

            Check(Mathf.Approximately(enemy.BodyRadiusWorld, 0.5f), "Initial radius");
            go.transform.localScale = Vector3.one * 2f;
            Check(Mathf.Approximately(enemy.BodyRadiusWorld, 1f), "Radius must follow scale changes");
            checks.Add("body radius follows elite scaling");

            int count = Enemy.AliveRegularCount;
            Call(enemy, "OnEnable");
            Check(Enemy.AliveRegularCount == count, "Duplicate registry entry");
            var clone = UnityEngine.Object.Instantiate(go);
            Check(Enemy.AliveRegularCount == count + 1, "Cloned enemies must be counted");
            clone.SetActive(false);
            Check(Enemy.AliveRegularCount == count, "Disabled enemy remained registered");
            checks.Add("clones register once and unregister on disable");

            // Disable particles for this non-lethal damage test; inspect the actual crack stage.
            Set(enemy, "shardBurst", null);
            enemy.TakeDamage(20f);
            Check(Mathf.Approximately(enemy.CurrentHealth, 20f), "Damage was not applied");
            Check(Get<int>(go.GetComponent<WaxCrackVisual>(), "visibleStage") == 2,
                  "Cracks must reflect this hit, not the previous hit");
            checks.Add("hit visuals reflect current health");
            var visual = New("SurfaceCrackCheck");
            visual.AddComponent<MeshFilter>().sharedMesh = FxShapes.Sphere;
            visual.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("AdditiveFX");
            var surfaceCrack = visual.AddComponent<WaxCrackVisual>();
            visual.SetActive(true);
            surfaceCrack.SetDamage(0.6f);
            var surfaceOverlay = visual.transform.Find("WaxSurfaceCracks");
            Check(surfaceOverlay != null && visual.GetComponentsInChildren<LineRenderer>().Length == 0,
                  "Cracks must use the body mesh rather than floating lines");
            Check(surfaceOverlay.GetComponent<MeshFilter>().sharedMesh == FxShapes.Sphere,
                  "Crack overlay does not match the original surface");
            visual.transform.localScale = new Vector3(2f,0.4f,2f);
            Call(surfaceCrack,"LateUpdate");
            Check(surfaceOverlay.localPosition == Vector3.zero && surfaceOverlay.localScale == Vector3.one,
                  "Cracks detached during squash");
            surfaceCrack.SetDamage(0f);
            Check(!surfaceOverlay.GetComponent<Renderer>().enabled,"Restored health left visible cracks");
            checks.Add("cracks share the body mesh, follow squash and clear when health resets");


            var attack = New("Attack").AddComponent<PlayerAutoAttack>();
            var upgrades = New("Upgrades").AddComponent<UpgradeSystem>();
            upgrades.panel = New("Panel");
            upgrades.panel.SetActive(true);
            Time.timeScale = 1f;
            Call(upgrades, "Awake");
            Check(!upgrades.panel.activeSelf && Time.timeScale == 1f,
                  "Retired upgrade overlay must stay hidden without pausing");
            checks.Add("legacy upgrade panel is hidden without changing game time");

            var spawner = New("Spawner").AddComponent<EnemySpawner>();
            spawner.player = New("SpawnOrigin").transform;
            spawner.spawnRadius = 1f;
            spawner.bossPrefab = New("BossPrefab");
            spawner.bossPrefab.AddComponent<Enemy>().isBoss = true;
            Set(spawner, "killCount", spawner.killsToSpawnBoss);
            Set(spawner, "elapsed", spawner.bossEarliestSeconds);
            var blocker = New("Blocker");
            blocker.AddComponent<BoxCollider>().size = Vector3.one * 100f;
            // Terrain sampling places spawns near y=0, regardless of the test player's height.
            blocker.transform.position = new Vector3(10000, 0, 10000);
            blocker.SetActive(true);
            Physics.SyncTransforms();
            Call(spawner, "TrySpawnBoss");
            Check(!Get<bool>(spawner, "bossSpawned"), "Failed boss spawn marked successful");
            blocker.SetActive(false);
            Physics.SyncTransforms();
            Call(spawner, "TrySpawnBoss");
            Check(Get<bool>(spawner, "bossSpawned"), "Boss spawn did not retry successfully");
            checks.Add("blocked boss spawn retries successfully");

            // Use a temporary mouse; the held-button check runs after the press frame is over.
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            InputSystem.Update();
            InputSystem.Update();
            attack.gameObject.SetActive(true);
            attack.useHitStop = false;
            attack.holdToAttack = true;
            Call(attack, "Update");
            Check(Get<float>(attack, "manualCooldownTimer") > 0f, "Held right button did not attack");
            attack.holdToAttack = false;
            Set(attack, "manualCooldownTimer", 0f);
            Call(attack, "Update");
            Check(Get<float>(attack, "manualCooldownTimer") <= 0f, "Hold option off still attacks");
            Time.timeScale = 0f;
            Set(attack, "bufferTimer", 0.2f);
            Call(attack, "Update");
            Check(Get<float>(attack, "bufferTimer") == 0f, "Paused menu retained attack input");
            checks.Add("held attack respects option and pause");

            var buffObject = New("BuffPlayer");
            var buffAttack = buffObject.AddComponent<PlayerAutoAttack>();
            var buffs = buffObject.AddComponent<PowerUpEffects>();
            var buffHealth = buffObject.AddComponent<PlayerHealth>();
            buffObject.SetActive(true);
            buffs.Activate(PowerUpEffects.Kind.Rage);
            buffAttack.manualAttackDamage += 8f;
            Check(buffAttack.manualAttackDamage == 38f && buffs.DamageMultiplier == 2f,
                  "Rage changed permanent upgrade stats");
            Set(buffs, "<Remaining>k__BackingField", 0f);
            Check(buffs.DamageMultiplier == 1f && buffAttack.manualAttackDamage == 38f,
                  "Expired rage remained active or lost upgrades");
            buffs.Activate(PowerUpEffects.Kind.Frenzy);
            buffAttack.manualAttackCooldown *= 0.85f;
            Check(Mathf.Approximately(buffAttack.manualAttackCooldown, 0.425f) && buffs.CooldownMultiplier == 0.5f,
                  "Frenzy changed permanent cooldown");
            Set(buffs, "<Remaining>k__BackingField", 0f);
            Check(buffs.CooldownMultiplier == 1f && Mathf.Approximately(buffAttack.manualAttackCooldown, 0.425f),
                  "Frenzy expiry lost the permanent upgrade");
            checks.Add("temporary rage and frenzy preserve permanent upgrades and expire");

            buffs.Activate(PowerUpEffects.Kind.Shield);
            buffHealth.TakeDamage(10f);
            Check(buffHealth.currentHealth == 100f, "Shield did not block damage");
            buffs.Activate(PowerUpEffects.Kind.Rage);
            buffHealth.TakeDamage(10f);
            Check(buffHealth.currentHealth == 90f, "Replaced shield remained active");
            checks.Add("shield blocks damage and ends immediately when replaced");
            buffs.Activate(PowerUpEffects.Kind.Slow);
            Check(PowerUpEffects.EnemySpeedScale < 1f, "Slow did not activate");
            buffs.enabled = false;
            Check(PowerUpEffects.EnemySpeedScale == 1f && !buffs.Active, "Disabled buff left global slow active");
            checks.Add("disabling a buff restores global movement speed");

            var wardenA = New("WardenA").AddComponent<EnemyWarden>();
            var wardenB = New("WardenB").AddComponent<EnemyWarden>();
            wardenA.gameObject.SetActive(true);
            wardenB.gameObject.SetActive(true);
            enemy.damageTakenMultiplier = 2f;
            enemy.SetShield(wardenA, 0.4f);
            enemy.SetShield(wardenB, 0.3f);
            Check(Mathf.Approximately(enemy.EffectiveDamageMultiplier, 1.2f), "Shield overwrote vulnerability");
            enemy.RemoveShield(wardenA);
            Check(Mathf.Approximately(enemy.EffectiveDamageMultiplier, 1.4f), "Removing one shield removed the other");
            enemy.RemoveShield(wardenB);
            Check(enemy.EffectiveDamageMultiplier == 2f, "Shields failed to restore vulnerability");
            checks.Add("overlapping shields preserve other sources and charger vulnerability");

            foreach (PowerUpEffects.Kind kind in Enum.GetValues(typeof(PowerUpEffects.Kind)))
            {
                PowerUp.Spawn(new Vector3(12000, 1000, 12000), kind);
                foreach (var pickup in UnityEngine.Object.FindObjectsByType<PowerUp>(FindObjectsSortMode.None))
                {
                    if (pickup.transform.position.x < 11999) continue;
                    pickup.gameObject.name = Prefix + "Pickup";
                    Check(Get<Material>(pickup, "coreMat").color == PowerUpEffects.ColorOf(kind),
                          "Pickup visual was created before kind was assigned");
                    UnityEngine.Object.DestroyImmediate(pickup.gameObject);
                }
            }
            checks.Add("all four pickup kinds initialize with their correct color");

            upgrades.CancelChoices();
            Check(!upgrades.panel.activeSelf, "Game-end cleanup reactivated old upgrade UI");

            var movementObject = New("Movement");
            var movement = movementObject.AddComponent<PlayerController>();
            var dodge = movementObject.AddComponent<PlayerDash>();
            movementObject.SetActive(true);
            Time.timeScale = 0f;
            var rotation = movementObject.transform.rotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, 100) });
            InputSystem.Update();
            Call(movement, "Update");
            Check(movementObject.transform.rotation == rotation, "Paused mouse input moved the camera");
            Set(dodge, "dashTimer", 1f);
            Set(dodge, "dashRemaining", 4f);
            Call(dodge, "Update");
            Check(Get<float>(dodge, "dashRemaining") == 4f, "Dash progressed during pause");
            checks.Add("paused camera and dash ignore gameplay input");

            var livePlayer=GameObject.FindGameObjectWithTag("Player");
            var liveAttack=livePlayer.GetComponent<PlayerAutoAttack>();
            float scaleBeforeHitStopCheck = Time.timeScale;
            try
            {
                Time.timeScale = 0.65f;
                var stop = (System.Collections.IEnumerator)Call(liveAttack, "HitStop", 1f);
                Check(stop.MoveNext() && Time.timeScale < 0.65f, "Hit stop did not begin");
                Check(!stop.MoveNext() && Mathf.Approximately(Time.timeScale, 0.65f), "Hit stop lost the previous speed");
                stop = (System.Collections.IEnumerator)Call(liveAttack, "HitStop", 1f);
                stop.MoveNext();
                Call(liveAttack, "OnDisable");
                Check(Mathf.Approximately(Time.timeScale, 0.65f), "Cancelled attack leaves the game slowed");
                ((System.IDisposable)stop).Dispose();
                stop = (System.Collections.IEnumerator)Call(liveAttack, "HitStop", 1f);
                stop.MoveNext();
                Time.timeScale = 0f;
                ((System.IDisposable)stop).Dispose();
                Check(Time.timeScale == 0f, "Hit stop resumed a paused game");
                checks.Add("hit stop restores prior speed on completion and cancellation without resuming pause");
            }
            finally { Time.timeScale = scaleBeforeHitStopCheck; }
            Set(liveAttack,"manualCooldownTimer",0.5f);
            float normalDelay=(float)Call(liveAttack,"PlayAttackMotion");
            Set(liveAttack,"manualCooldownTimer",0.25f);
            float fastDelay=(float)Call(liveAttack,"PlayAttackMotion");
            Check(Mathf.Approximately(fastDelay,normalDelay*0.5f) && Mathf.Approximately(liveAttack.animator.GetFloat("AttackSpeed"),2f),
                  "Attack animation and impact timing must scale together");
            checks.Add("attack speed scales both animation and damage delay");
            if(liveAttack.handGrip!=null)
            {
                Check(Mathf.Approximately(normalDelay,0.5f*SwordSwingMotion.ContactPhase),"Damage is out of sync with the fast cut");
                var before=SwordSwingMotion.Evaluate(SwordSwingMotion.WindupEnd);
                var after=SwordSwingMotion.Evaluate(SwordSwingMotion.CutEnd);
                Check(before.yaw-after.yaw>110f,"Cut lacks a readable sweeping arc");
                foreach(float boundary in new[]{SwordSwingMotion.WindupEnd,SwordSwingMotion.CutEnd,0.56f,0.78f})
                {
                    const float epsilon=0.0001f;
                    var left=SwordSwingMotion.Evaluate(boundary-epsilon);
                    var middle=SwordSwingMotion.Evaluate(boundary);
                    var right=SwordSwingMotion.Evaluate(boundary+epsilon);
                    var incoming=(middle.wrist-left.wrist)/epsilon;
                    var outgoing=(right.wrist-middle.wrist)/epsilon;
                    Check(Vector3.Distance(incoming,outgoing)<0.1f,"Wrist velocity jumps at a pose boundary");
                    Check(incoming.magnitude>0.1f,"Swing stops at an intermediate pose");
                }
                Check(SwordSwingMotion.ContactPhase>SwordSwingMotion.WindupEnd && SwordSwingMotion.ContactPhase<SwordSwingMotion.CutEnd,"Contact is outside the blade stroke");
                checks.Add("continuous cut preserves velocity through intermediate poses and synchronizes contact");
            }

            var originalRotation=livePlayer.transform.rotation;
            var hand=liveAttack.animator.GetBoneTransform(HumanBodyBones.RightHand);
            var upper=liveAttack.animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lower=liveAttack.animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            float armLength=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
            for(int heading=0;heading<4;heading++)
            {
                livePlayer.transform.rotation=Quaternion.Euler(0f,heading*90f,0f);
                for(int frame=0;frame<=60;frame++)
                {
                    liveAttack.ApplySwordPose(frame/60f,true);
                    Vector3 blade=livePlayer.transform.InverseTransformDirection(liveAttack.swordModel.up);
                    Check(blade.z>0f,"Blade swings behind the character");
                    if(frame==0 || frame==60) Check(blade.y>0.6f,"Ready pose leaves the sword pointing down");
                    Check(Quaternion.Angle(Quaternion.Euler(liveAttack.gripIdleEuler),liveAttack.swordModel.localRotation)<0.01f,"Sword slips inside the hand");
                    if(liveAttack.handGrip!=null)
                    {
                        Check(Vector3.Distance(liveAttack.swordModel.localPosition,liveAttack.handGrip.swordLocalPosition)<0.00001f,"Handle slips out of its authored palm socket");
                        Check(liveAttack.handGrip.fingers.Length==12,"Rebuilt hand is missing finger joints");
                        foreach(var finger in liveAttack.handGrip.fingers)
                            Check(finger.bone!=null && Quaternion.Angle(finger.bone.localRotation,finger.rotation)<0.02f,"Finger grip does not hold during the cut");
                    }
                    else Check(Vector3.Distance(hand.position,liveAttack.swordModel.position)<0.1f,"Handle detaches from the hand");
                    float posedLength=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
                    Check(Mathf.Abs(posedLength-armLength)<0.001f,"Arm stretches during the cut");
                }
            }
            livePlayer.transform.rotation=originalRotation;
            liveAttack.ApplySwordPose(0f,false);
            var idleBlade=liveAttack.swordModel.up;
            liveAttack.ApplySwordPose(1f,true);
            Check(Vector3.Angle(idleBlade,liveAttack.swordModel.up)<0.01f,"Swing does not return to the ready pose");
            checks.Add("244 sword poses retain forward blade, upright ready pose, attached grip and arm lengths across four headings");
            checks.Add("swing returns to the upright ready pose");
            var spine=liveAttack.animator.GetBoneTransform(HumanBodyBones.Spine);
            liveAttack.ResetSwordPose();
            Quaternion originalSpine=spine.localRotation;
            liveAttack.ApplySwordPose(0.55f,true);
            Quaternion posedSpine=spine.localRotation;
            for(int repeat=0;repeat<120;repeat++) liveAttack.ApplySwordPose(0.55f,true);
            Check(Quaternion.Angle(posedSpine,spine.localRotation)<0.02f,"Paused swing accumulates torso rotation");
            liveAttack.ResetSwordPose();
            Check(Quaternion.Angle(originalSpine,spine.localRotation)<0.02f,"Sword pose does not restore locomotion bones");
            checks.Add("torso offsets do not accumulate and restore locomotion pose");
            Set(liveAttack,"manualCooldownTimer",0f);
            Set(liveAttack,"swingHold",0f);
            liveAttack.animator.SetLayerWeight(liveAttack.attackLayerIndex,0f);
            liveAttack.animator.SetFloat("AttackSpeed",1f);
            var cleanMesh=livePlayer.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            foreach(var weight in cleanMesh.boneWeights)
                Check(Mathf.Abs(weight.weight0+weight.weight1+weight.weight2+weight.weight3-1f)<0.001f,"Invalid skin weight sum");
            Check(cleanMesh.bindposes.Length>0 && cleanMesh.vertexCount>0,"Character skin binding is missing");
            checks.Add("polished mesh retains valid skin weights and bind poses");

            Debug.Log("COMBAT REGRESSION PASS (" + checks.Count + "): " + string.Join("; ", checks));
        }
        catch (Exception ex)
        {
            Debug.LogError("COMBAT REGRESSION FAIL after " + checks.Count + " checks: " + ex);
        }
        finally
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name.StartsWith(Prefix, StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go);
            if (stats != null)
            {
                Set(stats, "<Running>k__BackingField", wasRunning);
                Set(stats, "<Clicks>k__BackingField", savedClicks);
                Set(stats, "<Hits>k__BackingField", savedHits);
            }
            PowerUpEffects.Instance = originalEffects;
            PowerUpEffects.EnemySpeedScale = originalSlow;
            Time.timeScale = savedScale;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
        }
    }
}
