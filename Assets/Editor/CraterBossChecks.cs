using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

// Run explicitly in Play mode. Test-only changes are discarded on leaving Play mode.
public static class CraterBossChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Dir="output/crater-boss-approved/checks/";
    static string report;
    static PlayerHealth hp;
    static CraterBossAttacks boss;
    static Vector3 playerStart;
    static Camera review;
    static void Check(bool ok,string label)
    {
        report+=(ok?"PASS: ":"FAIL: ")+label+"\n";File.WriteAllText(Dir+"play-tests.txt",report);
        if(!ok)throw new Exception("CRATER CHECK: "+label);
    }
    static void Place(Vector3 p)
    {
        var cc=hp.GetComponent<CharacterController>();cc.enabled=false;hp.transform.position=p;cc.enabled=true;Physics.SyncTransforms();
    }
    static void ResetHealth(){hp.currentHealth=hp.maxHealth;typeof(PlayerHealth).GetField("invincibilityTimer",Private).SetValue(hp,0f);}
    static void SetRunning(bool running){typeof(RunStats).GetField("<Running>k__BackingField",Private).SetValue(RunStats.Instance,running);}
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play mode first");
        Directory.CreateDirectory(Dir);report="Crater boss actual Play-mode checks\n";
        Object.FindFirstObjectByType<EnemySpawner>().StartCoroutine(Checks());
    }
    static IEnumerator Capture(string label,int frames)
    {
        string dir="output/crater-boss-approved/play/"+label;Directory.CreateDirectory(dir);
        var rt=new RenderTexture(1000,750,24);var tex=new Texture2D(1000,750,TextureFormat.RGB24,false);
        var trace=new System.Text.StringBuilder();
        for(int i=0;i<frames;i++)
        {
            yield return new WaitForSeconds(.12f);
            review.targetTexture=rt;review.Render();RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,1000,750),0,0);tex.Apply();File.WriteAllBytes(dir+"/frame-"+i.ToString("D3")+".png",tex.EncodeToPNG());
            review.targetTexture=null;RenderTexture.active=null;trace.AppendLine(Time.time+" "+boss.Phase+" hp="+hp.currentHealth+" effects="+boss.ActiveEffects);
        }
        File.WriteAllText(dir+"/trace.txt",trace.ToString());Object.Destroy(rt);Object.Destroy(tex);
    }
    static IEnumerator FinishAttack()
    {
        float deadline=Time.time+12;
        while(boss.Phase!="Idle" && Time.time<deadline)yield return null;
        Check(boss.Phase=="Idle","attack finishes within 12 seconds");
    }
    static IEnumerator Checks()
    {
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
        var maps=Object.FindFirstObjectByType<MapSandbox>();
        yield return (IEnumerator)typeof(MapSandbox).GetMethod("ChangeMap",Private).Invoke(maps,new object[]{4});
        var sp=Object.FindFirstObjectByType<EnemySpawner>();sp.enabled=false;
        foreach(var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
        yield return null;
        hp=Object.FindFirstObjectByType<PlayerHealth>();hp.GetComponent<PlayerController>().enabled=false;hp.GetComponent<PlayerAutoAttack>().enabled=false;hp.GetComponent<PlayerGunCombat>().enabled=false;
        playerStart=hp.transform.position;ResetHealth();Time.timeScale=1;
        Vector3 spawn;Check(MapNavigation.Sample(playerStart+Vector3.forward*13,20,out spawn),"crater spawn is on navigation surface");
        typeof(EnemySpawner).GetField("bossArrivalPosition",Private).SetValue(sp,spawn);
        typeof(EnemySpawner).GetMethod("SpawnConfiguredBoss",Private).Invoke(sp,null);
        boss=Object.FindFirstObjectByType<CraterBossAttacks>();Check(boss!=null,"stage 5 spawner selects the new crater prefab");
        var enemy=boss.GetComponent<Enemy>();var brain=boss.GetComponent<BossAttacks>();brain.StopAllCoroutines();
        Check(enemy.maxHealth==2400 && Mathf.Approximately(enemy.moveSpeed,4.2f),"existing health and movement values preserved");enemy.moveSpeed=0;
        var motion=boss.GetComponent<BossAttackMotion>();Check(motion.authoredThrow!=null && motion.authoredPour!=null && motion.authoredWalk!=null,"authored throw, pour and walk clips assigned");
        Check(sp.crystalBossPrefab.name=="CrystalBossEnemy","stage 4 crystal boss preserved");
        var go=new GameObject("CraterReviewCamera");review=go.AddComponent<Camera>();review.CopyFrom(Camera.main);review.enabled=false;
        review.transform.position=spawn+new Vector3(-13,13,-19);review.transform.LookAt(spawn+new Vector3(0,2,-6));review.fieldOfView=57;
        boss.StartCoroutine(boss.Throw());yield return Capture("throw-final",27);yield return FinishAttack();
        Check(Mathf.Approximately(hp.maxHealth-hp.currentHealth,boss.throwDamage),"lava throw damages once at impact");Check(boss.ActiveEffects==0,"throw effects cleaned up");
        ResetHealth();Place(playerStart);boss.StartCoroutine(boss.Throw());yield return new WaitForSeconds(.3f);
        Vector3 dodge;Check(MapNavigation.Sample(playerStart+Vector3.right*6,3,out dodge),"dodge location exists");Place(dodge);
        yield return FinishAttack();Check(hp.currentHealth==hp.maxHealth,"moving away after tell dodges the locked throw");
        Place(playerStart);ResetHealth();boss.StartCoroutine(boss.Throw());yield return new WaitForSeconds(.35f);
        Time.timeScale=0;float progress=motion.Progress;Vector3 hand=motion.FistPoint("R");float hpBefore=hp.currentHealth;
        yield return new WaitForSecondsRealtime(.5f);
        Check(motion.Progress==progress && Vector3.Distance(hand,motion.FistPoint("R"))<.001f && hp.currentHealth==hpBefore,"pause freezes throw pose and damage");
        SetRunning(false);yield return null;Check(boss.ActiveEffects==0 && !enemy.externalMovement,"run end cancels throw and releases movement");
        boss.StopAllCoroutines();Time.timeScale=1;SetRunning(true);Place(playerStart);ResetHealth();
        // A wall on the ballistic path must absorb the throw.
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="CraterTestWall";wall.transform.position=Vector3.Lerp(spawn,playerStart,.55f)+Vector3.up*4;
        wall.transform.localScale=new Vector3(12,12,1);Physics.SyncTransforms();boss.StartCoroutine(boss.Throw());yield return FinishAttack();
        Check(hp.currentHealth==hp.maxHealth,"solid wall blocks lava projectile");Object.Destroy(wall);yield return null;
        Place(playerStart);ResetHealth();boss.StartCoroutine(boss.Pour());yield return Capture("flow-final",40);yield return FinishAttack();
        float damage=hp.maxHealth-hp.currentHealth;
        Check(damage>=boss.flowDamage && damage<=boss.flowDamage*5,"standing in lava takes bounded periodic damage");Check(boss.ActiveEffects==0,"lava stream and hand effects expire");
        Place(playerStart);ResetHealth();boss.StartCoroutine(boss.Pour());yield return new WaitForSeconds(.3f);Place(dodge);
        yield return FinishAttack();Check(hp.currentHealth==hp.maxHealth,"stepping sideways avoids the lava lane");
        Place(playerStart);boss.StartCoroutine(boss.Pour());yield return new WaitForSeconds(2);
        Time.timeScale=0;hpBefore=hp.currentHealth;var ribbon=GameObject.Find("GroundLavaStream");Check(ribbon!=null,"active flow mesh exists");
        var before=ribbon.GetComponent<MeshFilter>().sharedMesh.vertices;yield return new WaitForSecondsRealtime(.5f);
        var after=ribbon.GetComponent<MeshFilter>().sharedMesh.vertices;bool same=before.Length==after.Length;
        for(int i=0;i<before.Length && same;i++)same=before[i]==after[i];
        Check(same && hp.currentHealth==hpBefore,"pause freezes lava spread and damage");Time.timeScale=1;
        brain.enabled=false;yield return null;Check(boss.ActiveEffects==0 && !enemy.externalMovement,"disabling attack brain clears active lava");boss.StopAllCoroutines();brain.enabled=true;brain.StopAllCoroutines();
        // Let the real brain choose both attacks without test invocation.
        Place(dodge);ResetHealth();hp.GrantInvincibility(30);brain.restBetweenAttacks=.2f;brain.restJitter=0;
        int throws=boss.Throws,flows=boss.Flows;brain.enabled=false;brain.enabled=true;yield return new WaitForSeconds(12);
        Check(boss.Throws>throws && boss.Flows>flows,"normal boss brain alternates throw and flow");brain.StopAllCoroutines();boss.StopAllCoroutines();boss.Cancel();
        boss.StartCoroutine(boss.Pour());yield return new WaitForSeconds(2);enemy.TakeDamage(enemy.CurrentHealth+1000);
        yield return null;Check(enemy.IsDying && boss.ActiveEffects==0,"boss death cancels active lava immediately");
        yield return new WaitForSeconds(.5f);Check(Enemy.BossDefeated,"final boss death sets progression flag");
        Check(maps.PortalPositions!=null && maps.PortalPositions.Length>4,"final exit position exists");
        Place(maps.PortalPositions[4]);yield return new WaitForSecondsRealtime(1);
        bool entered=maps.EnterPortal(4);yield return null;
        Check(!RunStats.Instance.Running,"final exit completes the run");
        report+="ALL CHECKS PASSED\n";File.WriteAllText(Dir+"play-tests.txt",report);Debug.Log("CRATER CHECKS COMPLETE\n"+report);
        Object.Destroy(review.gameObject);
    }
}
