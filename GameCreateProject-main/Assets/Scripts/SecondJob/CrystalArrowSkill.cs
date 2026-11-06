using UnityEngine;

// [2차 C] 가시 덩굴 화살 (궁극기)
// 큰 씨앗 화살이 돌/나무를 무시하고 날아가다가 첫 적에게 맞으면
//  ① 가시 덩굴이 터짐 (범위 피해 + 속박: 움직이지 못함, 초록빛)
//  ② 그 자리에서 씨앗 8개가 사방으로 튐              ← 1번째 갈라짐 (빠름)
//  ③ 씨앗이 적에게 맞으면 3갈래로 갈라짐             ← 2번째 갈라짐 (더 느림)
//  ④ 또 맞으면 다시 3갈래                            ← 3번째 갈라짐 (가장 느림, 여기서 끝)
// 그림: Resources/SkillFx/thorn_arrow, thorn_seed, thorn_burst (없으면 초록 원으로 대신)
// (클래스 이름은 씬 연결이 끊기지 않도록 CrystalArrowSkill 그대로 둠)
[DisallowMultipleComponent]
public class CrystalArrowSkill : ArcherSkillBase, ISecondJobSkill
{
    public const string DisplayName = "가시 덩굴 화살";
    private static readonly Color ThornColor = new Color(0.55f, 0.9f, 0.35f);   // 그림이 없을 때 화살 색
    private static readonly Color RootTint = new Color(0.55f, 1f, 0.45f);      // 속박된 적 색

    [Header("씨앗 화살")]
    [SerializeField, Min(1)] private int hitDamage = 25;
    [SerializeField, Min(1f)] private float speed = 11f;
    [SerializeField, Min(1f)] private float range = 30f;
    [SerializeField, Min(0.5f)] private float arrowScale = 3.2f;

    [Header("가시 덩굴 (폭발 + 속박)")]
    [SerializeField, Min(0.5f)] private float explosionRadius = 3f;
    [SerializeField, Min(0)] private int explosionDamage = 12;
    [SerializeField, Min(0f)] private float stunSeconds = 1.5f;

    [Header("튀는 씨앗 (1번째 갈라짐)")]
    [SerializeField, Range(0, 24)] private int scatterCount = 8;
    [SerializeField, Min(1)] private int scatterDamage = 5;
    [SerializeField, Min(1f)] private float scatterRange = 6f;
    [Tooltip("첫 분열탄 이미지 크기. 다음 단계는 이 값에 Split Scale Falloff를 곱합니다. 타격 판정은 그대로입니다.")]
    [SerializeField, Min(0.01f)] private float scatterScale = 0.9f;

    [Header("연쇄 분열 (맞을 때마다 갈라짐)")]
    [Tooltip("갈라지는 단계별 속도. 1번째(사방 8발) → 2번째 → 3번째. 최대 3단계까지 갈라짐")]
    [SerializeField] private float[] splitSpeeds = { 18f, 11f, 6.5f };
    [Tooltip("2번째부터 한 번 맞을 때 몇 갈래로 갈라지는지")]
    [SerializeField, Range(1, 6)] private int splitCount = 3;
    [Tooltip("갈라질 때 퍼지는 전체 각도")]
    [SerializeField, Range(0f, 120f)] private float splitAngle = 50f;
    [Tooltip("단계가 내려갈수록 화살 크기 배율")]
    [SerializeField, Range(0.3f, 1f)] private float splitScaleFalloff = 0.8f;

    private void Reset()
    {
        key = KeyCode.C;
        cooldown = 20f;
        aimRange = 16f;
        arrowColor = ThornColor;
    }

    protected override bool Cast()
    {
        Vector2 aim = AimDirection();
        SkillProjectile arrow = SkillProjectile.Spawn(arrowSprite, ThornColor, Origin, aim,
            speed, range, SkillModifiers.Damage(hitDamage), 0.6f, false, true, Enemies, arrowScale, 62);
        arrow.UseFrames("thorn_arrow", 12f).UseHitFx(null, 0f);
        arrow.OnHit = (projectile, point, enemy) => Explode(point, enemy, projectile);

        SkillFx.Burst(Origin, 0.5f, 2.2f, new Color(ThornColor.r, ThornColor.g, ThornColor.b, 0.7f), 0.3f, true);
        return true;
    }

    private void Explode(Vector2 point, EnemyHealth hitEnemy, SkillProjectile parent)
    {
        if (OwnerDead) return;
        // 가시 덩굴 그림 (범위 지름 크기). 없으면 초록 원
        if (!SkillFx.Anim("thorn_burst", point, explosionRadius * 2.2f, 0.6f, 0f, 63))
        {
            SkillFx.Burst(point, 0.5f, explosionRadius * 2f, new Color(0.35f, 0.75f, 0.25f, 0.55f), 0.45f);
            SkillFx.Burst(point, 0.5f, explosionRadius * 2.2f, new Color(0.85f, 1f, 0.6f, 0.9f), 0.35f, true);
        }

        foreach (EnemyHealth enemy in SkillUtil.Enemies(Enemies))
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            if (Vector2.Distance(point, enemy.transform.position) > explosionRadius + SkillUtil.EnemyRadius)
                continue;
            SkillStun.Apply(enemy, stunSeconds, RootTint); // 속박
            if (explosionDamage > 0)
                enemy.TakeDamage(SkillModifiers.Damage(explosionDamage));
        }

        // 1번째 갈라짐: 사방으로 scatterCount발
        for (int i = 0; i < scatterCount; i++)
        {
            Vector2 dir = SkillUtil.Rotate(Vector2.right, 360f / scatterCount * i);
            SpawnSplitArrow(point, dir, 0, parent);
        }
    }

    // stage: 0 = 1번째(가장 빠름), 1 = 2번째, 2 = 3번째(가장 느림)
    private void SpawnSplitArrow(Vector2 from, Vector2 dir, int stage, SkillProjectile parent)
    {
        if (OwnerDead || splitSpeeds == null || stage >= Mathf.Min(3, splitSpeeds.Length))
            return;

        float scale = Mathf.Max(0.01f, scatterScale) * Mathf.Pow(splitScaleFalloff, stage);
        Color color = Color.Lerp(ThornColor, Color.white, stage * 0.25f);
        SkillProjectile p = SkillProjectile.Spawn(arrowSprite, color, from, dir,
            splitSpeeds[stage], scatterRange, SkillModifiers.Damage(scatterDamage), 0.2f,
            false, false, Enemies, scale);
        p.UseFrames("thorn_seed", 12f).UseHitFx("arrow_hit", 0.7f); // 씨앗이 맞으면 잎 터짐
        p.InheritHits(parent);

        // 다음 단계가 있으면, 적에게 맞는 순간 splitCount갈래로 갈라짐
        if (stage + 1 < Mathf.Min(3, splitSpeeds.Length))
        {
            p.OnHit = (projectile, point, enemy) =>
            {
                if (OwnerDead) return;
                SkillFx.Burst(point, 0.2f, 0.9f, new Color(0.7f, 1f, 0.55f, 0.7f), 0.18f, true);
                for (int i = 0; i < splitCount; i++)
                {
                    float angle = splitCount == 1 ? 0f
                        : Mathf.Lerp(-splitAngle * 0.5f, splitAngle * 0.5f, i / (splitCount - 1f));
                    SpawnSplitArrow(point, SkillUtil.Rotate(projectile.Direction, angle), stage + 1, projectile);
                }
            };
        }
    }
}
