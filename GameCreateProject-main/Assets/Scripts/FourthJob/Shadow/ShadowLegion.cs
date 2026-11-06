using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [4차 그림자 오리진 · Ctrl+C] 그림자 군단
//  1) 속박  : 화면 안의 적이 전부 그 자리에 묶임 (표식 단계에서 먼저 죽지 않도록)
//  2) 소환  : 플레이어 주위에 분신이 빙 둘러 나타나고, 적마다 바로 옆에도 분신이 나타남
//  3) 일제 사격 : 분신들이 표식 화살을 쏨 (피해 없음, 표식만 새김)
//  4) 그물  : 표식끼리 보라색 선으로 이어짐
//  5) 대폭발 : 표식이 한꺼번에 터짐
// 메모리: 분신·연결선·화살을 지우지 않고 재사용 (화면 모습은 줄이지 않음)
[DisallowMultipleComponent]
public class ShadowLegion : FourthJobSkill
{
    [Header("분신")]
    [Tooltip("플레이어 주위에 둘러서는 분신 수")]
    [SerializeField, Range(4, 24)] private int ringCount = 12;
    [SerializeField, Min(0.5f)] private float ringRadius = 2.2f;
    [Tooltip("적 옆에 나타나는 분신 최대 수 (0 = 화면 안 모든 적 옆에). 가까운 적부터")]
    [SerializeField, Min(0)] private int maxEnemyClones = 0;
    [Tooltip("적과 분신 사이 거리")]
    [SerializeField, Min(0.3f)] private float enemyCloneDistance = 1.3f;
    [Tooltip("분신이 다 나타나는 데 걸리는 시간(초)")]
    [SerializeField, Min(0.05f)] private float summonSeconds = 0.45f;

    [Header("표식 사격 (피해 없음)")]
    [SerializeField, Min(0.05f)] private float arrowFlightSeconds = 0.2f;

    [Header("그물")]
    [Tooltip("표식끼리 이어지는 최대 거리")]
    [SerializeField, Min(0.5f)] private float linkDistance = 5f;
    [Tooltip("표식 하나가 이어지는 가까운 표식 수 (클수록 촘촘한 그물)")]
    [SerializeField, Range(1, 6)] private int linksPerMark = 3;
    [SerializeField, Min(0.1f)] private float netSeconds = 0.6f;

    [Header("대폭발")]
    [SerializeField] private bool blastKillsMobs = true;
    [SerializeField, Min(1)] private int blastDamage = 220;
    [SerializeField, Min(1)] private int bossBlastDamage = 600;

    public override ArcherBranch Branch => ArcherBranch.Shadow;
    public override string DisplayName => "오리진 · 그림자 군단";
    public override string Description => "적을 묶고 주위·적 곁에 분신 군단을 불러\n표식 화살 → 표식 그물 → 한꺼번에 대폭발";

    private static readonly Color CloneColor = new Color(0.22f, 0.05f, 0.36f, 0.9f);
    private static readonly Color MarkArrowColor = new Color(0.85f, 0.6f, 1f);
    private static readonly Color LinkColor = new Color(0.85f, 0.55f, 1f, 1f);

    private readonly List<EnemyHealth> targets = new List<EnemyHealth>(128);
    private readonly List<SpriteRenderer> clones = new List<SpriteRenderer>(48);   // 재사용
    private readonly List<LineRenderer> links = new List<LineRenderer>(48);        // 재사용
    private readonly List<int> linkA = new List<int>(64), linkB = new List<int>(64);
    private Sprite[] linkFrames;
    private Material linkMaterial;
    private readonly List<Vector2> ringPositions = new List<Vector2>(24);
    private Transform cloneRoot;
    private int usedClones, usedLinks;

    private void Reset() { key = KeyCode.C; maxCharges = 1; chargeSeconds = 90f; cutInId = "shadow_origin"; }

