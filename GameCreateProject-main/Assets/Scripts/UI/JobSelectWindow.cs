using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 1차 전직 선택 창 (화면 가운데).
// 한 캐릭터를 가운데로 반 가른 그림 한 장: 왼쪽 절반 = 전사, 오른쪽 절반 = 궁수.
// 반쪽을 누르면 선택(금테), 확인을 누르면 전직. 팀원 창(LevelAndJobUI)의 선택 창은 숨기고 이 창을 대신 띄움.
// 전직 처리 자체는 팀원 코드 PlayerProgression.ConfirmJob을 그대로 부름.
// 그림·나무 틀은 Tools > Game UI > Pixel UI - Apply가 자동으로 넣어 줌.
[DisallowMultipleComponent]
public class JobSelectWindow : MonoBehaviour
{
    [Header("그림 (Pixel UI - Apply가 자동으로 넣음)")]
    [SerializeField] private Sprite splitArt;          // 반반 그림 (왼쪽 전사 / 오른쪽 궁수)
    [SerializeField] private Sprite panel;
    [SerializeField] private Sprite innerFrame;
    [SerializeField] private Sprite titleBoard;
    [SerializeField] private Sprite buttonGreen;
    [SerializeField] private Sprite buttonRed;
    [SerializeField] private Sprite buttonDisabled;
    [SerializeField] private float panelPixelsPerUnit = 3f;
    [SerializeField] private float framePixelsPerUnit = 5f;

    [Header("글자")]
    [SerializeField] private string title = "직업 선택";
    [SerializeField] private string warriorName = "전사";
    [SerializeField] private string archerName = "궁수";

    [Header("설정")]
    [Tooltip("전사가 완성되면 켜세요. 꺼져 있으면 전사 쪽은 어둡게 '준비 중'으로 잠김")]
    [SerializeField] private bool warriorUnlocked = false;
    [SerializeField] private Vector2 windowSize = new Vector2(760f, 900f);
    [SerializeField, Min(200f)] private float artSize = 600f;
    [SerializeField] private Color selectColor = new Color(1f, 0.82f, 0.36f, 1f);

    private PlayerProgression progression;
    private GameObject root;
    private Half warrior, archer;
    private PlayerJob selected = PlayerJob.None;
    private Button confirm;
    private Image confirmArt;
    private Text confirmLabel, message;
    private GameObject oldOverlay;
    private bool wasOpen;
    private float nextOverlaySearch;
    private Font font;

    private static readonly Color Light = new Color(1f, 0.93f, 0.75f), Dim = new Color(0.62f, 0.62f, 0.62f);

    private void Start()
    {
        progression = PlayerProgression.Instance != null ? PlayerProgression.Instance : FindAnyObjectByType<PlayerProgression>();
        if (progression == null) { enabled = false; return; }
        Build();
        progression.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (progression != null) progression.Changed -= Refresh;
    }

    // ───────── 열기 / 닫기 ─────────
    private void Refresh()
    {
        if (root == null) return;
        bool open = progression.IsChoosingJob;
        if (open && !wasOpen) Select(PlayerJob.None);
        root.SetActive(open);
        wasOpen = open;
    }

    private void LateUpdate()
    {
        // 팀원 창의 옛날 선택 창은 계속 숨김 (이 창이 대신 뜸)
        if (oldOverlay == null && Time.unscaledTime >= nextOverlaySearch)
        {
            nextOverlaySearch = Time.unscaledTime + 0.5f; // 찾는 건 가끔만
            GameObject canvas = GameObject.Find("LevelAndJobCanvas");
            Transform t = canvas != null ? canvas.transform.Find("JobSelectionOverlay") : null;
            if (t != null) oldOverlay = t.gameObject;
        }
        if (oldOverlay != null && oldOverlay.activeSelf) oldOverlay.SetActive(false);

        if (root == null || !root.activeSelf) return;
        if (UIKeyInput.EscapeDown) progression.CloseJobSelection();
        else if (ConfirmDown() && selected != PlayerJob.None) Confirm();
    }

    private void Select(PlayerJob job)
    {
        if (job == PlayerJob.Warrior && !warriorUnlocked) return;
        selected = job;
        warrior.SetSelected(job == PlayerJob.Warrior);
        archer.SetSelected(job == PlayerJob.Archer);
        bool has = job != PlayerJob.None;
        confirm.interactable = has;
        confirmArt.sprite = has || buttonDisabled == null ? buttonGreen : buttonDisabled;
        confirmLabel.color = has ? Color.white : Dim;
        message.text = has ? "선택한 직업: " + (job == PlayerJob.Warrior ? warriorName : archerName) : "직업을 선택하세요.";
    }

