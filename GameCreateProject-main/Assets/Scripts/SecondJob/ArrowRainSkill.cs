using System.Collections;
using UnityEngine;

// [2차 X] 화살비 (임시, 미스포츈 E 느낌)
// 가까운 적 위치(없으면 앞쪽)에 원형 장판을 깔고, 그 안에 일정 시간 화살이 쏟아지며 계속 피해.
[DisallowMultipleComponent]
public class ArrowRainSkill : ArcherSkillBase, ISecondJobSkill
{
    [Header("화살비")]
    [SerializeField, Min(0.5f)] private float radius = 2.5f;
    [SerializeField, Min(0.2f)] private float duration = 2.5f;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.25f;
    [SerializeField, Min(1)] private int damagePerTick = 2;
    [Tooltip("한 번에 떨어지는 화살 연출 개수")]
    [SerializeField, Range(1, 20)] private int arrowsPerTick = 6;
    [Tooltip("떨어지는 화살 이미지 크기. 장판 범위와 피해량은 바뀌지 않습니다.")]
    [SerializeField, Min(0.01f)] private float arrowScale = 0.7f;
    [Tooltip("적이 없을 때 앞쪽 몇 칸에 깔지")]
    [SerializeField, Min(0f)] private float defaultDistance = 3f;
    [SerializeField] private Color zoneColor = new Color(1f, 0.55f, 0.25f, 0.22f);

    private GameObject zone;

    private void Reset()
    {
        key = KeyCode.X;
        cooldown = 8f;
        aimRange = 7f;
    }

    protected override bool Cast()
    {
        StopAllCoroutines();
        EnemyHealth target = SkillUtil.Nearest(Enemies, Origin, aimRange);
        Vector2 center = target != null ? (Vector2)target.transform.position
            : Origin + LastMoveDirection * defaultDistance;
        StartCoroutine(Rain(center));
        return true;
    }

    private IEnumerator Rain(Vector2 center)
    {
        if (zone != null) Destroy(zone);
        zone = CreateZone(center);
        SkillFx.Burst(center, radius * 2f, radius * 2.3f, new Color(1f, 0.7f, 0.3f, 0.7f), 0.3f, true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            while (Time.timeScale <= 0f && !OwnerDead) yield return null;
            if (OwnerDead) break;
            for (int i = 0; i < arrowsPerTick; i++)
            {
                Vector2 spot = center + Random.insideUnitCircle * radius;
                SkillFx.Fall(arrowSprite, spot, 1.6f, arrowColor, 0.14f, Mathf.Max(0.01f, arrowScale));
            }
            SkillUtil.DamageInRadius(Enemies, center, radius, SkillModifiers.Damage(damagePerTick));

            float wait = Mathf.Min(Mathf.Max(0.05f, tickInterval), duration - elapsed);
            yield return new WaitForSeconds(wait);
            elapsed += wait;
        }

        if (zone != null) Destroy(zone);
    }

    private GameObject CreateZone(Vector2 center)
    {
        GameObject obj = new GameObject("ArrowRainZone");
        obj.transform.position = center;
        obj.transform.localScale = Vector3.one * radius * 2f;

        SpriteRenderer fill = obj.AddComponent<SpriteRenderer>();
        fill.sprite = SkillUtil.CircleSprite();
        fill.color = zoneColor;
        fill.sortingOrder = 5;

        GameObject edge = new GameObject("Edge");
        edge.transform.SetParent(obj.transform, false);
        SpriteRenderer ring = edge.AddComponent<SpriteRenderer>();
        ring.sprite = SkillUtil.RingSprite();
        ring.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.8f);
        ring.sortingOrder = 6;
        return obj;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (zone != null) Destroy(zone);
    }
}
