using UnityEngine;

// [3차 폭풍 C] 관통 돌풍 화살 : 거대한 바람 화살이 일직선으로 관통하며 맞은 적을 양옆으로 날려버림
[DisallowMultipleComponent]
public class PiercingGaleSkill : ThirdJobSkill
{
    [Header("관통 돌풍 화살")]
    [SerializeField, Min(1)] private int damage = 45;
    [SerializeField, Min(1f)] private float speed = 16f;
    [SerializeField, Min(1f)] private float range = 25f;
    [SerializeField, Min(0.5f)] private float arrowScale = 3.2f;
    [SerializeField, Min(0f)] private float knockback = 9f;

    public override ArcherBranch Branch => ArcherBranch.Storm;

    private void Reset() { key = KeyCode.C; cooldown = 12f; aimRange = 14f; arrowColor = JobTierUtil.StormColor; }

    protected override bool Cast()
    {
        Vector2 dir = AimDirection();
        SkillProjectile p = SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, dir, speed, range,
            Dmg(damage), 0.75f, true, true, Enemies, arrowScale);
        p.OnHit += (proj, pos, enemy) =>
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            float s = Vector2.Dot((Vector2)enemy.transform.position - pos, side) >= 0f ? 1f : -1f;
            JobTierUtil.Knockback(enemy, side * s * knockback + dir * 2f);
            SkillFx.Burst(enemy.transform.position, 0.3f, 1.2f, JobTierUtil.StormColor, 0.25f, true);
        };
        SkillFx.Burst(Origin, 0.5f, 2.4f, new Color(0.7f, 1f, 0.9f, 0.7f), 0.3f, true);
        return true;
    }
}
