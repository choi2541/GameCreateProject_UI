using UnityEngine;

// 증강·스탯으로 바뀌는 스킬 공통 배율. 스킬 코드가 이 값을 읽어서 적용합니다.
//   피해량  = 기본 피해 × DamageMultiplier(증강) × (1 + SKL 스탯 보너스)
//   쿨타임  = 기본 쿨타임 × CooldownMultiplier(증강) × (1 - CDR 스탯 감소)
// 판이 새로 시작될 때(AugmentSystem 생성 시) 1로 초기화됩니다.
public static class SkillModifiers
{
    public static float DamageMultiplier = 1f;
    public static float CooldownMultiplier = 1f;

    public static void Reset()
    {
        DamageMultiplier = 1f;
        CooldownMultiplier = 1f;
    }

    public static int Damage(int baseDamage)
    {
        PlayerStats stats = PlayerStats.Instance;
        float statBonus = stats != null ? stats.SkillDamageBonus : 0f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * DamageMultiplier * (1f + statBonus)));
    }

    public static float Cooldown(float baseCooldown)
    {
        PlayerStats stats = PlayerStats.Instance;
        float reduction = stats != null ? stats.CooldownReduction : 0f;
        return Mathf.Max(0.05f, baseCooldown * CooldownMultiplier * (1f - reduction));
    }
}
