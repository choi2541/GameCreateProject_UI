using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Tab 캐릭터 창 아래의 작은 "보유 증강" 줄.
// 아이콘(등급 색 원 + 첫 글자 또는 증강 이미지) + 개수, 마우스를 올리면 이름/설명.
[DisallowMultipleComponent]
public class OwnedAugmentsUI : MonoBehaviour
{
    [SerializeField] private Transform iconContainer;
    [SerializeField] private Text emptyText;
    [SerializeField] private Sprite circleSprite;
    [SerializeField, Min(16f)] private float iconSize = 52f;

    private readonly List<GameObject> icons = new List<GameObject>();
    private AugmentSystem system;

    private void OnEnable()
    {
        system = AugmentSystem.Instance;
        if (system != null)
            system.OwnedChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (system != null)
            system.OwnedChanged -= Refresh;
    }

    private void Refresh()
    {
        foreach (GameObject icon in icons)
            Destroy(icon);
        icons.Clear();

        var owned = system != null ? system.Owned() : new List<(AugmentSystem.Augment, int)>();
        if (emptyText != null)
            emptyText.gameObject.SetActive(owned.Count == 0);

        foreach (var entry in owned)
            icons.Add(CreateIcon(entry.Item1, entry.Item2));
    }

    private GameObject CreateIcon(AugmentSystem.Augment augment, int count)
    {
        Color color = AugmentCardUI.ColorOf(augment.rarity);
        Font font = emptyText != null ? emptyText.font : null;

        GameObject root = new GameObject(augment.id, typeof(RectTransform));
        root.transform.SetParent(iconContainer, false);
        ((RectTransform)root.transform).sizeDelta = new Vector2(iconSize, iconSize);

        // 등급 색 테두리
        Image frame = root.AddComponent<Image>();
        frame.sprite = circleSprite;
        frame.color = color;
        frame.raycastTarget = true; // 툴팁용

        GameObject inner = new GameObject("Icon", typeof(RectTransform));
        inner.transform.SetParent(root.transform, false);
        RectTransform innerRect = (RectTransform)inner.transform;
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(4f, 4f);
        innerRect.offsetMax = new Vector2(-4f, -4f);
        Image icon = inner.AddComponent<Image>();
        icon.raycastTarget = false;
        if (augment.icon != null)
        {
            icon.sprite = augment.icon;
            icon.preserveAspect = true;
        }
        else
        {
            icon.sprite = circleSprite;
            icon.color = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.5f);
            AddText(inner.transform, font, augment.displayName.Length > 0 ? augment.displayName.Substring(0, 1) : "?",
                Mathf.RoundToInt(iconSize * 0.45f), Color.white, TextAnchor.MiddleCenter, Vector2.zero);
        }

        if (count > 1)
            AddText(root.transform, font, "x" + count, Mathf.RoundToInt(iconSize * 0.32f), new Color(1f, 0.85f, 0.3f),
                TextAnchor.LowerRight, new Vector2(6f, -6f));

        UITooltipTrigger tooltip = root.AddComponent<UITooltipTrigger>();
        string stackInfo = augment.maxStacks >= 99 ? $"보유 {count}" : $"{count} / {augment.maxStacks}";
        tooltip.SetContent(augment.displayName, $"{augment.description}\n{stackInfo}");
        return root;
    }

    private static void AddText(Transform parent, Font font, string value, int size, Color color, TextAnchor align, Vector2 offset)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)obj.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset;
        rect.offsetMax = offset;
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        obj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
    }
}
