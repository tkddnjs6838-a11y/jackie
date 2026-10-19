using UnityEngine;
using UnityEngine.UI;

// Uses separate artwork layers: only monsters move; the hero and sword stay anchored.
[DisallowMultipleComponent]
public sealed class TitleArtworkMotion : MonoBehaviour
{
    RawImage monsters, hero;
    Image original;
    TitleSwordGlint glint, gunGlint;
    float age;
    Vector2 monsterBase;
    bool ready;
    public bool Initialize()
    {
        if(ready)return true;
        var plate=Resources.Load<Texture2D>("UI/TitleMotion/Background");
        var foes=Resources.Load<Texture2D>("UI/TitleMotion/Monsters");
        var player=Resources.Load<Texture2D>("UI/TitleMotion/Hero");
        if(plate==null||foes==null||player==null)return false;
        original=GetComponent<Image>();
        Layer("BackgroundPlate",plate);
        monsters=Layer("MonsterLayer",foes);hero=Layer("HeroLayer",player);
        var bounds=((RectTransform)transform).rect;
        // Align the extracted full-canvas art with the approved title composition.
        hero.rectTransform.localScale=Vector3.one*.8f;
        hero.rectTransform.anchoredPosition=new Vector2(bounds.width*.0988f,-bounds.height*.021f);
        monsters.rectTransform.localScale=Vector3.one*.9f;
        monsterBase=new Vector2(0,bounds.height*.05f);
        var shine=new GameObject("SwordLight",typeof(RectTransform),typeof(CanvasRenderer),typeof(TitleSwordGlint));
        shine.layer=hero.gameObject.layer;
        shine.transform.SetParent(hero.transform,false);Stretch((RectTransform)shine.transform);
        glint=shine.GetComponent<TitleSwordGlint>();glint.raycastTarget=false;
        // Draw the light above the menu artwork without intercepting any input.
        var lightCanvas=shine.AddComponent<Canvas>();
        lightCanvas.overrideSorting=true;
        var menuCanvas=GetComponentInParent<Canvas>();
        lightCanvas.sortingLayerID=menuCanvas!=null?menuCanvas.sortingLayerID:0;
        lightCanvas.sortingOrder=(menuCanvas!=null?menuCanvas.sortingOrder:300)+1;
        gunGlint=GunLight(shine.transform,"UpperBarrelLight",1);
        if(original!=null)original.enabled=false;
        ready=true;age=0;Apply();return true;
    }
    TitleSwordGlint GunLight(Transform parent,string name,int barrel)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TitleSwordGlint));
        go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);Stretch((RectTransform)go.transform);
        var effect=go.GetComponent<TitleSwordGlint>();effect.raycastTarget=false;effect.SetGunBarrel(barrel);return effect;
    }
    RawImage Layer(string name,Texture texture)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
        go.layer=gameObject.layer;
        go.transform.SetParent(transform,false);Stretch((RectTransform)go.transform);
        var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;return image;
    }
    static void Stretch(RectTransform r)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    void OnEnable(){age=0;if(ready)Apply();}
    void Update(){if(!ready)return;age+=Mathf.Min(Time.unscaledDeltaTime,.05f);Apply();}
    void Apply()
    {
        float appear=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.12f)/.65f));
        monsters.color=new Color(1,1,1,appear);
        // Two distinct beats (strong / light), each with a short rest. No drifting or rotation.
        float beatClock=Mathf.Max(0,age-.77f);
        float beat=Mathf.Repeat(beatClock,.5f);
        float lift=beat<.10f?Mathf.SmoothStep(0,1,beat/.10f)
            :1-Mathf.SmoothStep(0,1,Mathf.Clamp01((beat-.10f)/.23f));
        float strength=((int)(beatClock/.5f)%2==0)?4f:2.5f;
        float uiScale=((RectTransform)transform).rect.height/720f;
        monsters.rectTransform.anchoredPosition=monsterBase+Vector2.up*(lift*strength*uiScale*appear-16*(1-appear));
        monsters.rectTransform.localRotation=Quaternion.identity;
        // Fade only: no idle motion, scaling or drift on the hero.
        hero.color=new Color(1,1,1,Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.88f)/.62f)));
        glint.SetTime(age-1.65f);
        gunGlint.SetTime(age-2.25f);
    }
}
