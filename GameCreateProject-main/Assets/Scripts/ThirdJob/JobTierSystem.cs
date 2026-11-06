using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// 3차 / 4차 전직 (팀 코드 수정 없음, 2차 전직 애드온 위에서 동작)
//
// Player 오브젝트에 붙이면:
//   2차 전직 후 Lv70 → 게임 정지 + 3차 선택 창
//     · 힘 > 스킬 피해력 → 그림자 궁수만 선택 가능 / 반대 → 폭풍 궁수만 / 같으면 둘 다
//     · [나중에]를 누르면 화면 왼쪽 위 버튼으로 다시 열기
//   3차 후 Lv120 → "4차 전직" 창 (같은 갈래 이어감, 새 스킬 2개 소개)
//
// 전직하면
//   3차 : 2차 스킬(ISecondJobSkill)과 기본 대쉬(PlayerDash)를 끄고, 고른 갈래의 3차 스킬을 켬
//   4차 : 같은 갈래의 4차 스킬(Ctrl+X 어센트 / Ctrl+C 오리진)을 추가로 켬
//
// 기능 전체 제거 : Player에서 이 컴포넌트를 Remove → ThirdJob / FourthJob 폴더 삭제
// 4차만 제거     : Player에서 4차 스킬 컴포넌트 Remove → FourthJob 폴더 삭제 (4차 창이 안 뜸)
//
// 테스트 (에디터에서만) : F9 = 3차 창 바로 열기 / F10 = 4차 창 바로 열기 (레벨 무시)
// ─────────────────────────────────────────────────────────────────────────────
[DisallowMultipleComponent]
public class JobTierSystem : MonoBehaviour
{
    [Header("전직 레벨")]
    [SerializeField, Min(2)] private int thirdLevel = 70;
    [SerializeField, Min(3)] private int fourthLevel = 120;

    [Header("직업 이름 (Tab 창에 표시)")]
    [SerializeField] private string shadowName = "그림자 궁수";
    [SerializeField] private string stormName = "폭풍 궁수";

    [Header("선택 창 그림 (비우면 Resources/JobArt 에서 자동으로 불러옴)")]
    [SerializeField] private Sprite shadowArt;
    [SerializeField] private Sprite stormArt;

    [Header("전직 순간 실행할 것")]
    [SerializeField] private UnityEvent onThirdAdvanced = new UnityEvent();
    [SerializeField] private UnityEvent onFourthAdvanced = new UnityEvent();

    [Tooltip("비워두면 맑은 고딕 등 PC 기본 한글 폰트")]
    [SerializeField] private Font uiFont;

    public static JobTierSystem Instance { get; private set; }

    public int Tier { get; private set; } = 2;           // 2 = 아직 3차 전 / 3 / 4
    public ArcherBranch Branch { get; private set; }
    public bool IsChoosing { get; private set; }

    private PlayerProgression progression;
    private PlayerHealth health;
    private readonly List<MonoBehaviour> secondSkills = new List<MonoBehaviour>();
    private readonly List<MonoBehaviour> thirdSkills = new List<MonoBehaviour>();
    private readonly List<MonoBehaviour> fourthSkills = new List<MonoBehaviour>();
    private MonoBehaviour baseDash;

    private float previousTimeScale = 1f;
    private bool thirdPrompted, fourthPrompted, deferred, deathHandled;
    private int selected = -1;

    // UI
    private GameObject canvasObject, thirdRoot, fourthRoot, laterButton;
    private Text laterLabel;
    private Font runtimeFont;
    private Image[] cardFrames = new Image[2];
    private GameObject[] cardLocks = new GameObject[2];
    private Text[] cardStats = new Text[2];
    private Button[] cardButtons = new Button[2];
    private Button confirmButton;
    private Text confirmLabel;
    private Text fourthTitle;
    private bool framed; // 도트 테두리(나무/돌)를 쓰는지
    private Button thirdLater;
    private RectTransform fourthList;

