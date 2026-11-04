using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [4차 폭풍 어센트 · Ctrl+X] 뇌령의 화살
// 번개 정령이 실린 화살이 가장 가까운 적에게 날아가 맞는 순간,
// 화면 안의 모든 적에게 전격이 연쇄로 튐 → 잡몹 즉사 / 보스는 큰 피해 + 기절
// 연쇄는 한 마리씩 차례로 튐. 적이 아주 많을 때만 maxChainSeconds 안에 끝나도록 한 번에 여러 줄기가 동시에 튐
// (모든 적에게 번개 줄기 + 흰 심지 + 착탄 고리가 그대로 그려짐)
[DisallowMultipleComponent]
public class ThunderSpiritArrow : FourthJobSkill
{
    [Header("뇌령의 화살")]
    [SerializeField, Min(1f)] private float arrowSpeed = 22f;
    [Tooltip("번개가 다음 적으로 튀는 간격(초)")]
    [SerializeField, Min(0.01f)] private float hopDelay = 0.04f;
    [Tooltip("연쇄 전체가 이 시간 안에 끝나도록 함 (적이 많으면 여러 줄기가 동시에 튐)")]
    [SerializeField, Min(0.2f)] private float maxChainSeconds = 1.6f;
    [SerializeField] private bool instantKillMobs = true;
    [SerializeField, Min(1)] private int mobDamage = 300;
    [SerializeField, Min(1)] private int bossDamage = 400;
    [SerializeField] private bool instantKillBoss = false;
    [SerializeField, Min(0f)] private float bossStun = 1f;

    [Header("이펙트 그림 크기 (Resources/SkillFx/)")]
    [Tooltip("번개 화살 길이(월드 단위)")]
    [SerializeField, Min(0.2f)] private float arrowLength = 1.6f;
    [Tooltip("첫 명중 폭발 크기")]
    [SerializeField, Min(0.2f)] private float hitSize = 4.5f;
    [Tooltip("연쇄로 맞은 적마다 나오는 전격 크기")]
    [SerializeField, Min(0.2f)] private float zapSize = 1.7f;

    private const string ArrowFx = "thunder_arrow", HitFx = "thunder_hit", ZapFx = "thunder_zap";

    public override ArcherBranch Branch => ArcherBranch.Storm;
    public override string DisplayName => "어센트 · 뇌령의 화살";
    public override string Description => "번개 정령이 실린 화살이 적에게 꽂히면\n화면 안의 모든 적에게 전격이 연쇄로 튐";

    private readonly List<EnemyHealth> order = new List<EnemyHealth>(128);
    private readonly List<Vector2> fromPoints = new List<Vector2>(128);
    private GameObject arrow;
    private SpriteRenderer arrowRenderer;

    private void Reset() { key = KeyCode.X; maxCharges = 3; chargeSeconds = 20f; cutInId = "storm_ascent"; }

