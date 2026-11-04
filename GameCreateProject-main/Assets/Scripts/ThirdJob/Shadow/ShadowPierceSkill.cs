using System.Collections;
using UnityEngine;

// [3차 그림자 Z] 그림자 관통 : 관통 화살 3발을 부채꼴로 하나씩 쏘고, 맞은 적과 그 주변 적에게 저주 표식
// (적 체력이 낮으면 맞은 적은 바로 죽기 때문에, 주변으로 표식이 퍼져야 X로 터뜨릴 대상이 남습니다)
[DisallowMultipleComponent]
public class ShadowPierceSkill : ThirdJobSkill
{
    [Header("그림자 관통")]
    [SerializeField, Range(1, 8)] private int arrowCount = 3;
    [Tooltip("화살 사이 간격(초). 크게 하면 하나씩 나가는 게 더 잘 보임")]
    [SerializeField, Min(0.02f)] private float interval = 0.22f;
    [Tooltip("부채꼴 각도(도). 0이면 한 줄로 나감")]
    [SerializeField, Range(0f, 45f)] private float spreadAngle = 9f;
    [SerializeField, Min(1)] private int damage = 14;
    [SerializeField, Min(1f)] private float speed = 22f;
    [SerializeField, Min(1f)] private float range = 14f;
    [SerializeField, Min(0.5f)] private float arrowScale = 1.7f;
    [Tooltip("표식 유지 시간(초)")]
    [SerializeField, Min(0.5f)] private float markSeconds = 6f;
    [Tooltip("맞은 자리 주변으로 표식이 퍼지는 범위")]
    [SerializeField, Min(0f)] private float markSpreadRadius = 2f;

    public override ArcherBranch Branch => ArcherBranch.Shadow;

    private void Reset() { key = KeyCode.Z; cooldown = 5f; arrowColor = JobTierUtil.ShadowColor; }

    protected override bool Cast()
    {
        StartCoroutine(Fire());
        return true;
    }

    private IEnumerator Fire()
    {
        for (int i = 0; i < arrowCount; i++)
        {
            // 가운데 → 왼쪽 → 오른쪽 → ... 순서로 각도를 벌림
            float offset = 0f;
            if (i > 0) offset = ((i + 1) / 2) * spreadAngle * (i % 2 == 1 ? 1f : -1f);
            Vector2 dir = SkillUtil.Rotate(AimDirection(), offset);

            SkillProjectile p = SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, dir, speed, range,
                Dmg(damage), 0.3f, true, false, Enemies, arrowScale);
            p.OnHit += (proj, pos, enemy) =>
            {
                ShadowMark.Apply(enemy, markSeconds);
                if (markSpreadRadius > 0f)
                    ShadowMark.Spread(Enemies, pos, markSpreadRadius, markSeconds, enemy);
                SkillFx.Burst(pos, 0.3f, 1.1f, JobTierUtil.ShadowColor, 0.2f, true);
            };
            SkillFx.Burst(Origin, 0.3f, 1.1f, new Color(0.5f, 0.2f, 0.8f, 0.7f), 0.18f, true);
            yield return new WaitForSeconds(interval);
        }
    }
}
