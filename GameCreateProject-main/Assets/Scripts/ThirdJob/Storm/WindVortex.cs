using System.Collections.Generic;
using UnityEngine;

// 회오리 오브젝트 (3차 X, 4차 오리진이 같이 사용). 적을 빨아들이며 일정 간격으로 피해.
// Resources/SkillFx/storm_tornado/ 그림이 있으면 토네이도 그림으로, 없으면 고리 여러 개로 그립니다.
public class WindVortex : MonoBehaviour
{
    public float radius = 2.2f;      // 빨아들이는 범위
    public float coreRadius = 1.1f;  // 피해 범위
    public float pullSpeed = 3f;
    public float tickInterval = 0.25f;
    public int tickDamage = 4;
    public float lifetime = 2.5f;
    public Vector2 velocity;         // 0이 아니면 돌아다님
    public Transform enemies;

    private readonly List<Transform> rings = new List<Transform>();
    private readonly List<SpriteRenderer> ringRenderers = new List<SpriteRenderer>();
    private float age, nextTick;
    private float scale = 1f;
    private SpriteRenderer tornado;

    public const string TornadoFx = "storm_tornado";
    public static float TornadoHeight = 2.6f;   // size 1일 때 토네이도 높이(월드 단위)
    public static float TornadoFps = 14f;

    public static WindVortex Spawn(Vector2 position, float size, Transform enemyContainer)
    {
        GameObject obj = new GameObject("WindVortex");
        obj.transform.position = position;
        WindVortex v = obj.AddComponent<WindVortex>();
        v.enemies = enemyContainer;
        v.scale = size;

        if (SkillFx.Frames(TornadoFx) != null)
        {
            GameObject t = new GameObject("Tornado");
            t.transform.SetParent(obj.transform, false);
            v.tornado = t.AddComponent<SpriteRenderer>();
            v.tornado.sprite = SkillFx.LoopFrame(TornadoFx, 0f, TornadoFps);
            v.tornado.sortingOrder = 57;
            v.ApplyTornadoSize();
            return v;
        }

        for (int i = 0; i < 6; i++)
        {
            GameObject ring = new GameObject("Ring" + i);
            ring.transform.SetParent(obj.transform, false);
            float k = (i + 1) / 6f;
            ring.transform.localPosition = new Vector3(0f, (i - 2.5f) * 0.18f * size, 0f);
            ring.transform.localScale = new Vector3(0.6f + 1.6f * k, (0.6f + 1.6f * k) * 0.35f, 1f) * size;
            SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = SkillUtil.RingSprite();
            sr.color = new Color(0.75f, 1f, 0.93f, 0.75f);
            sr.sortingOrder = 57;
            v.rings.Add(ring.transform);
            v.ringRenderers.Add(sr);
        }
        return v;
    }

    public void SetSize(float size)
    {
        float ratio = size / scale;
        scale = size;
        if (tornado != null) { ApplyTornadoSize(); return; }
        foreach (Transform r in rings)
        {
            r.localScale *= ratio;
            r.localPosition *= ratio;
        }
    }

    // 토네이도 아래쪽 끝이 회오리 중심(빨아들이는 곳)에 오도록 위로 올림
    private void ApplyTornadoSize()
    {
        float h = TornadoHeight * scale;
        float s = h / Mathf.Max(0.01f, tornado.sprite.bounds.size.y);
        tornado.transform.localScale = new Vector3(s, s, 1f);
        tornado.transform.localPosition = new Vector3(0f, h * 0.38f, 0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (velocity != Vector2.zero)
        {
            Vector2 pos = (Vector2)transform.position + velocity * dt;
            if (!SkillUtil.IsInsideCamera(pos, -1f)) velocity = -velocity; // 화면 안에서만 돌아다님
            transform.position = pos;
        }

        for (int i = 0; i < rings.Count; i++)
        {
            float sway = Mathf.Sin(Time.time * 10f + i * 0.8f) * 0.08f * scale;
            Vector3 p = rings[i].localPosition; p.x = sway; rings[i].localPosition = p;
        }
        float alpha = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((lifetime - age) / 0.3f);
        if (tornado != null)
        {
            tornado.sprite = SkillFx.LoopFrame(TornadoFx, age, TornadoFps);
            tornado.color = new Color(1f, 1f, 1f, alpha);
        }
        for (int i = 0; i < ringRenderers.Count; i++)
        {
            Color c = ringRenderers[i].color; c.a = 0.75f * alpha; ringRenderers[i].color = c;
        }

        Vector2 center = transform.position;
        List<EnemyHealth> all = SkillUtil.EnemiesNow(enemies);
        for (int i = 0; i < all.Count; i++)
        {
            EnemyHealth e = all[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            float d = Vector2.Distance(center, e.transform.position);
            if (d <= radius * scale)
                JobTierUtil.Pull(e, center, pullSpeed);
        }
        if (age >= nextTick)
        {
            nextTick = age + tickInterval;
            SkillUtil.DamageInRadius(enemies, center, coreRadius * scale, tickDamage);
        }
        if (age >= lifetime) Destroy(gameObject);
    }
}
