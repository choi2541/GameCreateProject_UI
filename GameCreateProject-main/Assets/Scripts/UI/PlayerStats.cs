using System;
using UnityEngine;

// 스탯 찍기 데이터. Player 오브젝트에 붙입니다. (GameUIBuilder가 자동으로 붙여줌)
// 레벨업마다 pointsPerLevel만큼 포인트가 생기고, 스탯 창에서 + 버튼으로 투자합니다.
//
// [개발 파트 참고] 스탯 효과를 적용하려면 값을 읽어서 쓰면 됩니다.
//   int str = PlayerStats.Instance != null ? PlayerStats.Instance.Get("STR") : 0;
//   PlayerStats.Instance.StatChanged += (id, value) => { ... };  // 포인트를 찍을 때마다 호출
[DisallowMultipleComponent]
public class PlayerStats : MonoBehaviour
{
    [Serializable]
    public class StatDefinition
    {
        [Tooltip("코드에서 찾을 때 쓰는 이름 (예: STR)")]
        public string id = "STR";
        [Tooltip("툴팁 제목에 표시되는 이름")]
        public string displayName = "힘";
        [TextArea(2, 4)]
        public string description = "";
        [Tooltip("스탯 아이콘 이미지. 비워두면 임시 색 박스 + 글자로 표시됩니다.")]
        public Sprite icon;
        public Color placeholderColor = new Color(0.8f, 0.3f, 0.3f);
        [Min(0)] public int baseValue = 0;
        [Min(1)] public int maxValue = 99;
    }

    public static PlayerStats Instance { get; private set; }

    [SerializeField, Min(0)] private int pointsPerLevel = 3;
    [Tooltip("켜면 스탯마다 최대치 없이 무한으로 찍을 수 있음 (레벨도 무한이라서). 끄면 각 스탯의 Max Value까지만")]
    [SerializeField] private bool unlimitedStats = true;
    [SerializeField, Min(0)] private int startingPoints = 0;

    [Header("스킬에 바로 적용되는 스탯 (SKL, CDR)")]
    [Tooltip("SKL 1포인트당 스킬 피해 +%")]
    [SerializeField, Min(0f)] private float skillDamagePerPoint = 2f;
    [Tooltip("CDR 1포인트당 스킬 쿨타임 -%")]
    [SerializeField, Min(0f)] private float cooldownPerPoint = 1f;
    [Tooltip("쿨타임 감소 최대치 %")]
    [SerializeField, Range(0f, 90f)] private float maxCooldownReduction = 50f;

    [Header("[임시] 스탯을 실제 게임 수치에 적용")]
    [Tooltip("켜면 실행할 때 StatEffects가 자동으로 붙어서 힘=공격력, 체력=최대HP, 스피드=이동속도, 경험치=경험치량에 적용됩니다.\n끄면 스탯 창에서 포인트만 찍히고 게임 수치는 그대로입니다.")]
    [SerializeField] private bool applyStatEffects = true;

    [SerializeField]
    private StatDefinition[] stats = DefaultStats();

    // 기본 스탯 6개 (임시 설명)
    public static StatDefinition[] DefaultStats()
    {
        return new[]
        {
            new StatDefinition { id = "STR", displayName = "힘",
                description = "공격력이 증가합니다.\n(임시 설명 · 효과 연결 필요)",
                placeholderColor = new Color(0.86f, 0.33f, 0.3f) },
            new StatDefinition { id = "VIT", displayName = "체력",
                description = "최대 체력이 증가합니다.\n(임시 설명 · 효과 연결 필요)",
                placeholderColor = new Color(0.33f, 0.72f, 0.42f) },
            new StatDefinition { id = "SPD", displayName = "스피드",
                description = "이동 속도가 증가합니다.\n(임시 설명 · 효과 연결 필요)",
                placeholderColor = new Color(0.35f, 0.6f, 0.95f) },
            new StatDefinition { id = "EXP", displayName = "경험치",
                description = "1포인트당 얻는 경험치 +1% (99포인트 = +99%)\n(효과 연결 필요)",
                placeholderColor = new Color(0.95f, 0.78f, 0.25f) },
            new StatDefinition { id = "SKL", displayName = "스킬 피해력",
                description = "1포인트당 스킬 피해 +2%\n(대쉬 화살, 2차 Z·X·C에 적용됨)",
                placeholderColor = new Color(0.72f, 0.42f, 0.95f) },
            new StatDefinition { id = "CDR", displayName = "쿨타임 감소",
                description = "1포인트당 스킬 쿨타임 -1% (최대 50%)\n(대쉬, 2차 Z·X·C에 적용됨)",
                placeholderColor = new Color(0.3f, 0.85f, 0.85f) },
        };
    }

