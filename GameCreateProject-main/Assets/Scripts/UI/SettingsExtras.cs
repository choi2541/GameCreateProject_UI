using UnityEngine;
using UnityEngine.UI;

// 설정 창에 추가하는 항목 (실행할 때 기존 설정 창의 버튼·글자 모양을 복사해서 만듦 → UI를 새로 빌드할 필요 없음)
//   TAB WINDOW : HOLD (누르고 있는 동안) / TOGGLE (눌러서 열고 다시 눌러서 닫기)
//   KEY SETTINGS : 키 바꾸기 창 (대쉬, 스킬 1~3, 캐릭터 창)
[DisallowMultipleComponent]
public class SettingsExtras : MonoBehaviour
{
    private RectTransform window;     // 원래 설정 창
    private GameObject keyWindow;     // 키 설정 창
    private Text tabModeText;
    private Text noteText;
    private readonly Text[] keyTexts = new Text[KeyBindings.All.Length];
    private int waitingIndex = -1;
    private static KeyCode[] keyboardKeys;
    private static int captureEndFrame = -1;

    // 키를 기다리는 중 (이 프레임에 막 끝난 경우 포함) → GameHUD가 ESC/Tab을 무시하도록
    public static bool IsCapturing => anyWaiting || Time.frameCount == captureEndFrame;
    private static bool anyWaiting;

    // SettingsUI가 실행될 때 한 번 호출. window = 설정 창(볼륨 슬라이더들의 부모)
    public static void Attach(RectTransform settingsWindow)
    {
        if (settingsWindow == null || settingsWindow.parent == null) return;
        GameObject panel = settingsWindow.parent.gameObject;
        if (panel.GetComponent<SettingsExtras>() != null) return;
        panel.AddComponent<SettingsExtras>().Build(settingsWindow);
    }

