using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// 시간이 지날수록(기본) 또는 플레이어 레벨이 오를수록 적이 강해짐 + 빨간 동그라미 대신 슬라임/버섯 모습 (색 = 강함 단계).
// 기본은 "시간 기준": 1분마다 체력·속도가 오르고, 5분마다 몸통 피해 +1, 3분마다 색이 바뀜.
// 게임 씬에 플레이어 성장(PlayerProgression)이 있으면 실행할 때 자동으로 생김. 따로 설치할 것 없음.
// 숫자를 바꾸려면: 플레이 중 Hierarchy의 "EnemyLevelScaling" 선택 → 인스펙터 (또는 씬에 직접 붙여 두고 조절)
// 팀원 코드(EnemyHealth, EnemyMovement, PlayerHealth)는 고치지 않고 값만 바꿈.
[DisallowMultipleComponent]
public class EnemyLevelScaling : MonoBehaviour
{
    public static EnemyLevelScaling Instance { get; private set; }

    public enum ScaleBy { Time, Level }

    [Header("무엇에 따라 강해질지")]
    [Tooltip("Time = 버틴 시간(1분마다) / Level = 플레이어 레벨 (예전 방식)")]
    [SerializeField] private ScaleBy scaleBy = ScaleBy.Time;

    [Header("시간 기준 (1분마다)")]
    [Tooltip("체력 배수 = 1 + 이 값 × 분 + 아래 값 × 분²   (기본: 5분 2.75배, 10분 5.5배, 15분 9.25배)")]
    [SerializeField, Min(0f)] private float healthPerMinute = 0.25f;
    [Tooltip("뒤로 갈수록 더 가파르게 (분² 곱하는 값)")]
    [SerializeField, Min(0f)] private float healthPerMinuteSquared = 0.02f;
    [Tooltip("이동 속도 +% (0.03 = 1분마다 3%, 최대 배수는 아래 '적 이동 속도 최대 배수')")]
    [SerializeField, Min(0f)] private float speedPerMinute = 0.03f;
    [Tooltip("이 분마다 몸통 피해 +1 (0 = 안 올림)")]
    [SerializeField, Min(0f)] private float contactDamageEveryMinutes = 5f;
    [Tooltip("이 분마다 색(강함 단계)이 바뀜: 초록 → 파랑 → 보라 → 빨강 → 금색")]
    [SerializeField, Min(0.1f)] private float minutesPerTier = 3f;

    [Header("기본 속도")]
    [Tooltip("모든 적 이동 속도 배수 (1 = 원래 속도, 0.75 = 25% 느리게)")]
    [SerializeField, Range(0.2f, 2f)] private float baseSpeedMultiplier = 0.75f;

    [Header("레벨 기준일 때 (플레이어 레벨 1 오를 때마다)")]
    [Tooltip("적 체력 +% (0.06 = 레벨마다 6%)")]
    [SerializeField, Min(0f)] private float healthPerLevel = 0.06f;
    [Tooltip("적 이동 속도 +% (0.004 = 레벨마다 0.4%)")]
    [SerializeField, Min(0f)] private float speedPerLevel = 0.004f;
    [Tooltip("적 이동 속도 최대 배수")]
    [SerializeField, Min(1f)] private float maxSpeedMultiplier = 1.5f;
    [Tooltip("이 레벨마다 적이 주는 접촉 피해 +1 (0 = 안 올림)")]
    [SerializeField, Min(0)] private int contactDamageEveryLevels = 15;

