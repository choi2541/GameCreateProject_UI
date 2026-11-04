using System.Collections.Generic;
using UnityEngine;

// 배경음 / 효과음 볼륨 분리. 팀의 GameAudio 코드를 고치지 않고, 씬의 모든 AudioSource에 배율을 곱합니다.
//   반복 재생(loop) 소리  → 배경음(BGM)
//   그 외                 → 효과음(SFX)
// 전체 볼륨은 AudioListener.volume (SettingsUI).
[DisallowMultipleComponent]
public class AudioVolumeController : MonoBehaviour
{
    public const string BgmKey = "BgmVolume";
    public const string SfxKey = "SfxVolume";

    public static float Bgm = 1f;
    public static float Sfx = 1f;

    // 원래 볼륨 기억 (배율을 여러 번 곱하지 않도록)
    private readonly Dictionary<AudioSource, float> original = new Dictionary<AudioSource, float>();
    private readonly List<AudioSource> stale = new List<AudioSource>();
    private float nextScan;

    public void RegisterSource(AudioSource source)
    {
        if (source == null) return;
        if (!original.ContainsKey(source)) original[source] = source.volume;
        source.volume = original[source] * (source.loop ? Bgm : Sfx);
    }

    private void Awake()
    {
        Bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
    }

    public static void SetBgm(float value)
    {
        Bgm = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmKey, Bgm);
    }

    public static void SetSfx(float value)
    {
        Sfx = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, Sfx);
    }

    private void Update()
    {
        // 새로 생긴 AudioSource를 찾기 위해 가끔만 전체 검색
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                RegisterSource(source);
            }
        }

        stale.Clear();
        foreach (KeyValuePair<AudioSource, float> pair in original)
        {
            AudioSource source = pair.Key;
            if (source == null)
            {
                stale.Add(source);
                continue;
            }
            source.volume = pair.Value * (source.loop ? Bgm : Sfx);
        }
        foreach (AudioSource s in stale)
            original.Remove(s);
    }

    private void OnDestroy()
    {
        // 원래 볼륨으로 되돌림
        foreach (KeyValuePair<AudioSource, float> pair in original)
        {
            if (pair.Key != null)
                pair.Key.volume = pair.Value;
        }
    }
}
