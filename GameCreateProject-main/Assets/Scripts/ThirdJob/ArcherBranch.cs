using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 3차 갈래: 그림자 궁수(힘) / 폭풍 궁수(스킬 피해력)
public enum ArcherBranch { Shadow, Storm }

// 3차 스킬 표시용. JobTierSystem이 고른 갈래의 스킬만 켭니다.
public interface IThirdJobSkill { ArcherBranch Branch { get; } }

// 4차 스킬 표시용. (4차 전직 창에 이름/설명/아이콘이 표시됨)
public interface IFourthJobSkill
{
    ArcherBranch Branch { get; }
    string DisplayName { get; }
    string Description { get; }
    string KeyText { get; }
    Sprite Icon { get; }
}

// 3·4차 스킬이 같이 쓰는 도구
public static class JobTierUtil
{
    public static readonly Color ShadowColor = new Color(0.78f, 0.5f, 1f);
    public static readonly Color ShadowDark = new Color(0.22f, 0.06f, 0.32f);
    public static readonly Color StormColor = new Color(0.6f, 1f, 0.88f);
    public static readonly Color BoltColor = new Color(1f, 0.9f, 0.35f);

    // 그림자 궁수: 힘 1포인트당 스킬 피해 +%
    public static float StrDamagePerPoint = 2f;
    // 그림자 궁수 패시브(암살자의 눈): 치명타 확률 / 표식 붙은 적 추가 확률 / 치명타 배율
    public static float CritChance = 0.2f, CritChanceMarked = 0.2f, CritMultiplier = 2f;

