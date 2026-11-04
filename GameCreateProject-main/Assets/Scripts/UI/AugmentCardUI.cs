using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 증강 카드 한 장. 클릭하면 선택, 마우스를 올리면 살짝 커집니다.
[DisallowMultipleComponent]
public class AugmentCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private Text iconLetter;
    [SerializeField] private Text rarityText;
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text stackText;

    public event Action Clicked;

    public static Color ColorOf(AugmentSystem.Rarity rarity)
    {
        return rarity == AugmentSystem.Rarity.Epic ? EpicColor
            : rarity == AugmentSystem.Rarity.Rare ? RareColor : CommonColor;
    }

    // 등급 색: 흰색(Common) / 파란색(Rare) / 보라색(Epic)
    public static readonly Color CommonColor = new Color(0.92f, 0.93f, 0.96f);
    public static readonly Color RareColor = new Color(0.35f, 0.62f, 1f);
    public static readonly Color EpicColor = new Color(0.78f, 0.42f, 1f);

    private bool hovered;

    public void Show(AugmentSystem.Augment augment, int currentStacks)
    {
        Color color = ColorOf(augment.rarity);

        if (frame != null) frame.color = color;
        if (icon != null)
        {
            bool hasIcon = augment.icon != null;
            if (hasIcon) icon.sprite = augment.icon;
            icon.color = hasIcon ? Color.white : new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.6f);
            icon.preserveAspect = hasIcon;
            if (iconLetter != null)
            {
                iconLetter.gameObject.SetActive(!hasIcon);
                iconLetter.text = augment.displayName.Length > 0 ? augment.displayName.Substring(0, 1) : "?";
            }
        }
        if (rarityText != null)
        {
            rarityText.text = augment.rarity == AugmentSystem.Rarity.Epic ? "EPIC"
                : augment.rarity == AugmentSystem.Rarity.Rare ? "RARE" : "COMMON";
            rarityText.color = color;
        }
        if (nameText != null) nameText.text = augment.displayName;
        if (descriptionText != null) descriptionText.text = augment.description;
        if (stackText != null)
            stackText.text = augment.maxStacks >= 99 ? (currentStacks > 0 ? $"보유 {currentStacks}" : "")
                : $"{currentStacks} / {augment.maxStacks}";

        hovered = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        // 일시정지 중에도 부드럽게 (unscaled)
        float target = hovered ? 1.06f : 1f;
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 0.8f);
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

    private void OnDisable()
    {
        hovered = false;
        transform.localScale = Vector3.one;
    }
}
