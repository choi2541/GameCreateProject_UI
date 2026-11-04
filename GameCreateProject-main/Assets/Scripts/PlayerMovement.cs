using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [Header("대쉬")]
    [SerializeField, Min(1)] private int maxDashCharges = 3;
    [SerializeField, Min(0.01f)] private float dashRechargeTime = 3f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 18f;
    [SerializeField] private bool showDashCharges = true;
    [Header("대쉬 소리 (선택)")]
    [SerializeField] private AudioClip dashSound;
    [SerializeField, Range(0f, 1f)] private float dashVolume = 0.5f;

    private Rigidbody2D body;
    private PlayerHealth health;
    private AudioSource dashSource;
    private Collider2D[] playerColliders;
    private Vector2 moveInput;
    private Vector2 lastDirection = Vector2.right;
    private Vector2 dashDirection;
    private float dashRemaining;
    private float rechargeElapsed;
    private int charges;
    private readonly List<Collider2D> nearby = new List<Collider2D>();
    private readonly List<CollisionPair> ignored = new List<CollisionPair>();
    private ContactFilter2D enemyFilter;
    private struct CollisionPair { public Collider2D player, enemy; }

    public bool IsDashing { get; private set; }
    public Vector2 LastMoveDirection => lastDirection;
    public event System.Action<Vector2, Vector2> DashStarted;
    public int DashCharges => charges;
    public int MaxDashCharges => Mathf.Max(1, maxDashCharges);
    public float DashRechargeDuration => Mathf.Max(0.01f, dashRechargeTime);
    public float DashRechargeRemaining => charges >= MaxDashCharges ? 0f
        : Mathf.Max(0f, DashRechargeDuration - rechargeElapsed);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        charges = Mathf.Max(1, maxDashCharges);
        enemyFilter = new ContactFilter2D();
        enemyFilter.SetLayerMask(~0);
        enemyFilter.useTriggers = true;
        dashSource = gameObject.AddComponent<AudioSource>();
        dashSource.playOnAwake = false;
        dashSource.spatialBlend = 0f;
        dashSource.loop = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || (health != null && health.IsDead)) return;
        if (charges < Mathf.Max(1, maxDashCharges))
        {
            rechargeElapsed += Time.deltaTime;
            float interval = Mathf.Max(0.01f, dashRechargeTime);
            while (rechargeElapsed >= interval && charges < Mathf.Max(1, maxDashCharges))
            {
                rechargeElapsed -= interval;
                charges++;
            }
            if (charges >= Mathf.Max(1, maxDashCharges)) rechargeElapsed = 0f;
        }

        float x = 0f, y = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x--;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x++;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y--;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y++;
        moveInput = new Vector2(x, y).normalized;
        if (moveInput.sqrMagnitude > 0f) lastDirection = moveInput;

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            TryStartDash();
    }

    private bool TryStartDash()
    {
        if (IsDashing || charges <= 0 || Time.timeScale <= 0f || (health != null && health.IsDead))
            return false;
        charges--;
        dashDirection = lastDirection;
        dashRemaining = Mathf.Max(0.01f, dashDuration);
        IsDashing = true;
        if (dashSound != null) dashSource.PlayOneShot(dashSound, dashVolume);
        DashStarted?.Invoke(body.position, dashDirection);
        return true;
    }

    private void FixedUpdate()
    {
        if (health != null && health.IsDead)
        {
            EndDash();
            body.linearVelocity = Vector2.zero;
            return;
        }
        // 직전 물리 스텝까지 관통을 유지한 뒤 충돌을 복구합니다.
        if (IsDashing && dashRemaining <= 0f) EndDash();
        if (IsDashing)
        {
            float step = Mathf.Min(Time.fixedDeltaTime, dashRemaining);
            Vector2 travel = dashDirection * Mathf.Max(0.1f, dashSpeed) * step;
            IgnoreEnemiesAlongStep(travel);
            body.linearVelocity = travel / Time.fixedDeltaTime;
            dashRemaining -= Time.fixedDeltaTime;
        }
        else body.linearVelocity = moveInput * moveSpeed;
    }

    private void IgnoreEnemiesAlongStep(Vector2 travel)
    {
        foreach (Collider2D player in playerColliders)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy) continue;
            Bounds bounds = player.bounds;
            // 다음 이동 구간과 적의 접근 여유를 포함합니다. 새로 생성된 적도 매 스텝 확인합니다.
            Vector2 center = (Vector2)bounds.center + travel * 0.5f;
            float radius = bounds.extents.magnitude + travel.magnitude * 0.5f + 1f;
            Physics2D.OverlapCircle(center, radius, enemyFilter, nearby);
            foreach (Collider2D other in nearby)
            {
                if (other == null || other.transform.IsChildOf(transform)
                    || other.GetComponentInParent<EnemyMovement>() == null
                    || Physics2D.GetIgnoreCollision(player, other)) continue;
                Physics2D.IgnoreCollision(player, other, true);
                ignored.Add(new CollisionPair { player = player, enemy = other });
            }
        }
    }

    private void EndDash()
    {
        IsDashing = false;
        dashRemaining = 0f;
        foreach (CollisionPair pair in ignored)
            if (pair.player != null && pair.enemy != null)
                Physics2D.IgnoreCollision(pair.player, pair.enemy, false);
        ignored.Clear();
    }

    private void OnDisable()
    {
        EndDash();
        moveInput = Vector2.zero;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void OnGUI()
    {
        if (!showDashCharges) return;
        string remaining = charges < Mathf.Max(1, maxDashCharges)
            ? $"  +1: {Mathf.Max(0f, dashRechargeTime - rechargeElapsed):F1}s" : "";
        GUI.Box(new Rect(10, 95, 280, 30), $"Dash [Shift]: {charges}/{maxDashCharges}{remaining}");
    }
}
