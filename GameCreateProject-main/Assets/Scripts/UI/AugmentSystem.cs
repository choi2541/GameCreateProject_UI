using System;
using System.Collections.Generic;
using UnityEngine;

// 증강 선택 (보물상자 등에서 호출). 게임을 멈추고 카드 3장 중 하나를 고르게 합니다.
//
// [보물상자 연결 - 개발 파트 참고]  상자를 먹을 때 이 한 줄만 호출하면 됩니다.
//     AugmentSystem.Offer();
//   전직 선택 같은 다른 창이 떠 있으면 그 창이 닫힌 뒤 자동으로 열립니다. (여러 번 호출하면 차례대로)
//
// [효과]
//   StatPoints    : 스탯 포인트 +값
//   SkillDamage   : 스킬 피해 +값%  (SkillModifiers 사용 스킬: 대쉬 화살, 2차 Z/X/C)
//   SkillCooldown : 스킬 쿨타임 -값% (같은 스킬들)
//   Lifesteal     : (패시브) 적에게 준 피해의 값%만큼 체력 회복 (평타·스킬·보스 피해 모두 포함)
//   Custom        : 효과 없음. AugmentSystem.Picked 이벤트로 개발 파트가 직접 처리
//                   예) AugmentSystem.Instance.Picked += a => { if (a.id == "HEAL") ... };
[DisallowMultipleComponent]
public class AugmentSystem : MonoBehaviour
{
    public enum Effect { Custom, StatPoints, SkillDamage, SkillCooldown, Lifesteal }
    public enum Rarity { Common, Rare, Epic }

    [Serializable]
    public class Augment
    {
        public string id = "NEW";
        public string displayName = "새 증강";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;
        public Rarity rarity = Rarity.Common;
        public Effect effect = Effect.Custom;
        [Tooltip("효과 수치 (스탯 포인트 개수, 또는 %)")]
        public float value = 0f;
        [Tooltip("최대 몇 번까지 고를 수 있는지")]
        [Min(1)] public int maxStacks = 5;
        [Tooltip("체크하면 게임 시작부터 이 증강을 1개 가지고 시작")]
        public bool startOwned = false;
    }

    public static AugmentSystem Instance { get; private set; }

    [SerializeField, Range(1, 3)] private int choices = 3;
    [Tooltip("창이 뜬 직후 이 시간 동안은 클릭 무시 (실수 방지)")]
    [SerializeField, Min(0f)] private float inputDelay = 0.4f;

    [Header("화면")]
    [SerializeField] private GameObject panel;
    [SerializeField] private AugmentCardUI[] cards = new AugmentCardUI[0];

    [Header("증강 목록 (임시)")]
    [SerializeField]
    private Augment[] augments =
    {
        new Augment { id = "SHARP", displayName = "날카로운 화살", description = "스킬 피해 +20%",
            rarity = Rarity.Common, effect = Effect.SkillDamage, value = 20f, maxStacks = 5 },
        new Augment { id = "QUICK", displayName = "빠른 손놀림", description = "스킬 쿨타임 -10%",
            rarity = Rarity.Common, effect = Effect.SkillCooldown, value = 10f, maxStacks = 4 },
        new Augment { id = "TRAIN", displayName = "수련", description = "스탯 포인트 +3",
            rarity = Rarity.Common, effect = Effect.StatPoints, value = 3f, maxStacks = 99 },
        new Augment { id = "FOCUS", displayName = "집중", description = "스킬 피해 +40%",
            rarity = Rarity.Rare, effect = Effect.SkillDamage, value = 40f, maxStacks = 3 },
        new Augment { id = "TALENT", displayName = "재능", description = "스탯 포인트 +6",
            rarity = Rarity.Rare, effect = Effect.StatPoints, value = 6f, maxStacks = 99 },
        new Augment { id = "HASTE", displayName = "신속", description = "스킬 쿨타임 -25%",
            rarity = Rarity.Epic, effect = Effect.SkillCooldown, value = 25f, maxStacks = 2 },
        new Augment { id = "VAMP", displayName = "흡혈", description = "(패시브) 준 피해의 3%만큼 체력 회복",
            rarity = Rarity.Rare, effect = Effect.Lifesteal, value = 3f, maxStacks = 3,
            startOwned = true }, // 임시: 게임 시작부터 흡혈 1개 보유
    };

    private readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
    private readonly List<Augment> offered = new List<Augment>();
    private int pending;
    private float openedAt;
    private float previousTimeScale = 1f;
    private float lifestealPercent;
    private float healBank;

