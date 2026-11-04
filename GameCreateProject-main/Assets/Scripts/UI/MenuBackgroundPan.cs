using UnityEngine;

// 메인 화면 배경을 양옆으로 천천히 왔다갔다 움직입니다.
// 화면보다 넓게 잘린 그림(AspectRatioFitter: Envelope Parent)의 남는 부분 안에서만 움직이므로 빈 곳이 보이지 않습니다.
[RequireComponent(typeof(RectTransform))]
public class MenuBackgroundPan : MonoBehaviour
{
    [Tooltip("한쪽 끝 → 반대쪽 끝 → 원래 자리까지 걸리는 시간(초). 클수록 느림")]
    [SerializeField, Min(2f)] private float cycleSeconds = 40f;
    [Tooltip("남는 폭 중 얼마나 쓸지 (1 = 끝에서 끝까지)")]
    [SerializeField, Range(0f, 1f)] private float range = 0.9f;

    private RectTransform rect;
    private RectTransform parent;

    private void Awake()
    {
        rect = (RectTransform)transform;
        parent = rect.parent as RectTransform;
    }

    private void LateUpdate()
    {
        if (parent == null)
            return;

        // 그림이 화면보다 넓은 만큼의 절반이 한쪽으로 움직일 수 있는 거리
        float overflow = Mathf.Max(0f, (rect.rect.width - parent.rect.width) * 0.5f) * range;
        // 끝에서 천천히 멈췄다가 돌아가도록 코사인 곡선 사용 (일시정지와 무관하게 실제 시간 기준)
        float phase = Time.unscaledTime / cycleSeconds * Mathf.PI * 2f;
        rect.anchoredPosition = new Vector2(Mathf.Sin(phase) * overflow, rect.anchoredPosition.y);
    }
}
