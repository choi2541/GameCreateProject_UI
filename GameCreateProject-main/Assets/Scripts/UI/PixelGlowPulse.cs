using UnityEngine;
using UnityEngine.UI;

// 빛나는 그림을 은은하게 깜빡이기 (CHOOSE JOB)
[DisallowMultipleComponent]
public class PixelGlowPulse : MonoBehaviour
{
    [SerializeField] private Image glow;
    [SerializeField, Min(0.1f)] private float speed = 3f;
    public void Set(Image image) { glow = image; }
    private void LateUpdate()
    {
        if (glow == null) return;
        float a = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed));
        Color c = glow.color; c.a = a; glow.color = c;
    }
}
