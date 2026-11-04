using UnityEngine;
using UnityEngine.UI;

// 한글이 깨지지 않도록 게임 시작 시 Canvas 아래 모든 Text의 폰트를 바꿉니다.
// font 칸에 한글 .ttf를 넣으면 그 폰트를, 비워두면 PC에 설치된 맑은 고딕 등을 사용합니다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-500)]
public class GameUIFont : MonoBehaviour
{
    [SerializeField] private Font font;

    private Font runtimeFont;

    private void Awake()
    {
        Font target = font;
        if (target == null)
        {
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);
            target = runtimeFont;
        }

        foreach (Text text in GetComponentsInChildren<Text>(true))
            text.font = target;
    }

    private void OnDestroy()
    {
        if (runtimeFont != null)
            Destroy(runtimeFont);
    }
}
