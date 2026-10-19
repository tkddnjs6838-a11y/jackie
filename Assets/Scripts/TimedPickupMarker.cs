using UnityEngine;

// Persistent, camera-facing silhouette and localized explanation above timed pickups.
public sealed class TimedPickupMarker:MonoBehaviour
{
    public PowerUpEffects.Kind kind;
    Transform sign;Material lineMaterial;TextMesh label;Camera view;
    void Start()
    {
        sign=new GameObject("EffectMarker").transform;sign.SetParent(transform,false);sign.localPosition=Vector3.up*.65f;
        var line=sign.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.045f;
        lineMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));lineMaterial.color=PowerUpEffects.ColorOf(kind);line.sharedMaterial=lineMaterial;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        Vector2[] p;
        switch(kind)
        {
            case PowerUpEffects.Kind.Rage:p=new[]{new Vector2(0,.32f),new Vector2(.12f,.08f),new Vector2(.06f,-.08f),new Vector2(.2f,-.08f),new Vector2(.2f,-.15f),new Vector2(.05f,-.15f),new Vector2(.05f,-.3f),new Vector2(-.05f,-.3f),new Vector2(-.05f,-.15f),new Vector2(-.2f,-.15f),new Vector2(-.2f,-.08f),new Vector2(-.06f,-.08f),new Vector2(-.12f,.08f)};break;
            case PowerUpEffects.Kind.Frenzy:p=new[]{new Vector2(.08f,.32f),new Vector2(-.2f,-.03f),new Vector2(-.03f,-.03f),new Vector2(-.08f,-.32f),new Vector2(.2f,.06f),new Vector2(.03f,.06f)};break;
            case PowerUpEffects.Kind.Shield:p=new[]{new Vector2(0,.3f),new Vector2(.25f,.2f),new Vector2(.2f,-.12f),new Vector2(0,-.3f),new Vector2(-.2f,-.12f),new Vector2(-.25f,.2f)};break;
            default:p=new[]{new Vector2(-.22f,.28f),new Vector2(.22f,.28f),new Vector2(.14f,.1f),new Vector2(-.14f,-.1f),new Vector2(-.22f,-.28f),new Vector2(.22f,-.28f),new Vector2(.14f,-.1f),new Vector2(-.14f,.1f)};break;
        }
        line.positionCount=p.Length;for(int i=0;i<p.Length;i++)line.SetPosition(i,p[i]);
        var g=new GameObject("EffectName");g.transform.SetParent(sign,false);g.transform.localPosition=Vector3.up*.61f;
        label=g.AddComponent<TextMesh>();label.font=Resources.Load<Font>("UI/AugmentKorean");label.fontSize=48;label.characterSize=.018f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;
        label.color=PowerUpEffects.ColorOf(kind);var fx=PowerUpEffects.Instance;
        label.text=PowerUpEffects.DisplayName(kind)+(fx!=null?"\n"+fx.Description(kind)+" · "+fx.DurationOf(kind).ToString("0.#")+"초":"");
        if(label.font!=null)g.GetComponent<Renderer>().sharedMaterial=label.font.material;
    }
    void LateUpdate()
    {
        if(view==null)view=Camera.main;if(view==null||sign==null)return;
        sign.rotation=view.transform.rotation;
        bool visible=(view.transform.position-transform.position).sqrMagnitude<400 && Time.timeScale>0;
        if(sign.gameObject.activeSelf!=visible)sign.gameObject.SetActive(visible);
    }
    void OnDestroy(){if(lineMaterial!=null)Destroy(lineMaterial);}
}