    public bool IsOpen { get; private set; }
    public event Action<Augment> Picked;

    // 보물상자에서 호출
    public static void Offer()
    {
        if (Instance != null)
            Instance.pending++;
        else
            Debug.LogWarning("[AugmentSystem] 씬에 AugmentSystem이 없습니다.");
    }

    public int GetStacks(string id) => stacks.TryGetValue(id, out int n) ? n : 0;

    // 지금까지 가진 증강 (캐릭터 창 목록용)
    public List<(Augment augment, int count)> Owned()
    {
        List<(Augment, int)> list = new List<(Augment, int)>();
        foreach (Augment a in augments)
        {
            int n = GetStacks(a.id);
            if (n > 0) list.Add((a, n));
        }
        return list;
    }

    public event Action OwnedChanged;

    private void Awake()
    {
        Instance = this;
        SkillModifiers.Reset();
        if (panel != null)
            panel.SetActive(false);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            if (cards[i] != null)
                cards[i].Clicked += () => Choose(index);
        }
    }

    private void Start()
    {
        // 시작부터 가지고 있는 증강
        foreach (Augment a in augments)
        {
            if (a.startOwned && GetStacks(a.id) == 0)
            {
                stacks[a.id] = 1;
                Apply(a);
            }
        }
        OwnedChanged?.Invoke();
    }

    private void OnEnable() => DamageTracker.EnemyDamaged += OnEnemyDamaged;
    private void OnDisable() => DamageTracker.EnemyDamaged -= OnEnemyDamaged;

    // 흡혈: 준 피해를 모아서 1 이상이 되면 체력 1 회복
    private void OnEnemyDamaged(EnemyHealth enemy, int amount, Vector3 position)
    {
        if (lifestealPercent <= 0f)
            return;
        healBank += amount * lifestealPercent / 100f;
        if (healBank >= 1f)
        {
            int heal = Mathf.FloorToInt(healBank);
            healBank -= heal;
            DamageTracker.HealPlayer(heal);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("테스트: 증강 창 열기")]
    private void DebugOffer()
    {
        if (Application.isPlaying)
            pending++;
    }

    private void Update()
    {
        // 다른 창(일시정지, 전직 선택 등)이 없고 게임이 돌아갈 때만 엽니다.
        if (IsOpen || pending <= 0 || Time.timeScale <= 0f)
            return;
        GameSession session = GameSession.Instance;
        if (session != null && (session.IsGameOver || session.IsPaused))
            return;

        pending--;
        Open();
    }

    private void Open()
    {
        offered.Clear();
        List<Augment> pool = new List<Augment>();
        foreach (Augment a in augments)
        {
            if (GetStacks(a.id) < a.maxStacks)
                pool.Add(a);
        }
        if (pool.Count == 0)
            return; // 더 고를 증강이 없음

        for (int i = 0; i < choices && pool.Count > 0; i++)
        {
            int pick = UnityEngine.Random.Range(0, pool.Count);
            offered.Add(pool[pick]);
            pool.RemoveAt(pick);
        }

        IsOpen = true;
        openedAt = Time.unscaledTime;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        UITooltip.Hide();

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            bool used = i < offered.Count;
            cards[i].gameObject.SetActive(used);
            if (used)
                cards[i].Show(offered[i], GetStacks(offered[i].id));
        }
        if (panel != null)
            panel.SetActive(true);
    }

    private void Choose(int index)
    {
        if (!IsOpen || index >= offered.Count || Time.unscaledTime - openedAt < inputDelay)
            return;

        Augment chosen = offered[index];
        stacks[chosen.id] = GetStacks(chosen.id) + 1;
        Apply(chosen);
        OwnedChanged?.Invoke();

        IsOpen = false;
        if (panel != null)
            panel.SetActive(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        Picked?.Invoke(chosen);
    }

    private void Apply(Augment a)
    {
        switch (a.effect)
        {
            case Effect.StatPoints:
                if (PlayerStats.Instance != null)
                    PlayerStats.Instance.AddBonusPoints(Mathf.RoundToInt(a.value));
                break;
            case Effect.SkillDamage:
                SkillModifiers.DamageMultiplier += a.value / 100f;
                break;
            case Effect.SkillCooldown:
                SkillModifiers.CooldownMultiplier *= Mathf.Clamp01(1f - a.value / 100f);
                break;
            case Effect.Lifesteal:
                lifestealPercent += a.value;
                break;
        }
    }
}
