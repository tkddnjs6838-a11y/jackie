using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Presentation and local menu preferences only; GameFlowManager owns starting the run.
public sealed class StartScreenView : MonoBehaviour
{
    public Button startButton;
    public GameObject testModeNotice;
    public Button settingsButton;
    public Button closeSettingsButton;
    public GameObject settingsPanel;
    public Slider volumeSlider;
    public Text volumeLabel;
    const string VolumeKey="audio.masterVolume";
    void Awake()
    {
        if(settingsPanel!=null)settingsPanel.SetActive(false);
        if(settingsButton!=null)settingsButton.onClick.AddListener(OpenSettings);
        if(closeSettingsButton!=null)closeSettingsButton.onClick.AddListener(CloseSettings);
        if(volumeSlider!=null)
        {
            float volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1f));
            volumeSlider.SetValueWithoutNotify(volume);ApplyVolume(volume);
            volumeSlider.onValueChanged.AddListener(ApplyVolume);
        }
    }
    void Update()
    {
        if(settingsPanel!=null && settingsPanel.activeSelf && Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettings();
    }
    public void OpenSettings()
    {
        if(settingsPanel==null)return;
        settingsPanel.SetActive(true);
        if(startButton!=null)startButton.interactable=false;
        if(settingsButton!=null)settingsButton.interactable=false;
        if(closeSettingsButton!=null)closeSettingsButton.Select();
    }
    public void CloseSettings()
    {
        if(settingsPanel!=null)settingsPanel.SetActive(false);
        if(startButton!=null)startButton.interactable=true;
        if(settingsButton!=null)settingsButton.interactable=true;
        if(volumeSlider!=null){PlayerPrefs.SetFloat(VolumeKey,volumeSlider.value);PlayerPrefs.Save();}
        if(startButton!=null)startButton.Select();
    }
    void ApplyVolume(float value)
    {
        AudioListener.volume=value;
        if(volumeLabel!=null)volumeLabel.text=Mathf.RoundToInt(value*100)+"%";
    }
    void OnDisable(){if(settingsPanel!=null)settingsPanel.SetActive(false);}
    void OnDestroy()
    {
        if(settingsButton!=null)settingsButton.onClick.RemoveListener(OpenSettings);
        if(closeSettingsButton!=null)closeSettingsButton.onClick.RemoveListener(CloseSettings);
        if(volumeSlider!=null)volumeSlider.onValueChanged.RemoveListener(ApplyVolume);
    }
    public void ShowTestMode(bool enabled)
    {
        if(testModeNotice!=null)testModeNotice.SetActive(enabled);
    }
}
