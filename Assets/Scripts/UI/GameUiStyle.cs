using UnityEngine;
using UnityEngine.UI;

// Shared cut-corner treatment for gameplay cards; text remains level and readable.
public static class GameUiStyle
{
    public static readonly Color Ink=new Color(.085f,.09f,.10f,.88f);
    public static readonly Color Gold=new Color(.72f,.74f,.73f);
    static Sprite card;
    public static Sprite Card
    {
        get
        {
            if(card!=null)return card;
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="CutCornerUi";texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x-Mathf.Clamp(x,6,size-7),dy=y-Mathf.Clamp(y,6,size-7);
                float edge=6-Mathf.Sqrt(dx*dx+dy*dy);
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(edge));
            }
            texture.SetPixels32(pixels);texture.Apply();
            card=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));return card;
        }
    }
    public static void Apply(Image image,Color color)
    {
        if(image==null)return;image.sprite=Card;image.type=Image.Type.Sliced;image.color=color;
    }
    public static void Accent(Transform parent)
    {
        // Reserved for layouts that need emphasis; gameplay cards use spacing instead.
    }
    public static void StyleResult(GameObject panel)
    {
        if(panel==null)return;
        foreach(var button in panel.GetComponentsInChildren<Button>(true))
        {
            Apply(button.GetComponent<Image>(),Gold);
            foreach(var text in button.GetComponentsInChildren<Text>(true)){text.color=Ink;text.fontStyle=FontStyle.Bold;}
        }
    }
}
