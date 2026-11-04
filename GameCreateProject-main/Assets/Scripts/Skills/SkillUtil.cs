using System.Collections.Generic;
using UnityEngine;

// 스킬들이 같이 쓰는 도구: 적 찾기, 범위 피해, 장애물 검사, 임시 이미지 생성
public static class SkillUtil
{
    // 적 크기를 대략 이 반지름으로 보고 맞았는지 판정합니다.
    public const float EnemyRadius = 0.35f;

    private static Transform cachedContainer;
    private static readonly List<Collider2D> overlapResults = new List<Collider2D>(8);

    private static Sprite arrowSprite;
    private static Sprite circleSprite;
    private static Sprite ringSprite;

    // 적들이 들어있는 부모 오브젝트 (EnemySpawner). 직접 지정하지 않으면 자동으로 찾습니다.
    public static Transform EnemyContainer(Transform preferred)
    {
        if (preferred != null)
            return preferred;
        if (cachedContainer == null)
        {
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            cachedContainer = spawner != null ? spawner.transform : null;
        }
        return cachedContainer;
    }

    // ───────── 적 목록 (프레임마다 한 번만 모음 → 메모리 절약) ─────────
    private class EnemyCache { public int frame = -1; public readonly List<EnemyHealth> list = new List<EnemyHealth>(256); }
    private static readonly Dictionary<Transform, EnemyCache> enemyCaches = new Dictionary<Transform, EnemyCache>();
    private static readonly List<EnemyHealth> emptyEnemies = new List<EnemyHealth>(0);

    // 이번 프레임의 살아있는 적 목록 (공용). 읽기만 하고 수정·보관하지 마세요.
    // 같은 프레임 안에서는 내용이 바뀌지 않으므로 반복 중에 적이 죽거나 다른 스킬이 호출해도 안전합니다.
    // (그 사이 죽은 적이 들어있을 수 있으니 null / isActiveAndEnabled 확인)
    public static List<EnemyHealth> EnemiesNow(Transform container)
    {
        if (container == null)
            return emptyEnemies;
        if (!enemyCaches.TryGetValue(container, out EnemyCache cache))
        {
            if (enemyCaches.Count > 8) enemyCaches.Clear(); // 씬이 바뀌어 남은 항목 정리
            cache = new EnemyCache();
            enemyCaches[container] = cache;
        }
        if (cache.frame != Time.frameCount)
        {
            cache.frame = Time.frameCount;
            cache.list.Clear();
            foreach (Transform child in container)
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;
                EnemyHealth health = child.GetComponent<EnemyHealth>();
                if (health != null && health.isActiveAndEnabled)
                    cache.list.Add(health);
            }
        }
        return cache.list;
    }

    // 살아있는 적 목록의 복사본 (보관하거나 수정해도 되는 새 목록)
    public static List<EnemyHealth> Enemies(Transform container)
    {
        return new List<EnemyHealth>(EnemiesNow(container));
    }

    public static EnemyHealth Nearest(Transform container, Vector2 from, float range)
    {
        EnemyHealth best = null;
        float bestDistance = range;
        List<EnemyHealth> list = EnemiesNow(container);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth enemy = list[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
                continue;
            float distance = Vector2.Distance(from, enemy.transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }
        return best;
    }

    // 원 안의 적에게 피해. 맞은 적 수를 돌려줍니다.
    public static int DamageInRadius(Transform container, Vector2 center, float radius, int damage)
    {
        int count = 0;
        List<EnemyHealth> list = EnemiesNow(container);
        for (int i = 0; i < list.Count; i++)
        {
            EnemyHealth enemy = list[i];
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled)
                continue;
            if (Vector2.Distance(center, enemy.transform.position) <= radius + EnemyRadius)
            {
                enemy.TakeDamage(damage);
                count++;
            }
        }
        return count;
    }

    public static int ObstacleMask => LayerMask.GetMask("Obstacle");

    // 돌/나무 기둥(트리거가 아닌 콜라이더)에 닿는지
    public static bool HitsObstacle(Vector2 position, float radius, int mask)
    {
        if (mask == 0)
            return false;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = false;
        return Physics2D.OverlapCircle(position, radius, filter, overlapResults) > 0;
    }

    // 방향키 입력 (구 Input Manager 기준. 팀 코드와 동일)
    public static Vector2 MoveInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return input.sqrMagnitude > 0.01f ? input.normalized : Vector2.zero;
    }

    public static bool IsInsideCamera(Vector2 position, float margin)
    {
        Camera cam = GameCamera.Get(); // Tag가 MainCamera가 아니어도 동작
        if (cam == null || !cam.orthographic)
            return true;
        Vector2 center = cam.transform.position;
        float halfH = cam.orthographicSize + margin;
        float halfW = cam.orthographicSize * cam.aspect + margin;
        return Mathf.Abs(position.x - center.x) <= halfW && Mathf.Abs(position.y - center.y) <= halfH;
    }

    public static Quaternion RotationFor(Vector2 direction)
    {
        return Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ───────── 임시 이미지 (화살 이미지를 지정하지 않았을 때 사용) ─────────

    // 오른쪽을 향한 흰색 화살 (가로 1칸)
    public static Sprite ArrowSprite()
    {
        if (arrowSprite != null)
            return arrowSprite;
        const int w = 32, h = 8;
        Texture2D tex = NewTexture(w, h);
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cy = Mathf.Abs(y + 0.5f - h * 0.5f);
                bool shaft = x < 24 && cy < 1.2f;
                bool head = x >= 22 && cy < (w - x) * 0.45f;
                bool fletch = x < 6 && cy < 3.2f && cy > 1.2f;
                px[y * w + x] = shaft || head || fletch ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        return arrowSprite;
    }

    // 지름 1칸짜리 흰 원
    public static Sprite CircleSprite()
    {
        if (circleSprite == null)
            circleSprite = MakeDisc(64, 0f);
        return circleSprite;
    }

    // 지름 1칸짜리 흰 테두리 원
    public static Sprite RingSprite()
    {
        if (ringSprite == null)
            ringSprite = MakeDisc(64, 0.12f);
        return ringSprite;
    }

    private static Sprite MakeDisc(int size, float ringThickness)
    {
        Texture2D tex = NewTexture(size, size);
        Color32[] px = new Color32[size * size];
        float r = size * 0.5f;
        float inner = ringThickness > 0f ? r * (1f - ringThickness * 2f) : -1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d);
                if (inner > 0f)
                    a *= Mathf.Clamp01(d - inner);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Texture2D NewTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}
