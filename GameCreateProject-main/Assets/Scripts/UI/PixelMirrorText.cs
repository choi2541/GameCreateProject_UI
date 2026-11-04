using UnityEngine;
using UnityEngine.UI;

// 원래 글자(코드가 바꾸는 글자)를 판 위 글자로 따라 씀 (STAT +N [TAB])
[DisallowMultipleComponent]
public class PixelMirrorText : MonoBehaviour
{
    [SerializeField] private Text source;
    [SerializeField] private Text target;
    public void Set(Text from, Text to) { source = from; target = to; }
    private void LateUpdate()
    {
        if (source != null && target != null && target.text != source.text) target.text = source.text;
    }
}
