using UnityEngine;
using UnityEngine.UI;

// 체력이 낮으면 화면 가장자리가 붉게 두근거립니다.
// 화면 전체를 덮는 Image에 붙입니다 (가운데는 투명, 가장자리만 붉게 — 그림은 실행 시 자동 생성).
[RequireComponent(typeof(Image))]
public class LowHealthVignette : MonoBehaviour
{
    [Tooltip("체력이 이 비율 이하이면 표시 (0.3 = 30%)")]
    [SerializeField, Range(0.05f, 0.9f)] private float threshold = 0.3f;
    [SerializeField] private Color color = new Color(0.85f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;
    [Tooltip("두근거리는 속도 (체력이 낮을수록 빨라짐)")]
    [SerializeField, Min(0.1f)] private float beatSpeed = 4f;

    private Image image;
    private float alpha;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = MakeVignette(128);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        SetAlpha(0f);
    }

    private void Update()
    {
        GameSession session = GameSession.Instance;
        PlayerHealth player = session != null ? session.Player : null;
        float target = 0f;

        if (player != null && !player.IsDead && player.MaxHealth > 0)
        {
            float ratio = (float)player.CurrentHealth / player.MaxHealth;
            if (ratio <= threshold)
            {
                float danger = 1f - ratio / threshold; // 0 ~ 1 (낮을수록 위험)
                float speed = beatSpeed * (1f + danger);
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
                target = Mathf.Lerp(minAlpha, maxAlpha, beat) * Mathf.Lerp(0.7f, 1f, danger);
            }
        }

        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 2f);
        SetAlpha(alpha);
    }

    private void SetAlpha(float a)
    {
        Color c = color;
        c.a = a;
        image.color = c;
        image.enabled = a > 0.001f;
    }

    // 가운데 투명 → 가장자리 불투명 (흰색, 색은 Image.color로 입힘)
    private static Sprite MakeVignette(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v) / 1.41421f;            // 0(가운데) ~ 1(모서리)
                float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                a = a * a * (3f - 2f * a);                                   // 부드럽게
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
