using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 데미지 숫자. 맞은 위치에서 작은 숫자가 오른쪽 위 대각선으로 살짝 흐릿하게 빠르게 사라집니다.
//   적 피해   : 흰색 (큰 피해는 노란색, 크게)
//   플레이어  : 빨간 -숫자 / 회복은 초록 +숫자
// DamageTracker의 이벤트를 받아 표시만 합니다. (게임 시간 기준이라 일시정지하면 같이 멈춤)
[DisallowMultipleComponent]
public class DamageNumbers : MonoBehaviour
{
    [SerializeField, Min(1)] private int bigHitThreshold = 15;
    [Tooltip("보이는 시간(초). 짧을수록 바로 사라짐")]
    [SerializeField, Min(0.1f)] private float showSeconds = 0.45f;
    [Tooltip("사라지는 동안 움직이는 방향/거리 (픽셀). 기본: 오른쪽 위 대각선")]
    [SerializeField] private Vector2 drift = new Vector2(38f, 38f);
    [Tooltip("숫자 크기")]
    [SerializeField, Range(10, 80)] private int numberSize = 22;
    [Tooltip("최대 불투명도 (낮을수록 흐릿)")]
    [SerializeField, Range(0.1f, 1f)] private float opacity = 0.75f;
    [SerializeField, Range(10, 300)] private int maxOnScreen = 80;
    [Tooltip("비워두면 맑은 고딕 등 PC 기본 폰트")]
    [SerializeField] private Font font;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color bigColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private Color playerHurtColor = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color playerHealColor = new Color(0.4f, 1f, 0.5f);
    [Header("내가 맞는 숫자 (몹 숫자보다 크고 진하게)")]
    [SerializeField, Min(0.5f)] private float playerHurtScale = 1.5f;
    [SerializeField, Min(0.1f)] private float playerHurtSeconds = 0.8f;
    [SerializeField, Range(0.1f, 1f)] private float playerHurtOpacity = 1f;

    [Tooltip("확인용: 플레이어 체력이 바뀔 때마다 Console에 기록")]
    [SerializeField] private bool logPlayerHits = true;

    private class Popup
    {
        public Text text;
        public Vector3 world;
        public Transform follow; // 있으면 이 대상을 따라다님 (플레이어)
        public Vector2 jitter;
        public float age;
        public float scale;
        public Color color;
        public float seconds;   // 보이는 시간
        public float opacity;   // 최대 불투명도
        public float popFrom;   // 처음 튀어나오는 크기 배율
    }

    private RectTransform canvasRect;
    private GameObject canvasObject;
    private Font runtimeFont;
    private readonly List<Popup> active = new List<Popup>();
    private readonly Stack<Text> pool = new Stack<Text>();
    private bool loggedFirst;

    private void Awake()
    {
        if (font == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 32);
            font = runtimeFont;
        }

        canvasObject = new GameObject("DamageNumberCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7; // HUD(10) 아래 → 일시정지/Tab 창이 덮음
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvasObject.transform;
    }

    private void OnEnable()
    {
        DamageTracker.EnemyDamaged += OnEnemyDamaged;
        DamageTracker.PlayerHealthChanged += OnPlayerChanged;
    }

    private void OnDisable()
    {
        DamageTracker.EnemyDamaged -= OnEnemyDamaged;
        DamageTracker.PlayerHealthChanged -= OnPlayerChanged;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
        if (runtimeFont != null) Destroy(runtimeFont);
    }

    private void OnEnemyDamaged(EnemyHealth enemy, int amount, Vector3 position)
    {
        bool big = amount >= bigHitThreshold;
        Spawn(amount.ToString(), position, big ? bigColor : normalColor, big ? 1.25f : 1f);
    }

