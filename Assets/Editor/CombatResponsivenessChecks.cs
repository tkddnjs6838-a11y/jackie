using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CombatResponsivenessChecks
{
    const BindingFlags P=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run()
    {
        if(!EditorApplication.isPlaying || RunStats.Instance==null || RunStats.Instance.Running)
            throw new Exception("Run at the Play Mode title screen");
        var a=Object.FindFirstObjectByType<PlayerAutoAttack>();var w=a.GetComponent<PlayerWeapons>();
        var origin=a.transform.position;float scale=Time.timeScale;
        bool hadAxe=w.HasAxe;var equipped=w.Equipped;
        var cd=typeof(PlayerAutoAttack).GetField("manualCooldownTimer",P);
        var hold=typeof(PlayerAutoAttack).GetField("swingHold",P);
        float oldCd=(float)cd.GetValue(a),oldHold=(float)hold.GetValue(a);
        var running=typeof(RunStats).GetField("<Running>k__BackingField",P);
        var target=new GameObject("__ResponseTarget");var wall=new GameObject("__ResponseWall");
        var spawnObject=new GameObject("__ResponseSpawner");spawnObject.SetActive(false);
        try
        {
            a.transform.position=new Vector3(10000,1000,10000);
            target.transform.position=a.transform.position+a.transform.forward*2;
            target.AddComponent<CapsuleCollider>();var enemy=target.AddComponent<Enemy>();enemy.enabled=false;
            wall.transform.position=a.transform.position+a.transform.forward;
            wall.transform.rotation=a.transform.rotation;var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(4,4,.3f);
            var query=typeof(PlayerAutoAttack).GetMethod("FindTargetsInFront",P);
            Func<bool> included=()=>((System.Collections.Generic.List<Enemy>)query.Invoke(a,new object[]{3f,160f})).Contains(enemy);
            Physics.SyncTransforms();Check(!included(),"Solid wall did not block melee");
            box.isTrigger=true;Physics.SyncTransforms();Check(included(),"Trigger incorrectly blocked melee");
            wall.SetActive(false);Physics.SyncTransforms();Check(included(),"Clear melee target was lost");
            Time.timeScale=1;running.SetValue(RunStats.Instance,true);hold.SetValue(a,0f);cd.SetValue(a,.4f);
            Check(w.CollectAxe() && w.HasAxe,"Axe pickup rejected during attack");
            Check(w.Equipped==PlayerWeapons.Kind.Sword,"Pickup changed weapon mid-swing");
            Check(!w.TryApplyPendingEquip(),"Queued swap ignored cooldown");
            cd.SetValue(a,0f);Check(w.TryApplyPendingEquip() && w.Equipped==PlayerWeapons.Kind.Axe,"Pickup swap did not finish");
            cd.SetValue(a,.4f);Check(w.RequestEquip(PlayerWeapons.Kind.Sword),"Swap request rejected");
            Check(w.RequestEquip(PlayerWeapons.Kind.Axe),"Reselect failed");
            cd.SetValue(a,0f);Check(!w.TryApplyPendingEquip() && w.Equipped==PlayerWeapons.Kind.Axe,"Reselect did not cancel pending swap");
            cd.SetValue(a,.4f);w.RequestEquip(PlayerWeapons.Kind.Sword);Time.timeScale=0;cd.SetValue(a,0f);
            Check(!w.TryApplyPendingEquip(),"Paused game applied swap");Time.timeScale=1;
            Check(w.TryApplyPendingEquip() && w.Equipped==PlayerWeapons.Kind.Sword,"Resume lost queued swap");
            var spawn=spawnObject.AddComponent<EnemySpawner>();spawn.ConfigureStage(0);
            Check(spawn.ObjectiveStatus.Contains("0 / 12"),"Kill objective missing");
            typeof(EnemySpawner).GetField("killCount",P).SetValue(spawn,12);
            typeof(EnemySpawner).GetField("elapsed",P).SetValue(spawn,20f);
            Check(spawn.ObjectiveStatus.Contains("10초"),"Boss wait countdown missing");
            typeof(EnemySpawner).GetField("elapsed",P).SetValue(spawn,30f);
            Check(spawn.ObjectiveStatus=="보스 등장 준비 중","Placement retry mislabelled as fight");
            typeof(EnemySpawner).GetField("bossSpawned",P).SetValue(spawn,true);
            Check(spawn.ObjectiveStatus=="보스를 격파하세요","Boss fight objective missing");
            Debug.Log("COMBAT RESPONSIVENESS PASS: walls, triggers, clear target, pickup during swing, deferred equip, latest request, pause/resume, boss objective states");
        }
        finally
        {
            a.transform.position=origin;
            cd.SetValue(a,0f);hold.SetValue(a,0f);w.TryEquip(equipped);
            typeof(PlayerWeapons).GetField("<HasAxe>k__BackingField",P).SetValue(w,hadAxe);
            cd.SetValue(a,oldCd);hold.SetValue(a,oldHold);
            running.SetValue(RunStats.Instance,false);Time.timeScale=scale;
            Object.DestroyImmediate(target);Object.DestroyImmediate(wall);Object.DestroyImmediate(spawnObject);
        }
    }
}