    [Header("적 모습")]
    [SerializeField] private bool useSkins = true;
    [Tooltip("이 레벨마다 색(강함 단계)이 바뀜: 초록 → 파랑 → 보라 → 빨강 → 금색")]
    [SerializeField, Min(1)] private int levelsPerTier = 25;
    [Tooltip("원래 동그라미보다 몇 배 크게 그릴지")]
    [SerializeField, Min(0.1f)] private float skinSize = 1.25f;
    [Header("움직이는 그림 (Resources/EnemySkins/<종류>_anim_<색>.png, 가로로 칸이 이어진 그림)")]
    [SerializeField, Min(1)] private int animFrames = 8;
    [SerializeField, Min(1f)] private float animFps = 10f;
    [Tooltip("칸 높이 중 몸이 차지하는 비율 (위쪽은 뛰어오르는 공간)")]
    [SerializeField, Range(0.2f, 1f)] private float animBodyFraction = 0.65f;
    [Tooltip("칸 아래에서 몸 가운데까지 비율 (적 위치 = 몸 가운데)")]
    [SerializeField, Range(0f, 1f)] private float animPivotY = 0.34f;

    [Header("경험치 곡선 (팀원 파일에 한 줄 적용하면 사용됨)")]
    [SerializeField, Min(1)] private int requiredFirst = 5;
    [SerializeField, Min(1f)] private float requiredBase = 8f;
    [SerializeField, Min(0f)] private float requiredPerLevel = 3f;
    [SerializeField, Min(1f)] private float killExpBase = 1f;
    [SerializeField, Min(0f)] private float killExpPerLevel = 0.04f;

    public int RequiredFirst => requiredFirst;
    public float RequiredBase => requiredBase;
    public float RequiredPerLevel => requiredPerLevel;
    public float KillExpBase => killExpBase;
    public float KillExpPerLevel => killExpPerLevel;
    public int CurrentTier => Mathf.Clamp(scaleBy == ScaleBy.Time
        ? Mathf.FloorToInt(Minutes / Mathf.Max(0.1f, minutesPerTier))
        : (Level - 1) / levelsPerTier, 0, TierNames.Length - 1);

    // 버틴 시간(분)
    public static float Minutes => GameSession.Instance != null ? GameSession.Instance.SurvivalTime / 60f : 0f;

    // 지금 나오는 적의 체력 배수 / 속도 배수 / 몸통 피해 추가 (치트 패널·디버그 표시용으로도 씀)
    public float HealthMultiplier => scaleBy == ScaleBy.Time
        ? 1f + healthPerMinute * Minutes + healthPerMinuteSquared * Minutes * Minutes
        : 1f + healthPerLevel * (Level - 1);
    public float SpeedMultiplier => baseSpeedMultiplier * Mathf.Min(scaleBy == ScaleBy.Time
        ? 1f + speedPerMinute * Minutes
        : 1f + speedPerLevel * (Level - 1), maxSpeedMultiplier);
    public int ContactBonus => scaleBy == ScaleBy.Time
        ? (contactDamageEveryMinutes > 0f ? Mathf.FloorToInt(Minutes / contactDamageEveryMinutes) : 0)
        : (contactDamageEveryLevels > 0 ? (Level - 1) / contactDamageEveryLevels : 0);

    private static readonly string[] TierNames = { "green", "blue", "purple", "red", "gold" };
    private static readonly string[] Kinds = { "slime", "mushroom" };
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private readonly HashSet<EnemyHealth> seen = new HashSet<EnemyHealth>();
    private readonly List<EnemyHealth> drop = new List<EnemyHealth>();
    private Sprite[,] skins;
    private Sprite[,][] anims;
    private FieldInfo maxHealthField, currentHealthField, moveSpeedField, contactField;
    private PlayerHealth player;
    private int baseContactDamage = -1;
    private float nextCleanup;

    private static int Level => PlayerProgression.Instance != null ? PlayerProgression.Instance.Level : 1;

    // ───────── 자동 생성 ─────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Create();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Create();

