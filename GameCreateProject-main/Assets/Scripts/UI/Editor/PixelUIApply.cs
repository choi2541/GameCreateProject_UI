#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴: Tools > Game UI > Pixel UI - Apply (Wood)   ← 나무 (기본)
//       Tools > Game UI > Pixel UI - Apply (Stone)  ← 돌
// 도트 그림을 메인 화면 씬과 게임 씬의 UI에 "직접" 넣고 씬을 저장합니다. (배경 그림처럼 Inspector에서 보임)
// 게임 중에 그림을 불러오는 코드는 없음. 한 번 누르면 끝. 숫자를 바꿨거나 테마를 바꿀 때 다시 누르면 됨.
//
// 그림 위치: 프로젝트 어디든 폴더 이름만 Wood / Stone / HUD 이면 찾음 (권장: Assets/UI/PixelUI/Wood 등)
public static class PixelUIApply
{
    // ───────── 테마별 숫자 (그림에 맞춰 잰 값) ─────────
    private class Theme
    {
        public string Name;
        public float PanelBorderPx, PanelBorderOnScreen; public bool PanelTiled;
        public float FrameBorderPx, FrameBorderOnScreen;
        public float ButtonCap, PauseCircleRatio, TitleTextWidth;
        public Color TitleColor, SubtitleColor, Outline;
    }

    private static readonly Theme Wood = new Theme
    {
        Name = "Wood", PanelBorderPx = 235f, PanelBorderOnScreen = 75f, PanelTiled = false,
        FrameBorderPx = 200f, FrameBorderOnScreen = 40f, ButtonCap = 0.14f, PauseCircleRatio = 628f / 671f,
        TitleTextWidth = 0.70f, TitleColor = new Color(1f, 0.78f, 0.36f), SubtitleColor = new Color(0.93f, 0.86f, 0.74f, 0.9f),
        Outline = new Color(0.1f, 0.06f, 0.04f, 1f),
    };

    private static readonly Theme Stone = new Theme
    {
        Name = "Stone", PanelBorderPx = 250f, PanelBorderOnScreen = 100f, PanelTiled = true,
        FrameBorderPx = 180f, FrameBorderOnScreen = 50f, ButtonCap = 0.16f, PauseCircleRatio = 604f / 693f,
        TitleTextWidth = 0.70f, TitleColor = new Color(1f, 0.84f, 0.35f), SubtitleColor = new Color(0.8f, 0.92f, 0.94f, 0.9f),
        Outline = new Color(0.07f, 0.09f, 0.14f, 1f),
    };

    private const float PauseCircleSize = 64f;       // 일시정지 버튼 동그라미 지름
    private const float PausePanelExtraHeight = 50f; // 일시정지 창: 버튼 5개가 아래 모서리 장식과 안 겹치게
    private static readonly Color LabelColor = new Color(1f, 0.96f, 0.88f);

    // HUD 위치·크기 (1920x1080 기준)
    private const float ExpTop = 2f, ExpHeight = 20f;
    private static readonly Vector2 BadgePos = new Vector2(18f, -30f), BadgeSize = new Vector2(104f, 108f);
    private static readonly Vector2 HpPos = new Vector2(128f, -38f), HpSize = new Vector2(420f, 46f);
    private static readonly Vector2 StatPos = new Vector2(132f, -92f), StatSize = new Vector2(236f, 44f);
    private static readonly Vector2 JobPos = new Vector2(378f, -90f), JobSize = new Vector2(236f, 48f);
    private static readonly Vector2 TimerPos = new Vector2(0f, -30f), TimerSize = new Vector2(270f, 68f); // 시간판: 높이 그대로, 양옆만 줄임
    private static readonly Vector2 KillPos = new Vector2(-110f, -36f), KillSize = new Vector2(220f, 52f);
    private static readonly Vector2 KeyTagPos = new Vector2(0f, -4f);
    private static readonly Vector2 KeyTagSize = new Vector2(100f, 28f); // 칸(104)보다 좁게 → 옆 칸 이름표와 안 붙음
    private static readonly Vector2 SkillBarPos = new Vector2(-40f, 52f); // 스킬 칸: 오른쪽 아래 고정
    private const float RingScale = 108f / 104f;

    // HUD 그림 안에서 잰 값 (그림 픽셀)
    private static readonly Vector4 HpSlot = new Vector4(194f, 50f, 43f, 52f);   // 왼, 아래, 오른, 위
    private static readonly Vector4 ExpSlot = new Vector4(63f, 20f, 65f, 17f);
    private const float PlateIconRight = 132f, TagIconRight = 154f, JobIconRight = 146f;
    private static readonly Vector4 JobPad = new Vector4(6f, 9f, 6f, 8f);
    private const float BadgeCircleCenterY = 90f / 222f;
    private const float RingOuterRatio = 552f / 598f, RingHoleRatio = 431f / 598f;

