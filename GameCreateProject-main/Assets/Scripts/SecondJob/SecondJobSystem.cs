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
        new SecondJob { displayName = "2차 궁수", description = "부채꼴 연사 · 화살비\n가시 덩굴 화살 · 회피 사격" },
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
    private bool framed; // 도트 테두리(나무/돌)를 쓰는지

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
        {
            SetEnabled(job.enableSkills, false);
            // 씬에 저장된 예전 이름(크리스탈 애로우) → 새 이름
            if (job.description != null) job.description = job.description.Replace("크리스탈 애로우", CrystalArrowSkill.DisplayName);
        }
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
        {
            if (framed)
            {
                float v = i == selected ? 1f : 0.62f;
                boxes[i].color = new Color(v, v, v, 1f);
                boxes[i].rectTransform.localScale = Vector3.one * (i == selected ? 1f : 0.96f);
            }
            else boxes[i].color = i == selected ? SelectedColor : BoxColor;
        }
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
        framed = JobWindowSkin.HasFrames;

        // ① 확인 창: "2차 전직을 하시겠습니까?"
        RectTransform prompt = Rect("Prompt", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        promptRoot = prompt.gameObject;
        AddImage(prompt, new Color(0f, 0f, 0f, 0.7f), true);
        RectTransform promptBox = Centered("Box", prompt, Vector2.zero, new Vector2(760f, 400f));
        Image promptImage = AddImage(promptBox, new Color(0.08f, 0.09f, 0.12f, 0.97f), true);
        if (framed) JobWindowSkin.Frame(promptImage, JobWindowSkin.Second);
        Text lv = Label("Level", promptBox, font, $"Lv.{requiredLevel} 달성!", 34, new Color(1f, 0.8f, 0.28f),
            new Vector2(0f, 110f), new Vector2(640f, 50f));
        Text q = Label("Question", promptBox, font, "2차 전직을 하시겠습니까?", 44, Color.white,
            new Vector2(0f, 35f), new Vector2(640f, 70f));
        JobWindowSkin.Outline(lv, 2f); JobWindowSkin.Outline(q, 2.5f);
        Button yes = MakeButton("Yes", promptBox, font, "예", new Color(0.22f, 0.55f, 0.36f),
            new Vector2(-140f, -100f), new Vector2(240f, 76f), OpenSelection);
        Button no = MakeButton("Later", promptBox, font, "나중에", new Color(0.2f, 0.23f, 0.31f),
            new Vector2(140f, -100f), new Vector2(240f, 76f), Later);
        if (framed)
        {
            JobWindowSkin.SkinButton(yes, true); JobWindowSkin.SkinButton(no, false);
            JobWindowSkin.FitRow(-100f, 28f, yes, no);
        }
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

        // HUD의 CHOOSE JOB 버튼과 같은 모양·같은 자리로 (도트 UI가 있을 때)
        Button hudLater = JobWindowSkin.CloneHudJobButton("SecondJobLaterButton", "2차 전직 하기", OnLaterButton, out Text _);
        if (hudLater != null)
        {
            Destroy(laterButton);
            laterButton = hudLater.gameObject;
        }

        // ② 직업 선택 창
        RectTransform root = Rect("Select", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        selectRoot = root.gameObject;
        AddImage(root, new Color(0f, 0f, 0f, 0.75f), true);

        Text title = Label("Title", root, font, "2차 전직", 64, new Color(1f, 0.8f, 0.28f),
            new Vector2(0f, 385f), new Vector2(900f, 84f));
        Text subtitle = Label("Subtitle", root, font, "직업을 선택한 뒤 확인을 눌러주세요.", 28, new Color(0.88f, 0.88f, 0.9f),
            new Vector2(0f, 325f), new Vector2(900f, 42f));
        JobWindowSkin.Outline(title, 3f); JobWindowSkin.Outline(subtitle, 2f);

        // 직업 상자 (가로로 나열)
        float boxW = framed ? 600f : 400f, boxH = framed ? 580f : 460f;
        const float gap = 60f;
        Sprite fallbackArt = LoadArt("JobArt/select_second"); // 2차 카드 그림 (없으면 그림 없이 글자만)
        float startX = -(jobs.Length - 1) * (boxW + gap) * 0.5f;
        boxes = new Image[jobs.Length];
        for (int i = 0; i < jobs.Length; i++)
        {
            int index = i;
            SecondJob job = jobs[i];
            RectTransform box = Centered("JobBox" + i, root, new Vector2(startX + i * (boxW + gap), framed ? -15f : 0f), new Vector2(boxW, boxH));
            boxes[i] = AddImage(box, BoxColor, true);
            if (framed) JobWindowSkin.Frame(boxes[i], JobWindowSkin.Second);
            Button button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = boxes[i];
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Select(index));

            Sprite art = job.icon != null ? job.icon : fallbackArt;
            if (framed)
            {
                // 도트 테두리 카드: 위 = 그림(가로로 긴 일러스트), 아래 = 이름·설명
                float artW = boxW - 110f, artH = artW * 819f / 1456f;
                if (art != null)
                {
                    Image pic = AddImage(Centered("Art", box, new Vector2(0f, 100f), new Vector2(artW, artH)), Color.white, false);
                    pic.sprite = art;
                    pic.preserveAspect = true;
                }
                float textY = art != null ? -105f : 40f;
                Text n = Label("Name", box, font, job.displayName, 44, new Color(0.62f, 0.92f, 0.5f), new Vector2(0f, textY), new Vector2(boxW - 100f, 56f));
                JobWindowSkin.Outline(n, 2.5f);
                Text d = Label("Desc", box, font, job.description, 24, new Color(0.88f, 0.88f, 0.9f),
                    new Vector2(0f, textY - 85f), new Vector2(boxW - 110f, 100f));
                d.alignment = TextAnchor.UpperCenter;
                continue;
            }

            RectTransform iconRect = Centered("Icon", box, new Vector2(0f, 90f), new Vector2(200f, 200f));
            Image icon = AddImage(iconRect, art != null ? Color.white : new Color(1f, 1f, 1f, 0.08f), false);
            icon.sprite = art;
            icon.preserveAspect = true;

            Label("Name", box, font, job.displayName, 40, Color.white, new Vector2(0f, -55f), new Vector2(360f, 56f));
            Text desc = Label("Desc", box, font, job.description, 24, new Color(0.75f, 0.77f, 0.83f),
                new Vector2(0f, -150f), new Vector2(340f, 110f));
            desc.alignment = TextAnchor.UpperCenter;
        }

        RectTransform confirm = Centered("Confirm", root, new Vector2(0f, framed ? -390f : -340f), new Vector2(320f, 76f));
        Image confirmImage = AddImage(confirm, new Color(0.22f, 0.55f, 0.36f), true);
        confirmButton = confirm.gameObject.AddComponent<Button>();
        confirmButton.targetGraphic = confirmImage;
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = confirmButton.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        confirmButton.colors = colors;
        confirmButton.onClick.AddListener(Confirm);
        Label("Label", confirm, font, "확인", 28, Color.white, Vector2.zero, new Vector2(320f, 76f));
        if (framed) { JobWindowSkin.SkinButton(confirmButton, true); JobWindowSkin.FitRow(-390f, 0f, confirmButton); }

        selectRoot.SetActive(false);
    }

    private static Button MakeButton(string name, Transform parent, Font font, string label, Color color,
        Vector2 position, Vector2 size, UnityAction onClick)
    {
        RectTransform rect = Centered(name, parent, position, size);
        Image image = AddImage(rect, color, true);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        Label("Label", rect, font, label, 28, Color.white, Vector2.zero, size);
        return button;
    }

    private static Sprite LoadArt(string path)
    {
        Texture2D tex = Resources.Load<Texture2D>(path);
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
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
