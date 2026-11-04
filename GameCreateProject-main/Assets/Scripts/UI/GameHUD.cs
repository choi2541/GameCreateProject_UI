using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 게임 플레이 화면 HUD와 창 전환을 관리합니다.
//  - ESC / 일시정지 버튼 : 일시정지 메뉴 (계속 / 설정 / 다시하기 / 메인 메뉴 / 게임 종료)
//  - Tab 누르고 있기     : 캐릭터 창 (왼쪽 캐릭터 정보, 오른쪽 스탯 찍기). 떼면 게임으로 복귀
[DisallowMultipleComponent]
public class GameHUD : MonoBehaviour
{
    private enum View { None, Pause, Settings, Character }

    [Header("체력")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Text hpText;

    [Header("레벨 / 경험치")]
    [SerializeField] private Image expFill;
    [SerializeField] private Text levelText;
    [SerializeField] private Text statHintText;
    [Tooltip("레벨업할 때 잠깐 나타나는 표시")]
    [SerializeField] private Text levelUpText;
    [Tooltip("남은 스탯 포인트가 이 값 이상이면 STAT 알림이 흔들립니다.")]
    [SerializeField, Min(1)] private int statShakeThreshold = 5;
    [SerializeField] private Button jobButton;

    [Header("생존 시간 / 처치 수")]
    [SerializeField] private Text timeText;
    [SerializeField] private Text killText;

    [Header("상단 버튼")]
    [SerializeField] private Button pauseButton;

    [Header("일시정지 메뉴")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("설정")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsBackButton;

    [Header("캐릭터 창 (Tab)")]
    [SerializeField] private GameObject characterPanel;

    private GameSession session;
    private PlayerProgression progression;
    private View current = View.None;
    private bool characterHeldByTab;

    private int shownHp = -1;
    private int shownMaxHp = -1;
    private int shownLevel = -1;
    private int shownSecond = -1;
    private int shownKills = -1;
    private int shownPoints = -1;

    private const float LevelUpDuration = 1.6f;
    private float levelUpTimer = -1f;
    private Vector2 levelUpBasePos;
    private Vector2 statHintBasePos;

    private void Start()
    {
        PlayerStats.Ensure(); // 처음부터 스탯 포인트 표시(STAT +N)가 되도록
        KeyBindingApplier.Ensure(gameObject); // 설정에서 바꾼 키를 스킬에 적용
        if (levelUpText != null)
        {
            levelUpBasePos = levelUpText.rectTransform.anchoredPosition;
            levelUpText.gameObject.SetActive(false);
        }
        if (statHintText != null)
            statHintBasePos = statHintText.rectTransform.anchoredPosition;

        session = GameSession.Instance;
        progression = PlayerProgression.Instance;
        if (session == null)
            Debug.LogWarning("[GameHUD] 씬에 GameSession이 없습니다. 생존 시간/처치 수/일시정지가 작동하지 않습니다.", this);

        AddClick(pauseButton, () => Open(View.Pause));
        AddClick(resumeButton, CloseAll);
        AddClick(settingsButton, () => Show(View.Settings));
        AddClick(settingsBackButton, () => Show(View.Pause));
        AddClick(retryButton, () => { if (session != null) session.Restart(); });
        AddClick(mainMenuButton, () => { if (session != null) session.LoadMainMenu(); });
        AddClick(quitButton, () => { if (session != null) session.QuitGame(); });
        AddClick(jobButton, () => { if (progression != null) progression.OpenJobSelection(); });

        if (mainMenuButton != null)
            mainMenuButton.gameObject.SetActive(session != null && session.HasMainMenu);

        Show(View.None);
    }

    private void Update()
    {
        if (session == null) session = GameSession.Instance;
        if (progression == null) progression = PlayerProgression.Instance;

        bool gameOver = session != null && session.IsGameOver;
        if (gameOver)
        {
            if (current != View.None) Show(View.None);
        }
        else
        {
            HandleKeys();
        }

        RefreshHealth();
        RefreshLevel();
        RefreshStats();

        bool canPause = session != null && session.CanPause;
        if (pauseButton != null) pauseButton.interactable = canPause;

        if (jobButton != null)
        {
            bool show = progression != null && progression.CanChooseJob
                && !progression.IsChoosingJob && !gameOver && current == View.None;
            if (jobButton.gameObject.activeSelf != show)
                jobButton.gameObject.SetActive(show);
        }
    }

    private void HandleKeys()
    {
        if (SettingsExtras.IsCapturing)
            return; // 키 설정에서 키를 기다리는 중
        if (UIKeyInput.EscapeDown)
        {
            switch (current)
            {
                case View.None: Open(View.Pause); break;
                case View.Settings: Show(View.Pause); break;
                default: CloseAll(); break;
            }
            return;
        }

        bool toggleMode = KeyBindings.TabToggle;
        if (current == View.None && UIKeyInput.TabDown)
        {
            if (Open(View.Character))
            {
                characterHeldByTab = !toggleMode;
                UpdateCharacterHint(toggleMode);
            }
            return;
        }

        // 눌러서 열기 방식: 한 번 더 누르면 닫힘
        if (current == View.Character && toggleMode && UIKeyInput.TabDown)
        {
            CloseAll();
            return;
        }

        // 누르고 있기 방식: 떼면 게임으로 돌아갑니다.
        if (current == View.Character && characterHeldByTab && !UIKeyInput.TabHeld)
            CloseAll();
    }

    // 게임을 멈추고 창을 엽니다. 이미 멈출 수 없는 상태(전직 선택 중 등)면 false.
    private bool Open(View target)
    {
        if (current != View.None)
        {
            Show(target);
            return true;
        }
        if (session == null || !session.Pause())
            return false;
        Show(target);
        return true;
    }

    public void CloseAll()
    {
        Show(View.None);
        if (session != null)
            session.Resume();
    }

    private void Show(View target)
    {
        current = target;
        if (target != View.Character)
            characterHeldByTab = false;

        UITooltip.Hide();
        SetActive(pausePanel, target == View.Pause);
        SetActive(settingsPanel, target == View.Settings);
        SetActive(characterPanel, target == View.Character);
    }

    // 캐릭터 창 아래 안내 문구를 현재 키 / 방식에 맞게
    private Text characterHint;
    private string hintRest;

    private void UpdateCharacterHint(bool toggleMode)
    {
        if (characterPanel == null) return;
        if (characterHint == null)
        {
            Transform t = characterPanel.transform.Find("Hint");
            characterHint = t != null ? t.GetComponent<Text>() : null;
            if (characterHint == null) return;
            string text = characterHint.text;
            int dot = text.IndexOf('·');
            hintRest = dot >= 0 ? "   " + text.Substring(dot) : "";
        }
        string key = KeyBindings.Pretty(KeyBindings.Get(GameAction.Character));
        characterHint.text = (toggleMode ? $"Press [{key}] to close" : $"Hold [{key}]") + hintRest;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private void RefreshHealth()
    {
        PlayerHealth player = session != null ? session.Player : null;
        if (player == null)
            return;

        int hp = player.CurrentHealth;
        int max = Mathf.Max(1, player.MaxHealth);
        if (hp == shownHp && max == shownMaxHp)
            return;

        shownHp = hp;
        shownMaxHp = max;
        if (hpFill != null) hpFill.fillAmount = (float)hp / max;
        if (hpText != null) hpText.text = $"{hp} / {max}";
    }

    private void RefreshLevel()
    {
        if (progression != null)
        {
            float required = Mathf.Max(1, progression.RequiredExperience);
            // EXP 스탯으로 모인 소수점 경험치도 같이 (레벨업해도 남은 만큼 바로 보임)
            if (expFill != null)
                expFill.fillAmount = Mathf.Clamp01((progression.Experience + StatEffects.PendingExp) / required);

            if (progression.Level != shownLevel)
            {
                // 첫 표시(-1)가 아니라 실제로 레벨이 오른 경우에만 연출
                if (shownLevel > 0 && progression.Level > shownLevel)
                    PlayLevelUp(progression.Level);
                shownLevel = progression.Level;
                if (levelText != null) levelText.text = $"Lv. {shownLevel}";
            }
        }

        PlayerStats stats = PlayerStats.Instance;
        int points = stats != null ? stats.AvailablePoints : 0;
        if (points != shownPoints)
        {
            shownPoints = points;
            if (statHintText != null)
            {
                statHintText.gameObject.SetActive(points > 0);
                statHintText.text = $"STAT +{points}  [TAB]";
            }
        }
    }

    private void RefreshStats()
    {
        if (session == null)
            return;

        int second = Mathf.FloorToInt(session.SurvivalTime);
        if (second != shownSecond)
        {
            shownSecond = second;
            if (timeText != null) timeText.text = GameSession.FormatTime(second);
        }

        if (session.Kills != shownKills)
        {
            shownKills = session.Kills;
            if (killText != null) killText.text = $"KILLS  {shownKills}";
        }
    }

    // ───────── 연출 (일시정지 중에도 재생되도록 unscaled 시간 사용) ─────────

    private void LateUpdate()
    {
        AnimateLevelUp();
        AnimateStatHint();
    }

    private void PlayLevelUp(int level)
    {
        levelUpTimer = 0f;
        if (levelUpText != null)
        {
            levelUpText.text = $"LEVEL UP!  Lv. {level}";
            levelUpText.gameObject.SetActive(true);
        }
    }

    private void AnimateLevelUp()
    {
        if (levelUpTimer < 0f)
            return;

        levelUpTimer += Time.unscaledDeltaTime;
        float t = levelUpTimer;

        // 레벨 숫자 톡 튀기
        if (levelText != null)
        {
            float punch = t < 0.3f ? 1f + 0.35f * Mathf.Sin(t / 0.3f * Mathf.PI) : 1f;
            levelText.rectTransform.localScale = new Vector3(punch, punch, 1f);
        }

        if (levelUpText != null)
        {
            // 0~0.2초: 커지며 등장 / ~1.1초: 유지하며 살짝 위로 / 마지막 0.5초: 사라짐
            float scale = t < 0.2f ? Mathf.Lerp(0.4f, 1.15f, t / 0.2f)
                : t < 0.32f ? Mathf.Lerp(1.15f, 1f, (t - 0.2f) / 0.12f) : 1f;
            float alpha = t < 1.1f ? 1f : Mathf.Clamp01(1f - (t - 1.1f) / (LevelUpDuration - 1.1f));
            RectTransform rect = levelUpText.rectTransform;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = levelUpBasePos + new Vector2(0f, Mathf.Min(t, 1f) * 14f);
            Color c = levelUpText.color;
            c.a = alpha;
            levelUpText.color = c;
        }

        if (t >= LevelUpDuration)
        {
            levelUpTimer = -1f;
            if (levelUpText != null) levelUpText.gameObject.SetActive(false);
            if (levelText != null) levelText.rectTransform.localScale = Vector3.one;
        }
    }

    private void AnimateStatHint()
    {
        if (statHintText == null || !statHintText.gameObject.activeSelf)
            return;

        RectTransform rect = statHintText.rectTransform;
        bool shake = shownPoints >= statShakeThreshold;
        float cycle = Time.unscaledTime % 1.4f;

        if (shake && cycle < 0.5f)
        {
            // 1.4초마다 0.5초 동안 좌우로 흔들림 (점점 잦아듦)
            float fade = 1f - cycle / 0.5f;
            float wave = Mathf.Sin(cycle * 45f);
            rect.anchoredPosition = statHintBasePos + new Vector2(wave * 6f * fade, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, wave * 5f * fade);
        }
        else
        {
            rect.anchoredPosition = statHintBasePos;
            rect.localRotation = Quaternion.identity;
        }
    }

    private static void AddClick(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }
}
