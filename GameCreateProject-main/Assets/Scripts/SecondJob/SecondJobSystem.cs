using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// 2차 전직: 기존 직업 공격 관리 및 충전 대쉬와 연동
//
// Player 오브젝트에 붙이면:
//   1차 직업(궁수) + Lv30 → 게임 정지 + "2차 전직을 하시겠습니까?" → [예] 선택창 / [나중에] 화면 버튼으로 다시 열기
//   선택창에서 고르면
//   · Disable On Advance 목록의 1차 스킬을 끄고
//   · 고른 2차 직업의 Enable Skills 목록을 켜고
//   · 대쉬 등에 OnSecondJobAdvanced 알림 + On Advanced 이벤트 실행
//
// 2차 스킬 추가/삭제 : Inspector 목록에 스킬 컴포넌트를 넣고 빼기만 하면 됨
// 기능을 사용하지 않을 때는 씬의 이 컴포넌트를 비활성화합니다.
// JobAttackController도 이 타입을 참조하므로 소스 파일만 삭제하지 마세요.
//
// ※ 2차 스킬은 JobAttackController의 목록에 넣지 마세요. 이 컴포넌트가 직접 켜고 끕니다.
// ※ 목록을 비워두면 실행 시 자동으로 채웁니다:
//    Disable On Advance ← PiercingProjectileSkill / Enable Skills ← ISecondJobSkill 스킬들
// ─────────────────────────────────────────────────────────────────────────────

// 2차 스킬 표시용. 이 인터페이스가 붙은 스킬은 2차 전직 때 자동으로 켜집니다.
public interface ISecondJobSkill { }

[DisallowMultipleComponent]
public class SecondJobSystem : MonoBehaviour
{
    [Serializable]
    public class SecondJob
    {
        public string displayName = "2차 궁수";
        [TextArea(2, 4)] public string description = "2차 전직 (임시 설명)";
        [Tooltip("선택창에 표시할 이미지 (선택)")]
        public Sprite icon;
        [Tooltip("이 2차 직업이 되면 켤 스킬/공격 컴포넌트 (Z, X, C 2차 스킬 등)")]
        public MonoBehaviour[] enableSkills = new MonoBehaviour[0];
    }

    [Header("전직 조건")]
    [SerializeField] private PlayerJob requiredFirstJob = PlayerJob.Archer;
    [SerializeField, Min(2)] private int requiredLevel = 30;

    [Header("2차 전직 시 끌 1차 스킬 (예: 관통탄)")]
    [SerializeField] private MonoBehaviour[] disableOnAdvance = new MonoBehaviour[0];

    [Header("2차 직업 목록 (2개 넣으면 선택 상자 2개)")]
    [SerializeField]
    private SecondJob[] jobs =
    {
        new SecondJob { displayName = "2차 궁수", description = "부채꼴 연사 · 화살비\n크리스탈 애로우 · 회피 사격" },
    };

    [Header("2차 전직 순간 실행할 것 (예: 대쉬 데미지 켜기)")]
    [SerializeField] private UnityEvent onAdvanced = new UnityEvent();

    [Header("선택창")]
    [Tooltip("비워두면 맑은 고딕 등 PC 기본 한글 폰트 사용")]
    [SerializeField] private Font uiFont;

    public static SecondJobSystem Instance { get; private set; }

    public int ChosenIndex { get; private set; } = -1;
    public bool HasAdvanced => ChosenIndex >= 0;
    public bool IsChoosing { get; private set; }
    public SecondJob Current => HasAdvanced ? jobs[ChosenIndex] : null;
    public event Action<SecondJob> Advanced;

    // 3차 이상 전직 시스템이 켜는 값. 켜지면 2차 스킬을 다시 켜지 않습니다.
    public static bool SupersededByHigherJob;

    private PlayerProgression progression;
    private PlayerHealth health;
    private float previousTimeScale = 1f;
    private bool deathHandled;

