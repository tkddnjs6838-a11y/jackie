using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public GameObject startPanel;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;

    public Button startButton;
    public Button retryButton;
    public Button victoryRetryButton;

    public Text victoryTimeText;
    public Text gameOverStatsText;
    public Text victoryStatsText;

    private bool started;
    private bool finished;

    void Awake()
    {
        // Statics survive SceneManager.LoadScene, so clear the boss flag on every (re)start.
        Enemy.ResetRunState();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (startPanel != null) startPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        

        EnsureControlGuide();
        if (victoryPanel != null) victoryPanel.SetActive(false);

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (victoryRetryButton != null) victoryRetryButton.onClick.AddListener(OnRetryClicked);
    }

    void EnsureControlGuide()
    {
        if (startPanel == null || startPanel.transform.Find("ControlGuide") != null) return;

        var guide = new GameObject("ControlGuide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        guide.transform.SetParent(startPanel.transform, false);
        var rect = guide.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -205f);
        rect.sizeDelta = new Vector2(610f, 126f);

        var background = guide.GetComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.055f, 0.86f);
        background.raycastTarget = false;

        var textObject = new GameObject("GuideText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(guide.transform, false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);

        var label = textObject.GetComponent<Text>();
        var sample = startPanel.GetComponentInChildren<Text>(true);
        label.font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "이동  WASD    |    기본 공격  좌클릭    |    회피  Space\n강공격  우클릭    |    마무리 콤보  좌 → 좌 → 우";
        if(WeaponTestSettings.AllGunsAtStart)
            label.text += "\n총 테스트  3 샷건 · 4 기관총 · 5 레이저  |  발사 좌클릭 · 재장전 R";
        label.text += "\n보유 증강 확인  Tab";
        label.fontSize = 22;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }


    void OnEnable()
    {
        PlayerHealth.OnPlayerDied += HandlePlayerDied;
        WinZone.OnPlayerWon += HandlePlayerWon;
    }

    void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= HandlePlayerDied;
        WinZone.OnPlayerWon -= HandlePlayerWon;
    }

    void OnStartClicked()
    {
        if (started || finished) return;
        started = true;
        if (startPanel != null) startPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (RunStats.Instance != null) RunStats.Instance.BeginRun();
        // browsers only allow audio to start after a real click, so the run button is the
        // earliest point the music can legally begin in the web build
        if (GameAudio.Instance != null) GameAudio.Instance.StartRunMusic();
    }

    void HandlePlayerDied()
    {
        if (!started || finished) return;
        finished = true;
        CloseGameplayPanels();
        FinishRun(gameOverStatsText);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        ReleaseCursor();
    }

    /// <summary>Closes the run and writes the summary, including any new personal bests.</summary>
    void FinishRun(Text target)
    {
        var stats = RunStats.Instance;
        if (stats == null) return;

        int kills = stats.Kills;
        int combo = stats.BestComboThisRun;
        float time = stats.SurvivalTime;
        int clicks = stats.Clicks;
        int hits = stats.Hits;
        float accuracy = stats.Accuracy;

        bool newKills = kills > stats.BestKills;
        bool newCombo = combo > stats.BestCombo;
        bool newTime = time > stats.BestTime;

        stats.EndRun();   // saves the records, so read the old bests before this

        if (target == null) return;
        target.text =
            "Survived  " + RunStats.FormatTime(time) + (newTime ? "   NEW BEST" : "") + "\n" +
            "Kills  " + kills + (newKills ? "   NEW BEST" : "") + "\n" +
            "Best Combo  x" + combo + (newCombo ? "   NEW BEST" : "") + "\n" +
            "Clicks  " + clicks + "   (" + hits + " connected, " + Mathf.RoundToInt(accuracy * 100f) + "%)\n" +
            "\nRecords    " + RunStats.FormatTime(stats.BestTime) + "    " + stats.BestKills + " kills    x" + stats.BestCombo;
    }

    void HandlePlayerWon()
    {
        if (!started || finished) return;
        finished = true;
        CloseGameplayPanels();
        if (victoryTimeText != null)
            victoryTimeText.text = "Clear Time  " + (RunStats.Instance != null ? RunStats.Instance.SurvivalTime : Time.timeSinceLevelLoad).ToString("F1") + "s";

        FinishRun(victoryStatsText);
        GameAudio.Play(GameAudio.Sfx.Victory);
        if (victoryPanel != null) victoryPanel.SetActive(true);
        ReleaseCursor();
    }

    void CloseGameplayPanels()
    {
        Time.timeScale = 0f;
        if (startPanel != null) startPanel.SetActive(false);
        var upgrades = FindFirstObjectByType<UpgradeSystem>();
        if (upgrades != null) upgrades.CancelChoices();
    }

    void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnRetryClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
