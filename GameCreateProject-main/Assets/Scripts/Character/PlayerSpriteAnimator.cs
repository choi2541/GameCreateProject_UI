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

    [Header("전직 전 (초보) 그림: 직업을 고르기 전에는 이 그림으로 (Setup Player Animation이 자동으로 넣음)")]
    [SerializeField] private Clip noviceIdle = new Clip { frames = 4, fps = 4f, facesRight = true };
    [SerializeField] private Clip noviceWalkDown = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip noviceWalkUp = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip noviceWalkRight = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip noviceWalkLeft = new Clip { fps = 10f, facesRight = false };
    [SerializeField] private Clip noviceHurt = new Clip { fps = 12f, facesRight = true };
    [Tooltip("공격 3종: 공격할 때마다 차례로 (찌르기 → 내려베기 → 내려찍기)")]
    [SerializeField] private Clip[] noviceAttacks = { new Clip { fps = 12f }, new Clip { fps = 12f }, new Clip { fps = 12f } };

    [Header("전직 전 공격 이펙트 (공격 3종과 같은 순서)")]
    [SerializeField] private Clip[] noviceAttackFx = { new Clip { fps = 24f }, new Clip { fps = 24f }, new Clip { fps = 24f } };
    [Tooltip("이펙트가 터지는 공격 칸 (0부터)")]
    [SerializeField] private int[] noviceHitFrame = { 3, 3, 4 };
    [Tooltip("이펙트 위치·크기 (캐릭터 키 = 1 기준): x = 앞쪽 거리, y = 발에서 높이, z = 이펙트 크기(긴 변)")]
    [SerializeField] private Vector3[] noviceFxPlace = { new Vector3(0.62f, 0.47f, 1.17f), new Vector3(0.36f, 0.5f, 0.94f), new Vector3(0.2f, 0.03f, 1.02f) };

    [Header("크기")]
    [Tooltip("1 = 원래 주인공 그림과 같은 키. 크게 하려면 1.2 같은 값")]
    [SerializeField, Min(0.1f)] private float sizeMultiplier = 1f;
    [Tooltip("그림 칸 안에서 캐릭터 키(픽셀). 그림을 새로 만들면 맞춰 주세요")]
    [SerializeField, Min(1f)] private float characterPixelHeight = 128f;
    [Tooltip("그림 칸 아래 여백(픽셀)")]
    [SerializeField, Min(0f)] private float feetMarginPixels = 3f;

    [Header("전직 전 공격 타이밍")]
    [Tooltip("켜면 팀 자동 공격이 나가기 직전에 휘두르기를 미리 시작 → 칼이 닿는 순간에 적이 맞음")]
    [SerializeField] private bool syncAttackToHit = true;
    [Tooltip("팀 자동 공격의 노란 부채꼴 선 숨기기")]
    [SerializeField] private bool hideAttackArc = true;

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
    private PlayerProgression progression;
    private bool usingNovice;
    private int nextAttack;
    private SpriteRenderer fxRenderer;
    private Clip fxClip;
    private float fxStart = -1f, unitHeight;
    private Vector3 fxPlace;
    private bool fxLeft;
    private float time, oneShotEnd;
    private bool oneShot, facingLeft;
    private bool ready;

    // 팀 자동 공격(AutoAttack) 읽기 (코드 수정 없이 값만 읽음)
    private Component autoAttack;
    private FieldInfo aaTimer, aaRange, aaContainer;
    private Renderer aaLine;
    private bool preStarted;
    private float preStartedAt;

    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        attackVisual = GetComponent("PlayerAttackVisual");
        health = GetComponent("PlayerHealth");
        if (attackVisual != null) attackShowing = attackVisual.GetType().GetField("showingAttack", Flags);
        foreach (MonoBehaviour b in GetComponentsInChildren<MonoBehaviour>(true))
            if (b != null && b.GetType().Name == "AutoAttack") { autoAttack = b; break; }
        if (autoAttack != null)
        {
            System.Type t = autoAttack.GetType();
            aaTimer = t.GetField("attackTimer", Flags);
            aaRange = t.GetField("attackRange", Flags);
            aaContainer = t.GetField("enemyContainer", Flags);
            aaLine = autoAttack.GetComponent<LineRenderer>();
        }
        if (health != null)
        {
            healthBlinking = health.GetType().GetField("isBlinking", Flags);
            healthDead = health.GetType().GetField("isDead", Flags);
        }
        if (sr == null || (idle.strip == null && noviceIdle.strip == null)) { enabled = false; return; }
        progression = GetComponent<PlayerProgression>();

        // 원래 그림의 키에 맞춰 크기 계산
        float targetHeight = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
        unitHeight = targetHeight * sizeMultiplier;
        float ppu = characterPixelHeight / Mathf.Max(0.01f, unitHeight);
        foreach (Clip c in Clips()) if (System.Array.IndexOf(noviceAttackFx, c) < 0) Slice(c, ppu);
        for (int i = 0; i < noviceAttackFx.Length; i++) SliceFx(noviceAttackFx[i], i);
        ready = true;
        usingNovice = Novice();
        Play(Idle, false);
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
        yield return noviceIdle; yield return noviceWalkDown; yield return noviceWalkUp; yield return noviceWalkRight;
        yield return noviceWalkLeft; yield return noviceHurt;
        foreach (Clip c in noviceAttacks) yield return c;
        foreach (Clip c in noviceAttackFx) yield return c;
    }

    // 이펙트: 가운데 기준, 긴 변이 (캐릭터 키 × 크기)가 되게
    private void SliceFx(Clip c, int index)
    {
        if (c == null || c.strip == null) return;
        int n = Mathf.Max(1, c.frames);
        float w = (float)c.strip.width / n, h = c.strip.height;
        float size = index < noviceFxPlace.Length ? noviceFxPlace[index].z : 1f;
        float ppu = Mathf.Max(w, h) / Mathf.Max(0.01f, unitHeight * size);
        c.sprites = new Sprite[n];
        for (int i = 0; i < n; i++)
            c.sprites[i] = Sprite.Create(c.strip, new Rect(Mathf.Round(i * w), 0f, Mathf.Floor(w), h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
    }

    // 캐릭터 창(Tab)에 보여줄 고정 그림: 서 있기 첫 칸 (공격·걷기 중이어도 항상 같은 모습)
    public Sprite PortraitSprite
    {
        get
        {
            if (!ready) return null;
            Clip c = Novice() ? noviceIdle : idle;
            if (c.sprites == null || c.sprites.Length == 0) c = idle.sprites != null ? idle : noviceIdle;
            return c.sprites != null && c.sprites.Length > 0 ? c.sprites[0] : null;
        }
    }

    private bool Novice() => noviceIdle.sprites != null && progression != null && progression.CurrentJob == PlayerJob.None;
    private Clip Idle => usingNovice ? noviceIdle : idle;
    private Clip Hurt => usingNovice ? noviceHurt : hurt;

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

        // 전직하면 그림 세트를 바꿈
        bool novice = Novice();
        if (novice != usingNovice) { usingNovice = novice; oneShot = false; Play(Idle, false); }

        // 맞기 / 공격 (한 번 재생)
        bool blink = ReadBool(health, healthBlinking);
        bool attack = ReadBool(attackVisual, attackShowing);
        if (hideAttackArc && aaLine != null && aaLine.enabled) aaLine.enabled = false; // 노란 부채꼴 선 숨김
        if (blink && !lastBlink && Hurt.sprites != null) { Play(Hurt, true); preStarted = false; }
        else if (attack && !lastAttack && !(oneShot && current == Hurt))
        {
            // 미리 휘두르기를 시작했으면 그대로 (지금이 칼이 닿는 순간), 아니면 타격 칸부터 바로
            bool alreadySwinging = preStarted && usingNovice && Time.time - preStartedAt < 0.6f;
            if (!alreadySwinging) StartAttack(usingNovice && syncAttackToHit);
            preStarted = false;
        }
        else if (usingNovice && syncAttackToHit && !oneShot) TryPreSwing();
        lastBlink = blink; lastAttack = attack;

        if (oneShot && Time.time >= oneShotEnd) oneShot = false;
        if (!oneShot)
        {
            Clip want = Idle;
            if (velocity.sqrMagnitude > walkThreshold * walkThreshold)
            {
                Clip r = usingNovice ? noviceWalkRight : walkRight, l = usingNovice ? noviceWalkLeft : walkLeft;
                Clip u = usingNovice ? noviceWalkUp : walkUp, d = usingNovice ? noviceWalkDown : walkDown;
                if (Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y)) want = velocity.x >= 0f ? r : l;
                else want = velocity.y >= 0f ? u : d;
                if (want.sprites == null) want = d.sprites != null ? d : Idle;
            }
            if (want != current) Play(want, false);
        }

        float speed = movement != null && movement.IsDashing ? dashSpeedUp : 1f;
        time += Time.deltaTime * speed;
        int frame = current.fps <= 0f ? 0 : Mathf.FloorToInt(time * current.fps);
        frame = oneShot ? Mathf.Min(frame, current.sprites.Length - 1) : frame % current.sprites.Length;
        sr.sprite = current.sprites[frame];
        sr.flipX = current.facesRight && facingLeft;
        UpdateFx();
    }

    // 팀 자동 공격 남은 시간을 보고, 칼이 닿는 칸이 공격 순간에 오도록 미리 휘두르기 시작
    private void TryPreSwing()
    {
        if (autoAttack == null || aaTimer == null || preStarted || !((Behaviour)autoAttack).isActiveAndEnabled) return;
        if (noviceAttacks == null || noviceAttacks.Length == 0) return;
        int index = nextAttack % noviceAttacks.Length;
        Clip c = noviceAttacks[index];
        if (c == null || c.sprites == null) return;
        float lead = HitTime(index, c);
        float left = (float)aaTimer.GetValue(autoAttack);
        if (left > lead || left <= 0f) return;
        float range = aaRange != null ? (float)aaRange.GetValue(autoAttack) : 5f;
        Transform container = aaContainer != null ? aaContainer.GetValue(autoAttack) as Transform : null;
        if (SkillUtil.Nearest(SkillUtil.EnemyContainer(container), transform.position, range) == null) return; // 맞을 적이 없으면 안 휘두름
        StartAttack(false, lead - left);
        preStarted = true;
        preStartedAt = Time.time;
    }

    private float HitTime(int index, Clip c)
    {
        int hit = index < noviceHitFrame.Length ? noviceHitFrame[index] : 3;
        return hit / Mathf.Max(1f, c.fps);
    }

    // skipToHit: 칼이 닿는 칸부터 바로 / alreadyPlayed: 이미 지나간 시간(초)만큼 앞당겨 시작
    private void StartAttack(bool skipToHit = false, float alreadyPlayed = 0f)
    {
        if (!usingNovice)
        {
            if (shoot.sprites != null) Play(shoot, true);
            return;
        }
        if (noviceAttacks == null || noviceAttacks.Length == 0) return;
        int index = nextAttack % noviceAttacks.Length;
        nextAttack++;
        Clip c = noviceAttacks[index];
        if (c == null || c.sprites == null) return;

        // 가장 가까운 적 쪽을 봄 (자동 공격은 적 방향으로 나가므로)
        EnemyHealth target = SkillUtil.Nearest(SkillUtil.EnemyContainer(null), transform.position, 8f);
        if (target != null) facingLeft = target.transform.position.x < transform.position.x;
        Play(c, true);
        float hitTime = HitTime(index, c);
        float offset = skipToHit ? hitTime : Mathf.Clamp(alreadyPlayed, 0f, hitTime);
        time = offset;
        oneShotEnd -= offset;

        // 이펙트 예약: 타격 칸에 터짐
        fxClip = index < noviceAttackFx.Length ? noviceAttackFx[index] : null;
        if (fxClip != null && fxClip.sprites != null)
        {
            fxStart = Time.time + (hitTime - offset);
            fxPlace = index < noviceFxPlace.Length ? noviceFxPlace[index] : Vector3.zero;
            fxLeft = facingLeft;
        }
    }

    private void UpdateFx()
    {
        if (fxClip == null || fxStart < 0f) { if (fxRenderer != null) fxRenderer.enabled = false; return; }
        float t = Time.time - fxStart;
        if (t < 0f) { if (fxRenderer != null) fxRenderer.enabled = false; return; }
        int f = Mathf.FloorToInt(t * fxClip.fps);
        if (f >= fxClip.sprites.Length) { fxStart = -1f; if (fxRenderer != null) fxRenderer.enabled = false; return; }
        if (fxRenderer == null)
        {
            var go = new GameObject("AttackFx");
            go.transform.SetParent(transform, false);
            fxRenderer = go.AddComponent<SpriteRenderer>();
            fxRenderer.sortingLayerID = sr.sortingLayerID;
            fxRenderer.sortingOrder = sr.sortingOrder + 1;
        }
        fxRenderer.enabled = true;
        fxRenderer.sprite = fxClip.sprites[f];
        fxRenderer.flipX = fxLeft;
        // 몸 가운데 기준 → 발 = -0.5
        Vector3 local = new Vector3((fxLeft ? -1f : 1f) * fxPlace.x * unitHeight, (fxPlace.y - 0.5f) * unitHeight, 0f);
        Vector3 s = transform.lossyScale;
        fxRenderer.transform.localPosition = new Vector3(local.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)), local.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 0f);
        fxRenderer.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
    }

    private void Play(Clip c, bool once)
    {
        if (c == null || c.sprites == null) c = Idle;
        if (c == null || c.sprites == null) c = usingNovice ? idle : noviceIdle;
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
