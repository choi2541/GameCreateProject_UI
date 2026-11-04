using UnityEngine;
using UnityEngine.UI;

// 게임 오버 화면. GameSession.GameOver 이벤트를 받아 결과를 표시합니다.
// 이 컴포넌트가 붙은 오브젝트는 항상 켜두고, panel만 껐다 켭니다.
[DisallowMultipleComponent]
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

    [Header("결과 텍스트")]
    [SerializeField] private Text timeText;
    [SerializeField] private Text killText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text bestText;
    [SerializeField] private GameObject newBestLabel;

    [Header("버튼")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    private GameSession session;
    private float fadeTimer = -1f;

    private void Start()
    {
        if (panel != null)
            panel.SetActive(false);

        session = GameSession.Instance;
        if (session == null)
        {
            Debug.LogWarning("[GameOverUI] 씬에 GameSession이 없습니다.", this);
            return;
        }

        session.GameOver += Show;

        if (retryButton != null)
            retryButton.onClick.AddListener(session.Restart);

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(session.LoadMainMenu);
            if (!session.HasMainMenu)
            {
                // 메인 메뉴 씬이 없으면 버튼을 숨기고 다시하기를 가운데로 옮깁니다.
                mainMenuButton.gameObject.SetActive(false);
                if (retryButton != null)
                {
                    RectTransform rect = (RectTransform)retryButton.transform;
                    rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
                }
            }
        }

        if (session.IsGameOver)
            Show();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.GameOver -= Show;
    }

    private void Update()
    {
        if (fadeTimer < 0f || fadeGroup == null)
            return;

        // timeScale이 0이므로 unscaled 시간으로 페이드합니다.
        fadeTimer += Time.unscaledDeltaTime;
        fadeGroup.alpha = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeDuration);
        if (fadeGroup.alpha >= 1f)
            fadeTimer = -1f;
    }

    private void Show()
    {
        if (panel == null || session == null)
            return;

        PlayerProgression progression = PlayerProgression.Instance;

        if (timeText != null) timeText.text = GameSession.FormatTime(session.SurvivalTime);
        if (killText != null) killText.text = session.Kills.ToString();
        if (levelText != null) levelText.text = progression != null ? $"Lv. {progression.Level}" : "-";
        if (bestText != null) bestText.text = GameSession.FormatTime(session.BestTime);
        if (newBestLabel != null) newBestLabel.SetActive(session.IsNewBest);

        panel.SetActive(true);
        if (fadeGroup != null)
        {
            fadeGroup.alpha = fadeDuration > 0f ? 0f : 1f;
            fadeTimer = 0f;
        }
    }
}
