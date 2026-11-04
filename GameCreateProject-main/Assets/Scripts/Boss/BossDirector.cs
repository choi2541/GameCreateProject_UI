using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 보스 등장 관리 + 보스 UI (임시)
//  - 생존 시간 5분 / 10분 / 15분에 보스 등장 (점점 강해짐, 15분 보스는 3페이즈)
//  - 등장 전 경고 + 화면 가장자리 화살표로 어느 방향에서 오는지 표시 (방향은 랜덤)
//  - 보스가 화면 밖에 있으면 화살표가 계속 보스 방향을 가리킴
//  - 화면 위쪽(시간 아래)에 보스 이름 + 체력바 + 페이즈, 기술 사용 시 기술 이름 표시
//  - 처치하면 경험치 + 증강 선택 창
//
// 씬의 BossSystem 오브젝트에 붙습니다. (Tools > Game UI > Setup Boss + Mob HP Bars)
// 빼려면: BossSystem 오브젝트 삭제 + Boss 폴더 삭제. 다른 파일은 이 폴더를 참조하지 않습니다.
[DisallowMultipleComponent]
public class BossDirector : MonoBehaviour
{
    [Serializable]
    public class BossTier
    {
        public string displayName = "보스";
        [Tooltip("등장 시간 (생존 시간, 초)")]
        public float spawnTime = 300f;
        [Min(1)] public int maxHealth = 250;
        [Min(0.1f)] public float moveSpeed = 1.6f;
        [Tooltip("크기 배율 (1 = 1칸)")]
        [Min(0.5f)] public float scale = 2f;
        public Color color = new Color(0.55f, 0.1f, 0.12f);
        [Tooltip("보스 이미지. 비워두면 임시 원")]
        public Sprite sprite;
        [Min(0)] public int contactDamage = 1;
        [Min(0)] public int attackDamage = 2;
        [Tooltip("공격 사이 간격 (초)")]
        [Min(0.3f)] public float attackInterval = 3f;
        [Range(1, 3)] public int phases = 1;
        [Tooltip("페이즈 이름 (1페이즈부터)")]
        public string[] phaseNames = new string[0];
        public BossController.Pattern[] patterns = { BossController.Pattern.Slam, BossController.Pattern.Charge };
        [Min(0)] public int expReward = 10;
        [HideInInspector] public int tierIndex;
    }

    [SerializeField]
    private BossTier[] tiers =
    {
        new BossTier
        {
            displayName = "숲의 파수꾼", spawnTime = 300f, maxHealth = 250, moveSpeed = 1.6f, scale = 2f,
            color = new Color(0.45f, 0.3f, 0.15f), attackInterval = 3f, phases = 1, expReward = 10,
            patterns = new[] { BossController.Pattern.Slam, BossController.Pattern.Charge }
        },
        new BossTier
        {
            displayName = "붉은 송곳니", spawnTime = 600f, maxHealth = 600, moveSpeed = 1.9f, scale = 2.4f,
            color = new Color(0.65f, 0.12f, 0.12f), attackInterval = 2.4f, phases = 1, expReward = 20,
            patterns = new[] { BossController.Pattern.Slam, BossController.Pattern.Charge, BossController.Pattern.Radial }
        },
        new BossTier
        {
            displayName = "타락한 고목왕", spawnTime = 900f, maxHealth = 1400, moveSpeed = 2f, scale = 2.8f,
            color = new Color(0.3f, 0.12f, 0.35f), attackInterval = 2.2f, phases = 3, expReward = 40,
            phaseNames = new[] { "1페이즈", "2페이즈 · 분노", "3페이즈 · 광폭화" },
            patterns = new[] { BossController.Pattern.Slam, BossController.Pattern.Charge }
        },
    };

    [SerializeField, Min(0f)] private float warningSeconds = 3f;
    [SerializeField] private bool offerAugmentOnKill = true;
    [Tooltip("마지막 보스(15분)를 쓰러뜨리면 게임 클리어 (GameClear가 화면 표시)")]
    [SerializeField] private bool lastBossEndsGame = true;

    // 마지막 보스 처치 → GameClear가 받아서 클리어 화면을 띄움
    public event Action FinalBossDefeated;
    [Tooltip("비워두면 맑은 고딕 등 PC 기본 한글 폰트")]
    [SerializeField] private Font uiFont;

    private int nextTier;
    private bool spawning;
    private BossController boss;
    private Vector2 incomingDirection;
    private bool showIncoming;

    // UI
    private GameObject canvasObject;
    private RectTransform canvasRect;
    private Font runtimeFont;
    private GameObject barRoot;
    private Text nameText;
    private Text phaseText;
    private RectTransform fill;
    private Text calloutText;
    private Text bannerTitle;
    private Text bannerSub;
    private GameObject banner;
    private RectTransform arrow;
    private RectTransform arrowLabel;
    private float calloutTimer;
    private float bannerTimer;

