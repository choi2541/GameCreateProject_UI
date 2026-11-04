using UnityEngine;
using UnityEngine.UI;

// 스탯 한 칸:     ( 아이콘 )        ← 마우스를 올리면 설명
//                  5  ▲            ← 값, ▲ = +1
// 숫자를 누르면:  [+5]  5  [+10]    ← 양옆 버튼으로 한 번에 올리기 (숫자를 다시 누르면 닫힘)
[DisallowMultipleComponent]
public class StatRowUI : MonoBehaviour
{
    [Tooltip("아이콘 뒤 원형 테두리. 스탯 이미지를 넣으면 자동으로 숨김")]
    [SerializeField] private Image iconFrame;
    [SerializeField] private Image icon;
    [SerializeField] private Text iconLetter;
    [SerializeField] private UITooltipTrigger tooltip;
    [SerializeField] private Text nameText;
    [SerializeField] private Text valueText;
    [SerializeField] private Button valueButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button plus5Button;
    [SerializeField] private Button plus10Button;

    // 한 번에 하나의 칸만 펼쳐집니다.
    private static StatRowUI expanded;

    private PlayerStats stats;
    private int index;

    public void Bind(PlayerStats target, int statIndex)
    {
        stats = target;
        index = statIndex;
        PlayerStats.StatDefinition def = stats.GetDefinition(index);

        if (icon != null)
        {
            bool hasIcon = def.icon != null;
            icon.sprite = hasIcon ? def.icon : icon.sprite;
            icon.color = hasIcon ? Color.white : def.placeholderColor;
            icon.preserveAspect = hasIcon;
            if (hasIcon)
                icon.type = Image.Type.Simple;
            if (iconFrame != null)
                iconFrame.enabled = !hasIcon; // 룬 같은 모양 이미지는 테두리 없이
            if (iconLetter != null)
            {
                iconLetter.text = def.id;
                iconLetter.gameObject.SetActive(!hasIcon);
            }
        }

        if (tooltip != null)
            tooltip.SetContent($"{def.displayName} ({def.id})", def.description);

        if (nameText != null)
            nameText.text = def.id;

        Hook(plusButton, () => stats.AddPoints(index, 1));
        Hook(plus5Button, () => stats.AddPoints(index, 5));
        Hook(plus10Button, () => stats.AddPoints(index, 10));
        Hook(valueButton, ToggleQuickAdd);

        SetExpanded(false);
        Refresh();
    }

    private static void Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ToggleQuickAdd()
    {
        bool open = expanded != this;
        if (expanded != null && expanded != this)
            expanded.SetExpanded(false);
        SetExpanded(open);
    }

    private void SetExpanded(bool open)
    {
        if (open) expanded = this;
        else if (expanded == this) expanded = null;

        if (plus5Button != null) plus5Button.gameObject.SetActive(open);
        if (plus10Button != null) plus10Button.gameObject.SetActive(open);
        if (plusButton != null) plusButton.gameObject.SetActive(!open);
        Refresh();
    }

    public void Refresh()
    {
        if (stats == null)
            return;
        if (valueText != null)
            valueText.text = stats.GetValue(index).ToString();
        if (plusButton != null)
            plusButton.interactable = stats.CanAddPoints(index, 1);
        if (plus5Button != null)
            plus5Button.interactable = stats.CanAddPoints(index, 5);
        if (plus10Button != null)
            plus10Button.interactable = stats.CanAddPoints(index, 10);
    }

    private void OnDisable()
    {
        // 창이 닫히면 펼친 상태도 닫습니다.
        if (expanded == this)
            SetExpanded(false);
    }
}
