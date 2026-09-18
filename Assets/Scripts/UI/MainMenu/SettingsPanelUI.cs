using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Reads and saves the controls placed under SettingsPanel in the scene.</summary>
public class SettingsPanelUI : MonoBehaviour
{
    private const string BgmVolumeKey = "Settings.BgmVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";
    private const string FullScreenKey = "Settings.FullScreen";
    private const string VSyncKey = "Settings.VSync";
    private const string LanguageKey = "Settings.Language";

    [Header("Audio")]
    public AudioSource backgroundMusicSource;
    public Slider bgmSlider;
    public TMP_Text bgmPercent;
    public Slider sfxSlider;
    public TMP_Text sfxPercent;

    [Header("Display and language")]
    public Button fullScreenButton;
    public TMP_Text fullScreenText;
    public Button vSyncButton;
    public TMP_Text vSyncText;
    public Button languageButton;
    public TMP_Text languageText;

    [Header("Actions")]
    public Button saveButton;
    public Button closeButton;

    private float bgmVolume;
    private float sfxVolume;
    private bool fullScreen;
    private bool vSync;
    private string language;
    private bool eventsBound;

    // Future sound effect sources can use this saved value when they are added.
    public static float SoundEffectsVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);

    public void ApplySavedSettings()
    {
        if (backgroundMusicSource != null)
            backgroundMusicSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 0.8f));
        if (PlayerPrefs.HasKey(FullScreenKey))
            Screen.fullScreen = PlayerPrefs.GetInt(FullScreenKey) == 1;
        if (PlayerPrefs.HasKey(VSyncKey))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) == 1 ? 1 : 0;
    }

    public void Open()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError("SettingsPanelUI: connect all controls in the Inspector.", this);
            return;
        }

        gameObject.SetActive(true);
        BindEvents();
        LoadValues();
        RefreshControls();
        PreviewBgm(bgmVolume);
    }

    private bool HasRequiredReferences()
    {
        return backgroundMusicSource != null &&
               bgmSlider != null && bgmPercent != null &&
               sfxSlider != null && sfxPercent != null &&
               fullScreenButton != null && fullScreenText != null &&
               vSyncButton != null && vSyncText != null &&
               languageButton != null && languageText != null &&
               saveButton != null && closeButton != null;
    }

    private void BindEvents()
    {
        if (eventsBound) return;
        eventsBound = true;

        bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        fullScreenButton.onClick.AddListener(ToggleFullScreen);
        vSyncButton.onClick.AddListener(ToggleVSync);
        languageButton.onClick.AddListener(ToggleLanguage);
        saveButton.onClick.AddListener(Save);
        closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (!eventsBound) return;

        bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        fullScreenButton.onClick.RemoveListener(ToggleFullScreen);
        vSyncButton.onClick.RemoveListener(ToggleVSync);
        languageButton.onClick.RemoveListener(ToggleLanguage);
        saveButton.onClick.RemoveListener(Save);
        closeButton.onClick.RemoveListener(Close);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Close()
    {
        PreviewBgm(PlayerPrefs.GetFloat(BgmVolumeKey, 0.8f));
        gameObject.SetActive(false);
    }

    public void Save()
    {
        Screen.fullScreen = fullScreen;
        QualitySettings.vSyncCount = vSync ? 1 : 0;
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.SetInt(FullScreenKey, fullScreen ? 1 : 0);
        PlayerPrefs.SetInt(VSyncKey, vSync ? 1 : 0);
        PlayerPrefs.SetString(LanguageKey, language);
        PlayerPrefs.Save();
        Close();
    }

    private void LoadValues()
    {
        bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        fullScreen = PlayerPrefs.GetInt(FullScreenKey, Screen.fullScreen ? 1 : 0) == 1;
        vSync = PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        language = PlayerPrefs.GetString(LanguageKey, "ENGLISH");
    }

    private void RefreshControls()
    {
        bgmSlider.SetValueWithoutNotify(bgmVolume);
        sfxSlider.SetValueWithoutNotify(sfxVolume);
        bgmPercent.text = $"{Mathf.RoundToInt(bgmVolume * 100f)}%";
        sfxPercent.text = $"{Mathf.RoundToInt(sfxVolume * 100f)}%";
        fullScreenText.text = fullScreen ? "ON" : "OFF";
        vSyncText.text = vSync ? "ON" : "OFF";
        languageText.text = language;
    }

    private void OnBgmChanged(float value)
    {
        bgmVolume = value;
        bgmPercent.text = $"{Mathf.RoundToInt(value * 100f)}%";
        PreviewBgm(value);
    }

    private void OnSfxChanged(float value)
    {
        sfxVolume = value;
        sfxPercent.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void ToggleFullScreen()
    {
        fullScreen = !fullScreen;
        RefreshControls();
    }

    private void ToggleVSync()
    {
        vSync = !vSync;
        RefreshControls();
    }

    private void ToggleLanguage()
    {
        language = language == "ENGLISH" ? "KOREAN" : "ENGLISH";
        RefreshControls();
    }

    private void PreviewBgm(float value)
    {
        backgroundMusicSource.volume = Mathf.Clamp01(value);
    }
}