    // 9칸 나누기 테두리 (그림 픽셀: 왼, 아래, 오른, 위) — 그림 가져오기 설정에 직접 넣음
    // 그림 원래 가로 크기. 그림을 줄여도(가벼워지게) 아래 숫자들(테두리, 칸 위치)은 원래 크기 기준이라
    // "지금 크기 ÷ 원래 크기" 비율(K)을 곱해서 맞춤. 그림을 새로 바꾸면 여기 숫자도 새 그림 크기로.
    private static readonly Dictionary<string, float> OriginalWidth = new Dictionary<string, float>
    {
        { "HUD/ui_button_job", 1004f },
        { "HUD/ui_button_job_glow", 1004f },
        { "HUD/ui_exp_fill", 1552f },
        { "HUD/ui_exp_frame", 1684f },
        { "HUD/ui_hp_fill", 1448f },
        { "HUD/ui_hp_frame", 1652f },
        { "HUD/ui_hp_lag", 1448f },
        { "HUD/ui_key_tag", 2036f },
        { "HUD/ui_kill_plate", 912f },
        { "HUD/ui_level_badge", 216f },
        { "HUD/ui_rune_cdr", 440f },
        { "HUD/ui_rune_exp", 440f },
        { "HUD/ui_rune_skl", 440f },
        { "HUD/ui_rune_spd", 440f },
        { "HUD/ui_rune_str", 440f },
        { "HUD/ui_rune_vit", 440f },
        { "HUD/ui_skill_slot_locked", 600f },
        { "HUD/ui_skill_slot_normal", 600f },
        { "HUD/ui_skill_slot_ready", 600f },
        { "HUD/ui_stat_tag", 992f },
        { "HUD/ui_timer_plate", 916f },
        { "Stone/ui_button_disabled", 1288f },
        { "Stone/ui_button_green", 1312f },
        { "Stone/ui_button_normal", 1296f },
        { "Stone/ui_button_red", 1288f },
        { "Stone/ui_inner_frame", 788f },
        { "Stone/ui_inner_frame_highlight", 788f },
        { "Stone/ui_panel", 1140f },
        { "Stone/ui_pause_hover", 696f },
        { "Stone/ui_pause_normal", 696f },
        { "Stone/ui_pause_pressed", 696f },
        { "Stone/ui_title_board", 2100f },
        { "Wood/ui_button_disabled", 1320f },
        { "Wood/ui_button_green", 1376f },
        { "Wood/ui_button_normal", 1320f },
        { "Wood/ui_button_red", 1376f },
        { "Wood/ui_inner_frame", 768f },
        { "Wood/ui_inner_frame_highlight", 768f },
        { "Wood/ui_panel", 1196f },
        { "Wood/ui_pause_hover", 672f },
        { "Wood/ui_pause_normal", 672f },
        { "Wood/ui_pause_pressed", 672f },
        { "Wood/ui_title_board", 2084f }
    };

    private static float KOf(string key, float currentWidth) =>
        OriginalWidth.TryGetValue(key, out float w) && w > 0f ? currentWidth / w : 1f;

    private static float K(Sprite s)
    {
        if (s == null) return 1f;
        string p = AssetDatabase.GetAssetPath(s).Replace('\\', '/');
        string key = Path.GetFileName(Path.GetDirectoryName(p)) + "/" + Path.GetFileNameWithoutExtension(p);
        return KOf(key, s.rect.width);
    }

    private static readonly Dictionary<string, Vector4> HudBorders = new Dictionary<string, Vector4>
    {
        { "ui_hp_frame", new Vector4(260f, 0f, 60f, 0f) },
        { "ui_exp_frame", new Vector4(110f, 0f, 110f, 0f) },
        { "ui_timer_plate", new Vector4(170f, 0f, 40f, 0f) },
        { "ui_kill_plate", new Vector4(170f, 0f, 40f, 0f) },
        { "ui_stat_tag", new Vector4(190f, 0f, 40f, 0f) },
        { "ui_button_job", new Vector4(166f, 0f, 46f, 0f) },
        { "ui_button_job_glow", new Vector4(166f, 0f, 46f, 0f) },
        { "ui_key_tag", new Vector4(95f, 0f, 95f, 0f) },
    };

    private static readonly string[] GreenButtons = { "NewGameButton", "ResumeButton" };
    private static readonly string[] RedButtons = { "QuitButton" };
    private static readonly string[] NormalButtons = { "ContinueButton", "SettingsButton", "RetryButton", "MainMenuButton", "BackButton" };
    private static readonly string[,] PanelTargets =
    {
        { "Window", "PausePanel" }, { "Window", "SettingsPanel" }, { "KeyBindWindow", "SettingsPanel" },
        { "CharacterWindow", "" }, { "StatWindow", "" },
    };
    private static readonly string[] FrameTargets = { "PortraitFrame", "OwnedAugments" };

    // ───────── 메뉴 ─────────
    [MenuItem("Tools/Game UI/Pixel UI - Apply (Wood)")]
    private static void ApplyWood() => Run(Wood);

    [MenuItem("Tools/Game UI/Pixel UI - Apply (Stone)")]
    private static void ApplyStone() => Run(Stone);

    private static Theme theme;
    private static Dictionary<string, string> paths;      // "Wood/ui_panel" → 파일 경로
    private static readonly Dictionary<Sprite, Vector4> body = new Dictionary<Sprite, Vector4>();
    private static readonly Dictionary<Sprite, float> plank = new Dictionary<Sprite, float>();

    private static void Run(Theme t)
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Pixel UI", "플레이를 끄고 눌러 주세요.", "확인"); return; }
        // 열린 씬에 저장 안 한 변경(예: 2·3차 Setup)이 있으면 먼저 저장 → 씬을 다시 열어도 안 사라짐
        EditorSceneManager.SaveOpenScenes();
        theme = t;
        body.Clear(); plank.Clear();

        FindImages();
        int themeCount = 0, hudCount = 0;
        foreach (string k in paths.Keys) { if (k.StartsWith(t.Name + "/")) themeCount++; if (k.StartsWith("HUD/")) hudCount++; }
        if (themeCount == 0 && hudCount == 0)
        {
            EditorUtility.DisplayDialog("Pixel UI", $"그림을 못 찾았어요.\n{t.Name} 폴더와 HUD 폴더(안에 ui_*.png)를 프로젝트 Assets 아래에 넣어 주세요.\n(권장: Assets/UI/PixelUI/{t.Name}, Assets/UI/PixelUI/HUD)", "확인");
            return;
        }
        SetupImporters();

