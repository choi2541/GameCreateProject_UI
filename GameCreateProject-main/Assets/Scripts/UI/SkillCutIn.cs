using Serializable = System.SerializableAttribute;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 스킬 컷인: 일러스트가 오른쪽에서 휙 들어왔다가 왼쪽으로 빠지는 연출 (메이플 6차 스킬 컷인 느낌).
// 그림은 Assets/Resources/CutIn/ 에서 이름으로 불러옵니다. (인스펙터 연결 필요 없음)
//   - 움직이는 컷인: CutIn/이름/ 폴더 안에 프레임 PNG 여러 장 (파일 이름 순서대로 재생)
//   - 멈춘 컷인   : CutIn/이름.png 한 장
//
//   SkillCutIn.Play("shadow_ascent");   // 그림자 궁수 어센트
//   SkillCutIn.Play("shadow_origin");   // 그림자 궁수 오리진
//   SkillCutIn.Play("storm_ascent");    // 폭풍 궁수 어센트
//   SkillCutIn.Play("storm_origin");    // 폭풍 궁수 오리진
//
// 처음 Play할 때 화면용 캔버스를 스스로 만듭니다. 게임 시간(timeScale)과 상관없이 재생됩니다.
// 에디터에서는 F5~F8로 4장을 미리 볼 수 있습니다. (빌드에는 들어가지 않음)
[DisallowMultipleComponent]
public class SkillCutIn : MonoBehaviour
{
    public const string ResourceFolder = "CutIn/";

    [Header("시간 (초)")]
    [SerializeField, Min(0.05f)] private float enterTime = 0.14f;
    [SerializeField, Min(0.05f)] private float holdTime = 0.55f;
    [SerializeField, Min(0.05f)] private float exitTime = 0.22f;

