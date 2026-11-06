using UnityEngine;

// 적 겉모습: 원래 그림(동그라미)은 숨기고, 자식에 슬라임/버섯 그림을 그려서 통통 튀거나 뒤뚱거리게 함.
// 충돌 크기·체력바 위치는 원래 그대로 (원래 그림 크기에 맞춰 그림 크기를 정함).
// EnemyLevelScaling이 실행 중에 붙임.
[DisallowMultipleComponent]
public class EnemySkin : MonoBehaviour
{
    public enum Motion { Bounce, Waddle, Frames }

    private Sprite[] frames;
    private float fps;

    // 그림 자체가 움직이는 경우 (8칸 등): 칸을 차례로 보여 주기만 함
    public void InitAnimated(Sprite[] sprites, float framesPerSecond, float sizeMultiplier)
    {
        frames = sprites; fps = framesPerSecond;
        Init(sprites[0], Motion.Frames, sizeMultiplier);
    }

    private SpriteRenderer original, skin;
    private Transform skinTransform;
    private Rigidbody2D body;
    private Motion motion;
    private float phase, speed;
    private Vector3 lastPosition;
    private bool faceLeft;

    public void Init(Sprite sprite, Motion how, float sizeMultiplier)
    {
        original = GetComponent<SpriteRenderer>();
        if (original == null) original = GetComponentInChildren<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        motion = how;
        phase = Random.value * 10f;
        speed = Random.Range(0.85f, 1.15f);
        lastPosition = transform.position;

        if (skin == null)
        {
            var go = new GameObject("Skin");
            skinTransform = go.transform;
            skinTransform.SetParent(transform, false);
            skin = go.AddComponent<SpriteRenderer>();
        }
        skin.sprite = sprite;

        float targetHeight = 1f;
        if (original != null)
        {
            targetHeight = original.bounds.size.y;
            skin.sortingLayerID = original.sortingLayerID;
            skin.sortingOrder = original.sortingOrder;
            skin.sharedMaterial = original.sharedMaterial;
            baseColor = original.color;
            original.enabled = false;
        }
        float spriteHeight = sprite.bounds.size.y * Mathf.Max(0.0001f, transform.lossyScale.y);
        float s = targetHeight * sizeMultiplier / Mathf.Max(0.0001f, spriteHeight);
        skinTransform.localScale = new Vector3(s, s, 1f);
        baseScale = skinTransform.localScale;
    }

    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;

    private void LateUpdate()
    {
        if (skin == null) return;
        // 원래 몹 색(빨강)은 쓰지 않고, 기절·피격처럼 잠깐 바뀐 색만 그대로 보이게
        if (original != null) skin.color = original.color == baseColor ? Color.white : original.color;

        Vector3 pos = transform.position;
        float vx = body != null ? body.linearVelocity.x : (pos.x - lastPosition.x) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = pos;
        if (Mathf.Abs(vx) > 0.05f) faceLeft = vx < 0f;

        float t = (Time.time + phase) * speed;
        float sx = faceLeft ? -1f : 1f;
        if (motion == Motion.Frames)
        {
            skin.sprite = frames[(int)(t * fps) % frames.Length];
            skinTransform.localScale = new Vector3(baseScale.x * sx, baseScale.y, 1f);
            skinTransform.localPosition = Vector3.zero;
            skinTransform.localRotation = Quaternion.identity;
        }
        else if (motion == Motion.Bounce)
        {
            float k = Mathf.Sin(t * 8f);
            skinTransform.localScale = new Vector3(baseScale.x * (1f - 0.07f * k) * sx, baseScale.y * (1f + 0.09f * k), 1f);
            skinTransform.localPosition = Vector3.zero;
            skinTransform.localRotation = Quaternion.identity;
        }
        else
        {
            skinTransform.localScale = new Vector3(baseScale.x * sx, baseScale.y, 1f);
            skinTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 9f) * 7f);
            skinTransform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 9f)) * 0.04f, 0f);
        }
    }

    private void OnDisable()
    {
        if (original != null && skin == null) original.enabled = true;
    }
}
