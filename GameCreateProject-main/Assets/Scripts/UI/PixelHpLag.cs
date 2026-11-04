using UnityEngine;
using UnityEngine.UI;

// 체력이 줄면 연한 채움이 잠깐 남았다가 따라 줄어듦 (얼마나 맞았는지 보이게)
[DisallowMultipleComponent]
public class PixelHpLag : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private Image lag;
    [SerializeField, Min(0f)] private float holdSeconds = 0.35f;
    [SerializeField, Min(0.05f)] private float shrinkPerSecond = 0.9f;
    private float last = -1f, hold;

    public void Set(Image fillImage, Image lagImage) { fill = fillImage; lag = lagImage; }

    private void LateUpdate()
    {
        if (fill == null || lag == null) return;
        float f = fill.fillAmount;
        if (last < 0f) { lag.fillAmount = f; last = f; return; }
        if (f < last - 0.0001f) hold = holdSeconds;
        last = f;
        if (lag.fillAmount <= f) { lag.fillAmount = f; return; } // 회복은 바로
        if (hold > 0f) { hold -= Time.unscaledDeltaTime; return; }
        lag.fillAmount = Mathf.MoveTowards(lag.fillAmount, f, shrinkPerSecond * Time.unscaledDeltaTime);
    }
}