    private static readonly Color ShadowUI = new Color(0.78f, 0.55f, 1f);
    private static readonly Color StormUI = new Color(0.5f, 0.92f, 0.82f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("JobTierSystem은 하나만 두세요.", this);
            enabled = false;
            return;
        }
        Instance = this;
        progression = GetComponent<PlayerProgression>();
        health = GetComponent<PlayerHealth>();
        CollectSkills();
        SetEnabled(thirdSkills, false);
        SetEnabled(fourthSkills, false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SecondJobSystem.SupersededByHigherJob = false;
        if (canvasObject != null) Destroy(canvasObject);
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    private void CollectSkills()
    {
        secondSkills.Clear(); thirdSkills.Clear(); fourthSkills.Clear();
        foreach (MonoBehaviour b in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b is ISecondJobSkill) secondSkills.Add(b);
            if (b is IThirdJobSkill) thirdSkills.Add(b);
            if (b is IFourthJobSkill) fourthSkills.Add(b);
            if (b is PlayerDash) baseDash = b;
        }
    }

    private bool SecondDone => SecondJobSystem.Instance != null && SecondJobSystem.Instance.HasAdvanced;
    private int Level => progression != null ? progression.Level : 0;

    private void Update()
    {
        if (progression == null) progression = PlayerProgression.Instance;
        bool dead = health != null && health.IsDead;

        if (dead)
        {
            if (IsChoosing) Close(false);
            ShowLater(false);
            if (!deathHandled && Tier >= 3)
            {
                deathHandled = true;
                SetEnabled(thirdSkills, false);
                SetEnabled(fourthSkills, false);
            }
            return;
        }

#if UNITY_EDITOR
        if (!IsChoosing && Time.timeScale > 0f)
        {
            if (Input.GetKeyDown(KeyCode.F9) && Tier == 2) OpenThird();
            if (Input.GetKeyDown(KeyCode.F10) && Tier == 3) OpenFourth();
            if (Input.GetKeyDown(KeyCode.F10) && Tier == 2) { Advance3(PreferredBranch()); OpenFourth(); }
        }
#endif

        if (Tier >= 3) EnforceSkills();

        if (IsChoosing) return;
        if (Tier == 2 && !thirdPrompted && CanThird()) { thirdPrompted = true; OpenThird(); }
        else if (Tier == 3 && !fourthPrompted && CanFourth()) { fourthPrompted = true; OpenFourth(); }

        bool laterAvailable = deferred && ((Tier == 2 && CanThird()) || (Tier == 3 && CanFourth()));
        ShowLater(laterAvailable);
    }

    private bool CanThird() => SecondDone && Level >= thirdLevel && Time.timeScale > 0f;
    private bool CanFourth() => Tier == 3 && Level >= fourthLevel && HasFourthSkills() && Time.timeScale > 0f;

    private bool HasFourthSkills()
    {
        foreach (MonoBehaviour b in fourthSkills)
            if (b != null && ((IFourthJobSkill)b).Branch == Branch) return true;
        return false;
    }

    // 다른 스크립트가 스킬을 다시 켜도 현재 전직 상태를 유지
    private void EnforceSkills()
    {
        SecondJobSystem.SupersededByHigherJob = true;
        SetEnabled(secondSkills, false);
        if (baseDash != null && baseDash.enabled) baseDash.enabled = false;
        foreach (MonoBehaviour b in thirdSkills)
            if (b != null) Set(b, ((IThirdJobSkill)b).Branch == Branch);
        foreach (MonoBehaviour b in fourthSkills)
            if (b != null) Set(b, Tier >= 4 && ((IFourthJobSkill)b).Branch == Branch);
    }

    private static void Set(MonoBehaviour b, bool on) { if (b.enabled != on) b.enabled = on; }

    private static void SetEnabled(List<MonoBehaviour> list, bool on)
    {
        foreach (MonoBehaviour b in list) if (b != null) Set(b, on);
    }

    // ───────── 스탯으로 갈래 판정 ─────────
    private int Str => PlayerStats.Instance != null ? PlayerStats.Instance.Get("STR") : 0;
    private int Skl => PlayerStats.Instance != null ? PlayerStats.Instance.Get("SKL") : 0;

    private bool Allowed(ArcherBranch b)
    {
        if (Str == Skl) return true;
        return b == ArcherBranch.Shadow ? Str > Skl : Skl > Str;
    }

