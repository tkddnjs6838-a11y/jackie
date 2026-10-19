using UnityEngine;

// Presentation only. Button events and click/hover feedback stay on their existing controls.
[DisallowMultipleComponent]
public sealed class StartScreenEntrance : MonoBehaviour
{
    sealed class Part
    {
        public RectTransform rect;
        public Vector2 position;
        public Vector3 scale;
        public CanvasGroup group;
        public float alpha;
        public bool interactable, blocksRaycasts;
        public Part(RectTransform target)
        {
            rect=target;
            position=rect.anchoredPosition;scale=rect.localScale;
            group=target.GetComponent<CanvasGroup>();
            if(group==null)group=target.gameObject.AddComponent<CanvasGroup>();
            alpha=group.alpha;interactable=group.interactable;blocksRaycasts=group.blocksRaycasts;
        }
        public void Restore()
        {
            if(rect==null)return;
            rect.anchoredPosition=position;rect.localScale=scale;
            group.alpha=alpha;group.interactable=interactable;group.blocksRaycasts=blocksRaycasts;
        }
        public void Reveal(float progress, float offset)
        {
            if(rect==null)return;
            float t=Mathf.Clamp01(progress), ease=1-Mathf.Pow(1-t,3);
            rect.anchoredPosition=position+Vector2.down*(offset*(1-ease));
            group.alpha=alpha*Mathf.SmoothStep(0,1,t);
            // Invisible controls cannot receive pointer or keyboard navigation.
            group.interactable=interactable&&t>=1;
            group.blocksRaycasts=blocksRaycasts&&t>=1;
        }
    }
    Part logo,start,settings;
    bool ready,finished;
    float age;

    public void Initialize(StartScreenView view)
    {
        if(ready)return;
        logo=FindPart("TitleLogo");
        var art=transform.Find("BackgroundArt");
        if(art!=null)
        {
            var motion=art.GetComponent<TitleArtworkMotion>();
            if(motion==null)motion=art.gameObject.AddComponent<TitleArtworkMotion>();
            motion.Initialize();
        }
        if(view.startButton!=null)start=new Part(view.startButton.transform as RectTransform);
        if(view.settingsButton!=null)settings=new Part(view.settingsButton.transform as RectTransform);
        ready=true;Begin();
    }
    Part FindPart(string name)
    {
        var rect=transform.Find(name) as RectTransform;
        return rect!=null&&rect.gameObject.activeSelf?new Part(rect):null;
    }
    void OnEnable(){if(ready)Begin();}
    void Begin(){age=0;finished=false;Apply();}
    void Update()
    {
        if(!ready||finished)return;
        // Scene loading must not consume the entire entrance in one stalled frame.
        age+=Mathf.Min(Time.unscaledDeltaTime,.05f);
        Apply();
        if(age>=2.2f){Restore();finished=true;}
    }
    void Apply()
    {
        if(logo!=null)
        {
            float t=Mathf.Clamp01((age-.08f)/.62f);
            logo.Reveal(t,24);
            float u=t-1;
            float pop=1+2.1f*u*u*u+1.1f*u*u;
            logo.rect.localScale=logo.scale*Mathf.LerpUnclamped(.88f,1,pop);
        }
        start?.Reveal((age-1.35f)/.42f,18);
        settings?.Reveal((age-1.49f)/.38f,12);

    }
    void Restore(){logo?.Restore();start?.Restore();settings?.Restore();}
    void OnDisable(){if(ready)Restore();}
    void OnDestroy(){if(ready)Restore();}
}
