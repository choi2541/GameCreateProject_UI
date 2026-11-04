using UnityEngine;

// DamageTracker가 각 적에게 자동으로 붙이는 작은 감지기.
// 한 방에 죽어서 바로 사라지는 적도, 사라지는 순간 체력을 확인해 피해를 알립니다.
// (단순 정리로 사라진 적은 체력이 그대로라 피해로 치지 않음)
public class DamageProbe : MonoBehaviour
{
    public int LastHp { get; set; }
    public bool Ready { get; set; }

    private EnemyHealth health;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void OnDestroy()
    {
        if (!Ready || health == null)
            return;
        if (DamageTracker.TryReadHp(health, out int hp) && hp < LastHp)
            DamageTracker.Report(health, LastHp - hp, transform.position);
    }
}