    private static void Create()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<EnemyLevelScaling>() != null) return;
        if (FindAnyObjectByType<PlayerProgression>() == null) return; // 게임 씬에서만
        new GameObject("EnemyLevelScaling").AddComponent<EnemyLevelScaling>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        maxHealthField = typeof(EnemyHealth).GetField("maxHealth", Flags);
        currentHealthField = typeof(EnemyHealth).GetField("currentHealth", Flags);
        moveSpeedField = typeof(EnemyMovement).GetField("moveSpeed", Flags);
        contactField = typeof(PlayerHealth).GetField("contactDamage", Flags);
        LoadSkins();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadSkins()
    {
        skins = new Sprite[Kinds.Length, TierNames.Length];
        for (int k = 0; k < Kinds.Length; k++)
            for (int t = 0; t < TierNames.Length; t++)
                skins[k, t] = Resources.Load<Sprite>("EnemySkins/" + Kinds[k] + "_" + TierNames[t]);
        anims = new Sprite[Kinds.Length, TierNames.Length][];
        for (int k = 0; k < Kinds.Length; k++)
            for (int t = 0; t < TierNames.Length; t++)
            {
                Texture2D tex = Resources.Load<Texture2D>("EnemySkins/" + Kinds[k] + "_anim_" + TierNames[t]);
                if (tex == null) continue;
                int n = Mathf.Max(1, animFrames);
                float w = (float)tex.width / n;
                var frames = new Sprite[n];
                for (int i = 0; i < n; i++)
                    frames[i] = Sprite.Create(tex, new Rect(Mathf.Round(i * w), 0f, Mathf.Floor(w), tex.height),
                        new Vector2(0.5f, animPivotY), 32f, 0, SpriteMeshType.FullRect);
                anims[k, t] = frames;
            }
    }

    // ───────── 매 프레임: 새로 나온 적만 한 번 손봄 ─────────
    private void LateUpdate()
    {
        int level = Level;
        UpdateContactDamage(level);

        List<EnemyHealth> enemies = SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null));
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth e = enemies[i];
            if (e == null || seen.Contains(e)) continue;
            seen.Add(e);
            if (e.GetComponent<BossController>() != null || e.GetComponent<BossHitProxy>() != null) continue; // 보스는 따로
            Strengthen(e, level);
            if (useSkins) Dress(e);
        }

        if (Time.unscaledTime >= nextCleanup)
        {
            nextCleanup = Time.unscaledTime + 2f;
            drop.Clear();
            foreach (EnemyHealth e in seen) if (e == null) drop.Add(e);
            foreach (EnemyHealth e in drop) seen.Remove(e);
        }
    }

    private void Strengthen(EnemyHealth e, int level)
    {
        float hp = HealthMultiplier;
        if (maxHealthField != null && currentHealthField != null && hp > 1f)
        {
            int max = Mathf.CeilToInt((int)maxHealthField.GetValue(e) * hp);
            maxHealthField.SetValue(e, max);
            currentHealthField.SetValue(e, max);
        }

        EnemyMovement move = e.GetComponent<EnemyMovement>();
        if (move != null && moveSpeedField != null)
        {
            float speed = SpeedMultiplier;
            moveSpeedField.SetValue(move, (float)moveSpeedField.GetValue(move) * speed);
        }
    }

    private void Dress(EnemyHealth e)
    {
        int tier = CurrentTier;
        int kind = Random.Range(0, Kinds.Length);
        EnemySkin skin = e.GetComponent<EnemySkin>();
        Sprite[] anim = anims[kind, tier];
        if (anim != null)
        {
            if (skin == null) skin = e.gameObject.AddComponent<EnemySkin>();
            skin.InitAnimated(anim, animFps, skinSize / animBodyFraction);
            return;
        }
        Sprite s = skins[kind, tier] != null ? skins[kind, tier] : skins[0, tier];
        if (s == null) return;
        if (skin == null) skin = e.gameObject.AddComponent<EnemySkin>();
        skin.Init(s, kind == 0 ? EnemySkin.Motion.Bounce : EnemySkin.Motion.Waddle, skinSize);
    }

    private void UpdateContactDamage(int level)
    {
        if (contactField == null) return;
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerHealth>();
            if (player == null) return;
            baseContactDamage = (int)contactField.GetValue(player);
        }
        int want = baseContactDamage + ContactBonus;
        if ((int)contactField.GetValue(player) != want) contactField.SetValue(player, want);
    }
}
