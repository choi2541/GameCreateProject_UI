using UnityEngine;

// 적을 잠깐 멈추게 합니다 (기절). 적의 EnemyMovement를 잠시 끄는 방식이라 팀 코드를 고치지 않습니다.
public class SkillStun : MonoBehaviour
{
    private EnemyMovement movement;
    private Rigidbody2D body;
    private SpriteRenderer[] sprites;
    private float remaining;
    private bool movementWasEnabled;
    private Color[] originalColors;

    public static void Apply(EnemyHealth enemy, float seconds)
    {
        if (enemy == null || enemy.IsDead || seconds <= 0f)
            return;
        SkillStun stun = enemy.GetComponent<SkillStun>();
        if (stun == null)
            stun = enemy.gameObject.AddComponent<SkillStun>();
        stun.remaining = Mathf.Max(stun.remaining, seconds);
    }

    // 색을 바꿔서 멈춤 (예: 가시 덩굴 속박 = 초록)
    public static void Apply(EnemyHealth enemy, float seconds, Color tint)
    {
        Apply(enemy, seconds);
        SkillStun stun = enemy != null ? enemy.GetComponent<SkillStun>() : null;
        if (stun != null) stun.Tint(tint);
    }

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        body = GetComponent<Rigidbody2D>();
        sprites = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) originalColors[i] = sprites[i].color;
        movementWasEnabled = movement != null && movement.enabled;
        if (movement != null)
            movement.enabled = false;
        Tint(new Color(0.6f, 0.85f, 1f));
    }

    private void Update()
    {
        if (body != null)
            body.linearVelocity = Vector2.zero;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
            Destroy(this);
    }

    private void OnDestroy()
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (movement != null && (health == null || !health.IsDead))
            movement.enabled = movementWasEnabled;
        if (sprites != null)
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] != null)
                {
                    Color color = originalColors[i];
                    color.a = sprites[i].color.a;
                    sprites[i].color = color;
                }
    }

    private void Tint(Color color)
    {
        if (sprites == null)
            return;
        foreach (SpriteRenderer sr in sprites)
        {
            if (sr == null)
                continue;
            Color c = color;
            c.a = sr.color.a;
            sr.color = c;
        }
    }
}
