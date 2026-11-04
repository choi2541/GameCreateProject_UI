using UnityEngine;

// 스킬 효과음 재생 (2D, 화면 어디서든 같은 크기)
// 효과음 볼륨(설정 > EFFECTS)은 AudioVolumeController가 자동으로 곱해 줍니다.
public static class SkillSound
{
    private static AudioSource source;
    private static float lastPlayTime;
    private static AudioClip lastClip;

    // clip이 비어 있으면 아무것도 안 함. 같은 소리가 너무 겹치지 않게 minGap초 안의 같은 소리는 생략
    public static void Play(AudioClip clip, float volume = 1f, float minGap = 0.03f)
    {
        if (clip == null || volume <= 0f) return;
        if (clip == lastClip && Time.unscaledTime - lastPlayTime < minGap) return;
        lastClip = clip;
        lastPlayTime = Time.unscaledTime;

        if (source == null)
        {
            GameObject obj = new GameObject("[SkillSound]");
            Object.DontDestroyOnLoad(obj);
            source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false; // loop가 아니라서 효과음(SFX) 볼륨이 적용됨
        }
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
