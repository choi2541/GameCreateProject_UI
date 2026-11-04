using UnityEngine;

// 보스가 쏘는 탄. 플레이어에게 닿으면 피해를 주고 사라집니다. (돌/나무는 통과)
public class BossBullet : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float range;
    private float travelled;
    private float radius;
    private int damage;
    private PlayerHealth player;

    public static void Spawn(Vector2 position, Vector2 direction, float speed, float range, int damage, Color color, float size = 0.35f)
    {
        GameObject obj = new GameObject("BossBullet");
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * size;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = SkillUtil.CircleSprite();
        sr.color = color;
        sr.sortingOrder = 58;

        BossBullet b = obj.AddComponent<BossBullet>();
        b.direction = direction.normalized;
        b.speed = speed;
        b.range = range;
        b.damage = damage;
        b.radius = size * 0.5f;
        b.player = BossUtil.Player();
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        if (step <= 0f)
            return;

        transform.position = (Vector2)transform.position + direction * step;
        travelled += step;

        if (player != null && !player.IsDead
            && Vector2.Distance(transform.position, player.transform.position) <= radius + 0.3f)
        {
            BossUtil.DamagePlayer(player, damage);
            Destroy(gameObject);
            return;
        }

        if (travelled >= range)
            Destroy(gameObject);
    }
}
