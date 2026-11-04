using UnityEngine;
using UnityEngine.UI;

// 스킬 칸 고리: 못 쓰는 칸은 회색 고리 (금빛 고리는 4차 칸 전용)
[DisallowMultipleComponent]
public class PixelSkillRing : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image ring;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Color readyColor;

    public void Set(Image bg, Image ringImage, Sprite normal, Sprite locked, Color ready)
    {
        background = bg; ring = ringImage; normalSprite = normal; lockedSprite = locked; readyColor = ready;
    }

    private void LateUpdate()
    {
        if (ring == null) return;
        bool usable = background == null || Same(background.color, readyColor);
        Sprite want = usable || lockedSprite == null ? normalSprite : lockedSprite;
        if (ring.sprite != want) ring.sprite = want;
    }

    private static bool Same(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
}
