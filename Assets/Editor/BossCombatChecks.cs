using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

public static class BossCombatChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string report;
    static object Call(object target,string method)=>target.GetType().GetMethod(method,Private).Invoke(target,null);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static void Check(bool ok,string label)
    {
        if(!ok)throw new Exception("BOSS COMBAT FAIL: "+label);
        report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/boss-combat-checks.txt",report);
    }
    public static void Run()
    {
        report="";
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var health=Object.FindFirstObjectByType<PlayerHealth>();float originalHealth=health.currentHealth;
        var boss=Object.Instantiate(spawner.bossPrefab,health.transform.position,Quaternion.identity).GetComponent<Enemy>();
        boss.enabled=false;var attacks=boss.GetComponent<BossAttacks>();attacks.StopAllCoroutines();
        try
        {
            Set(health,"invincibilityTimer",0f);
            Check((bool)Call(attacks,"Ready"),"running boss has a valid target");
            boss.externalMovement=true;Set(boss,"contactTimer",100f);Call(boss,"Update");
            Check(health.currentHealth==originalHealth,"boss tell and recovery suppress contact damage");
            Check((float)typeof(Enemy).GetField("contactTimer",Private).GetValue(boss)==0,"pattern resets contact cooldown");
            boss.externalMovement=false;Call(boss,"Update");
            Check(health.currentHealth==originalHealth,"recovery does not end with immediate contact hit");
            Set(boss,"contactTimer",100f);Call(boss,"Update");
            Check(health.currentHealth<originalHealth,"ordinary boss contact still works after cooldown");
            health.currentHealth=originalHealth;Set(health,"invincibilityTimer",0f);
            attacks.chargeWindUp=0;attacks.chargeDistance=100;
            var charge=(IEnumerator)Call(attacks,"Charge");Time.timeScale=0;var position=boss.transform.position;
            charge.MoveNext();charge.MoveNext();
            Check(boss.transform.position==position && health.currentHealth==originalHealth,"paused charge neither moves nor damages");
            Time.timeScale=1;charge.MoveNext();
            Check(Mathf.Approximately(health.currentHealth,originalHealth-attacks.chargeDamage),"resumed charge hits once");
            Set(health,"invincibilityTimer",0f);charge.MoveNext();
            Check(Mathf.Approximately(health.currentHealth,originalHealth-attacks.chargeDamage),"charge cannot hit twice even after invincibility ends");
            Set(RunStats.Instance,"<Running>k__BackingField",false);
            Check(!charge.MoveNext() && !boss.externalMovement,"run end aborts charge and releases movement");
            Check(!(bool)Call(attacks,"Ready"),"ended run cannot start another boss attack");
            Set(RunStats.Instance,"<Running>k__BackingField",true);
            health.currentHealth=originalHealth;Set(health,"invincibilityTimer",0f);
            attacks.slamWindUp=0;Time.timeScale=0;
            var slam=(IEnumerator)Call(attacks,"Slam");slam.MoveNext();
            Check(health.currentHealth==originalHealth,"paused slam cannot land damage");
            Time.timeScale=1;slam.MoveNext();
            Check(Mathf.Approximately(health.currentHealth,originalHealth-attacks.slamDamage),"resumed slam applies its single strike");
            slam.MoveNext();Check(!boss.externalMovement,"slam recovery returns movement control");
            Debug.Log("BOSS COMBAT CHECKS COMPLETE\n"+report);
        }
        finally
        {
            Time.timeScale=1;Set(RunStats.Instance,"<Running>k__BackingField",true);
            health.currentHealth=originalHealth;Set(health,"invincibilityTimer",0f);
            Object.Destroy(boss.gameObject);spawner.enabled=true;
        }
    }
}
