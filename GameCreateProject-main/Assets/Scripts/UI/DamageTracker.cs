using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// 피해/회복 감지기. 팀 코드(EnemyHealth, PlayerHealth)를 고치지 않고, 매 프레임 체력 변화를 지켜봅니다.
//   EnemyDamaged(적, 피해량, 위치)   : 적의 체력이 줄었을 때 (평타·스킬·보스 모두)
//   PlayerHealthChanged(변화량, 위치) : 플레이어 체력이 바뀌었을 때 (- 피해 / + 회복)
// 데미지 숫자, 흡혈 증강 등이 이 이벤트를 사용합니다.
[DisallowMultipleComponent]
[DefaultExecutionOrder(32000)] // 다른 스크립트의 Update(공격)가 끝난 뒤에 확인
public class DamageTracker : MonoBehaviour
{
    public static event Action<EnemyHealth, int, Vector3> EnemyDamaged;
    public static event Action<int, Vector3> PlayerHealthChanged;

    private static readonly FieldInfo enemyHp =
        typeof(EnemyHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private readonly Dictionary<EnemyHealth, DamageProbe> probes = new Dictionary<EnemyHealth, DamageProbe>();
    private readonly List<EnemyHealth> stale = new List<EnemyHealth>();
    private readonly HashSet<EnemyHealth> seen = new HashSet<EnemyHealth>();
    private int lastPlayerHp = int.MinValue;

    // ───────── 자동 설치 ─────────
    // 게임 씬(GameSession이 있는 씬)에 DamageTracker가 없으면 자동으로 만들어 붙입니다.
    // → Build Gameplay UI를 다시 실행하지 않아도 데미지 숫자/흡혈이 동작
    // (화면 표시만 함. 흡혈은 증강을 가졌을 때만 동작하고, 지금은 증강이 꺼져 있어서 영향 없음)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureInstalled();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstalled();

    private static void EnsureInstalled()
    {
        if (FindAnyObjectByType<GameSession>() == null)
            return; // 메인 메뉴 등 게임 씬이 아니면 무시
        if (FindAnyObjectByType<DamageTracker>() != null)
            return;

        GameObject obj = new GameObject("CombatFeedback (auto)");
        obj.AddComponent<DamageTracker>();
        if (FindAnyObjectByType<DamageNumbers>() == null)
            obj.AddComponent<DamageNumbers>();
        Debug.Log("[DamageTracker] 데미지 숫자/흡혈용 오브젝트를 자동으로 추가했습니다.");
    }

    public static bool TryReadHp(EnemyHealth enemy, out int hp)
    {
        hp = 0;
        if (enemyHp == null || enemy == null)
            return false;
        hp = (int)enemyHp.GetValue(enemy);
        return true;
    }

    public static void Report(EnemyHealth enemy, int amount, Vector3 position)
    {
        if (amount > 0)
            EnemyDamaged?.Invoke(enemy, amount, position);
    }

    private void Start()
    {
        if (enemyHp == null)
            Debug.LogWarning("[DamageTracker] EnemyHealth 구조가 바뀌어 피해를 감지할 수 없습니다.");
    }

    // 공격(Update)이 끝난 직후 + 코루틴 공격이 끝난 뒤(LateUpdate) 두 번 확인
    private void Update()
    {
        TrackEnemies();
    }

    private void LateUpdate()
    {
        TrackEnemies();
        TrackPlayer();
    }

    private void TrackEnemies()
    {
        if (enemyHp == null)
            return;

        seen.Clear();
        foreach (EnemyHealth enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            int hp = (int)enemyHp.GetValue(enemy);
            seen.Add(enemy);

            if (!probes.TryGetValue(enemy, out DamageProbe probe) || probe == null)
            {
                // 처음 본 적: 감지기를 붙이고 현재 체력 기록
                probe = enemy.GetComponent<DamageProbe>();
                if (probe == null) probe = enemy.gameObject.AddComponent<DamageProbe>();
                probe.LastHp = hp;
                probe.Ready = true;
                probes[enemy] = probe;
                continue;
            }

            if (hp < probe.LastHp)
                Report(enemy, probe.LastHp - hp, enemy.transform.position);
            probe.LastHp = hp; // 늘어난 경우(보스 내부 처리 등)는 기록만
        }

        stale.Clear();
        foreach (EnemyHealth key in probes.Keys)
        {
            if (key == null || !seen.Contains(key))
                stale.Add(key);
        }
        foreach (EnemyHealth key in stale)
            probes.Remove(key);
    }

    private void TrackPlayer()
    {
        GameSession session = GameSession.Instance;
        PlayerHealth player = session != null ? session.Player : null;
        if (player == null)
            return;

        int hp = player.CurrentHealth;
        if (lastPlayerHp != int.MinValue && hp != lastPlayerHp)
            PlayerHealthChanged?.Invoke(hp - lastPlayerHp, player.transform.position);
        lastPlayerHp = hp;
    }

    // ───────── 플레이어 회복 (흡혈 등). PlayerHealth를 고치지 않기 위해 변수를 직접 씁니다. ─────────
    private static readonly FieldInfo playerHp =
        typeof(PlayerHealth).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool HealPlayer(int amount)
    {
        GameSession session = GameSession.Instance;
        PlayerHealth player = session != null ? session.Player : null;
        if (player == null || player.IsDead || amount <= 0 || playerHp == null)
            return false;

        int hp = player.CurrentHealth;
        int healed = Mathf.Min(player.MaxHealth, hp + amount);
        if (healed == hp)
            return false;
        playerHp.SetValue(player, healed);
        return true;
    }
}
