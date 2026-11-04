using System.Collections;
using UnityEngine;

// 그림자 분신 연출: 플레이어 그림을 복사해서 검보라색으로 칠함 (그림이 없으면 둥근 그림자)
public class ShadowClone : MonoBehaviour
{
    private SpriteRenderer sr;
    private float fade = -1f, fadeTime;

    public static GameObject Create(GameObject player, Vector2 position, float alpha = 0.85f)
    {
        GameObject obj = new GameObject("ShadowClone");
        obj.transform.position = position;
        ShadowClone clone = obj.AddComponent<ShadowClone>();
        clone.sr = obj.AddComponent<SpriteRenderer>();
        SpriteRenderer source = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
        if (source != null && source.sprite != null)
        {
            clone.sr.sprite = source.sprite;
            clone.sr.flipX = source.flipX;
            obj.transform.localScale = source.transform.lossyScale;
        }
        else
        {
            clone.sr.sprite = SkillUtil.CircleSprite();
            obj.transform.localScale = Vector3.one * 0.7f;
        }
        clone.sr.color = new Color(0.25f, 0.05f, 0.4f, alpha);
        clone.sr.sortingOrder = 40;
        SkillFx.Burst(position, 0.4f, 1.4f, new Color(0.3f, 0.08f, 0.45f, 0.7f), 0.3f);
        return obj;
    }

    public static void FadeOut(GameObject obj, float seconds)
    {
        ShadowClone clone = obj != null ? obj.GetComponent<ShadowClone>() : null;
        if (clone == null) { if (obj != null) Destroy(obj); return; }
        clone.fade = 0f; clone.fadeTime = Mathf.Max(0.01f, seconds);
    }

    private void Update()
    {
        if (fade < 0f) return;
        fade += Time.deltaTime;
        Color c = sr.color; c.a = Mathf.Lerp(0.85f, 0f, fade / fadeTime); sr.color = c;
        if (fade >= fadeTime) Destroy(gameObject);
    }
}
