using System.Collections;
using UnityEngine;

// 대쉬 (SHIFT, 쿨타임 2초). Player 오브젝트에 붙입니다.
//  - 1차 (전직 직후) : 이동 방향으로 빠르게 이동. 데미지 없음
//  - 2차 (Lv30 전직) : 가까운 적 반대쪽으로 뒤로 날아가면서 적 방향으로 화살 발사
//
// 2차 모드는 SecondJobSystem이 전직할 때 OnSecondJobAdvanced()를 자동 호출해서 켜집니다.
// (SecondJobSystem을 지워도 이 파일은 에러 없이 1차 대쉬로 동작)
// ※ 대쉬 중 무적은 아직 없음 (PlayerHealth 수정이 필요해서 보류)
[DisallowMultipleComponent]
public class PlayerDash : ArcherSkillBase
{
    [Header("대쉬")]
    [SerializeField, Min(0.5f)] private float distance = 3.5f;
    [SerializeField, Min(0.05f)] private float duration = 0.18f;
    [Tooltip("벽(돌/나무) 충돌 판정 반지름")]
    [SerializeField, Min(0.05f)] private float bodyRadius = 0.3f;
    [Tooltip("전직해야 대쉬를 쓸 수 있음")]
    [SerializeField] private bool requireJob = true;

    [Header("2차 대쉬 (뒤로 날며 화살)")]
    [SerializeField] private bool secondJobMode = false;
    [SerializeField, Min(1)] private int arrowCount = 3;
    [SerializeField, Range(0f, 45f)] private float arrowSpread = 12f;
    [SerializeField, Min(1)] private int arrowDamage = 6;
    [SerializeField, Min(1f)] private float arrowSpeed = 16f;

    private PlayerMovement movement;
    private Rigidbody2D body;
    private PlayerProgression progression;
    private PlayerHealth health;
    private bool dashing;

    public bool SecondJobMode => secondJobMode;

    private void Reset()
    {
        key = KeyCode.LeftShift;
        cooldown = 2f;
        arrowColor = new Color(0.75f, 0.95f, 1f);
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
        progression = GetComponentInParent<PlayerProgression>();
        health = GetComponentInParent<PlayerHealth>();

        // 전직 전에는 꺼둡니다 → 스킬 칸도 숨겨지고, 전직하는 순간 Z와 함께 나타남
        if (requireJob && progression != null)
        {
            progression.JobChanged += OnJobChanged;
            if (progression.CurrentJob == PlayerJob.None)
                enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (progression != null)
            progression.JobChanged -= OnJobChanged;
    }

    private void OnJobChanged(PlayerJob job)
    {
        if (job != PlayerJob.None && (health == null || !health.IsDead))
            enabled = true;
    }

    // SecondJobSystem이 2차 전직 순간 BroadcastMessage로 호출
    public void OnSecondJobAdvanced()
    {
        secondJobMode = true;
    }

    protected override bool Cast()
    {
        if (dashing || (health != null && health.IsDead))
            return false;
        if (requireJob && progression != null && progression.CurrentJob == PlayerJob.None)
            return false;

        Vector2 direction;
        if (secondJobMode)
        {
            // 가까운 적의 반대쪽으로 뒤로 날고, 적 쪽으로 화살
            Vector2 aim = AimDirection();
            direction = -aim;
            FireArrows(aim);
        }
        else
        {
            Vector2 input = SkillUtil.MoveInput();
            direction = input != Vector2.zero ? input : LastMoveDirection;
        }

        StartCoroutine(DashRoutine(direction.normalized));
        return true;
    }

    private void FireArrows(Vector2 aim)
    {
        for (int i = 0; i < arrowCount; i++)
        {
            float angle = arrowCount == 1 ? 0f : Mathf.Lerp(-arrowSpread, arrowSpread, i / (arrowCount - 1f));
            SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, SkillUtil.Rotate(aim, angle),
                arrowSpeed, aimRange, SkillModifiers.Damage(arrowDamage), 0.2f, false, false, Enemies);
        }
        SkillFx.Burst(Origin, 0.3f, 1.4f, new Color(0.75f, 0.95f, 1f, 0.6f), 0.2f, true);
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        dashing = true;
        bool movementWasOn = movement != null && movement.enabled;
        if (movement != null) movement.enabled = false;

        int mask = SkillUtil.ObstacleMask;
        float speed = distance / duration;
        float elapsed = 0f;
        float nextGhost = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            float step = speed * dt;

            Vector2 from = body != null ? body.position : (Vector2)transform.position;
            if (mask != 0)
            {
                RaycastHit2D hit = Physics2D.CircleCast(from, bodyRadius, direction, step, mask);
                if (hit.collider != null && !hit.collider.isTrigger)
                    step = Mathf.Max(0f, hit.distance - 0.02f);
            }

            Vector2 to = from + direction * step;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.MovePosition(to);
            }
            else
            {
                transform.position = to;
            }

            if (elapsed >= nextGhost)
            {
                nextGhost = elapsed + 0.05f;
                SkillFx.Burst(from, 0.5f, 0.2f, new Color(1f, 1f, 1f, 0.35f), 0.2f);
            }

            if (step <= 0f)
                break; // 벽에 막힘
        }

        if (movement != null && movementWasOn && (health == null || !health.IsDead))
            movement.enabled = true;
        dashing = false;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (dashing && movement != null && (health == null || !health.IsDead))
            movement.enabled = true;
        dashing = false;
    }
}
