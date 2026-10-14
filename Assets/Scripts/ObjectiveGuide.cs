using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;

/// <summary>
/// After the boss dies, points the player back to the goal zone:
/// a rotating on-screen arrow with the remaining distance, plus a short banner.
/// </summary>
public class ObjectiveGuide : MonoBehaviour
{
    public Transform target;
    public Transform player;

    public GameObject guideRoot;
    public RectTransform arrowRect;
    public Image arrowImage;
    public Text distanceText;

    public GameObject bannerRoot;
    public Text bannerText;

    public string bannerMessage = "BOSS DEFEATED!  Return to the start zone.";
    public float bannerDuration = 5f;
    public string distanceFormat = "Goal  {0} m";
    public float arrivedDistance = 3f;

    private float bannerTimer;
    private Camera cam;
    NavMeshPath route;
    readonly Vector3[] corners=new Vector3[64];
    Vector3 waypoint;
    float routeDistance,nextRoute;
    bool stageGuide;
    public void SetStageTarget(Transform destination,bool finalStage)
    {
        ResetGuide();target=destination;stageGuide=true;
        bannerMessage=finalStage?"최종 보스 격파 · 출구로 이동하세요":"스테이지 클리어 · 다음 포탈이 열렸습니다";
        distanceFormat="포탈까지 {0} m";
        if(guideRoot!=null)GameUiStyle.Apply(guideRoot.GetComponent<Image>(),GameUiStyle.Ink);
        if(bannerRoot!=null)GameUiStyle.Apply(bannerRoot.GetComponent<Image>(),GameUiStyle.Ink);
        var font=Resources.Load<Font>("UI/AugmentKorean");
        if(distanceText!=null){distanceText.font=font;distanceText.color=new Color(.84f,.86f,.84f);}
        if(bannerText!=null){bannerText.font=font;bannerText.color=new Color(.84f,.86f,.84f);bannerText.fontSize=24;}
    }
    public void ResetGuide()
    {
        bannerTimer=0;nextRoute=0;target=null;
        if(guideRoot!=null)guideRoot.SetActive(false);
        if(bannerRoot!=null)bannerRoot.SetActive(false);
    }

    void Awake()
    {
        route=new NavMeshPath();
        if (target == null)
        {
            var zone = GameObject.Find("WinZone");
            if (zone != null) target = zone.transform;
        }
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (arrowImage != null && arrowImage.sprite == null)
            arrowImage.sprite = BuildArrowSprite();

        if (guideRoot != null) guideRoot.SetActive(false);
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    void OnEnable()
    {
        Enemy.OnBossDefeated += HandleBossDefeated;
        WinZone.OnPlayerWon += HandlePlayerWon;
    }

    void OnDisable()
    {
        Enemy.OnBossDefeated -= HandleBossDefeated;
        WinZone.OnPlayerWon -= HandlePlayerWon;
    }

    void HandleBossDefeated()
    {
        if(target==null)return;
        if (guideRoot != null) guideRoot.SetActive(true);

        if (bannerRoot != null)
        {
            if (bannerText != null) bannerText.text = bannerMessage;
            bannerRoot.SetActive(true);
            bannerTimer = bannerDuration;
        }
    }

    void HandlePlayerWon()
    {
        if (guideRoot != null) guideRoot.SetActive(false);
        if (bannerRoot != null) bannerRoot.SetActive(false);
    }

    void Update()
    {
        if(stageGuide && (RunStats.Instance==null || !RunStats.Instance.Running))
        {if(guideRoot!=null)guideRoot.SetActive(false);if(bannerRoot!=null)bannerRoot.SetActive(false);return;}
        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            if (bannerTimer <= 0f && bannerRoot != null)
                bannerRoot.SetActive(false);
        }

        if (guideRoot == null || !guideRoot.activeSelf) return;
        if (target == null || player == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // --- distance (horizontal only, so height differences don't inflate it) ---
        waypoint=target.position;
        if(stageGuide && Time.unscaledTime>=nextRoute)
        {
            nextRoute=Time.unscaledTime+.3f;routeDistance=0;
            if(MapNavigation.Sample(player.position,3,out var from) && MapNavigation.Sample(target.position,2,out var to)
                && NavMesh.CalculatePath(from,to,NavMesh.AllAreas,route) && route.status==NavMeshPathStatus.PathComplete)
            {
                int count=route.GetCornersNonAlloc(corners);
                for(int i=1;i<count;i++)routeDistance+=Vector3.Distance(corners[i-1],corners[i]);
            }
            else route.ClearCorners();
        }
        if(stageGuide)
        {
            int count=route.GetCornersNonAlloc(corners);
            for(int i=1;i<count;i++)if((corners[i]-player.position).sqrMagnitude>1f){waypoint=corners[i];break;}
        }
        Vector3 flat = waypoint - player.position;
        flat.y = 0f;
        Vector3 toGoal=target.position-player.position;toGoal.y=0;
        float dist = toGoal.magnitude;

        if (distanceText != null)
            distanceText.text = dist <= arrivedDistance
                ? (stageGuide?"E  포탈 입장":"Goal  arrived")
                : string.Format(distanceFormat, Mathf.RoundToInt(stageGuide?Mathf.Max(dist,routeDistance):dist));

        // --- arrow rotation toward the target ---
        // Bearing is taken in world space rather than screen space: WorldToScreenPoint
        // mirrors points behind the camera, and the usual "flip around centre" fix
        // distorts the angle badly (measured 112 deg where 150 deg was correct).
        if (arrowRect != null && flat.sqrMagnitude > 0.0001f)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude > 0.0001f)
            {
                // positive bearing = target is to the player's right
                float bearing = Vector3.SignedAngle(camForward.normalized, flat.normalized, Vector3.up);
                // sprite points up at 0 deg; Unity's z rotation is counter-clockwise
                arrowRect.localRotation = Quaternion.Euler(0f, 0f, -bearing);
            }
        }
    }

    Sprite BuildArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);            // 0 at base, 1 at tip
            float halfWidth = (1f - t) * 0.5f * size;
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - size * 0.5f) <= halfWidth;
                tex.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
