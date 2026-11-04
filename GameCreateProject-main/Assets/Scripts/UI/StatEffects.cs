using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// [임시] 스탯 포인트를 실제 게임에 적용합니다. Player에 PlayerStats와 같이 붙습니다.
// 팀원 코드는 고치지 않고, 각 컴포넌트의 Inspector 값(공격력, 이동 속도, 최대 체력 등)을
// "원래 값 × 스탯 보너스"로 바꿔 넣는 방식입니다. 이 컴포넌트를 지우면(Remove) 원래대로 돌아갑니다.
//
//   STR : 기본 공격 피해 +%      (AutoAttack / AutoProjectileAttack / AutoGunAttack)
//   VIT : 최대 체력 +            (늘어난 만큼 현재 체력도 회복)
//   SPD : 이동 속도 +%           (PlayerMovement)
//   EXP : 얻는 경험치 +%         (소수점은 모아서 1씩 추가)
//   SKL : 스킬 피해 +%           (PiercingProjectileSkill + 2~4차 스킬)
//   CDR : 스킬 쿨타임 -%         (PiercingProjectileSkill + 2~4차 스킬)
// 숫자는 전부 Inspector에서 조절. (밸런스는 나중에)
[DisallowMultipleComponent]
public class StatEffects : MonoBehaviour
{
    [Header("1포인트당 효과")]
    [Tooltip("STR 1포인트당 기본 공격 피해 +%")]
    [SerializeField, Min(0f)] private float strDamagePercent = 5f;
    [Tooltip("VIT 1포인트당 최대 체력 +")]
    [SerializeField, Min(0f)] private float vitHealth = 1f;
    [Tooltip("SPD 1포인트당 이동 속도 +%")]
    [SerializeField, Min(0f)] private float spdMovePercent = 2f;
    [Tooltip("EXP 1포인트당 얻는 경험치 +%")]
    [SerializeField, Min(0f)] private float expGainPercent = 1f;

    [Header("경험치 계산 (팀 ExperienceProgress와 같게)")]
    [SerializeField, Min(1)] private int firstLevelExp = 5;
    [SerializeField, Min(1)] private int otherLevelExp = 10;

    [Header("스탯 창 설명도 위 숫자로 바꾸기")]
    [SerializeField] private bool updateDescriptions = true;

    private class Target
    {
        public Component component;
        public FieldInfo field;
        public double baseValue;
        public string stat; // STR, SKL, CDR, SPD, VIT
    }

    private static readonly (string type, string field, string stat)[] Map =
    {
        ("AutoAttack", "damage", "STR"),
        ("AutoProjectileAttack", "damage", "STR"),
        ("AutoGunAttack", "damage", "STR"),
        ("PiercingProjectileSkill", "damage", "SKL"),
        ("PiercingProjectileSkill", "cooldown", "CDR"),
        ("PlayerMovement", "moveSpeed", "SPD"),
        ("PlayerHealth", "maxHealth", "VIT"),
    };

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private readonly List<Target> targets = new List<Target>();
    private readonly HashSet<Component> scanned = new HashSet<Component>();
    private PlayerStats stats;
    private PlayerProgression progression;
    private PlayerHealth health;
    private FieldInfo currentHealthField;
    private float nextScan;
    private long lastTotalExp;
    private double expCarry;
    // EXP 스탯으로 모인 소수점 경험치 (아직 1이 안 돼서 레벨 경험치에 안 들어간 것)
    // → 경험치바에 같이 보여 줌. 레벨업해도 사라지지 않고 다음 레벨로 넘어감
    public static float PendingExp { get; private set; }
    private bool addingExp;

    private void Start()
    {
        stats = PlayerStats.Instance;
        progression = PlayerProgression.Instance;
        health = GetComponentInParent<PlayerHealth>();
        if (health != null)
            currentHealthField = typeof(PlayerHealth).GetField("currentHealth", Flags);

        if (stats != null) stats.StatChanged += OnStatChanged;
        if (progression != null)
        {
            progression.Changed += OnProgressChanged;
            lastTotalExp = TotalExp();
        }
        if (updateDescriptions) WriteDescriptions();
        Scan();
        Apply();
    }

    private void OnDestroy()
    {
        PendingExp = 0f;
        if (stats != null) stats.StatChanged -= OnStatChanged;
        if (progression != null) progression.Changed -= OnProgressChanged;
        // 지우면 원래 값으로
        foreach (Target t in targets)
            if (t.component != null) Set(t, t.baseValue);
    }

