using UnityEngine;

// 프레임 그림을 계속 반복 재생 (예: 화살비 바닥 표시). SkillFx.Loop 이 붙임.
public class SkillFxLoop : MonoBehaviour
{
    private string id;
    private float fps;
    private SpriteRenderer sr;
    private float time;

    public void Set(string frameId, float framesPerSecond, SpriteRenderer target, float startTime = 0f)
    {
        id = frameId; fps = framesPerSecond; sr = target; time = startTime;
        sr.sprite = SkillFx.LoopFrame(id, 0f, fps);
    }

    private void Update()
    {
        if (sr == null) return;
        time += Time.deltaTime; // 일시정지하면 같이 멈춤
        Sprite s = SkillFx.LoopFrame(id, time, fps);
        if (s != null) sr.sprite = s;
    }
}