    private void Awake()
    {
        for (int i = 0; i < tiers.Length; i++)
            tiers[i].tierIndex = i;

        BuildUI();
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    private void Update()
    {
        GameSession session = GameSession.Instance;
        if (session != null && !session.IsGameOver && !spawning
            && nextTier < tiers.Length && session.SurvivalTime >= tiers[nextTier].spawnTime)
        {
            StartCoroutine(SpawnRoutine(nextTier));
            nextTier++;
        }
    }

    private void LateUpdate()
    {
        UpdateBar();
        UpdateArrow();
        UpdateTexts();
    }

    // ───────── 테스트 (플레이 중 컴포넌트 ⋮ 메뉴) ─────────
    [ContextMenu("테스트: 5분 보스 지금 소환")] private void Test1() => TestSpawn(0);
    [ContextMenu("테스트: 10분 보스 지금 소환")] private void Test2() => TestSpawn(1);
    [ContextMenu("테스트: 15분 보스(3페이즈) 지금 소환")] private void Test3() => TestSpawn(2);

    private void TestSpawn(int index)
    {
        if (Application.isPlaying && !spawning && index < tiers.Length)
            StartCoroutine(SpawnRoutine(index));
    }

    // ───────── 등장 ─────────

    private IEnumerator SpawnRoutine(int index)
    {
        spawning = true;
        BossTier tier = tiers[index];
        PlayerHealth player = BossUtil.Player();
        Vector2 center = player != null ? (Vector2)player.transform.position : Vector2.zero;

        incomingDirection = SkillUtil.Rotate(Vector2.right, UnityEngine.Random.Range(0f, 360f));
        showIncoming = true;
        ShowBanner("WARNING", $"{tier.displayName} 이(가) {BossUtil.CompassName(incomingDirection)}에서 다가옵니다", warningSeconds + 1f);
        yield return new WaitForSeconds(warningSeconds);

        // 카메라 바로 바깥, 정해진 방향에 생성
        if (player != null) center = player.transform.position;
        Camera cam = GameCamera.Get(); // Tag가 MainCamera가 아니어도 동작
        float distance = cam != null && cam.orthographic
            ? Mathf.Max(cam.orthographicSize * cam.aspect, cam.orthographicSize) + tier.scale + 1f
            : 12f;
        Vector2 position = center + incomingDirection * distance;

        Transform container = SkillUtil.EnemyContainer(null);
        int layer = 0;
        if (container != null && container.childCount > 0)
            layer = container.GetChild(0).gameObject.layer; // 일반 몹과 같은 레이어 (평타가 맞도록)

        // 보스 본체는 적 목록 밖에 둠 (스킬 판정은 판정점이 담당 → 중복 피해 방지)
        GameObject obj = new GameObject($"Boss_{tier.displayName}");
        obj.transform.position = position;

        boss = obj.AddComponent<BossController>();
        boss.Init(tier, tier.sprite, layer, container);
        boss.SkillAnnounced += name => ShowCallout($"【 {name} 】");
        boss.PhaseChanged += (phase, title) =>
        {
            ShowBanner(title, $"{tier.displayName} 의 힘이 강해집니다", 2f);
            ShowCallout(title);
        };
        boss.Died += OnBossDied;

        showIncoming = false;
        spawning = false;
    }

    private void OnBossDied(BossController dead)
    {
        ShowBanner("BOSS DEFEATED", $"{dead.DisplayName} 처치!", 2.5f);
        if (boss == dead) boss = null;

        bool final = lastBossEndsGame && dead.TierIndex == tiers.Length - 1;
        if (final)
        {
            FinalBossDefeated?.Invoke();
            return; // 마지막 보스는 증강 대신 클리어
        }
        if (offerAugmentOnKill)
            AugmentSystem.Offer();
    }

    // ───────── UI ─────────

    private void UpdateBar()
    {
        bool show = boss != null && !boss.IsDead;
        if (barRoot.activeSelf != show)
            barRoot.SetActive(show);
        if (!show)
            return;

        float ratio = Mathf.Clamp01((float)boss.CurrentHp / boss.MaxHp);
        fill.anchorMax = new Vector2(ratio, 1f);
        nameText.text = boss.DisplayName;
        phaseText.text = boss.PhaseCount > 1 ? $"PHASE {boss.Phase} / {boss.PhaseCount}" : "";
        fill.GetComponent<Image>().color = boss.IsTransitioning
            ? Color.Lerp(new Color(1f, 0.85f, 0.4f), Color.white, Mathf.PingPong(Time.time * 4f, 1f))
            : new Color(0.85f, 0.15f, 0.18f);
    }

    private void UpdateArrow()
    {
        Camera cam = GameCamera.Get(); // Tag가 MainCamera가 아니어도 동작
        Vector2 target = Vector2.zero;
        bool show = false;
        PlayerHealth player = BossUtil.Player();

        if (showIncoming && player != null)
        {
            target = (Vector2)player.transform.position + incomingDirection * 50f;
            show = true;
        }
        else if (boss != null && !boss.IsDead)
        {
            target = boss.transform.position;
            show = !SkillUtil.IsInsideCamera(target, -0.5f);
        }

        if (cam == null || !show)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        arrow.gameObject.SetActive(true);
        Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5f;
        Vector2 screenPos = cam.WorldToScreenPoint(target);
        Vector2 dir = (screenPos - screenCenter).normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        // 화면 가장자리 안쪽에 고정
        Vector2 half = canvasRect.rect.size * 0.5f - new Vector2(90f, 90f);
        float t = Mathf.Min(Mathf.Abs(half.x / (dir.x == 0f ? 0.0001f : dir.x)),
                            Mathf.Abs(half.y / (dir.y == 0f ? 0.0001f : dir.y)));
        arrow.anchoredPosition = dir * t;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f);
        arrow.localScale = new Vector3(pulse, pulse, 1f);
        arrowLabel.rotation = Quaternion.identity; // 글자는 항상 똑바로
    }

