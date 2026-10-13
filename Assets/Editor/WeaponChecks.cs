using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Object=UnityEngine.Object;

public static class WeaponChecks
{
    static readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    [MenuItem("Tools/Validation/Run Weapon Checks")]
    public static void Run()
    {
        var flow=Object.FindFirstObjectByType<GameFlowManager>();
        if(!EditorApplication.isPlaying || RunStats.Instance.Running){Debug.LogWarning("Weapon checks require Play Mode title screen.");return;}
        flow.StartCoroutine(Check(flow));
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception("WEAPON CHECK: "+message);Debug.Log("WEAPON PASS: "+message);}
    static IEnumerator Check(GameFlowManager flow)
    {
        var attack=Object.FindFirstObjectByType<PlayerAutoAttack>();var weapons=attack.GetComponent<PlayerWeapons>();
        Require(weapons!=null && !weapons.HasAxe && !weapons.TryEquip(PlayerWeapons.Kind.Axe),"new run starts with sword and locked axe");
        flow.startButton.onClick.Invoke();Object.FindFirstObjectByType<PlayerHealth>().GrantInvincibility(1000);
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        for(int i=0;i<3;i++){var enemy=Object.Instantiate(spawner.enemyPrefab,attack.transform.position+Vector3.forward*4,Quaternion.identity).GetComponent<Enemy>();enemy.TakeDamage(100000);}
        yield return null;
        var pickup=Object.FindFirstObjectByType<WeaponPickup>();Require(pickup!=null && WeaponPickup.ActiveCount==1,"third kill drops one axe crate");
        pickup.transform.position=attack.transform.position;
        yield return null;yield return null;
        Require(weapons.HasAxe && weapons.Equipped==PlayerWeapons.Kind.Axe,"nearby crate collects and equips axe");
        Require(WeaponPickup.ActiveCount==0,"collected crate removed");
        Require(weapons.TryEquip(PlayerWeapons.Kind.Sword) && weapons.DamageScale==1f,"switch back preserves sword stats");
        Require(weapons.TryEquip(PlayerWeapons.Kind.Axe) && weapons.DamageScale>1 && weapons.ArcBonus>0,"axe has heavier wider attack");
        var swordRenderer=attack.swordModel.GetComponentInChildren<Renderer>();
        Require(!swordRenderer.enabled,"sword mesh hidden while axe is equipped");
        var oldAngle=attack.attackAngle;attack.attackAngle=160;
        var victims=new Enemy[3];float[] angles={0,100,150};
        for(int i=0;i<3;i++)
        {
            var at=attack.transform.position+Quaternion.Euler(0,angles[i],0)*attack.transform.forward*1.8f;
            victims[i]=Object.Instantiate(spawner.enemyPrefab,at,Quaternion.identity).GetComponent<Enemy>();
            victims[i].maxHealth=1000;victims[i].RefreshConfiguredStatsAndColor();victims[i].externalMovement=true;
        }
        typeof(PlayerAutoAttack).GetMethod("Swing",flags).Invoke(attack,new object[]{false,false});
        Require(!weapons.TryEquip(PlayerWeapons.Kind.Sword),"switching blocked during swing");
        yield return new WaitForSeconds(attack.manualAttackCooldown*1.85f+.2f);
        Require(victims[0].CurrentHealth<1000 && victims[1].CurrentHealth<1000 && victims[2].CurrentHealth==1000,"wide arc hits front and side, excludes rear");
        Require(Mathf.Abs((1000-victims[0].CurrentHealth)-attack.manualAttackDamage*2.1f)<.1f,"axe damage multiplier applied once");
        var beforeContact=AxeSwingMotion.Evaluate(AxeSwingMotion.ContactPhase-.005f);
        var afterContact=AxeSwingMotion.Evaluate(AxeSwingMotion.ContactPhase+.005f);
        Require(beforeContact.pitch>afterContact.pitch && beforeContact.wrist.y>afterContact.wrist.y,"axe carries downward motion through impact");
        for(int i=28;i<=62;i++)
        {
            var pose=AxeSwingMotion.Evaluate(i/100f);
            Require(Mathf.Abs(pose.yaw-15f)<.01f && Mathf.Abs(pose.twist)<.01f && pose.wrist.x>0f,"downward chop stays on the weapon side without lateral sweep: "+i);
        }
        Require(Resources.Load<AudioClip>("Audio/AxeHit")!=null,"axe impact audio is included");
        attack.attackAngle=oldAngle;
        foreach(var victim in victims)Object.Destroy(victim.gameObject);
        Directory.CreateDirectory("output/weapons");
        ScreenCapture.CaptureScreenshot("output/weapons/axe-game.png");
        // Close-up of actual posed character and held axe, independent of gameplay camera framing.
        var camera=new GameObject("WeaponReviewCamera",typeof(Camera)).GetComponent<Camera>();
        var bounds=new Bounds(attack.transform.position,Vector3.zero);
        foreach(var renderer in attack.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
        camera.transform.position=bounds.center+attack.transform.rotation*new Vector3(2,1,3.5f);
        camera.transform.LookAt(bounds.center);camera.orthographic=true;camera.orthographicSize=Mathf.Max(1.1f,bounds.extents.magnitude);
        var rt=new RenderTexture(800,800,24);camera.targetTexture=rt;
        var previous=RenderTexture.active;var texture=new Texture2D(800,800,TextureFormat.RGB24,false);
        attack.enabled=false; bool animated=attack.animator.enabled; attack.animator.enabled=false; camera.enabled=false;
        float lowestClearance=float.PositiveInfinity;
        var heldAxe=attack.swordModel.Find("Axe(Clone)");
        Require(heldAxe!=null,"equipped axe exists for clearance check");
        for(int frame=0;frame<=50;frame++)
        {
            float phase=frame/50f;
            attack.ApplySwordPose(phase,phase>0 && phase<1);
            yield return new WaitForEndOfFrame();
            foreach(var filter in heldAxe.GetComponentsInChildren<MeshFilter>())
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var world=filter.transform.TransformPoint(vertex);
                    float ground=Terrain.activeTerrain!=null ? Terrain.activeTerrain.SampleHeight(world)+Terrain.activeTerrain.transform.position.y : attack.transform.position.y;
                    lowestClearance=Mathf.Min(lowestClearance,world.y-ground);
                }
            camera.Render();RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,800,800),0,0);texture.Apply();
            File.WriteAllBytes("output/weapons/axe-pose-"+Mathf.RoundToInt(phase*100)+".png",texture.EncodeToPNG());
        }
        attack.ResetSwordPose();attack.animator.enabled=animated;attack.enabled=true;RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(camera.gameObject);
        File.WriteAllText("output/weapons/clearance.txt", "Minimum axe vertex clearance across 51 animation samples: "+lowestClearance.ToString("F4")+" m");
        Require(lowestClearance>=.08f,"axe clears terrain by at least 8 cm in sampled poses: "+lowestClearance);
        File.WriteAllText("output/weapons/checks.txt","PASS: initial sword/locked axe, third-kill single crate, collection/removal, sword switch/stat preservation, axe scale, sword hidden, attack switch lock, wide arc target exclusion and damage multiplier.\n");
        Debug.Log("WEAPON CHECKS COMPLETE");
    }
}
