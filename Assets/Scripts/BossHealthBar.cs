using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-of-screen health bar for the boss. Shows itself when a boss spawns and hides
/// again once it dies. The pale trailing bar drains a moment behind the red one so a
/// big hit reads as a chunk taken off.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    public GameObject root;
    public RectTransform fillRect;
    public RectTransform trailRect;
    public Image fillImage;
    public Text nameText;
    public Text healthText;

    public string bossName = "BOSS";
    public Color fullColor = new Color(0.85f, 0.22f, 0.20f, 1f);
    public Color lowColor = new Color(1f, 0.62f, 0.15f, 1f);
    public float fillLerpSpeed = 9f;
    public float trailLerpSpeed = 1.6f;
    public float trailDelay = 0.35f;

    private Enemy boss;
    private float displayed = 1f;
    private float trailed = 1f;
    private float trailHold;

    void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    RectTransform panel;
    RectTransform canvasRect;
    Canvas canvas;
    Vector2 lastCanvasSize;
    Rect lastSafeArea;

    void Start()
    {
        if(root==null)return;
        panel=root.GetComponent<RectTransform>();
        canvas=root.GetComponentInParent<Canvas>();
        if(panel==null || canvas==null)return;
        canvasRect=canvas.GetComponent<RectTransform>();
        ToyHudStyle.Frame(root.GetComponent<Image>());
        GameUiStyle.Accent(root.transform);
        var inner=root.transform.Find("Inner") as RectTransform;
        if(inner!=null)
        {
            inner.anchorMin=Vector2.zero;inner.anchorMax=Vector2.one;
            inner.offsetMin=new Vector2(12,9);inner.offsetMax=new Vector2(-12,-38);
            GameUiStyle.Apply(inner.GetComponent<Image>(),new Color(.07f,.12f,.23f));
        }
        GameUiStyle.Apply(fillImage,fullColor);
        if(fillImage!=null && fillImage.GetComponent<UiSurfaceGradient>()==null)fillImage.gameObject.AddComponent<UiSurfaceGradient>();
        if(trailRect!=null)GameUiStyle.Apply(trailRect.GetComponent<Image>(),new Color(1,.72f,.52f,.75f));
        StyleLabel(nameText,new Vector2(0,.45f),new Vector2(.65f,1),TextAnchor.MiddleLeft,19);
        StyleLabel(healthText,new Vector2(.65f,.45f),Vector2.one,TextAnchor.MiddleRight,16);
        RefreshLayout();
    }

    static void StyleLabel(Text label,Vector2 min,Vector2 max,TextAnchor alignment,int size)
    {
        if(label==null)return;
        var rect=label.rectTransform;rect.anchorMin=min;rect.anchorMax=max;
        rect.offsetMin=new Vector2(12,0);rect.offsetMax=new Vector2(-12,0);
        label.fontSize=size;label.alignment=alignment;label.fontStyle=FontStyle.Bold;
        label.color=Color.white;label.resizeTextForBestFit=true;
        label.resizeTextMinSize=12;label.resizeTextMaxSize=size;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;
    }

    void RefreshLayout()
    {
        if(panel==null || canvasRect==null)return;
        lastCanvasSize=canvasRect.rect.size;lastSafeArea=Screen.safeArea;
        float scale=Mathf.Max(.01f,canvas.scaleFactor);
        float safeWidth=lastSafeArea.width/scale;
        float topInset=(Screen.height-lastSafeArea.yMax)/scale;
        float centerOffset=(lastSafeArea.center.x-Screen.width*.5f)/scale;
        panel.anchorMin=panel.anchorMax=new Vector2(.5f,1);
        panel.pivot=new Vector2(.5f,.5f);panel.localScale=Vector3.one;
        panel.sizeDelta=new Vector2(Mathf.Min(640,Mathf.Min(lastCanvasSize.x,safeWidth)*.60f),64);
        panel.anchoredPosition=new Vector2(centerOffset,-140-topInset);
    }

    void OnEnable()
    {
        Enemy.OnBossSpawned += HandleBossSpawned;
        Enemy.OnEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Enemy.OnBossSpawned -= HandleBossSpawned;
        Enemy.OnEnemyDied -= HandleEnemyDied;
    }

    void HandleBossSpawned(Enemy spawned)
    {
        boss = spawned;
        displayed = 1f;
        trailed = 1f;
        trailHold = 0f;

        if (nameText != null) nameText.text = bossName;
        if (root != null) root.SetActive(true);
        UpdateLabel(spawned.maxHealth, spawned.maxHealth);
    }

    void HandleEnemyDied(Enemy died)
    {
        if (died != boss) return;
        boss = null;
        if (root != null) root.SetActive(false);
    }

    void Update()
    {
        if(canvasRect!=null && (canvasRect.rect.size!=lastCanvasSize || Screen.safeArea!=lastSafeArea))RefreshLayout();
        if (root == null || !root.activeSelf) return;

        // the boss object is destroyed after its death animation
        if (boss == null)
        {
            root.SetActive(false);
            return;
        }

        float target = boss.HealthFraction;

        float previous = displayed;
        displayed = Mathf.MoveTowards(displayed, target, fillLerpSpeed * Time.unscaledDeltaTime);
        if (displayed < previous) trailHold = trailDelay;

        if (trailHold > 0f) trailHold -= Time.unscaledDeltaTime;
        else trailed = Mathf.MoveTowards(trailed, displayed, trailLerpSpeed * Time.unscaledDeltaTime);
        if (trailed < displayed) trailed = displayed;

        SetWidth(fillRect, displayed);
        SetWidth(trailRect, trailed);

        if (fillImage != null)
            fillImage.color = Color.Lerp(lowColor, fullColor, Mathf.Clamp01(target * 1.4f));

        UpdateLabel(boss.CurrentHealth, boss.maxHealth);
    }

    void UpdateLabel(float current, float max)
    {
        if (healthText == null) return;
        healthText.text = Mathf.Max(0, Mathf.CeilToInt(current)) + " / " + Mathf.RoundToInt(max);
    }

    static void SetWidth(RectTransform rect, float fraction)
    {
        if (rect == null) return;
        var anchorMax = rect.anchorMax;
        anchorMax.x = Mathf.Clamp01(fraction);
        rect.anchorMax = anchorMax;
    }
}