    // 갈래별 피해 (3차부터 계수 적용)
    //   폭풍 = AP 계수 : 기본 피해 × (1 + 스킬 피해력 보너스 × 계수)
    //   그림자 = AD 계수 : 기본 피해 × (1 + 힘 보너스 × 계수)  (+ 치명타 패시브)
    //   계수(ratio)는 스킬마다 Inspector "Stat Ratio" (1 = 스탯 보너스 그대로, 2 = 두 배)
    public static int Damage(int baseDamage, ArcherBranch branch, EnemyHealth target = null, float ratio = 1f)
    {
        PlayerStats stats = PlayerStats.Instance;
        if (branch == ArcherBranch.Storm)
        {
            float ap = stats != null ? stats.SkillDamageBonus : 0f;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * SkillModifiers.DamageMultiplier * (1f + ap * ratio)));
        }

        float str = stats != null ? stats.Get("STR") : 0;
        float value = baseDamage * SkillModifiers.DamageMultiplier * (1f + str * StrDamagePerPoint / 100f * ratio);

        if (JobTierSystem.Instance != null && JobTierSystem.Instance.Branch == ArcherBranch.Shadow)
        {
            float chance = CritChance + (target != null && ShadowMark.Has(target) ? CritChanceMarked : 0f);
            if (Random.value < chance)
                value *= CritMultiplier;
        }
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    // 보스인지 (Boss 폴더를 지워도 에러 없도록 이름으로 확인)
    public static bool IsBoss(EnemyHealth enemy)
    {
        return enemy != null && enemy.GetComponent("BossController") != null;
    }

    public static void Hit(EnemyHealth enemy, int damage)
    {
        if (enemy != null && enemy.isActiveAndEnabled)
            enemy.TakeDamage(damage);
    }

    // 화면 안의 적 (가까운 순서). 새 목록을 돌려줌
    public static List<EnemyHealth> OnScreen(Transform container, Vector2 from, float margin = 0.2f)
    {
        return OnScreen(container, from, new List<EnemyHealth>(), margin);
    }

    // 화면 안의 적 (가까운 순서)을 buffer에 채움 (매번 새 목록을 만들지 않아 메모리 절약)
    public static List<EnemyHealth> OnScreen(Transform container, Vector2 from, List<EnemyHealth> buffer, float margin = 0.2f)
    {
        buffer.Clear();
        List<EnemyHealth> all = SkillUtil.EnemiesNow(container);
        for (int i = 0; i < all.Count; i++)
        {
            EnemyHealth e = all[i];
            if (e != null && e.isActiveAndEnabled && SkillUtil.IsInsideCamera(e.transform.position, margin))
                buffer.Add(e);
        }
        sortFrom = from;
        buffer.Sort(ByDistance);
        return buffer;
    }

    private static Vector2 sortFrom;
    private static readonly System.Comparison<EnemyHealth> ByDistance = (a, b) =>
        ((Vector2)a.transform.position - sortFrom).sqrMagnitude.CompareTo(((Vector2)b.transform.position - sortFrom).sqrMagnitude);

    // ───────── 플레이어 무적 (PlayerHealth를 고치지 않고 다음 피격 가능 시간만 늦춤) ─────────
    private static readonly FieldInfo nextDamageField =
        typeof(PlayerHealth).GetField("nextDamageTime", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Invincible(PlayerHealth player, float seconds)
    {
        if (player == null || nextDamageField == null)
            return;
        float current = (float)nextDamageField.GetValue(player);
        nextDamageField.SetValue(player, Mathf.Max(current, Time.time + seconds));
    }

    // ───────── 적 밀치기 (EnemyMovement를 잠깐 끄고 미는 방식) ─────────
    public static void Knockback(EnemyHealth enemy, Vector2 velocity, float seconds = 0.25f)
    {
        if (enemy == null || IsBoss(enemy))
            return;
        EnemyPush push = enemy.GetComponent<EnemyPush>();
        if (push == null)
            push = enemy.gameObject.AddComponent<EnemyPush>();
        push.Begin(velocity, seconds);
    }

    // 적을 한 지점으로 끌어당김 (회오리)
    public static void Pull(EnemyHealth enemy, Vector2 center, float speed)
    {
        if (enemy == null || IsBoss(enemy))
            return;
        Vector2 pos = enemy.transform.position;
        Vector2 to = Vector2.MoveTowards(pos, center, speed * Time.deltaTime);
        Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
        if (body != null) body.position = to;
        enemy.transform.position = to;
    }

    // ───────── 대쉬 (벽 판정 포함) ─────────
    public static IEnumerator Dash(MonoBehaviour owner, Vector2 direction, float distance, float duration,
        Color ghostColor, System.Action<Vector2, Vector2> onStep = null)
    {
        PlayerMovement movement = owner.GetComponent<PlayerMovement>();
        Rigidbody2D body = owner.GetComponent<Rigidbody2D>();
        PlayerHealth health = owner.GetComponentInParent<PlayerHealth>();
        bool movementWasOn = movement != null && movement.enabled;
        if (movement != null) movement.enabled = false;

        int mask = SkillUtil.ObstacleMask;
        float speed = distance / duration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            float dt = Time.fixedDeltaTime;
            elapsed += dt;
            float step = speed * dt;
            Vector2 from = body != null ? body.position : (Vector2)owner.transform.position;
            if (mask != 0)
            {
                RaycastHit2D hit = Physics2D.CircleCast(from, 0.3f, direction, step, mask);
                if (hit.collider != null && !hit.collider.isTrigger)
                    step = Mathf.Max(0f, hit.distance - 0.02f);
            }
            Vector2 to = from + direction * step;
            if (body != null) { body.linearVelocity = Vector2.zero; body.MovePosition(to); }
            else owner.transform.position = to;

            SkillFx.Burst(from, 0.55f, 0.2f, ghostColor, 0.25f);
            onStep?.Invoke(from, to);
            if (step <= 0f)
                break;
        }
        if (movement != null && movementWasOn && (health == null || !health.IsDead))
            movement.enabled = true;
    }

    // ───────── 선 그리기 (번개, 조준선, 표식 연결) ─────────
    private static Material lineMaterial;

    public static LineRenderer NewLine(string name, Color color, float width, int sortingOrder = 62)
    {
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        GameObject obj = new GameObject(name);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.sortingOrder = sortingOrder;
        line.positionCount = 0;
        return line;
    }

    // 지그재그 번개 모양으로 점 채우기
    public static void SetBolt(LineRenderer line, Vector2 a, Vector2 b, float jitter)
    {
        float length = Vector2.Distance(a, b);
        int count = Mathf.Clamp(Mathf.CeilToInt(length / 0.35f), 2, 40);
        Vector2 dir = (b - a).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        line.positionCount = count + 1;
        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            float off = (i == 0 || i == count) ? 0f : Random.Range(-jitter, jitter);
            line.SetPosition(i, Vector2.Lerp(a, b, t) + normal * off);
        }
    }

    public static void SetStraight(LineRenderer line, Vector2 a, Vector2 b)
    {
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    // 잠깐 보였다 사라지는 선 (다 쓴 선은 꺼서 다시 씀. 보이는 모습은 똑같음)
    // MaxFlashLines는 극단적인 상황에서만 걸리는 안전장치
    public static int MaxFlashLines = 1000;
    private static readonly Stack<LineFader> linePool = new Stack<LineFader>();
    private static Transform lineRoot;
    internal static int activeFlashLines;

    public static void FlashLine(Vector2 a, Vector2 b, Color color, float width, float seconds, bool bolt, float jitter = 0.25f)
    {
        if (activeFlashLines >= MaxFlashLines)
            return;
        LineFader fader = null;
        while (fader == null && linePool.Count > 0)
            fader = linePool.Pop();
        if (fader == null)
        {
            if (lineRoot == null) lineRoot = new GameObject("[FlashLines]").transform;
            LineRenderer line = NewLine("FlashLine", color, width);
            line.transform.SetParent(lineRoot, false);
            fader = line.gameObject.AddComponent<LineFader>();
        }
        fader.gameObject.SetActive(true);
        fader.Init(fader.GetComponent<LineRenderer>(), a, b, color, width, seconds, bolt, jitter);
    }

    internal static void ReleaseLine(LineFader fader)
    {
        if (linePool.Count < 300)
        {
            fader.gameObject.SetActive(false);
            linePool.Push(fader);
        }
        else UnityEngine.Object.Destroy(fader.gameObject);
    }
}

