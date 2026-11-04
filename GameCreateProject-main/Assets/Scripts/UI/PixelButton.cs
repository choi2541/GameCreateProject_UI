using UnityEngine;
using UnityEngine.UI;

// 도트 버튼: 누를 수 없으면 "누를 수 없음" 그림으로, 버튼 크기가 바뀌면 그림을 다시 맞춤.
// 그림(Art)은 버튼 자식. 버튼 칸 = 그림의 몸통(판), 잎·리본 같은 장식은 칸 밖으로. (Pixel UI - Apply 메뉴가 붙임)
[DisallowMultipleComponent]
public class PixelButton : MonoBehaviour
{
    [SerializeField] private Image art;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite disabledSprite;
    [Tooltip("그림에서 몸통 바깥 장식 크기 (그림 픽셀: 왼, 아래, 오른, 위)")]
    [SerializeField] private Vector4 normalInset;
    [SerializeField] private Vector4 disabledInset;
    [SerializeField] private Text label;

    private Button button;
    private bool lastInteractable = true;
    private bool first = true;

    public void Set(Image artImage, Sprite normal, Vector4 normalBody, Sprite disabled, Vector4 disabledBody, Text text)
    {
        art = artImage; normalSprite = normal; normalInset = normalBody;
        disabledSprite = disabled; disabledInset = disabledBody; label = text;
        Fit(art, normalSprite, normalInset);
    }

    private void Awake() => button = GetComponent<Button>();

    private void LateUpdate()
    {
        if (button == null || art == null) return;
        bool on = button.interactable;
        if (!first && on == lastInteractable) return;
        first = false;
        lastInteractable = on;
        bool useDisabled = !on && disabledSprite != null;
        Fit(art, useDisabled ? disabledSprite : normalSprite, useDisabled ? disabledInset : normalInset);
        if (label != null)
        {
            Color c = label.color;
            c.a = on ? 1f : 0.7f;
            label.color = c;
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (art == null || art.sprite == null) return;
        bool useDisabled = disabledSprite != null && art.sprite == disabledSprite;
        Fit(art, art.sprite, useDisabled ? disabledInset : normalInset);
    }

    // 몸통 높이를 버튼 높이에 맞추고, 장식만큼 그림을 버튼 밖으로 넓힘
    public static void Fit(Image art, Sprite sprite, Vector4 inset)
    {
        if (art == null || sprite == null) return;
        art.sprite = sprite;
        art.type = Image.Type.Sliced;
        RectTransform rt = (RectTransform)art.transform;
        RectTransform parent = rt.parent as RectTransform;
        float h = parent == null ? 1f : parent.rect.height > 1f ? parent.rect.height : Mathf.Max(1f, parent.sizeDelta.y);
        float bodyPx = Mathf.Max(1f, sprite.rect.height - inset.y - inset.w);
        float m = bodyPx / h;
        art.pixelsPerUnitMultiplier = m;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-inset.x / m, -inset.y / m);
        rt.offsetMax = new Vector2(inset.z / m, inset.w / m);
    }
}
