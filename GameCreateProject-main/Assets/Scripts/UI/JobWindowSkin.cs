using UnityEngine;
using UnityEngine.UI;

// 2·3·4차 전직 창을 도트 테마(나무/돌)로 꾸미는 도우미.
// 그림은 게임 씬 HUD의 PixelUISprites에 들어 있음 (Tools > Game UI > Pixel UI - Apply 가 채움).
// 그림이 없으면 아무것도 안 하고 false → 창은 예전 색 상자 모양 그대로.
public static class JobWindowSkin
{
    public const int Second = 0, Shadow = 1, Storm = 2, Fourth = 3;

    // 테두리 모서리 장식이 화면에서 차지할 크기 (1920x1080 기준 픽셀)
    public const float CornerOnScreen = 80f;

    public static readonly Color LabelColor = new Color(1f, 0.96f, 0.88f);
    public static readonly Color OutlineColor = new Color(0.1f, 0.06f, 0.04f, 1f);

    public static bool HasFrames => PixelUISprites.JobFrame(Second) != null;

    // 카드/창 테두리: 9칸 나누기로 어떤 크기에도 맞춤
    public static bool Frame(Image img, int kind)
    {
        Sprite s = PixelUISprites.JobFrame(kind);
        if (img == null || s == null) return false;
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.color = Color.white;
        float corner = Mathf.Max(1f, s.border.x);
        img.pixelsPerUnitMultiplier = corner / CornerOnScreen;
        return true;
    }

    // 테두리 안쪽에 내용이 들어갈 여백 (화면 픽셀)
    public static float Inset(int kind)
    {
        Sprite s = PixelUISprites.JobFrame(kind);
        return s == null ? 6f : CornerOnScreen * 0.55f;
    }

    // 버튼을 테마 버튼 그림으로 (초록 = 확인, 보통 = 나중에)
    public static bool SkinButton(Button b, bool green)
    {
        PixelUISprites px = PixelUISprites.Instance;
        if (b == null || px == null) return false;
        Sprite sprite = green && px.buttonGreen != null ? px.buttonGreen : px.buttonNormal;
        if (sprite == null) return false;
        Vector4 inset = sprite == px.buttonGreen ? px.buttonGreenInset : px.buttonNormalInset;

        Image hit = b.GetComponent<Image>(); // 원래 색 상자: 투명하게 두고 클릭 판정만
        if (hit != null) { hit.color = new Color(1f, 1f, 1f, 0f); hit.raycastTarget = true; }

        var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artGo.transform.SetParent(b.transform, false);
        artGo.transform.SetAsFirstSibling(); // 글자보다 뒤
        Image art = artGo.GetComponent<Image>();
        art.raycastTarget = false;
        b.targetGraphic = art;
        ColorBlock c = b.colors;
        c.normalColor = new Color(0.93f, 0.93f, 0.93f);
        c.highlightedColor = Color.white;
        c.selectedColor = new Color(0.93f, 0.93f, 0.93f);
        c.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        c.disabledColor = px.buttonDisabled != null ? Color.white : new Color(0.55f, 0.55f, 0.55f, 0.8f);
        b.colors = c;

        Text label = b.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = LabelColor;
            label.alignByGeometry = true;   // 글자 모양 기준으로 위아래 가운데
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform lr = label.rectTransform; // 글자 칸 = 버튼 칸 (판자 가운데)
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            Outline(label, 2.5f);
        }
        PixelButton pb = b.GetComponent<PixelButton>();
        if (pb == null) pb = b.gameObject.AddComponent<PixelButton>();
        pb.Set(art, sprite, inset, px.buttonDisabled, px.buttonDisabledInset, label);
        return true;
    }

    // 버튼 너비를 글자에 딱 맞춤 (양끝 장식 + 여백) 후, 여러 개를 화면 가운데 기준으로 나란히 놓음
    public static void FitRow(float y, float gap, params Button[] buttons)
    {
        PixelUISprites px = PixelUISprites.Instance;
        float total = 0f;
        var widths = new float[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            RectTransform r = (RectTransform)b.transform;
            widths[i] = r.sizeDelta.x;
            Text label = b.GetComponentInChildren<Text>(true);
            PixelButton pb = b.GetComponent<PixelButton>();
            if (px != null && pb != null && label != null)
            {
                Sprite s = b.targetGraphic is Image art && art.sprite != null ? art.sprite : px.buttonNormal;
                Vector4 inset = s == px.buttonGreen ? px.buttonGreenInset : s == px.buttonDisabled ? px.buttonDisabledInset : px.buttonNormalInset;
                float h = Mathf.Max(1f, r.sizeDelta.y);
                float m = Mathf.Max(1f, s.rect.height - inset.y - inset.w) / h;     // 그림 픽셀 → 화면 픽셀
                float capL = Mathf.Max(0f, s.border.x - inset.x) / m;               // 왼쪽 장식이 판자 안을 차지하는 폭
                float capR = Mathf.Max(0f, s.border.z - inset.z) / m;
                float textW = Mathf.Max(label.preferredWidth, label.fontSize * 3f); // 아주 짧은 글자(예)도 "나중에" 정도 폭은 유지
                widths[i] = Mathf.Ceil(textW + capL + capR + 28f);
                r.sizeDelta = new Vector2(widths[i], r.sizeDelta.y);
            }
            total += widths[i];
        }
        total += gap * Mathf.Max(0, buttons.Length - 1);
        float x = -total * 0.5f;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            RectTransform r = (RectTransform)buttons[i].transform;
            r.anchoredPosition = new Vector2(x + widths[i] * 0.5f, y);
            x += widths[i] + gap;
        }
    }

    // HUD의 "CHOOSE JOB" 버튼(도트 그림, 빛 깜빡임)을 그대로 복사해서 "2차/3차/4차 전직 하기" 버튼으로 씀.
    // 위치도 같은 자리(STAT 옆). HUD에 JobButton이 없으면 null → 예전 색 상자 버튼 그대로.
    public static Button CloneHudJobButton(string name, string label, UnityEngine.Events.UnityAction onClick, out Text text)
    {
        text = null;
        Button source = null;
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.name == "JobButton" && b.transform.parent != null && b.transform.parent.name == "HUD" && b.transform.Find("Art") != null) { source = b; break; }
        if (source == null) return null;

        GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = name;
        copy.SetActive(false);
        Button button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // 원래 버튼(1차 전직) 기능은 지움
        button.onClick.AddListener(onClick);
        button.interactable = true;
        foreach (Text t in copy.GetComponentsInChildren<Text>(true))
        {
            t.text = label;
            if (text == null) text = t;
        }
        return button;
    }

    public static void Outline(Text t, float distance)
    {
        if (t == null) return;
        Outline o = t.GetComponent<Outline>();
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = OutlineColor;
        o.effectDistance = new Vector2(distance, -distance);
    }

    // 받침 고르기: "궁수" + 로 / "전사" + 로 / "기사" + 로, 받침 있으면 "으로" (ㄹ 받침은 "로")
    public static string WithRo(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return word + "(으)로";
        int jong = (last - 0xAC00) % 28;
        return word + (jong == 0 || jong == 8 ? "로" : "으로");
    }
}
