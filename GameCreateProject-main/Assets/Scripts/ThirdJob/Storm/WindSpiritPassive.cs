using UnityEngine;

// [3차 폭풍] 바람 정령 : 공격하지 않고 늘 곁에 따라다님.
//   폭풍 스킬(Z·X·C·Ctrl+X·Ctrl+C)을 쓸 때만 가까운 적 쪽으로 휙 나가 바람을 일으키고 돌아옴 (보여주기용, 피해 없음)
//   모습 고르기 (Player → Wind Spirit Passive → Look)
//     Spirit = 지느러미 귀 정령 (폭풍 궁수 그림 속 정령을 도트로)  ← 기본
//     Orb    = 바람 구슬 (얼굴 없는 소용돌이 구슬)
//   그림: Resources/SkillFx/wind_creature, wind_orb (없으면 청록 동그라미)
[DisallowMultipleComponent]
public class WindSpiritPassive : MonoBehaviour, IThirdJobSkill
{
    public enum Look { Spirit, Orb }

    [Tooltip("정령 모습: Spirit = 정령 / Orb = 바람 구슬")]
    [SerializeField] private Look look = Look.Spirit;
    [Tooltip("화면에서의 크기 (칸)")]
    [SerializeField, Min(0.2f)] private float size = 0.85f;
    [Tooltip("스킬 쓸 때 앞으로 나가는 거리 (칸)")]
    [SerializeField, Min(0f)] private float dashOut = 1.6f;
    [Tooltip("스킬 때 적을 찾는 거리 (칸)")]
    [SerializeField, Min(1f)] private float range = 12f;
    [SerializeField] private Transform enemyContainer;

    public ArcherBranch Branch => ArcherBranch.Storm;

    private const float ActSeconds = 0.55f;

    private GameObject spirit;
    private SpriteRenderer view;
    private Look built;
    private Vector2 pos, lastPlayer;
    private bool facingLeft;
    private float actStart = -9f;
    private Vector2 actDir = Vector2.right;
    private bool burstDone;

    private void OnEnable()
    {
        Build();
        ArcherSkillBase.SkillCast += OnSkillCast;
    }

    private void OnDisable()
    {
        ArcherSkillBase.SkillCast -= OnSkillCast;
        if (spirit != null) Destroy(spirit);
    }

    private void Build()
    {
        if (spirit != null) Destroy(spirit);
        built = look;
        spirit = new GameObject("WindSpirit");
        view = spirit.AddComponent<SpriteRenderer>();
        view.sortingOrder = 59;
        string id = look == Look.Spirit ? "wind_creature" : "wind_orb";
        Sprite[] f = SkillFx.Frames(id);
        if (f != null)
        {
            spirit.AddComponent<SkillFxLoop>().Set(id, 8f, view);
            float s = size / Mathf.Max(0.01f, f[0].bounds.size.y);
            spirit.transform.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            view.sprite = SkillUtil.CircleSprite();
            view.color = new Color(0.7f, 1f, 0.9f, 0.9f);
            spirit.transform.localScale = Vector3.one * size * 0.4f;
        }
        lastPlayer = transform.position;
        pos = Home();
        spirit.transform.position = pos;
    }

    // 평소 자리: 플레이어 뒤쪽 어깨 높이, 살짝 둥실
    private Vector2 Home()
    {
        Vector2 p = transform.position;
        return p + new Vector2(facingLeft ? 0.6f : -0.6f, 0.9f + Mathf.Sin(Time.time * 2.4f) * 0.08f);
    }

    private void OnSkillCast(MonoBehaviour skill)
    {
        if (spirit == null || skill == null) return;
        bool storm = skill is IThirdJobSkill t3 ? t3.Branch == ArcherBranch.Storm
                   : skill is IFourthJobSkill t4 && t4.Branch == ArcherBranch.Storm;
        if (!storm) return;
        EnemyHealth target = SkillUtil.Nearest(SkillUtil.EnemyContainer(enemyContainer), transform.position, range);
        actDir = target != null ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
                                : (facingLeft ? Vector2.left : Vector2.right);
        actStart = Time.time;
        burstDone = false;
    }

    private void Update()
    {
        if (built != look) Build(); // 플레이 중에 Look을 바꿔도 바로 바뀜
        if (spirit == null) return;

        Vector2 player = transform.position;
        Vector2 moved = player - lastPlayer;
        lastPlayer = player;
        if (Mathf.Abs(moved.x) > 0.001f) facingLeft = moved.x < 0f;

        Vector2 home = Home();
        float k = (Time.time - actStart) / ActSeconds;
        if (k >= 0f && k < 1f)
        {
            // 스킬: 앞으로 나갔다가 돌아옴, 가장 멀리 나간 순간 바람이 터짐
            Vector2 front = player + Vector2.up * 0.6f + actDir * dashOut;
            pos = Vector2.Lerp(home, front, Mathf.Sin(k * Mathf.PI));
            if (!burstDone && k >= 0.45f)
            {
                burstDone = true;
                if (!SkillFx.Anim("gale_hit", pos, 1.2f, 0.28f, 0f, 62))
                    SkillFx.Burst(pos, 0.3f, 1.2f, new Color(0.7f, 1f, 0.9f, 0.6f), 0.25f, true);
            }
            view.flipX = actDir.x < 0f; // 그림은 오른쪽을 봄
        }
        else
        {
            pos = Vector2.Lerp(pos, home, 1f - Mathf.Exp(-6f * Time.deltaTime)); // 살짝 늦게 따라옴
            view.flipX = facingLeft;
        }
        spirit.transform.position = pos;
    }
}