// 잠깐 보였다 사라지는 선 (번개는 매 프레임 모양이 바뀜)
public class LineFader : MonoBehaviour
{
    private LineRenderer line;
    private Vector2 a, b;
    private Color color;
    private float width, duration, time, jitter, nextShape;
    private bool bolt, live;

    public void Init(LineRenderer l, Vector2 from, Vector2 to, Color c, float w, float seconds, bool isBolt, float j)
    {
        line = l; a = from; b = to; color = c; width = w; duration = Mathf.Max(0.01f, seconds); bolt = isBolt; jitter = j;
        time = 0f; nextShape = 0f;
        if (!live) { live = true; JobTierUtil.activeFlashLines++; }
        if (bolt) JobTierUtil.SetBolt(line, a, b, jitter);
        else JobTierUtil.SetStraight(line, a, b);
        Apply();
    }

    private void Update()
    {
        time += Time.deltaTime;
        if (time >= duration) { Finish(); return; }
        // 번개는 매 프레임 모양이 바뀌며 지직거림 (점 배열은 재사용이라 메모리 증가 없음)
        if (bolt) JobTierUtil.SetBolt(line, a, b, jitter);
        Apply();
    }

    private void Apply()
    {
        float k = 1f - Mathf.Clamp01(time / duration);
        Color c = color; c.a *= k;
        line.startColor = line.endColor = c;
        line.startWidth = line.endWidth = width * (0.4f + 0.6f * k);
    }

    private void Finish()
    {
        if (live) { live = false; JobTierUtil.activeFlashLines = Mathf.Max(0, JobTierUtil.activeFlashLines - 1); }
        JobTierUtil.ReleaseLine(this);
    }

    private void OnDestroy()
    {
        if (live) { live = false; JobTierUtil.activeFlashLines = Mathf.Max(0, JobTierUtil.activeFlashLines - 1); }
    }
}

// 적 밀치기 (EnemyMovement를 잠깐 끔)
public class EnemyPush : MonoBehaviour
{
    private EnemyMovement movement;
    private Rigidbody2D body;
    private Vector2 velocity;
    private float remaining;

