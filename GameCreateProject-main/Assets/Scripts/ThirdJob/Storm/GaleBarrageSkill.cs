using System.Collections;
using UnityEngine;

// [3차 폭풍 Z] 질풍 연사 : 2초 동안 가까운 적에게 바람 화살을 쏟아냄
[DisallowMultipleComponent]
public class GaleBarrageSkill : ThirdJobSkill
{
    [Header("질풍 연사")]
    [SerializeField, Min(0.2f)] private float duration = 2f;
    [SerializeField, Min(0.02f)] private float interval = 0.07f;
    [SerializeField, Range(0f, 45f)] private float spread = 12f;
    [SerializeField, Min(1)] private int damage = 4;
    [SerializeField, Min(1f)] private float speed = 20f;

    public override ArcherBranch Branch => ArcherBranch.Storm;

    private void Reset() { key = KeyCode.Z; cooldown = 7f; arrowColor = JobTierUtil.StormColor; }

    protected override bool Cast()
    {
        StartCoroutine(Barrage());
        return true;
    }

    private IEnumerator Barrage()
    {
        float t = 0f;
        while (t < duration)
        {
            Vector2 dir = SkillUtil.Rotate(AimDirection(), Random.Range(-spread, spread));
            SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, dir, speed, aimRange + 2f,
                Dmg(damage), 0.22f, false, false, Enemies, 1.1f);
            yield return new WaitForSeconds(interval);
            t += interval;
        }
    }
}
