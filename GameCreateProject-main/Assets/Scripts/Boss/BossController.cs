using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 보스 한 마리 (임시). BossDirector가 생성합니다.
//
// 체력: 팀의 EnemyHealth를 붙여서 공격에 맞게 하되, 아주 큰 "보호 체력(Guard)"을 깔아서
//       EnemyHealth가 스스로 죽이지 않게 하고, 매 프레임 깎인 만큼을 읽어 보스 체력에서 빼고 되돌립니다.
//       → 페이즈/무적/사망을 보스가 직접 관리
//
// 판정 (다른 파일 수정 없이 큰 몸 전체에 맞게 하기)
//   - 보스 본체: 콜라이더가 있어서 팀의 평타 등 물리 공격에 맞음 (적 목록에는 넣지 않음)
//   - 보이지 않는 판정점 1개: 적 목록(EnemySpawner)에 들어 있어서 스킬 화살·범위 공격에 맞음
//     평소엔 보스 중심에 있고, 보스 몸 안으로 들어온 스킬 화살이 있으면 그 화살 위치로 이동
//     → 화살은 같은 대상을 한 번만 맞히므로, 관통 화살도 보스에게는 딱 한 번만 피해
//
// 패턴 (모두 빨간 예고 표시 후 발동, 발동 순간 HP바 아래에 기술 이름 표시)
//   Slam    : 플레이어 위치에 원형 예고 → 내려찍기
//   Charge  : 플레이어 방향 직선 예고 → 돌진
//   Radial  : 사방으로 탄 발사
//   Barrage : 원형 예고 3개를 연속으로 (마지막 페이즈 전용)
[DefaultExecutionOrder(-100)] // 스킬 화살이 움직이기 전에 판정점을 옮겨둠
public class BossController : MonoBehaviour
{
    public enum Pattern { Slam, Charge, Radial, Barrage }

    private const int Guard = 1000000;

    public string DisplayName { get; private set; }
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Phase { get; private set; } = 1;
    public int PhaseCount { get; private set; } = 1;
    public float HitRadius { get; private set; } = 1f;
    public bool IsDead { get; private set; }
    public bool IsTransitioning { get; private set; }
    public int TierIndex => tier != null ? tier.tierIndex : -1;

    public event Action<string> SkillAnnounced;
    public event Action<int, string> PhaseChanged;
    public event Action<BossController> Died;

    private BossDirector.BossTier tier;
    private EnemyHealth health;
    private SpriteRenderer body;
    private PlayerHealth player;
    private float attackTimer;
    private bool busy;
    private Pattern lastPattern = (Pattern)(-1);
    private Coroutine attackRoutine;

    private EnemyHealth hitPoint;
    private readonly HashSet<SkillProjectile> served = new HashSet<SkillProjectile>();

    private float SpeedScale => 1f + 0.15f * (Phase - 1);
    private float TempoScale => 1f - 0.2f * (Phase - 1);
    private int AttackDamage => tier.attackDamage + (Phase >= 3 ? 1 : 0);

    public void Init(BossDirector.BossTier config, Sprite sprite, int layer, Transform enemyContainer)
    {
        tier = config;
        DisplayName = config.displayName;
        MaxHp = Mathf.Max(1, config.maxHealth);
        CurrentHp = MaxHp;
        PhaseCount = Mathf.Max(1, config.phases);
        HitRadius = 0.5f * config.scale;
        gameObject.layer = layer;
        transform.localScale = Vector3.one * config.scale;

        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = sprite != null ? sprite : SkillUtil.CircleSprite();
        body.color = sprite != null ? Color.white : config.color;
        body.sortingOrder = 40;

        // 플레이어 공격이 맞을 수 있도록 콜라이더 + 물리 몸체 (돌/나무는 통과)
        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;

        health = gameObject.AddComponent<EnemyHealth>();
        BossUtil.SetHp(health, Guard, Guard);

        // 보이지 않는 판정점 (적 목록 안에 둠)
        GameObject proxy = new GameObject("BossHitPoint");
        proxy.layer = layer;
        proxy.transform.position = transform.position;
        if (enemyContainer != null)
            proxy.transform.SetParent(enemyContainer, true);
        proxy.AddComponent<BossHitProxy>().Owner = this;
        hitPoint = proxy.AddComponent<EnemyHealth>();
        BossUtil.SetHp(hitPoint, Guard, Guard);

        player = BossUtil.Player();
        attackTimer = 1.5f;
    }