    protected override IEnumerator Execute()
    {
        Vector2 center = Origin;
        playerSprite = GetComponentInChildren<SpriteRenderer>();
        targets.Clear();
        targets.AddRange(OnScreenNow(center)); // 가까운 순서

        // 1) 속박: 폭발 직전까지 움직이지 못함
        float bindSeconds = summonSeconds + arrowFlightSeconds + netSeconds + 0.4f;
        for (int i = 0; i < targets.Count; i++)
        {
            SkillStun.Apply(targets[i], bindSeconds);
            // 사슬 속박 그림 (발밑에서 조여듦, 폭발 직전까지)
            SpriteRenderer chain = targets[i] != null ? SkillFx.Loop("legion_bind", targets[i].transform, 1.5f, 8f, 41) : null;
            if (chain != null) Destroy(chain.gameObject, bindSeconds);
        }

        // 2) 소환: 주위에 빙 둘러서 + 적 옆에
        usedClones = 0;
        ringPositions.Clear();
        int enemyClones = maxEnemyClones <= 0 ? targets.Count : Mathf.Min(maxEnemyClones, targets.Count);
        int totalClones = ringCount + enemyClones;
        float perClone = summonSeconds / Mathf.Max(1, totalClones);
        float waited = 0f;

        for (int i = 0; i < ringCount; i++)
        {
            float a = i / (float)ringCount * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * ringRadius;
            ringPositions.Add(p);
            ShowClone(p, p.x < center.x);
            waited += perClone;
            if (waited >= 0.02f) { waited = 0f; yield return new WaitForSeconds(0.02f); }
        }
        for (int i = 0; i < enemyClones; i++)
        {
            EnemyHealth e = targets[i];
            if (e == null) { ShowClone(center, false); continue; } // 순서를 맞추려고 자리만 채움
            Vector2 ep = e.transform.position;
            Vector2 toPlayer = center - ep;
            Vector2 side = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Random.insideUnitCircle.normalized;
            side = SkillUtil.Rotate(side, Random.Range(-50f, 50f));
            Vector2 p = ep + side * enemyCloneDistance;
            ShowClone(p, ep.x < p.x);
            waited += perClone;
            if (waited >= 0.02f) { waited = 0f; yield return new WaitForSeconds(0.02f); }
        }
        SkillFx.Burst(center, 0.5f, ringRadius * 2.6f, JobTierUtil.ShadowColor, 0.3f, true);
        yield return new WaitForSeconds(0.12f);

        // 3) 일제 사격 (표식만)
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth e = targets[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            Vector2 at = e.transform.position;
            Vector2 src = i < enemyClones ? (Vector2)clones[ringCount + i].transform.position : NearestRing(at);
            if (!SkillFx.FlyFrames("legion_arrow", src, at, arrowFlightSeconds, 1.6f))
                SkillFx.Fly(arrowSprite, src, at, MarkArrowColor, arrowFlightSeconds, 1.2f);
        }
        yield return new WaitForSeconds(arrowFlightSeconds);
        float markSeconds = netSeconds + 1f;
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth e = targets[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            ShadowMark.Apply(e, markSeconds);
        }

        // 4) 그물: 각 표식을 가장 가까운 표식과 연결 (선 수 제한)
        BuildLinks();
        for (float t = 0f; t < netSeconds; t += Time.deltaTime)
        {
            float alpha = Mathf.Clamp01(t / (netSeconds * 0.5f)) * 0.9f;
            if (linkMaterial != null) linkMaterial.mainTexture = linkFrames[Mathf.FloorToInt(t * 12f) % linkFrames.Length].texture;
            for (int i = 0; i < usedLinks; i++)
            {
                EnemyHealth a = targets[linkA[i]], b = targets[linkB[i]];
                LineRenderer l = links[i];
                if (a == null || b == null) { l.positionCount = 0; continue; }
                JobTierUtil.SetStraight(l, a.transform.position, b.transform.position);
                Color c = linkMaterial != null ? Color.white : LinkColor; c.a = linkMaterial != null ? Mathf.Min(1f, alpha / 0.9f) : alpha;
                l.startColor = l.endColor = c;
            }
            for (int i = 0; i < usedClones; i++) // 분신이 기를 모으듯 살짝 커짐
                clones[i].transform.localScale = cloneScale * (1f + 0.15f * (t / netSeconds));
            yield return null;
        }
        HideLinks();

        // 5) 대폭발
        PlayImpact();
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth e = targets[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            Vector2 at = e.transform.position;
            bool boss = JobTierUtil.IsBoss(e);
            int dmg = boss ? Dmg(bossBlastDamage, e) : (blastKillsMobs ? 999999 : Dmg(blastDamage, e));
            ShadowMark.Consume(e);
            JobTierUtil.Hit(e, dmg);
            if (!SkillFx.Anim("legion_blast", at, 3f, 0.55f, 0f, 63))
            {
                SkillFx.Burst(at, 0.4f, 2.6f, new Color(0.95f, 0.85f, 1f, 0.95f), 0.35f);
                SkillFx.Burst(at, 0.4f, 3f, JobTierUtil.ShadowColor, 0.4f, true);
            }
        }
        SkillFx.Burst(center, 0.5f, ringRadius * 4f, new Color(0.6f, 0.3f, 0.9f, 0.7f), 0.45f, true);

        // 분신 사라짐 (그림 분신은 연기로 흩어짐)
        for (int i = 0; i < usedClones; i++) CloneLook.Dissolve(clones[i], 0.4f);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.4f;
            for (int i = 0; i < usedClones; i++)
            {
                if (CloneLook.IsAnimated(clones[i])) continue;
                Color c = CloneColor; c.a *= k;
                clones[i].color = c;
            }
            yield return null;
        }
        HideClones();
        targets.Clear();
    }

    // ───────── 분신 (재사용) ─────────
    private Vector3 cloneScale = Vector3.one * 0.7f;
    private SpriteRenderer playerSprite;

    private void ShowClone(Vector2 position, bool faceLeft)
    {
        if (cloneRoot == null) cloneRoot = new GameObject("[ShadowLegion]").transform;
        SpriteRenderer source = playerSprite;
        SpriteRenderer sr;
        if (usedClones < clones.Count) sr = clones[usedClones];
        else
        {
            GameObject obj = new GameObject("LegionClone");
            obj.transform.SetParent(cloneRoot, false);
            sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 40;
            clones.Add(sr);
        }
        usedClones++;

        if (CloneLook.Apply(sr, faceLeft, out Vector3 framedScale))
        {
            cloneScale = framedScale; // 분신 그림: 연기에서 나타나 활을 쏨
            sr.transform.position = position;
            sr.gameObject.SetActive(true);
            if (!SkillFx.Anim("shadow_step", position, 1.6f, 0.3f, 0f, 61))
                SkillFx.Burst(position, 0.3f, 1.3f, new Color(0.3f, 0.08f, 0.45f, 0.7f), 0.25f);
            return;
        }
        CloneLook old = sr.GetComponent<CloneLook>();
        if (old != null) old.enabled = false;
        if (source != null && source.sprite != null)
        {
            sr.sprite = source.sprite;
            cloneScale = source.transform.lossyScale;
        }
        else
        {
            sr.sprite = SkillUtil.CircleSprite();
            cloneScale = Vector3.one * 0.7f;
        }
        sr.flipX = faceLeft;
        sr.color = CloneColor;
        sr.transform.position = position;
        sr.transform.localScale = cloneScale;
        sr.gameObject.SetActive(true);
        if (!SkillFx.Anim("shadow_step", position, 1.6f, 0.3f, 0f, 61))
            SkillFx.Burst(position, 0.3f, 1.3f, new Color(0.3f, 0.08f, 0.45f, 0.7f), 0.25f);
    }

    private Vector2 NearestRing(Vector2 at)
    {
        Vector2 best = Origin; float bestD = float.MaxValue;
        for (int i = 0; i < ringPositions.Count; i++)
        {
            float d = (ringPositions[i] - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = ringPositions[i]; }
        }
        return best;
    }

    private void HideClones()
    {
        for (int i = 0; i < clones.Count; i++)
            if (clones[i] != null) clones[i].gameObject.SetActive(false);
        usedClones = 0;
    }

    // ───────── 그물 선 (재사용) ─────────
    private readonly HashSet<long> linkKeys = new HashSet<long>();
    private readonly int[] nearIndex = new int[6];
    private readonly float[] nearDist = new float[6];

    // 각 표식을 가까운 표식 linksPerMark개와 이어서 촘촘한 그물을 만듦 (같은 선은 한 번만)
    private void BuildLinks()
    {
        linkA.Clear(); linkB.Clear(); linkKeys.Clear();
        float maxSqr = linkDistance * linkDistance;
        int k = Mathf.Clamp(linksPerMark, 1, nearIndex.Length);
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth a = targets[i];
            if (a == null || !a.isActiveAndEnabled) continue;
            Vector2 pa = a.transform.position;
            int found = 0;
            for (int j = 0; j < targets.Count; j++)
            {
                if (j == i) continue;
                EnemyHealth b = targets[j];
                if (b == null || !b.isActiveAndEnabled) continue;
                float d = ((Vector2)b.transform.position - pa).sqrMagnitude;
                if (d > maxSqr) continue;
                // 가까운 k개만 정렬해서 유지
                int slot = found < k ? found++ : k;
                if (slot == k) { if (d >= nearDist[k - 1]) continue; slot = k - 1; }
                while (slot > 0 && nearDist[slot - 1] > d) { nearDist[slot] = nearDist[slot - 1]; nearIndex[slot] = nearIndex[slot - 1]; slot--; }
                nearDist[slot] = d; nearIndex[slot] = j;
            }
            for (int n = 0; n < found; n++)
            {
                int j = nearIndex[n];
                long key = (long)Mathf.Min(i, j) * 100000L + Mathf.Max(i, j);
                if (!linkKeys.Add(key)) continue;
                linkA.Add(i); linkB.Add(j);
            }
        }

        usedLinks = linkA.Count;
        while (links.Count < usedLinks)
        {
            LineRenderer l = JobTierUtil.NewLine("LegionLink", LinkColor, 0.06f, 61);
            if (cloneRoot == null) cloneRoot = new GameObject("[ShadowLegion]").transform;
            l.transform.SetParent(cloneRoot, false);
            links.Add(l);
        }
        // 그물선 그림이 있으면 선에 입힘 (선 길이에 맞게 늘어남)
        linkFrames = SkillFx.Frames("legion_link");
        if (linkFrames != null && linkMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) { linkMaterial = new Material(sh); linkMaterial.mainTexture = linkFrames[0].texture; }
        }
        for (int i = 0; i < links.Count; i++)
        {
            links[i].gameObject.SetActive(i < usedLinks);
            links[i].positionCount = 0;
            if (linkMaterial != null)
            {
                links[i].sharedMaterial = linkMaterial;
                links[i].textureMode = LineTextureMode.Stretch;
                links[i].startWidth = links[i].endWidth = 0.4f;
            }
        }
    }

    private void HideLinks()
    {
        for (int i = 0; i < links.Count; i++)
            if (links[i] != null) { links[i].positionCount = 0; links[i].gameObject.SetActive(false); }
        usedLinks = 0;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        HideClones();
        HideLinks();
    }

    private void OnDestroy()
    {
        if (cloneRoot != null) Destroy(cloneRoot.gameObject);
    }
}
