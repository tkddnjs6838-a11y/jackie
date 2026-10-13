using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Repeatable styling of the existing prefab; preserves its original start button identity.
public static class StartScreenStyling
{
    static Font font;
    static Sprite rounded;
    static readonly Color Cream=new Color(1f,.94f,.81f);
    static readonly Color Orange=new Color(1f,.55f,.12f);
    static RectTransform Place(GameObject go,Transform parent,Vector2 at,Vector2 size)
    {
        go.transform.SetParent(parent,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=at;r.sizeDelta=size;r.localScale=Vector3.one;return r;
    }
    static Image Box(Transform parent,string name,Vector2 at,Vector2 size,Color color,bool curved=true)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));Place(go,parent,at,size);
        var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
        if(curved){image.sprite=rounded;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=.2f;}
        return image;
    }
    static Text Label(Transform parent,string name,string words,int size,Vector2 at,Vector2 bounds,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Text));Place(go,parent,at,bounds);
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.text=words;t.color=color;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
    }
    static void StyleButton(Button button,string words,Transform parent,Vector2 at,Vector2 size,Color color,Color ink,int textSize)
    {
        Place(button.gameObject,parent,at,size);
        var image=button.GetComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=.2f;image.color=color;image.raycastTarget=true;button.targetGraphic=image;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.12f,1.12f);colors.pressedColor=new Color(.76f,.76f,.76f);colors.selectedColor=new Color(1.07f,1.07f,1.07f);colors.fadeDuration=.12f;button.colors=colors;
        var label=button.GetComponentInChildren<Text>();if(label==null)label=Label(button.transform,"Label",words,textSize,Vector2.zero,size,ink);
        Place(label.gameObject,button.transform,Vector2.zero,size-new Vector2(20,8));label.font=font;label.fontSize=textSize;label.text=words;label.color=ink;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
    }
    static Button ButtonAt(Transform parent,string name,string words,Vector2 at,Vector2 size,Color color,Color ink,int textSize)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));var b=go.GetComponent<Button>();StyleButton(b,words,parent,at,size,color,ink,textSize);return b;
    }
    public static void Apply()
    {
        const string artPath="Assets/Design/UI/Art/StartBackground-roster-v3.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(artPath);
        if(importer==null)throw new Exception("Start screen background must be imported first.");
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Bold.otf");rounded=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var root=PrefabUtility.LoadPrefabContents(StartScreenWorkshop.PrefabPath);
        try
        {
            var view=root.GetComponent<StartScreenView>();
            var menuCanvas=root.GetComponent<Canvas>();if(menuCanvas==null)menuCanvas=root.AddComponent<Canvas>();
            menuCanvas.overrideSorting=true;menuCanvas.sortingOrder=300;
            if(root.GetComponent<GraphicRaycaster>()==null)root.AddComponent<GraphicRaycaster>();
            foreach(var name in new[]{"TitleLogo","SettingsRim","BackgroundArt","MenuAccent","SettingsButton","SettingsOverlay","StartShadow","MenuFooter"})
            {var old=root.transform.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
            var art=Box(root.transform,"BackgroundArt",Vector2.zero,new Vector2(1280,720),Color.white,false);
            var ar=art.rectTransform;ar.anchorMin=Vector2.zero;ar.anchorMax=Vector2.one;ar.sizeDelta=Vector2.zero;art.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(artPath);art.preserveAspect=false;art.transform.SetAsFirstSibling();
            // Art and typography remain separate, so menu labels stay sharp at every resolution.
            var title=root.transform.Find("TitleTextKo").GetComponent<Text>();Place(title.gameObject,root.transform,new Vector2(-345,130),new Vector2(500,250));
            title.font=font;title.text="왁꾸볼\n뿌수기";title.fontSize=90;title.lineSpacing=.85f;title.verticalOverflow=VerticalWrapMode.Overflow;title.horizontalOverflow=HorizontalWrapMode.Overflow;title.color=Cream;title.alignment=TextAnchor.MiddleLeft;title.raycastTarget=false;
            var shadow=title.GetComponent<Shadow>();if(shadow==null)shadow=title.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.65f);shadow.effectDistance=new Vector2(3,-5);
            const string logoPath="Assets/Design/UI/Art/TitleLogo-cracked-v1.png";
            var logoImporter=(TextureImporter)AssetImporter.GetAtPath(logoPath);logoImporter.textureType=TextureImporterType.Sprite;logoImporter.spriteImportMode=SpriteImportMode.Single;logoImporter.alphaIsTransparency=true;logoImporter.mipmapEnabled=false;logoImporter.SaveAndReimport();
            var logo=Box(root.transform,"TitleLogo",new Vector2(-365,143),new Vector2(470,300),Color.white,false);logo.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);logo.preserveAspect=true;
            title.enabled=false;
            var en=root.transform.Find("TitleText").GetComponent<Text>();Place(en.gameObject,root.transform,new Vector2(-365,0),new Vector2(420,30));en.font=font;en.text="W A K K U B A L L  S M A S H";en.fontSize=15;en.color=new Color(.7f,.75f,.8f);en.alignment=TextAnchor.MiddleCenter;
            
            const string buttonPath="Assets/Design/UI/Art/StartButton-slash-v1.png";
            var buttonImporter=(TextureImporter)AssetImporter.GetAtPath(buttonPath);
            buttonImporter.textureType=TextureImporterType.Sprite;buttonImporter.spriteImportMode=SpriteImportMode.Single;
            buttonImporter.alphaIsTransparency=true;buttonImporter.mipmapEnabled=false;buttonImporter.maxTextureSize=2048;
            buttonImporter.textureCompression=TextureImporterCompression.Uncompressed;buttonImporter.SaveAndReimport();
            StyleButton(view.startButton,"게임 시작",root.transform,new Vector2(-365,-120),new Vector2(490,164),Color.white,Color.black,31);
            var gradient=view.startButton.GetComponent<MenuButtonGradient>();
            if(gradient!=null)UnityEngine.Object.DestroyImmediate(gradient);
            var startImage=view.startButton.GetComponent<Image>();
            startImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(buttonPath);startImage.type=Image.Type.Simple;startImage.preserveAspect=true;
            // The illustrated lettering is part of the selected button artwork.
            view.startButton.GetComponentInChildren<Text>(true).enabled=false;
            view.startButton.transform.SetAsLastSibling();
            view.settingsButton=ButtonAt(root.transform,"SettingsButton","설정",new Vector2(-365,-222),new Vector2(150,46),Color.clear,Cream,23);
            view.settingsButton.targetGraphic=view.settingsButton.GetComponentInChildren<Text>();
            var guide=root.transform.Find("ControlGuide");guide.gameObject.SetActive(false);
            Place(view.testModeNotice,root.transform,new Vector2(-340,-300),new Vector2(510,35));view.testModeNotice.GetComponent<Text>().fontSize=16;view.testModeNotice.SetActive(false);
            Label(root.transform,"MenuFooter","",14,new Vector2(0,-320),new Vector2(200,25),Cream);
            var overlay=Box(root.transform,"SettingsOverlay",Vector2.zero,new Vector2(1280,720),new Color(.015f,.025f,.045f,.94f),false);overlay.raycastTarget=true;
            var rect=overlay.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.sizeDelta=Vector2.zero;view.settingsPanel=overlay.gameObject;
            Label(overlay.transform,"Heading","설정",36,new Vector2(0,255),new Vector2(650,60),Cream);
            Label(overlay.transform,"VolumeTitle","전체 음량",22,new Vector2(-235,154),new Vector2(170,40),Cream);
            view.volumeLabel=Label(overlay.transform,"VolumeValue","100%",22,new Vector2(265,154),new Vector2(100,40),Cream);
            var sliderObject=new GameObject("MasterVolume",typeof(RectTransform),typeof(Slider));Place(sliderObject,overlay.transform,new Vector2(0,95),new Vector2(600,40));
            var track=Box(sliderObject.transform,"Track",Vector2.zero,new Vector2(600,10),new Color(.21f,.26f,.32f));track.raycastTarget=true;
            var fillArea=new GameObject("FillArea",typeof(RectTransform));Place(fillArea,sliderObject.transform,Vector2.zero,new Vector2(600,10));
            var fill=Box(fillArea.transform,"Fill",Vector2.zero,new Vector2(600,10),Orange);
            var handle=Box(sliderObject.transform,"Handle",Vector2.zero,new Vector2(24,32),Cream);handle.raycastTarget=true;
            view.volumeSlider=sliderObject.GetComponent<Slider>();view.volumeSlider.fillRect=fill.rectTransform;view.volumeSlider.handleRect=handle.rectTransform;view.volumeSlider.targetGraphic=handle;view.volumeSlider.minValue=0;view.volumeSlider.maxValue=1;view.volumeSlider.value=1;
            var controls=Label(overlay.transform,"Controls","이동  WASD     ·     회피  Space\n공격 / 총 발사  좌클릭     ·     강공격  우클릭\n재장전  R     ·     보유 증강  Tab\n마무리 콤보  좌 → 좌 → 우",22,new Vector2(0,-80),new Vector2(880,200),new Color(.76f,.81f,.88f));controls.lineSpacing=1.6f;
            view.closeSettingsButton=ButtonAt(overlay.transform,"Close","돌아가기  ·  Esc",new Vector2(0,-255),new Vector2(360,60),Orange,new Color(.10f,.075f,.045f),24);
            overlay.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,StartScreenWorkshop.PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
}
