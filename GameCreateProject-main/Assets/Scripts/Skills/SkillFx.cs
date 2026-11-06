using System.Collections.Generic;
using UnityEngine;

// 간단한 스킬 연출 (임시). 게임 시간 기준이라 일시정지하면 같이 멈춥니다.
//  - Burst : 원이 커지면서 사라짐 (폭발, 착탄)
//  - Fall  : 위에서 떨어지는 화살 (화살비)
//  - Fly   : 한 점에서 다른 점으로 날아가는 화살 (연출용, 피해 없음)
//  - Anim  : Resources/SkillFx/{이름}/ 안의 프레임 그림을 순서대로 재생 (없으면 false → 기존 연출 사용)
// 메모리: 다 쓴 연출은 지우지 않고 꺼서 다시 씁니다(풀). 보이는 모습은 똑같습니다.
// MaxActive는 화면이 터질 듯한 극단적인 상황에서만 걸리는 안전장치입니다.
public class SkillFx : MonoBehaviour
{
    private enum Mode { Burst, Fall, Fly, Anim }

    // 안전장치: 동시에 보이는 연출 최대 수 (평소에는 닿지 않음. 피해에는 영향 없음)
    public static int MaxActive = 1500;
    // 꺼둔 채로 보관할 최대 수
    public static int MaxPooled = 400;

    private static readonly Stack<SkillFx> pool = new Stack<SkillFx>();
    private static int activeCount;
    private static Transform root;

    private Mode mode;
    private SpriteRenderer sr;
    private float duration;
    private float time;
    private Vector3 fromScale;
    private Vector3 toScale;
    private Vector2 fromPos;
    private Vector2 toPos;
    private Color baseColor;
    private bool live;
    private Sprite[] frames;
    private string landId;
    private float landSize, landDuration;

    // ───────── 프레임 그림 (Resources/SkillFx/{id}/ 의 PNG를 이름순으로) ─────────
    private const string FrameFolder = "SkillFx/";
    private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();

