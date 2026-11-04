using UnityEngine;

// 보스 공격 예고 표시 (빨간 장판). 정해진 시간 동안 안쪽이 차오르고, 다 차면 사라집니다.
// 실제 피해는 BossController가 같은 시간에 판정합니다.
public class BossFx : MonoBehaviour
{
    private SpriteRenderer fill;
    private Transform fillTransform;
    private float duration;
    private float time;
    private bool isLine;
    private Vector3 fullScale;

    private static readonly Color AreaColor = new Color(1f, 0.15f, 0.1f, 0.18f);
    private static readonly Color FillColor = new Color(1f, 0.2f, 0.1f, 0.38f);
    private static readonly Color EdgeColor = new Color(1f, 0.3f, 0.2f, 0.85f);

    // 원형 예고 (내려찍기)
    public static BossFx Circle(Vector2 center, float radius, float duration)
    {
        BossFx fx = Create("BossWarnCircle", center, duration);
        fx.transform.localScale = Vector3.one * radius * 2f;

        Add(fx.transform, "Area", SkillUtil.CircleSprite(), AreaColor, 3);
        Add(fx.transform, "Edge", SkillUtil.RingSprite(), EdgeColor, 4);
        fx.fill = Add(fx.transform, "Fill", SkillUtil.CircleSprite(), FillColor, 5);
        fx.fillTransform = fx.fill.transform;
        fx.fullScale = Vector3.one;
        fx.fillTransform.localScale = Vector3.zero;
        return fx;
    }

    // 직선 예고 (돌진). origin에서 dir 방향으로 length만큼
    public static BossFx Line(Vector2 origin, Vector2 dir, float length, float width, float duration)
    {
        BossFx fx = Create("BossWarnLine", origin, duration);
        fx.isLine = true;
        fx.transform.rotation = SkillUtil.RotationFor(dir);

        SpriteRenderer area = Add(fx.transform, "Area", BossUtil.SquareLeft(), AreaColor, 3);
        area.transform.localScale = new Vector3(length, width, 1f);
        fx.fill = Add(fx.transform, "Fill", BossUtil.SquareLeft(), FillColor, 5);
        fx.fillTransform = fx.fill.transform;
        fx.fullScale = new Vector3(length, width, 1f);
        fx.fillTransform.localScale = new Vector3(0f, width, 1f);
        return fx;
    }

    private static BossFx Create(string name, Vector2 position, float duration)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = position;
        BossFx fx = obj.AddComponent<BossFx>();
        fx.duration = Mathf.Max(0.05f, duration);
        return fx;
    }

    private static SpriteRenderer Add(Transform parent, string name, Sprite sprite, Color color, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float t = Mathf.Clamp01(time / duration);
        fillTransform.localScale = isLine
            ? new Vector3(fullScale.x * t, fullScale.y, 1f)
            : fullScale * t;
        if (t >= 1f)
            Destroy(gameObject);
    }
}