    // 선택창 (실행 중에 코드로 생성)
    private GameObject canvasObject;
    private GameObject promptRoot;
    private GameObject selectRoot;
    private GameObject laterButton;
    private bool promptShown;
    private bool deferred;
    private Font runtimeFont;
    private Image[] boxes;
    private Button confirmButton;
    private int selected = -1;

    private static readonly Color BoxColor = new Color(0.13f, 0.15f, 0.2f, 1f);
    private static readonly Color SelectedColor = new Color(0.22f, 0.4f, 0.32f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("SecondJobSystem은 하나만 두세요.", this);
            enabled = false;
            return;
        }
        Instance = this;
        progression = GetComponent<PlayerProgression>();
        health = GetComponent<PlayerHealth>();
        AutoFillLists();

        // 2차 스킬은 전직 전까지 꺼둡니다.
        foreach (SecondJob job in jobs)
            SetEnabled(job.enableSkills, false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        SupersededByHigherJob = false;
        if (HasAdvanced)
            CharacterWindowUI.JobNameOverride = null;
        if (canvasObject != null) Destroy(canvasObject);
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    private void Update()
    {
        if (progression == null) progression = PlayerProgression.Instance;
        bool dead = health != null && health.IsDead;

        if (dead)
        {
            if (IsChoosing) CloseSelection(false);
            ShowLaterButton(false);
            if (!deathHandled && HasAdvanced)
            {
                deathHandled = true;
                SetEnabled(Current.enableSkills, false);
            }
            return;
        }

        if (HasAdvanced)
        {
            // 다른 스크립트가 1차 스킬을 다시 켜도 2차 상태를 유지합니다.
            // (3차 이상으로 전직하면 그쪽 시스템이 스킬을 관리하므로 손대지 않음)
            SetEnabled(disableOnAdvance, false);
            if (!SupersededByHigherJob)
                SetEnabled(Current.enableSkills, true);
            return;
        }

        // 처음 조건을 만족하면 한 번 물어봄. "나중에"를 누르면 화면의 버튼으로 다시 열 수 있음
        if (!IsChoosing && !promptShown && CanAdvance())
            OpenPrompt();

        ShowLaterButton(deferred && !IsChoosing);
    }

    private bool CanAdvance()
    {
        return progression != null && jobs.Length > 0
            && progression.CurrentJob == requiredFirstJob
            && progression.Level >= requiredLevel
            && !progression.IsChoosingJob
            && Time.timeScale > 0f;
    }

    [ContextMenu("목록 자동 채우기")]
    private void AutoFillLists()
    {
        if (disableOnAdvance == null || disableOnAdvance.Length == 0)
            disableOnAdvance = GetComponentsInChildren<PiercingProjectileSkill>(true);

        if (jobs != null && jobs.Length > 0
            && (jobs[0].enableSkills == null || jobs[0].enableSkills.Length == 0))
        {
            var found = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ISecondJobSkill)
                    found.Add(behaviour);
            }
            jobs[0].enableSkills = found.ToArray();
        }
    }

    [ContextMenu("테스트: 지금 2차 전직 창 열기")]
    private void DebugOpen()
    {
        if (Application.isPlaying && !HasAdvanced && !IsChoosing && jobs.Length > 0)
            OpenPrompt();
    }

    // ───────────────────────── 전직 처리 ─────────────────────────

    // "2차 전직을 하시겠습니까?" (게임 정지)
    private void OpenPrompt()
    {
        promptShown = true;
        BeginChoosing();
        promptRoot.SetActive(true);
        selectRoot.SetActive(false);
    }

    // 직업 선택 상자 (게임 정지)
    private void OpenSelection()
    {
        if (!IsChoosing)
            BeginChoosing();
        selected = jobs.Length == 1 ? 0 : -1;
        promptRoot.SetActive(false);
        selectRoot.SetActive(true);
        RefreshSelection();
    }

    private void BeginChoosing()
    {
        IsChoosing = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (canvasObject == null)
            BuildUI();
        ShowLaterButton(false);
    }

    private void Later()
    {
        deferred = true;
        CloseSelection(true);
    }

    private void OnLaterButton()
    {
        if (!IsChoosing && !HasAdvanced && CanAdvance())
            OpenSelection();
    }

    private void CloseSelection(bool resume)
    {
        IsChoosing = false;
        if (promptRoot != null) promptRoot.SetActive(false);
        if (selectRoot != null) selectRoot.SetActive(false);
        if (resume && (health == null || !health.IsDead))
            Time.timeScale = previousTimeScale;
    }

    private void ShowLaterButton(bool show)
    {
        if (laterButton != null && laterButton.activeSelf != show)
            laterButton.SetActive(show);
    }

    private void Select(int index)
    {
        selected = index;
        RefreshSelection();
    }

    private void Confirm()
    {
        if (!IsChoosing || HasAdvanced || (health != null && health.IsDead)
            || selected < 0 || selected >= jobs.Length)
            return;

        ChosenIndex = selected;
        deferred = false;
        SetEnabled(disableOnAdvance, false);
        SetEnabled(Current.enableSkills, true);
        CharacterWindowUI.JobNameOverride = Current.displayName;

        CloseSelection(true);
        // 대쉬 등 2차 모드가 있는 스크립트에 알림 (받는 쪽이 없어도 에러 없음)
        BroadcastMessage("OnSecondJobAdvanced", SendMessageOptions.DontRequireReceiver);
        onAdvanced?.Invoke();
        Advanced?.Invoke(Current);
    }

    private void OnDisable()
    {
        if (IsChoosing) CloseSelection(true);
        ShowLaterButton(false);
        if (HasAdvanced) SetEnabled(Current.enableSkills, false);
    }

    private static void SetEnabled(MonoBehaviour[] list, bool value)
    {
        if (list == null) return;
        foreach (MonoBehaviour behaviour in list)
        {
            if (behaviour != null && behaviour.enabled != value)
                behaviour.enabled = value;
        }
    }

    // ───────────────────────── 선택창 UI ─────────────────────────

    private void RefreshSelection()
    {
        if (boxes == null) return;
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].color = i == selected ? SelectedColor : BoxColor;
        if (confirmButton != null)
            confirmButton.interactable = selected >= 0;
    }

    private void BuildUI()
    {
        Font font = uiFont;
        if (font == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            font = runtimeFont;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
            Debug.LogWarning("[SecondJobSystem] EventSystem이 없어 버튼이 눌리지 않을 수 있습니다.", this);

        canvasObject = new GameObject("SecondJobCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRoot = (RectTransform)canvasObject.transform;

        // ① 확인 창: "2차 전직을 하시겠습니까?"
        RectTransform prompt = Rect("Prompt", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        promptRoot = prompt.gameObject;
        AddImage(prompt, new Color(0f, 0f, 0f, 0.7f), true);
        RectTransform promptBox = Centered("Box", prompt, Vector2.zero, new Vector2(760f, 380f));
        AddImage(promptBox, new Color(0.08f, 0.09f, 0.12f, 0.97f), true);
        Label("Level", promptBox, font, $"Lv.{requiredLevel} 달성!", 34, new Color(1f, 0.8f, 0.28f),
            new Vector2(0f, 120f), new Vector2(700f, 50f));
        Label("Question", promptBox, font, "2차 전직을 하시겠습니까?", 46, Color.white,
            new Vector2(0f, 40f), new Vector2(700f, 70f));
        MakeButton("Yes", promptBox, font, "예", new Color(0.22f, 0.55f, 0.36f),
            new Vector2(-150f, -100f), new Vector2(260f, 80f), OpenSelection);
        MakeButton("Later", promptBox, font, "나중에", new Color(0.2f, 0.23f, 0.31f),
            new Vector2(150f, -100f), new Vector2(260f, 80f), Later);
        promptRoot.SetActive(false);

        // ③ "나중에"를 눌렀을 때 화면 왼쪽 위에 남는 버튼
        RectTransform later = (RectTransform)new GameObject("LaterButton", typeof(RectTransform)).transform;
        later.SetParent(canvasRoot, false);
        later.anchorMin = later.anchorMax = new Vector2(0f, 1f);
        later.pivot = new Vector2(0f, 1f);
        later.sizeDelta = new Vector2(230f, 44f);
        later.anchoredPosition = new Vector2(32f, -164f);
        Image laterImage = AddImage(later, new Color(0.55f, 0.36f, 0.12f, 0.95f), true);
        Button laterBtn = later.gameObject.AddComponent<Button>();
        laterBtn.targetGraphic = laterImage;
        laterBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        laterBtn.onClick.AddListener(OnLaterButton);
        Label("Label", later, font, "2차 전직 하기", 24, Color.white, Vector2.zero, new Vector2(230f, 44f));
        laterButton = later.gameObject;
        laterButton.SetActive(false);

        // ② 직업 선택 창
        RectTransform root = Rect("Select", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        selectRoot = root.gameObject;
        AddImage(root, new Color(0f, 0f, 0f, 0.75f), true);

        Label("Title", root, font, "2차 전직", 64, new Color(1f, 0.8f, 0.28f),
            new Vector2(0f, 360f), new Vector2(900f, 90f));
        Label("Subtitle", root, font, "직업을 선택한 뒤 확인을 눌러주세요.", 28, new Color(0.75f, 0.77f, 0.83f),
            new Vector2(0f, 290f), new Vector2(900f, 44f));

        // 직업 상자 (가로로 나열)
        const float boxW = 400f, boxH = 460f, gap = 60f;
        float startX = -(jobs.Length - 1) * (boxW + gap) * 0.5f;
        boxes = new Image[jobs.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            int index = i;
            SecondJob job = jobs[i];
            RectTransform box = Centered("JobBox" + i, root, new Vector2(startX + i * (boxW + gap), 0f), new Vector2(boxW, boxH));
            boxes[i] = AddImage(box, BoxColor, true);
            Button button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = boxes[i];
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Select(index));

            RectTransform iconRect = Centered("Icon", box, new Vector2(0f, 90f), new Vector2(200f, 200f));
            Image icon = AddImage(iconRect, job.icon != null ? Color.white : new Color(1f, 1f, 1f, 0.08f), false);
            icon.sprite = job.icon;
            icon.preserveAspect = true;

            Label("Name", box, font, job.displayName, 40, Color.white, new Vector2(0f, -55f), new Vector2(360f, 56f));
            Text desc = Label("Desc", box, font, job.description, 24, new Color(0.75f, 0.77f, 0.83f),
                new Vector2(0f, -150f), new Vector2(340f, 110f));
            desc.alignment = TextAnchor.UpperCenter;
        }

        RectTransform confirm = Centered("Confirm", root, new Vector2(0f, -340f), new Vector2(320f, 80f));
        Image confirmImage = AddImage(confirm, new Color(0.22f, 0.55f, 0.36f), true);
        confirmButton = confirm.gameObject.AddComponent<Button>();
        confirmButton.targetGraphic = confirmImage;
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = confirmButton.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        confirmButton.colors = colors;
        confirmButton.onClick.AddListener(Confirm);
        Label("Label", confirm, font, "확인", 34, Color.white, Vector2.zero, new Vector2(320f, 80f));

        selectRoot.SetActive(false);
    }

    private static void MakeButton(string name, Transform parent, Font font, string label, Color color,
        Vector2 position, Vector2 size, UnityAction onClick)
    {
        RectTransform rect = Centered(name, parent, position, size);
        Image image = AddImage(rect, color, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        Label("Label", rect, font, label, 34, Color.white, Vector2.zero, size);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offMin;
        rect.offsetMax = offMax;
        return rect;
    }

    private static RectTransform Centered(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
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
        RectTransform rect = Centered(name, parent, position, boxSize);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