        // 빌드에 들어가는 씬 전부에 적용 (메인 화면 + 게임)
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var done = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (!s.enabled || !File.Exists(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int changed = ApplyToOpenScene();
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done.Add($"{Path.GetFileNameWithoutExtension(s.path)} ({changed}곳)");
            }
        }
        if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);

        string msg = done.Count > 0 ? $"{t.Name} 그림을 넣었어요:\n  " + string.Join("\n  ", done) + $"\n\n그림: {t.Name} {themeCount}장, HUD {hudCount}장"
                                    : "넣을 UI를 못 찾았어요. Build Settings에 메인 화면 씬과 게임 씬이 있는지 확인해 주세요.";
        Debug.Log("[Pixel UI] " + msg.Replace("\n", " "));
        EditorUtility.DisplayDialog("Pixel UI", msg, "확인");
    }

    // ───────── 그림 찾기 · 가져오기 설정 ─────────
    private static void FindImages()
    {
        paths = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (!p.EndsWith(".png") || !Path.GetFileName(p).StartsWith("ui_")) continue;
            string folder = Path.GetFileName(Path.GetDirectoryName(p));
            if (folder != "Wood" && folder != "Stone" && folder != "HUD") continue;
            string key = folder + "/" + Path.GetFileNameWithoutExtension(p);
            if (!paths.ContainsKey(key)) paths[key] = p;
            else Debug.LogWarning($"[Pixel UI] 같은 그림이 두 군데 있어요: {paths[key]} / {p} (앞의 것을 씀)");
        }
    }

    private static void SetupImporters()
    {
        foreach (KeyValuePair<string, string> kv in paths)
        {
            TextureImporter imp = AssetImporter.GetAtPath(kv.Value) as TextureImporter;
            if (imp == null) continue;
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            Vector4 border = BorderFor(kv.Key, w, h) * KOf(kv.Key, w);
            bool dirty = imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single
                || imp.filterMode != FilterMode.Point || imp.textureCompression != TextureImporterCompression.CompressedHQ || imp.isReadable
                || imp.mipmapEnabled || imp.spriteBorder != border || imp.maxTextureSize < 4096 || imp.spritePixelsPerUnit != 100f;
            if (!dirty) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;          // 도트 선명하게
            imp.textureCompression = TextureImporterCompression.CompressedHQ; // 고화질 압축(PC는 BC7): 화질 거의 그대로, 메모리 1/4
            imp.isReadable = false;          // 게임 중에 그림을 읽지 않음 → 메모리 사본 안 만듦
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            imp.spritePixelsPerUnit = 100f;
            imp.spriteBorder = border;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 9칸 나누기가 깔끔하게
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
    }

    private static Vector4 BorderFor(string key, int w, int h)
    {
        string name = key.Substring(key.IndexOf('/') + 1);
        if (key.StartsWith("HUD/")) return HudBorders.TryGetValue(name, out Vector4 b) ? b : Vector4.zero;
        if (name.StartsWith("ui_button_"))
        {
            float cap = Mathf.Round(w * theme.ButtonCap);
            return new Vector4(cap, 0f, cap, 0f);
        }
        float all = name == "ui_panel" ? theme.PanelBorderPx
                  : name.StartsWith("ui_inner_frame") ? theme.FrameBorderPx : 0f;
        all = Mathf.Min(all, Mathf.Floor(Mathf.Min(w, h) / Mathf.Max(0.01f, KOf(key, w)) * 0.5f) - 1f);
        return all > 0f ? new Vector4(all, all, all, all) : Vector4.zero;
    }

    private static Sprite S(string folder, string name)
    {
        if (!paths.TryGetValue(folder + "/" + name, out string p)) return null;
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
        if (s != null && !body.ContainsKey(s)) Measure(s, p);
        return s;
    }

    // 그림 파일을 직접 읽어서 판자 위치(글자 가운데)와 몸통(장식 뺀 부분)을 잼
    private static void Measure(Sprite s, string path)
    {
        var tex = new Texture2D(2, 2);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(path))) return;
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height, bottom = -1, top = -1;
            for (int y = 0; y < h; y++)
            {
                int filled = 0;
                for (int x = 0; x < w; x++) if (px[y * w + x].a > 0) filled++;
                if (filled > w * 0.6f) { if (bottom < 0) bottom = y; top = y; }
            }
            if (bottom < 0) { body[s] = Vector4.zero; plank[s] = 0f; return; }
            plank[s] = ((top + bottom) * 0.5f - (h - 1) * 0.5f) / h;
            int rows = top - bottom + 1, left = -1, right = -1;
            for (int x = 0; x < w; x++)
            {
                int filled = 0;
                for (int y = bottom; y <= top; y++) if (px[y * w + x].a > 0) filled++;
                if (filled > rows * 0.8f) { if (left < 0) left = x; right = x; }
            }
            if (left < 0) { left = 0; right = w - 1; }
            body[s] = new Vector4(left, bottom, w - 1 - right, h - 1 - top);
        }
        finally { Object.DestroyImmediate(tex); }
    }

    private static Vector4 BodyOf(Sprite s) => s != null && body.TryGetValue(s, out Vector4 v) ? v : Vector4.zero;
    private static float PlankOf(Sprite s) => s != null && plank.TryGetValue(s, out float v) ? v : 0f;

    // ───────── 열린 씬에 넣기 ─────────
    private static int ApplyToOpenScene()
    {
        string T = theme.Name;
        Sprite normal = S(T, "ui_button_normal"), green = S(T, "ui_button_green"), red = S(T, "ui_button_red"),
               disabled = S(T, "ui_button_disabled"), title = S(T, "ui_title_board"), panel = S(T, "ui_panel"),
               frame = S(T, "ui_inner_frame"), frameHi = S(T, "ui_inner_frame_highlight"),
               pauseN = S(T, "ui_pause_normal"), pauseH = S(T, "ui_pause_hover"), pauseP = S(T, "ui_pause_pressed");
        int count = 0;

        // 예전에 붙었다가 "Missing (Script)"가 된 컴포넌트 정리 (UI 캔버스 안만)
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.gameObject.scene.IsValid() || canvas.transform.parent != null) continue;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!b.gameObject.scene.IsValid()) continue;
            string n = b.name;
            Sprite sp = Contains(GreenButtons, n) ? (green != null ? green : normal)
                      : Contains(RedButtons, n) ? (red != null ? red : normal)
                      : Contains(NormalButtons, n) ? normal : null;
            if (sp != null) { SkinButton(b, sp, disabled); count++; }
            else if (n == "PauseButton" && pauseN != null) { SkinPause(b, pauseN, pauseH, pauseP); count++; }
        }

        foreach (Image img in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!img.gameObject.scene.IsValid()) continue;
            string n = img.name;
            if (n == "TitleBox" && title != null) { SkinTitle(img, title); count++; }
            else if (panel != null && IsPanelTarget(img)) { SkinPanel(img, panel); count++; }
            else if (frame != null && Contains(FrameTargets, n)) { SkinFrame(img, frame); count++; }
            else if (frame != null && n == "Inner" && img.transform.parent != null && img.transform.parent.GetComponent<AugmentCardUI>() != null)
            {
                SkinFrame(img, frame);
                PixelHoverSprite hs = GetOrAdd<PixelHoverSprite>(img.transform.parent.gameObject);
                hs.Set(img, frame, frameHi != null ? frameHi : frame);
                count++;
            }
        }

        foreach (RectTransform hud in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hud.name == "HUD" && hud.Find("HpBar") != null && hud.gameObject.scene.IsValid())
            {
                count += SkinHud(hud);
                count += SetupJobWindow(hud, panel, frame, title, green, red, disabled);
                count += SetupJobFrames(hud, normal, green, disabled);
            }

        return count;
    }

    // ───────── 버튼 ─────────
    private static void SkinButton(Button b, Sprite sprite, Sprite disabled)
    {
        Image hit = b.GetComponent<Image>(); // 원래 버튼 그림: 투명하게 두고 클릭 판정만
        if (hit == null) return;
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        Image art = Child(b.transform, "Art", sprite);
        art.transform.SetAsFirstSibling(); // 글자보다 뒤
        b.targetGraphic = art;
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.93f, 0.93f, 0.93f);
        c.highlightedColor = Color.white;
        c.selectedColor = new Color(0.93f, 0.93f, 0.93f);
        c.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        c.disabledColor = Color.white;
        b.colors = c;

        Text label = FirstText(b.transform);
        if (label != null)
        {
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.color = LabelColor;
            AddOutline(label, 2.5f);
        }
        PixelButton pb = GetOrAdd<PixelButton>(b.gameObject);
        pb.Set(art, sprite, BodyOf(sprite), disabled, BodyOf(disabled), label);
        EditorUtility.SetDirty(pb);
    }

    private static void SkinPause(Button b, Sprite n, Sprite h, Sprite p)
    {
        Image img = b.GetComponent<Image>();
        if (img == null) return;
        img.sprite = n;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        b.targetGraphic = img;

        // 그림 둘레의 빛 자리만큼 크게 → 동그라미가 PauseCircleSize. 가운데 위치는 그대로
        RectTransform rt = (RectTransform)img.transform;
        float size = PauseCircleSize / theme.PauseCircleRatio;
        Vector2 grow = new Vector2(size, size) - rt.sizeDelta;
        rt.anchoredPosition += new Vector2(grow.x * (rt.pivot.x - 0.5f), grow.y * (rt.pivot.y - 0.5f));
        rt.sizeDelta = new Vector2(size, size);

        b.transition = Selectable.Transition.SpriteSwap;
        SpriteState st = b.spriteState;
        st.highlightedSprite = h != null ? h : n;
        st.pressedSprite = p != null ? p : n;
        st.selectedSprite = n;
        st.disabledSprite = n;
        b.spriteState = st;
        Text label = b.GetComponentInChildren<Text>(true);
        if (label != null) label.enabled = false; // 그림에 II가 있음
    }

    // ───────── 제목 판 ─────────
    private static void SkinTitle(Image box, Sprite sprite)
    {
        box.sprite = sprite;
        box.type = Image.Type.Simple;
        box.preserveAspect = true;
        box.color = Color.white;
        RectTransform rt = (RectTransform)box.transform;
        float w = rt.sizeDelta.x;
        float boxH = w * sprite.rect.height / sprite.rect.width;
        rt.sizeDelta = new Vector2(w, boxH);
        float plankY = PlankOf(sprite) * boxH;

        foreach (Text t in box.GetComponentsInChildren<Text>(true))
        {
            Outline o = AddOutline(t, 2f);
            t.alignment = TextAnchor.MiddleCenter;
            RectTransform tr = t.rectTransform;
            if (t.name == "Title")
            {
                t.color = theme.TitleColor;
                o.effectDistance = new Vector2(4f, -4f);
                tr.sizeDelta = new Vector2(w * theme.TitleTextWidth, boxH * 0.32f); // 양옆 기둥 사이에만
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextMaxSize = Mathf.Max(t.fontSize, 20);
                t.resizeTextMinSize = 20;
                t.resizeTextForBestFit = true;
                tr.anchoredPosition = new Vector2(0f, plankY + boxH * 0.06f);
            }
            else if (t.name == "Subtitle")
            {
                t.color = theme.SubtitleColor;
                tr.sizeDelta = new Vector2(w * theme.TitleTextWidth, tr.sizeDelta.y);
                tr.anchoredPosition = new Vector2(0f, plankY - boxH * 0.17f);
            }
        }
    }

    // ───────── 창 판 / 안쪽 칸 ─────────
    private static bool IsPanelTarget(Image img)
    {
        Transform parent = img.transform.parent;
        for (int i = 0; i < PanelTargets.GetLength(0); i++)
        {
            if (img.name != PanelTargets[i, 0]) continue;
            string p = PanelTargets[i, 1];
            if (p.Length == 0 || (parent != null && parent.name == p)) return true;
        }
        return false;
    }

    private static void SkinPanel(Image img, Sprite panel)
    {
        // 일시정지 창: 처음 넣을 때만 길게 (다시 눌러도 또 길어지지 않게)
        bool already = img.sprite != null && AssetDatabase.GetAssetPath(img.sprite).Contains("/ui_panel");
        if (!already && img.transform.parent != null && img.transform.parent.name == "PausePanel")
            ((RectTransform)img.transform).sizeDelta += new Vector2(0f, PausePanelExtraHeight);
        img.sprite = panel;
        img.type = theme.PanelTiled ? Image.Type.Tiled : Image.Type.Sliced;
        img.fillCenter = true;
        img.color = Color.white;
        img.pixelsPerUnitMultiplier = theme.PanelBorderPx * K(panel) / Mathf.Max(1f, theme.PanelBorderOnScreen);

        Transform title = img.transform.Find("Title");
        Text t = title != null ? title.GetComponent<Text>() : null;
        if (t != null)
        {
            t.color = theme.TitleColor;
            AddOutline(t, 3f);
            RectTransform rt = t.rectTransform;
            if (rt.anchorMin.y > 0.99f && rt.anchoredPosition.y > -52f) rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -52f);
        }
    }

    private static void SkinFrame(Image img, Sprite frame)
    {
        img.sprite = frame;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.color = Color.white;
        img.pixelsPerUnitMultiplier = theme.FrameBorderPx * K(frame) / Mathf.Max(1f, theme.FrameBorderOnScreen);
    }

    // ───────── 1차 전직 선택 창 (반반 그림) ─────────
    private const string JobArtName = "job_select_split";
    private static int SetupJobWindow(RectTransform hud, Sprite panel, Sprite frame, Sprite title, Sprite green, Sprite red, Sprite disabled)
    {
        if (Object.FindAnyObjectByType<PlayerProgression>(FindObjectsInactive.Include) == null) return 0; // 게임 씬에만
        Transform canvasRoot = hud.root;
        JobSelectWindow win = GetOrAdd<JobSelectWindow>(canvasRoot.gameObject);

        Sprite art = null;
        foreach (string guid in AssetDatabase.FindAssets(JobArtName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) != JobArtName) continue;
            TextureImporter imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.isReadable || imp.mipmapEnabled
                || imp.filterMode != FilterMode.Bilinear || imp.textureCompression != TextureImporterCompression.CompressedHQ))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Bilinear;   // 일러스트라 부드럽게
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.isReadable = false;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            art = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            break;
        }
        if (art == null) Debug.LogWarning("[Pixel UI] 직업 선택 그림(" + JobArtName + ".png)을 못 찾았어요. 창은 색 상자로 나와요.");

        var so = new SerializedObject(win);
        SetRef(so, "splitArt", art);
        SetRef(so, "panel", panel);
        SetRef(so, "innerFrame", frame);
        SetRef(so, "titleBoard", title);
        SetRef(so, "buttonGreen", green);
        SetRef(so, "buttonRed", red);
        SetRef(so, "buttonDisabled", disabled);
        so.FindProperty("panelPixelsPerUnit").floatValue = theme.PanelBorderPx * K(panel) / Mathf.Max(1f, theme.PanelBorderOnScreen);
        so.FindProperty("framePixelsPerUnit").floatValue = theme.FrameBorderPx * K(frame) / Mathf.Max(1f, theme.FrameBorderOnScreen);
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    // ───────── 2·3·4차 전직 창 테두리 (frame_<종류>_<wood|stone>.png) ─────────
    private static readonly string[] JobFrameKinds = { "2nd", "shadow", "storm", "4th" };
    private const float JobFrameBorderAt800 = 185f; // 가로 800픽셀 그림에서 모서리 장식 크기

    private static int SetupJobFrames(RectTransform hud, Sprite normal, Sprite green, Sprite disabled)
    {
        PixelUISprites holder = GetOrAdd<PixelUISprites>(hud.gameObject);
        string suffix = "_" + theme.Name.ToLowerInvariant();
        var frames = new Sprite[JobFrameKinds.Length];
        int found = 0;
        for (int i = 0; i < JobFrameKinds.Length; i++)
        {
            string want = "frame_" + JobFrameKinds[i] + suffix;
            foreach (string guid in AssetDatabase.FindAssets(want + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) != want) continue;
                TextureImporter imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                float b = Mathf.Round(JobFrameBorderAt800 * w / 800f);
                var border = new Vector4(b, b, b, b);
                if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != border || !imp.mipmapEnabled
                    || imp.filterMode != FilterMode.Bilinear || imp.isReadable || imp.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.filterMode = FilterMode.Bilinear;   // 작게 줄여 보여서 부드럽게
                    imp.mipmapEnabled = true;               // 줄여도 자글거림 없이
                    imp.textureCompression = TextureImporterCompression.CompressedHQ;
                    imp.isReadable = false;
                    imp.alphaIsTransparency = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.maxTextureSize = 2048;
                    imp.spritePixelsPerUnit = 100f;
                    imp.spriteBorder = border;
                    var settings = new TextureImporterSettings();
                    imp.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    imp.SetTextureSettings(settings);
                    imp.SaveAndReimport();
                }
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (frames[i] != null) found++;
                break;
            }
        }
        if (found < JobFrameKinds.Length)
            Debug.LogWarning($"[Pixel UI] 전직 창 테두리 {found}/{JobFrameKinds.Length}장만 찾았어요 (frame_*{suffix}.png). 없는 창은 색 상자로 나와요.");

        holder.jobFrames = found == JobFrameKinds.Length ? frames : new Sprite[JobFrameKinds.Length];
        holder.buttonNormal = normal; holder.buttonNormalInset = BodyOf(normal);
        holder.buttonGreen = green; holder.buttonGreenInset = BodyOf(green);
        holder.buttonDisabled = disabled; holder.buttonDisabledInset = BodyOf(disabled);
        EditorUtility.SetDirty(holder);
        return found > 0 ? 1 : 0;
    }

    // ───────── 게임 화면 HUD ─────────
    private static int SkinHud(RectTransform hud)
    {
        int count = 0;
        Sprite hpFrame = S("HUD", "ui_hp_frame"), hpFill = S("HUD", "ui_hp_fill"), hpLag = S("HUD", "ui_hp_lag"),
               expFrame = S("HUD", "ui_exp_frame"), expFill = S("HUD", "ui_exp_fill"), badge = S("HUD", "ui_level_badge"),
               job = S("HUD", "ui_button_job"), jobGlow = S("HUD", "ui_button_job_glow"), tag = S("HUD", "ui_stat_tag"),
               timer = S("HUD", "ui_timer_plate"), kill = S("HUD", "ui_kill_plate"), keyTag = S("HUD", "ui_key_tag"),
               ringN = S("HUD", "ui_skill_slot_normal"), ringR = S("HUD", "ui_skill_slot_ready"), ringL = S("HUD", "ui_skill_slot_locked");

        // 체력바
        RectTransform hp = hud.Find("HpBar") as RectTransform;
        if (hp != null && hpFrame != null)
        {
            Place(hp, TL, HpPos, HpSize);
            float m = SetSliced(hp.GetComponent<Image>(), hpFrame, HpSize.y);
            Image fill = ChildImage(hp, "Fill");
            if (fill != null)
            {
                FitInside(fill.rectTransform, HpSlot, m);
                if (hpFill != null) { fill.sprite = hpFill; fill.color = Color.white; }
                if (hpLag != null)
                {
                    Image lag = Child(hp, "HpLag", hpLag);
                    lag.type = Image.Type.Filled;
                    lag.fillMethod = Image.FillMethod.Horizontal;
                    lag.fillAmount = fill.fillAmount;
                    FitInside(lag.rectTransform, HpSlot, m);
                    PutBehind(lag.transform, fill.transform); // 빨간 채움 뒤
                    GetOrAdd<PixelHpLag>(hp.gameObject).Set(fill, lag);
                }
            }
            Text hpText = ChildText(hp, "Text");
            if (hpText != null) { FitInside(hpText.rectTransform, HpSlot, m); BestFit(hpText, 24); AddOutline(hpText, 2f); }
            count++;
        }

        // 경험치바 (화면 맨 위 끝에서 끝까지)
        RectTransform exp = hud.Find("ExpBar") as RectTransform;
        if (exp != null && expFrame != null)
        {
            exp.anchorMin = new Vector2(0f, 1f);
            exp.anchorMax = Vector2.one;
            exp.pivot = new Vector2(0.5f, 1f);
            exp.sizeDelta = new Vector2(0f, ExpHeight);
            exp.anchoredPosition = new Vector2(0f, -ExpTop);
            float m = SetSliced(exp.GetComponent<Image>(), expFrame, ExpHeight);
            Image fill = ChildImage(exp, "Fill");
            if (fill != null)
            {
                FitInside(fill.rectTransform, ExpSlot, m);
                if (expFill != null) { fill.sprite = expFill; fill.color = Color.white; }
            }
            count++;
        }

        // 레벨 배지 + 숫자
        Text level = ChildText(hud, "LevelText");
        if (level != null && badge != null)
        {
            Image b = Child(hud, "LevelBadge", badge);
            b.preserveAspect = true;
            Place(b.rectTransform, TL, BadgePos, BadgeSize);
            PutBehind(b.transform, level.transform);
            RectTransform lr = level.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(BadgeSize.x * 0.68f, BadgeSize.y * 0.4f);
            lr.anchoredPosition = new Vector2(BadgePos.x + BadgeSize.x * 0.5f, BadgePos.y - BadgeSize.y * BadgeCircleCenterY);
            level.alignment = TextAnchor.MiddleCenter;
            BestFit(level, 26);
            AddOutline(level, 2f);
            count++;
        }

        // STAT +N [TAB] 알림 표
        Text stat = ChildText(hud, "StatHint");
        if (stat != null && tag != null)
        {
            Place(stat.rectTransform, TL, StatPos, StatSize);
            Image plate = Child(stat.transform, "Tag", tag);
            Stretch(plate.rectTransform);
            float m = SetSliced(plate, tag, StatSize.y);
            Text copy = ChildTextCopy(stat, plate.transform, "TagText");
            copy.rectTransform.offsetMin = new Vector2((TagIconRight + 16f) / m, 0f);
            copy.rectTransform.offsetMax = new Vector2(-40f / m, 0f);
            BestFit(copy, 22);
            Color c = stat.color; c.a = 0f; stat.color = c; // 원래 글자는 숨기고 판 위 글자로
            GetOrAdd<PixelMirrorText>(stat.gameObject).Set(stat, copy);
            count++;
        }

        // CHOOSE JOB
        Transform jt = hud.Find("JobButton");
        Button jobButton = jt != null ? jt.GetComponent<Button>() : null;
        if (jobButton != null && job != null) { SkinJob(jobButton, job, jobGlow); count++; }

        // 스킬 칸
        RepairSkillSlots(hud);
        if (ringN != null)
            foreach (SkillSlotUI slot in hud.GetComponentsInChildren<SkillSlotUI>(true)) { SkinSkillSlot(slot, ringN, ringL, keyTag); count++; }
        // 스킬 칸 묶음: 오른쪽 아래 (가운데 정렬 예외)
        RectTransform skillBar = hud.Find("SkillBar") as RectTransform;
        if (skillBar != null)
        {
            Place(skillBar, new Vector2(1f, 0f), SkillBarPos, skillBar.sizeDelta);
            count++;
        }

        // 시간 / 처치 수
        Text time = ChildText(hud, "TimeText");
        if (time != null && timer != null)
        {
            Image plate = Child(hud, "TimerPlate", timer);
            Place(plate.rectTransform, TC, TimerPos, TimerSize);
            float m = SetSliced(plate, timer, TimerSize.y);
            PutBehind(plate.transform, time.transform);
            float left = (PlateIconRight + 14f) / m, right = 24f;
            // 글자: 모래시계 옆 빈칸 가운데(0.5)보다 판 가운데 쪽으로 (0.25) → 오른쪽으로 치우쳐 보이지 않게
            PlaceInPlate(time, TC, TimerPos + new Vector2((left - right) * 0.25f, 0f), TimerSize.x - left - right, TimerSize.y, 40);
            AddOutline(time, 2f);
            count++;
        }
        Text kills = ChildText(hud, "KillText");
        if (kills != null && kill != null)
        {
            Image plate = Child(hud, "KillPlate", kill);
            Place(plate.rectTransform, TR, KillPos, KillSize);
            float m = SetSliced(plate, kill, KillSize.y);
            PutBehind(plate.transform, kills.transform);
            float left = (PlateIconRight + 14f) / m, right = 14f;
            PlaceInPlate(kills, TR, KillPos - new Vector2(right, 0f), KillSize.x - left - right, KillSize.y, 26);
            AddOutline(kills, 2f);
            count++;
        }

        // 게임 중에 만들어지는 UI용 그림 (4차 칸 고리, 스탯 룬)
        PixelUISprites holder = GetOrAdd<PixelUISprites>(hud.gameObject);
        holder.ringNormal = ringN; holder.ringReady = ringR; holder.ringLocked = ringL;
        holder.ringOuterRatio = RingOuterRatio; holder.ringHoleRatio = RingHoleRatio;
        string[] ids = { "str", "vit", "spd", "exp", "skl", "cdr" };
        holder.runes = new Sprite[ids.Length];
        for (int i = 0; i < ids.Length; i++) holder.runes[i] = S("HUD", "ui_rune_" + ids[i]);
        EditorUtility.SetDirty(holder);
        return count;
    }

    private static void SkinJob(Button b, Sprite job, Sprite glowSprite)
    {
        Place((RectTransform)b.transform, TL, JobPos, JobSize);
        Image hit = b.GetComponent<Image>();
        if (hit != null) { hit.color = new Color(1f, 1f, 1f, 0f); hit.raycastTarget = true; }
        float m = (job.rect.height / K(job) - JobPad.y - JobPad.w) / JobSize.y; // 원래 크기 기준
        Image art = Child(b.transform, "Art", job);
        Image glow = glowSprite != null ? Child(b.transform, "Glow", glowSprite) : null;
        foreach (Image img in new[] { art, glow })
        {
            if (img == null) continue;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = m * K(job); // 실제 그림 크기 기준
            RectTransform r = img.rectTransform;
            Stretch(r);
            r.offsetMin = new Vector2(-JobPad.x / m, -JobPad.y / m);
            r.offsetMax = new Vector2(JobPad.z / m, JobPad.w / m);
        }
        art.transform.SetAsFirstSibling();
        if (glow != null) { glow.transform.SetSiblingIndex(1); GetOrAdd<PixelGlowPulse>(b.gameObject).Set(glow); }
        b.targetGraphic = art;
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.95f, 0.95f, 0.95f);
        c.highlightedColor = Color.white;
        c.selectedColor = new Color(0.95f, 0.95f, 0.95f);
        c.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        c.disabledColor = new Color(0.6f, 0.6f, 0.6f);
        b.colors = c;
        Text label = FirstText(b.transform);
        if (label != null)
        {
            RectTransform lr = label.rectTransform;
            Stretch(lr);
            lr.offsetMin = new Vector2((JobIconRight - JobPad.x + 10f) / m, 0f);
            lr.offsetMax = new Vector2(-(46f - JobPad.z) / m, 0f);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.93f, 0.7f);
            BestFit(label, 20);
            AddOutline(label, 2f);
        }
    }

    // 스킬 칸(SkillSlot1~4)에 SkillSlotUI가 빠져 있으면 (예: SHIFT 대쉬 칸) 다시 붙이고 자식들과 연결
    private static readonly string[] SlotKeys = { "SHIFT", "Z", "X", "C" };
    private static readonly string[][] SlotWords = { new[] { "Dash" }, new[] { "Piercing" }, new string[0], new string[0] }; // 빌더와 같게 (X·C는 키로 연결)
    private static void RepairSkillSlots(Transform hud)
    {
        Transform bar = hud.Find("SkillBar");
        if (bar == null) return;
        foreach (Transform child in bar)
        {
            if (!child.name.StartsWith("SkillSlot") || child.GetComponent<SkillSlotUI>() != null) continue;
            int index = Mathf.Clamp(child.GetSiblingIndex(), 0, SlotKeys.Length - 1);
            int.TryParse(child.name.Substring("SkillSlot".Length), out int n);
            if (n >= 1 && n <= SlotKeys.Length) index = n - 1;

            SkillSlotUI ui = child.gameObject.AddComponent<SkillSlotUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("fallbackKeyLabel").stringValue = SlotKeys[index];
            so.FindProperty("showAfterJob").boolValue = index == 0;
            SerializedProperty words = so.FindProperty("autoBindKeywords");
            words.arraySize = SlotWords[index].Length;
            for (int i = 0; i < SlotWords[index].Length; i++) words.GetArrayElementAtIndex(i).stringValue = SlotWords[index][i];
            SetRef(so, "background", ChildImage(child, "Background"));
            SetRef(so, "icon", ChildImage(child, "Icon"));
            SetRef(so, "cooldownOverlay", ChildImage(child, "Cooldown"));
            SetRef(so, "cooldownText", ChildText(child, "CooldownText"));
            SetRef(so, "keyText", ChildText(child, "KeyText"));
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[PixelUI] " + child.name + "에 빠져 있던 SkillSlotUI를 다시 붙였어요 (" + SlotKeys[index] + ")");
        }
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && value != null) p.objectReferenceValue = value;
    }

    private static void SkinSkillSlot(SkillSlotUI slot, Sprite ringN, Sprite ringL, Sprite keyTag)
    {
        RectTransform sr = (RectTransform)slot.transform;
        Image rim = slot.GetComponent<Image>();
        if (rim != null) rim.color = new Color(1f, 1f, 1f, 0f); // 원래 흰 테두리 숨김

        var so = new SerializedObject(slot);
        Color ready = new Color(0.11f, 0.12f, 0.15f, 0.95f);
        SetColor(so, "readyColor", ready);
        SetColor(so, "lockedColor", new Color(0.07f, 0.07f, 0.09f, 0.9f));
        SetColor(so, "emptyColor", new Color(0.06f, 0.06f, 0.07f, 0.6f));
        so.ApplyModifiedPropertiesWithoutUndo();
        Image background = so.FindProperty("background")?.objectReferenceValue as Image;
        Text cooldownText = so.FindProperty("cooldownText")?.objectReferenceValue as Text;
        Text keyText = so.FindProperty("keyText")?.objectReferenceValue as Text;

        float size = Mathf.Max(sr.sizeDelta.x, 1f) * RingScale / RingOuterRatio;
        Image ring = Child(sr, "Ring", ringN);
        RectTransform r = ring.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size);
        r.anchoredPosition = Vector2.zero;
        ring.preserveAspect = true;
        if (cooldownText != null && cooldownText.transform.parent == sr) PutBehind(ring.transform, cooldownText.transform); // 쿨타임 덮개 위, 쿨타임 숫자 아래
        else ring.transform.SetAsLastSibling();
        if (cooldownText != null) AddOutline(cooldownText, 2f);
        GetOrAdd<PixelSkillRing>(slot.gameObject).Set(background, ring, ringN, ringL, ready);

        if (keyText != null && keyTag != null)
        {
            RectTransform kr = keyText.rectTransform;
            Image plate = Child(kr.parent, "KeyTag", keyTag);
            RectTransform pr = plate.rectTransform;
            // 칸 아래 가운데에 고정 위치 (몇 번 다시 눌러도 같은 자리)
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 1f);
            pr.sizeDelta = KeyTagSize;
            pr.anchoredPosition = KeyTagPos;
            kr.anchorMin = kr.anchorMax = pr.anchorMin; kr.pivot = pr.pivot;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = keyTag.rect.height / KeyTagSize.y;
            PutBehind(plate.transform, keyText.transform);
            kr.sizeDelta = KeyTagSize - new Vector2(30f, 6f); // 이름표 양쪽 장식 안쪽에만 글자 (SHIFT 3/3처럼 길어도 안 삐져나오게)
            kr.anchoredPosition = pr.anchoredPosition + new Vector2(0f, -2f);
            keyText.alignment = TextAnchor.MiddleCenter;
            keyText.color = new Color(0.93f, 0.88f, 0.76f);
            BestFit(keyText, 15);
            keyText.resizeTextMinSize = 8;
            keyText.alignByGeometry = true;
            AddOutline(keyText, 1.5f);
        }
    }

    // ───────── 도우미 ─────────
    private static readonly Vector2 TL = new Vector2(0f, 1f), TC = new Vector2(0.5f, 1f), TR = new Vector2(1f, 1f);

    // back을 front 바로 뒤(아래)에 둠. 몇 번을 다시 눌러도 순서가 뒤집히지 않음
    // (예전엔 다시 누르면 판이 글자 위로 올라가서 시간·KILLS·키 이름·레벨 글자가 가려졌음)
    private static void PutBehind(Transform back, Transform front)
    {
        if (back == null || front == null || back.parent != front.parent) return;
        back.SetSiblingIndex(front.GetSiblingIndex());
        if (back.GetSiblingIndex() > front.GetSiblingIndex()) front.SetSiblingIndex(back.GetSiblingIndex());
    }

    private static void Place(RectTransform r, Vector2 corner, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = corner;
        r.pivot = corner;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static float SetSliced(Image img, Sprite sprite, float height)
    {
        float m = sprite.rect.height / Mathf.Max(1f, height);
        float k = K(sprite);
        if (img == null) return m / k;
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.pixelsPerUnitMultiplier = m;
        return m / k; // 돌려주는 값은 원래 그림 크기 기준 (칸 위치 숫자들이 원래 크기 기준이라)
    }

    private static void FitInside(RectTransform r, Vector4 slot, float m)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(slot.x / m, slot.y / m);
        r.offsetMax = new Vector2(-slot.z / m, -slot.w / m);
    }

    // 자식 그림 찾기 (없으면 만들기) — 다시 눌러도 같은 걸 고쳐 씀
    private static Image Child(Transform parent, string name, Sprite sprite)
    {
        Transform t = parent.Find(name);
        Image img = t != null ? t.GetComponent<Image>() : null;
        if (img == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
        }
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
    }

    private static Text ChildTextCopy(Text source, Transform parent, string name)
    {
        Transform tr = parent.Find(name);
        Text t = tr != null ? tr.GetComponent<Text>() : null;
        if (t == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            t = go.GetComponent<Text>();
        }
        t.font = source.font;
        t.fontSize = source.fontSize;
        t.fontStyle = source.fontStyle;
        Color c = source.color; c.a = 1f; t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = source.text;
        t.raycastTarget = false;
        Stretch(t.rectTransform);
        AddOutline(t, 2f);
        return t;
    }

    private static Image ChildImage(Transform parent, string name) { Transform t = parent.Find(name); return t != null ? t.GetComponent<Image>() : null; }
    private static Text ChildText(Transform parent, string name) { Transform t = parent.Find(name); return t != null ? t.GetComponent<Text>() : null; }

    private static Text FirstText(Transform root)
    {
        foreach (Text t in root.GetComponentsInChildren<Text>(true)) return t;
        return null;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    // 판 안 글자: 판 테두리 안쪽(높이 78%)에만 두고, 실제 글자 모양 기준으로 가운데 (맑은 고딕은 그냥 두면 숫자가 아래로 처짐)
    private const float PlateTextHeight = 0.78f, PlateTextLift = 0.02f;
    private static void PlaceInPlate(Text t, Vector2 corner, Vector2 platePos, float width, float plateHeight, int max)
    {
        float h = Mathf.Round(plateHeight * PlateTextHeight);
        float y = platePos.y - (plateHeight - h) * 0.5f + Mathf.Round(plateHeight * PlateTextLift);
        Place(t.rectTransform, corner, new Vector2(platePos.x, y), new Vector2(width, h));
        t.alignment = TextAnchor.MiddleCenter;
        t.alignByGeometry = true;   // 글꼴 여백 말고 실제 글자 모양 기준으로 가운데
        t.fontSize = max;           // 옛날 값(52 등)이 남아 있지 않게
        t.lineSpacing = 1f;
        BestFit(t, max);
    }

    private static void BestFit(Text t, int max)
    {
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = max;
        t.resizeTextForBestFit = true;
    }

    private static Outline AddOutline(Text t, float d)
    {
        Outline o = GetOrAdd<Outline>(t.gameObject);
        o.effectColor = theme.Outline;
        o.effectDistance = new Vector2(d, -d);
        return o;
    }

    private static void SetColor(SerializedObject so, string field, Color c)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null && p.propertyType == SerializedPropertyType.Color) p.colorValue = c;
    }

    private static bool Contains(string[] list, string name)
    {
        for (int i = 0; i < list.Length; i++) if (list[i] == name) return true;
        return false;
    }
}
#endif