    private void Confirm()
    {
        if (selected == PlayerJob.None) return;
        progression.ConfirmJob(selected);
    }

    private static bool ConfirmDown()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    // ───────── 만들기 ─────────
    private void Build()
    {
        Text any = FindAnyObjectByType<Text>();
        font = any != null ? any.font : Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);

        // 게임 UI 캔버스(1920x1080 기준) 안에 들어가고, 정렬 순서만 맨 위로
        root = new GameObject("JobSelectWindow", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 110; // 팀원 선택 창(100)보다 위
        if (GetComponentInParent<Canvas>() == null)
        {
            // 캔버스 밖에 붙었으면 혼자서도 보이게
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        RectTransform r = (RectTransform)root.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;

        // 어두운 배경 (뒤 게임 화면 클릭 막음)
        Img("Dim", r, null, new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).raycastTarget = true;

        // 나무 창
        Vector2 c = new Vector2(0.5f, 0.5f);
        Image win = Img("Window", r, panel, panel != null ? Color.white : new Color(0.3f, 0.22f, 0.14f), c, c, new Vector2(0f, -20f), windowSize);
        if (panel != null) { win.type = Image.Type.Sliced; win.pixelsPerUnitMultiplier = panelPixelsPerUnit; }
        RectTransform w = win.rectTransform;

        // 제목 판
        Vector2 tc = new Vector2(0.5f, 1f);
        float titleW = 440f, titleH = titleBoard != null ? titleW * titleBoard.rect.height / titleBoard.rect.width : 90f;
        Image tb = Img("TitleBoard", w, titleBoard, titleBoard != null ? Color.white : new Color(0.45f, 0.3f, 0.16f), tc, tc, new Vector2(0f, titleH * 0.38f), new Vector2(titleW, titleH));
        Label("Title", tb.rectTransform, title, 34, Light, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // 그림 틀 + 반반 그림
        float artTop = -120f;
        Image fr = Img("ArtFrame", w, innerFrame, innerFrame != null ? Color.white : new Color(0.2f, 0.15f, 0.1f), tc, tc, new Vector2(0f, artTop + 30f), new Vector2(artSize + 60f, artSize + 60f));
        if (innerFrame != null) { fr.type = Image.Type.Sliced; fr.pixelsPerUnitMultiplier = framePixelsPerUnit; }
        RectTransform art = Rect("Art", w, tc, tc, new Vector2(0f, artTop), new Vector2(artSize, artSize));
        warrior = MakeHalf(art, PlayerJob.Warrior, warriorName, 0f, !warriorUnlocked);
        archer = MakeHalf(art, PlayerJob.Archer, archerName, 0.5f, false);
        Img("Seam", art, null, new Color(1f, 0.93f, 0.67f, 0.8f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(2f, 0f));

        // 안내 글
        float below = artTop - artSize;
        message = Label("Message", w, "직업을 선택하세요.", 24, new Color(0.88f, 0.8f, 0.65f), tc, tc, new Vector2(0f, below - 26f), new Vector2(artSize, 40f));

        // 확인 / 닫기
        Vector2 bs = new Vector2(230f, 66f);
        confirm = MakeButton("Confirm", w, "확인", buttonGreen, new Vector2(-bs.x * 0.5f - 20f, below - 76f), bs, out confirmArt, out confirmLabel);
        confirm.onClick.AddListener(Confirm);
        Button close = MakeButton("Close", w, "닫기", buttonRed, new Vector2(bs.x * 0.5f + 20f, below - 76f), bs, out _, out _);
        close.onClick.AddListener(progression.CloseJobSelection);

        root.SetActive(false);
    }

    private Half MakeHalf(RectTransform art, PlayerJob job, string name, float u, bool locked)
    {
        RectTransform rt = Rect(name, art, new Vector2(u, 0f), new Vector2(u + 0.5f, 1f), Vector2.zero, Vector2.zero);
        rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
        RawImage pic = rt.gameObject.AddComponent<RawImage>();
        if (splitArt != null)
        {
            Texture tex = splitArt.texture;
            Rect sr = splitArt.textureRect;
            pic.texture = tex;
            pic.uvRect = new Rect(sr.x / tex.width + u * sr.width / tex.width, sr.y / tex.height, sr.width * 0.5f / tex.width, sr.height / tex.height);
        }
        else pic.color = job == PlayerJob.Warrior ? new Color(0.55f, 0.38f, 0.2f) : new Color(0.16f, 0.3f, 0.38f);

        // 아래 이름 띠
        Img("NameStrip", rt, null, new Color(0.04f, 0.03f, 0.02f, 0.62f), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 80f));
        Text label = Label("Name", rt, name, 34, locked ? Dim : Color.white, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(0f, 60f));

        // 선택 금테 (네 변)
        GameObject sel = new GameObject("Selected", typeof(RectTransform));
        sel.transform.SetParent(rt, false);
        RectTransform s = (RectTransform)sel.transform;
        s.anchorMin = Vector2.zero; s.anchorMax = Vector2.one; s.offsetMin = s.offsetMax = Vector2.zero;
        const float t = 5f;
        Img("Top", s, null, selectColor, new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, t));
        Img("Bottom", s, null, selectColor, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t));
        Img("Left", s, null, selectColor, Vector2.zero, new Vector2(0f, 1f), new Vector2(t * 0.5f, 0f), new Vector2(t, 0f));
        Img("Right", s, null, selectColor, new Vector2(1f, 0f), Vector2.one, new Vector2(-t * 0.5f, 0f), new Vector2(t, 0f));
        sel.SetActive(false);

        // 잠김 (준비 중)
        if (locked)
        {
            pic.color = new Color(0.4f, 0.4f, 0.43f, 1f);
            Image tag = Img("Locked", rt, null, new Color(0.08f, 0.07f, 0.06f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 52f));
            Label("Text", tag.rectTransform, "준비 중", 26, new Color(0.82f, 0.8f, 0.76f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        Half h = rt.gameObject.AddComponent<Half>();
        h.Init(this, job, pic, label, sel, locked);
        return h;
    }

    private Button MakeButton(string name, RectTransform parent, string text, Sprite sprite, Vector2 pos, Vector2 size, out Image art, out Text label)
    {
        Vector2 tc = new Vector2(0.5f, 1f);
        art = Img(name, parent, sprite, sprite != null ? Color.white : new Color(0.3f, 0.4f, 0.25f), tc, tc, pos, size);
        art.raycastTarget = true;
        if (sprite != null && sprite.border != Vector4.zero) { art.type = Image.Type.Sliced; art.pixelsPerUnitMultiplier = sprite.rect.height / size.y; }
        Button b = art.gameObject.AddComponent<Button>();
        b.targetGraphic = art;
        label = Label("Label", art.rectTransform, text, 28, Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return b;
    }

    // ───────── 작은 도우미 ─────────
    private static RectTransform Rect(string name, RectTransform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = new Vector2(0.5f, aMin.y == aMax.y ? aMin.y : 0.5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    private static Image Img(string name, RectTransform parent, Sprite sprite, Color color, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        Image i = Rect(name, parent, aMin, aMax, pos, size).gameObject.AddComponent<Image>();
        i.sprite = sprite; i.color = color; i.raycastTarget = false;
        return i;
    }

    private Text Label(string name, RectTransform parent, string value, int size, Color color, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta)
    {
        Text t = Rect(name, parent, aMin, aMax, pos, sizeDelta).gameObject.AddComponent<Text>();
        t.font = font; t.text = value; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.color = color;
        t.alignment = TextAnchor.MiddleCenter; t.alignByGeometry = true; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        Outline o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.12f, 0.08f, 0.04f, 1f); o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    // 반쪽 그림 하나 (마우스 올리면 밝게, 누르면 선택)
    private class Half : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private JobSelectWindow owner;
        private PlayerJob job;
        private RawImage pic;
        private Text label;
        private GameObject frame;
        private bool locked, hover, selected;

        public void Init(JobSelectWindow o, PlayerJob j, RawImage p, Text l, GameObject f, bool lk)
        {
            owner = o; job = j; pic = p; label = l; frame = f; locked = lk;
            pic.raycastTarget = true;
            Paint();
        }

        public void SetSelected(bool on) { selected = on; Paint(); }
        public void OnPointerEnter(PointerEventData e) { hover = true; Paint(); }
        public void OnPointerExit(PointerEventData e) { hover = false; Paint(); }
        public void OnPointerClick(PointerEventData e) { if (!locked) owner.Select(job); }

        private void Paint()
        {
            if (locked) return; // 잠긴 쪽은 늘 어둡게
            float b = selected ? 1f : hover ? 0.95f : 0.82f;
            pic.color = new Color(b, b, b, 1f);
            frame.SetActive(selected);
            label.color = selected ? owner.selectColor : Color.white;
        }
    }
}
