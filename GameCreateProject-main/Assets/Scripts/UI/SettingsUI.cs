using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 설정 창: 전체 / 배경음 / 효과음 볼륨, 전체 화면. 값은 PlayerPrefs에 저장되어 다음 실행에도 유지됩니다.
// Canvas에 붙여 두면 게임 시작 시 저장된 전체 볼륨을 바로 적용합니다.
// (배경음/효과음 적용은 AudioVolumeController 담당)
[DisallowMultipleComponent]
public class SettingsUI : MonoBehaviour
{
    private const string VolumeKey = "MasterVolume";

    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeValueText;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Text bgmValueText;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Text sfxValueText;
    [SerializeField] private Toggle fullscreenToggle;

    private void Awake()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
    }

    private void Start()
    {
        Bind(volumeSlider, AudioListener.volume, value =>
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            UpdateTexts();
        });
        Bind(bgmSlider, PlayerPrefs.GetFloat(AudioVolumeController.BgmKey, 1f), value =>
        {
            AudioVolumeController.SetBgm(value);
            UpdateTexts();
        });
        Bind(sfxSlider, PlayerPrefs.GetFloat(AudioVolumeController.SfxKey, 1f), value =>
        {
            AudioVolumeController.SetSfx(value);
            UpdateTexts();
        });
        UpdateTexts();

        // Tab 창 방식 + 키 설정 항목 추가
        if (volumeSlider != null)
            SettingsExtras.Attach(volumeSlider.transform.parent as RectTransform);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.AddListener(value =>
            {
                ScreenModeController controller = FindAnyObjectByType<ScreenModeController>();
                if (controller != null) controller.SetFullscreen(value);
                else if (!Application.isEditor)
                {
                    Resolution desktop = Screen.currentResolution;
                    Screen.SetResolution(value ? desktop.width : Mathf.Min(1280, desktop.width - 100),
                        value ? desktop.height : Mathf.Min(720, desktop.height - 100),
                        value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                }
            });
        }
    }

    private void Update()
    {
        if (fullscreenToggle != null && fullscreenToggle.isOn != Screen.fullScreen)
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    private static void Bind(Slider slider, float value, UnityAction<float> onChange)
    {
        if (slider == null)
            return;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);
    }

    private void UpdateTexts()
    {
        SetPercent(volumeValueText, AudioListener.volume);
        SetPercent(bgmValueText, bgmSlider != null ? bgmSlider.value : AudioVolumeController.Bgm);
        SetPercent(sfxValueText, sfxSlider != null ? sfxSlider.value : AudioVolumeController.Sfx);
    }

    private static void SetPercent(Text text, float value)
    {
        if (text != null)
            text.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
