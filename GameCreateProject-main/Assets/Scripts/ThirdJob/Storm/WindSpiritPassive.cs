using UnityEngine;

// [3차 폭풍 패시브] 바람 정령 : 플레이어 주변을 도는 정령이 일정 간격으로 가까운 적에게 바람 화살을 쏨
[DisallowMultipleComponent]
public class WindSpiritPassive : MonoBehaviour, IThirdJobSkill
{
    [SerializeField, Min(0.1f)] private float interval = 1.2f;
    [SerializeField, Min(1)] private int damage = 5;
    [SerializeField, Min(1f)] private float range = 8f;
    [SerializeField] private Sprite spiritSprite;
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Transform enemyContainer;

    public ArcherBranch Branch => ArcherBranch.Storm;

    private GameObject spirit;
    private float next;

    private void OnEnable()
    {
        spirit = new GameObject("WindSpirit");
        SpriteRenderer sr = spirit.AddComponent<SpriteRenderer>();
        sr.sprite = spiritSprite != null ? spiritSprite : SkillUtil.CircleSprite();
        sr.color = spiritSprite != null ? Color.white : new Color(0.7f, 1f, 0.9f, 0.9f);
        sr.sortingOrder = 59;
        spirit.transform.localScale = Vector3.one * (spiritSprite != null ? 1f : 0.35f);
        next = Time.time + interval;
    }

    private void OnDisable()
    {
        if (spirit != null) Destroy(spirit);
    }

    private void Update()
    {
        if (spirit == null) return;
        float a = Time.time * 2.2f;
        Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * 0.8f, 0.6f + Mathf.Sin(a * 2f) * 0.15f);
        spirit.transform.position = pos;

        if (Time.timeScale <= 0f || Time.time < next) return;
        next = Time.time + interval;
        Transform enemies = SkillUtil.EnemyContainer(enemyContainer);
        EnemyHealth target = SkillUtil.Nearest(enemies, pos, range);
        if (target == null) return;
        Vector2 dir = ((Vector2)target.transform.position - pos).normalized;
        SkillProjectile.Spawn(arrowSprite, JobTierUtil.StormColor, pos, dir, 18f, range + 2f,
            JobTierUtil.Damage(damage, ArcherBranch.Storm), 0.22f, false, false, enemies);
    }
}
