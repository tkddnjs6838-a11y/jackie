using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class MobileControlChecks
{
    const string Report="output/gun-effects/mobile-results.txt";
    static MobileControlChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="output/gun-effects/mobile-check.txt";
        if(EditorApplication.isCompiling || !File.Exists(request))return;
        EditorPrefs.SetBool("Wakuball.MobilePreview",true);
        if(!EditorApplication.isPlaying)return;
        File.Delete(request);Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Run());
    }
    static void Check(bool pass,string description){File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+description+"\n");if(!pass)throw new Exception(description);}
    static PointerEventData Pointer(int id,TouchControl c){return new PointerEventData(EventSystem.current){pointerId=id,position=RectTransformUtility.WorldToScreenPoint(null,c.transform.position)};}
    static IEnumerator Run()
    {
        File.WriteAllText(Report,"RUNNING\n");
        Application.runInBackground=true;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();var map=Object.FindFirstObjectByType<MapSandbox>();
        while(map.Busy || map.CurrentMap<0)yield return null;
        flow.startButton.onClick.Invoke();
        File.AppendAllText(Report,"Run started; scale="+Time.timeScale+"\n");yield return null;yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();var weapons=player.GetComponent<PlayerWeapons>();var gun=player.GetComponent<PlayerGunCombat>();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        TouchControl Find(string name)=>Array.Find(Object.FindObjectsByType<TouchControl>(FindObjectsSortMode.None),x=>x.name==name);
        var move=Find("Move");var attack=Find("Attack");var look=Find("Look");
        Check(move!=null && attack!=null && look!=null,"Touch controls visible during run");
        var a=Pointer(1,move);a.position+=Vector2.up*65;move.OnPointerDown(a);
        var b=Pointer(2,attack);attack.OnPointerDown(b);
        Check(MobileControls.Move.y>.1f && MobileControls.Firing,"Independent movement and attack fingers");
        attack.OnPointerUp(Pointer(3,attack));Check(MobileControls.Firing,"Unrelated finger cannot cancel firing");
        var before=player.transform.position;yield return new WaitForSeconds(.18f);
        Check(Vector3.Distance(before,player.transform.position)>.1f,"Stick moves character");
        Check(player.GetComponent<PlayerAutoAttack>().AttackInProgress,"Held touch starts melee attack");
        attack.OnPointerUp(b);move.OnPointerUp(a);Check(!MobileControls.Firing && MobileControls.Move==Vector2.zero,"Releasing fingers clears input");
        var l=Pointer(4,look);look.OnPointerDown(l);l.delta=new Vector2(40,0);float yaw=player.transform.eulerAngles.y;look.OnDrag(l);yield return null;yield return null;look.OnPointerUp(l);
        Check(Mathf.Abs(Mathf.DeltaAngle(yaw,player.transform.eulerAngles.y))>1,"Swipe turns aim");
        var dash=Find("Dash");dash.OnPointerDown(Pointer(5,dash));yield return null;yield return null;dash.Release();
        Check(!player.GetComponent<PlayerDash>().IsReady,"Dash touch consumes cooldown");
        yield return new WaitForSeconds(1.2f);
        weapons.CollectGun(PlayerWeapons.Kind.Shotgun);Check(weapons.TryEquip(PlayerWeapons.Kind.Shotgun),"Shotgun equipped for touch test");
        int shots=gun.ShotsFired;attack.OnPointerDown(b);yield return new WaitForSeconds(.3f);attack.Release();
        Check(gun.ShotsFired>shots,"Attack touch fires gun");
        var reload=Find("Reload");reload.OnPointerDown(Pointer(6,reload));reload.Release();
        Check(gun.IsReloading,"Reload button starts reload");yield return new WaitForSeconds(1.7f);
        Check(gun.Ammo==PlayerGunCombat.Capacity(PlayerWeapons.Kind.Shotgun),"Reload fills magazine");
        var swap=Find("Switch");swap.OnPointerDown(Pointer(7,swap));swap.Release();Check(weapons.Equipped==PlayerWeapons.Kind.Sword,"Weapon button cycles owned weapons");
        var portal=Find("Portal");portal.OnPointerDown(Pointer(8,portal));portal.Release();Check(MobileControls.TakeInteract(),"Portal touch generates interaction");
        attack.OnPointerDown(b);Time.timeScale=0;yield return null;yield return null;
        Check(!MobileControls.Firing && MobileControls.Move==Vector2.zero,"Pause cancels held touches");Time.timeScale=1;yield return null;yield return null;
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("output/gun-effects/mobile-ui.png");yield return null;
        flow.CompleteStages();yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("output/gun-effects/result-ui.png");yield return null;
        Check(Object.FindObjectsByType<TouchControl>(FindObjectsSortMode.None).Length==0,"Result screen hides touch controls");
        File.AppendAllText(Report,"COMPLETE\n");
    }
}
