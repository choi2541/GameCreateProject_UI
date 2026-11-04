using System.Collections;
using UnityEngine;

// [3차 그림자 C] 암흑 저격 : 1초 조준 후 화면을 가로지르는 일직선 저격. 표식 붙은 적은 피해 2배
[DisallowMultipleComponent]
public class DarkSnipeSkill : ThirdJobSkill
{
    [Header("암흑 저격")]
    [SerializeField, Min(0.1f)] private float aimTime = 1f;
    [SerializeField, Min(1)] private int damage = 60;
    [SerializeField, Min(1f)] private float markedMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float beamWidth = 0.6f;
    [SerializeField, Min(5f)] private float beamLength = 30f;

    public override ArcherBranch Branch => ArcherBranch.Shadow;

    private void Reset() { key = KeyCode.C; cooldown = 14f; aimRange = 14f; }

    protected override bool Cast()
    {
        StartCoroutine(Snipe());
        return true;
    }

    private IEnumerator Snipe()
    {
        LineRenderer aimLine = JobTierUtil.NewLine("SnipeAim", new Color(0.85f, 0.5f, 1f, 0.3f), 0.04f);
        float t = 0f;
        Vector2 dir = AimDirection();
        while (t < aimTime)
        {
            t += Time.deltaTime;
            dir = AimDirection(); // 조준 중에는 가장 가까운 적을 따라감
            float k = t / aimTime;
            aimLine.startColor = aimLine.endColor = new Color(0.85f, 0.5f, 1f, 0.3f + 0.6f * k);
            aimLine.startWidth = aimLine.endWidth = 0.03f + 0.06f * k;
            JobTierUtil.SetStraight(aimLine, Origin, Origin + dir * beamLength);
            yield return null;
        }
        Destroy(aimLine.gameObject);

        Vector2 from = Origin;
        Vector2 to = from + dir * beamLength;
        JobTierUtil.FlashLine(from, to, new Color(0.3f, 0.05f, 0.45f, 0.9f), beamWidth * 2.2f, 0.35f, false);
        JobTierUtil.FlashLine(from, to, new Color(0.97f, 0.88f, 1f, 1f), beamWidth, 0.35f, false);
        SkillFx.Burst(from, 0.4f, 2.2f, JobTierUtil.ShadowColor, 0.3f, true);

        foreach (EnemyHealth enemy in SkillUtil.Enemies(Enemies))
        {
            Vector2 v = (Vector2)enemy.transform.position - from;
            float along = Vector2.Dot(v, dir);
            float side = Mathf.Abs(v.x * dir.y - v.y * dir.x);
            if (along < 0f || along > beamLength || side > beamWidth * 0.5f + SkillUtil.EnemyRadius)
                continue;
            bool marked = ShadowMark.Has(enemy);
            int dmg = Dmg(damage, enemy);
            if (marked) { dmg = Mathf.RoundToInt(dmg * markedMultiplier); ShadowMark.Consume(enemy); }
            JobTierUtil.Hit(enemy, dmg);
            SkillFx.Burst(enemy.transform.position, 0.3f, 1.3f, marked ? new Color(1f, 0.5f, 0.85f) : JobTierUtil.ShadowColor, 0.25f, true);
        }
    }
}