    private ArcherBranch PreferredBranch() => Skl > Str ? ArcherBranch.Storm : ArcherBranch.Shadow;

    // ───────── 전직 처리 ─────────
    private void Advance3(ArcherBranch branch)
    {
        Branch = branch;
        Tier = 3;
        deferred = false;
        CollectSkills();
        EnforceSkills();
        CharacterWindowUI.JobNameOverride = branch == ArcherBranch.Shadow ? shadowName : stormName;
        BroadcastMessage("OnThirdJobAdvanced", SendMessageOptions.DontRequireReceiver);
        onThirdAdvanced?.Invoke();
    }

    private void Advance4()
    {
        Tier = 4;
        deferred = false;
        CollectSkills();
        EnforceSkills();
        CharacterWindowUI.JobNameOverride = (Branch == ArcherBranch.Shadow ? shadowName : stormName) + " (4차)";
        BroadcastMessage("OnFourthJobAdvanced", SendMessageOptions.DontRequireReceiver);
        onFourthAdvanced?.Invoke();
    }

    // ───────── 창 열고 닫기 ─────────
    private void Begin()
    {
        IsChoosing = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (canvasObject == null) BuildUI();
        ShowLater(false);
    }

    private void Close(bool resume)
    {
        IsChoosing = false;
        if (thirdRoot != null) thirdRoot.SetActive(false);
        if (fourthRoot != null) fourthRoot.SetActive(false);
        if (resume) Time.timeScale = previousTimeScale;
    }

    private void Later()
    {
        deferred = true;
        Close(true);
    }

    private void OnLaterButton()
    {
        if (IsChoosing) return;
        if (Tier == 2 && CanThird()) OpenThird();
        else if (Tier == 3 && CanFourth()) OpenFourth();
    }

    private void ShowLater(bool show)
    {
        if (laterButton == null) return;
        if (laterButton.activeSelf != show) laterButton.SetActive(show);
        if (show && laterLabel != null) laterLabel.text = Tier == 2 ? "3차 전직 하기" : "4차 전직 하기";
    }

    private void OpenThird()
    {
        Begin();
        thirdRoot.SetActive(true);
        fourthRoot.SetActive(false);
        selected = -1;
        bool shadowOk = Allowed(ArcherBranch.Shadow), stormOk = Allowed(ArcherBranch.Storm);
        if (shadowOk && !stormOk) selected = 0;
        if (stormOk && !shadowOk) selected = 1;
        RefreshThird();
    }

    private void RefreshThird()
    {
        bool[] ok = { Allowed(ArcherBranch.Shadow), Allowed(ArcherBranch.Storm) };
        Color[] col = { ShadowUI, StormUI };
        string versus = Str == 0 && Skl == 0
            ? "힘 0  ·  스킬 피해력 0  (TAB에서 스탯을 찍으면 길이 정해져요)"
            : $"힘 {Str}  ·  스킬 피해력 {Skl}";
        for (int i = 0; i < 2; i++)
        {
            cardLocks[i].SetActive(!ok[i]);
            cardButtons[i].interactable = ok[i];
            if (framed)
            {
                // 도트 테두리: 고른 카드는 밝고 크게, 나머지는 어둡게
                float v = i == selected ? 1f : ok[i] ? 0.62f : 0.45f;
                cardFrames[i].color = new Color(v, v, v, 1f);
                cardFrames[i].rectTransform.localScale = Vector3.one * (i == selected ? 1f : 0.96f);
            }
            else cardFrames[i].color = i == selected ? col[i] : new Color(col[i].r, col[i].g, col[i].b, ok[i] ? 0.35f : 0.15f);
            cardStats[i].text = versus;
        }
        confirmButton.interactable = selected >= 0;
        confirmLabel.text = selected == 0 ? JobWindowSkin.WithRo(shadowName) + " 전직"
                          : selected == 1 ? JobWindowSkin.WithRo(stormName) + " 전직" : "직업을 선택하세요";
        if (framed) JobWindowSkin.FitRow(-440f, 28f, confirmButton, thirdLater); // 글자에 딱 맞게, 가운데 정렬
    }

    private void SelectCard(int i)
    {
        if (!Allowed(i == 0 ? ArcherBranch.Shadow : ArcherBranch.Storm)) return;
        selected = i;
        RefreshThird();
    }

