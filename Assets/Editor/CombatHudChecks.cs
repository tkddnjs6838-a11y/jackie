using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class CombatHudChecks
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static string report;
    static void Check(bool ok,string label){if(!ok)throw new Exception("COMBAT HUD FAIL: "+label);report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/combat-hud-checks.txt",report);}
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";yield return null;
        var hud=Object.FindFirstObjectByType<CombatHudView>();var flow=Object.FindFirstObjectByType<GameFlowManager>();
        Check(hud!=null && !hud.Root.activeSelf,"combat HUD hidden on title screen");flow.startButton.onClick.Invoke();yield return null;
        var weapons=hud.GetComponent<PlayerWeapons>();var gun=hud.GetComponent<PlayerGunCombat>();var health=hud.GetComponent<PlayerHealth>();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;health.GrantInvincibility(1000);gun.ReadInput=false;
        try
        {
            hud.Refresh();Check(hud.Root.activeSelf && hud.AmmoLabel.text=="" && hud.VisibleAmmoIndicators==0,"sword hides ammunition and bullet indicators");
            var hp=hud.Root.transform.Find("Health").GetComponent<RectTransform>();var dash=hud.Root.transform.Find("Dash").GetComponent<RectTransform>();
            Check(hp.anchorMax==Vector2.one && dash.anchorMax==Vector2.one && dash.anchoredPosition.y<hp.anchoredPosition.y,"HP top right with dash immediately below");
            weapons.CollectGun(PlayerWeapons.Kind.Shotgun);weapons.TryEquip(PlayerWeapons.Kind.Shotgun);hud.Refresh();
            Check(hud.LitAmmoIndicators==2 && hud.VisibleAmmoIndicators==2,"shotgun has two loaded shell indicators");
            typeof(PlayerGunCombat).GetMethod("Fire",P).Invoke(gun,null);hud.Refresh();
            Check(gun.Ammo==1 && hud.LitAmmoIndicators==1 && hud.AmmoLabel.text.StartsWith("1 "),"firing updates loaded count and shell indicator");
            gun.BeginReload();hud.Refresh();Check(hud.StateLabel.text=="재장전 중","reload status shown");
            Time.timeScale=0;float progress=gun.ReloadFraction;yield return new WaitForSecondsRealtime(.2f);Check(gun.ReloadFraction==progress,"pause freezes reload progression");Time.timeScale=1;
            yield return new WaitForSeconds(3);hud.Refresh();Check(gun.Ammo==2 && hud.LitAmmoIndicators==2 && !gun.IsReloading,"reload restores shell indicators");
            Capture(hud,"combat-hud-shotgun");
            weapons.CollectGun(PlayerWeapons.Kind.MachineGun);weapons.TryEquip(PlayerWeapons.Kind.MachineGun);hud.Refresh();Check(hud.VisibleAmmoIndicators==30 && hud.LitAmmoIndicators==gun.Ammo,"machine gun uses 30 capacity indicators");
            weapons.CollectGun(PlayerWeapons.Kind.LaserGun);weapons.TryEquip(PlayerWeapons.Kind.LaserGun);hud.Refresh();Check(hud.VisibleAmmoIndicators==3,"laser uses three energy indicators");
            var mag=(int[])typeof(PlayerGunCombat).GetField("magazine",P).GetValue(gun);var reserve=(int[])typeof(PlayerGunCombat).GetField("reserve",P).GetValue(gun);mag[2]=0;reserve[2]=0;hud.Refresh();Check(hud.StateLabel.text.Contains("탄약 없음") && hud.LitAmmoIndicators==0,"empty reserve prompts weapon change");
            weapons.TryEquip(PlayerWeapons.Kind.Sword);hud.Refresh();Check(hud.VisibleAmmoIndicators==0 && hud.AmmoLabel.text=="","switching back to sword removes ammo UI");
            typeof(RunStats).GetField("<Running>k__BackingField",P).SetValue(RunStats.Instance,false);hud.Refresh();Check(!hud.Root.activeSelf,"HUD hidden when run ends");
            Debug.Log("COMBAT HUD CHECKS COMPLETE\n"+report);
        }
        finally{Time.timeScale=1;gun.ReadInput=true;spawner.enabled=true;}
    }
    static void Capture(CombatHudView hud,string filename)
    {
        var camera=Camera.main;var canvas=hud.HudCanvas;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;var previous=camera.targetTexture;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/"+filename+".png",texture.EncodeToPNG());}
        finally{canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;camera.targetTexture=previous;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(texture);}
    }
}
