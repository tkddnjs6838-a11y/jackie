using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GunCombatChecks
{
    static GunCombatChecks(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string path="output/gun-effects/request.txt";
        if(EditorApplication.isCompiling || !EditorApplication.isPlaying || !File.Exists(path))return;
        var action=File.ReadAllText(path).Trim();File.Delete(path);if(action=="pacing")EnemyPacingChecks.Run();else if(action=="progression")ProgressionChecks.Run();else if(action=="inventory")AugmentInventoryChecks.Run();else if(action=="choices")AugmentChoiceChecks.Run();else if(action=="loadout")CheckTestLoadout();else if(action=="drops")PickupDropChecks.Run();else if(action=="timed")TimedItemChecks.Run();else if(action=="growth")CharacterGrowthChecks.Run();else if(action=="regression")CombatRegressionChecks.Run();else Run();
    }
    static void CheckTestLoadout()
    {
        var weapons=Object.FindFirstObjectByType<PlayerWeapons>();
        var gun=weapons.GetComponent<PlayerGunCombat>();
        if(!WeaponTestSettings.AllGunsAtStart || weapons.Equipped!=PlayerWeapons.Kind.Shotgun)throw new Exception("Test preset did not start with shotgun");
        var result="PASS: fresh run defaults to shotgun\n";
        foreach(var kind in new[]{PlayerWeapons.Kind.Shotgun,PlayerWeapons.Kind.MachineGun,PlayerWeapons.Kind.LaserGun})
        {
            if(!weapons.Owns(kind) || !weapons.TryEquip(kind) || gun.Ammo!=PlayerGunCombat.Capacity(kind) || gun.Reserve<=0)
                throw new Exception("Test loadout unavailable: "+kind);
            result+="PASS: "+kind+" unlocked, switchable, loaded "+gun.Ammo+" / "+gun.Reserve+"\n";
        }
        weapons.TryEquip(PlayerWeapons.Kind.Shotgun);
        File.WriteAllText("output/gun-effects/startup-test.txt",result);Debug.Log("GUN TEST LOADOUT PASS\n"+result);
    }
    [MenuItem("Tools/Validation/Run Gun Combat Checks")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying || RunStats.Instance.Running){Debug.LogWarning("Gun checks require Play title screen");return;}
        Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Check());
    }
    static string report;
    static void CheckThat(bool value,string text){if(!value)throw new Exception("GUN CHECK FAILED: "+text);report+="PASS: "+text+"\n";File.WriteAllText("output/gun-effects/checks.txt",report);}
    static IEnumerator Check()
    {
        report="";Directory.CreateDirectory("output/gun-effects");
        var flow=Object.FindFirstObjectByType<GameFlowManager>();var attack=Object.FindFirstObjectByType<PlayerAutoAttack>();
        var weapons=attack.GetComponent<PlayerWeapons>();var gun=attack.GetComponent<PlayerGunCombat>();
        var controller=attack.GetComponent<PlayerController>();var spawner=Object.FindFirstObjectByType<EnemySpawner>();
        flow.startButton.onClick.Invoke();Object.FindFirstObjectByType<PlayerHealth>().GrantInvincibility(1000);spawner.enabled=false;controller.enabled=false;gun.ReadInput=false;
        var targets=new Enemy[2];GameObject wall=null;
        var originalPlayerPosition=attack.transform.position;
        var aimCamera=Camera.main;var oldCameraPosition=aimCamera.transform.position;var oldCameraRotation=aimCamera.transform.rotation;
        var shake=CameraShake.Instance;bool shakeEnabled=shake!=null && shake.enabled;if(shake!=null)shake.enabled=false;gun.AimCamera=aimCamera;
        try
        {
            foreach(var kind in new[]{PlayerWeapons.Kind.Shotgun,PlayerWeapons.Kind.MachineGun,PlayerWeapons.Kind.LaserGun})
            {
                CheckThat((weapons.Owns(kind) || weapons.CollectGun(kind)) && weapons.TryEquip(kind),kind+" collects and equips");gun.ApplyPose();
                for(int i=0;i<2;i++)
                {
                    targets[i]=Object.Instantiate(spawner.enemyPrefab,attack.transform.position+attack.transform.forward*(3+i*2),Quaternion.identity).GetComponent<Enemy>();
                    targets[i].maxHealth=2000;targets[i].RefreshConfiguredStatsAndColor();targets[i].externalMovement=true;
                    var col=targets[i].GetComponent<Collider>();if(col!=null)targets[i].transform.position+=Vector3.up*(weapons.GunMuzzle.position.y-col.bounds.center.y);
                }
                aimCamera.transform.position=attack.transform.position+attack.transform.rotation*new Vector3(-.65f,1.8f,-3f);
                aimCamera.transform.LookAt(targets[0].GetComponent<Collider>().bounds.center);
                Physics.SyncTransforms();yield return null;
                CheckThat(gun.ReticleVisible,kind+" shows reticle when equipped");
                gun.ApplyPose();
                if(kind==PlayerWeapons.Kind.LaserGun)
                {
                    var ray=(gun.AimPoint()-weapons.GunMuzzle.position).normalized;
                    var point=gun.AimPoint()+ray*2;
                    targets[1].transform.position+=point-targets[1].GetComponent<Collider>().bounds.center;
                    Physics.SyncTransforms();
                }
                int shots=gun.ShotsFired;int ammo=gun.Ammo;
                gun.SetTrigger(true);float deadline=Time.time+2;
                while(gun.ShotsFired==shots && Time.time<deadline)yield return null;
                gun.SetTrigger(false);
                CheckThat(gun.ShotsFired==shots+1,kind+" fires once");
                var aimDirection=(gun.AimPoint()-gun.LastShotOrigin).normalized;
                CheckThat(Vector3.Dot(aimDirection,gun.LastShotDirection)>(kind==PlayerWeapons.Kind.MachineGun?.995f:.999f),kind+" follows screen-centre aim with camera offset");CheckThat(gun.Ammo==ammo-1,kind+" spends one round");
                CheckThat(gun.LastHitCount==(kind==PlayerWeapons.Kind.LaserGun?2:1),kind+" correct target penetration");
                yield return new WaitForEndOfFrame();Capture(attack,kind.ToString());
                if(kind==PlayerWeapons.Kind.Shotgun)
                {
                    yield return new WaitForSeconds(.07f);yield return new WaitForEndOfFrame();
                    Capture(attack,"Shotgun-pellets");
                    var review=new GameObject("ShotgunGameReview").AddComponent<Camera>();
                    review.CopyFrom(aimCamera);review.enabled=false;
                    review.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);
                    CaptureCamera(review,"shotgun-game-review");Object.Destroy(review.gameObject);
                    ScreenCapture.CaptureScreenshot("output/gun-effects/shotgun-pellets-game.png");
                }
                aimCamera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);
                ScreenCapture.CaptureScreenshot("output/gun-effects/visible-"+kind+".png");
                if(kind==PlayerWeapons.Kind.LaserGun)
                {
                    targets[0].transform.position=attack.transform.position+attack.transform.forward*.7f;
                    aimCamera.transform.LookAt(targets[0].GetComponent<Collider>().bounds.center);
                    CheckThat(Vector3.Dot(gun.PoseDirection(),attack.transform.forward)>.8f && Mathf.Abs(gun.PoseDirection().y)<.44f,"close target keeps barrel forward instead of flipping upright");
                    yield return null;yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot("output/gun-effects/close-target.png");
                }
                foreach(var target in targets)if(target!=null)Object.Destroy(target.gameObject);
                yield return new WaitForSeconds(.7f);
            }
            CheckThat(Resources.Load<AudioClip>("Audio/LaserCharge")!=null && Resources.Load<AudioClip>("Audio/PistolShot")!=null && Resources.Load<AudioClip>("Audio/MachineShot")!=null,"distinct shot and charge audio loaded");
            weapons.TryEquip(PlayerWeapons.Kind.MachineGun);int burstStart=gun.ShotsFired;gun.SetTrigger(true);
            float burstLimit=Time.time+5;while(!gun.IsReloading && Time.time<burstLimit)yield return null;gun.SetTrigger(false);
            CheckThat(gun.IsReloading && gun.Ammo==0 && gun.ShotsFired-burstStart==29,"held machine gun drains remaining magazine and auto reloads");
            yield return new WaitForSeconds(1.9f);CheckThat(gun.Ammo==30 && gun.Reserve==30,"automatic reload replenishes magazine from reserve");
            weapons.TryEquip(PlayerWeapons.Kind.Shotgun);int oldReserve=gun.Reserve;gun.BeginReload();CheckThat(gun.IsReloading,"manual reload begins");
            Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);CheckThat(!gun.ReticleVisible,"pause hides reticle");CheckThat(gun.IsReloading && gun.Reserve==oldReserve,"pause freezes reload");Time.timeScale=1;
            yield return new WaitForSeconds(1.6f);CheckThat(gun.Ammo==2 && gun.Reserve==oldReserve-1,"reload transfers exact missing ammo");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="GunCheckWall";wall.transform.position=attack.transform.position+attack.transform.forward*2+Vector3.up*.8f;wall.transform.localScale=new Vector3(3,3,.25f);wall.transform.rotation=attack.transform.rotation;
            targets[0]=Object.Instantiate(spawner.enemyPrefab,attack.transform.position+attack.transform.forward*4,Quaternion.identity).GetComponent<Enemy>();targets[0].externalMovement=true;float health=targets[0].CurrentHealth;
            Physics.SyncTransforms();int prior=gun.ShotsFired;gun.SetTrigger(true);float limit=Time.time+1;while(gun.ShotsFired==prior && Time.time<limit)yield return null;gun.SetTrigger(false);
            CheckThat(gun.LastHitCount==0 && targets[0].CurrentHealth==health,"wall blocks bullet damage");
            gun.BeginReload();weapons.TryEquip(PlayerWeapons.Kind.LaserGun);CheckThat(!gun.IsReloading,"weapon switch cancels reload");
            yield return new WaitForSeconds(.8f);gun.SetTrigger(true);yield return new WaitForSeconds(.1f);CheckThat(gun.IsCharging,"laser charges before firing");
            weapons.TryEquip(PlayerWeapons.Kind.Sword);CheckThat(!gun.IsCharging && !weapons.IsGun,"switch cancels laser charge and restores sword");
            yield return null;CheckThat(!gun.ReticleVisible,"sword hides reticle");
            if(wall!=null){Object.Destroy(wall);wall=null;}
            foreach(var target in targets)if(target!=null)Object.Destroy(target.gameObject);
            yield return null;
            weapons.TryEquip(PlayerWeapons.Kind.Shotgun);gun.BeginReload();yield return new WaitForSeconds(1.6f);
            attack.transform.position+=Vector3.right*1000;
            // Move the aim camera with the fixture before posing: the old camera was
            // left 1000 metres away and made the wrist point towards the previous arena.
            aimCamera.transform.position=attack.transform.position+Vector3.up*.8f-attack.transform.forward*2;
            aimCamera.transform.rotation=attack.transform.rotation;
            yield return null;gun.ApplyPose();
            Vector3 fanOrigin=weapons.GunMuzzle.position;
            Vector3 fanForward=(gun.AimPoint()-fanOrigin).normalized;
            for(int i=0;i<2;i++)
            {
                var direction=Quaternion.AngleAxis(i==0?-13.5f:13.5f,Vector3.up)*fanForward;
                targets[i]=Object.Instantiate(spawner.enemyPrefab,fanOrigin+direction*4,Quaternion.identity).GetComponent<Enemy>();
                targets[i].maxHealth=2000;targets[i].RefreshConfiguredStatsAndColor();targets[i].externalMovement=true;
                // This fixture checks horizontal spread; allow muzzle-height differences between gun designs.
                var spreadCapsule=targets[i].GetComponent<CapsuleCollider>();
                if(spreadCapsule!=null)spreadCapsule.height=3f/Mathf.Abs(targets[i].transform.lossyScale.y);
                targets[i].transform.position+=Vector3.up*(fanOrigin.y-targets[i].GetComponent<Collider>().bounds.center.y);
            }
            Physics.SyncTransforms();yield return null;
            int[] knockbackBefore={targets[0].KnockbackApplications,targets[1].KnockbackApplications};
            int spreadStart=gun.ShotsFired;gun.SetTrigger(true);
            float spreadLimit=Time.time+2;while(gun.ShotsFired==spreadStart && Time.time<spreadLimit)yield return null;gun.SetTrigger(false);
            CheckThat(gun.LastHitCount==2,"shotgun spread hits enemies on both sides");
            CheckThat(targets[0].KnockbackApplications==knockbackBefore[0]+1 && targets[1].KnockbackApplications==knockbackBefore[1]+1,"one knockback per enemy per shot: increments="+(targets[0].KnockbackApplications-knockbackBefore[0])+","+(targets[1].KnockbackApplications-knockbackBefore[1]));
            var before=targets[0].transform.position;yield return new WaitForSeconds(.25f);
            CheckThat(Vector3.Dot(targets[0].transform.position-before,fanForward)>.15f,"shotgun visibly pushes surviving enemy away");
            CheckThat(PlayerGunCombat.ShotgunPelletDamage(2)>PlayerGunCombat.ShotgunPelletDamage(8),"shotgun damage falls with distance");
            gun.SetTrigger(true);spreadStart=gun.ShotsFired;spreadLimit=Time.time+2;
            while(gun.ShotsFired==spreadStart && Time.time<spreadLimit)yield return null;gun.SetTrigger(false);
            CheckThat(gun.Ammo==0 && gun.IsReloading,"two shells trigger automatic reload");
            yield return new WaitForSeconds(1.6f);CheckThat(gun.Ammo==2,"shotgun reload restores two shells");
            // Isolated wall and boss impulse fixtures, away from scene props.
            var testPosition=attack.transform.position+attack.transform.right*20;
            targets[0].transform.position=testPosition;targets[1].transform.position=testPosition+attack.transform.right*5;
            targets[1].isBoss=true;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=testPosition+fanForward*1.2f+Vector3.up*.5f;
            wall.transform.rotation=attack.transform.rotation;wall.transform.localScale=new Vector3(4,4,.2f);
            Physics.SyncTransforms();
            targets[0].ApplyKnockback(fanForward,11);targets[1].ApplyKnockback(fanForward,11);
            before=targets[0].transform.position;var bossBefore=targets[1].transform.position;
            yield return new WaitForSeconds(.4f);
            float moved=Vector3.Dot(targets[0].transform.position-before,fanForward);
            float bossMoved=Vector3.Dot(targets[1].transform.position-bossBefore,fanForward);
            CheckThat(moved<1.1f,"knockback stops before wall");
            CheckThat(bossMoved<.15f,"boss strongly resists knockback");
            // Set boundary inventory states only in the test harness, then exercise public actions.
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var mags=(int[])typeof(PlayerGunCombat).GetField("magazine",flags).GetValue(gun);
            var reserves=(int[])typeof(PlayerGunCombat).GetField("reserve",flags).GetValue(gun);
            var owned=(bool[])typeof(PlayerWeapons).GetField("owned",flags).GetValue(weapons);
            weapons.TryEquip(PlayerWeapons.Kind.Sword);
            owned[1]=false;mags[1]=reserves[1]=0;
            CheckThat(!weapons.TryEquip(PlayerWeapons.Kind.MachineGun),"locked gun cannot be equipped");
            CheckThat(weapons.CollectGun(PlayerWeapons.Kind.MachineGun) && weapons.Equipped==PlayerWeapons.Kind.MachineGun && gun.Ammo==30 && gun.Reserve==60,"first acquisition unlocks and loads gun");
            mags[1]=7;reserves[1]=119;
            CheckThat(weapons.CollectGun(PlayerWeapons.Kind.MachineGun) && gun.Ammo==7 && gun.Reserve==120,"duplicate pickup caps reserve without refilling magazine");
            CheckThat(!weapons.CollectGun(PlayerWeapons.Kind.MachineGun),"full reserve rejects duplicate pickup");
            gun.BeginReload();yield return new WaitForSeconds(.2f);
            weapons.TryEquip(PlayerWeapons.Kind.Shotgun);weapons.TryEquip(PlayerWeapons.Kind.MachineGun);
            CheckThat(!gun.IsReloading && gun.Ammo==7 && gun.Reserve==120,"switching during reload neither grants nor consumes ammo");
            CheckThat(!weapons.TryEquip((PlayerWeapons.Kind)99) && !weapons.CollectGun(PlayerWeapons.Kind.Sword),"invalid weapon requests safely rejected");
            weapons.TryEquip(PlayerWeapons.Kind.Shotgun);mags[0]=2;reserves[0]=8;
            GunPickup.Spawn(attack.transform.position,PlayerWeapons.Kind.Shotgun);
            var pickup=Object.FindFirstObjectByType<GunPickup>();
            yield return null;yield return null;
            CheckThat(pickup!=null,"full ammo leaves gun pickup on ground");
            reserves[0]=0;
            yield return null;yield return null;
            CheckThat(pickup==null && reserves[0]==8 && mags[0]==2,"ground pickup replenishes ammo once and is consumed");
            mags[0]=1;reserves[0]=0;gun.SetTrigger(true);int lastShot=gun.ShotsFired;
            float emptyLimit=Time.time+2;while(gun.ShotsFired==lastShot && Time.time<emptyLimit)yield return null;
            gun.SetTrigger(false);yield return new WaitForSeconds(.8f);
            CheckThat(weapons.Equipped==PlayerWeapons.Kind.Sword && mags[0]==0,"last round automatically falls back to sword after trigger release");
            CheckThat(weapons.CollectAxe(),"axe acquisition succeeds");
            weapons.TryEquip(PlayerWeapons.Kind.Shotgun);yield return null;
            CheckThat(weapons.Equipped==PlayerWeapons.Kind.Axe,"empty gun falls back to owned axe");
            CheckThat(GunEffects.PoolCapacity==48,"effect pool bounded to 48 strokes");
            Debug.Log("GUN COMBAT CHECKS COMPLETE\n"+report);
        }
        finally
        {
            attack.transform.position=originalPlayerPosition;
            gun.AimCamera=null;aimCamera.transform.SetPositionAndRotation(oldCameraPosition,oldCameraRotation);if(shake!=null)shake.enabled=shakeEnabled;
            Time.timeScale=1;gun.SetTrigger(false);gun.CancelAction();gun.ReadInput=true;controller.enabled=true;spawner.enabled=true;
            foreach(var target in targets)if(target!=null)Object.Destroy(target.gameObject);if(wall!=null)Object.Destroy(wall);
        }
    }
    static void CaptureCamera(Camera camera,string name)
    {
        var rt=new RenderTexture(1152,648,24);var previous=RenderTexture.active;
        var texture=new Texture2D(1152,648,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1152,648),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(texture);}
    }
    static void Capture(PlayerAutoAttack attack,string name)
    {
        var go=new GameObject("GunReviewCamera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        var bounds=new Bounds(attack.swordModel.position,Vector3.zero);
        foreach(var renderer in attack.GetComponentsInChildren<Renderer>())if(renderer.enabled && renderer.gameObject.activeInHierarchy)bounds.Encapsulate(renderer.bounds);
        camera.transform.position=bounds.center+attack.transform.rotation*new Vector3(2.5f,1.0f,3.7f);camera.transform.LookAt(bounds.center);
        camera.orthographic=true;camera.orthographicSize=1.25f;
        var rt=new RenderTexture(900,700,24);var previous=RenderTexture.active;var texture=new Texture2D(900,700,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,900,700),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/"+name+".png",texture.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(go);}
    }
}
