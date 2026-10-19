using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Controlled, accelerated progression test. Real spawn/death/portal/retry paths are used;
// only the boss wait and enemies' health are shortened for validation. No scene is saved.
public static class ReleaseFlowChecks
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static int phase,stage;static bool active;static double next,deadline;
    static readonly List<string> results=new List<string>(),errors=new List<string>();
    static readonly string[] keys={"best_kills","best_combo","best_time"};
    static bool[] had;static int kills,combo;static float time;
    static DestinationPortal portal;static PlayerWeapons originalPlayer;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS: "+label);Save();}
    static void Save(){Directory.CreateDirectory("output/final-polish");File.WriteAllText("output/final-polish/flow-checks.txt",string.Join("\n",results));}
    public static void Run()
    {
        if(active || !EditorApplication.isPlaying || RunStats.Instance==null || RunStats.Instance.Running)throw new Exception("Run from Play Mode title screen");
        phase=stage=0;results.Clear();errors.Clear();next=0;deadline=EditorApplication.timeSinceStartup+180;active=true;
        had=Array.ConvertAll(keys,PlayerPrefs.HasKey);kills=PlayerPrefs.GetInt(keys[0]);combo=PlayerPrefs.GetInt(keys[1]);time=PlayerPrefs.GetFloat(keys[2]);
        Application.logMessageReceived+=Log;EditorApplication.update+=Tick;Save();
    }
    static void Log(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception) && !message.Contains("memoryless"))errors.Add(message);}
    static void Tick()
    {
        if(!active)return;
        try
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode ended early");
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timed out at phase "+phase+" stage "+stage);
            if(EditorApplication.timeSinceStartup<next)return;
            var flow=Object.FindFirstObjectByType<GameFlowManager>();var map=Object.FindFirstObjectByType<MapSandbox>();
            if(flow==null||map==null)return;
            var player=Object.FindFirstObjectByType<PlayerWeapons>();var spawn=Object.FindFirstObjectByType<EnemySpawner>();
            switch(phase)
            {
                case 0:
                    if(map.Busy||map.CurrentMap<0)return;
                    Check(!MapSandboxSettings.Enabled&&!WeaponTestSettings.AllGunsAtStart,"release mode: cheats disabled");
                    Check(flow.startPanel.activeSelf&&!flow.victoryPanel.activeSelf&&!flow.gameOverPanel.activeSelf,"title only on launch");
                    var view=flow.startPanel.GetComponent<StartScreenView>();view.OpenSettings();Check(!flow.startButton.interactable&&Time.timeScale==0,"settings blocks gameplay");view.CloseSettings();
                    flow.startButton.onClick.Invoke();Check(RunStats.Instance.Running&&Time.timeScale==1,"start button begins run");
                    originalPlayer=player;player.GetComponent<PlayerHealth>().GrantInvincibility(1000);phase=1;next=EditorApplication.timeSinceStartup+4;break;
                case 1:
                    if(map.Busy)return;
                    Check(map.CurrentMap==stage,"stage "+(stage+1)+" loaded");Check(player==originalPlayer,"player persists at stage "+(stage+1));
                    if(stage==0)Check(Enemy.AliveRegularCount>0,"regular enemies spawn in real time");
                    Check(!map.TravelTo(Mathf.Min(stage+1,4)),"next map locked before boss "+stage);
                    spawn.bossWarningSeconds=.15f;typeof(EnemySpawner).GetField("elapsed",P).SetValue(spawn,spawn.bossEarliestSeconds);
                    for(int i=0;i<spawn.killsToSpawnBoss+1 && spawn.KillCount<spawn.killsToSpawnBoss;i++)
                    {var e=Object.Instantiate(spawn.enemyPrefab,player.transform.position+Vector3.forward*8,Quaternion.identity).GetComponent<Enemy>();e.TakeDamage(100000);}
                    phase=2;break;
                case 2:
                    Enemy boss=null;foreach(var e in Enemy.ActiveEnemies)if(e!=null&&e.isBoss&&!e.IsDying){boss=e;break;}
                    if(boss==null)return;
                    Check(Object.FindFirstObjectByType<BossHealthBar>().root.activeSelf,"boss HP appears "+stage);
                    if(stage>=3)
                    {
                        Check(boss.GetComponent<LateStageBossLook>()!=null && boss.GetComponent<BossAttacks>().stageVariant==stage,"late boss variant configured "+stage);
                        Check(boss.transform.Find(stage==3?"CrystalCrown":"LavaHorn")!=null,"late boss silhouette created "+stage);
                    }
                    var audio=Object.FindFirstObjectByType<GameAudio>();
                    var source=(AudioSource)typeof(GameAudio).GetField("activeMusic",P).GetValue(audio);
                    Check(source.clip!=null && source.clip.name==(stage>=3?"MusicBossFinal":"MusicBossBounce"),"boss soundtrack selected "+stage);
                    boss.TakeDamage(100000);phase=3;next=EditorApplication.timeSinceStartup+1;break;
                case 3:
                    portal=Object.FindFirstObjectByType<DestinationPortal>();Check(portal!=null,"boss death activates portal "+stage);
                    Check(!Object.FindFirstObjectByType<BossHealthBar>().root.activeSelf,"boss HP hides after death "+stage);
                    Place(player,portal.transform.position+portal.transform.forward*.5f);phase=4;next=EditorApplication.timeSinceStartup+.15;break;
                case 4:
                    Place(player,portal.transform.position-portal.transform.forward*.5f);phase=5;next=EditorApplication.timeSinceStartup+.2;break;
                case 5:
                    if(stage<4){if(map.Busy||map.CurrentMap==stage)return;Check(map.CurrentMap==stage+1,"walking through portal advances stage "+stage);Check(SceneManager.sceneCount==2,"single environment after travel");stage++;phase=1;next=EditorApplication.timeSinceStartup+.9;}
                    else{if(!flow.victoryPanel.activeSelf)return;Check(!RunStats.Instance.Running&&Time.timeScale==0,"final portal completes run and pauses");ScreenCapture.CaptureScreenshot("output/final-polish/victory.png");phase=6;next=EditorApplication.timeSinceStartup+.4;}break;
                case 6: flow.victoryRetryButton.onClick.Invoke();phase=7;next=EditorApplication.timeSinceStartup+1;break;
                case 7:
                    if(map.Busy||map.CurrentMap!=0||!flow.startPanel.activeSelf)return;
                    Check(!Enemy.BossDefeated&&spawn.KillCount==0&&SceneManager.sceneCount==2,"victory retry resets flags and scenes");
                    flow.startButton.onClick.Invoke();player.GetComponent<PlayerHealth>().TakeDamage(100000);Check(flow.gameOverPanel.activeSelf&&!flow.victoryPanel.activeSelf,"death shows only game-over result");ScreenCapture.CaptureScreenshot("output/final-polish/game-over.png");phase=8;next=EditorApplication.timeSinceStartup+.4;break;
                case 8:flow.retryButton.onClick.Invoke();phase=9;next=EditorApplication.timeSinceStartup+1;break;
                case 9:
                    if(map.Busy||map.CurrentMap!=0||!flow.startPanel.activeSelf)return;
                    var health=player.GetComponent<PlayerHealth>();Check(health.currentHealth==health.maxHealth&&!RunStats.Instance.Running,"death retry restores health and title");Check(errors.Count==0,"no gameplay runtime errors");Finish(null);break;
            }
        }
        catch(Exception ex){Finish(ex.ToString());}
    }
    static void Place(PlayerWeapons p,Vector3 at){var c=p.GetComponent<CharacterController>();c.enabled=false;p.transform.position=at;c.enabled=true;Physics.SyncTransforms();}
    static void Finish(string error)
    {
        active=false;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(had[0])PlayerPrefs.SetInt(keys[0],kills);else PlayerPrefs.DeleteKey(keys[0]);if(had[1])PlayerPrefs.SetInt(keys[1],combo);else PlayerPrefs.DeleteKey(keys[1]);if(had[2])PlayerPrefs.SetFloat(keys[2],time);else PlayerPrefs.DeleteKey(keys[2]);PlayerPrefs.Save();
        results.Add(error==null?"COMPLETE":"FAIL: "+error);Save();Debug.Log("RELEASE FLOW "+(error==null?"PASS":"FAIL: "+error));
    }
}
