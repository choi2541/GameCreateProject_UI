using UnityEngine;
using UnityEngine.UI;

// 캐릭터 창 왼쪽: 캐릭터 모습, 직업, 레벨, 체력, 경험치, 생존 시간, 처치 수
[DisallowMultipleComponent]
public class CharacterWindowUI : MonoBehaviour
{
    // 다른 시스템(예: 2차 전직)이 직업 이름을 바꿔 표시하고 싶을 때 설정합니다. null이면 기본 이름.
    public static string JobNameOverride;

    [Tooltip("캐릭터 그림. 비워두면 플레이어의 현재 스프라이트를 가져옵니다.")]
    [SerializeField] private Sprite portraitOverride;
    [SerializeField] private Image portrait;
    [SerializeField] private Text jobText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text expText;
    [SerializeField] private Text timeText;
    [SerializeField] private Text killText;

    [Tooltip("Tab 창 오른쪽에 스탯 창(StatWindow)을 같이 보여줄지")]
    [SerializeField] private bool showStatWindow = true;

    private void Awake()
    {
        // 씬에서 스탯 창이 꺼져 있어도(병합 때 꺼둔 상태) 실행하면 자동으로 켜고 좌우로 나란히 놓음
        if (!showStatWindow || transform.parent == null) return;
        Transform stat = transform.parent.Find("StatWindow");
        if (stat == null) return;
        stat.gameObject.SetActive(true);
        ((RectTransform)transform).anchoredPosition = new Vector2(-380f, 20f);
        ((RectTransform)stat).anchoredPosition = new Vector2(380f, 20f);
    }

    private void OnEnable()
    {
        UpdatePortrait();
        Refresh();
    }

    private void Update()
    {
        UpdatePortrait(); // 고정 그림이라 움직이지 않음 (전직하면 바뀐 모습으로만 바뀜)
        Refresh();
    }

    private void UpdatePortrait()
    {
        if (portrait == null)
            return;

        Sprite sprite = portraitOverride;
        if (sprite == null)
        {
            GameSession session = GameSession.Instance;
            PlayerHealth player = session != null ? session.Player : null;
            PlayerSpriteAnimator anim = player != null ? player.GetComponent<PlayerSpriteAnimator>() : null;
            if (anim != null) sprite = anim.PortraitSprite; // 서 있는 모습 고정
            if (sprite == null && player != null)
            {
                foreach (SpriteRenderer sr in player.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.sprite != null && sr.enabled)
                    {
                        sprite = sr.sprite;
                        break;
                    }
                }
            }
        }

        if (portrait.sprite == sprite && portrait.enabled == (sprite != null)) return;
        portrait.sprite = sprite;
        portrait.enabled = sprite != null;
        portrait.preserveAspect = true;
    }

    private void Refresh()
    {
        GameSession session = GameSession.Instance;
        PlayerProgression progression = PlayerProgression.Instance;
        PlayerHealth player = session != null ? session.Player : null;

        string job = !string.IsNullOrEmpty(JobNameOverride) ? JobNameOverride
            : progression != null ? JobName(progression.CurrentJob) : "-";
        Set(jobText, job);
        Set(levelText, progression != null ? $"Lv. {progression.Level}" : "-");
        Set(hpText, player != null ? $"{player.CurrentHealth} / {player.MaxHealth}" : "-");
        Set(expText, progression != null ? $"{progression.Experience} / {progression.RequiredExperience}" : "-");
        Set(timeText, session != null ? GameSession.FormatTime(session.SurvivalTime) : "-");
        Set(killText, session != null ? session.Kills.ToString() : "-");
    }

    private static void Set(Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    public static string JobName(PlayerJob job)
    {
        switch (job)
        {
            case PlayerJob.Warrior: return "WARRIOR";
            case PlayerJob.Archer: return "ARCHER";
            default: return "NOVICE";
        }
    }
}
