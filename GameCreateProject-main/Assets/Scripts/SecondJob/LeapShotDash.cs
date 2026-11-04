using UnityEngine;

// [2차 대쉬] 도약 사격 : 팀 대쉬(Shift)를 쓰면 몹을 뛰어넘듯 높이 도약하고,
// 착지하면 화살 3발을 탕-탕-탕 연달아 쏨. (대쉬 이동·충전·몹 통과는 팀 대쉬 그대로)
//  - 화살마다 쏘는 순간 "가장 가까운 살아있는 적"을 노림 (대쉬 방향 앞쪽 우선)
//    → 앞 발로 죽을 적은 건너뛰고 다음 발은 그다음 가까운 적에게 (바로 그쪽으로 쏨)
//  - 날아가는 중에 목표가 죽으면 화살 위치에서 가장 가까운 다른 적으로 갈아탐 (약한 유도)
//  - 착지 직후 잠깐 무적 (몹 무리 위에 내려도 바로 안 맞음)
// 2차 전직하면 켜지고, 3차 전직하면 3차 대쉬 효과로 바뀝니다.
[DisallowMultipleComponent]
public class LeapShotDash : DashAddon, ISecondJobSkill
{
    [Header("도약")]
    [Tooltip("도약 높이 (그림만 올라감, 실제 위치는 그대로)")]
    [SerializeField, Min(0f)] private float leapHeight = 0.9f;
    [Tooltip("도약 중 커지는 정도")]
    [SerializeField, Range(0f, 0.6f)] private float leapScale = 0.2f;
    [Tooltip("대쉬 시간(초) — 팀 PlayerMovement의 Dash Duration과 맞추면 자연스러움")]
    [SerializeField, Min(0.05f)] private float leapSeconds = 0.2f;

    [Header("착지 사격")]
    [SerializeField, Range(1, 9)] private int arrowCount = 3;
    [Tooltip("화살 사이 간격(초). 0이면 한 번에")]
    [SerializeField, Min(0f)] private float shotInterval = 0.1f;
    [Tooltip("적이 하나도 없을 때 대쉬 방향으로 퍼지는 각도")]
    [SerializeField, Range(0f, 45f)] private float arrowSpread = 12f;
    [SerializeField, Min(1)] private int arrowDamage = 6;
    [SerializeField, Min(1f)] private float arrowSpeed = 16f;
    [Tooltip("유도 세기: 1초에 꺾는 각도 (0 = 유도 없음)")]
    [SerializeField, Min(0f)] private float homingTurnRate = 240f;
    [Tooltip("대쉬 방향 앞쪽 적을 우선 조준 (앞쪽으로 인정하는 각도의 절반)")]
    [SerializeField, Range(0f, 180f)] private float forwardAngle = 70f;

    [Header("착지 무적")]
    [Tooltip("착지 후 무적 시간(초). 0이면 없음")]
    [SerializeField, Min(0f)] private float landingInvincible = 0.25f;

