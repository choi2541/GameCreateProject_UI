using System;
using System.Collections.Generic;
using UnityEngine;

// 스킬용 화살. 직선으로 날아가며 적에게 피해를 줍니다.
//  - pierce      : 적을 관통하는지 (관통이면 같은 적은 한 번만 맞음)
//  - ignoreWalls : 돌/나무를 무시하는지 (궁극기용)
//  - OnHit       : 적에게 맞을 때 실행 (예: 폭발, 분열). 맞은 위치와 적을 넘겨줌
// 사거리를 넘거나 카메라 밖으로 나가면 사라집니다.
public class SkillProjectile : MonoBehaviour
{
    public Action<SkillProjectile, Vector2, EnemyHealth> OnHit;

    public Vector2 Direction => direction;
    public void InheritHits(SkillProjectile parent)
    {
        if (parent != null) alreadyHit.UnionWith(parent.alreadyHit);
    }

    // 이 적은 맞히지 않음 (분열된 화살이 방금 맞은 적에게 바로 다시 맞는 것 방지)
    // 약한 유도: target 쪽으로 1초에 turnRate도까지 방향을 꺾음 (target이 죽으면 직진)
    public void SetHoming(EnemyHealth target, float turnRate, bool retarget = false)
    {
        homingTarget = target;
        homingTurnRate = turnRate;
        homingRetarget = retarget;
    }

