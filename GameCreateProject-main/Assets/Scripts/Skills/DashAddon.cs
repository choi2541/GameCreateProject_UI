using System.Reflection;
using UnityEngine;

// 팀 대쉬(PlayerMovement, Shift 3칸 충전) 위에 효과를 얹는 공통 부분.
// 대쉬 자체(이동·충전·무적·몹 통과)는 팀 코드가 그대로 하고, 여기서는 대쉬가 시작/진행/끝날 때 효과만 추가합니다.
// → Shift 한 번에 대쉬가 두 번 나가지 않음. 쿨타임 칸이 없어서 SHIFT 칸은 팀 대쉬(3/3)를 그대로 표시.
public abstract class DashAddon : MonoBehaviour
{
    [Header("공통")]
    [Tooltip("적들이 들어있는 부모 오브젝트. 비워두면 EnemySpawner를 자동으로 찾습니다.")]
    [SerializeField] protected Transform enemyContainer;
    [Tooltip("화살 이미지. 비워두면 임시 화살 모양")]
    [SerializeField] protected Sprite arrowSprite;
    [SerializeField] protected Color arrowColor = new Color(0.75f, 0.95f, 1f);
    [Tooltip("자동 조준 거리")]
    [SerializeField, Min(0.5f)] protected float aimRange = 10f;

    private static readonly FieldInfo DashDirField =
        typeof(PlayerMovement).GetField("dashDirection", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private PlayerMovement movement;
    private bool wasDashing;
    private Vector2 lastPos;
    private Vector2 startPos;
    private float dashTime;

    protected Transform Enemies => SkillUtil.EnemyContainer(enemyContainer);
    protected Vector2 Origin => transform.position;
    protected float DashElapsed => dashTime;
    protected bool HasTeamDash => movement != null;

    protected virtual void Awake()
    {
        movement = GetComponentInParent<PlayerMovement>();
    }

    protected virtual void OnDisable()
    {
        if (wasDashing) { wasDashing = false; OnDashEnd(transform.position, startPos); }
        StopAllCoroutines();
    }

    private void LateUpdate()
    {
        if (movement == null) return;
        bool dashing = movement.IsDashing;
        Vector2 pos = transform.position;

        if (dashing && !wasDashing)
        {
            startPos = pos;
            lastPos = pos;
            dashTime = 0f;
            OnDashStart(DashDirection(pos), pos);
        }
        else if (dashing)
        {
            dashTime += Time.deltaTime;
            OnDashTick(lastPos, pos);
        }
        else if (wasDashing)
        {
            OnDashTick(lastPos, pos);
            OnDashEnd(pos, startPos);
        }

        wasDashing = dashing;
        lastPos = pos;
    }

    private Vector2 DashDirection(Vector2 pos)
    {
        if (DashDirField != null && DashDirField.FieldType == typeof(Vector2))
        {
            Vector2 d = (Vector2)DashDirField.GetValue(movement);
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        Vector2 input = SkillUtil.MoveInput();
        return input != Vector2.zero ? input : Vector2.right;
    }

    protected virtual void OnDashStart(Vector2 direction, Vector2 from) { }
    protected virtual void OnDashTick(Vector2 from, Vector2 to) { }
    protected virtual void OnDashEnd(Vector2 at, Vector2 from) { }

    // 가까운 적 방향 (없으면 fallback)
    protected Vector2 AimFrom(Vector2 at, Vector2 fallback)
    {
        EnemyHealth target = SkillUtil.Nearest(Enemies, at, aimRange);
        if (target == null) return fallback;
        Vector2 d = (Vector2)target.transform.position - at;
        return d.sqrMagnitude > 0.0001f ? d.normalized : fallback;
    }
}
