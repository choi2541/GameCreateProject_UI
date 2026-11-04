using System.Reflection;
using UnityEngine;

// 주인공 그림 애니메이션 (서 있기 / 4방향 걷기 / 활 쏘기 / 맞기).
// 그림 줄(가로로 칸이 이어진 png) 7장을 받아서 실행할 때 칸을 잘라 씀. Animator 없이 동작.
// 팀원 코드(PlayerMovement, PlayerAttackVisual, PlayerHealth)는 고치지 않고 상태만 읽음:
//  - 이동: Rigidbody2D 속도, 바라보는 방향: PlayerMovement.LastMoveDirection
//  - 쏘기: PlayerAttackVisual가 공격 그림을 띄우는 순간
//  - 맞기: PlayerHealth가 깜빡이기 시작하는 순간
// 그림은 Tools > Game UI > Setup Player Animation이 자동으로 넣어 줌.
[DefaultExecutionOrder(1000)] // 팀원 그림 코드 다음에 그려서 덮어씀
[DisallowMultipleComponent]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [System.Serializable]
    public class Clip
    {
        public Texture2D strip;          // 가로로 칸이 이어진 그림 한 줄
        [Min(1)] public int frames = 6;
        [Tooltip("0 = 움직이지 않고 첫 칸만 보여 줌")]
        [Min(0f)] public float fps = 10f;
        public bool facesRight = true;   // 왼쪽을 볼 때 좌우 뒤집기 (앞/뒤 그림은 끔)
        [System.NonSerialized] public Sprite[] sprites;
    }

    [Header("그림 (Setup Player Animation이 자동으로 넣음)")]
    [SerializeField] private Clip idle = new Clip { frames = 4, fps = 4f, facesRight = true }; // 서 있기: 살짝 숨쉬기 (오른쪽을 보는 그림, 0 = 멈춤)
    [SerializeField] private Clip walkDown = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip walkUp = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip walkRight = new Clip { fps = 10f, facesRight = true };
    [SerializeField] private Clip walkLeft = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip shoot = new Clip { frames = 8, fps = 26f, facesRight = true };
    [SerializeField] private Clip hurt = new Clip { fps = 16f, facesRight = true };

    [Header("크기")]
    [Tooltip("1 = 원래 주인공 그림과 같은 키. 크게 하려면 1.2 같은 값")]
    [SerializeField, Min(0.1f)] private float sizeMultiplier = 1f;
    [Tooltip("그림 칸 안에서 캐릭터 키(픽셀). 그림을 새로 만들면 맞춰 주세요")]
    [SerializeField, Min(1f)] private float characterPixelHeight = 256f;
    [Tooltip("그림 칸 아래 여백(픽셀)")]
    [SerializeField, Min(0f)] private float feetMarginPixels = 6f;

    [Header("동작")]
    [SerializeField, Min(0f)] private float walkThreshold = 0.15f;
    [Tooltip("대쉬 중 걷기 속도 배수")]
    [SerializeField, Min(1f)] private float dashSpeedUp = 2f;

    private SpriteRenderer sr;
    private Rigidbody2D body;
    private PlayerMovement movement;
    private Component attackVisual, health;
    private FieldInfo attackShowing, healthBlinking, healthDead;
    private bool lastAttack, lastBlink;

    private Clip current;
    private float time, oneShotEnd;
    private bool oneShot, facingLeft;
    private bool ready;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        attackVisual = GetComponent("PlayerAttackVisual");
        health = GetComponent("PlayerHealth");
        if (attackVisual != null) attackShowing = attackVisual.GetType().GetField("showingAttack", Flags);
        if (health != null)
        {
            healthBlinking = health.GetType().GetField("isBlinking", Flags);
            healthDead = health.GetType().GetField("isDead", Flags);
        }
        if (sr == null || idle.strip == null) { enabled = false; return; }

        // 원래 그림의 키에 맞춰 크기 계산
        float targetHeight = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
        float ppu = characterPixelHeight / Mathf.Max(0.01f, targetHeight * sizeMultiplier);
        foreach (Clip c in Clips()) Slice(c, ppu);
        ready = true;
        Play(idle, false);
    }

    private void OnDestroy()
    {
        foreach (Clip c in Clips())
        {
            if (c.sprites == null) continue;
            foreach (Sprite s in c.sprites) if (s != null) Destroy(s);
            c.sprites = null;
        }
    }

    private System.Collections.Generic.IEnumerable<Clip> Clips()
    {
        yield return idle; yield return walkDown; yield return walkUp; yield return walkRight;
        yield return walkLeft; yield return shoot; yield return hurt;
    }

    private void Slice(Clip c, float ppu)
    {
        if (c.strip == null) return;
        int n = Mathf.Max(1, c.frames);
        float w = (float)c.strip.width / n, h = c.strip.height;
        // 기준점: 캐릭터 몸 가운데 (원래 그림처럼 가운데 기준)
        Vector2 pivot = new Vector2(0.5f, (feetMarginPixels + characterPixelHeight * 0.5f) / h);
        c.sprites = new Sprite[n];
        for (int i = 0; i < n; i++)
        {
            c.sprites[i] = Sprite.Create(c.strip, new Rect(Mathf.Round(i * w), 0f, Mathf.Floor(w), h), pivot, ppu, 0, SpriteMeshType.FullRect);
            c.sprites[i].name = c.strip.name + "_" + i;
        }
    }

    private void LateUpdate()
    {
        if (!ready || Time.timeScale <= 0f) return;
        if (IsDead()) return; // 죽으면 마지막 모습 그대로

        // 바라보는 방향 (좌우)
        Vector2 velocity = body != null ? body.linearVelocity : Vector2.zero;
        Vector2 look = movement != null ? movement.LastMoveDirection : velocity;
        if (Mathf.Abs(look.x) > 0.01f) facingLeft = look.x < 0f;

        // 맞기 / 쏘기 (한 번 재생)
        bool blink = ReadBool(health, healthBlinking);
        bool attack = ReadBool(attackVisual, attackShowing);
        if (blink && !lastBlink && hurt.sprites != null) Play(hurt, true);
        else if (attack && !lastAttack && shoot.sprites != null && !(oneShot && current == hurt)) Play(shoot, true);
        lastBlink = blink; lastAttack = attack;

        if (oneShot && Time.time >= oneShotEnd) oneShot = false;
        if (!oneShot)
        {
            Clip want = idle;
            if (velocity.sqrMagnitude > walkThreshold * walkThreshold)
            {
                if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y)) want = velocity.x >= 0f ? walkRight : walkLeft;
                else want = velocity.y >= 0f ? walkUp : walkDown;
                if (want.sprites == null) want = walkDown.sprites != null ? walkDown : idle;
            }
            if (want != current) Play(want, false);
        }

        float speed = movement != null && movement.IsDashing ? dashSpeedUp : 1f;
        time += Time.deltaTime * speed;
        int frame = current.fps <= 0f ? 0 : Mathf.FloorToInt(time * current.fps);
        frame = oneShot ? Mathf.Min(frame, current.sprites.Length - 1) : frame % current.sprites.Length;
        sr.sprite = current.sprites[frame];
        sr.flipX = current.facesRight && facingLeft;
    }

    private void Play(Clip c, bool once)
    {
        if (c == null || c.sprites == null) c = idle;
        current = c; time = 0f; oneShot = once;
        if (once) oneShotEnd = Time.time + c.sprites.Length / Mathf.Max(1f, c.fps);
    }

    private bool IsDead()
    {
        return healthDead != null && ReadBool(health, healthDead);
    }

    private static bool ReadBool(Component target, FieldInfo field)
    {
        return target != null && field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(target);
    }
}