    private static readonly System.Reflection.FieldInfo NextDamageField = typeof(PlayerHealth).GetField("nextDamageTime",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    private SpriteRenderer body;      // 원래 캐릭터 그림
    private SpriteRenderer leapView;  // 도약 중 대신 보여줄 그림
    private SpriteRenderer shadow;    // 바닥 그림자
    private Vector2 lastDir = Vector2.right;

    protected override void OnDashStart(Vector2 direction, Vector2 from)
    {
        lastDir = direction;
        if (body == null) body = FindBody();
        if (body == null) return;

        if (leapView == null)
        {
            leapView = new GameObject("LeapView").AddComponent<SpriteRenderer>();
            leapView.transform.SetParent(transform, false);
            shadow = new GameObject("LeapShadow").AddComponent<SpriteRenderer>();
            shadow.transform.SetParent(transform, false);
            shadow.sprite = SkillUtil.CircleSprite();
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
        }
        leapView.sortingLayerID = body.sortingLayerID;
        leapView.sortingOrder = body.sortingOrder + 1;
        shadow.sortingLayerID = body.sortingLayerID;
        shadow.sortingOrder = body.sortingOrder - 1;
        leapView.gameObject.SetActive(true);
        shadow.gameObject.SetActive(true);
        body.enabled = false;
        UpdateLeap();
        SkillFx.Burst(from, 0.3f, 1.2f, new Color(0.75f, 0.95f, 1f, 0.5f), 0.2f, true);
    }

    protected override void OnDashTick(Vector2 from, Vector2 to)
    {
        UpdateLeap();
    }

    protected override void OnDashEnd(Vector2 at, Vector2 from)
    {
        EndLeap();
        GiveLandingInvincibility();

        // 착지 사격: 한 발씩 연달아, 쏘는 순간의 가장 가까운 적에게
        StartCoroutine(FireVolley());
        SkillFx.Burst(at, 0.3f, 1.6f, new Color(0.75f, 0.95f, 1f, 0.6f), 0.22f, true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EndLeap();
    }

    // 이번 사격에서 이미 날아가고 있는 화살의 피해 (이 피해로 죽을 적은 다음 화살이 건너뜀)
    private readonly System.Collections.Generic.Dictionary<EnemyHealth, int> reserved = new System.Collections.Generic.Dictionary<EnemyHealth, int>();

    private System.Collections.IEnumerator FireVolley()
    {
        reserved.Clear();
        for (int i = 0; i < arrowCount; i++)
        {
            Vector2 at = transform.position;
            EnemyHealth target = PickNearest(at, lastDir);
            int damage = SkillModifiers.Damage(arrowDamage);
            if (target != null)
                reserved[target] = (reserved.TryGetValue(target, out int r) ? r : 0) + damage;
            Vector2 dir;
            if (target != null) dir = ((Vector2)target.transform.position - at).normalized;
            else dir = SkillUtil.Rotate(lastDir, arrowCount == 1 ? 0f : Mathf.Lerp(-arrowSpread, arrowSpread, i / (arrowCount - 1f)));
            SkillProjectile p = SkillProjectile.Spawn(arrowSprite, arrowColor, at, dir,
                arrowSpeed, aimRange, damage, 0.2f, false, false, Enemies);
            if (homingTurnRate > 0f) p.SetHoming(target, homingTurnRate, true); // 목표가 죽으면 가까운 다른 적으로
            if (shotInterval > 0f && i < arrowCount - 1) yield return new WaitForSeconds(shotInterval);
        }
    }

    // 가장 가까운 "살아있는" 적 (대쉬 방향 앞쪽 ±forwardAngle 우선, 없으면 전체에서)
    private EnemyHealth PickNearest(Vector2 at, Vector2 forward)
    {
        System.Collections.Generic.List<EnemyHealth> all = SkillUtil.EnemiesNow(Enemies);
        float cosLimit = Mathf.Cos(forwardAngle * Mathf.Deg2Rad);
        EnemyHealth front = null, any = null;
        float frontD = aimRange, anyD = aimRange;
        for (int i = 0; i < all.Count; i++)
        {
            EnemyHealth e = all[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            if (DamageTracker.TryReadHp(e, out int hp))
            {
                if (hp <= 0) continue; // 방금 죽은 적
                if (reserved.TryGetValue(e, out int coming) && coming >= hp) continue; // 앞 화살이 죽일 적
            }
            Vector2 to = (Vector2)e.transform.position - at;
            float d = to.magnitude;
            if (d > aimRange) continue;
            if (d < anyD) { anyD = d; any = e; }
            if (d > 0.0001f && Vector2.Dot(to / d, forward) >= cosLimit && d < frontD) { frontD = d; front = e; }
        }
        return front != null ? front : any;
    }

    // 착지 직후 잠깐 피해를 안 받게 (팀 PlayerHealth의 무적 시간만 늘림)
    private void GiveLandingInvincibility()
    {
        if (landingInvincible <= 0f || NextDamageField == null) return;
        PlayerHealth health = GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead) return;
        float until = Time.time + landingInvincible;
        if ((float)NextDamageField.GetValue(health) < until) NextDamageField.SetValue(health, until);
    }

    private void UpdateLeap()
    {
        if (body == null || leapView == null) return;
        float t = Mathf.Clamp01(DashElapsed / leapSeconds);
        float arc = Mathf.Sin(t * Mathf.PI);              // 0 → 1 → 0
        Transform bt = body.transform;
        leapView.sprite = body.sprite;
        leapView.flipX = body.flipX;
        leapView.flipY = body.flipY;
        leapView.color = body.color;
        leapView.transform.position = bt.position + Vector3.up * (leapHeight * arc);
        leapView.transform.rotation = bt.rotation;
        leapView.transform.localScale = DivideScale(bt.lossyScale * (1f + leapScale * arc));

        float w = Mathf.Max(0.3f, body.bounds.size.x) * (1f - 0.35f * arc);
        shadow.transform.position = new Vector3(bt.position.x, body.bounds.min.y, bt.position.z);
        shadow.transform.localScale = DivideScale(new Vector3(w, w * 0.35f, 1f));
    }

    private void EndLeap()
    {
        if (body != null) body.enabled = true;
        if (leapView != null) leapView.gameObject.SetActive(false);
        if (shadow != null) shadow.gameObject.SetActive(false);
    }

    // 부모(Player) 크기를 빼서 월드 크기가 원하는 값이 되도록
    private Vector3 DivideScale(Vector3 world)
    {
        Vector3 p = transform.lossyScale;
        return new Vector3(world.x / Mathf.Max(0.0001f, p.x), world.y / Mathf.Max(0.0001f, p.y), 1f);
    }

    // 캐릭터 그림 찾기 (연출용 그림 제외, 켜져 있고 그림이 있는 것 중 가장 큰 것)
    private SpriteRenderer FindBody()
    {
        SpriteRenderer best = null;
        float bestArea = 0f;
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr == leapView || sr == shadow || !sr.enabled || sr.sprite == null) continue;
            float area = sr.bounds.size.x * sr.bounds.size.y;
            if (area > bestArea) { bestArea = area; best = sr; }
        }
        return best;
    }
}
