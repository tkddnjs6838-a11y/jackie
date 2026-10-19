using UnityEngine;
using UnityEngine.UI;

// Shared, non-interactive molded surfaces. All labels and values stay live UI.
public static class ToyHudStyle
{
    static readonly Sprite[] weapons=new Sprite[6],maps=new Sprite[6];
    static Texture2D weaponAtlas,mapAtlas;
    public static Sprite Weapon(int i)=>Slice(ref weaponAtlas,weapons,"UI/ToyWeapons",i);
    public static Sprite Map(int i)=>Slice(ref mapAtlas,maps,"UI/ToyMaps",i);
    static Sprite Slice(ref Texture2D atlas,Sprite[] sprites,string path,int i)
    {
        i=Mathf.Clamp(i,0,5);if(sprites[i]!=null)return sprites[i];
        if(atlas==null)atlas=Resources.Load<Texture2D>(path);if(atlas==null)return null;
        float w=atlas.width/3f,h=atlas.height/2f;
        return sprites[i]=Sprite.Create(atlas,new Rect(i%3*w,(1-i/3)*h,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
    }
    public static Image Piece(Transform parent,string name,Vector2 at,Vector2 size,Color color,bool circle=false)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var im=go.GetComponent<Image>();var r=im.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=at;r.sizeDelta=size;
        im.sprite=circle?GameUiStyle.Circle:GameUiStyle.Rounded;im.type=circle?Image.Type.Simple:Image.Type.Sliced;im.pixelsPerUnitMultiplier=1.2f;im.color=color;im.raycastTarget=false;return im;
    }
    static Image Layer(RectTransform parent,string name,float inset,Color color,float y=0,bool circle=false)
    {
        var im=Piece(parent,name,Vector2.zero,Vector2.zero,color,circle);var r=im.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,inset+y);r.offsetMax=new Vector2(-inset,-inset+y);return im;
    }
    public static void Frame(Image image,bool badge=false,bool gold=false)
    {
        if(image==null || image.transform.Find("ToyFace")!=null)return;
        image.sprite=badge?GameUiStyle.Circle:GameUiStyle.Rounded;image.type=badge?Image.Type.Simple:Image.Type.Sliced;image.pixelsPerUnitMultiplier=1.2f;
        image.color=gold?new Color(.50f,.27f,.015f):new Color(.025f,.15f,.33f);image.raycastTarget=false;
        var shadow=image.GetComponent<Shadow>() ?? image.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,.035f,.08f,.5f);shadow.effectDistance=new Vector2(0,-4);
        var r=image.rectTransform;
        Layer(r,"MoldedRim",1,gold?new Color(1,.68f,.025f):new Color(.02f,.57f,.93f),1,badge);
        Layer(r,"EdgeHighlight",2,gold?new Color(1,.96f,.53f):new Color(.28f,.82f,1),2,badge);
        var face=Layer(r,"ToyFace",gold?3:5,gold?new Color(1,.76f,.04f):badge?new Color(.02f,.62f,.95f):new Color(.035f,.095f,.20f),0,badge);
        var shading=face.gameObject.AddComponent<UiSurfaceGradient>();shading.topWhite=gold?.28f:badge?.28f:.10f;
        var rim=r.Find("EdgeHighlight").gameObject.AddComponent<UiSurfaceGradient>();rim.topWhite=.70f;rim.bottomMultiplier=.65f;
        r.Find("MoldedRim").SetSiblingIndex(0);r.Find("EdgeHighlight").SetSiblingIndex(1);r.Find("ToyFace").SetSiblingIndex(2);
    }
}
