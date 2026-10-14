using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MapSandboxChecks
{
    static string report;
    static MapSandboxChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="output/gun-effects/sandbox-check.txt";
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || !File.Exists(path))return;
        File.Delete(path);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static void Check(bool good,string message)
    {
        report+=(good?"PASS: ":"FAIL: ")+message+"\n";
        File.WriteAllText("output/gun-effects/sandbox-checks.txt",report);
        if(!good)throw new Exception(message);
    }
    static IEnumerator Run()
    {
        report="";
        var sandbox=Object.FindFirstObjectByType<MapSandbox>();
        float deadline=Time.realtimeSinceStartup+60;
        while(sandbox.Busy && Time.realtimeSinceStartup<deadline)yield return null;
        Check(!sandbox.Busy && sandbox.CurrentMap==0,"initial meadow load finishes");
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var weapons=Object.FindFirstObjectByType<PlayerWeapons>();var health=weapons.GetComponent<PlayerHealth>();var gun=weapons.GetComponent<PlayerGunCombat>();
        gun.ReadInput=false;
        float hp=health.currentHealth;health.TakeDamage(999999);Check(health.currentHealth==hp && !health.IsDead,"lethal damage ignored in sandbox");
        var fire=typeof(PlayerGunCombat).GetMethod("Fire",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(PlayerWeapons.Kind kind in Enum.GetValues(typeof(PlayerWeapons.Kind)))
        {
            Check(weapons.Owns(kind) && weapons.TryEquip(kind),"owned and equip: "+kind);
            if(kind>=PlayerWeapons.Kind.Shotgun)
            {
                int ammo=gun.Ammo,shots=gun.ShotsFired;
                for(int i=0;i<35;i++)fire.Invoke(gun,null);
                gun.BeginReload();
                Check(gun.Ammo==ammo && gun.ShotsFired==shots+35 && !gun.IsReloading,"35 shots without ammo loss/reload: "+kind);
            }
        }
        yield return null;
        Time.timeScale=0;Check(!sandbox.TravelTo(1),"paused travel rejected");Time.timeScale=1;
        Check(!sandbox.TravelTo(-1) && !sandbox.TravelTo(5),"invalid destinations rejected");
        foreach(int index in new[]{1,2,3,4,0,0})
        {
            Check(sandbox.TravelTo(index),"travel accepted: "+(index+1));
            Check(!sandbox.TravelTo(index),"duplicate travel blocked while loading");
            deadline=Time.realtimeSinceStartup+60;
            while(sandbox.Busy && Time.realtimeSinceStartup<deadline)yield return null;
            Check(!sandbox.Busy && sandbox.CurrentMap==index,"arrived: "+(index+1));
            Check(SceneManager.sceneCount==2 && Terrain.activeTerrains.Length==1,"one gameplay scene + one terrain environment");
            Check(Vector3.Distance(weapons.transform.position,sandbox.Arrival)<2,"player reaches map start");
            Check(GameObject.Find("SandboxPortals").transform.childCount==5,"five destination portals exist");
            Check(weapons.Equipped==PlayerWeapons.Kind.LaserGun && health.IsInvincible,"weapon and invulnerability persist");
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1);
        var controller=weapons.GetComponent<CharacterController>();controller.enabled=false;
        weapons.transform.position=sandbox.PortalPositions[1];controller.enabled=true;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.E));
        yield return null;yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        deadline=Time.realtimeSinceStartup+60;
        while(sandbox.Busy && Time.realtimeSinceStartup<deadline)yield return null;
        Check(!sandbox.Busy && sandbox.CurrentMap==1,"E key at nearby portal loads canyon");
        Check(sandbox.TravelTo(0),"return to meadow after E-key test");
        while(sandbox.Busy)yield return null;
        weapons.TryEquip(PlayerWeapons.Kind.Shotgun);gun.ReadInput=true;
        yield return new WaitForSecondsRealtime(.5f);
        ScreenCapture.CaptureScreenshot("output/gun-effects/map-sandbox.png");
        report+="COMPLETE\n";File.WriteAllText("output/gun-effects/sandbox-checks.txt",report);
        Debug.Log("MAP SANDBOX CHECKS COMPLETE\n"+report);
    }
}
