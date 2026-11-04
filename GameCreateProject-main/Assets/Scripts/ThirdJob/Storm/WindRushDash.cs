using System.Collections.Generic;
using UnityEngine;

// [3차 폭풍 SHIFT] 바람 돌진 : 팀 대쉬(Shift)로 지나가는 길의 적에게 피해 + 양옆으로 밀쳐냄
// (대쉬 이동·충전·몹 통과는 팀 대쉬 그대로. 3차 폭풍 전직 시 켜짐)
[DisallowMultipleComponent]
public class WindRushDash : DashAddon, IThirdJobSkill
{
    [Header("바람 돌진")]
    [SerializeField, Min(0.2f)] private float hitRadius = 1f;
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0f)] private float knockback = 8f;
    [Tooltip("AP 계수: 스킬 피해력 보너스를 몇 배로 받을지 (1 = 그대로)")]
    [SerializeField, Min(0f)] private float statRatio = 1f;

    public ArcherBranch Branch => ArcherBranch.Storm;

    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private Vector2 side;

    protected override void OnDashStart(Vector2 direction, Vector2 from)
    {
        hit.Clear();
        side = new Vector2(-direction.y, direction.x);
        SkillFx.Burst(from, 0.3f, 1.4f, new Color(0.7f, 1f, 0.9f, 0.6f), 0.2f, true);
    }

    protected override void OnDashTick(Vector2 from, Vector2 to)
    {
        // 바람 줄기 잔상
        JobTierUtil.FlashLine(from + side * 0.3f, to + side * 0.3f, new Color(0.7f, 1f, 0.9f, 0.5f), 0.08f, 0.25f, false);
        JobTierUtil.FlashLine(from - side * 0.3f, to - side * 0.3f, new Color(0.7f, 1f, 0.9f, 0.5f), 0.08f, 0.25f, false);

        List<EnemyHealth> all = SkillUtil.EnemiesNow(Enemies);
        for (int i = 0; i < all.Count; i++)
        {
            EnemyHealth e = all[i];
            if (e == null || !e.isActiveAndEnabled || hit.Contains(e)) continue;
            if (DistanceToSegment(e.transform.position, from, to) > hitRadius) continue;
            hit.Add(e);
            JobTierUtil.Hit(e, JobTierUtil.Damage(damage, Branch, e, statRatio));
            float s = Vector2.Dot((Vector2)e.transform.position - to, side) >= 0f ? 1f : -1f;
            JobTierUtil.Knockback(e, side * s * knockback);
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        if (len < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
        return Vector2.Distance(p, a + ab * t);
    }
}