    private void Build(RectTransform settingsWindow)
    {
        window = settingsWindow;
        Text labelTemplate = Find<Text>(window, "FullscreenLabel");
        Button buttonTemplate = Find<Button>(window, "BackButton");
        if (labelTemplate == null || buttonTemplate == null)
        {
            Debug.LogWarning("[SettingsExtras] 설정 창 모양을 찾지 못해 Tab/키 설정 항목을 추가하지 못했습니다.", this);
            return;
        }

        // 키 설정 창 = 원래 창을 복사해서 제목/BACK만 남김 (항목 추가 전에 복사)
        RectTransform keys = Instantiate(window, window.parent);
        keys.name = "KeyBindWindow";
        foreach (Transform child in keys)
        {
            if (child.name == "Title" || child.name == "BackButton") continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        keyWindow = keys.gameObject;

        // 두 창 모두 아래로 늘려서 자리 만들기 (제목은 위, BACK은 아래에 붙어 있어서 같이 이동)
        window.sizeDelta += new Vector2(0f, 300f); // BACK(아래 고정)과 KEY SETTINGS가 안 겹치게
        keys.sizeDelta = window.sizeDelta;

        // ── 원래 설정 창: TAB WINDOW + KEY SETTINGS ──
        MakeLabel(labelTemplate, window, "TAB WINDOW", new Vector2(-80f, -250f), new Vector2(400f, 40f));
        Button tabMode = MakeButton(buttonTemplate, window, "", new Vector2(170f, -250f), new Vector2(220f, 52f));
        tabModeText = tabMode.GetComponentInChildren<Text>(true);
        tabMode.onClick.AddListener(() => { KeyBindings.TabToggle = !KeyBindings.TabToggle; RefreshTexts(); });

        Button openKeys = MakeButton(buttonTemplate, window, "KEY SETTINGS", new Vector2(0f, -330f), new Vector2(360f, 60f));
        openKeys.onClick.AddListener(() => ShowKeys(true));

        // ── 키 설정 창 ──
        Text title = Find<Text>(keys, "Title");
        if (title != null) title.text = "KEY SETTINGS";
        Button keysBack = Find<Button>(keys, "BackButton");
        if (keysBack != null)
        {
            keysBack.onClick.RemoveAllListeners();
            keysBack.onClick.AddListener(() => ShowKeys(false));
        }

        float y = 250f;
        for (int i = 0; i < KeyBindings.All.Length; i++)
        {
            int index = i;
            GameAction action = KeyBindings.All[i];
            MakeLabel(labelTemplate, keys, KeyBindings.DisplayName(action), new Vector2(-120f, y), new Vector2(360f, 40f));
            Button b = MakeButton(buttonTemplate, keys, "", new Vector2(170f, y), new Vector2(220f, 52f));
            keyTexts[i] = b.GetComponentInChildren<Text>(true);
            b.onClick.AddListener(() => { waitingIndex = index; RefreshTexts(); });
            y -= 70f;
        }

        Button reset = MakeButton(buttonTemplate, keys, "RESET", new Vector2(0f, y - 20f), new Vector2(260f, 52f));
        reset.onClick.AddListener(() => { waitingIndex = -1; KeyBindings.ResetAll(); RefreshTexts(); });

        noteText = MakeLabel(labelTemplate, keys, "", new Vector2(0f, y - 100f), new Vector2(580f, 90f));
        noteText.alignment = TextAnchor.MiddleCenter;
        noteText.fontSize = Mathf.Max(16, labelTemplate.fontSize - 6);

        keyWindow.SetActive(false);
        RefreshTexts();
    }

    private void OnDisable()
    {
        // 설정 창을 닫으면 다음에 열 때 원래 창부터
        waitingIndex = -1;
        if (keyWindow != null && keyWindow.activeSelf) ShowKeys(false);
    }

    private void ShowKeys(bool show)
    {
        waitingIndex = -1;
        if (keyWindow != null) keyWindow.SetActive(show);
        if (window != null) window.gameObject.SetActive(!show);
        RefreshTexts();
    }

    private void Update()
    {
        if (waitingIndex < 0) return;
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        if (keyboardKeys == null) keyboardKeys = BuildKeyList();
        foreach (KeyCode k in keyboardKeys)
        {
            if (!Input.GetKeyDown(k)) continue;
            if (k != KeyCode.Escape) KeyBindings.Set(KeyBindings.All[waitingIndex], k);
            waitingIndex = -1;
            captureEndFrame = Time.frameCount;
            RefreshTexts();
            return;
        }
#endif
    }

    private void RefreshTexts()
    {
        anyWaiting = waitingIndex >= 0;
        if (tabModeText != null)
            tabModeText.text = KeyBindings.TabToggle ? "TOGGLE" : "HOLD";
        for (int i = 0; i < keyTexts.Length; i++)
        {
            if (keyTexts[i] == null) continue;
            keyTexts[i].text = i == waitingIndex ? "..." : KeyBindings.Pretty(KeyBindings.Get(KeyBindings.All[i]));
        }
        if (noteText != null)
        {
            noteText.text = waitingIndex >= 0
                ? "바꿀 키를 누르세요  (ESC = 취소)"
                : "4차 스킬은 Ctrl + SKILL 2 / SKILL 3\n같은 키를 고르면 두 키가 서로 바뀝니다";
        }
    }

    // 키보드 키만 (마우스·조이스틱 제외)
    private static KeyCode[] BuildKeyList()
    {
        var list = new System.Collections.Generic.List<KeyCode>();
        foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (k == KeyCode.None) continue;
            if (k >= KeyCode.Mouse0) continue; // Mouse0 이후는 마우스/조이스틱
            if (!list.Contains(k)) list.Add(k);
        }
        return list.ToArray();
    }

    // ───────── 만들기 도우미 (기존 모양 복사) ─────────
    private static T Find<T>(Transform root, string name) where T : Component
    {
        Transform t = root.Find(name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private static Text MakeLabel(Text template, Transform parent, string text, Vector2 pos, Vector2 size)
    {
        Text t = Instantiate(template, parent);
        t.name = "Label_" + text;
        t.text = text;
        t.gameObject.SetActive(true);
        Place((RectTransform)t.transform, pos, size);
        return t;
    }

    private static Button MakeButton(Button template, Transform parent, string text, Vector2 pos, Vector2 size)
    {
        Button b = Instantiate(template, parent);
        b.name = "Button_" + (string.IsNullOrEmpty(text) ? "Key" : text);
        b.onClick.RemoveAllListeners();
        b.gameObject.SetActive(true);
        Text label = b.GetComponentInChildren<Text>(true);
        if (label != null) label.text = text;
        Place((RectTransform)b.transform, pos, size);
        return b;
    }

    private static void Place(RectTransform r, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
