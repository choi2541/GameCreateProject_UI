using UnityEngine;

// [3차 폭풍 X] 회오리 : 가까운 적 위치(없으면 앞)에 회오리를 만들어 적을 빨아들이며 지속 피해
[DisallowMultipleComponent]
public class WindVortexSkill : ThirdJobSkill
{
    [Header("회오리")]
    [SerializeField, Min(0.5f)] private float duration = 2.5f;
    [SerializeField, Min(0.5f)] private float pullRadius = 2.4f;
    [SerializeField, Min(0.5f)] private float pullSpeed = 3f;
    [SerializeField, Min(1)] private int tickDamage = 5;
    [SerializeField, Min(0f)] private float defaultDistance = 3f;

    public override ArcherBranch Branch => ArcherBranch.Storm;

    private void Reset() { key = KeyCode.X; cooldown = 9f; aimRange = 8f; }

    protected override bool Cast()
    {
        EnemyHealth target = SkillUtil.Nearest(Enemies, Origin, aimRange);
        Vector2 at = target != null ? (Vector2)target.transform.position : Origin + LastMoveDirection * defaultDistance;
        WindVortex v = WindVortex.Spawn(at, 1f, Enemies);
        v.lifetime = duration;
        v.radius = pullRadius;
        v.pullSpeed = pullSpeed;
        v.tickDamage = Dmg(tickDamage);
        SkillFx.Burst(at, 0.5f, pullRadius * 2f, new Color(0.7f, 1f, 0.9f, 0.6f), 0.35f, true);
        return true;
    }
}
