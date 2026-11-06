using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// ───────────────────────────────────────────────────────────────
// [임시 치트] 테스트용. 이 파일 하나만 지우면 치트가 통째로 사라짐 (씬·다른 코드 수정 없음).
//   화면 오른쪽 "DEV" 탭 (또는 F1) → 레벨업 / 스탯 +999 / 전직 / 보스 소환 / 시간 무한 / 초기화
// 팀원 코드는 고치지 않고, 이미 있는 기능을 불러서 씀.
// ───────────────────────────────────────────────────────────────
public class DevCheats : MonoBehaviour
{
    private const bool Enabled = true; // false로 바꾸면 파일을 지우지 않고도 끌 수 있음

    private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private GameObject panel;
    private Text status, timeButtonLabel;
    private Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Enabled) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
        Create();
    }

    private static void OnLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Create();

    private static void Create()
    {
        if (FindAnyObjectByType<DevCheats>() != null || FindAnyObjectByType<PlayerProgression>() == null) return;
        new GameObject("DevCheats").AddComponent<DevCheats>();
    }

    // ───────── 화면 ─────────
    private void Start()
    {
        Text any = FindAnyObjectByType<Text>();
        font = any != null ? any.font : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);

        var canvasGo = new GameObject("DevCheatsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasGo.transform;

        // 열고 닫는 탭 (오른쪽 가운데)
        Button tab = MakeButton(root, "DEV", new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(56f, 120f), Toggle, new Color(0.15f, 0.12f, 0.1f, 0.85f));
        tab.GetComponentInChildren<Text>().fontSize = 18;

        // 패널
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform p = (RectTransform)panel.transform;
        p.SetParent(root, false);
        p.anchorMin = p.anchorMax = new Vector2(1f, 0.5f); p.pivot = new Vector2(1f, 0.5f);
        p.anchoredPosition = new Vector2(-64f, 0f); p.sizeDelta = new Vector2(300f, 0f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.92f);
        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(12, 12, 12, 12); v.spacing = 6f; v.childControlHeight = v.childControlWidth = true; v.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Header("치트 (테스트용)");
        Row("레벨 +1", () => AddLevels(1), "+10", () => AddLevels(10), "+100", () => AddLevels(100));
        Row("스탯 +999", () => PlayerStats.Ensure()?.AddBonusPoints(999));
        Header("전직");
        Row("1차 궁수", Job1, "2차", Job2);
        Row("3차 폭풍", () => Job3(ArcherBranch.Storm), "3차 그림자", () => Job3(ArcherBranch.Shadow));
        Row("4차", Job4);
        Header("보스");
        Row("5분 보스", () => Boss(0), "10분", () => Boss(1), "15분", () => Boss(2));
        Header("기타");
        Button timeBtn = Row("시간 무한: 끔", ToggleEndless)[0];
        timeButtonLabel = timeBtn.GetComponentInChildren<Text>();
        Row("초기화 (처음부터)", () => GameSession.Instance?.Restart());
        status = Label("", 16, new Color(1f, 0.85f, 0.5f));

        panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) Toggle();
    }

    private void Toggle() => panel.SetActive(!panel.activeSelf);
    private void Say(string text) { if (status != null) status.text = text; }

    // ───────── 기능 ─────────
    private static PlayerProgression Prog => PlayerProgression.Instance != null ? PlayerProgression.Instance : FindAnyObjectByType<PlayerProgression>();

    private void AddLevels(int count)
    {
        PlayerProgression p = Prog;
        if (p == null) return;
        int target = p.Level + count;
        int guard = count * 4 + 10;
        while (p.Level < target && guard-- > 0)
            p.AddExperience(Mathf.Max(1, p.RequiredExperience - p.Experience));
        Say("Lv." + p.Level);
    }

    private void ReachLevel(int level)
    {
        PlayerProgression p = Prog;
        if (p != null && p.Level < level) AddLevels(level - p.Level);
    }

    private void Job1()
    {
        PlayerProgression p = Prog;
        if (p == null) return;
        ReachLevel(2);
        if (p.CurrentJob == PlayerJob.None) { p.OpenJobSelection(); p.ConfirmJob(PlayerJob.Archer); }
        Say("1차: " + p.CurrentJob);
    }

    private void Job2()
    {
        Job1();
        SecondJobSystem s = SecondJobSystem.Instance;
        if (s == null) { Say("2차 시스템이 씬에 없어요 (Setup Archer Skills)"); return; }
        int need = s.GetType().GetField("requiredLevel", Any) is FieldInfo f ? (int)f.GetValue(s) : 30;
        ReachLevel(need);
        if (!s.HasAdvanced)
        {
            Call(s, "BeginChoosing");
            Call(s, "Select", 0);
            Call(s, "Confirm");
        }
        Say(s.HasAdvanced ? "2차 완료" : "2차 실패");
    }

    private void Job3(ArcherBranch branch)
    {
        JobTierSystem t = JobTierSystem.Instance;
        if (t == null) { Say("3·4차 시스템이 씬에 없어요 (Setup 3rd + 4th Job)"); return; }
        if (t.Tier >= 3) { Say("이미 " + t.Tier + "차"); return; }
        Job2();
        ReachLevel(GetInt(t, "thirdLevel", 70));
        Call(t, "Advance3", branch);
        Say("3차: " + branch);
    }

    private void Job4()
    {
        JobTierSystem t = JobTierSystem.Instance;
        if (t == null) { Say("3·4차 시스템이 씬에 없어요"); return; }
        if (t.Tier < 3) Job3(ArcherBranch.Storm);
        ReachLevel(GetInt(t, "fourthLevel", 120));
        if (t.Tier == 3) Call(t, "Advance4");
        Say("4차: " + t.Branch);
    }

    private void Boss(int index)
    {
        BossDirector d = FindAnyObjectByType<BossDirector>();
        if (d == null) { Say("보스 시스템이 씬에 없어요 (Setup Boss)"); return; }
        Call(d, "TestSpawn", index);
        Say("보스 " + (index + 1) + " 소환");
    }

    private bool endless;
    private void ToggleEndless()
    {
        BossDirector d = FindAnyObjectByType<BossDirector>();
        FieldInfo f = d != null ? d.GetType().GetField("lastBossEndsGame", Any) : null;
        if (f == null) { Say("보스 시스템이 없어서 원래도 시간 제한 없음"); return; }
        endless = !endless;
        f.SetValue(d, !endless);   // 15분 보스를 잡아도 게임이 끝나지 않음
        timeButtonLabel.text = endless ? "시간 무한: 켬" : "시간 무한: 끔";
        Say(endless ? "15분 보스를 잡아도 계속 플레이" : "15분 보스를 잡으면 클리어");
    }

    private static void Call(object target, string method, params object[] args)
    {
        foreach (MethodInfo m in target.GetType().GetMethods(Any))
            if (m.Name == method && m.GetParameters().Length == args.Length) { m.Invoke(target, args); return; }
        Debug.LogWarning("[DevCheats] " + target.GetType().Name + "." + method + " 를 못 찾았어요");
    }

    private static int GetInt(object target, string field, int fallback)
    {
        FieldInfo f = target.GetType().GetField(field, Any);
        return f != null && f.FieldType == typeof(int) ? (int)f.GetValue(target) : fallback;
    }

    // ───────── 작은 UI 도우미 ─────────
    private Button[] Row(string l1, Action a1, string l2 = null, Action a2 = null, string l3 = null, Action a3 = null)
    {
        var items = new System.Collections.Generic.List<(string label, Action action)> { (l1, a1) };
        if (l2 != null) items.Add((l2, a2));
        if (l3 != null) items.Add((l3, a3));
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(panel.transform, false);
        var h = row.GetComponent<HorizontalLayoutGroup>(); h.spacing = 6f; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = true;
        row.GetComponent<LayoutElement>().preferredHeight = 40f;
        var buttons = new Button[items.Count];
        for (int i = 0; i < items.Count; i++)
            buttons[i] = MakeButton((RectTransform)row.transform, items[i].label, Vector2.zero, Vector2.zero, Vector2.zero, items[i].action, new Color(0.32f, 0.24f, 0.15f, 1f));
        return buttons;
    }

    private void Header(string text) => Label(text, 18, new Color(1f, 0.93f, 0.75f));

    private Text Label(string text, int size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(panel.transform, false);
        go.GetComponent<LayoutElement>().preferredHeight = size + 10f;
        Text t = go.GetComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleLeft;
        return t;
    }

    private Button MakeButton(RectTransform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, Color color)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        if (size != Vector2.zero) { r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(1f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size; }
        go.GetComponent<Image>().color = color;
        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
        RectTransform lr = (RectTransform)label.transform;
        lr.SetParent(r, false); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        Text t = label.GetComponent<Text>();
        t.font = font; t.text = text; t.fontSize = 17; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
        return go.GetComponent<Button>();
    }
}
