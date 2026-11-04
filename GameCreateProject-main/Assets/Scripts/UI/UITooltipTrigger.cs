using UnityEngine;
using UnityEngine.EventSystems;

// 이 컴포넌트가 붙은 UI(Image 등, Raycast Target 켜짐)에 마우스를 올리면 툴팁이 뜹니다.
[DisallowMultipleComponent]
public class UITooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string title;
    [SerializeField, TextArea(2, 5)] private string body;

    public void SetContent(string newTitle, string newBody)
    {
        title = newTitle;
        body = newBody;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        UITooltip.Show(this, title, body);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UITooltip.Hide(this);
    }

    private void OnDisable()
    {
        UITooltip.Hide(this);
    }
}
