using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections;
using System.IO;
using Object=UnityEngine.Object;
public static class ToyCharacterChecks
{
    const string Out="output/character-b-integration/";
    static void Check(bool ok,string label){File.AppendAllText(Out+"checks.txt",(ok?"PASS: ":"FAIL: ")+label+"\n");if(!ok)throw new System.Exception(label);}
    public static void Run(){Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());}
    static IEnumerator Test()
    {
        File.WriteAllText(Out+"checks.txt","RUNNING\n");var map=Object.FindFirstObjectByType<MapSandbox>();while(map.Busy)yield return null;
        var flow=Object.FindFirstObjectByType<GameFlowManager>();flow.startButton.onClick.Invoke();
        var player=Object.FindFirstObjectByType<PlayerController>();var attack=player.GetComponent<PlayerAutoAttack>();player.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        Object.FindFirstObjectByType<EnemySpawner>().enabled=false;foreach(var e in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();GameObject target=null;
        try
        {
            Check(player.animator==attack.animator && attack.animator.name=="PlayerModel_B","movement and combat share new animator");
            Check(!player.transform.Find("PlayerModel_HandRig").gameObject.activeSelf,"old model disabled");
            var hand=ToyCharacterRig.Bone(attack.animator,HumanBodyBones.RightHand);
            Check(hand!=null && attack.swordModel.parent==hand,"sword attached to new right hand");
            var foot=ToyCharacterRig.Bone(attack.animator,HumanBodyBones.LeftLowerLeg);var initialFoot=foot.localRotation;var initial=player.transform.position;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
            float end=Time.time+.8f,maxBend=0,maxSpeed=0;
            while(Time.time<end){yield return null;maxBend=Mathf.Max(maxBend,Quaternion.Angle(initialFoot,foot.localRotation));maxSpeed=Mathf.Max(maxSpeed,attack.animator.GetFloat("Speed"));}
            Check(maxSpeed>1,"keyboard movement drives walk blend");
            Check(maxBend>3,"walk animation bends knee chain over a full cycle: "+maxBend);
            ScreenCapture.CaptureScreenshot(Out+"game-walk.png");
            yield return new WaitForSeconds(.35f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            Check(Vector3.Distance(initial,player.transform.position)>.5f,"new player moves through real controller");
            end=Time.time+1.5f;while(attack.animator.GetFloat("Speed")>=.3f && Time.time<end)yield return null;Check(attack.animator.GetFloat("Speed")<.3f,"release returns to ready pose");
            target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy.prefab"),player.transform.position+player.transform.forward*1.7f,Quaternion.identity);
            var enemy=target.GetComponent<Enemy>();enemy.maxHealth=1000;enemy.RefreshConfiguredStatsAndColor();enemy.enabled=false;float health=enemy.CurrentHealth;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.12f);
            Check(attack.AttackInProgress,"mouse input starts real melee swing");ScreenCapture.CaptureScreenshot(Out+"game-attack.png");
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return new WaitForSeconds(.8f);
            Check(enemy.CurrentHealth<health,"animated swing applies existing melee damage");
            Check(!attack.AttackInProgress,"attack recovers to locomotion");
            ScreenCapture.CaptureScreenshot(Out+"game-ready.png");File.AppendAllText(Out+"checks.txt","COMPLETE\n");
        }
        finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);if(target)Object.Destroy(target);}
    }
    public static void Preview()
    {
        Directory.CreateDirectory(Out+"frames");var preview=new PreviewRenderUtility();
        try
        {
            var go=Object.Instantiate(Object.FindFirstObjectByType<PlayerController>().gameObject);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;preview.AddSingleGO(go);
            foreach(var c in go.GetComponentsInChildren<Camera>(true))c.enabled=false;
            var attack=go.GetComponent<PlayerAutoAttack>();attack.animator.enabled=false;
            var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>();
            var baked=new Mesh[skins.Length];
            for(int k=0;k<skins.Length;k++)
            {
                baked[k]=new Mesh();var snapshot=new GameObject("PoseSnapshot");snapshot.transform.SetParent(skins[k].transform,false);snapshot.layer=skins[k].gameObject.layer;
                var scale=skins[k].transform.lossyScale;snapshot.transform.localScale=new Vector3(1f/scale.x,1f/scale.y,1f/scale.z);
                snapshot.AddComponent<MeshFilter>().sharedMesh=baked[k];snapshot.AddComponent<MeshRenderer>().sharedMaterials=skins[k].sharedMaterials;
                skins[k].enabled=false;
            }
            preview.camera.orthographic=true;preview.camera.orthographicSize=1.18f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=20;
            preview.camera.transform.position=new Vector3(-2,.65f,5);preview.camera.transform.LookAt(new Vector3(0,0,0));preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.22f,.25f,.28f);
            preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(40,215,0);preview.lights[1].intensity=1;preview.lights[1].transform.rotation=Quaternion.Euler(15,20,0);preview.ambientColor=new Color(.45f,.45f,.45f);
            foreach(string mode in new[]{"walk","attack"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Models/WakuballB/"+(mode=="walk"?"B_Walk":"B_Ready")+".anim");
                for(int i=0;i<24;i++)
                {
                    attack.ResetSwordPose();clip.SampleAnimation(attack.animator.gameObject,mode=="walk"?clip.length*i/24:0);attack.ApplySwordPose(i/23f,mode=="attack");
                    for(int k=0;k<skins.Length;k++)skins[k].BakeMesh(baked[k]);
                    preview.BeginStaticPreview(new Rect(0,0,520,600));preview.Render(true);var tex=preview.EndStaticPreview();File.WriteAllBytes(Out+"frames/"+mode+"-"+i.ToString("00")+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
                }
            }
            foreach(var mesh in baked)Object.DestroyImmediate(mesh);
        }
        finally{preview.Cleanup();}
    }
}
