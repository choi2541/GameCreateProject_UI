using UnityEngine;
using UnityEngine.UI;

// 마우스를 올리면 그림을 바꿔 줌 (증강 카드: 기본 칸 → 강조 칸)
[DisallowMultipleComponent]
public class PixelHoverSprite : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;

    public void Set(Image img, Sprite normal, Sprite hover) { image = img; normalSprite = normal; hoverSprite = hover; if (image != null) image.sprite = normal; }
    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (image != null && hoverSprite != null) image.sprite = hoverSprite; }
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { if (image != null) image.sprite = normalSprite; }
    private void OnDisable() { if (image != null) image.sprite = normalSprite; }
}
