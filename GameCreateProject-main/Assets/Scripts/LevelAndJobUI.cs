using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerProgression))]
public class LevelAndJobUI : MonoBehaviour
{
    [Header("나중에 이미지를 넣을 수 있습니다. 지금은 비워두세요.")]
    [SerializeField] private Sprite warriorIcon;
    [SerializeField] private Sprite archerIcon;
    [SerializeField] private Sprite panelBackground;
    [SerializeField] private Sprite experienceBarBackground;
    [SerializeField] private Sprite experienceBarFill;
    [SerializeField] private Font uiFont;

    [SerializeField] private bool showExperienceHUD = true;
    private PlayerProgression progression;
    private GameObject canvasObject;
    private GameObject modal;
    private GameObject ownedEventSystem;
    private Font runtimeFont;
    private Font activeFont;
    private RectTransform fillRect;
    private Text expText;
    private Text jobText;
    private Text selectionText;
    private Text archerMark;
    private Button jobButton;
    private Button confirmButton;
    private Button archerButton;
    private Image archerRow;
    private PlayerJob selectedJob;
    private bool modalWasOpen;

    private void Awake()
    {
        progression = GetComponent<PlayerProgression>();
    }

    private void OnEnable()
    {
        progression.Changed += Refresh;
        if (canvasObject != null)
        {
            canvasObject.SetActive(true);
            Refresh();
        }
    }

    private void Start()
    {
        BuildUI();
        Refresh();
    }