    private void Update()
    {
        // 직업이 바뀌며 새로 켜지는 공격 컴포넌트도 잡기 위해 가끔 다시 찾음
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            if (Scan()) Apply();
        }
    }

    private void OnStatChanged(string id, int value) => Apply();

    // ───────── 값 바꾸기 ─────────
    private bool Scan()
    {
        bool added = false;
        Transform root = health != null ? health.transform : transform;
        foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b == null || scanned.Contains(b)) continue;
            scanned.Add(b);
            string typeName = b.GetType().Name;
            foreach (var m in Map)
            {
                if (m.type != typeName) continue;
                FieldInfo f = b.GetType().GetField(m.field, Flags);
                if (f == null || (f.FieldType != typeof(int) && f.FieldType != typeof(float))) continue;
                targets.Add(new Target { component = b, field = f, stat = m.stat, baseValue = System.Convert.ToDouble(f.GetValue(b)) });
                added = true;
            }
        }
        return added;
    }

    private void Apply()
    {
        if (stats == null) stats = PlayerStats.Instance;
        if (stats == null) return;

        foreach (Target t in targets)
        {
            if (t.component == null) continue;
            double v = t.baseValue;
            switch (t.stat)
            {
                case "STR": v *= 1.0 + stats.Get("STR") * strDamagePercent / 100.0; break;
                case "SKL": v *= 1.0 + stats.SkillDamageBonus; break;
                case "CDR": v *= 1.0 - stats.CooldownReduction; break;
                case "SPD": v *= 1.0 + stats.Get("SPD") * spdMovePercent / 100.0; break;
                case "VIT": v += stats.Get("VIT") * vitHealth; break;
            }

            if (t.stat == "VIT")
            {
                int before = System.Convert.ToInt32(t.field.GetValue(t.component));
                int after = Mathf.Max(1, Mathf.RoundToInt((float)v));
                Set(t, after);
                // 늘어난 최대 체력만큼 현재 체력도 회복
                if (currentHealthField != null && t.component == health && health != null && !health.IsDead)
                {
                    int cur = (int)currentHealthField.GetValue(health);
                    currentHealthField.SetValue(health, Mathf.Clamp(cur + (after - before), 1, after));
                }
            }
            else Set(t, v);
        }
    }

    private static void Set(Target t, double value)
    {
        if (t.field.FieldType == typeof(int))
            t.field.SetValue(t.component, Mathf.Max(1, Mathf.RoundToInt((float)value)));
        else
            t.field.SetValue(t.component, Mathf.Max(0.01f, (float)value));
    }

    // ───────── 경험치 보너스 ─────────
    private long TotalExp()
    {
        if (progression == null) return 0;
        int level = progression.Level;
        long total = progression.Experience;
        if (level >= 2) total += firstLevelExp + (long)(level - 2) * otherLevelExp;
        return total;
    }

    private void OnProgressChanged()
    {
        if (addingExp || progression == null) return;
        long total = TotalExp();
        long gained = total - lastTotalExp;
        lastTotalExp = total;
        if (gained <= 0 || stats == null) return;

        expCarry += gained * stats.Get("EXP") * expGainPercent / 100.0;
        int extra = (int)expCarry;
        PendingExp = (float)(expCarry - extra);
        if (extra <= 0) return;
        expCarry -= extra;
        addingExp = true;
        progression.AddExperience(extra);
        addingExp = false;
        lastTotalExp = TotalExp();
    }

    // ───────── 스탯 창 설명 ─────────
    private void WriteDescriptions()
    {
        if (stats == null) return;
        SetDesc("STR", $"1포인트당 기본 공격 피해 +{strDamagePercent:0.#}%");
        SetDesc("VIT", $"1포인트당 최대 체력 +{vitHealth:0.#}");
        SetDesc("SPD", $"1포인트당 이동 속도 +{spdMovePercent:0.#}%");
        SetDesc("EXP", $"1포인트당 얻는 경험치 +{expGainPercent:0.#}%");
        SetDesc("SKL", "1포인트당 스킬 피해 +2%\n(관통 스킬, 2~4차 스킬)");
        SetDesc("CDR", "1포인트당 스킬 쿨타임 -1% (최대 50%)\n(관통 스킬, 2~4차 스킬)");
    }

    private void SetDesc(string id, string text)
    {
        int i = stats.IndexOf(id);
        if (i >= 0) stats.GetDefinition(i).description = text + "\n(임시 수치)";
    }
}
