#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

// 메뉴: Tools > Game UI > Build Gameplay UI
// 현재 열린 씬에 HUD / 일시정지 메뉴 / 설정 / 캐릭터 창(Tab) / 게임 오버 / 툴팁을 만들고 참조까지 연결합니다.
// 만든 뒤에는 씬에서 자유롭게 위치·색·이미지를 수정하면 됩니다. (다시 실행하면 새로 만들어지니 주의)
public static class GameUIBuilder
{
    private const string CanvasName = "GameUICanvas";
    private const string SpriteFolder = "Assets/UI/Generated";

    private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.96f);
    private static readonly Color InnerColor = new Color(0.13f, 0.15f, 0.2f, 1f);
    private static readonly Color TrackColor = new Color(0.04f, 0.04f, 0.06f, 0.85f);
    private static readonly Color HpColor = new Color(0.88f, 0.22f, 0.25f);
    private static readonly Color ExpColor = new Color(0.35f, 0.72f, 1f);
    private static readonly Color AccentColor = new Color(1f, 0.8f, 0.28f);
    private static readonly Color ButtonColor = new Color(0.2f, 0.23f, 0.31f, 0.95f);
    private static readonly Color GoodColor = new Color(0.22f, 0.55f, 0.36f);
    private static readonly Color DangerColor = new Color(0.9f, 0.22f, 0.26f);
    private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.98f);
    private static readonly Color SubTextColor = new Color(0.68f, 0.7f, 0.77f);
    private static readonly Color CooldownColor = new Color(0f, 0f, 0f, 0.65f);

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
    private static readonly Vector2 MiddleRight = new Vector2(1f, 0.5f);

    private static Sprite square;
    private static Sprite rounded;
    private static Sprite circle;
    private static Sprite triangle;
    private static Font font;

    // 하단 오른쪽 스킬 슬롯 4개 (왼쪽 → 오른쪽): SHIFT(대쉬), Z, X, C
    // 키가 같은 스킬 스크립트가 생기면 자동으로 연결됩니다.
    private static readonly string[][] SlotKeywords =
    {
        new[] { "Dash" },
        new[] { "Piercing" },
        new string[0],
        new string[0],
    };
    private static readonly string[] SlotFallbackKeys = { "SHIFT", "Z", "X", "C" };

    [MenuItem("Tools/Game UI/Build Gameplay UI")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }

        GameObject old = GameObject.Find(CanvasName);
        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("Game UI",
                    $"{CanvasName}이(가) 이미 있습니다.\n지우고 새로 만들까요?\n(직접 수정한 위치/색은 초기화됩니다)",
                    "새로 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(old);
        }

        LoadAssets();
        EnsureEventSystem();

        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (player == null)
            Debug.LogWarning("[GameUIBuilder] 씬에서 PlayerHealth를 찾지 못했습니다. GameSession의 Player를 직접 연결해주세요.");
        EnsureSession(player);
        // UI-only merge: do not add gameplay stats.
        TurnOffOldDebugUI(player);

        GameObject canvasObject = CreateCanvas();
        Transform root = canvasObject.transform;

        // 체력 낮을 때 화면 가장자리 붉게 (HUD 뒤)
        Img(Stretched("LowHealthVignette", root), null, Color.clear).gameObject.AddComponent<LowHealthVignette>();

        GameHUD hud = canvasObject.AddComponent<GameHUD>();
        SerializedObject hudSo = new SerializedObject(hud);
        BuildHud(root, hudSo, player);
        BuildPauseMenu(root, hudSo);
        BuildSettings(canvasObject, hudSo);
        BuildCharacterWindow(root, hudSo);
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        // UI-only merge: do not enable augments.
        BuildGameOver(canvasObject);
        BuildTooltip(canvasObject);
        canvasObject.AddComponent<GameUIFont>();
        // canvasObject.AddComponent<DamageTracker>();       // 피해/회복 감지 (흡혈, 데미지 숫자)
        // canvasObject.AddComponent<DamageNumbers>();       // 데미지 숫자
        canvasObject.AddComponent<AudioVolumeController>(); // 배경음/효과음 볼륨

        Undo.RegisterCreatedObjectUndo(canvasObject, "Build Gameplay UI");
        EnsureSceneInBuild(canvasObject.scene);
        EditorSceneManager.MarkSceneDirty(canvasObject.scene);
        Selection.activeGameObject = canvasObject;
        Debug.Log("[GameUIBuilder] 게임 플레이 UI 생성 완료. 씬을 저장해주세요 (Ctrl+S).");
    }

    // ───────────────────────── 시작 화면 (별도 씬) ─────────────────────────

    private const string MenuSceneName = "MainMenu";
    // 메인 화면 배경 이미지. 같은 이름으로 다른 그림을 덮어쓰면 다음 생성 때 그 그림이 쓰입니다.
    private const string MenuBackgroundPath = "Assets/UI/MainMenuBackground.png";
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    // 게임 씬을 연 상태에서 실행하세요.
    // 1) 지금 열린 씬을 "게임 씬"으로 기억하고, GameSession의 Main Menu Scene 칸을 채움
    // 2) Assets/Scenes/MainMenu.unity 새 씬을 만들고 시작 화면 UI를 생성
    // 3) Build Settings 순서를 [MainMenu, 게임 씬]으로 맞춤
    [MenuItem("Tools/Game UI/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }

        Scene gameScene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(gameScene.path) || gameScene.path == MenuScenePath)
        {
            EditorUtility.DisplayDialog("Game UI", "먼저 게임 씬을 열고 저장한 상태에서 실행해주세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null &&
            !EditorUtility.DisplayDialog("Game UI", $"{MenuScenePath}이(가) 이미 있습니다.\n새로 만들까요?", "새로 만들기", "취소"))
            return;

        string gamePath = gameScene.path;
        string gameName = gameScene.name;

        // 게임 씬의 GameSession에 메인 메뉴 씬 이름 연결
        GameSession session = Object.FindAnyObjectByType<GameSession>(FindObjectsInactive.Include);
        if (session != null)
        {
            SerializedObject so = new SerializedObject(session);
            so.FindProperty("mainMenuScene").stringValue = MenuSceneName;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);
        }
        else
        {
            Debug.LogWarning("[GameUIBuilder] 게임 씬에 GameSession이 없습니다. Build Gameplay UI를 먼저 실행하면 MAIN MENU 버튼이 연결됩니다.");
        }

        // 새 씬 생성
        LoadAssets();
        EnsureFolder("Assets/Scenes");
        Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);

        EnsureEventSystem();
        GameObject canvasObject = CreateCanvas();
        canvasObject.name = "MainMenuCanvas";
        BuildMainMenu(canvasObject, gameName);
        canvasObject.AddComponent<GameUIFont>();
        canvasObject.AddComponent<AudioVolumeController>();

        EditorSceneManager.SaveScene(menuScene, MenuScenePath);
        SetBuildOrder(MenuScenePath, gamePath);
        Selection.activeGameObject = canvasObject;
        Debug.Log($"[GameUIBuilder] 시작 화면 씬 생성 완료: {MenuScenePath} → 시작하면 '{gameName}' 씬으로 이동합니다.");
    }

    private static void BuildMainMenu(GameObject canvasObject, string gameSceneName)
    {
        Transform root = canvasObject.transform;
        RectTransform bg = Stretched("Background", root);
        Img(bg, null, new Color(0.07f, 0.08f, 0.11f));

        // 배경 그림: 화면을 꽉 채우고, 비율이 안 맞는 부분은 잘림 (가운데 기준)
        Sprite art = LoadMenuBackground();
        if (art != null)
        {
            RectTransform artRect = Node("Art", bg);
            artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.sizeDelta = new Vector2(1920f, 1080f);
            Image artImage = Img(artRect, art, Color.white);
            artImage.preserveAspect = false;
            AspectRatioFitter fitter = artRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = art.rect.width / Mathf.Max(1f, art.rect.height);
            artRect.gameObject.AddComponent<MenuBackgroundPan>(); // 양옆으로 천천히 왔다갔다

            // 글자가 잘 보이도록 살짝 어둡게
            Img(Stretched("Dim", bg), null, new Color(0f, 0f, 0f, 0.28f));
        }

        // 제목 상자 (임시 제목)
        RectTransform titleBox = Anchored("TitleBox", root, TopCenter, TopCenter,
            new Vector2(0f, -110f), new Vector2(1000f, 240f));
        Img(titleBox, rounded, new Color(InnerColor.r, InnerColor.g, InnerColor.b, 0.82f));
        Label("Title", titleBox, "PROJECT SURVIVOR", 80, AccentColor, TextAnchor.MiddleCenter,
            Center, Center, new Vector2(0f, 18f), new Vector2(960f, 120f), true);
        Label("Subtitle", titleBox, "(temporary title)", 26, SubTextColor, TextAnchor.MiddleCenter,
            Center, Center, new Vector2(0f, -64f), new Vector2(960f, 40f));

        // 버튼 3개 (세로)
        RectTransform list = Anchored("Buttons", root, Center, TopCenter,
            new Vector2(0f, 20f), new Vector2(440f, 360f));
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button newGame = MakeButton("NewGameButton", list, "NEW GAME", GoodColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(440f, 90f), 36);
        Button cont = MakeButton("ContinueButton", list, "CONTINUE", ButtonColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(440f, 90f), 36);
        Button quit = MakeButton("QuitButton", list, "QUIT", DangerColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(440f, 90f), 36);

        Label("Version", root, "v0.5", 22, SubTextColor, TextAnchor.MiddleRight,
            BottomRight, BottomRight, new Vector2(-32f, 24f), new Vector2(300f, 30f));

        MainMenuUI ui = canvasObject.AddComponent<MainMenuUI>();
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("gameSceneName").stringValue = gameSceneName;
        Set(so, "newGameButton", newGame);
        Set(so, "continueButton", cont);
        Set(so, "quitButton", quit);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite LoadMenuBackground()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(MenuBackgroundPath) == null)
        {
            Debug.Log($"[GameUIBuilder] {MenuBackgroundPath} 이 없어 단색 배경을 사용합니다.");
            return null;
        }
        TextureImporter importer = AssetImporter.GetAtPath(MenuBackgroundPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(MenuBackgroundPath);
    }

    // Build Settings를 [메뉴 씬, 게임 씬, 나머지...] 순서로 맞춥니다.
    private static void SetBuildOrder(string menuPath, string gamePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(menuPath, true),
            new EditorBuildSettingsScene(gamePath, true),
        };
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path != menuPath && scene.path != gamePath)
                scenes.Add(scene);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ───────────────────────── HUD ─────────────────────────

    private static void BuildHud(Transform root, SerializedObject so, PlayerHealth player)
    {
        RectTransform hud = Stretched("HUD", root);

        // 경험치 바 (화면 최상단 전체)
        RectTransform expTrack = Node("ExpBar", hud);
        expTrack.anchorMin = new Vector2(0f, 1f);
        expTrack.anchorMax = Vector2.one;
        expTrack.pivot = TopCenter;
        expTrack.sizeDelta = new Vector2(0f, 14f);
        expTrack.anchoredPosition = Vector2.zero;
        Img(expTrack, square, TrackColor);
        Image expFill = Filled(Stretched("Fill", expTrack, 2f), square, ExpColor, Image.FillMethod.Horizontal);
        expFill.fillAmount = 0f;

        // 체력 바 (좌상단)
        RectTransform hpTrack = Anchored("HpBar", hud, TopLeft, TopLeft, new Vector2(32f, -36f), new Vector2(380f, 34f));
        Img(hpTrack, rounded, TrackColor);
        Image hpFill = Filled(Stretched("Fill", hpTrack, 4f), square, HpColor, Image.FillMethod.Horizontal);
        Text hpText = Txt(Stretched("Text", hpTrack), "10 / 10", 22, TextColor, TextAnchor.MiddleCenter, true);

        // 레벨, 스탯 포인트 알림, 전직 버튼 (체력 바 아래)
        Text levelText = Label("LevelText", hud, "Lv. 1", 30, AccentColor, TextAnchor.MiddleLeft,
            TopLeft, TopLeft, new Vector2(34f, -80f), new Vector2(120f, 40f), true);
        Text statHint = Label("StatHint", hud, "STAT +0  [TAB]", 22, ExpColor, TextAnchor.MiddleLeft,
            TopLeft, MiddleLeft, new Vector2(34f, -140f), new Vector2(300f, 32f), true);
        statHint.gameObject.SetActive(false);
        Button jobButton = MakeButton("JobButton", hud, "CHOOSE JOB", GoodColor, rounded,
            TopLeft, TopLeft, new Vector2(150f, -80f), new Vector2(200f, 40f), 20);
        jobButton.gameObject.SetActive(false);

        // 생존 시간 (상단 중앙)
        Text timeText = Label("TimeText", hud, "00:00", 52, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -28f), new Vector2(320f, 64f), true);

        // 레벨업 표시 (시간 아래, 잠깐 나타났다 사라짐)
        Text levelUp = Label("LevelUpText", hud, "LEVEL UP!", 34, AccentColor, TextAnchor.MiddleCenter,
            TopCenter, Center, new Vector2(0f, -130f), new Vector2(500f, 50f), true);
        levelUp.gameObject.SetActive(false);

        // 우상단: 일시정지, 처치 수
        Button pauseButton = MakeButton("PauseButton", hud, "II", ButtonColor, circle,
            TopRight, TopRight, new Vector2(-32f, -32f), new Vector2(64f, 64f), 26);
        Text killText = Label("KillText", hud, "KILLS  0", 30, TextColor, TextAnchor.MiddleRight,
            TopRight, TopRight, new Vector2(-116f, -42f), new Vector2(260f, 44f), true);

        // 스킬 슬롯 4개 (오른쪽 아래 끝)
        RectTransform skillBar = Anchored("SkillBar", hud, BottomRight, BottomRight,
            new Vector2(-40f, 52f), new Vector2(4 * 104f + 3 * 20f, 104f));
        List<MonoBehaviour> skills = FindSkills(player);
        for (int i = 0; i < 4; i++)
        {
            float x = i * (104f + 20f);
            MonoBehaviour skill = FindByKeyword(skills, SlotKeywords[i]);
            BuildSkillSlot(skillBar, i, new Vector2(x, 0f), skill, SlotKeywords[i], SlotFallbackKeys[i], i == 0);
        }

        Set(so, "hpFill", hpFill);
        Set(so, "hpText", hpText);
        Set(so, "expFill", expFill);
        Set(so, "levelText", levelText);
        Set(so, "statHintText", statHint);
        Set(so, "levelUpText", levelUp);
        Set(so, "jobButton", jobButton);
        Set(so, "timeText", timeText);
        Set(so, "killText", killText);
        Set(so, "pauseButton", pauseButton);
    }

    private static void BuildSkillSlot(Transform parent, int index, Vector2 position, MonoBehaviour skill,
        string[] keywords, string fallbackKey, bool showAfterJob)
    {
        RectTransform slot = Anchored($"SkillSlot{index + 1}", parent, Vector2.zero, Vector2.zero,
            position, new Vector2(104f, 104f));
        Img(slot, circle, new Color(1f, 1f, 1f, 0.22f)); // 테두리

        Image background = Img(Stretched("Background", slot, 5f), circle, ButtonColor);
        Image icon = Img(Stretched("Icon", slot, 20f), null, Color.white);
        icon.preserveAspect = true;
        icon.enabled = false;

        Image overlay = Filled(Stretched("Cooldown", slot, 5f), circle, CooldownColor, Image.FillMethod.Radial360);
        overlay.fillAmount = 0f;
        overlay.enabled = false;

        Text cooldownText = Txt(Stretched("CooldownText", slot), "", 34, TextColor, TextAnchor.MiddleCenter, true);
        Text keyText = Label("KeyText", slot, fallbackKey, 20, SubTextColor, TextAnchor.MiddleCenter,
            BottomCenter, TopCenter, new Vector2(0f, -4f), new Vector2(140f, 28f), true);

        SkillSlotUI ui = slot.gameObject.AddComponent<SkillSlotUI>();
        SerializedObject so = new SerializedObject(ui);
        Set(so, "skillSource", skill);
        SetStrings(so, "autoBindKeywords", keywords);
        so.FindProperty("fallbackKeyLabel").stringValue = fallbackKey;
        so.FindProperty("showAfterJob").boolValue = showAfterJob;
        Set(so, "background", background);
        Set(so, "icon", icon);
        Set(so, "cooldownOverlay", overlay);
        Set(so, "cooldownText", cooldownText);
        Set(so, "keyText", keyText);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 일시정지 메뉴 ─────────────────────────

    private static void BuildPauseMenu(Transform root, SerializedObject so)
    {
        RectTransform panel = Overlay("PausePanel", root, 0.6f);
        RectTransform window = Box("Window", panel, new Vector2(520f, 640f));
        Label("Title", window, "PAUSED", 56, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -40f), new Vector2(440f, 80f));

        RectTransform list = Anchored("Buttons", window, TopCenter, TopCenter,
            new Vector2(0f, -150f), new Vector2(360f, 440f));
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Button resume = MakeButton("ResumeButton", list, "RESUME", GoodColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(360f, 72f));
        Button settings = MakeButton("SettingsButton", list, "SETTINGS", ButtonColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(360f, 72f));
        Button retry = MakeButton("RetryButton", list, "RETRY", ButtonColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(360f, 72f));
        Button menu = MakeButton("MainMenuButton", list, "MAIN MENU", ButtonColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(360f, 72f));
        Button quit = MakeButton("QuitButton", list, "QUIT GAME", DangerColor, rounded,
            TopCenter, TopCenter, Vector2.zero, new Vector2(360f, 72f));

        Label("Hint", panel, "ESC", 22, SubTextColor, TextAnchor.MiddleCenter,
            BottomCenter, BottomCenter, new Vector2(0f, 40f), new Vector2(400f, 30f));
        panel.gameObject.SetActive(false);

        Set(so, "pausePanel", panel.gameObject);
        Set(so, "resumeButton", resume);
        Set(so, "settingsButton", settings);
        Set(so, "retryButton", retry);
        Set(so, "mainMenuButton", menu);
        Set(so, "quitButton", quit);
    }

    // ───────────────────────── 설정 ─────────────────────────

    private static void BuildSettings(GameObject canvasObject, SerializedObject hudSo)
    {
        Transform root = canvasObject.transform;
        RectTransform panel = Overlay("SettingsPanel", root, 0.6f);
        RectTransform window = Box("Window", panel, new Vector2(640f, 680f));
        Label("Title", window, "SETTINGS", 52, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -36f), new Vector2(500f, 76f));

        Text masterValue = VolumeRow(window, "MASTER VOLUME", 170f, out Slider master);
        Text bgmValue = VolumeRow(window, "MUSIC (BGM)", 60f, out Slider bgm);
        Text sfxValue = VolumeRow(window, "EFFECTS (SFX)", -50f, out Slider sfx);

        Label("FullscreenLabel", window, "FULLSCREEN", 26, SubTextColor, TextAnchor.MiddleLeft,
            Center, Center, new Vector2(-80f, -160f), new Vector2(400f, 40f));
        Toggle fullscreen = MakeToggle("FullscreenToggle", window, new Vector2(210f, -160f));

        Button back = MakeButton("BackButton", window, "BACK", ButtonColor, rounded,
            BottomCenter, BottomCenter, new Vector2(0f, 36f), new Vector2(300f, 70f));
        panel.gameObject.SetActive(false);

        SettingsUI settings = canvasObject.AddComponent<SettingsUI>();
        SerializedObject so = new SerializedObject(settings);
        Set(so, "volumeSlider", master);
        Set(so, "volumeValueText", masterValue);
        Set(so, "bgmSlider", bgm);
        Set(so, "bgmValueText", bgmValue);
        Set(so, "sfxSlider", sfx);
        Set(so, "sfxValueText", sfxValue);
        Set(so, "fullscreenToggle", fullscreen);
        so.ApplyModifiedPropertiesWithoutUndo();

        Set(hudSo, "settingsPanel", panel.gameObject);
        Set(hudSo, "settingsBackButton", back);
    }

    // 라벨 + 퍼센트 + 슬라이더 한 줄
    private static Text VolumeRow(Transform window, string label, float y, out Slider slider)
    {
        Label(label + " Label", window, label, 26, SubTextColor, TextAnchor.MiddleLeft,
            Center, Center, new Vector2(-80f, y), new Vector2(400f, 40f));
        Text value = Label(label + " Value", window, "100%", 26, TextColor, TextAnchor.MiddleRight,
            Center, Center, new Vector2(170f, y), new Vector2(120f, 40f));
        slider = MakeSlider(label + " Slider", window, new Vector2(0f, y - 46f), new Vector2(480f, 24f));
        return value;
    }

    // ───────────────────────── 캐릭터 창 (Tab) ─────────────────────────

    private static void BuildCharacterWindow(Transform root, SerializedObject hudSo)
    {
        RectTransform panel = Overlay("CharacterPanel", root, 0.65f);
        Vector2 windowSize = new Vector2(700f, 760f);

        // 왼쪽: 캐릭터 정보
        RectTransform left = Box("CharacterWindow", panel, windowSize);
        left.anchoredPosition = new Vector2(-380f, 20f);
        Label("Title", left, "CHARACTER", 44, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(600f, 64f));

        RectTransform frame = Anchored("PortraitFrame", left, TopCenter, TopCenter,
            new Vector2(0f, -110f), new Vector2(240f, 240f));
        Img(frame, rounded, InnerColor);
        Image portrait = Img(Stretched("Portrait", frame, 24f), null, Color.white);
        portrait.preserveAspect = true;

        Text jobText = Label("JobText", left, "NOVICE", 40, AccentColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -364f), new Vector2(600f, 56f), true);
        Text levelText = Label("LevelText", left, "Lv. 1", 28, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -418f), new Vector2(600f, 40f));

        Text hp = InfoRow(left, "HP", -480f);
        Text exp = InfoRow(left, "EXP", -530f);
        Text time = InfoRow(left, "TIME", -580f);
        Text kills = InfoRow(left, "KILLS", -630f);

        // 캐릭터 창 바로 아래: 보유 증강 (작은 아이콘 줄)
        RectTransform strip = Anchored("OwnedAugments", panel, Center, Center, new Vector2(-380f, -405f), new Vector2(700f, 70f));
        Img(strip, rounded, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.9f));
        Label("Title", strip, "AUGMENTS", 20, SubTextColor, TextAnchor.MiddleLeft,
            MiddleLeft, MiddleLeft, new Vector2(18f, 0f), new Vector2(120f, 30f));
        RectTransform icons = Anchored("Icons", strip, MiddleLeft, MiddleLeft, new Vector2(140f, 0f), new Vector2(545f, 56f));
        HorizontalLayoutGroup iconLayout = icons.gameObject.AddComponent<HorizontalLayoutGroup>();
        iconLayout.spacing = 8f;
        iconLayout.childAlignment = TextAnchor.MiddleLeft;
        iconLayout.childControlWidth = false;
        iconLayout.childControlHeight = false;
        iconLayout.childForceExpandWidth = false;
        iconLayout.childForceExpandHeight = false;
        Text emptyText = Label("Empty", strip, "-", 22, SubTextColor, TextAnchor.MiddleLeft,
            MiddleLeft, MiddleLeft, new Vector2(140f, 0f), new Vector2(400f, 30f));
        OwnedAugmentsUI owned = strip.gameObject.AddComponent<OwnedAugmentsUI>();
        SerializedObject ownedSo = new SerializedObject(owned);
        Set(ownedSo, "iconContainer", icons);
        Set(ownedSo, "emptyText", emptyText);
        Set(ownedSo, "circleSprite", circle);
        ownedSo.ApplyModifiedPropertiesWithoutUndo();

        CharacterWindowUI info = left.gameObject.AddComponent<CharacterWindowUI>();
        SerializedObject infoSo = new SerializedObject(info);
        Set(infoSo, "portrait", portrait);
        Set(infoSo, "jobText", jobText);
        Set(infoSo, "levelText", levelText);
        Set(infoSo, "hpText", hp);
        Set(infoSo, "expText", exp);
        Set(infoSo, "timeText", time);
        Set(infoSo, "killText", kills);
        infoSo.ApplyModifiedPropertiesWithoutUndo();

        // 오른쪽: 스탯 찍기
        RectTransform right = Box("StatWindow", panel, windowSize);
        right.anchoredPosition = new Vector2(380f, 20f);
        right.gameObject.SetActive(false);
        strip.gameObject.SetActive(false);
        left.anchoredPosition = new Vector2(0f, 20f);
        Label("Title", right, "STATS", 44, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -30f), new Vector2(600f, 64f));
        Text points = Label("PointsText", right, "POINTS  0", 30, AccentColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -104f), new Vector2(600f, 44f), true);

        // 스탯 칸: 동그란 아이콘이 가로로 3개씩, 아래에 값 + ▲
        RectTransform rows = Anchored("StatGrid", right, TopCenter, TopCenter,
            new Vector2(0f, -190f), new Vector2(620f, 520f));
        GridLayoutGroup grid = rows.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 230f);
        grid.spacing = new Vector2(10f, 24f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        StatRowUI template = BuildStatCellTemplate(rows);

        StatPanelUI statPanel = right.gameObject.AddComponent<StatPanelUI>();
        SerializedObject statSo = new SerializedObject(statPanel);
        Set(statSo, "rowTemplate", template);
        Set(statSo, "rowContainer", rows);
        Set(statSo, "pointsText", points);
        statSo.ApplyModifiedPropertiesWithoutUndo();

        Label("Hint", panel, "Hold [TAB]   ·   Hover an icon for details   ·   Click a number for +5 / +10", 24, SubTextColor,
            TextAnchor.MiddleCenter, BottomCenter, BottomCenter, new Vector2(0f, 46f), new Vector2(900f, 34f));

        panel.gameObject.SetActive(false);
        Set(hudSo, "characterPanel", panel.gameObject);
    }

    private static Text InfoRow(Transform window, string label, float y)
    {
        Label(label + " Label", window, label, 26, SubTextColor, TextAnchor.MiddleLeft,
            TopCenter, TopCenter, new Vector2(-130f, y), new Vector2(300f, 44f));
        return Label(label + " Value", window, "-", 28, TextColor, TextAnchor.MiddleRight,
            TopCenter, TopCenter, new Vector2(130f, y), new Vector2(300f, 44f));
    }

    private static StatRowUI BuildStatCellTemplate(Transform parent)
    {
        RectTransform cell = Node("StatCellTemplate", parent);

        // 동그란 아이콘 (나중에 PlayerStats에서 이미지 지정). 마우스를 올리면 툴팁.
        RectTransform ring = Anchored("IconRing", cell, TopCenter, TopCenter, Vector2.zero, new Vector2(150f, 150f));
        Image frame = Img(ring, circle, new Color(1f, 1f, 1f, 0.22f));
        RectTransform iconRect = Stretched("Icon", ring, 6f);
        Image icon = Img(iconRect, circle, Color.gray, true);
        Text letter = Txt(Stretched("Letter", iconRect), "STR", 30, TextColor, TextAnchor.MiddleCenter, true);
        UITooltipTrigger trigger = iconRect.gameObject.AddComponent<UITooltipTrigger>();

        // 아래 줄:  [+5]  값  [+10]  /  평소에는  값 ▲
        RectTransform valueRect = Anchored("Value", cell, BottomCenter, BottomCenter,
            new Vector2(0f, 6f), new Vector2(76f, 60f));
        Image valueHit = Img(valueRect, null, new Color(1f, 1f, 1f, 0f), true); // 클릭 영역
        Button valueButton = valueRect.gameObject.AddComponent<Button>();
        valueButton.targetGraphic = valueHit;
        valueButton.navigation = new Navigation { mode = Navigation.Mode.None };
        Text valueText = Txt(Stretched("Text", valueRect), "0", 44, AccentColor, TextAnchor.MiddleCenter, true);

        RectTransform plusRect = Anchored("PlusButton", cell, BottomCenter, BottomCenter,
            new Vector2(64f, 12f), new Vector2(44f, 44f));
        Image plusImage = Img(plusRect, triangle, GoodColor, true);
        Button plus = plusRect.gameObject.AddComponent<Button>();
        plus.targetGraphic = plusImage;
        plus.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = plus.colors;
        colors.highlightedColor = new Color(0.8f, 1f, 0.85f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);
        plus.colors = colors;

        Button plus5 = MakeButton("Plus5Button", cell, "+5", GoodColor, rounded,
            BottomCenter, BottomCenter, new Vector2(-68f, 12f), new Vector2(58f, 44f), 22);
        Button plus10 = MakeButton("Plus10Button", cell, "+10", GoodColor, rounded,
            BottomCenter, BottomCenter, new Vector2(68f, 12f), new Vector2(58f, 44f), 22);
        plus5.gameObject.SetActive(false);
        plus10.gameObject.SetActive(false);

        StatRowUI ui = cell.gameObject.AddComponent<StatRowUI>();
        SerializedObject so = new SerializedObject(ui);
        Set(so, "iconFrame", frame);
        Set(so, "icon", icon);
        Set(so, "iconLetter", letter);
        Set(so, "tooltip", trigger);
        Set(so, "valueText", valueText);
        Set(so, "valueButton", valueButton);
        Set(so, "plusButton", plus);
        Set(so, "plus5Button", plus5);
        Set(so, "plus10Button", plus10);
        so.ApplyModifiedPropertiesWithoutUndo();

        cell.gameObject.SetActive(false);
        return ui;
    }

    // ───────────────────────── 증강 선택 (카드 3장) ─────────────────────────

    private static void BuildAugmentPanel(GameObject canvasObject)
    {
        RectTransform panel = Overlay("AugmentPanel", canvasObject.transform, 0.72f);
        Label("Title", panel, "CHOOSE AN AUGMENT", 56, AccentColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -110f), new Vector2(1200f, 80f), true);

        AugmentCardUI[] cards = new AugmentCardUI[3];
        for (int i = 0; i < 3; i++)
            cards[i] = BuildAugmentCard(panel, i, new Vector2((i - 1) * 470f, -30f));

        Label("Hint", panel, "Click a card", 24, SubTextColor, TextAnchor.MiddleCenter,
            BottomCenter, BottomCenter, new Vector2(0f, 60f), new Vector2(600f, 34f));
        panel.gameObject.SetActive(false);

        AugmentSystem system = canvasObject.AddComponent<AugmentSystem>();
        SerializedObject so = new SerializedObject(system);
        Set(so, "panel", panel.gameObject);
        SerializedProperty list = so.FindProperty("cards");
        list.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AugmentCardUI BuildAugmentCard(Transform parent, int index, Vector2 position)
    {
        RectTransform card = Anchored($"Card{index + 1}", parent, Center, Center, position, new Vector2(420f, 580f));
        Image frame = Img(card, rounded, Color.gray, true); // 등급 색 테두리 (클릭 판정)
        RectTransform inner = Stretched("Inner", card, 6f);
        Img(inner, rounded, PanelColor);

        Text rarity = Label("Rarity", inner, "COMMON", 24, SubTextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -26f), new Vector2(380f, 34f));

        RectTransform iconRect = Anchored("Icon", inner, TopCenter, TopCenter, new Vector2(0f, -76f), new Vector2(170f, 170f));
        Image icon = Img(iconRect, circle, Color.gray);
        Text letter = Txt(Stretched("Letter", iconRect), "?", 72, TextColor, TextAnchor.MiddleCenter, true);

        Text name = Label("Name", inner, "Augment", 40, TextColor, TextAnchor.MiddleCenter,
            TopCenter, TopCenter, new Vector2(0f, -272f), new Vector2(380f, 56f), true);
        Text desc = Label("Description", inner, "Description", 28, SubTextColor, TextAnchor.UpperCenter,
            TopCenter, TopCenter, new Vector2(0f, -346f), new Vector2(360f, 140f));
        desc.fontStyle = FontStyle.Normal;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text stack = Label("Stacks", inner, "0 / 5", 24, AccentColor, TextAnchor.MiddleCenter,
            BottomCenter, BottomCenter, new Vector2(0f, 24f), new Vector2(380f, 34f));

        AugmentCardUI ui = card.gameObject.AddComponent<AugmentCardUI>();
        SerializedObject so = new SerializedObject(ui);
        Set(so, "frame", frame);
        Set(so, "icon", icon);
        Set(so, "iconLetter", letter);
        Set(so, "rarityText", rarity);
        Set(so, "nameText", name);
        Set(so, "descriptionText", desc);
        Set(so, "stackText", stack);
        so.ApplyModifiedPropertiesWithoutUndo();
        return ui;
    }

    // ───────────────────────── 게임 오버 ─────────────────────────

    private static void BuildGameOver(GameObject canvasObject)
    {
        Transform root = canvasObject.transform;
        RectTransform panel = Overlay("GameOverPanel", root, 0.8f);
        CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

        Label("Title", panel, "GAME OVER", 110, DangerColor, TextAnchor.MiddleCenter,
            Center, Center, new Vector2(0f, 280f), new Vector2(1000f, 140f), true);
        GameObject newBest = Label("NewBest", panel, "NEW BEST!", 34, AccentColor, TextAnchor.MiddleCenter,
            Center, Center, new Vector2(0f, 190f), new Vector2(400f, 50f), true).gameObject;

        RectTransform stats = Box("Stats", panel, new Vector2(640f, 300f));
        stats.anchoredPosition = new Vector2(0f, -10f);
        Text timeText = StatRow(stats, "SURVIVAL TIME", 105f);
        Text killText = StatRow(stats, "KILLS", 35f);
        Text levelText = StatRow(stats, "LEVEL", -35f);
        Text bestText = StatRow(stats, "BEST TIME", -105f);

        Button retryButton = MakeButton("RetryButton", panel, "RETRY", GoodColor, rounded,
            Center, Center, new Vector2(-170f, -260f), new Vector2(300f, 84f), 32);
        Button menuButton = MakeButton("MainMenuButton", panel, "MAIN MENU", ButtonColor, rounded,
            Center, Center, new Vector2(170f, -260f), new Vector2(300f, 84f), 32);
        panel.gameObject.SetActive(false);

        GameOverUI ui = canvasObject.AddComponent<GameOverUI>();
        SerializedObject so = new SerializedObject(ui);
        Set(so, "panel", panel.gameObject);
        Set(so, "fadeGroup", group);
        Set(so, "timeText", timeText);
        Set(so, "killText", killText);
        Set(so, "levelText", levelText);
        Set(so, "bestText", bestText);
        Set(so, "newBestLabel", newBest);
        Set(so, "retryButton", retryButton);
        Set(so, "mainMenuButton", menuButton);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Text StatRow(Transform box, string label, float y)
    {
        Label(label + " Label", box, label, 28, SubTextColor, TextAnchor.MiddleLeft,
            Center, Center, new Vector2(-130f, y), new Vector2(300f, 50f));
        return Label(label + " Value", box, "-", 34, TextColor, TextAnchor.MiddleRight,
            Center, Center, new Vector2(130f, y), new Vector2(300f, 50f));
    }

    // ───────────────────────── 툴팁 ─────────────────────────

    private static void BuildTooltip(GameObject canvasObject)
    {
        RectTransform tip = Anchored("Tooltip", canvasObject.transform, Center, TopLeft, Vector2.zero, new Vector2(380f, 100f));
        Image bg = Img(tip, rounded, new Color(0.05f, 0.06f, 0.08f, 0.97f));
        bg.raycastTarget = false;

        VerticalLayoutGroup layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 16, 18);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = tip.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CanvasGroup group = tip.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        Text title = Txt(Node("Title", tip), "Title", 26, AccentColor, TextAnchor.UpperLeft);
        title.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text body = Txt(Node("Body", tip), "Description", 22, TextColor, TextAnchor.UpperLeft);
        body.fontStyle = FontStyle.Normal;
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.lineSpacing = 1.15f;

        tip.gameObject.SetActive(false);
        tip.SetAsLastSibling();

        UITooltip ui = canvasObject.AddComponent<UITooltip>();
        SerializedObject so = new SerializedObject(ui);
        Set(so, "root", tip);
        Set(so, "titleText", title);
        Set(so, "bodyText", body);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 씬 준비 ─────────────────────────

    private static GameObject CreateCanvas()
    {
        GameObject obj = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        obj.layer = 5; // UI

        Canvas canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // 전직 선택창(100)보다 아래

        CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return obj;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject obj = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        obj.AddComponent<InputSystemUIInputModule>();
#else
        obj.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(obj, "Create EventSystem");
    }

    private static void EnsureSession(PlayerHealth player)
    {
        GameSession session = Object.FindAnyObjectByType<GameSession>(FindObjectsInactive.Include);
        if (session == null)
        {
            GameObject obj = new GameObject("GameSession");
            Undo.RegisterCreatedObjectUndo(obj, "Create GameSession");
            session = obj.AddComponent<GameSession>();
        }

        if (player == null)
            return;

        SerializedObject so = new SerializedObject(session);
        SerializedProperty prop = so.FindProperty("player");
        if (prop != null && prop.objectReferenceValue == null)
        {
            prop.objectReferenceValue = player;
            so.ApplyModifiedProperties();
        }
    }

    private static void EnsurePlayerStats(PlayerHealth player)
    {
        if (player == null)
            return;
        PlayerStats existing = player.GetComponent<PlayerStats>();
        if (existing != null)
        {
            // 예전에 붙인 PlayerStats(3개)에 새 기본 스탯을 추가 (아이콘·설명은 유지)
            Undo.RecordObject(existing, "Add Missing Stats");
            existing.AddMissingDefaults();
            EditorUtility.SetDirty(existing);
            return;
        }
        Undo.AddComponent<PlayerStats>(player.gameObject);
        Debug.Log("[GameUIBuilder] Player에 PlayerStats를 추가했습니다. 스탯 아이콘/설명은 여기서 수정하세요.", player);
    }

    // 예전 OnGUI 표시와 LevelAndJobUI 하단 경험치 바를 끕니다. (새 HUD와 겹치지 않게)
    private static void TurnOffOldDebugUI(PlayerHealth player)
    {
        if (player == null)
            return;

        SetBool(player, "showDebugGUI", false);
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null) SetBool(movement, "showDashCharges", false);
        if (player.GetComponent<StackDashUIAdapter>() == null)
            Undo.AddComponent<StackDashUIAdapter>(player.gameObject);

        LevelAndJobUI levelUI = player.GetComponentInChildren<LevelAndJobUI>(true);
        if (levelUI != null)
            SetBool(levelUI, "showExperienceHUD", false);

        foreach (PiercingProjectileSkill skill in player.GetComponentsInChildren<PiercingProjectileSkill>(true))
            SetBool(skill, "showCooldown", false);
    }

    private static void SetBool(Object target, string property, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogWarning($"[GameUIBuilder] {target.GetType().Name}에 '{property}'가 없습니다. 수정된 팀 스크립트로 교체했는지 확인해주세요.", target);
            return;
        }
        prop.boolValue = value;
        so.ApplyModifiedProperties();
    }

    // 다시하기(씬 재로드)가 작동하려면 씬이 Build Settings에 있어야 합니다.
    private static void EnsureSceneInBuild(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogWarning("[GameUIBuilder] 씬이 아직 저장되지 않았습니다. 저장 후 다시 실행하면 Build Settings에 자동 등록됩니다.");
            return;
        }

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path != scene.path)
                continue;
            if (!scenes[i].enabled)
            {
                scenes[i].enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[GameUIBuilder] {scene.path}을(를) Build Settings에 추가했습니다.");
    }

    private static List<MonoBehaviour> FindSkills(PlayerHealth player)
    {
        List<MonoBehaviour> list = new List<MonoBehaviour>();
        if (player == null)
            return list;

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is ISkillCooldown)
                list.Add(behaviour);
        }
        return list;
    }

    private static MonoBehaviour FindByKeyword(List<MonoBehaviour> skills, string[] keywords)
    {
        foreach (MonoBehaviour skill in skills)
        {
            string typeName = skill.GetType().Name;
            foreach (string keyword in keywords)
            {
                if (typeName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return skill;
            }
        }
        return null;
    }

    // ───────────────────────── 에셋 ─────────────────────────

    private static void LoadAssets()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureFolder("Assets/UI");
        EnsureFolder(SpriteFolder);
        square = SpriteAsset("UI_Square.png", 8, 0f, 0);
        rounded = SpriteAsset("UI_Rounded.png", 64, 16f, 16);
        circle = SpriteAsset("UI_Circle.png", 256, 128f, 0);
        triangle = TriangleAsset("UI_Triangle.png", 64);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    // 흰색 사각형 / 둥근 사각형 / 원 스프라이트를 PNG로 만들어 저장합니다. (이미 있으면 재사용)
    private static Sprite SpriteAsset(string fileName, int size, float radius, int border)
    {
        string path = $"{SpriteFolder}/{fileName}";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 1f;
                if (radius > 0f)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    alpha = Mathf.Clamp01(radius - distance + 0.5f);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 위쪽을 가리키는 흰색 삼각형 (스탯 올리기 버튼용)
    private static Sprite TriangleAsset(string fileName, int size)
    {
        string path = $"{SpriteFolder}/{fileName}";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        float margin = size * 0.08f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                // 아래 변(y=margin)에서 위 꼭짓점(y=size-margin)으로 갈수록 좁아짐
                float t = (py - margin) / (size - 2f * margin);
                float halfWidth = (1f - t) * (size * 0.5f - margin);
                float edge = Mathf.Min(halfWidth - Mathf.Abs(px - size * 0.5f), py - margin);
                float alpha = t < 0f || t > 1f ? 0f : Mathf.Clamp01(edge + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ───────────────────────── UI 헬퍼 ─────────────────────────

    private static RectTransform Node(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    private static RectTransform Anchored(string name, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        RectTransform rect = Node(name, parent);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static RectTransform Stretched(string name, Transform parent, float inset = 0f)
    {
        RectTransform rect = Node(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    private static RectTransform Overlay(string name, Transform parent, float dim)
    {
        RectTransform rect = Stretched(name, parent);
        Img(rect, null, new Color(0f, 0f, 0f, dim), true); // 뒤쪽 클릭 차단
        return rect;
    }

    private static RectTransform Box(string name, Transform parent, Vector2 size)
    {
        RectTransform rect = Anchored(name, parent, Center, Center, Vector2.zero, size);
        Img(rect, rounded, PanelColor, true);
        return rect;
    }

    private static Image Img(RectTransform rect, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null && sprite == rounded)
            image.type = Image.Type.Sliced;
        return image;
    }

    private static Image Filled(RectTransform rect, Sprite sprite, Color color, Image.FillMethod method)
    {
        Image image = Img(rect, sprite, color);
        image.type = Image.Type.Filled;
        image.fillMethod = method;
        image.fillAmount = 1f;
        if (method == Image.FillMethod.Radial360)
        {
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = false;
        }
        else
        {
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        return image;
    }

    private static Text Txt(RectTransform rect, string value, int size, Color color, TextAnchor align,
        bool outline = false)
    {
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (outline)
        {
            Outline effect = rect.gameObject.AddComponent<Outline>();
            effect.effectColor = new Color(0f, 0f, 0f, 0.7f);
            effect.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    private static Text Label(string name, Transform parent, string value, int size, Color color,
        TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 boxSize, bool outline = false)
    {
        return Txt(Anchored(name, parent, anchor, pivot, position, boxSize), value, size, color, align, outline);
    }

    private static Button MakeButton(string name, Transform parent, string label, Color color, Sprite shape,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, int fontSize = 30)
    {
        RectTransform rect = Anchored(name, parent, anchor, pivot, position, size);
        Image image = Img(rect, shape, color, true);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        // 키보드(Space/Enter)로 버튼이 눌리지 않게 합니다.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        Txt(Stretched("Label", rect), label, fontSize, TextColor, TextAnchor.MiddleCenter);
        return button;
    }

    private static Slider MakeSlider(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = Anchored(name, parent, Center, Center, position, size);
        Img(Stretched("Background", rect), rounded, TrackColor);

        RectTransform fillArea = Stretched("Fill Area", rect);
        RectTransform fill = Stretched("Fill", fillArea);
        fill.sizeDelta = Vector2.zero;
        Img(fill, rounded, ExpColor);

        RectTransform handleArea = Stretched("Handle Slide Area", rect);
        handleArea.offsetMin = new Vector2(16f, 0f);
        handleArea.offsetMax = new Vector2(-16f, 0f);
        RectTransform handle = Node("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(36f, 16f);
        Image handleImage = Img(handle, circle, TextColor, true);

        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }

    private static Toggle MakeToggle(string name, Transform parent, Vector2 position)
    {
        RectTransform rect = Anchored(name, parent, Center, Center, position, new Vector2(56f, 56f));
        Image bg = Img(rect, rounded, TrackColor, true);
        Image check = Img(Stretched("Checkmark", rect, 12f), rounded, AccentColor);

        Toggle toggle = rect.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = check;
        toggle.isOn = true;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        return toggle;
    }

    private static void Set(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
        {
            Debug.LogError($"[GameUIBuilder] {so.targetObject.GetType().Name}에 '{property}' 필드가 없습니다.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    private static void SetStrings(SerializedObject so, string property, string[] values)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop == null)
            return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
#endif
