using System.Collections;
using UnityEngine;

// [3차 그림자 SHIFT] 그림자 걸음 : 팀 대쉬(Shift)를 쓰면 출발한 자리에 그림자 분신이 남아 2초간 같이 사격
// (대쉬 이동·충전·몹 통과는 팀 대쉬 그대로. 3차 그림자 전직 시 켜짐)
[DisallowMultipleComponent]
public class ShadowStepDash : DashAddon, IThirdJobSkill
{
    [Header("그림자 걸음")]
    [SerializeField, Min(0.2f)] private float cloneSeconds = 2f;
    [SerializeField, Min(0.05f)] private float cloneFireInterval = 0.3f;
    [SerializeField, Min(1)] private int cloneDamage = 7;
    [Tooltip("AD 계수: 힘 보너스를 몇 배로 받을지 (1 = 그대로)")]
    [SerializeField, Min(0f)] private float statRatio = 1f;

    public ArcherBranch Branch => ArcherBranch.Shadow;

    private void Reset() { arrowColor = JobTierUtil.ShadowColor; }

    protected override void OnDashStart(Vector2 direction, Vector2 from)
    {
        StartCoroutine(Clone(from));
        SkillFx.Burst(from, 0.3f, 1.3f, new Color(0.35f, 0.1f, 0.55f, 0.6f), 0.25f);
    }

    protected override void OnDashTick(Vector2 from, Vector2 to)
    {
        // 지나간 길에 보라색 잔상
        JobTierUtil.FlashLine(from, to, new Color(0.35f, 0.1f, 0.55f, 0.55f), 0.35f, 0.25f, false);
    }

    private IEnumerator Clone(Vector2 at)
    {
        GameObject clone = ShadowClone.Create(gameObject, at);
        float t = 0f, next = 0.1f;
        while (t < cloneSeconds && clone != null)
        {
            t += Time.deltaTime;
            if (t >= next)
            {
                next += cloneFireInterval;
                EnemyHealth target = SkillUtil.Nearest(Enemies, at, aimRange);
                if (target != null)
                {
                    Vector2 d = ((Vector2)target.transform.position - at).normalized;
                    SkillProjectile.Spawn(arrowSprite, arrowColor, at, d, 18f, aimRange,
                        JobTierUtil.Damage(cloneDamage, Branch, target, statRatio), 0.25f, false, false, Enemies);
                }
            }
            yield return null;
        }
        if (clone != null) ShadowClone.FadeOut(clone, 0.3f);
    }
}
