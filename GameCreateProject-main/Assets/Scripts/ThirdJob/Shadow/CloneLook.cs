using UnityEngine;

// 그림자 분신 그림 (Resources/SkillFx/legion_clone, 8칸):
//   0~2 연기에서 나타남 → 3~5 활을 당기고 쏘기를 반복 → 6~7 연기로 흩어짐
// 그림이 없으면 Apply가 false → 예전처럼 플레이어 그림을 보라로 칠한 분신 사용
public class CloneLook : MonoBehaviour
{
    public const string Id = "legion_clone";
    public const float Height = 1.9f;          // 화면에서의 키 (칸, 연기 포함)
    private const float AppearSeconds = 0.24f;
    private const float LoopFps = 6f;

    private SpriteRenderer sr;
    private Sprite[] frames;
    private float age, dissolveAt = -1f, dissolveTime = 0.3f;

    public static bool Has => SkillFx.Frames(Id) != null;

    // 분신 모습을 그림으로. scale을 돌려줌 (분신 크기 조절할 때 기준)
    public static bool Apply(SpriteRenderer target, bool faceLeft, out Vector3 scale)
    {
        scale = Vector3.one;
        Sprite[] f = SkillFx.Frames(Id);
        if (target == null || f == null) return false;
        CloneLook look = target.GetComponent<CloneLook>();
        if (look == null) look = target.gameObject.AddComponent<CloneLook>();
        look.sr = target; look.frames = f; look.age = 0f; look.dissolveAt = -1f;
        target.sprite = f[0];
        target.color = Color.white;
        target.flipX = faceLeft;   // 그림은 오른쪽을 봄
        float s = Height / Mathf.Max(0.01f, f[0].bounds.size.y);
        scale = new Vector3(s, s, 1f);
        target.transform.localScale = scale;
        look.enabled = true;
        return true;
    }

    // 연기로 흩어지며 사라짐 (seconds 동안)
    public static void Dissolve(SpriteRenderer target, float seconds)
    {
        CloneLook look = target != null ? target.GetComponent<CloneLook>() : null;
        if (look == null || !look.enabled) return;
        look.dissolveAt = look.age;
        look.dissolveTime = Mathf.Max(0.05f, seconds);
    }

    public static bool IsAnimated(SpriteRenderer target)
    {
        CloneLook look = target != null ? target.GetComponent<CloneLook>() : null;
        return look != null && look.enabled;
    }

    private void Update()
    {
        if (sr == null || frames == null || frames.Length < 8) return;
        age += Time.deltaTime;
        int f;
        if (dissolveAt >= 0f)
        {
            float k = (age - dissolveAt) / dissolveTime;
            f = k < 0.5f ? 6 : 7;
            Color c = sr.color; c.a = Mathf.Clamp01(1.4f - k); sr.color = c;
        }
        else if (age < AppearSeconds) f = Mathf.Min(2, Mathf.FloorToInt(age / AppearSeconds * 3f));
        else f = 3 + Mathf.FloorToInt((age - AppearSeconds) * LoopFps) % 3;
        sr.sprite = frames[f];
    }
}