    private void ConfirmThird()
    {
        if (selected < 0) return;
        Close(true);
        Advance3(selected == 0 ? ArcherBranch.Shadow : ArcherBranch.Storm);
    }

    private void OpenFourth()
    {
        if (!HasFourthSkills()) return;
        Begin();
        thirdRoot.SetActive(false);
        fourthRoot.SetActive(true);
        Font font = uiFont != null ? uiFont : runtimeFont;
        Color col = Branch == ArcherBranch.Shadow ? ShadowUI : StormUI;
        fourthTitle.text = $"Lv{fourthLevel} 달성!  {(Branch == ArcherBranch.Shadow ? shadowName : stormName)}의 힘이 깨어납니다";

        foreach (Transform child in fourthList) Destroy(child.gameObject);
        int row = 0;
        foreach (MonoBehaviour b in fourthSkills)
        {
            if (b == null) continue;
            IFourthJobSkill s = (IFourthJobSkill)b;
            if (s.Branch != Branch) continue;
            RectTransform r = Centered("Skill" + row, fourthList, new Vector2(0f, 95f - row * 190f), new Vector2(1000f, 170f));
            AddImage(r, framed ? new Color(0.22f, 0.16f, 0.3f, 0.75f) : new Color(0.16f, 0.12f, 0.22f, 1f), false);
            RectTransform ic = Centered("Icon", r, new Vector2(-405f, 0f), new Vector2(136f, 136f));
            AddImage(Centered("Frame", r, new Vector2(-405f, 0f), new Vector2(146f, 146f)), col, false).transform.SetAsFirstSibling();
            Image icon = AddImage(ic, Color.white, false);
            icon.sprite = s.Icon;
            icon.preserveAspect = true;
            if (icon.sprite == null) icon.color = col;
            Text name = Label("Name", r, font, s.DisplayName, 34, col, new Vector2(110f, 45f), new Vector2(760f, 46f));
            name.alignment = TextAnchor.MiddleLeft;
            Text key = Label("Key", r, font, s.KeyText + "  또는 아이콘 클릭", 22, new Color(1f, 0.85f, 0.45f), new Vector2(110f, 5f), new Vector2(760f, 30f));
            key.alignment = TextAnchor.MiddleLeft;
            Text desc = Label("Desc", r, font, s.Description.Replace("\n", " "), 22, new Color(0.85f, 0.85f, 0.88f), new Vector2(110f, -40f), new Vector2(760f, 50f));
            desc.alignment = TextAnchor.MiddleLeft;
            row++;
        }
    }

    private void ConfirmFourth()
    {
        Close(true);
        Advance4();
    }

    [ContextMenu("테스트: 3차 창 열기")] private void DebugThird() { if (Application.isPlaying && Tier == 2) OpenThird(); }
    [ContextMenu("테스트: 4차 창 열기")] private void DebugFourth() { if (Application.isPlaying && Tier == 3) OpenFourth(); }

