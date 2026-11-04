using UnityEngine;

// [3차 그림자 패시브] 그림자 낙인
//  ① 공격이 적중할 때마다 일정 확률로 저주 표식 (기본 공격·스킬 모두)
//  ② 표식 붙은 적이 죽으면 가까운 적들에게 표식이 옮겨붙음 (라이즈 E 느낌)
// 3차 그림자 전직 시 켜짐. 적중 감지는 데미지 숫자용 DamageTracker를 사용합니다.
[DisallowMultipleComponent]
public class ShadowMarkPassive : MonoBehaviour, IThirdJobSkill
{
    [Header("적중 시 표식")]
    [Tooltip("적중할 때 표식이 붙을 확률 (0.2 = 20%)")]
    [SerializeField, Range(0f, 1f)] private float markChance = 0.2f;
    [SerializeField, Min(0.5f)] private float markSeconds = 6f;

    [Header("죽으면 퍼짐")]
    [SerializeField] private bool spreadOnDeath = true;
    [SerializeField, Min(0.5f)] private float spreadRadius = 2.5f;
    [SerializeField, Range(1, 8)] private int spreadCount = 3;

    public ArcherBranch Branch => ArcherBranch.Shadow;

    private void OnEnable()
    {
        DamageTracker.EnemyDamaged += OnEnemyDamaged;
        ShadowMark.SpreadOnDeath = spreadOnDeath;
        ShadowMark.DeathSpreadRadius = spreadRadius;
        ShadowMark.DeathSpreadCount = spreadCount;
        ShadowMark.DeathSpreadSeconds = markSeconds;
    }

    private void OnDisable()
    {
        DamageTracker.EnemyDamaged -= OnEnemyDamaged;
        ShadowMark.SpreadOnDeath = false;
    }

    private void OnEnemyDamaged(EnemyHealth enemy, int amount, Vector3 position)
    {
        if (enemy == null || amount <= 0 || ShadowMark.Has(enemy)) return;
        if (Random.value < markChance)
            ShadowMark.Apply(enemy, markSeconds);
    }
}