    // 플레이어 숫자는 플레이어를 따라다님 → 대쉬 중에 맞아도 캐릭터 위에 보임
    private void OnPlayerChanged(int delta, Vector3 position)
    {
        GameSession session = GameSession.Instance;
        Transform player = session != null && session.Player != null ? session.Player.transform : null;
        if (logPlayerHits && session != null && session.Player != null)
        {
            int now = session.Player.CurrentHealth;
            Debug.Log($"[데미지 숫자] 플레이어 {(delta < 0 ? delta.ToString() : "+" + delta)} " +
                      $"(HP {now - delta} → {now}) → 화면에 숫자 표시함");
        }
        if (delta < 0)
            Spawn(delta.ToString(), position, playerHurtColor, playerHurtScale, player,
                  playerHurtSeconds, playerHurtOpacity, 1.8f, true);
        else
            Spawn("+" + delta, position, playerHealColor, 1.1f, player);
    }

    private void Spawn(string value, Vector3 world, Color color, float scale, Transform follow = null,
        float seconds = -1f, float alpha = -1f, float popFrom = 1.2f, bool strong = false)
    {
        if (active.Count >= maxOnScreen)
            Recycle(0); // 너무 많으면 가장 오래된 것부터 정리

        if (!loggedFirst)
        {
            loggedFirst = true;
            Debug.Log($"[DamageNumbers] 첫 데미지 숫자 표시: {value}");
        }

        Text text = pool.Count > 0 ? pool.Pop() : CreateText();
        text.gameObject.SetActive(true);
        text.text = value;
        text.transform.SetAsLastSibling(); // 최신 숫자가 위에

        // 진한 숫자는 굵은 검은 테두리, 일반 숫자는 연한 테두리
        Outline outline = text.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = strong ? new Color(0f, 0f, 0f, 0.95f) : new Color(0f, 0f, 0f, 0.35f);
            outline.effectDistance = strong ? new Vector2(2.5f, -2.5f) : new Vector2(1f, -1f);
        }
        active.Add(new Popup
        {
            text = text,
            world = world,
            follow = follow,
            jitter = new Vector2(Random.Range(4f, 18f), Random.Range(12f, 24f)), // 맞은 곳 살짝 오른쪽 위에서 시작
            scale = scale,
            color = color,
            seconds = seconds > 0f ? seconds : showSeconds,
            opacity = alpha > 0f ? alpha : opacity,
            popFrom = popFrom,
        });
    }

    private Text CreateText()
    {
        GameObject obj = new GameObject("Num", typeof(RectTransform));
        obj.transform.SetParent(canvasRect, false);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = numberSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        ((RectTransform)obj.transform).sizeDelta = new Vector2(160f, 50f);
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.35f); // 흐릿한 느낌을 위해 테두리도 연하게
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private void Recycle(int index)
    {
        Popup p = active[index];
        p.text.gameObject.SetActive(false);
        pool.Push(p.text);
        active.RemoveAt(index);
    }

    private void LateUpdate()
    {
        Camera cam = GameCamera.Get();
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup p = active[i];
            p.age += Time.deltaTime;
            float t = p.age / p.seconds;
            if (t >= 1f || cam == null)
            {
                Recycle(i);
                continue;
            }

            Vector3 at = p.follow != null ? p.follow.position : p.world;
            Vector2 screen = cam.WorldToScreenPoint(at);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
            // 오른쪽 위 대각선으로 빠르게 빠졌다가 느려짐
            float move = 1f - (1f - t) * (1f - t);
            p.text.rectTransform.anchoredPosition = local + p.jitter + drift * move;

            // 아주 살짝만 커졌다가 원래 크기로
            float pop = t < 0.12f ? Mathf.Lerp(p.popFrom, 1f, t / 0.12f) : 1f;
            float s = p.scale * pop;
            p.text.rectTransform.localScale = new Vector3(s, s, 1f);

            // 흐릿하게: 최대 불투명도를 낮추고, 절반 지나면 서서히 사라짐
            Color c = p.color;
            c.a = p.opacity * (t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f);
            p.text.color = c;
        }
    }
}