    // 목표가 죽었을 때: 화살 위치에서 가장 가까운, 아직 안 맞힌 살아있는 적 (남은 사거리 안)
    // 날아가는 방향 앞쪽(±90도) 적을 먼저 고름 → 뒤쪽 적 때문에 크게 휘어 도는 것 방지
    private void Retarget(Vector2 from)
    {
        homingTarget = null;
        float limit = Mathf.Max(0.5f, range - travelled);
        float best = float.MaxValue;
        System.Collections.Generic.List<EnemyHealth> list = SkillUtil.EnemiesNow(enemies);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth e = list[i];
            if (e == null || !e.isActiveAndEnabled || alreadyHit.Contains(e)) continue;
            if (DamageTracker.TryReadHp(e, out int hp) && hp <= 0) continue;
            Vector2 to = (Vector2)e.transform.position - from;
            float d = to.magnitude;
            if (d > limit) continue;
            float score = Vector2.Dot(to, direction) >= 0f ? d : d + 1000f; // 앞쪽 우선
            if (score < best) { best = score; homingTarget = e; }
        }
    }

    public void Ignore(EnemyHealth enemy)
    {
        if (enemy != null)
            alreadyHit.Add(enemy);
    }

    private Vector2 direction;
    private EnemyHealth homingTarget;
    private float homingTurnRate;   // 1초에 꺾을 수 있는 각도
    private bool homingRetarget;    // 목표가 죽으면 가까운 다른 적으로 갈아타기
    private float nextRetarget;
    private float speed;
    private float range;
    private float travelled;
    private float radius;
    private int damage;
    private bool pierce;
    private bool ignoreWalls;
    private int obstacleMask;
    private Transform enemies;
    private PlayerHealth owner;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>();
    private readonly List<EnemyHit> enemyHits = new List<EnemyHit>();
    private struct EnemyHit { public EnemyHealth enemy; public float distance; }
    private readonly HashSet<EnemyHealth> alreadyHit = new HashSet<EnemyHealth>();

    public static SkillProjectile Spawn(Sprite sprite, Color color, Vector2 position, Vector2 direction,
        float speed, float range, int damage, float radius, bool pierce, bool ignoreWalls,
        Transform enemyContainer, float scale = 1f, int sortingOrder = 60)
    {
        GameObject obj = new GameObject("SkillArrow");
        obj.transform.position = position;
        obj.transform.rotation = SkillUtil.RotationFor(direction);
        obj.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : SkillUtil.ArrowSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        SkillProjectile p = obj.AddComponent<SkillProjectile>();
        p.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        p.speed = speed;
        p.range = range;
        p.damage = damage;
        p.radius = radius;
        p.pierce = pierce;
        p.ignoreWalls = ignoreWalls;
        p.obstacleMask = SkillUtil.ObstacleMask;
        p.enemies = enemyContainer;
        p.owner = PlayerProgression.Instance != null
            ? PlayerProgression.Instance.GetComponent<PlayerHealth>() : null;
        return p;
    }

    private void Update()
    {
        if (owner != null && owner.IsDead) { Destroy(gameObject); return; }
        float step = Mathf.Min(Mathf.Max(0f, range - travelled), speed * Time.deltaTime);
        if (step <= 0f)
            return; // 일시정지 중

        if (homingRetarget && homingTurnRate > 0f && Time.time >= nextRetarget
            && (homingTarget == null || !homingTarget.isActiveAndEnabled
                || (DamageTracker.TryReadHp(homingTarget, out int targetHp) && targetHp <= 0)))
        {
            nextRetarget = Time.time + 0.1f;
            Retarget(transform.position);
        }

        if (homingTarget != null && homingTarget.isActiveAndEnabled && homingTurnRate > 0f)
        {
            Vector2 want = (Vector2)homingTarget.transform.position - (Vector2)transform.position;
            if (want.sqrMagnitude > 0.0001f)
            {
                float angle = Vector2.SignedAngle(direction, want);
                float max = homingTurnRate * Time.deltaTime;
                direction = SkillUtil.Rotate(direction, Mathf.Clamp(angle, -max, max)).normalized;
                transform.rotation = SkillUtil.RotationFor(direction);
            }
        }

        Vector2 start = transform.position;
        float wallDistance = float.PositiveInfinity;
        if (!ignoreWalls)
        {
            if (SkillUtil.HitsObstacle(start, radius * 0.5f, obstacleMask)) wallDistance = 0f;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(obstacleMask);
            filter.useTriggers = false;
            Physics2D.CircleCast(start, radius * 0.5f, direction, filter, wallHits, step);
            foreach (RaycastHit2D hit in wallHits)
                wallDistance = Mathf.Min(wallDistance, hit.distance);
        }

        enemyHits.Clear();
        List<EnemyHealth> list = SkillUtil.EnemiesNow(enemies);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth enemy = list[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled || alreadyHit.Contains(enemy))
                continue;
            float distance = SegmentEntryDistance(start, direction, step, enemy.transform.position,
                radius + SkillUtil.EnemyRadius);
            if (distance <= step && distance < wallDistance)
                enemyHits.Add(new EnemyHit { enemy = enemy, distance = distance });
        }
        enemyHits.Sort((a, b) => a.distance.CompareTo(b.distance));
        float moved = 0f;
        foreach (EnemyHit hit in enemyHits)
        {
            EnemyHealth enemy = hit.enemy;
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            Vector2 point = start + direction * hit.distance;
            transform.position = point;
            travelled += hit.distance - moved;
            moved = hit.distance;
            alreadyHit.Add(enemy);
            enemy.TakeDamage(damage);
            OnHit?.Invoke(this, point, enemy);
            if (!pierce)
            {
                Destroy(gameObject);
                return;
            }
            // 처치 경험치로 전직창이 열리면 남은 이동은 재개 후 처리합니다.
            if (Time.timeScale <= 0f) return;
        }
        float advance = Mathf.Min(step, wallDistance);
        Vector2 position = start + direction * advance;
        transform.position = position;
        travelled += advance - moved;
        if (wallDistance <= step) { Destroy(gameObject); return; }
        if (travelled >= range || !SkillUtil.IsInsideCamera(position, 2f))
            Destroy(gameObject);
    }

    // 기존 원형 적 판정 반경을 유지하면서 이동 구간의 첫 접촉점을 구합니다.
    internal static float SegmentEntryDistance(Vector2 start, Vector2 unitDirection, float length,
        Vector2 center, float combinedRadius)
    {
        Vector2 offset = center - start;
        float c = offset.sqrMagnitude - combinedRadius * combinedRadius;
        if (c <= 0f) return 0f;
        float along = Vector2.Dot(offset, unitDirection);
        float discriminant = along * along - c;
        if (along < 0f || discriminant < 0f) return float.PositiveInfinity;
        float entry = along - Mathf.Sqrt(discriminant);
        return entry <= length ? Mathf.Max(0f, entry) : float.PositiveInfinity;
    }
}
