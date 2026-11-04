using UnityEngine;
using UnityEngine.UI;

// 마우스를 따라다니는 설명창. Canvas 맨 아래(가장 위에 그려지는 위치)에 하나만 둡니다.
[DisallowMultipleComponent]
public class UITooltip : MonoBehaviour
{
    public static UITooltip Instance { get; private set; }

    [SerializeField] private RectTransform root;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Vector2 offset = new Vector2(20f, -20f);

    private RectTransform canvasRect;
    private object owner;

    private void Awake()
    {
        Instance = this;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            canvasRect = (RectTransform)canvas.rootCanvas.transform;
        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void Show(object requester, string title, string body)
    {
        if (Instance != null)
            Instance.ShowInternal(requester, title, body);
    }

    // requester를 주면 그 대상이 띄운 툴팁일 때만 닫습니다.
    public static void Hide(object requester = null)
    {
        if (Instance == null || Instance.root == null)
            return;
        if (requester != null && requester != Instance.owner)
            return;
        Instance.owner = null;
        Instance.root.gameObject.SetActive(false);
    }

    private void ShowInternal(object requester, string title, string body)
    {
        if (root == null)
            return;

        owner = requester;
        if (titleText != null)
        {
            titleText.text = title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }
        if (bodyText != null)
        {
            bodyText.text = body;
            bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        }

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        Follow();
    }

    private void LateUpdate()
    {
        if (root != null && root.gameObject.activeSelf)
            Follow();
    }

    private void Follow()
    {
        if (canvasRect == null)
            return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, UIKeyInput.MousePosition, null, out Vector2 local);

        Vector2 half = canvasRect.rect.size * 0.5f;
        Vector2 size = root.rect.size;

        // 화면 밖으로 나가면 반대쪽으로 뒤집습니다.
        bool flipX = local.x + offset.x + size.x > half.x;
        bool flipY = local.y + offset.y - size.y < -half.y;

        root.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        root.anchoredPosition = local + new Vector2(flipX ? -offset.x : offset.x, flipY ? -offset.y : offset.y);
    }
}
