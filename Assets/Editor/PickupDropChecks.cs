using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
public static class PickupDropChecks
{
    static string report;
    static void Check(bool ok,string label){if(!ok)throw new Exception("DROP CHECK FAILED: "+label);report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/drop-checks.txt",report);}
    static int Count()=>WeaponPickup.ActiveCount+GunPickup.ActiveCount+CharacterUpgradePickup.ActiveCount+PowerUp.ActiveCount;
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static void ClearRewards()
    {
        foreach(var x in Object.FindObjectsByType<GunPickup>(FindObjectsSortMode.None))Object.Destroy(x.gameObject);
        foreach(var x in Object.FindObjectsByType<CharacterUpgradePickup>(FindObjectsSortMode.None))Object.Destroy(x.gameObject);
        foreach(var x in Object.FindObjectsByType<PowerUp>(FindObjectsSortMode.None))Object.Destroy(x.gameObject);
    }
    static IEnumerator Test()
    {
        report="";var random=UnityEngine.Random.state;UnityEngine.Random.InitState(614);
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var weapons=Object.FindFirstObjectByType<PlayerWeapons>();var controller=weapons.GetComponent<PlayerController>();controller.enabled=false;
        weapons.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        var originalPosition=weapons.transform.position;
        var dropper=Object.FindFirstObjectByType<PickupDropper>();
        var method=typeof(PickupDropper).GetMethod("HandleEnemyDied",BindingFlags.Instance|BindingFlags.NonPublic);
        var victim=new GameObject("DropFixture");victim.SetActive(false);victim.transform.position=weapons.transform.position+Vector3.right*12;
        var enemy=victim.AddComponent<Enemy>();
        try
        {
            for(int i=1;i<=8;i++)
            {
                int before=Count();method.Invoke(dropper,new object[]{enemy});
                Check(Count()-before<=1,"intro kill "+i+" produces at most one reward");
                if(i==3)Check(WeaponPickup.ActiveCount==1,"third kill introduces axe crate");
                if(i==5)Check(CharacterUpgradePickup.ActiveCount==1,"fifth kill introduces growth crate");
            }
            weapons.CollectAxe();
            if(!weapons.Owns(PlayerWeapons.Kind.Shotgun))weapons.CollectGun(PlayerWeapons.Kind.Shotgun);
            yield return null;yield return null;
            ClearRewards();yield return null;
            for(int i=0;i<120;i++)
            {
                int before=Count();method.Invoke(dropper,new object[]{enemy});
                if(Count()-before>1)throw new Exception("Multiple categories dropped by one victim");
                if(GunPickup.ActiveCount>2 || CharacterUpgradePickup.ActiveCount>2 || PowerUp.ActiveCount>dropper.maxActivePowerUps)throw new Exception("Drop cap exceeded");
            }
            Check(true,"120 kills keep one reward per victim and category caps");
            int[] totals=new int[3];for(int i=0;i<10000;i++)totals[(int)PickupDropper.SelectGun((i+.5f)/10000)-2]++;
            Check(totals[0]==6500 && totals[1]==2900 && totals[2]==600,"gun rarity weights are 65 / 29 / 6 percent");
            ClearRewards();yield return null;
            victim.AddComponent<EliteMarker>();
            method.Invoke(dropper,new object[]{enemy});Check(PowerUp.ActiveCount==1,"elite prioritizes one timed reward");
            method.Invoke(dropper,new object[]{enemy});Check(PowerUp.ActiveCount==1,"elite cannot exceed timed reward cap");
            ClearRewards();yield return null;
            Vector3 at=weapons.transform.position+Vector3.right*50;
            if(Terrain.activeTerrain!=null)at.y=Terrain.activeTerrain.SampleHeight(at)+Terrain.activeTerrain.transform.position.y;
            GunPickup.Spawn(at,PlayerWeapons.Kind.Shotgun);
            CharacterUpgradePickup.Spawn(at+Vector3.right*1.6f,CharacterGrowth.Kind.Vitality);
            PowerUp.Spawn(at+Vector3.right*3.2f,PowerUpEffects.Kind.Shield);
            yield return null;
            Check(Object.FindObjectsByType<PickupCrateVisual>(FindObjectsSortMode.None).Length==3,"three reward categories have separate crate markers");
            foreach(var view in Object.FindObjectsByType<PickupCrateVisual>(FindObjectsSortMode.None))foreach(var renderer in view.GetComponentsInChildren<MeshRenderer>())
                if(renderer.sharedMaterial==null || !renderer.sharedMaterial.shader.isSupported)throw new Exception("Crate material unsupported");
            Check(true,"crate materials are supported");
            var camera=new GameObject("DropReviewCamera").AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=at+new Vector3(1.6f,3,-5);camera.transform.LookAt(at+new Vector3(1.6f,.7f,0));camera.orthographic=true;camera.orthographicSize=2.1f;
            var rt=new RenderTexture(1000,600,24);var old=RenderTexture.active;var texture=new Texture2D(1000,600,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1000,600),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/item-crates.png",texture.EncodeToPNG());}
            finally{RenderTexture.active=old;camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);}
            var gun=weapons.GetComponent<PlayerGunCombat>();
            var reserves=(int[])typeof(PlayerGunCombat).GetField("reserve",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(gun);reserves[0]=0;
            yield return new WaitForSeconds(.8f);
            weapons.transform.position=at;yield return null;yield return null;
            Check(GunPickup.ActiveCount==0 && reserves[0]==8,"weapon crate replenishes ammo and disappears");
            var growth=weapons.GetComponent<CharacterGrowth>();int level=growth.Level(CharacterGrowth.Kind.Vitality);
            weapons.transform.position=at+Vector3.right*1.6f;yield return null;yield return null;
            var choices=Object.FindFirstObjectByType<UpgradeSystem>();
            Check(CharacterUpgradePickup.ActiveCount==0 && choices.Choosing && Time.timeScale==0,"growth crate opens selection and disappears");
            var chosen=choices.Offered[0];int previousLevel=growth.Level(chosen);
            Check(choices.Choose(0) && growth.Level(chosen)==previousLevel+1 && Time.timeScale==1,"selected augment applies once and resumes");
            weapons.transform.position=at+Vector3.right*3.2f;yield return null;yield return null;
            Check(PowerUp.ActiveCount==0 && PowerUpEffects.Instance.ShieldActive,"timed crate activates shield and disappears");
            Debug.Log("PICKUP DROP CHECKS COMPLETE\n"+report);
        }
        finally{weapons.transform.position=originalPosition;UnityEngine.Random.state=random;ClearRewards();Object.Destroy(victim);spawner.enabled=true;controller.enabled=true;}
    }
}