    private void UpdateTexts()
    {
        if (calloutTimer > 0f)
        {
            calloutTimer -= Time.unscaledDeltaTime;
            SetAlpha(calloutText, Mathf.Clamp01(calloutTimer / 0.4f));
        }
        else if (calloutText.gameObject.activeSelf)
        {
            calloutText.gameObject.SetActive(false);
        }

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.unscaledDeltaTime;
            float blink = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
            SetAlpha(bannerTitle, Mathf.Clamp01(bannerTimer / 0.5f) * blink);
            SetAlpha(bannerSub, Mathf.Clamp01(bannerTimer / 0.5f));
        }
        else if (banner.activeSelf)
        {
            banner.SetActive(false);
        }
    }

    private void ShowCallout(string text)
    {
        calloutText.text = text;
        calloutText.gameObject.SetActive(true);
        calloutTimer = 1.3f;
    }

    private void ShowBanner(string title, string sub, float seconds)
    {
        bannerTitle.text = title;
        bannerSub.text = sub;
        banner.SetActive(true);
        bannerTimer = seconds;
    }

    private static void SetAlpha(Text text, float a)
    {
        Color c = text.color;
        c.a = a;
        text.color = c;
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

        canvasObject = new GameObject("BossCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // HUD(10) 아래 → 일시정지/Tab 창이 보스 UI를 덮음
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvasObject.transform;

        // 보스 체력바 (시간 아래쪽)
        RectTransform bar = Rect("BossBar", canvasRect, new Vector2(0.5f, 1f), new Vector2(0f, -205f), new Vector2(900f, 90f));
        barRoot = bar.gameObject;
        nameText = Label("Name", bar, font, "BOSS", 34, new Color(1f, 0.85f, 0.6f), new Vector2(0f, 26f), new Vector2(900f, 44f));
        phaseText = Label("Phase", bar, font, "", 22, new Color(1f, 0.6f, 0.5f), new Vector2(360f, 26f), new Vector2(200f, 40f));
        phaseText.alignment = TextAnchor.MiddleRight;
        RectTransform track = Rect("Track", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f), new Vector2(900f, 26f));
        AddImage(track, new Color(0.05f, 0.05f, 0.07f, 0.9f));
        fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
        fill.SetParent(track, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        AddImage(fill, new Color(0.85f, 0.15f, 0.18f));
        barRoot.SetActive(false);

        // 기술 이름 (체력바 아래)
        calloutText = Label("Callout", canvasRect, font, "", 32, new Color(1f, 0.55f, 0.3f),
            new Vector2(0f, -270f), new Vector2(900f, 50f), new Vector2(0.5f, 1f));
        calloutText.gameObject.SetActive(false);

        // 경고 배너 (화면 가운데 위쪽)
        RectTransform bannerRect = Rect("Banner", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1400f, 140f));
        banner = bannerRect.gameObject;
        bannerTitle = Label("Title", bannerRect, font, "WARNING", 72, new Color(1f, 0.25f, 0.2f), new Vector2(0f, 24f), new Vector2(1400f, 90f));
        bannerSub = Label("Sub", bannerRect, font, "", 30, Color.white, new Vector2(0f, -44f), new Vector2(1400f, 44f));
        banner.SetActive(false);

        // 화면 가장자리 화살표
        arrow = Rect("Arrow", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
        Image arrowImage = AddImage(arrow, new Color(1f, 0.25f, 0.2f, 0.95f));
        arrowImage.sprite = BossUtil.Triangle();
        arrowLabel = Label("Label", arrow, font, "BOSS", 22, Color.white, new Vector2(0f, -52f), new Vector2(120f, 30f)).rectTransform;
        arrow.gameObject.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text Label(string name, Transform parent, Font font, string value, int size, Color color,
        Vector2 position, Vector2 boxSize, Vector2? anchor = null)
    {
        RectTransform rect = Rect(name, parent, anchor ?? new Vector2(0.5f, 0.5f), position, boxSize);
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
        rect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }
}
