using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [3차 그림자 X] 저주 폭발 : 화면 안의 표식 붙은 적을 가까운 순서로 연달아 터뜨림 (주변 적도 피해)
// 표식이 하나도 없으면 가까운 적 몇 마리를 약하게 터뜨림 (키를 눌렀는데 아무 일도 없는 상황 방지)
[DisallowMultipleComponent]
public class CurseBurstSkill : ThirdJobSkill
{
    [Header("저주 폭발")]
    [SerializeField, Min(1)] private int damage = 32;
    [SerializeField, Min(0f)] private float splashRadius = 1.6f;
    [SerializeField, Min(1)] private int splashDamage = 12;
    [Tooltip("표식이 조여드는 시간(초)")]
    [SerializeField, Min(0f)] private float chargeTime = 0.2f;
    [Tooltip("다음 표식이 터지기까지 간격(초)")]
    [SerializeField, Min(0f)] private float chainInterval = 0.06f;

    [Header("표식이 없을 때")]
    [Tooltip("표식이 없을 때 터뜨릴 가까운 적 수")]
    [SerializeField, Min(1)] private int noMarkTargets = 3;
    [Tooltip("표식이 없을 때 피해 배율")]
    [SerializeField, Range(0.1f, 1f)] private float noMarkDamageScale = 0.6f;

    public override ArcherBranch Branch => ArcherBranch.Shadow;

    private void Reset() { key = KeyCode.X; cooldown = 8f; }

    protected override bool Cast()
    {
        List<EnemyHealth> onScreen = JobTierUtil.OnScreen(Enemies, Origin);
        List<EnemyHealth> marked = onScreen.FindAll(ShadowMark.Has);
        bool hasMarks = marked.Count > 0;
        if (!hasMarks)
            marked = onScreen.GetRange(0, Mathf.Min(noMarkTargets, onScreen.Count));

        SkillFx.Burst(Origin, 0.4f, 2.4f, JobTierUtil.ShadowColor, 0.25f, true);
        if (marked.Count > 0)
            StartCoroutine(Chain(marked, hasMarks ? 1f : noMarkDamageScale, hasMarks ? 1f : 0.75f));
        return true;
    }

    private IEnumerator Chain(List<EnemyHealth> targets, float damageScale, float size)
    {
        float r = splashRadius * size;
        // 1) 터질 적 위에 링이 조여듦
        foreach (EnemyHealth e in targets)
        {
            if (e == null) continue;
            SkillFx.Burst(e.transform.position, r * 2.6f, 0.3f, JobTierUtil.ShadowColor, chargeTime, true);
            JobTierUtil.FlashLine(Origin, e.transform.position, new Color(0.6f, 0.3f, 0.9f, 0.7f), 0.05f, chargeTime, false);
        }
        if (chargeTime > 0f) yield return new WaitForSeconds(chargeTime);

        // 2) 가까운 순서로 연달아 폭발
        foreach (EnemyHealth enemy in targets)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            Vector2 pos = enemy.transform.position;
            int main = Mathf.Max(1, Mathf.RoundToInt(Dmg(damage, enemy) * damageScale));
            int splash = Mathf.Max(1, Mathf.RoundToInt(Dmg(splashDamage, enemy) * damageScale));
            ShadowMark.Consume(enemy);

            List<EnemyHealth> near = new List<EnemyHealth>();
            foreach (EnemyHealth other in SkillUtil.Enemies(Enemies))
                if (other != null && other != enemy && Vector2.Distance(pos, other.transform.position) <= r)
                    near.Add(other);

            JobTierUtil.Hit(enemy, main);
            foreach (EnemyHealth other in near) JobTierUtil.Hit(other, splash);

            SkillFx.Burst(pos, 0.4f, r * 2f, new Color(0.25f, 0.05f, 0.4f, 0.8f), 0.35f);
            SkillFx.Burst(pos, 0.3f, r * 2.4f, JobTierUtil.ShadowColor, 0.35f, true);
            for (int i = 0; i < 4; i++) // 사방으로 튀는 가시
            {
                Vector2 d = SkillUtil.Rotate(Vector2.up, i * 90f + 45f);
                JobTierUtil.FlashLine(pos, pos + d * r * 1.1f, JobTierUtil.ShadowColor, 0.08f, 0.2f, false);
            }
            if (chainInterval > 0f) yield return new WaitForSeconds(chainInterval);
        }
    }
}
