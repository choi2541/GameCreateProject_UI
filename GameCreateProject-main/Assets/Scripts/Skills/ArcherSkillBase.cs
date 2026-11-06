using UnityEngine;

// 키를 누르면 쿨타임마다 한 번 발동하는 스킬의 공통 부분.
// ISkillCooldown을 구현하므로 하단 스킬 칸에 자동으로 표시됩니다.
public abstract class ArcherSkillBase : MonoBehaviour, ISkillCooldown
{
    [Header("공통")]
    [SerializeField] protected KeyCode key = KeyCode.Z;
    [SerializeField, Min(0.05f)] protected float cooldown = 5f;
    [Tooltip("적들이 들어있는 부모 오브젝트. 비워두면 EnemySpawner를 자동으로 찾습니다.")]
    [SerializeField] protected Transform enemyContainer;
    [Tooltip("화살 이미지. 비워두면 임시 화살 모양을 사용합니다.")]
    [SerializeField] protected Sprite arrowSprite;
    [SerializeField] protected Color arrowColor = new Color(1f, 0.95f, 0.7f);
    [Tooltip("적을 자동 조준하는 거리")]
    [SerializeField, Min(0.5f)] protected float aimRange = 10f;

    [Header("사운드 (비워두면 소리 없음)")]
    [Tooltip("스킬을 쓰는 순간 나는 소리")]
    [SerializeField] protected AudioClip castSound;
    [SerializeField, Range(0f, 1f)] protected float castVolume = 0.8f;
    [Tooltip("맞거나 터질 때 나는 소리 (스킬마다 쓰는 곳이 다름)")]
    [SerializeField] protected AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] protected float hitVolume = 0.6f;

    private float nextCastTime;
    private Vector2 lastMoveDirection = Vector2.right;

    public float Cooldown => SkillModifiers.Cooldown(cooldown);
    public float CooldownRemaining => Mathf.Max(0f, nextCastTime - Time.time);
    public string KeyLabel => key.ToString();

    protected Transform Enemies => SkillUtil.EnemyContainer(enemyContainer);
    protected Vector2 Origin => transform.position;
    protected Vector2 LastMoveDirection => GetComponentInParent<PlayerMovement>() is PlayerMovement movement
        ? movement.LastMoveDirection : lastMoveDirection;
    protected bool OwnerDead => GetComponentInParent<PlayerHealth>() is PlayerHealth health && health.IsDead;

    protected virtual void Update()
    {
        if (OwnerDead) { enabled = false; return; }
        Vector2 move = SkillUtil.MoveInput();
        if (move != Vector2.zero)
            lastMoveDirection = move;

        if (Time.timeScale <= 0f || Time.time < nextCastTime)
            return;
        if (!Input.GetKeyDown(key))
            return;
        // Ctrl+X / Ctrl+C 는 4차 스킬용이라 일반 스킬은 나가지 않음
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            return;
        if (Cast())
        {
            nextCastTime = Time.time + SkillModifiers.Cooldown(cooldown);
            SkillSound.Play(castSound, castVolume);
            NotifyCast(this);
        }
    }

    // 발동에 성공하면 true (쿨타임 시작)
    protected abstract bool Cast();

    // 스킬이 나갈 때 알림 (예: 바람 정령이 같이 움직임). 받는 쪽이 없어도 됨
    public static event System.Action<MonoBehaviour> SkillCast;
    public static void NotifyCast(MonoBehaviour skill) => SkillCast?.Invoke(skill);

    // 맞음/폭발 소리 (자식 스킬에서 원하는 순간에 호출)
    protected void PlayHitSound() => SkillSound.Play(hitSound, hitVolume, 0.05f);

    // 가장 가까운 적 방향. 없으면 마지막 이동 방향.
    protected Vector2 AimDirection()
    {
        EnemyHealth target = SkillUtil.Nearest(Enemies, Origin, aimRange);
        if (target == null)
            return LastMoveDirection;
        Vector2 offset = (Vector2)target.transform.position - Origin;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : LastMoveDirection;
    }

    protected virtual void OnDisable()
    {
        // 꺼질 때 진행 중이던 연출(코루틴)도 멈춥니다.
        StopAllCoroutines();
    }
}