    // ───────── UI 만들기 ─────────
    private void BuildUI()
    {
        Font font = uiFont;
        if (font == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            font = runtimeFont;
        }
        if (FindAnyObjectByType<EventSystem>() == null)
            Debug.LogWarning("[JobTierSystem] EventSystem이 없어 버튼이 눌리지 않을 수 있습니다.", this);
        if (shadowArt == null) shadowArt = LoadArt("JobArt/select_shadow");
        if (stormArt == null) stormArt = LoadArt("JobArt/select_storm");

        canvasObject = new GameObject("JobTierCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 112;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasObject.transform;

        framed = JobWindowSkin.HasFrames;

        // ── 3차 선택 창 (위쪽 시간판과 안 겹치게 전체를 조금 아래로)
        RectTransform third = Stretch("Third", root);
        thirdRoot = third.gameObject;
        AddImage(third, new Color(0f, 0f, 0f, 0.78f), true);
        Text t3 = Label("Title", third, font, "3차 전직", 64, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 385f), new Vector2(900f, 84f));
        Text s3 = Label("Sub", third, font, $"Lv{thirdLevel} 달성!  스탯에 따라 갈 수 있는 길이 정해져요", 28, new Color(0.9f, 0.9f, 0.92f), new Vector2(0f, 325f), new Vector2(1200f, 42f));
        JobWindowSkin.Outline(t3, 3f); JobWindowSkin.Outline(s3, 2f);
        MakeCard(0, third, font, new Vector2(-350f, -45f), shadowName, "표식 · 저격 · 분신  /  한 방 위주  ·  AD 계수(힘)", "힘이 스킬 피해력보다 높아야 선택 가능", shadowArt, ShadowUI, new Color(0.12f, 0.07f, 0.18f, 1f));
        MakeCard(1, third, font, new Vector2(350f, -45f), stormName, "바람 · 회오리 · 연사  /  범위 위주  ·  AP 계수(스킬 피해력)", "스킬 피해력이 힘보다 높아야 선택 가능", stormArt, StormUI, new Color(0.07f, 0.15f, 0.17f, 1f));
        RectTransform confirm = Centered("Confirm", third, new Vector2(-215f, -440f), new Vector2(400f, 76f)); // 두 버튼 같은 크기, 가운데 기준 좌우 대칭
        Image ci = AddImage(confirm, new Color(0.55f, 0.38f, 0.8f), true);
        confirmButton = confirm.gameObject.AddComponent<Button>();
        confirmButton.targetGraphic = ci;
        confirmButton.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock cb = confirmButton.colors; cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f); confirmButton.colors = cb;
        confirmButton.onClick.AddListener(ConfirmThird);
        confirmLabel = Label("Label", confirm, font, "", 28, Color.white, Vector2.zero, new Vector2(400f, 76f));
        Button later3 = MakeButton("Later", third, font, "나중에", new Color(0.22f, 0.24f, 0.3f), new Vector2(215f, -440f), new Vector2(400f, 76f), Later);
        thirdLater = later3;
        if (framed) { JobWindowSkin.SkinButton(confirmButton, true); JobWindowSkin.SkinButton(later3, false); }
        thirdRoot.SetActive(false);

        // ── 4차 창
        RectTransform fourth = Stretch("Fourth", root);
        fourthRoot = fourth.gameObject;
        AddImage(fourth, new Color(0f, 0f, 0f, 0.78f), true);
        RectTransform box = Centered("Box", fourth, new Vector2(0f, -20f), new Vector2(1140f, 780f));
        Image boxImage = AddImage(box, new Color(0.1f, 0.07f, 0.15f, 0.97f), true);
        if (framed) JobWindowSkin.Frame(boxImage, JobWindowSkin.Fourth); // 4차 = 금장식 테두리
        Text t4 = Label("Title", box, font, "4차 전직", 66, new Color(1f, 0.85f, 0.45f), new Vector2(0f, 300f), new Vector2(900f, 90f));
        fourthTitle = Label("Sub", box, font, "", 30, new Color(0.9f, 0.9f, 0.92f), new Vector2(0f, 232f), new Vector2(1000f, 44f));
        JobWindowSkin.Outline(t4, 3f); JobWindowSkin.Outline(fourthTitle, 2f);
        fourthList = Centered("List", box, new Vector2(0f, 0f), new Vector2(1000f, 400f));
        Button yes4 = MakeButton("Yes", box, font, "4차 전직하기", new Color(0.55f, 0.38f, 0.8f), new Vector2(-170f, -300f), new Vector2(300f, 76f), ConfirmFourth);
        Button later4 = MakeButton("Later", box, font, "나중에", new Color(0.22f, 0.24f, 0.3f), new Vector2(170f, -300f), new Vector2(300f, 76f), Later);
        if (framed)
        {
            JobWindowSkin.SkinButton(yes4, true); JobWindowSkin.SkinButton(later4, false);
            JobWindowSkin.FitRow(-300f, 28f, yes4, later4);
        }
        fourthRoot.SetActive(false);