    // 프레임 그림 목록. 없으면 null. (한 번 읽으면 기억해 둠)
    public static Sprite[] Frames(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (frameCache.TryGetValue(id, out Sprite[] cached))
            return cached.Length > 0 ? cached : null;

        List<Texture2D> textures = new List<Texture2D>(Resources.LoadAll<Texture2D>(FrameFolder + id));
        textures.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        Sprite[] result = new Sprite[textures.Count];
        for (int i = 0; i < textures.Count; i++)
        {
            Texture2D tex = textures[i];
            tex.filterMode = FilterMode.Point; // 도트가 번지지 않게
            result[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        frameCache[id] = result;
        return result.Length > 0 ? result : null;
    }

    // 반복 재생용: 시간에 맞는 프레임 (그림이 없으면 null)
    public static Sprite LoopFrame(string id, float time, float fps)
    {
        Sprite[] f = Frames(id);
        if (f == null) return null;
        int i = Mathf.FloorToInt(time * fps) % f.Length;
        return f[i < 0 ? i + f.Length : i];
    }

    // 프레임 그림을 한 번 재생. height = 화면에서의 높이(월드 단위). 그림이 없으면 false
    public static bool Anim(string id, Vector2 position, float height, float duration, float rotation = 0f, int sortingOrder = 60)
    {
        Sprite[] f = Frames(id);
        if (f == null) return false;
        SkillFx fx = Create(position, f[0], Color.white, sortingOrder);
        if (fx == null) return true; // 안전장치에 걸림 (그림은 있음)
        fx.mode = Mode.Anim;
        fx.frames = f;
        fx.duration = duration;
        float scale = height / Mathf.Max(0.01f, f[0].bounds.size.y);
        fx.transform.localScale = new Vector3(scale, scale, 1f);
        fx.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        return true;
    }

    public static int ActiveCount => activeCount;

    public static void Burst(Vector2 position, float startDiameter, float endDiameter, Color color, float duration,
        bool ring = false, int sortingOrder = 55)
    {
        SkillFx fx = Create(position, ring ? SkillUtil.RingSprite() : SkillUtil.CircleSprite(), color, sortingOrder);
        if (fx == null) return;
        fx.mode = Mode.Burst;
        fx.duration = duration;
        fx.fromScale = Vector3.one * startDiameter;
        fx.toScale = Vector3.one * endDiameter;
        fx.transform.localScale = fx.fromScale;
    }

    // 프레임 그림으로 떨어지는 화살 (그림이 아래를 향함) + 땅에 닿으면 landId 이펙트. 그림이 없으면 false
    public static bool FallFrames(string id, Vector2 target, float fallHeight, float duration, float size,
        string landId = null, float landSize = 1f, float landDuration = 0.3f)
    {
        Sprite[] f = Frames(id);
        if (f == null) return false;
        SkillFx fx = Create(target + Vector2.up * fallHeight, f[0], Color.white, 61);
        if (fx == null) return true;
        fx.mode = Mode.Fall;
        fx.frames = f;
        fx.duration = duration;
        fx.fromPos = target + Vector2.up * fallHeight;
        fx.toPos = target;
        float s = size / Mathf.Max(0.01f, f[0].bounds.size.y);
        fx.transform.localScale = new Vector3(s, s, 1f);
        fx.landId = landId; fx.landSize = landSize; fx.landDuration = landDuration;
        return true;
    }

    // 두 점 사이를 잇는 프레임 그림 (저격 빔, 돌진 바람 등). 그림 가로 = 길이, 세로 = thickness. 없으면 false
    public static bool Beam(string id, Vector2 from, Vector2 to, float thickness, float duration, int sortingOrder = 62)
    {
        Sprite[] f = Frames(id);
        if (f == null) return false;
        Vector2 d = to - from;
        SkillFx fx = Create((from + to) * 0.5f, f[0], Color.white, sortingOrder);
        if (fx == null) return true;
        fx.mode = Mode.Anim;
        fx.frames = f;
        fx.duration = duration;
        Vector3 b = f[0].bounds.size;
        fx.transform.localScale = new Vector3(d.magnitude / Mathf.Max(0.01f, b.x), thickness / Mathf.Max(0.01f, b.y), 1f);
        fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        return true;
    }

    // 프레임 화살이 한 점에서 다른 점으로 날아감 (연출용). length = 긴 변 길이. 없으면 false
    public static bool FlyFrames(string id, Vector2 from, Vector2 to, float duration, float length, int sortingOrder = 61)
    {
        Sprite[] f = Frames(id);
        if (f == null) return false;
        SkillFx fx = Create(from, f[0], Color.white, sortingOrder);
        if (fx == null) return true;
        fx.mode = Mode.Fly;
        fx.frames = f;
        fx.duration = duration;
        fx.fromPos = from; fx.toPos = to;
        fx.transform.rotation = SkillUtil.RotationFor(to - from);
        float s = length / Mathf.Max(0.01f, Mathf.Max(f[0].bounds.size.x, f[0].bounds.size.y));
        fx.transform.localScale = new Vector3(s, s, 1f);
        return true;
    }

    // 계속 반복되는 프레임 그림을 parent 아래에 붙임 (size = 긴 변 길이). 그림이 없으면 null
    public static SpriteRenderer Loop(string id, Transform parent, float size, float fps, int sortingOrder)
    {
        Sprite[] f = Frames(id);
        if (f == null) return null;
        var go = new GameObject("Loop_" + id);
        go.transform.SetParent(parent, false);
        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = sortingOrder;
        Vector3 b = f[0].bounds.size;
        float s = size / Mathf.Max(0.01f, Mathf.Max(b.x, b.y));
        Vector3 ps = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(s / Mathf.Max(0.0001f, ps.x), s / Mathf.Max(0.0001f, ps.y), 1f);
        go.AddComponent<SkillFxLoop>().Set(id, fps, r);
        return r;
    }

    public static void Fall(Sprite arrow, Vector2 target, float height, Color color, float duration, float scale = 0.8f)
    {
        SkillFx fx = Create(target + Vector2.up * height, arrow != null ? arrow : SkillUtil.ArrowSprite(), color, 61);
        if (fx == null) return;
        fx.mode = Mode.Fall;
        fx.duration = duration;
        fx.fromPos = target + Vector2.up * height;
        fx.toPos = target;
        fx.transform.rotation = Quaternion.Euler(0f, 0f, -90f); // 아래를 향함
        float s = SkillUtil.ArrowScale(arrow != null ? arrow : SkillUtil.ArrowSprite(), scale);
        fx.transform.localScale = new Vector3(s, s, 1f);
    }

    public static void Fly(Sprite arrow, Vector2 from, Vector2 to, Color color, float duration, float scale = 1f, int sortingOrder = 61)
    {
        SkillFx fx = Create(from, arrow != null ? arrow : SkillUtil.ArrowSprite(), color, sortingOrder);
        if (fx == null) return;
        fx.mode = Mode.Fly;
        fx.duration = duration;
        fx.fromPos = from;
        fx.toPos = to;
        fx.transform.rotation = SkillUtil.RotationFor(to - from);
        float s = SkillUtil.ArrowScale(arrow != null ? arrow : SkillUtil.ArrowSprite(), scale);
        fx.transform.localScale = new Vector3(s, s, 1f);
    }

    private static SkillFx Create(Vector2 position, Sprite sprite, Color color, int sortingOrder)
    {
        if (activeCount >= MaxActive)
            return null;

        SkillFx fx = null;
        while (fx == null && pool.Count > 0)
            fx = pool.Pop(); // 씬이 바뀌어 지워진 것은 건너뜀
        if (fx == null)
        {
            if (root == null) root = new GameObject("[SkillFx]").transform;
            GameObject obj = new GameObject("SkillFx");
            obj.transform.SetParent(root, false);
            fx = obj.AddComponent<SkillFx>();
            fx.sr = obj.AddComponent<SpriteRenderer>();
        }

        Transform t = fx.transform;
        t.position = position;
        t.rotation = Quaternion.identity;
        t.localScale = Vector3.one;
        fx.sr.sprite = sprite;
        fx.sr.color = color;
        fx.sr.sortingOrder = sortingOrder;
        fx.baseColor = color;
        fx.time = 0f;
        fx.live = true;
        activeCount++;
        fx.gameObject.SetActive(true);
        return fx;
    }

    private void Update()
    {
        time += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(time / duration) : 1f;

        switch (mode)
        {
            case Mode.Burst:
                transform.localScale = Vector3.Lerp(fromScale, toScale, 1f - (1f - t) * (1f - t));
                SetAlpha(1f - t);
                break;
            case Mode.Fall:
                transform.position = Vector2.Lerp(fromPos, toPos, t * t);
                if (frames != null) sr.sprite = frames[Mathf.FloorToInt(time * 12f) % frames.Length];
                SetAlpha(t < 0.8f || landId != null ? 1f : (1f - t) / 0.2f);
                if (t >= 1f && landId != null) { string id = landId; landId = null; Anim(id, toPos, landSize, landDuration); }
                break;
            case Mode.Fly:
                transform.position = Vector2.Lerp(fromPos, toPos, t);
                if (frames != null) sr.sprite = frames[Mathf.FloorToInt(time * 12f) % frames.Length];
                SetAlpha(t < 0.85f ? 1f : (1f - t) / 0.15f);
                break;
            case Mode.Anim:
                sr.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];
                break;
        }

        if (t >= 1f)
            Release();
    }

    private void Release()
    {
        if (!live) return;
        live = false;
        frames = null;
        landId = null;
        activeCount = Mathf.Max(0, activeCount - 1);
        if (pool.Count < MaxPooled)
        {
            gameObject.SetActive(false);
            pool.Push(this);
        }
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 씬 전환 등으로 켜진 채 지워지면 개수만 정리
        if (live) { live = false; activeCount = Mathf.Max(0, activeCount - 1); }
    }

    private void SetAlpha(float a)
    {
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Clamp01(a);
        sr.color = c;
    }
}
