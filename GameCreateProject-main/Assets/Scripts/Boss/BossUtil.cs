using System.Reflection;
using UnityEngine;

// 보스/몹 HP바용 도구.
// 팀 코드(EnemyHealth, PlayerHealth)를 고치지 않기 위해, 비공개 변수를 '읽기/쓰기만' 합니다(리플렉션).
// 팀원이 변수 이름(currentHealth 등)을 바꾸면 경고가 뜨고 해당 기능만 꺼집니다. (게임은 안 멈춤)
public static class BossUtil
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    // ───────── EnemyHealth (currentHealth, maxHealth) ─────────
    private static readonly FieldInfo enemyCurrent = typeof(EnemyHealth).GetField("currentHealth", Flags);
    private static readonly FieldInfo enemyMax = typeof(EnemyHealth).GetField("maxHealth", Flags);

    public static bool CanReadEnemyHp => enemyCurrent != null && enemyMax != null;

    public static bool TryGetHp(EnemyHealth enemy, out int current, out int max)
    {
        current = max = 0;
        if (enemy == null || !CanReadEnemyHp)
            return false;
        current = (int)enemyCurrent.GetValue(enemy);
        max = (int)enemyMax.GetValue(enemy);
        return true;
    }

    public static void SetHp(EnemyHealth enemy, int current, int max)
    {
        if (enemy == null || !CanReadEnemyHp)
            return;
        enemyMax.SetValue(enemy, max);
        enemyCurrent.SetValue(enemy, current);
    }

    // ───────── PlayerHealth 피해 (원래 적 접촉 피해와 같은 규칙: 무적 시간, 깜빡임, 사망) ─────────
    private static readonly FieldInfo pCurrent = typeof(PlayerHealth).GetField("currentHealth", Flags);
    private static readonly FieldInfo pNextDamage = typeof(PlayerHealth).GetField("nextDamageTime", Flags);
    private static readonly FieldInfo pInvincible = typeof(PlayerHealth).GetField("invincibleTime", Flags);
    private static readonly FieldInfo pBlinkStart = typeof(PlayerHealth).GetField("blinkStartTime", Flags);
    private static readonly FieldInfo pIsBlinking = typeof(PlayerHealth).GetField("isBlinking", Flags);
    private static readonly FieldInfo pBlinkAlpha = typeof(PlayerHealth).GetField("blinkAlpha", Flags);
    private static readonly MethodInfo pDie = typeof(PlayerHealth).GetMethod("Die", Flags);
    private static readonly MethodInfo pSetAlpha = typeof(PlayerHealth).GetMethod("SetAlphaMultiplier", Flags);
    private static bool warned;
    private static float lastBlockedLog;

    public static bool DamagePlayer(PlayerHealth player, int damage)
    {
        if (player == null || player.IsDead || damage <= 0 || Time.timeScale <= 0f)
            return false;
        if (pCurrent == null || pNextDamage == null || pDie == null)
        {
            if (!warned)
            {
                warned = true;
                Debug.LogWarning("[Boss] PlayerHealth 구조가 바뀌어 보스 공격이 플레이어에게 피해를 줄 수 없습니다.");
            }
            return false;
        }

        if (Time.time < (float)pNextDamage.GetValue(player))
        {
            // 확인용 기록 (0.5초에 한 번만)
            if (Time.unscaledTime - lastBlockedLog > 0.5f)
            {
                lastBlockedLog = Time.unscaledTime;
                Debug.Log($"[보스] 공격이 맞았지만 무적 시간이라 피해 없음 (피해 {damage} 무시됨)");
            }
            return false; // 무적 시간
        }

        int hp = Mathf.Max(0, (int)pCurrent.GetValue(player) - damage);
        Debug.Log($"[보스] 공격 명중: 플레이어에게 피해 {damage}");
        pCurrent.SetValue(player, hp);
        GameAudio.Instance?.PlayPlayerHit();

        if (hp == 0)
        {
            pDie.Invoke(player, null);
            return true;
        }

        float invincible = pInvincible != null ? (float)pInvincible.GetValue(player) : 1f;
        pNextDamage.SetValue(player, Time.time + Mathf.Max(0f, invincible));
        if (invincible > 0f && pBlinkStart != null && pIsBlinking != null)
        {
            pBlinkStart.SetValue(player, Time.time);
            pIsBlinking.SetValue(player, true);
            if (pSetAlpha != null && pBlinkAlpha != null)
                pSetAlpha.Invoke(player, new object[] { (float)pBlinkAlpha.GetValue(player) });
        }
        return true;
    }

    public static PlayerHealth Player()
    {
        GameSession session = GameSession.Instance;
        return session != null ? session.Player : Object.FindAnyObjectByType<PlayerHealth>();
    }

    // ───────── 임시 이미지 ─────────
    private static Sprite square;
    private static Sprite squareLeft;
    private static Sprite triangle;

    // 1x1칸 흰 사각형 (가운데 기준)
    public static Sprite Square()
    {
        if (square == null) square = MakeSquare(new Vector2(0.5f, 0.5f));
        return square;
    }

    // 1x1칸 흰 사각형 (왼쪽 기준 → 가로 크기만 바꾸면 왼쪽부터 채워짐)
    public static Sprite SquareLeft()
    {
        if (squareLeft == null) squareLeft = MakeSquare(new Vector2(0f, 0.5f));
        return squareLeft;
    }

    // 위를 향한 흰 삼각형 (화면 가장자리 화살표용)
    public static Sprite Triangle()
    {
        if (triangle != null)
            return triangle;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float t = (y + 0.5f) / size;
                float half = (1f - t) * size * 0.5f;
                float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - size * 0.5f) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        triangle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return triangle;
    }

    private static Sprite MakeSquare(Vector2 pivot)
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), pivot, 4f);
    }

    // 방향 → "북동쪽" 같은 이름
    public static string CompassName(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        string[] names = { "동쪽", "북동쪽", "북쪽", "북서쪽", "서쪽", "남서쪽", "남쪽", "남동쪽" };
        int index = Mathf.RoundToInt(((angle + 360f) % 360f) / 45f) % 8;
        return names[index];
    }
}
