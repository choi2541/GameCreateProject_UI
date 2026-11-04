using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 생존 시간, 처치 수, 일시정지, 게임 오버, 재시작을 관리합니다.
[DisallowMultipleComponent]
public class GameSession : MonoBehaviour
{
    private const string BestTimeKey = "BestSurvivalTime";

    public static GameSession Instance { get; private set; }

    [SerializeField] private PlayerHealth player;

    [Tooltip("메인 메뉴 씬 이름입니다. 비워두면 메인 메뉴 버튼이 숨겨집니다.")]
    [SerializeField] private string mainMenuScene = "";

    private bool restarting;
    private float pausedTimeScale = 1f;

    public float SurvivalTime { get; private set; }
    public int Kills { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsNewBest { get; private set; }
    public float BestTime { get; private set; }
    public PlayerHealth Player => player;
    public bool HasMainMenu => !string.IsNullOrEmpty(mainMenuScene);

    // 게임 오버, 이미 멈춘 상태, 전직 선택 중에는 일시정지할 수 없습니다.
    public bool CanPause
    {
        get
        {
            if (IsGameOver || IsPaused || Time.timeScale <= 0f)
                return false;
            PlayerProgression progression = PlayerProgression.Instance;
            return progression == null || !progression.IsChoosingJob;
        }
    }

    public event Action GameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("GameSession은 씬에 하나만 두세요.", this);
            enabled = false;
            return;
        }
        Instance = this;

        if (player == null)
            player = FindAnyObjectByType<PlayerHealth>();

        BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
    }

    private void Update()
    {
        if (IsGameOver)
            return;

        if (player != null && player.IsDead)
        {
            EndGame();
            return;
        }

        // 일시정지 중에는 deltaTime이 0이라 시간이 흐르지 않습니다.
        SurvivalTime += Time.deltaTime;
    }

    // 적이 죽을 때 호출합니다.
    public void RegisterKill()
    {
        if (!IsGameOver)
            Kills++;
    }

    public bool Pause()
    {
        if (!CanPause)
            return false;
        pausedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsPaused = true;
        return true;
    }

    public void Resume()
    {
        if (!IsPaused)
            return;
        IsPaused = false;
        if (!IsGameOver)
            Time.timeScale = pausedTimeScale;
    }

    public void Restart()
    {
        if (restarting) return;
        restarting = true;
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            SceneManager.LoadScene(scene.buildIndex);
        else
            SceneManager.LoadScene(scene.name);
    }

    public void LoadMainMenu()
    {
        if (!HasMainMenu)
            return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EndGame()
    {
        IsGameOver = true;
        IsPaused = false;

        if (SurvivalTime > BestTime)
        {
            BestTime = SurvivalTime;
            IsNewBest = true;
            PlayerPrefs.SetFloat(BestTimeKey, BestTime);
            PlayerPrefs.Save();
        }

        GameOver?.Invoke();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