    protected override IEnumerator Execute()
    {
        List<EnemyHealth> onScreen = OnScreenNow(Origin);
        if (onScreen.Count == 0)
        {
            SkillFx.Burst(Origin, 0.5f, 3f, JobTierUtil.BoltColor, 0.35f, true);
            yield break;
        }

        // 1) 화살이 첫 적에게 날아감 (화살 오브젝트는 재사용)
        EnemyHealth first = onScreen[0];
        if (arrow == null)
        {
            arrow = new GameObject("ThunderArrow");
            arrowRenderer = arrow.AddComponent<SpriteRenderer>();
            arrowRenderer.sortingOrder = 63;
            arrow.transform.localScale = Vector3.one * 1.8f;
        }
        bool arrowFrames = SkillFx.Frames(ArrowFx) != null;
        arrowRenderer.sprite = arrowFrames ? SkillFx.LoopFrame(ArrowFx, 0f, 14f)
            : arrowSprite != null ? arrowSprite : SkillUtil.ArrowSprite();
        arrowRenderer.color = arrowFrames ? Color.white : JobTierUtil.BoltColor;
        arrow.transform.localScale = Vector3.one * (arrowFrames ? arrowLength / arrowRenderer.sprite.bounds.size.x : 1.8f);
        arrow.SetActive(true);

        Vector2 pos = Origin;
        Vector2 to = first.transform.position;
        for (float t = 0f; t < 3f; t += Time.deltaTime)
        {
            if (first != null && first.isActiveAndEnabled) to = first.transform.position;
            Vector2 dir = (to - pos).sqrMagnitude > 0.0001f ? (to - pos).normalized : Vector2.right;
            pos = Vector2.MoveTowards(pos, to, arrowSpeed * Time.deltaTime);
            arrow.transform.position = pos;
            arrow.transform.rotation = SkillUtil.RotationFor(dir);
            if (arrowFrames) arrowRenderer.sprite = SkillFx.LoopFrame(ArrowFx, t, 14f);
            if (Random.value < 0.6f) // 꼬리 번개
                JobTierUtil.FlashLine(pos, pos - dir * 0.9f + Random.insideUnitCircle * 0.2f, JobTierUtil.BoltColor, 0.06f, 0.12f, true, 0.12f);
            if (Vector2.Distance(pos, to) < 0.2f) break;
            yield return null;
        }
        arrow.SetActive(false);

        // 2) 명중 폭발 (그림이 있으면 그림, 없으면 원)
        PlayImpact();
        if (!SkillFx.Anim(HitFx, pos, hitSize, 0.5f, 0f, 64))
        {
            SkillFx.Burst(pos, 0.5f, 3.2f, new Color(1f, 0.95f, 0.6f, 0.9f), 0.3f);
            SkillFx.Burst(pos, 0.5f, 4f, JobTierUtil.BoltColor, 0.35f, true);
        }

        // 3) 연쇄 순서를 한 번에 계산 (가까운 적으로 이어감)
        BuildChain(OnScreenNow(pos), pos);

        // 4) 한 마리씩 차례로 피해 + 번개 (적이 많으면 한 번에 여러 마리)
        int total = order.Count;
        int maxSteps = Mathf.Max(1, Mathf.FloorToInt(maxChainSeconds / hopDelay));
        int perStep = Mathf.Max(1, Mathf.CeilToInt(total / (float)maxSteps));
        for (int start = 0; start < total; start += perStep)
        {
            int end = Mathf.Min(total, start + perStep);
            for (int i = start; i < end; i++)
            {
                EnemyHealth e = order[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                Vector2 at = e.transform.position;
                JobTierUtil.FlashLine(fromPoints[i], at, JobTierUtil.BoltColor, 0.16f, 0.35f, true, 0.3f);
                JobTierUtil.FlashLine(fromPoints[i], at, Color.white, 0.05f, 0.25f, true, 0.3f);
                if (!SkillFx.Anim(ZapFx, at, zapSize, 0.32f, Random.Range(0f, 360f), 62))
                    SkillFx.Burst(at, 0.3f, 1.2f, JobTierUtil.BoltColor, 0.25f, true);
                if (JobTierUtil.IsBoss(e))
                {
                    JobTierUtil.Hit(e, instantKillBoss ? 999999 : Dmg(bossDamage));
                    SkillStun.Apply(e, bossStun);
                }
                else JobTierUtil.Hit(e, instantKillMobs ? 999999 : Dmg(mobDamage));
            }
            yield return new WaitForSeconds(hopDelay);
        }
        order.Clear();
        fromPoints.Clear();
    }

    // 가장 가까운 적부터 이어지는 순서 (한 번만 계산. 무한 반복 없음)
    private void BuildChain(List<EnemyHealth> candidates, Vector2 start)
    {
        order.Clear();
        fromPoints.Clear();
        order.AddRange(candidates);
        Vector2 from = start;
        for (int i = 0; i < order.Count; i++)
        {
            int best = i; float bestD = float.MaxValue;
            for (int j = i; j < order.Count; j++)
            {
                float d = ((Vector2)order[j].transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = j; }
            }
            EnemyHealth tmp = order[i]; order[i] = order[best]; order[best] = tmp;
            fromPoints.Add(from);
            from = order[i].transform.position;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (arrow != null) arrow.SetActive(false);
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow);
    }
}