    public void Begin(Vector2 v, float seconds)
    {
        if (movement == null) movement = GetComponent<EnemyMovement>();
        if (body == null) body = GetComponent<Rigidbody2D>();
        velocity = v;
        remaining = seconds;
        if (movement != null) movement.enabled = false;
        enabled = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 pos = (Vector2)transform.position + velocity * dt;
        if (body != null) { body.linearVelocity = Vector2.zero; body.position = pos; }
        transform.position = pos;
        velocity = Vector2.Lerp(velocity, Vector2.zero, 8f * dt);
        remaining -= dt;
        if (remaining <= 0f)
        {
            if (movement != null && GetComponent<SkillStun>() == null) movement.enabled = true;
            enabled = false;
        }
    }
}

// 그림자 궁수의 저주 표식 (보라색 고리). 일정 시간 뒤 사라짐.
public class ShadowMark : MonoBehaviour
{
    // 3차 그림자 패시브(ShadowMarkPassive)가 켜져 있을 때: 표식 붙은 적이 죽으면 주변으로 표식이 퍼짐 (라이즈 E 느낌)
    public static bool SpreadOnDeath;
    public static float DeathSpreadRadius = 2.5f;
    public static int DeathSpreadCount = 3;
    public static float DeathSpreadSeconds = 6f;

    private float remaining;
    private bool removedByCode; // 터뜨림/시간 끝 → 퍼지지 않음
    private EnemyHealth owner;
    private SpriteRenderer ring;
    private SpriteRenderer icon;
    private Transform iconRoot;
    private float age;
    private Sprite[] markFrames;
    private float markScale = 1f;