    // 기존 목록을 지우지 않고, 없는 기본 스탯만 뒤에 추가합니다. (빌더가 호출)
    [ContextMenu("빠진 기본 스탯 추가")]
    public void AddMissingDefaults()
    {
        var list = new System.Collections.Generic.List<StatDefinition>(stats ?? new StatDefinition[0]);
        foreach (StatDefinition def in DefaultStats())
        {
            if (list.Exists(s => string.Equals(s.id, def.id, StringComparison.OrdinalIgnoreCase)))
                continue;
            list.Add(def);
        }
        stats = list.ToArray();
    }

    // SkillModifiers가 읽는 값 (0.1 = 10%)
    public float SkillDamageBonus => Get("SKL") * skillDamagePerPoint / 100f;
    public float CooldownReduction => Mathf.Min(Get("CDR") * cooldownPerPoint, maxCooldownReduction) / 100f;

    private int[] invested;
    private int spent;
    private int bonusPoints;

    public event Action Changed;
    public event Action<string, int> StatChanged;

    public int Count => stats != null ? stats.Length : 0;

    public int TotalPoints
    {
        get
        {
            PlayerProgression progression = PlayerProgression.Instance;
            int level = progression != null ? progression.Level : 1;
            return startingPoints + bonusPoints + Mathf.Max(0, level - 1) * pointsPerLevel;
        }
    }

    public int AvailablePoints => Mathf.Max(0, TotalPoints - spent);

    // 씬에 PlayerStats가 없으면 Player에 자동으로 붙입니다 (스탯 창이 비지 않게).
    // 포인트와 찍은 숫자만 기억하고, 게임 수치(공격력·체력 등)는 바꾸지 않습니다.
    public static PlayerStats Ensure()
    {
        if (Instance != null) return Instance;
        PlayerHealth player = GameSession.Instance != null ? GameSession.Instance.Player : null;
        if (player == null) player = FindAnyObjectByType<PlayerHealth>();
        if (player == null) return null;
        PlayerStats found = player.GetComponent<PlayerStats>();
        return found != null ? found : player.gameObject.AddComponent<PlayerStats>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("PlayerStats는 하나만 두세요.", this);
            enabled = false;
            return;
        }
        Instance = this;
        invested = new int[Count];
        // 아이콘이 비어 있으면 도트 UI 룬 그림 사용 (게임 씬 HUD의 PixelUISprites에 들어 있음)
        for (int i = 0; i < Count; i++)
        {
            StatDefinition def = GetDefinition(i);
            if (def != null && def.icon == null) def.icon = PixelUISprites.Rune(def.id);
        }
    }

    private void Start()
    {
        if (applyStatEffects && GetComponent<StatEffects>() == null)
            gameObject.AddComponent<StatEffects>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 증강 등으로 추가 포인트 지급
    public void AddBonusPoints(int amount)
    {
        if (amount <= 0)
            return;
        bonusPoints += amount;
        Changed?.Invoke();
    }

    public StatDefinition GetDefinition(int index) => stats[index];

    public int GetValue(int index)
    {
        if (index < 0 || index >= Count)
            return 0;
        int extra = invested != null && index < invested.Length ? invested[index] : 0;
        return stats[index].baseValue + extra;
    }

    // id로 스탯 값을 가져옵니다. 없으면 0.
    public int Get(string id)
    {
        int index = IndexOf(id);
        return index >= 0 ? GetValue(index) : 0;
    }

    public int IndexOf(string id)
    {
        for (int i = 0; i < Count; i++)
        {
            if (string.Equals(stats[i].id, id, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public bool CanAddPoint(int index)
    {
        return isActiveAndEnabled && index >= 0 && index < Count
            && AvailablePoints > 0 && (unlimitedStats || GetValue(index) < stats[index].maxValue);
    }

    public bool AddPoint(int index) => AddPoints(index, 1);

    // amount만큼 한 번에 올릴 수 있는지 (포인트와 최대치가 충분해야 함)
    public bool CanAddPoints(int index, int amount)
    {
        return amount > 0 && isActiveAndEnabled && index >= 0 && index < Count
            && AvailablePoints >= amount && (unlimitedStats || GetValue(index) + amount <= stats[index].maxValue);
    }

    public bool AddPoints(int index, int amount)
    {
        if (!CanAddPoints(index, amount))
            return false;

        invested[index] += amount;
        spent += amount;
        StatChanged?.Invoke(stats[index].id, GetValue(index));
        Changed?.Invoke();
        return true;
    }
}