        // ── 나중에 → 화면 왼쪽 위 버튼 (2차 버튼 아래)
        RectTransform later = (RectTransform)new GameObject("LaterButton", typeof(RectTransform)).transform;
        later.SetParent(root, false);
        later.anchorMin = later.anchorMax = new Vector2(0f, 1f);
        later.pivot = new Vector2(0f, 1f);
        later.sizeDelta = new Vector2(230f, 44f);
        later.anchoredPosition = new Vector2(32f, -216f);
        Image li = AddImage(later, new Color(0.45f, 0.28f, 0.65f, 0.95f), true);
        Button lb = later.gameObject.AddComponent<Button>();
        lb.targetGraphic = li;
        lb.navigation = new Navigation { mode = Navigation.Mode.None };
        lb.onClick.AddListener(OnLaterButton);
        laterLabel = Label("Label", later, font, "3차 전직 하기", 24, Color.white, Vector2.zero, new Vector2(230f, 44f));
        laterButton = later.gameObject;
        laterButton.SetActive(false);

        // HUD의 CHOOSE JOB 버튼과 같은 모양·같은 자리로 (도트 UI가 있을 때)
        Button hudLater = JobWindowSkin.CloneHudJobButton("ThirdJobLaterButton", "3차 전직 하기", OnLaterButton, out Text hudLabel);
        if (hudLater != null)
        {
            Destroy(laterButton);
            laterButton = hudLater.gameObject;
            laterLabel = hudLabel;
        }
    }

    private void MakeCard(int i, RectTransform parent, Font font, Vector2 pos, string title, string sub, string lockText,
        Sprite art, Color col, Color bg)
    {
        RectTransform card = Centered("Card" + i, parent, pos, new Vector2(660f, 640f));
        cardFrames[i] = AddImage(card, col, true);
        if (framed) JobWindowSkin.Frame(cardFrames[i], i == 0 ? JobWindowSkin.Shadow : JobWindowSkin.Storm);
        cardButtons[i] = card.gameObject.AddComponent<Button>();
        cardButtons[i].targetGraphic = cardFrames[i];
        cardButtons[i].transition = Selectable.Transition.None;
        cardButtons[i].navigation = new Navigation { mode = Navigation.Mode.None };
        int index = i;
        cardButtons[i].onClick.AddListener(() => SelectCard(index));

        // 테두리 안쪽 (도트 테두리면 테두리 두께만큼 들여서, 아니면 6픽셀)
        float inset = framed ? JobWindowSkin.Inset(JobWindowSkin.Shadow) : 6f;
        RectTransform inner = Stretch("Inner", card);
        inner.offsetMin = new Vector2(inset, inset);
        inner.offsetMax = new Vector2(-inset, -inset);
        AddImage(inner, framed ? new Color(bg.r, bg.g, bg.b, 0.55f) : bg, false);
        float w = 660f - inset * 2f;
        float artW = w - 30f, artH = artW * 819f / 1456f;
        RectTransform artRect = Centered("Art", inner, new Vector2(0f, 100f), new Vector2(artW, artH));
        Image a = AddImage(artRect, Color.white, false);
        a.sprite = art;
        a.preserveAspect = true;
        if (art == null) a.color = new Color(col.r, col.g, col.b, 0.15f);

        Text nameText = Label("Name", inner, font, title, 44, col, new Vector2(0f, -105f), new Vector2(w, 56f));
        Text subText = Label("Sub", inner, font, sub, 22, new Color(0.88f, 0.88f, 0.9f), new Vector2(0f, -152f), new Vector2(w - 20f, 32f));
        cardStats[i] = Label("Stat", inner, font, "", 22, new Color(1f, 0.85f, 0.45f), new Vector2(0f, -196f), new Vector2(w - 20f, 32f));
        JobWindowSkin.Outline(nameText, 2.5f);

        RectTransform lockRect = Stretch("Lock", inner);
        AddImage(lockRect, new Color(0f, 0f, 0f, 0.62f), false);
        Label("LockIcon", lockRect, font, "잠김", 44, Color.white, new Vector2(0f, 40f), new Vector2(w, 60f));
        Label("LockText", lockRect, font, lockText, 24, new Color(0.9f, 0.9f, 0.9f), new Vector2(0f, -15f), new Vector2(w, 40f));
        cardLocks[i] = lockRect.gameObject;
    }

    private static Sprite LoadArt(string path)
    {
        Texture2D tex = Resources.Load<Texture2D>(path);
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
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

    private static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    private static RectTransform Centered(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = position;
        return r;
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