    private void Update()
    {
        if (IsDead || tier == null)
            return;
        if (player == null) player = BossUtil.Player();

        ReadHealth();
        if (IsDead)
            return;
        PlaceHitPoint();

        if (player != null && !player.IsDead)
        {
            // 몸통 접촉 피해
            if (Vector2.Distance(transform.position, player.transform.position) <= HitRadius + 0.3f)
                BossUtil.DamagePlayer(player, tier.contactDamage);

            if (!busy && !IsTransitioning)
            {
                ChasePlayer();
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                    attackRoutine = StartCoroutine(Attack());
            }
        }
    }

    private void ChasePlayer()
    {
        Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
        if (to.magnitude <= HitRadius + 0.6f)
            return;
        transform.position = (Vector2)transform.position + to.normalized * tier.moveSpeed * SpeedScale * Time.deltaTime;
    }

    // ───────── 체력 / 페이즈 / 사망 ─────────

    // 지난 프레임에 본체 + 판정점이 받은 피해를 합쳐서 반영
    private int TakeLoss(EnemyHealth target)
    {
        if (target == null || !BossUtil.TryGetHp(target, out int raw, out _))
            return 0;
        int loss = Guard - raw;
        if (loss != 0)
            BossUtil.SetHp(target, Guard, Guard);
        return Mathf.Max(0, loss);
    }

    private void ReadHealth()
    {
        int damage = TakeLoss(health) + TakeLoss(hitPoint);
        if (damage <= 0 || IsTransitioning)
            return; // 페이즈 전환 중에는 무적

        int hp = CurrentHp - damage;

        // 다음 페이즈 문턱 아래로 내려가면, 문턱에서 멈추고 페이즈 전환
        if (Phase < PhaseCount)
        {
            int threshold = Mathf.CeilToInt(MaxHp * (1f - (float)Phase / PhaseCount));
            if (hp <= threshold)
            {
                CurrentHp = Mathf.Max(1, threshold);
                StartPhaseTransition();
                return;
            }
        }

        CurrentHp = Mathf.Max(0, hp);
        if (CurrentHp <= 0)
            Die();
    }