    private void BuildUI()
    {
        activeFont = uiFont;
        if (activeFont == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
            activeFont = runtimeFont;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            ownedEventSystem = new GameObject("ProgressionEventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            ownedEventSystem.AddComponent<InputSystemUIInputModule>();
#else
            ownedEventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        canvasObject = new GameObject("LevelAndJobCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasObject.transform;

        RectTransform hud = Rect("ExperienceHUD", root, new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(20, 16), new Vector2(-20, 68));
        AddImage(hud, new Color(0.06f, 0.07f, 0.09f, 0.95f));
        RectTransform track = Rect("ExperienceTrack", hud, Vector2.zero, Vector2.one,
            new Vector2(6, 6), new Vector2(-210, -6));
        AddImage(track, new Color(0.18f, 0.18f, 0.16f), experienceBarBackground);
        fillRect = Rect("ExperienceFill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        AddImage(fillRect, new Color(0.85f, 0.76f, 0.18f), experienceBarFill);
        expText = Label("ExperienceText", track, "", 20, Color.white);
        jobButton = MakeButton("JobButton", hud, "1차 전직", new Color(0.3f, 0.28f, 0.2f));
        SetRect((RectTransform)jobButton.transform, new Vector2(1, 0), Vector2.one,
            new Vector2(-204, 6), new Vector2(-6, -6));
        jobText = jobButton.GetComponentInChildren<Text>();
        jobButton.onClick.AddListener(progression.OpenJobSelection);
        hud.gameObject.SetActive(showExperienceHUD);

        RectTransform overlay = Rect("JobSelectionOverlay", root, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        modal = overlay.gameObject;
        AddImage(overlay, new Color(0, 0, 0, 0.68f), null, true);
        RectTransform panel = Rect("JobPanel", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-250, -230), new Vector2(250, 230));
        AddImage(panel, new Color(0.77f, 0.73f, 0.64f), panelBackground);
        Text title = Label("Title", panel, "1차 전직", 30, new Color(0.15f, 0.12f, 0.09f));
        SetRect((RectTransform)title.transform, Vector2.up, Vector2.one,
            new Vector2(24, -64), new Vector2(-24, -14));
        Text description = Label("Description", panel,
            "직업을 선택한 뒤 확인을 눌러주세요.", 17, new Color(0.25f, 0.22f, 0.18f));
        SetRect((RectTransform)description.transform, Vector2.up, Vector2.one,
            new Vector2(20, -108), new Vector2(-20, -64));

        Button warrior = JobRow(panel, "WarriorRow", "전사  ·  준비 중", warriorIcon, -190,
            new Color(0.4f, 0.39f, 0.36f), out Image unusedRow, out Text unusedMark);
        warrior.interactable = false;
        archerButton = JobRow(panel, "ArcherRow", "궁수", archerIcon, -278,
            new Color(0.27f, 0.29f, 0.32f), out archerRow, out archerMark);
        archerButton.onClick.AddListener(() => { selectedJob = PlayerJob.Archer; RefreshSelection(); });

        selectionText = Label("SelectionText", panel, "직업을 선택하세요.", 18, new Color(0.2f, 0.17f, 0.13f));
        SetRect((RectTransform)selectionText.transform, new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(24, 92), new Vector2(-24, 152));
        confirmButton = MakeButton("Confirm", panel, "확인", new Color(0.69f, 0.08f, 0.31f));
        SetRect((RectTransform)confirmButton.transform, Vector2.zero, Vector2.zero,
            new Vector2(48, 30), new Vector2(238, 80));
        confirmButton.onClick.AddListener(() => progression.ConfirmJob(selectedJob));
        Button closeButton = MakeButton("Close", panel, "닫기", new Color(0.37f, 0.23f, 0.12f));
        SetRect((RectTransform)closeButton.transform, Vector2.zero, Vector2.zero,
            new Vector2(262, 30), new Vector2(452, 80));
        closeButton.onClick.AddListener(progression.CloseJobSelection);
        modal.SetActive(false);
    }

    private void Refresh()
    {
        if (canvasObject == null) return;
        fillRect.anchorMax = new Vector2((float)progression.Experience / progression.RequiredExperience, 1);
        expText.text = $"Lv. {progression.Level}    EXP {progression.Experience} / {progression.RequiredExperience}";
        jobText.text = progression.CurrentJob == PlayerJob.Archer ? "직업: 궁수"
            : progression.CanChooseJob ? "1차 전직 가능 · 선택" : "직업: 미전직";
        jobButton.interactable = progression.CanChooseJob && !progression.IsPlayerDead;
        bool open = progression.IsChoosingJob;
        if (open && !modalWasOpen) selectedJob = PlayerJob.None;
        modal.SetActive(open);
        if (open && !modalWasOpen && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(archerButton.gameObject);
        if (!open && modalWasOpen && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        modalWasOpen = open;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        bool chosen = selectedJob == PlayerJob.Archer;
        archerRow.color = chosen ? new Color(0.19f, 0.23f, 0.47f) : new Color(0.27f, 0.29f, 0.32f);
        archerMark.text = chosen ? "●" : "○";
        selectionText.text = chosen ? "선택한 직업: 궁수" : "직업을 선택하세요.";
        confirmButton.interactable = chosen;
    }

    private Button JobRow(RectTransform parent, string name, string text, Sprite icon,
        float bottom, Color color, out Image row, out Text mark)
    {
        Button button = MakeButton(name, parent, text, color);
        SetRect((RectTransform)button.transform, Vector2.up, Vector2.one,
            new Vector2(30, bottom), new Vector2(-30, bottom + 72));
        row = button.GetComponent<Image>();
        Text label = button.GetComponentInChildren<Text>();
        label.alignment = TextAnchor.MiddleLeft;
        SetRect((RectTransform)label.transform, Vector2.zero, Vector2.one,
            new Vector2(82, 0), new Vector2(-60, 0));
        RectTransform iconRect = Rect("Icon", (RectTransform)button.transform, new Vector2(0, 0.5f),
            new Vector2(0, 0.5f), new Vector2(14, -25), new Vector2(64, 25));
        Image image = AddImage(iconRect, icon == null ? new Color(0.6f, 0.58f, 0.5f) : Color.white, icon);
        image.preserveAspect = true;
        mark = Label("Selection", (RectTransform)button.transform, "○", 26, new Color(1f, 0.88f, 0.38f));
        SetRect((RectTransform)mark.transform, new Vector2(1, 0), Vector2.one,
            new Vector2(-58, 0), new Vector2(-10, 0));
        return button;
    }

    private Button MakeButton(string name, RectTransform parent, string text, Color color)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = AddImage(rect, color, null, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        button.colors = colors;
        Label("Label", rect, text, 22, Color.white);
        return button;
    }

    private Image AddImage(RectTransform rect, Color color, Sprite sprite = null, bool raycast = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = sprite == null ? color : Color.white;
        image.sprite = sprite;
        image.raycastTarget = raycast;
        return image;
    }

    private Text Label(string name, RectTransform parent, string value, int size, Color color)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = activeFont;
        text.fontSize = size;
        text.color = color;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RectTransform Rect(string name, RectTransform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        SetRect(rect, min, max, offsetMin, offsetMax);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void OnDisable()
    {
        if (progression != null)
        {
            progression.Changed -= Refresh;
            if (progression.IsChoosingJob) progression.CloseJobSelection();
        }
        if (canvasObject != null) canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
        if (runtimeFont != null) Destroy(runtimeFont);
    }
}
