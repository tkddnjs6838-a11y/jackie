using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class AugmentChoiceChecks
{
    static string report;
    static void Check(bool ok,string label)
    {
        if(!ok)throw new Exception("AUGMENT CHOICE FAIL: "+label);
        report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/choice-checks.txt",report);
    }
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";
        var flow=Object.FindFirstObjectByType<GameFlowManager>();var growth=Object.FindFirstObjectByType<CharacterGrowth>();var choices=Object.FindFirstObjectByType<UpgradeSystem>();
        Check(!choices.TryOffer(growth),"title blocks choices");flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var move=growth.GetComponent<PlayerController>();move.enabled=false;
        growth.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        var pickups=new List<GameObject>();
        var originalPosition=growth.transform.position;
        try
        {
            var box=CharacterUpgradePickup.Spawn(growth.transform.position,CharacterGrowth.Kind.Power);pickups.Add(box.gameObject);
            Time.timeScale=0;yield return null;yield return null;
            Check(box!=null && !choices.Choosing,"ordinary pause blocks crate collection");Time.timeScale=1;
            yield return new WaitForSeconds(.85f);
            Check(box!=null && !choices.Choosing,"crate spawned under player never auto-opens while standing still");
            growth.transform.position=originalPosition+Vector3.right*2;yield return null;yield return null;
            growth.transform.position=originalPosition;yield return null;yield return null;
            Check(box==null && choices.Choosing && Time.timeScale==0,"crate consumed and selection pauses combat");
            Check(choices.Offered.Count==3 && new HashSet<CharacterGrowth.Kind>(choices.Offered).Count==3,"three distinct available choices");
            int total=0;foreach(CharacterGrowth.Kind kind in Enum.GetValues(typeof(CharacterGrowth.Kind)))total+=growth.Level(kind);
            Check(total==0,"crate grants nothing before choosing");
            Check(Cursor.visible && Cursor.lockState==CursorLockMode.None,"selection unlocks cursor");
            Check(!choices.TryOffer(growth),"second offer cannot replace open choices");
            var second=CharacterUpgradePickup.Spawn(growth.transform.position,CharacterGrowth.Kind.Power);pickups.Add(second.gameObject);
            yield return null;Check(second!=null,"nearby second crate waits during selection");
            Check(!choices.Choose(-1) && !choices.Choose(3) && choices.Choosing,"invalid index leaves selection intact");
            foreach(var kind in choices.Offered)Check(growth.ChoiceDescription(kind).Contains("→"),kind+" shows before and after values");
            var font=Resources.Load<Font>("UI/AugmentKorean");Check(font!=null && font.HasCharacter('증') && font.HasCharacter('혈'),"bundled Korean font supports choice labels");
            // Render the actual overlay with its own camera for a deterministic visual check.
            var canvas=choices.ChoicePanel.GetComponent<Canvas>();var cameraObject=new GameObject("ChoiceCapture",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.06f,.09f);camera.cullingMask=1<<5;
            foreach(var child in choices.ChoicePanel.GetComponentsInChildren<Transform>())child.gameObject.layer=5;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("output/gun-effects/augment-choice.png",image.EncodeToPNG());}
            finally{canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(image);Object.Destroy(cameraObject);}
            var selected=choices.Offered[0];int before=growth.Level(selected);
            choices.ChoicePanel.GetComponentsInChildren<Button>()[0].onClick.Invoke();
            Check(growth.Level(selected)==before+1 && !choices.Choosing && Time.timeScale==1,"button grants selected augment once and resumes");
            Check(!choices.Choose(0) && growth.Level(selected)==before+1,"repeated click grants nothing");
            yield return new WaitForSeconds(.85f);
            Check(second!=null && !choices.Choosing && Time.timeScale==1,"second nearby crate cannot reopen choices after resume");
            growth.transform.position=originalPosition+Vector3.right*2;yield return null;yield return null;
            growth.transform.position=originalPosition;yield return null;yield return null;
            Check(second==null && choices.Choosing,"second crate requires a new approach");choices.Choose(0);
            box=CharacterUpgradePickup.Spawn(originalPosition+Vector3.right*3,CharacterGrowth.Kind.Power);pickups.Add(box.gameObject);
            yield return null;
            growth.transform.position=originalPosition+Vector3.right*3;yield return null;yield return null;
            Check(box!=null && !choices.Choosing,"newly spawned crate has a collection delay");
            yield return new WaitForSecondsRealtime(.9f);yield return null;
            Check(box==null && choices.Choosing,"approached crate can be collected after delay");choices.Choose(0);
            growth.transform.position=originalPosition;
            foreach(CharacterGrowth.Kind kind in Enum.GetValues(typeof(CharacterGrowth.Kind)))
                if(kind!=CharacterGrowth.Kind.BloodEdge)while(growth.CanCollect(kind))growth.TryCollect(kind);
            Check(choices.TryOffer(growth) && choices.Offered.Count==1 && choices.Offered[0]==CharacterGrowth.Kind.BloodEdge,"capped augments excluded; one remaining choice works");choices.Choose(0);
            while(growth.CanCollect(CharacterGrowth.Kind.BloodEdge))growth.TryCollect(CharacterGrowth.Kind.BloodEdge);
            Check(!choices.TryOffer(growth) && Time.timeScale==1,"all capped does not pause");
            box=CharacterUpgradePickup.Spawn(growth.transform.position,CharacterGrowth.Kind.Power);pickups.Add(box.gameObject);
            growth.transform.position=originalPosition+Vector3.right*2;yield return new WaitForSeconds(.85f);
            growth.transform.position=originalPosition;yield return null;yield return null;
            Check(box!=null && !choices.Choosing,"all capped leaves crate unconsumed");Object.Destroy(box.gameObject);
            // A separate run-local inventory checks game-end cancellation without clearing actual levels.
            var fixture=new GameObject("ChoiceEndFixture");pickups.Add(fixture);fixture.AddComponent<PlayerHealth>();var fresh=fixture.AddComponent<CharacterGrowth>();
            Check(choices.TryOffer(fresh),"uncapped inventory can open selection");
            typeof(GameFlowManager).GetMethod("HandlePlayerDied",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flow,null);
            Check(!choices.Choosing && !choices.ChoicePanel.activeSelf && Time.timeScale==0,"game over closes choices and keeps pause");
            Check(!choices.Choose(0) && !choices.TryOffer(fresh),"game over rejects stale click and new offer");
            Debug.Log("AUGMENT CHOICE CHECKS COMPLETE\n"+report);
        }
        finally{growth.transform.position=originalPosition;choices.CancelChoices();foreach(var obj in pickups)if(obj!=null)Object.Destroy(obj);spawner.enabled=true;move.enabled=true;}
    }
}