    // 보스 몸 안에 들어온 스킬 화살 중 아직 판정점을 안 거친 것 위치로 판정점을 옮김. 없으면 중심.
    private void PlaceHitPoint()
    {
        if (hitPoint == null)
            return;

        Vector2 center = transform.position;
        Vector2 target = center;
        float reach = HitRadius + 0.3f;

        foreach (SkillProjectile p in FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None))
        {
            if (served.Contains(p))
                continue;
            if (Vector2.Distance(center, p.transform.position) <= reach)
            {
                target = p.transform.position;
                served.Add(p);
                break; // 한 프레임에 하나씩
            }
        }
        served.RemoveWhere(p => p == null);
        hitPoint.transform.position = target;
    }

    private void OnDestroy()
    {
        if (hitPoint != null)
            Destroy(hitPoint.gameObject);
    }

    private void StartPhaseTransition()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        StopAllCoroutines();
        busy = false;
        StartCoroutine(PhaseTransition());
    }

    private IEnumerator PhaseTransition()
    {
        IsTransitioning = true;
        Phase++;
        string title = tier.phaseNames != null && tier.phaseNames.Length >= Phase
            ? tier.phaseNames[Phase - 1] : $"{Phase}페이즈";
        PhaseChanged?.Invoke(Phase, title);

        Color from = body.color;
        Color to = Phase >= PhaseCount ? new Color(0.75f, 0.1f, 0.25f) : new Color(0.85f, 0.35f, 0.1f);
        for (int i = 0; i < 3; i++)
        {
            SkillFx.Burst(transform.position, HitRadius, HitRadius * 6f, new Color(1f, 0.3f, 0.2f, 0.6f), 0.5f, true);
            yield return new WaitForSeconds(0.5f);
        }
        if (body.sprite == SkillUtil.CircleSprite())
            body.color = Color.Lerp(from, to, 0.8f);

        IsTransitioning = false;
        attackTimer = 0.8f;
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        StopAllCoroutines();

        SkillFx.Burst(transform.position, HitRadius, HitRadius * 8f, new Color(1f, 0.85f, 0.4f, 0.8f), 0.8f);
        SkillFx.Burst(transform.position, HitRadius, HitRadius * 10f, new Color(1f, 1f, 1f, 0.9f), 0.6f, true);

        if (PlayerProgression.Instance != null && tier.expReward > 0)
            PlayerProgression.Instance.AddExperience(tier.expReward);
        GameSession.Instance?.RegisterKill();

        Died?.Invoke(this);
        Destroy(gameObject);
    }

    // ───────── 공격 패턴 ─────────

    private IEnumerator Attack()
    {
        busy = true;
        Pattern pattern = ChoosePattern();
        lastPattern = pattern;

        switch (pattern)
        {
            case Pattern.Slam: yield return Slam(); break;
            case Pattern.Charge: yield return Charge(); break;
            case Pattern.Radial: yield return Radial(); break;
            case Pattern.Barrage: yield return Barrage(); break;
        }

        attackTimer = tier.attackInterval * Mathf.Max(0.4f, TempoScale);
        busy = false;
    }

    private Pattern ChoosePattern()
    {
        List<Pattern> pool = new List<Pattern>(tier.patterns ?? new Pattern[0]);
        if (PhaseCount > 1)
        {
            if (Phase >= 2 && !pool.Contains(Pattern.Radial)) pool.Add(Pattern.Radial);
            if (Phase >= 3 && !pool.Contains(Pattern.Barrage)) pool.Add(Pattern.Barrage);
        }
        if (pool.Count == 0) pool.Add(Pattern.Slam);
        if (pool.Count > 1) pool.Remove(lastPattern); // 같은 패턴 연속 방지
        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    private float SlamRadius => 1.4f + 0.35f * tier.scale;

    private IEnumerator Slam()
    {
        SkillAnnounced?.Invoke("대지 강타");
        yield return SlamAt(player.transform.position, 0.9f * TempoScale + 0.15f);
    }

    private IEnumerator SlamAt(Vector2 target, float warn)
    {
        BossFx.Circle(target, SlamRadius, warn);
        yield return new WaitForSeconds(warn);
        SkillFx.Burst(target, SlamRadius * 0.6f, SlamRadius * 2.2f, new Color(1f, 0.45f, 0.2f, 0.75f), 0.35f);
        if (player != null && Vector2.Distance(target, player.transform.position) <= SlamRadius)
            BossUtil.DamagePlayer(player, AttackDamage);
    }

    private IEnumerator Charge()
    {
        SkillAnnounced?.Invoke("돌진");
        Vector2 start = transform.position;
        Vector2 dir = ((Vector2)player.transform.position - start).normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        float length = 8f;
        float warn = 0.7f * TempoScale + 0.15f;

        BossFx.Line(start, dir, length, HitRadius * 2f, warn);
        yield return new WaitForSeconds(warn);

        const float speed = 16f;
        float moved = 0f;
        bool hit = false;
        while (moved < length)
        {
            float step = speed * Time.deltaTime;
            transform.position = (Vector2)transform.position + dir * step;
            moved += step;
            if (!hit && player != null && Vector2.Distance(transform.position, player.transform.position) <= HitRadius + 0.35f)
                hit = BossUtil.DamagePlayer(player, AttackDamage);
            if (moved % 0.6f < step)
                SkillFx.Burst(transform.position, HitRadius * 1.6f, HitRadius * 0.6f, new Color(1f, 0.4f, 0.3f, 0.3f), 0.25f);
            yield return null;
        }
    }

    private IEnumerator Radial()
    {
        SkillAnnounced?.Invoke("가시 폭발");
        // 짧게 부풀었다가 발사
        Vector3 baseScale = transform.localScale;
        float windup = 0.5f * TempoScale + 0.15f;
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            transform.localScale = baseScale * (1f + 0.12f * Mathf.Sin(t / windup * Mathf.PI));
            yield return null;
        }
        transform.localScale = baseScale;

        int waves = Phase >= 3 ? 2 : 1;
        int count = 12 + 4 * (Phase - 1) + 2 * tier.tierIndex;
        for (int w = 0; w < waves; w++)
        {
            float offset = w * (180f / count);
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = SkillUtil.Rotate(Vector2.right, offset + 360f / count * i);
                BossBullet.Spawn(transform.position, dir, 5.5f, 14f, 1, new Color(1f, 0.55f, 0.2f));
            }
            yield return new WaitForSeconds(0.35f);
        }
    }

    private IEnumerator Barrage()
    {
        SkillAnnounced?.Invoke("연속 강타");
        for (int i = 0; i < 3; i++)
        {
            Vector2 target = (Vector2)player.transform.position + UnityEngine.Random.insideUnitCircle * 1.2f;
            StartCoroutine(SlamAt(target, 0.8f));
            yield return new WaitForSeconds(0.3f);
        }
        yield return new WaitForSeconds(0.8f);
    }
}
