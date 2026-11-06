using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [4차 그림자 어센트 · Ctrl+X] 그림자 추적 화살
// 화살 한 발이 가장 가까운 적을 추적해 꿰뚫고, 다음 적으로 휘어져 날아가며
// 화면 안의 적을 하나씩 전부 관통한 뒤 화면 밖으로 사라짐. 관통당한 적은 큰 피해.
// 메모리: 화살·꼬리 선·목록을 재사용하고, 매 프레임 새 목록을 만들지 않음
[DisallowMultipleComponent]
public class ShadowHomingArrow : FourthJobSkill
{
    [Header("그림자 추적 화살")]
    [SerializeField, Min(1f)] private float speed = 16f;
    [Tooltip("1초에 꺾을 수 있는 각도")]
    [SerializeField, Min(30f)] private float turnRate = 620f;
    [SerializeField, Min(1)] private int damage = 150;
    [Tooltip("최대 관통 수 (0 = 화면 안 전부)")]
    [SerializeField, Min(0)] private int maxPierce = 0;
    [SerializeField, Min(1f)] private float maxSeconds = 8f;

    public override ArcherBranch Branch => ArcherBranch.Shadow;
    public override string DisplayName => "어센트 · 그림자 추적 화살";
    public override string Description => "추적 화살이 화면 안의 적을\n하나씩 전부 꿰뚫고 사라짐 (관통마다 큰 피해)";

    private const int TrailLength = 18;
    private readonly HashSet<EnemyHealth> hit = new HashSet<EnemyHealth>();
    private readonly Vector3[] trailPoints = new Vector3[TrailLength];
    private GameObject arrow;
    private SpriteRenderer arrowRenderer;
    private LineRenderer trail;

    private void Reset() { key = KeyCode.X; maxCharges = 3; chargeSeconds = 20f; cutInId = "shadow_ascent"; }

    protected override IEnumerator Execute()
    {
        EnsureObjects();
        arrow.SetActive(true);
        trail.gameObject.SetActive(true);
        hit.Clear();

        Vector2 pos = Origin;
        EnemyHealth first = SkillUtil.Nearest(Enemies, pos, 50f);
        Vector2 dir = first != null ? ((Vector2)first.transform.position - pos).normalized : Vector2.right;
        float life = 0f;
        bool leaving = false;
        int trailCount = 0;

        while (life < maxSeconds)
        {
            float dt = Time.deltaTime;
            life += dt;

            // 이번 프레임 적 목록 한 번만 훑으면서: 다음 목표 찾기 + 맞았는지 검사
            List<EnemyHealth> all = SkillUtil.EnemiesNow(Enemies);
            EnemyHealth target = null;
            float best = float.MaxValue;
            bool canPierce = !leaving && (maxPierce == 0 || hit.Count < maxPierce);
            float hitRange = 0.3f + SkillUtil.EnemyRadius;
            for (int i = 0; i < all.Count; i++)
            {
                EnemyHealth e = all[i];
                if (e == null || !e.isActiveAndEnabled || hit.Contains(e)) continue;
                Vector2 ep = e.transform.position;
                float d = Vector2.Distance(pos, ep);

                if (d <= hitRange)
                {
                    hit.Add(e);
                    JobTierUtil.Hit(e, Dmg(damage, e));
                    PlayImpact(0.08f);
                    SkillFx.Burst(ep, 0.4f, 1.8f, new Color(0.95f, 0.85f, 1f, 0.9f), 0.25f, true);
                    SkillFx.Burst(ep, 0.3f, 1.2f, new Color(0.35f, 0.08f, 0.5f, 0.7f), 0.3f);
                    continue;
                }
                if (canPierce && d < best && SkillUtil.IsInsideCamera(ep, 0.2f)) { best = d; target = e; }
            }
            if (target == null) leaving = true;

            if (target != null)
            {
                Vector2 want = ((Vector2)target.transform.position - pos).normalized;
                float angle = Vector2.SignedAngle(dir, want);
                dir = SkillUtil.Rotate(dir, Mathf.Clamp(angle, -turnRate * dt, turnRate * dt));
            }
            pos += dir * speed * dt;
            arrow.transform.position = pos;
            arrow.transform.rotation = SkillUtil.RotationFor(dir);

            // 꼬리: 고정 크기 배열을 한 칸씩 밀어서 재사용
            if (trailCount < TrailLength) trailCount++;
            for (int i = trailCount - 1; i > 0; i--) trailPoints[i] = trailPoints[i - 1];
            trailPoints[0] = pos;
            trail.positionCount = trailCount;
            trail.SetPositions(trailPoints);

            if (leaving && !SkillUtil.IsInsideCamera(pos, 1f)) break;
            yield return null;
        }
        Hide();
    }

    private void EnsureObjects()
    {
        if (arrow == null)
        {
            arrow = new GameObject("ShadowHomingArrow");
            arrowRenderer = arrow.AddComponent<SpriteRenderer>();
            arrowRenderer.sortingOrder = 63;
            arrow.transform.localScale = Vector3.one * 1.8f;
        }
        arrowRenderer.sprite = arrowSprite != null ? arrowSprite : SkillUtil.ArrowSprite();
        arrow.transform.localScale = Vector3.one * SkillUtil.ArrowScale(arrowRenderer.sprite, 1.8f);
        Sprite[] hf = SkillFx.Frames("homing_arrow"); // 보라 추적 화살 그림
        if (hf != null)
        {
            if (arrow.GetComponent<SkillFxLoop>() == null) arrow.AddComponent<SkillFxLoop>().Set("homing_arrow", 12f, arrowRenderer);
            arrow.transform.localScale = Vector3.one * SkillUtil.ArrowScale(hf[0], 2.2f);
        }
        arrowRenderer.color = new Color(0.9f, 0.7f, 1f);
        if (trail == null)
        {
            trail = JobTierUtil.NewLine("ShadowTrail", new Color(0.6f, 0.25f, 0.9f, 0.85f), 0.18f, 62);
            trail.endColor = new Color(0.3f, 0.05f, 0.45f, 0f);
        }
        trail.positionCount = 0;
    }

    private void Hide()
    {
        hit.Clear();
        if (arrow != null) arrow.SetActive(false);
        if (trail != null) { trail.positionCount = 0; trail.gameObject.SetActive(false); }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Hide();
    }

    private void OnDestroy()
    {
        if (arrow != null) Destroy(arrow);
        if (trail != null) Destroy(trail.gameObject);
    }
}
