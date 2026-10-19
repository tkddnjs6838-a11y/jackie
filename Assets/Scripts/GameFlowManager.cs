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
        if(GetComponent<MapSandbox>()==null)gameObject.AddComponent<MapSandbox>();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (startPanel != null) startPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        

        var startView = startPanel != null ? startPanel.GetComponent<StartScreenView>() : null;
        if (startView != null)
        {
            startButton = startView.startButton;
            startView.ShowTestMode(WeaponTestSettings.AllGunsAtStart);
        }
        if (victoryPanel != null) victoryPanel.SetActive(false);
        retryButton=RunResultView.Build(gameOverPanel,false);
        victoryRetryButton=RunResultView.Build(victoryPanel,true);

        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (victoryRetryButton != null) victoryRetryButton.onClick.AddListener(OnRetryClicked);
    }

    void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (retryButton != null) retryButton.onClick.RemoveListener(OnRetryClicked);
        if (victoryRetryButton != null) victoryRetryButton.onClick.RemoveListener(OnRetryClicked);
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
        var maps=GetComponent<MapSandbox>();
        if(maps!=null && (maps.Busy || maps.CurrentMap<0))return;
        started = true;
        // Keep audio initialization in the real click callback for WebGL browser policies.
        if (GameAudio.Instance != null) GameAudio.Instance.StartRunMusic();
        var feedback=startButton!=null?startButton.GetComponent<StartButtonSplitEffect>():null;
        if(feedback!=null && feedback.isActiveAndEnabled)
        {
            feedback.PlayLaunch();
            StartCoroutine(StartAfterFeedback());
        }
        else BeginGameplay();
    }

    System.Collections.IEnumerator StartAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(.28f);
        if(!finished)BeginGameplay();
    }

    void BeginGameplay()
    {
        if (startPanel != null) startPanel.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = MobileControls.Enabled ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = MobileControls.Enabled;

        if (RunStats.Instance != null) RunStats.Instance.BeginRun();

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

    public void CompleteStages() { HandlePlayerWon(); }

    void HandlePlayerWon()
    {
        if (!started || finished) return;
        finished = true;
        CloseGameplayPanels();
        if (victoryTimeText != null)
            victoryTimeText.text = "Clear Time  " + (RunStats.Instance != null ? RunStats.Instance.SurvivalTime : Time.timeSinceLevelLoad).ToString("F1") + "s";

        FinishRun(victoryStatsText);
        if(GameAudio.Instance!=null)GameAudio.Instance.StopMusic();
        GameAudio.Play(GameAudio.Sfx.Victory);
        if (victoryPanel != null) victoryPanel.SetActive(true);
        ReleaseCursor();
    }

    void CloseGameplayPanels()
    {
        Time.timeScale = 0f;
        foreach (var guide in FindObjectsByType<ObjectiveGuide>(FindObjectsSortMode.None)) guide.ResetGuide();
        GetComponent<MapSandbox>()?.HideHud();
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
        SceneManager.LoadScene(gameObject.scene.name);
    }
}
