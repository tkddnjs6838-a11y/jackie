using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

public static class ProgressionChecks
{
    static string report;
    static void Check(bool ok,string label){if(!ok)throw new Exception("PROGRESSION FAIL: "+label);report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/progression-checks.txt",report);}
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";var flow=Object.FindFirstObjectByType<GameFlowManager>();var weapons=Object.FindFirstObjectByType<PlayerWeapons>();
        Check(!WeaponTestSettings.AllGunsAtStart && weapons.Equipped==PlayerWeapons.Kind.Sword,"normal run starts with sword");
        Check(!weapons.HasAxe && !weapons.Owns(PlayerWeapons.Kind.Shotgun) && !weapons.Owns(PlayerWeapons.Kind.MachineGun) && !weapons.Owns(PlayerWeapons.Kind.LaserGun),"weapons must be earned");
        flow.startButton.onClick.Invoke();var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var move=weapons.GetComponent<PlayerController>();move.enabled=false;weapons.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        var dropper=Object.FindFirstObjectByType<PickupDropper>();var death=typeof(PickupDropper).GetMethod("HandleEnemyDied",BindingFlags.Instance|BindingFlags.NonPublic);
        var random=UnityEngine.Random.state;int noDropSeed=0;
        for(;noDropSeed<1000;noDropSeed++){UnityEngine.Random.InitState(noDropSeed);if(UnityEngine.Random.value>.5f)break;}
        var fixture=new GameObject("ProgressionVictim");fixture.SetActive(false);fixture.transform.position=weapons.transform.position+Vector3.right*20;var enemy=fixture.AddComponent<Enemy>();
        Action kill=()=>{UnityEngine.Random.InitState(noDropSeed);death.Invoke(dropper,new object[]{enemy});};
        try
        {
            kill();kill();Check(WeaponPickup.ActiveCount==0 && CharacterUpgradePickup.ActiveCount==0,"no rewards in first two controlled kills");
            kill();Check(WeaponPickup.ActiveCount==1 && weapons.CollectAxe(),"third kill grants obtainable axe");yield return null;yield return null;
            kill();kill();Check(CharacterUpgradePickup.ActiveCount==1,"fifth kill introduces augment");
            foreach(var box in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(box.gameObject);yield return null;
            kill();kill();kill();Check(GunPickup.ActiveCount==1,"eighth kill guarantees first shotgun");
            Check(weapons.CollectGun(PlayerWeapons.Kind.Shotgun) && weapons.Owns(PlayerWeapons.Kind.Shotgun),"shotgun acquisition unlocks ammunition-based combat");
            foreach(var box in Object.FindObjectsByType<GunPickup>(FindObjectsSortMode.None))Object.Destroy(box.gameObject);yield return null;
            while(dropper.KillCount<29)kill();Check(CharacterUpgradePickup.ActiveCount==0,"unlucky rolls remain random before protection threshold");
            kill();Check(dropper.KillCount==30 && CharacterUpgradePickup.ActiveCount==1,"25 kills since last augment guarantees a box");
            for(int i=0;i<30;i++)kill();Check(CharacterUpgradePickup.ActiveCount==1,"protection does not pile up boxes already on floor");
            foreach(var box in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(box.gameObject);yield return null;
            kill();Check(CharacterUpgradePickup.ActiveCount==1,"protection retries once the old box is gone");
            foreach(var box in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(box.gameObject);yield return null;
            var growth=weapons.GetComponent<CharacterGrowth>();foreach(CharacterGrowth.Kind kind in Enum.GetValues(typeof(CharacterGrowth.Kind)))while(growth.CanCollect(kind))growth.TryCollect(kind);
            for(int i=0;i<30;i++)kill();Check(CharacterUpgradePickup.ActiveCount==0,"fully upgraded player gets no unusable guaranteed box");
            int[] counts=new int[3];for(int i=0;i<10000;i++)counts[(int)PickupDropper.SelectGun((i+.5f)/10000)-2]++;
            Check(counts[0]==6500 && counts[1]==2900 && counts[2]==600,"gun mix preserves common shotgun medium machine gun rare laser");
            Check(Time.timeScale==1 && !Object.FindFirstObjectByType<UpgradeSystem>().Choosing,"drop events alone never pause or open choices");
            Debug.Log("PROGRESSION CHECKS COMPLETE\n"+report);
        }
        finally{Object.Destroy(fixture);UnityEngine.Random.state=random;spawner.enabled=true;move.enabled=true;}
    }
}