    [Header("연출")]
    [Tooltip("뒤 화면을 어둡게 하는 정도")]
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.5f;
    [Tooltip("시작할 때 하얗게 번쩍이는 정도")]
    [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.55f;
    [Tooltip("오른쪽에서 들어오는 거리 (px, 1920 기준)")]
    [SerializeField] private float enterDistance = 700f;
    [Tooltip("빠질 때 왼쪽으로 나가는 거리")]
    [SerializeField] private float exitDistance = 420f;
    [Tooltip("멈춰 있는 동안 천천히 밀리는 거리")]
    [SerializeField] private float driftDistance = 45f;
    [SerializeField, Range(0.5f, 1.5f)] private float illustrationScale = 1f;
    [Tooltip("스피드 라인 개수")]
    [SerializeField, Range(0, 40)] private int speedLines = 18;

    [Header("움직이는 컷인 (프레임 여러 장)")]
    [Tooltip("1초에 넘기는 장 수. 10이면 12장이 1.2초, 16장이 1.6초")]
    [SerializeField, Range(4f, 30f)] private float frameRate = 10f;

    [Serializable]
    public class Length
    {
        public string id = "storm_origin";
        [Tooltip("이 컷인이 처음부터 끝까지 재생되는 시간(초). 장 수와 상관없이 이 시간에 맞춰 재생")]
        [Min(0.2f)] public float seconds = 2f;
    }
    [Tooltip("컷인별 재생 시간 따로 정하기 (여기 없는 컷인은 위 Frame Rate 사용)")]
    [SerializeField] private Length[] lengths =
    {
        new Length { id = "storm_origin", seconds = 2f },
        new Length { id = "storm_ascent", seconds = 1.5f },
        new Length { id = "shadow_ascent", seconds = 1.6f },
        new Length { id = "shadow_origin", seconds = 2f },
    };
    [Tooltip("장과 장 사이를 살짝 겹쳐서 부드럽게 (AI 그림의 깜빡임 줄임)")]
    [SerializeField] private bool crossfade = true;
    [Tooltip("움직이는 컷인은 크게 날아오지 않고 이만큼만 살짝 밀려 들어옴")]
    [SerializeField] private float animEnterDistance = 120f;
    [Tooltip("마지막 몇 초 동안 사라짐 (그림 속 이펙트가 사라지는 장면과 겹치게)")]
    [SerializeField, Min(0.05f)] private float animFadeOut = 0.3f;

    private static SkillCutIn instance;
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, int> frameCounts = new Dictionary<string, int>(); // 장 수만 기억 (그림은 내려도)
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> kept = new HashSet<string>(); // 계속 들고 있는 컷인 (쓸 때마다 다시 불러와서 끊기지 않게)
    // 메모리 절약: 컷인 그림은 재생할 때만 올리고, 끝나면 내림 (평소 0MB, 재생 중 2~3초만)
    public static bool ReleaseAfterPlay = true;
    private string playingId;

    // 이름별 기본 색 (스피드 라인 / 어둠 색)
    private static readonly Dictionary<string, Color> tints = new Dictionary<string, Color>
    {
        { "shadow_ascent", new Color(0.55f, 0.25f, 0.95f) },
        { "shadow_origin", new Color(0.45f, 0.15f, 0.85f) },
        { "storm_ascent",  new Color(1f, 0.85f, 0.3f) },
        { "storm_origin",  new Color(0.45f, 0.95f, 0.8f) },
    };

    private Canvas canvas;
    private Image dim;
    private Image flash;
    private Image art;
    private RectTransform artRect;
    private readonly List<Image> lines = new List<Image>();
    private readonly List<float> lineSpeed = new List<float>();
    private Sprite lineSprite;
    private float time = -1f;
    private Sprite[] frames;
    private float hold;
    private Image artNext;
    private RectTransform artNextRect;
    private bool Animated => frames != null && frames.Length > 1;
    private float rate = 10f; // 이번 재생의 1초당 장 수
    private float TotalTime => Animated ? frames.Length / rate : enterTime + hold + exitTime;
    private Color tint = Color.white;

    public static bool IsPlaying => instance != null && instance.time >= 0f;

    // ───────── 재생 ─────────
    public static bool Play(string id)
    {
        Sprite[] loaded = Load(id);
        if (loaded == null)
        {
            Debug.LogWarning($"[컷인] Resources/{ResourceFolder}{id} (폴더 또는 .png) 를 찾지 못했습니다.");
            return false;
        }
        Color c = tints.TryGetValue(id, out Color t) ? t : Color.white;
        Ensure().Begin(loaded, c, id);
        return true;
    }

    public static void Play(Sprite sprite, Color tint)
    {
        if (sprite == null)
            return;
        Ensure().Begin(new[] { sprite }, tint, null);
    }

    // 이 컷인이 재생되는 시간(초). 그림이 없으면 0
    public static float Duration(string id)
    {
        int count;
        if (!frameCounts.TryGetValue(id ?? "", out count))
        {
            Sprite[] loaded = Load(id);
            if (loaded == null) return 0f;
            count = loaded.Length;
        }
        SkillCutIn c = Ensure();
        if (count <= 1)
            return c.enterTime + c.holdTime + c.exitTime;
        if (c.lengths != null)
            foreach (Length l in c.lengths)
                if (l != null && l.id == id && l.seconds > 0f)
                    return l.seconds;
        return count / c.frameRate;
    }

    // 컷인에서 아이콘으로 쓸 정사각형 그림 (캐릭터 얼굴 쪽을 잘라냄)
    public static Sprite Icon(string id, float frame = 0.45f)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (icons.TryGetValue(id, out Sprite cached) && cached != null) return cached;
        bool wasLoaded = cache.ContainsKey(id);
        Sprite[] loaded = Load(id);
        if (loaded == null) return null;
        Texture2D tex = loaded[Mathf.Clamp(Mathf.RoundToInt((loaded.Length - 1) * frame), 0, loaded.Length - 1)].texture;
        int size = Mathf.RoundToInt(tex.height * 0.62f) / 4 * 4; // 4의 배수 (압축 그림도 복사 가능하게)
        int x = Mathf.Clamp(Mathf.RoundToInt(tex.width * 0.74f - size * 0.5f), 0, tex.width - size) / 4 * 4;
        int y = Mathf.Clamp(Mathf.RoundToInt(tex.height * 0.58f - size * 0.5f), 0, tex.height - size) / 4 * 4;
        Sprite icon;
        if (SystemInfo.copyTextureSupport != UnityEngine.Rendering.CopyTextureSupport.None)
        {
            // 얼굴 부분만 작은 그림으로 복사 → 컷인 원본은 내려도 아이콘은 남음
            var small = new Texture2D(size, size, tex.format, false);
            small.filterMode = tex.filterMode;
            Graphics.CopyTexture(tex, 0, 0, x, y, size, size, small, 0, 0, 0, 0);
            icon = Sprite.Create(small, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
        else
        {
            icon = Sprite.Create(tex, new Rect(x, y, size, size), new Vector2(0.5f, 0.5f), 100f);
            icons[id] = icon;
            return icon; // 복사를 못 하는 기기: 원본을 계속 씀 (내리지 않음)
        }
        icons[id] = icon;
        if (!wasLoaded && ReleaseAfterPlay) Release(id);
        return icon;
    }

    // 컷인 그림을 메모리에서 내림 (다음 재생 때 다시 불러옴)
    public static void Release(string id)
    {
        if (string.IsNullOrEmpty(id) || kept.Contains(id) || !cache.TryGetValue(id, out Sprite[] s)) return;
        if (icons.TryGetValue(id, out Sprite ic) && ic != null && s != null)
            foreach (Sprite f in s) if (f != null && ic.texture == f.texture) return; // 아이콘이 원본을 쓰는 중
        cache.Remove(id);
        if (s == null) return;
        foreach (Sprite f in s)
        {
            if (f == null) continue;
            Texture2D t = f.texture;
            Object.Destroy(f);
            if (t != null) Resources.UnloadAsset(t);
        }
    }

    // 미리 불러 두기 (첫 재생 때 끊김 방지). 스킬이 생길 때 호출하면 좋음.
    public static void Preload(string id) => Load(id);

    // 지금 가진 스킬의 컷인: 미리 불러 두고 재생이 끝나도 내리지 않음 (4차 스킬처럼 자주 쓰는 것)
    // 쓸 때마다 디스크에서 다시 읽으면 그 순간 게임이 멈칫하고 컷인 앞부분이 잘림
    public static void Keep(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        kept.Add(id);
        Load(id);
    }

    // 스킬이 사라지면 다시 내릴 수 있게
    public static void Unkeep(string id)
    {
        if (string.IsNullOrEmpty(id) || !kept.Remove(id)) return;
        if (ReleaseAfterPlay && (instance == null || instance.playingId != id)) Release(id);
    }

    private static Sprite[] Load(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (cache.TryGetValue(id, out Sprite[] s) && s != null && s.Length > 0 && s[0] != null)
            return s;

        // 텍스처 타입이 Sprite가 아니어도 되도록 Texture2D로 불러서 직접 만듦
        List<Texture2D> textures = new List<Texture2D>(Resources.LoadAll<Texture2D>(ResourceFolder + id));
        if (textures.Count == 0)
        {
            Texture2D single = Resources.Load<Texture2D>(ResourceFolder + id);
            if (single != null)
                textures.Add(single);
        }
        if (textures.Count == 0)
            return null;

        textures.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // 00, 01, 02 ... 순서
        s = new Sprite[textures.Count];
        for (int i = 0; i < textures.Count; i++)
        {
            Texture2D tex = textures[i];
            s[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s[i].name = tex.name;
        }
        cache[id] = s;
        frameCounts[id] = s.Length;
        return s;
    }

    private static SkillCutIn Ensure()
    {
        if (instance != null)
            return instance;
        GameObject obj = new GameObject("SkillCutIn");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<SkillCutIn>();
        return instance;
    }

    // ───────── 화면 만들기 ─────────
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        GameObject canvasObj = new GameObject("SkillCutInCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 115; // 2차 전직 창(110) 위, 게임 클리어(120) 아래
        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasObj.transform;

        dim = FullImage("Dim", root, Color.black);

        lineSprite = MakeLineSprite();
        for (int i = 0; i < speedLines; i++)
        {
            Image line = NewImage("SpeedLine", root);
            line.sprite = lineSprite;
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            lines.Add(line);
            lineSpeed.Add(0f);
        }

        art = NewImage("Illustration", root);
        art.preserveAspect = true;
        artRect = art.rectTransform;
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
        artRect.sizeDelta = new Vector2(1920f, 1080f);

        artNext = NewImage("IllustrationNext", root); // 다음 장 (겹쳐서 부드럽게)
        artNext.preserveAspect = true;
        artNextRect = artNext.rectTransform;
        artNextRect.anchorMin = artNextRect.anchorMax = new Vector2(0.5f, 0.5f);
        artNextRect.sizeDelta = new Vector2(1920f, 1080f);
        artNext.enabled = false;

        flash = FullImage("Flash", root, Color.white);
        canvasObj.SetActive(false);
    }

    private static Image NewImage(string name, RectTransform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        Image image = obj.AddComponent<Image>();
        image.raycastTarget = false; // 클릭을 막지 않음
        return image;
    }

    private static Image FullImage(string name, RectTransform parent, Color color)
    {
        Image image = NewImage(name, parent);
        RectTransform r = image.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        color.a = 0f;
        image.color = color;
        return image;
    }

    // 가운데가 밝고 양끝이 투명한 가로 줄
    private static Sprite MakeLineSprite()
    {
        const int w = 128, h = 8;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = 1f - Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float a = Mathf.Sin(u * Mathf.PI) * v;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void Begin(Sprite[] sprites, Color c, string id)
    {
        rate = frameRate;
        if (lengths != null && id != null)
            foreach (Length l in lengths)
                if (l != null && l.id == id && l.seconds > 0f)
                    rate = sprites.Length / l.seconds;
        tint = c;
        if (playingId != null && playingId != id && ReleaseAfterPlay) Release(playingId); // 앞 컷인이 끝나기 전에 새 컷인
        playingId = id;
        frames = sprites;
        art.sprite = sprites[0];
        hold = holdTime;
        artRect.sizeDelta = new Vector2(1920f, 1080f) * illustrationScale;
        artNextRect.sizeDelta = artRect.sizeDelta;
        artNext.enabled = false;
        for (int i = 0; i < lines.Count; i++)
        {
            RectTransform r = lines[i].rectTransform;
            r.sizeDelta = new Vector2(Random.Range(260f, 700f), Random.Range(3f, 9f));
            r.anchoredPosition = new Vector2(Random.Range(-1100f, 1300f), Random.Range(-500f, 500f));
            lineSpeed[i] = Random.Range(2600f, 4200f);
        }
        time = 0f;
        canvas.gameObject.SetActive(true);
        Apply();
    }

    // ───────── 재생 ─────────
    private void Update()
    {
#if UNITY_EDITOR
        DebugKeys();
#endif
        if (time < 0f)
            return;

        // 렉으로 한 프레임이 길어져도 컷인 그림을 건너뛰지 않게 (한 번에 최대 1/30초만 진행)
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        time += dt;
        for (int i = 0; i < lines.Count; i++)
        {
            RectTransform r = lines[i].rectTransform;
            Vector2 p = r.anchoredPosition;
            p.x -= lineSpeed[i] * dt;
            if (p.x < -1300f)
                p.x += 2600f;
            r.anchoredPosition = p;
        }

        if (time >= TotalTime)
        {
            time = -1f;
            canvas.gameObject.SetActive(false);
            frames = null;
            art.sprite = null;
            artNext.sprite = null;
            if (ReleaseAfterPlay && playingId != null) Release(playingId); // 메모리에서 내림
            playingId = null;
            return;
        }
        Apply();
    }

    private void Apply()
    {
        if (Animated)
        {
            ApplyAnimated();
            return;
        }
        float x, alpha, scale, show;
        if (time < enterTime)
        {
            float t = EaseOut(time / enterTime);
            x = Mathf.Lerp(enterDistance, 0f, t);
            scale = Mathf.Lerp(1.12f, 1f, t);
            alpha = Mathf.Clamp01(t * 1.5f);
            show = t;
        }
        else if (time < enterTime + hold)
        {
            float t = (time - enterTime) / hold;
            x = -driftDistance * t;
            scale = 1f + 0.03f * t;
            alpha = 1f;
            show = 1f;
        }
        else
        {
            float t = EaseIn((time - enterTime - hold) / exitTime);
            x = Mathf.Lerp(-driftDistance, -driftDistance - exitDistance, t);
            scale = 1.03f;
            alpha = 1f - t;
            show = 1f - t;
        }

        artRect.anchoredPosition = new Vector2(x, 0f);
        artRect.localScale = new Vector3(scale, scale, 1f);
        art.color = new Color(1f, 1f, 1f, alpha);

        Color d = Color.Lerp(Color.black, tint * 0.25f, 0.5f);
        d.a = dimAlpha * show;
        dim.color = d;

        Color lc = Color.Lerp(tint, Color.white, 0.4f);
        lc.a = 0.55f * show;
        foreach (Image line in lines)
            line.color = lc;

        float f = flashAlpha * Mathf.Clamp01(1f - time / 0.16f);
        flash.color = new Color(1f, 1f, 1f, f);
    }

    // 움직이는 컷인: 그림 자체가 움직이니까 화면 이동은 최소로 (빠르게 나타나서 재생 → 끝부분에서 사라짐)
    private void ApplyAnimated()
    {
        float total = TotalTime;
        float alpha = Mathf.Clamp01(time / 0.08f) * Mathf.Clamp01((total - time) / animFadeOut);
        float x = Mathf.Lerp(animEnterDistance, 0f, EaseOut(time / 0.15f));

        float pos = time * rate;
        int index = Mathf.Clamp(Mathf.FloorToInt(pos), 0, frames.Length - 1);
        float blend = index < frames.Length - 1 && crossfade ? pos - index : 0f;

        art.sprite = frames[index];
        artRect.anchoredPosition = new Vector2(x, 0f);
        artRect.localScale = Vector3.one;
        art.color = new Color(1f, 1f, 1f, alpha);

        artNext.enabled = blend > 0.01f;
        if (artNext.enabled)
        {
            artNext.sprite = frames[index + 1];
            artNextRect.anchoredPosition = artRect.anchoredPosition;
            artNextRect.localScale = Vector3.one;
            artNext.color = new Color(1f, 1f, 1f, alpha * blend);
        }

        Color d = Color.Lerp(Color.black, tint * 0.25f, 0.5f);
        d.a = dimAlpha * alpha;
        dim.color = d;
        Color lc = Color.Lerp(tint, Color.white, 0.4f);
        lc.a = 0.55f * alpha;
        foreach (Image line in lines)
            line.color = lc;
        flash.color = new Color(1f, 1f, 1f, flashAlpha * Mathf.Clamp01(1f - time / 0.16f));
    }

    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    private static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }

#if UNITY_EDITOR
    // 에디터 전용: F5~F8 미리보기
    // UI-only merge: advanced-skill previews remain opt-in.
    private static void EditorBootstrap() => Ensure();

    private static void DebugKeys()
    {
        if (FKey(5)) Play("shadow_ascent");
        else if (FKey(6)) Play("shadow_origin");
        else if (FKey(7)) Play("storm_ascent");
        else if (FKey(8)) Play("storm_origin");
    }

    private static bool FKey(int n)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k == null) return false;
        switch (n)
        {
            case 5: return k.f5Key.wasPressedThisFrame;
            case 6: return k.f6Key.wasPressedThisFrame;
            case 7: return k.f7Key.wasPressedThisFrame;
            default: return k.f8Key.wasPressedThisFrame;
        }
#else
        return Input.GetKeyDown(KeyCode.F1 + (n - 1));
#endif
    }
#endif
}
