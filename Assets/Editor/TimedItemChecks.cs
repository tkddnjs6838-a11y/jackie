using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Object=UnityEngine.Object;
public static class TimedItemChecks
{
    static string report;
    static void Check(bool ok,string label){if(!ok)throw new Exception("TIMED ITEM CHECK FAILED: "+label);report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/timed-checks.txt",report);}
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";
        var flow=Object.FindFirstObjectByType<GameFlowManager>();var effects=PowerUpEffects.Instance;
        Check(!effects.Active && PowerUpEffects.EnemySpeedScale==1,"new run has no temporary effect");
        flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var player=effects.transform;var movement=player.GetComponent<PlayerController>();movement.enabled=false;
        var health=player.GetComponent<PlayerHealth>();var gun=player.GetComponent<PlayerGunCombat>();gun.ReadInput=false;
        var weapons=player.GetComponent<PlayerWeapons>();
        var enemies=Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var enabled=new bool[enemies.Length];
        for(int i=0;i<enemies.Length;i++){enabled[i]=enemies[i].enabled;enemies[i].enabled=false;}
        try
        {
            Check(effects.rageSeconds==5 && effects.frenzySeconds==5 && effects.slowSeconds==5 && effects.shieldSeconds==3,"scene durations are 5 seconds, shield 3 seconds");
            PowerUp.Spawn(player.position,PowerUpEffects.Kind.Rage);
            Time.timeScale=0;yield return null;yield return null;
            Check(!effects.Active && PowerUp.ActiveCount==1,"pause prevents pickup");
            Time.timeScale=1;yield return null;yield return null;
            Check(effects.ActiveKind==PowerUpEffects.Kind.Rage && effects.DamageMultiplier==2 && PowerUp.ActiveCount==0,"pickup activates rage once and disappears");
            float remaining=effects.Remaining;Time.timeScale=0;yield return new WaitForSecondsRealtime(.1f);
            Check(effects.Remaining==remaining,"pause freezes effect timer");Time.timeScale=1;
            effects.Activate(PowerUpEffects.Kind.Rage);
            Check(effects.Remaining==5 && effects.DamageMultiplier==2,"same effect refreshes duration without stacking strength");
            effects.Activate(PowerUpEffects.Kind.Slow);
            Check(PowerUpEffects.EnemySpeedScale==.45f && effects.DamageMultiplier==1,"slow replaces rage");
            effects.Activate(PowerUpEffects.Kind.Frenzy);
            Check(PowerUpEffects.EnemySpeedScale==1 && effects.CooldownMultiplier==.5f,"replacing slow restores enemy speed");
            foreach(var kind in new[]{PlayerWeapons.Kind.Shotgun,PlayerWeapons.Kind.MachineGun,PlayerWeapons.Kind.LaserGun})
            {
                weapons.TryEquip(kind);float fast=gun.FireInterval;
                effects.Activate(PowerUpEffects.Kind.Rage);float normal=gun.FireInterval;
                Check(Mathf.Approximately(fast,normal*.5f),kind+" frenzy halves firing interval");
                effects.Activate(PowerUpEffects.Kind.Frenzy);
            }
            effects.Activate(PowerUpEffects.Kind.Shield);float hp=health.currentHealth;
            health.TakeDamage(10);Check(health.currentHealth==hp,"shield blocks incoming damage");
            yield return new WaitForSeconds(3.1f);
            Check(!effects.Active && !effects.ShieldActive,"shield expires after three seconds");
            health.TakeDamage(10);Check(health.currentHealth==hp-10,"damage resumes after shield expires");
            effects.Activate(PowerUpEffects.Kind.Slow);yield return new WaitForSeconds(5.1f);
            Check(!effects.Active && PowerUpEffects.EnemySpeedScale==1,"slow expires and restores enemy speed");
            effects.Activate(PowerUpEffects.Kind.Rage);
            effects.Activate((PowerUpEffects.Kind)99);Check(effects.ActiveKind==PowerUpEffects.Kind.Rage,"invalid effect leaves valid effect intact");
            effects.enabled=false;Check(!effects.Active && PowerUpEffects.EnemySpeedScale==1,"disable clears temporary state");effects.enabled=true;
            Debug.Log("TIMED ITEM CHECKS COMPLETE\n"+report);
        }
        finally{for(int i=0;i<enemies.Length;i++)if(enemies[i]!=null)enemies[i].enabled=enabled[i];Time.timeScale=1;effects.enabled=false;effects.enabled=true;spawner.enabled=true;movement.enabled=true;gun.ReadInput=true;}
    }
}
