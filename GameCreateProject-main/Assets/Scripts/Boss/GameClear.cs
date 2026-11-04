using UnityEngine;
using UnityEngine.UI;

// 게임 클리어 (엔딩). 마지막 보스(15분)를 쓰러뜨리면 게임을 멈추고 클리어 화면을 띄웁니다.
//  - 생존 시간, 처치 수, 레벨, 직업, 최고 클리어 기록
//  - 다시하기 / 메인 메뉴 / 게임 종료
// BossSystem 오브젝트에 BossDirector와 같이 붙습니다. (Tools > Game UI > Setup Boss + Mob HP Bars)
[DisallowMultipleComponent]
public class GameClear : MonoBehaviour
{
    private const string BestClearKey = "BestClearTime";

    [SerializeField] private string title = "STAGE CLEAR";
    [SerializeField] private string subtitle = "모든 보스를 쓰러뜨렸습니다!";
    [SerializeField, Min(0f)] private float fadeSeconds = 1f;
    [Tooltip("비워두면 맑은 고딕 등 PC 기본 한글 폰트")]
    [SerializeField] private Font uiFont;

    public bool IsCleared { get; private set; }

    private BossDirector director;
    private GameObject canvasObject;
    private CanvasGroup group;
    private Text statsLabels;
    private Text statsValues;
    private Text bestText;
    private Font runtimeFont;
    private float fadeTimer = -1f;

    private void Awake()
    {
        director = GetComponent<BossDirector>();
        if (director == null)
            director = FindAnyObjectByType<BossDirector>();
        if (director != null)
            director.FinalBossDefeated += Clear;
    }

    private void OnDestroy()
    {
        if (director != null)
            director.FinalBossDefeated -= Clear;
        if (canvasObject != null) Destroy(canvasObject);
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    [ContextMenu("테스트: 클리어 화면 보기")]
    private void DebugClear()
    {
        if (Application.isPlaying)
            Clear();
    }

    private void Clear()
    {
        GameSession session = GameSession.Instance;
        if (IsCleared || (session != null && session.IsGameOver))
            return;
        IsCleared = true;

        // 게임 정지 (일시정지/Tab/증강 창도 timeScale 0이면 열리지 않음)
        Time.timeScale = 0f;
        UITooltip.Hide();

        float time = session != null ? session.SurvivalTime : 0f;
        float best = PlayerPrefs.GetFloat(BestClearKey, 0f);
        bool newBest = best <= 0f || time < best;
        if (newBest)
        {
            PlayerPrefs.SetFloat(BestClearKey, time);
            PlayerPrefs.Save();
            best = time;
        }

        if (canvasObject == null)
            BuildUI();

        PlayerProgression progression = PlayerProgression.Instance;
        string job = !string.IsNullOrEmpty(CharacterWindowUI.JobNameOverride) ? CharacterWindowUI.JobNameOverride
            : progression != null ? CharacterWindowUI.JobName(progression.CurrentJob) : "-";
        statsLabels.text = "CLEAR TIME\nKILLS\nLEVEL\nJOB";
        statsValues.text =
            $"{GameSession.FormatTime(time)}\n" +
            $"{(session != null ? session.Kills : 0)}\n" +
            $"{(progression != null ? progression.Level : 1)}\n" +
            $"{job}";
        bestText.text = newBest ? "NEW RECORD!" : $"BEST  {GameSession.FormatTime(best)}";

        canvasObject.SetActive(true);
        group.alpha = 0f;
        fadeTimer = 0f;
    }

    private void Update()
    {
        if (fadeTimer < 0f || group == null)
            return;
        fadeTimer += Time.unscaledDeltaTime;
        group.alpha = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeSeconds);
        group.interactable = group.blocksRaycasts = group.alpha >= 0.99f;
        if (group.alpha >= 1f)
            fadeTimer = -1f;
    }

    // ───────── 화면 (실행 중 코드로 생성) ─────────

    private void BuildUI()
    {
        Font font = uiFont;
        if (font == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            font = runtimeFont;
        }

        canvasObject = new GameObject("GameClearCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // 모든 화면 위
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasObject.GetComponent<CanvasGroup>();
        Transform root = canvasObject.transform;

        RectTransform dim = Rect("Dim", root, Vector2.zero, new Vector2(1920f, 1080f));
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.sizeDelta = Vector2.zero;
        AddImage(dim, new Color(0.02f, 0.03f, 0.05f, 0.88f), true);

        Label("Title", root, font, title, 120, new Color(1f, 0.82f, 0.3f), new Vector2(0f, 300f), new Vector2(1400f, 150f));
        Label("Subtitle", root, font, subtitle, 34, Color.white, new Vector2(0f, 205f), new Vector2(1400f, 50f));

        RectTransform box = Rect("Stats", root, new Vector2(0f, 10f), new Vector2(640f, 300f));
        AddImage(box, new Color(0.08f, 0.09f, 0.12f, 0.95f), false);
        // 왼쪽 항목 이름 / 오른쪽 값 (두 칸으로 나눠서 줄 맞춤)
        statsLabels = Label("Labels", box, font, "", 34, Color.white, new Vector2(-130f, 20f), new Vector2(260f, 230f));
        statsLabels.alignment = TextAnchor.MiddleLeft;
        statsLabels.lineSpacing = 1.2f;
        statsValues = Label("Values", box, font, "", 34, Color.white, new Vector2(130f, 20f), new Vector2(260f, 230f));
        statsValues.alignment = TextAnchor.MiddleLeft;
        statsValues.lineSpacing = 1.2f;
        bestText = Label("Best", box, font, "", 26, new Color(1f, 0.82f, 0.3f), new Vector2(0f, -122f), new Vector2(520f, 40f));

        GameSession session = GameSession.Instance;
        bool hasMenu = session != null && session.HasMainMenu;
        float[] xs = hasMenu ? new[] { -340f, 0f, 340f } : new[] { -170f, 170f };
        int n = 0;
        Button("Retry", root, font, "RETRY", new Color(0.22f, 0.55f, 0.36f), new Vector2(xs[n++], -260f),
            () => { if (GameSession.Instance != null) GameSession.Instance.Restart(); });
        if (hasMenu)
            Button("MainMenu", root, font, "MAIN MENU", new Color(0.2f, 0.23f, 0.31f), new Vector2(xs[n++], -260f),
                () => { if (GameSession.Instance != null) GameSession.Instance.LoadMainMenu(); });
        Button("Quit", root, font, "QUIT", new Color(0.9f, 0.22f, 0.26f), new Vector2(xs[n], -260f),
            () => { if (GameSession.Instance != null) GameSession.Instance.QuitGame(); });

        canvasObject.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color, bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static Text Label(string name, Transform parent, Font font, string value, int size, Color color,
        Vector2 position, Vector2 boxSize)
    {
        RectTransform rect = Rect(name, parent, position, boxSize);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Button(string name, Transform parent, Font font, string label, Color color, Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rect = Rect(name, parent, position, new Vector2(300f, 84f));
        Image image = AddImage(rect, color, true);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        Label("Label", rect, font, label, 32, Color.white, Vector2.zero, new Vector2(300f, 84f));
    }
}
