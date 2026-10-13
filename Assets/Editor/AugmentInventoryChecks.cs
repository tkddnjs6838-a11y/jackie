using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static class AugmentInventoryChecks
{
    static string report;
    static void Check(bool ok,string label)
    {
        if(!ok)throw new Exception("AUGMENT INVENTORY FAIL: "+label);
        report+="PASS: "+label+"\n";File.WriteAllText("output/gun-effects/inventory-checks.txt",report);
    }
    public static void Run()=>Object.FindFirstObjectByType<GameFlowManager>().StartCoroutine(Test());
    static IEnumerator Test()
    {
        report="";var flow=Object.FindFirstObjectByType<GameFlowManager>();var ui=Object.FindFirstObjectByType<UpgradeSystem>();
        var growth=Object.FindFirstObjectByType<CharacterGrowth>();
        Check(!ui.ToggleInventory(),"title screen blocks inventory");flow.startButton.onClick.Invoke();
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();spawner.enabled=false;
        var controller=growth.GetComponent<PlayerController>();controller.enabled=false;
        growth.GetComponent<PlayerHealth>().GrantInvincibility(1000);
        Keyboard keyboard=null;
        try
        {
            keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));InputSystem.Update();
            typeof(UpgradeSystem).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Check(ui.InventoryOpen && Time.timeScale==0,"Tab input opens inventory and pauses");
            Check(ui.InventoryPanel.transform.Find("Empty").gameObject.activeSelf,"empty run explains how to obtain augments");
            Check(Cursor.visible && Cursor.lockState==CursorLockMode.None,"inventory unlocks cursor");
            Check(!ui.TryOffer(growth),"crate choice cannot interrupt inventory");
            float elapsed=RunStats.Instance.SurvivalTime;yield return new WaitForSecondsRealtime(.15f);
            Check(Mathf.Approximately(elapsed,RunStats.Instance.SurvivalTime),"run timer stays frozen while inspecting");
            Check(ui.ToggleInventory() && !ui.InventoryOpen && Time.timeScale==1,"second toggle resumes combat");
            Check(!Cursor.visible && Cursor.lockState==CursorLockMode.Locked,"closing restores gameplay cursor");
            foreach(CharacterGrowth.Kind kind in Enum.GetValues(typeof(CharacterGrowth.Kind)))Check(growth.TryCollect(kind),kind+" fixture upgrade");
            growth.TryCollect(CharacterGrowth.Kind.Power);
            while(growth.CanCollect(CharacterGrowth.Kind.SecondWind))growth.TryCollect(CharacterGrowth.Kind.SecondWind);
            var effects=growth.GetComponent<PowerUpEffects>();effects.Activate(PowerUpEffects.Kind.Rage);
            Check(ui.ToggleInventory(),"owned augments open after acquisition");
            Check(!ui.InventoryPanel.transform.Find("Empty").gameObject.activeSelf,"empty message hidden when augments owned");
            for(int i=0;i<8;i++)Check(ui.InventoryPanel.transform.Find("Owned"+i).gameObject.activeSelf,"owned row "+i+" visible");
            Check(growth.OwnedDescription(CharacterGrowth.Kind.Power).Contains("2단계") && growth.OwnedDescription(CharacterGrowth.Kind.Power).Contains("+20%"),"stacked permanent damage shown without temporary rage");
            Check(growth.OwnedDescription(CharacterGrowth.Kind.SecondWind).Contains("최대 강화") && growth.OwnedDescription(CharacterGrowth.Kind.SecondWind).Contains("0.5초"),"stat cap shown even when reached before level five");
            Capture(ui.InventoryPanel);
            ui.InventoryPanel.GetComponentInChildren<Button>().onClick.Invoke();
            Check(!ui.InventoryOpen && Time.timeScale==1,"close button resumes combat");
            Check(ui.TryOffer(growth) && !ui.ToggleInventory() && ui.Choosing,"Tab cannot dismiss pending choice");ui.Choose(0);
            ui.ToggleInventory();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();
            typeof(UpgradeSystem).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            Check(!ui.InventoryOpen && Time.timeScale==1,"Escape closes inventory");
            ui.ToggleInventory();typeof(GameFlowManager).GetMethod("HandlePlayerDied",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,null);
            Check(!ui.InventoryOpen && !ui.InventoryPanel.activeSelf && Time.timeScale==0,"game over closes inventory without resuming");
            ui.CloseInventory();Check(!ui.ToggleInventory() && Time.timeScale==0,"stale close cannot resume finished run");
            Debug.Log("AUGMENT INVENTORY CHECKS COMPLETE\n"+report);
        }
        finally{if(keyboard!=null)InputSystem.RemoveDevice(keyboard);ui.CancelChoices();spawner.enabled=true;controller.enabled=true;}
    }
    static void Capture(GameObject panel)
    {
        var canvas=panel.GetComponent<Canvas>();var obj=new GameObject("InventoryCapture",typeof(Camera));var camera=obj.GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.06f,.09f);camera.cullingMask=1<<5;
        foreach(var child in panel.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var rt=new RenderTexture(1280,720,24);var old=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("output/gun-effects/augment-inventory.png",texture.EncodeToPNG());}
        finally{canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;RenderTexture.active=old;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(obj);}
    }
}
