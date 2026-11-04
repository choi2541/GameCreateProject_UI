using UnityEngine;

// 3차 스킬 공통: ArcherSkillBase + 갈래 표시. JobTierSystem이 켜고 끕니다.
public abstract class ThirdJobSkill : ArcherSkillBase, IThirdJobSkill
{
    public abstract ArcherBranch Branch { get; }

    [Header("계수 (폭풍 = AP · 스킬 피해력 / 그림자 = AD · 힘)")]
    [Tooltip("스탯 보너스를 몇 배로 받을지. 1 = 그대로, 1.5 = 1.5배, 0 = 스탯 영향 없음")]
    [SerializeField, Min(0f)] protected float statRatio = 1f;

    public float StatRatio => statRatio;
    public string RatioText => (Branch == ArcherBranch.Storm ? "AP" : "AD") + " 계수 " + (statRatio * 100f).ToString("0") + "%";

    protected int Dmg(int baseDamage, EnemyHealth target = null) => JobTierUtil.Damage(baseDamage, Branch, target, statRatio);
}