    public static void Apply(EnemyHealth enemy, float seconds)
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return;
        ShadowMark mark = enemy.GetComponent<ShadowMark>();
        if (mark == null) mark = enemy.gameObject.AddComponent<ShadowMark>();
        mark.remaining = Mathf.Max(mark.remaining, seconds);
    }

    // center 주변 적에게 표식을 퍼뜨림 (보라색 선으로 연결되는 연출). 퍼진 적 수를 돌려줌
    public static int Spread(Transform container, Vector2 center, float radius, float seconds, EnemyHealth except = null)
    {
        int n = 0;
        List<EnemyHealth> all = SkillUtil.EnemiesNow(container);
        for (int i = 0; i < all.Count; i++)
        {
            EnemyHealth e = all[i];
            if (e == null || e == except || !e.isActiveAndEnabled) continue;
            Vector2 p = e.transform.position;
            if (Vector2.Distance(center, p) > radius) continue;
            Apply(e, seconds);
            JobTierUtil.FlashLine(center, p, JobTierUtil.ShadowColor, 0.06f, 0.25f, false);
            n++;
        }
        return n;
    }

    public static bool Has(EnemyHealth enemy)
    {
        return enemy != null && enemy.GetComponent<ShadowMark>() != null;
    }

    // 표식을 터뜨림 (지움)
    public static void Consume(EnemyHealth enemy)
    {
        ShadowMark mark = enemy != null ? enemy.GetComponent<ShadowMark>() : null;
        if (mark != null) { mark.removedByCode = true; Destroy(mark); }
    }

    private void Awake()
    {
        GameObject obj = new GameObject("ShadowMark");
        obj.transform.SetParent(transform, false);
        obj.transform.localScale = Vector3.one * 0.9f;
        ring = obj.AddComponent<SpriteRenderer>();
        ring.sprite = SkillUtil.RingSprite();
        ring.color = JobTierUtil.ShadowColor;
        ring.sortingOrder = 58;

        // 머리 위 표식 (마름모)
        GameObject top = new GameObject("ShadowMarkIcon");
        iconRoot = top.transform;
        iconRoot.SetParent(transform, false);
        iconRoot.localPosition = new Vector3(0f, 0.75f, 0f);
        iconRoot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        iconRoot.localScale = new Vector3(0.28f, 0.28f, 1f);
        icon = top.AddComponent<SpriteRenderer>();
        icon.sprite = SkillUtil.CircleSprite();
        icon.color = JobTierUtil.ShadowColor;
        icon.sortingOrder = 59;
        markFrames = SkillFx.Frames("legion_mark"); // 표식 그림이 있으면 그걸로
        if (markFrames != null)
        {
            iconRoot.localRotation = Quaternion.identity;
            icon.sprite = markFrames[0];
            icon.color = Color.white;
            markScale = 0.75f / Mathf.Max(0.01f, markFrames[0].bounds.size.y);
        }
        SkillFx.Burst(transform.position, 0.2f, 1.2f, JobTierUtil.ShadowColor, 0.2f, true);
    }

    private void Update()
    {
        age += Time.deltaTime;
        remaining -= Time.deltaTime;
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 12f);
        if (ring != null)
        {
            Color c = JobTierUtil.ShadowColor; c.a = pulse;
            ring.color = c;
            ring.transform.Rotate(0f, 0f, 120f * Time.deltaTime);
        }
        if (iconRoot != null)
        {
            // 처음엔 크게 나타났다가 작아지고, 위아래로 둥실
            float pop = 1f + Mathf.Max(0f, 0.25f - age) * 4f;
            iconRoot.localPosition = new Vector3(0f, 0.75f + Mathf.Sin(age * 5f) * 0.06f, 0f);
            Color c = markFrames != null ? Color.white : JobTierUtil.ShadowColor;
            c.a = remaining < 1f ? Mathf.Clamp01(remaining) * pulse : 1f; // 끝나기 1초 전부터 깜빡임
            icon.color = c;
            if (markFrames != null && markFrames.Length >= 6)
            {
                // 표식 그림: 0~2 나타남 → 3~5 반짝이며 반복
                int f = age < 0.3f ? Mathf.Min(2, Mathf.FloorToInt(age / 0.3f * 3f)) : 3 + Mathf.FloorToInt((age - 0.3f) * 6f) % 3;
                icon.sprite = markFrames[f];
                iconRoot.localScale = new Vector3(markScale, markScale, 1f);
            }
            else iconRoot.localScale = new Vector3(0.28f, 0.28f, 1f) * pop;
        }
        if (remaining <= 0f) { removedByCode = true; Destroy(this); }
    }

    private void OnDestroy()
    {
        if (ring != null) Destroy(ring.gameObject);
        if (iconRoot != null) Destroy(iconRoot.gameObject);
        if (!removedByCode && SpreadOnDeath) SpreadFromDeath();
    }

    // 적이 죽어서 표식이 사라질 때만 (체력 0 확인) → 가까운 적 몇 마리에게 표식이 옮겨붙음
    private void SpreadFromDeath()
    {
        if (owner == null) owner = GetComponent<EnemyHealth>();
        if (owner == null || !DamageTracker.TryReadHp(owner, out int hp) || hp > 0) return; // 씬 정리 등은 무시
        Vector2 center = transform.position;
        List<EnemyHealth> all = SkillUtil.EnemiesNow(SkillUtil.EnemyContainer(null));
        float maxSqr = DeathSpreadRadius * DeathSpreadRadius;
        for (int n = 0; n < DeathSpreadCount; n++)
        {
            EnemyHealth best = null; float bestD = maxSqr;
            for (int i = 0; i < all.Count; i++)
            {
                EnemyHealth e = all[i];
                if (e == null || e == owner || !e.isActiveAndEnabled || Has(e)) continue;
                if (DamageTracker.TryReadHp(e, out int ehp) && ehp <= 0) continue;
                float d = ((Vector2)e.transform.position - center).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = e; }
            }
            if (best == null) break;
            Apply(best, DeathSpreadSeconds);
            JobTierUtil.FlashLine(center, best.transform.position, JobTierUtil.ShadowColor, 0.07f, 0.3f, true, 0.15f);
        }
        SkillFx.Burst(center, 0.3f, DeathSpreadRadius * 1.2f, JobTierUtil.ShadowColor, 0.3f, true);
    }
}

