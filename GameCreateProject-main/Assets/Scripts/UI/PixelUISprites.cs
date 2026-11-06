using UnityEngine;
using UnityEngine.UI;

// 게임 중에 새로 만들어지는 UI가 쓰는 그림 (4차 칸 고리, 스탯 룬). 게임 씬 HUD에 붙어 있음 (Pixel UI - Apply 메뉴가 채움)
[DisallowMultipleComponent]
public class PixelUISprites : MonoBehaviour
{
    [Header("스킬 칸 고리")]
    public Sprite ringNormal;
    public Sprite ringReady;   // 금빛 = 4차 칸
    public Sprite ringLocked;
    [Tooltip("고리 그림 안에서 고리 바깥 지름 비율 / 가운데 구멍 지름 비율")]
    public float ringOuterRatio = 552f / 598f;
    public float ringHoleRatio = 431f / 598f;

    [Header("스탯 룬 (STR, VIT, SPD, EXP, SKL, CDR 순서)")]
    public Sprite[] runes = new Sprite[6];
    private static readonly string[] RuneIds = { "STR", "VIT", "SPD", "EXP", "SKL", "CDR" };

    [Header("전직 창 (2·3·4차) — 테마(나무/돌)에 맞춰 Pixel UI - Apply가 채움")]
    [Tooltip("카드 테두리: 0 = 2차, 1 = 3차 그림자, 2 = 3차 폭풍, 3 = 4차")]
    public Sprite[] jobFrames = new Sprite[4];
    public Sprite buttonNormal, buttonGreen, buttonDisabled;
    [Tooltip("버튼 그림에서 몸통 바깥 장식 크기 (왼, 아래, 오른, 위)")]
    public Vector4 buttonNormalInset, buttonGreenInset, buttonDisabledInset;

    public static Sprite JobFrame(int index)
    {
        PixelUISprites s = Instance;
        return s != null && s.jobFrames != null && index >= 0 && index < s.jobFrames.Length ? s.jobFrames[index] : null;
    }

    private static PixelUISprites cached;
    public static PixelUISprites Instance
    {
        get
        {
            if (cached == null) cached = FindAnyObjectByType<PixelUISprites>(FindObjectsInactive.Include);
            return cached;
        }
    }

    public static Sprite Rune(string statId)
    {
        PixelUISprites s = Instance;
        if (s == null || s.runes == null || string.IsNullOrEmpty(statId)) return null;
        int i = System.Array.IndexOf(RuneIds, statId.ToUpperInvariant());
        return i >= 0 && i < s.runes.Length ? s.runes[i] : null;
    }
}
