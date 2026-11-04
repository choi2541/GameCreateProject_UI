using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 시작 화면 (임시). NEW GAME / CONTINUE / QUIT
//
// [이어하기 연결 - 개발 파트 참고]
//  - 저장 기능이 생기면 저장할 때 PlayerPrefs.SetInt(MainMenuUI.SaveFlagKey, 1) 을 해주세요.
//    그러면 CONTINUE 버튼이 활성화됩니다. (지금은 저장 기능이 없어서 비활성)
//  - 게임 씬에서 MainMenuUI.ContinueRequested 가 true면 "이어하기"로 들어온 것이니 저장 데이터를 불러오면 됩니다.
[DisallowMultipleComponent]
public class MainMenuUI : MonoBehaviour
{
    public const string SaveFlagKey = "HasSaveData";

    public static bool ContinueRequested { get; private set; }

    [Tooltip("시작 / 이어하기 때 불러올 게임 씬 이름 (Build Settings에 있어야 함)")]
    [SerializeField] private string gameSceneName = "";

    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        Time.timeScale = 1f;

        if (newGameButton != null)
            newGameButton.onClick.AddListener(StartNewGame);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueGame);
            continueButton.interactable = PlayerPrefs.GetInt(SaveFlagKey, 0) == 1;
        }
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (string.IsNullOrEmpty(gameSceneName))
            Debug.LogWarning("[MainMenuUI] Game Scene Name이 비어 있습니다.", this);
    }

    public void StartNewGame()
    {
        ContinueRequested = false;
        LoadGame();
    }

    public void ContinueGame()
    {
        ContinueRequested = true;
        LoadGame();
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

    private void LoadGame()
    {
        if (string.IsNullOrEmpty(gameSceneName))
            return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }
}
