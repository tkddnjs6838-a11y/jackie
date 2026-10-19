using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// A controlled integration run in the loaded scene. No scene/prefab edits are saved.
public static class SceneFlowChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int phase;
    static bool longRun;
    static double nextSwing, nextSplitWave;
    static Quaternion savedPlayerRotation;
    static readonly List<float> stressFrameTimes = new List<float>();
    static System.Collections.IEnumerator RecordStressFrames()
    {
        yield return null;
        while (active && longRun && phase == 1)
        {
            if (Time.timeScale > 0f && Time.unscaledDeltaTime > 0f) stressFrameTimes.Add(Time.unscaledDeltaTime);
            yield return null;
        }
    }
    [MenuItem("Tools/Validation/Run 45 Second Combat Stress Check")]
    public static void RunStress()
    {
        if (active || !EditorApplication.isPlaying || RunStats.Instance == null || RunStats.Instance.Running) return;
        longRun = true;
        savedPlayerRotation = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>().transform.rotation;
        Run();
    }
    static double next;
    static float runStartedAt;
    static double spawnCheckDeadline;
    static Enemy target;
    static float beforeHealth;
    static GameFlowManager flow;
    static PlayerHealth health;
    static readonly List<string> passed = new List<string>();
    static readonly string[] keys = { "best_kills", "best_combo", "best_time" };
    static bool[] hadKeys;
    static int kills, combo;
    static float time;
    static bool active;
    static readonly List<string> errors = new List<string>();
    static object Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, Private).Invoke(instance, args);
    static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
        passed.Add(message);
    }

    [MenuItem("Tools/Validation/Run Scene Flow Checks %#&t")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying || RunStats.Instance == null || RunStats.Instance.Running || active)
        {
            Debug.LogWarning("Start Scene Flow Checks in Play Mode at the title screen.");
            return;
        }
        Debug.Log("SCENE FLOW START");
        hadKeys = Array.ConvertAll(keys, PlayerPrefs.HasKey);
        kills = PlayerPrefs.GetInt(keys[0]); combo = PlayerPrefs.GetInt(keys[1]); time = PlayerPrefs.GetFloat(keys[2]);
        passed.Clear(); errors.Clear(); active = true; phase = 0;
        next = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += Log;
        EditorApplication.update += Tick;
    }

    // Capture actual Game View frames during the integration strike for visual review.
    static System.Collections.IEnumerator CaptureSwordFrames()
    {
        string directory = System.IO.Path.GetFullPath("output/sword-motion-refine/gameplay");
        System.IO.Directory.CreateDirectory(directory);
        float previous = 0f;
        foreach (float time in new[] { 0f, 0.14f, 0.26f, 0.40f, 0.52f })
        {
            if (time > previous) yield return new WaitForSeconds(time - previous);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, "strike-" + Mathf.RoundToInt(time * 1000f) + ".png"));
            previous = time;
        }
    }

    static void StressStep()
    {
        if (EditorApplication.timeSinceStartup >= nextSplitWave)
        {
            nextSplitWave += 10;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemySplitter.prefab");
            UnityEngine.Object.Instantiate(prefab, health.transform.position + health.transform.forward * 2f, Quaternion.identity);
        }
        Enemy nearest = null; float distance = float.PositiveInfinity;
        foreach (var enemy in Enemy.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDying) continue;
            float value = (enemy.transform.position-health.transform.position).sqrMagnitude;
            if (value < distance) { distance = value; nearest = enemy; }
        }
        if (nearest != null)
        {
            var direction = nearest.transform.position-health.transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > .001f) health.transform.rotation = Quaternion.LookRotation(direction);
        }
        Call(health.GetComponent<PlayerAutoAttack>(), "Swing");
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.name != "WaxSurfaceCracks") continue;
            foreach (var material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported)
                    throw new Exception("Invalid crack material during extended combat: " + renderer.name);
        }
    }

    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            errors.Add(message);
    }

    static void Tick()
    {
        if (!active) return;
        if (!EditorApplication.isPlaying) { Finish(false, "Play Mode exited early"); return; }
        if (EditorApplication.timeSinceStartup < next)
        {
            if (longRun && phase == 1 && EditorApplication.timeSinceStartup >= nextSwing)
            {
                nextSwing = EditorApplication.timeSinceStartup + .55;
                try { StressStep(); } catch (Exception error) { Finish(false, error.ToString()); }
            }
            return;
        }
        // Editor updates continue during slow/background player frames; give the game
        // five scaled seconds, with a wall-clock watchdog for a genuinely stalled run.
        if(phase==1 && !longRun && Time.time-runStartedAt<5f && EditorApplication.timeSinceStartup<spawnCheckDeadline)return;
        try
        {
            flow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
            health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            var spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();
            Debug.Log("SCENE FLOW PHASE " + phase);
            switch (phase)
            {
                case 0:
                    int missing = 0;
                    foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    Assert(missing == 0, "scene has no missing scripts");
                    Assert(flow.startPanel.activeSelf && !flow.gameOverPanel.activeSelf && !flow.victoryPanel.activeSelf,
                           "title screen hides both result panels");
                    var startView=flow.startPanel.GetComponent<StartScreenView>();
                    Assert(startView!=null && startView.startButton==flow.startButton,"prefab view supplies the gameplay start button");
                    Assert(Array.IndexOf(AssetDatabase.GetDependencies(flow.gameObject.scene.path),StartScreenWorkshop.PrefabPath)>=0,"start screen remains linked to design prefab");
                    Assert(flow.startPanel.transform.Find("ControlGuide")!=null,"control guide is serialized in the prefab");
                    Assert(startView.testModeNotice.activeSelf==WeaponTestSettings.AllGunsAtStart,"test-only instructions follow active preset");
                    bool hadVolume=PlayerPrefs.HasKey("audio.masterVolume");
                    float savedVolume=PlayerPrefs.GetFloat("audio.masterVolume",1f), liveVolume=AudioListener.volume;
                    try
                    {
                        startView.settingsButton.onClick.Invoke();
                        Assert(startView.settingsPanel.activeSelf && !flow.startButton.interactable && Time.timeScale==0,"settings opens and blocks starting behind the modal");
                        startView.volumeSlider.value=.37f;
                        Assert(Mathf.Abs(AudioListener.volume-.37f)<.001f && startView.volumeLabel.text=="37%","volume slider controls audio and percentage");
                        startView.volumeSlider.value=liveVolume;
                        startView.closeSettingsButton.onClick.Invoke();
                        Assert(!startView.settingsPanel.activeSelf && flow.startButton.interactable && Time.timeScale==0,"closing settings restores menu without starting combat");
                    }
                    finally
                    {
                        AudioListener.volume=liveVolume;
                        if(hadVolume)PlayerPrefs.SetFloat("audio.masterVolume",savedVolume);else PlayerPrefs.DeleteKey("audio.masterVolume");
                        PlayerPrefs.Save();
                    }
                    CaptureStartScreen(flow);
                    flow.startButton.onClick.Invoke();
                    health.GrantInvincibility(1000f);
                    Assert(RunStats.Instance.Running && Time.timeScale == 1f && !flow.startPanel.activeSelf,
                           "start button begins the run");
                    runStartedAt=Time.time;spawnCheckDeadline=EditorApplication.timeSinceStartup+45;
                    next = EditorApplication.timeSinceStartup + (longRun ? 45 : 5);
                    nextSwing = EditorApplication.timeSinceStartup + .5;
                    nextSplitWave = EditorApplication.timeSinceStartup + 5;
                    if (longRun) { stressFrameTimes.Clear(); flow.StartCoroutine(RecordStressFrames()); }
                    break;
                case 1:
                    Assert(Enemy.AliveRegularCount >= 2, "normal enemy spawning runs over time");
                    var player = health.transform;
                    var obj = UnityEngine.Object.Instantiate(spawner.enemyPrefab,
                        player.position + player.forward * 1.5f, Quaternion.identity);
                    target = obj.GetComponent<Enemy>();
                    target.externalMovement = true;
                    beforeHealth = target.CurrentHealth;
                    Call(player.GetComponent<PlayerAutoAttack>(), "Swing");
                    player.GetComponent<PlayerAutoAttack>().StartCoroutine(CaptureSwordFrames());
                    next = EditorApplication.timeSinceStartup + 1.5;
                    break;
                case 2:
                    Assert(target == null || target.IsDying || target.CurrentHealth < beforeHealth,
                           "animated sword strike applies delayed damage in the scene");
                    // Controlled kills test the actual death events, progression and serialized UI bindings.
                    spawner.bossWarningSeconds=0;
                    typeof(EnemySpawner).GetField("elapsed", Private).SetValue(spawner,spawner.bossEarliestSeconds);
                    for (int i = 0; i < spawner.killsToSpawnBoss && spawner.KillCount < spawner.killsToSpawnBoss; i++)
                    {
                        var victim = UnityEngine.Object.Instantiate(spawner.enemyPrefab,
                            health.transform.position + Vector3.forward * 10, Quaternion.identity).GetComponent<Enemy>();
                        victim.TakeDamage(1000000f);
                    }
                    Enemy boss = null;
                    foreach (var e in Enemy.ActiveEnemies) if (e.isBoss) boss = e;
                    Assert(boss != null, "kill threshold spawns the boss");
                    var bossUI = UnityEngine.Object.FindFirstObjectByType<BossHealthBar>();
                    Assert(bossUI != null && bossUI.root.activeSelf, "boss health bar becomes visible");
                    var patterns = boss.GetComponent<BossAttacks>();
                    Call(patterns, "Ready");
                    patterns.StartCoroutine((System.Collections.IEnumerator)Call(patterns, "Slam"));
                    boss.TakeDamage(1000000f);
                    Assert(!patterns.enabled && !boss.externalMovement, "boss death disables its attack behaviour");
                    Assert(Enemy.BossDefeated && !bossUI.root.activeSelf, "boss death unlocks the goal and hides boss HP");
                    next = EditorApplication.timeSinceStartup + 1.5;
                    break;
                case 3:
                    var goal = UnityEngine.Object.FindFirstObjectByType<WinZone>();
                    var cc = health.GetComponent<CharacterController>();
                    cc.enabled = false;
                    health.transform.position = goal.GetComponent<Collider>().bounds.center;
                    cc.enabled = true;
                    Physics.SyncTransforms();
                    next = EditorApplication.timeSinceStartup + 1;
                    break;
                case 4:
                    Assert(flow.victoryPanel.activeSelf && !RunStats.Instance.Running && Time.timeScale == 0f,
                           "entering goal trigger shows victory and pauses the run");
                    flow.victoryRetryButton.onClick.Invoke();
                    next = EditorApplication.timeSinceStartup + 1;
                    break;
                case 5:
                    Assert(flow.startPanel.activeSelf && !Enemy.BossDefeated && spawner.KillCount == 0,
                           "victory retry resets scene progression");
                    flow.startButton.onClick.Invoke();
                    health.TakeDamage(1000000f);
                    Assert(flow.gameOverPanel.activeSelf && !flow.victoryPanel.activeSelf && Time.timeScale == 0f,
                           "lethal damage shows only game over");
                    flow.retryButton.onClick.Invoke();
                    next = EditorApplication.timeSinceStartup + 1;
                    break;
                case 6:
                    Assert(flow.startPanel.activeSelf && health.currentHealth == health.maxHealth && !RunStats.Instance.Running,
                           "death retry restores full health and title screen");
                    Assert(errors.Count == 0, "no runtime errors during scene flow checks");
                    Finish(true, string.Join("; ", passed));
                    return;
            }
            phase++;
        }
        catch (Exception ex) { Finish(false, "phase " + phase + ": " + ex); }
    }

    static void CaptureStartScreen(GameFlowManager manager)
    {
        var canvas=manager.startPanel.GetComponentInParent<Canvas>().rootCanvas;var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;float distance=canvas.planeDistance;
        var obj=new GameObject("StartScreenCapture",typeof(Camera));var camera=obj.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.cullingMask=1<<5;
        foreach(var child in canvas.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();System.IO.File.WriteAllBytes("output/gun-effects/start-screen.png",texture.EncodeToPNG());}
        finally{canvas.renderMode=mode;canvas.worldCamera=previousCamera;canvas.planeDistance=distance;RenderTexture.active=old;UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(obj);}
    }

    static void Finish(bool success, string message)
    {
        if (longRun)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            if (player != null) player.transform.rotation = savedPlayerRotation;
            Debug.Log("45 SECOND COMBAT STRESS " + (success ? "PASS" : "FAIL"));
            if (stressFrameTimes.Count > 0)
            {
                stressFrameTimes.Sort();
                float median = stressFrameTimes[stressFrameTimes.Count/2];
                float p95 = stressFrameTimes[Mathf.Min(stressFrameTimes.Count-1, Mathf.FloorToInt(stressFrameTimes.Count*.95f))];
                string result = $"45s editor combat: frames={stressFrameTimes.Count}, median={median*1000:F2} ms, p95={p95*1000:F2} ms, worst={stressFrameTimes[stressFrameTimes.Count-1]*1000:F2} ms. Diagnostic scans and test spawns included; not a standalone build benchmark.";
                System.IO.Directory.CreateDirectory("output/wax-clone-check");
                System.IO.File.WriteAllText("output/wax-clone-check/stress.txt", result);
                Debug.Log(result);
            }
            longRun = false;
        }

        active = false;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        // Integration checks exercise EndRun, so restore the user's previous records.
        if (hadKeys != null)
        {
            if (hadKeys[0]) PlayerPrefs.SetInt(keys[0], kills); else PlayerPrefs.DeleteKey(keys[0]);
            if (hadKeys[1]) PlayerPrefs.SetInt(keys[1], combo); else PlayerPrefs.DeleteKey(keys[1]);
            if (hadKeys[2]) PlayerPrefs.SetFloat(keys[2], time); else PlayerPrefs.DeleteKey(keys[2]);
            PlayerPrefs.Save();
        }
        if (success) Debug.Log("SCENE FLOW PASS (" + passed.Count + "): " + message);
        else Debug.LogError("SCENE FLOW FAIL: " + message + " Errors: " + string.Join("; ", errors));
    }
}
