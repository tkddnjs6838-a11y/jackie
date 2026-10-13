using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class CharacterGrowthChecks
{
    static string report;
    static void Check(bool ok,string message)
    {
        if(!ok)throw new Exception("GROWTH CHECK FAILED: "+message);
        report+="PASS: "+message+"\n";File.WriteAllText("output/gun-effects/growth-checks.txt",report);
    }
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";
        var flow=Object.FindFirstObjectByType<GameFlowManager>();var growth=Object.FindFirstObjectByType<CharacterGrowth>();
        Check(growth!=null && growth.Level(CharacterGrowth.Kind.Power)==0 && growth.Level(CharacterGrowth.Kind.Speed)==0 && growth.Level(CharacterGrowth.Kind.Vitality)==0,"new run has zero growth");
        Check(!growth.TryCollect(CharacterGrowth.Kind.Power),"title screen blocks upgrades");
        flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var health=growth.GetComponent<PlayerHealth>();health.GrantInvincibility(1000);
        var move=growth.GetComponent<PlayerController>();move.enabled=false;
        CharacterUpgradePickup pickup=null;
        try
        {
            var victim=new GameObject("GrowthDropFixture");victim.SetActive(false);
            victim.transform.position=growth.transform.position+Vector3.right*10;
            var enemy=victim.AddComponent<Enemy>();
            var dropper=Object.FindFirstObjectByType<PickupDropper>();
            var onDeath=typeof(PickupDropper).GetMethod("HandleEnemyDied",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            for(int i=0;i<4;i++)onDeath.Invoke(dropper,new object[]{enemy});
            Check(CharacterUpgradePickup.ActiveCount==0,"no growth drop before fifth kill");
            onDeath.Invoke(dropper,new object[]{enemy});
            Check(CharacterUpgradePickup.ActiveCount==1,"fifth kill guarantees first growth crate");
            foreach(var crate in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(crate.gameObject);
            Object.Destroy(victim);yield return null;
            float originalHealth=health.maxHealth;health.currentHealth-=20;
            Time.timeScale=0;
            Check(!growth.TryCollect(CharacterGrowth.Kind.Vitality),"paused direct grants are blocked");
            Time.timeScale=1;
            Check(growth.TryCollect(CharacterGrowth.Kind.Vitality),"vitality grant succeeds");
            Check(health.maxHealth==originalHealth+15 && health.currentHealth==originalHealth-5,"vitality adds 15 max health and heals 15");
            Check(growth.TryCollect(CharacterGrowth.Kind.Power) && Mathf.Approximately(growth.DamageMultiplier,1.1f),"power adds ten percent");
            Check(growth.TryCollect(CharacterGrowth.Kind.Speed) && Mathf.Approximately(growth.MoveMultiplier,1.05f),"speed adds five percent");
            var effects=growth.GetComponent<PowerUpEffects>();effects.Activate(PowerUpEffects.Kind.Rage);
            Check(Mathf.Approximately(growth.DamageMultiplier*effects.DamageMultiplier,2.2f),"temporary rage multiplies permanent growth");
            effects.enabled=false;
            Check(Mathf.Approximately(growth.DamageMultiplier,1.1f),"temporary buff expiry preserves growth");effects.enabled=true;
            var attack=growth.GetComponent<PlayerAutoAttack>();
            var dash=growth.GetComponent<PlayerDash>();
            float oldRange=attack.manualAttackRange,oldAngle=attack.attackAngle,oldHeal=attack.lifestealPerHit,oldDash=dash.cooldown;
            var gun=growth.GetComponent<PlayerGunCombat>();float oldInterval=gun.FireInterval;
            foreach(var kind in new[]{CharacterGrowth.Kind.QuickHands,CharacterGrowth.Kind.LongReach,CharacterGrowth.Kind.WideSwing,CharacterGrowth.Kind.BloodEdge,CharacterGrowth.Kind.SecondWind})
            {
                Check(growth.TryCollect(kind) && growth.Level(kind)==1,kind+" applies once");
            }
            Check(Mathf.Approximately(gun.FireInterval,oldInterval*.85f),"quick hands reduces gun fire interval");
            Check(Mathf.Approximately(growth.CooldownMultiplier,.85f),"quick hands provides melee cooldown multiplier");
            Check(Mathf.Approximately(attack.manualAttackRange,Mathf.Min(6,oldRange+.4f)),"long reach increases melee range");
            Check(Mathf.Approximately(attack.attackAngle,Mathf.Min(300,oldAngle+25)),"wide swing increases melee arc");
            Check(Mathf.Approximately(attack.lifestealPerHit,Mathf.Min(9,oldHeal+3)),"blood edge adds healing on melee hit");
            Check(Mathf.Approximately(dash.cooldown,Mathf.Max(.5f,oldDash-.4f)),"second wind reduces dash cooldown");
            var oldUI=Object.FindFirstObjectByType<UpgradeSystem>();
            var killFixture=new GameObject("AugmentMilestoneFixture");killFixture.SetActive(false);
            var killEnemy=killFixture.AddComponent<Enemy>();
            // Exercise real death subscribers across every former milestone, including an elite.
            var deathEvent=typeof(Enemy).GetField("OnEnemyDied",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var notify=deathEvent.GetValue(null) as Action<Enemy>;
            for(int i=0;i<310;i++)notify?.Invoke(killEnemy);
            killFixture.AddComponent<EliteMarker>();notify?.Invoke(killEnemy);
            Object.Destroy(killFixture);
            Check(Time.timeScale==1 && (oldUI==null || oldUI.panel==null || !oldUI.panel.activeSelf),"310 kills and elite kill never open a choice or pause combat");
            foreach(var crate in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(crate.gameObject);
            yield return null;
            foreach(CharacterGrowth.Kind kind in Enum.GetValues(typeof(CharacterGrowth.Kind)))
            {
                while(growth.CanCollect(kind))Check(growth.TryCollect(kind),kind+" stack "+growth.Level(kind));
                Check(!growth.TryCollect(kind),kind+" rejects upgrades at cap");
            }
            Check(!growth.TryRandomAvailable(out _),"all capped stats stop growth drops");
            Check(!growth.TryCollect((CharacterGrowth.Kind)99),"invalid upgrade is rejected");
            pickup=CharacterUpgradePickup.Spawn(growth.transform.position,CharacterGrowth.Kind.Power);
            yield return null;yield return null;Check(pickup!=null,"capped upgrade remains on floor");
            pickup.lifetime=.02f;yield return new WaitForSeconds(.06f);yield return null;
            Check(pickup==null && CharacterUpgradePickup.ActiveCount==0,"expired pickup releases active limit");
            Debug.Log("CHARACTER GROWTH CHECKS COMPLETE\n"+report);
        }
        finally{Time.timeScale=1;spawner.enabled=true;move.enabled=true;if(pickup!=null)Object.Destroy(pickup.gameObject);}
    }
}
