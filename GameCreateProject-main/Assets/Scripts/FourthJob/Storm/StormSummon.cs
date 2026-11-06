using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [4차 폭풍 오리진 · Ctrl+C] 폭풍 소환
// 하늘로 화살 → 화면에 폭풍 → 토네이도 3개가 돌아다니며 빨아들이고 지속 피해
// → 가운데로 모여 하나로 합쳐짐 → 대폭발 (잡몹 날려버림 / 보스 큰 피해)
[DisallowMultipleComponent]
public class StormSummon : FourthJobSkill
{
    [Header("폭풍 소환")]
    [SerializeField, Range(1, 6)] private int tornadoCount = 3;
    [SerializeField, Min(0.5f)] private float roamSeconds = 2.6f;
    [SerializeField, Min(0.1f)] private float mergeSeconds = 0.6f;
    [SerializeField, Min(1)] private int tickDamage = 12;
    [SerializeField, Min(1)] private int blastDamage = 120;
    [SerializeField] private bool blastKillsMobs = true;
    [SerializeField, Min(1)] private int bossBlastDamage = 500;
    [Header("바인드")]
    [Tooltip("발동하는 순간 화면 안의 적을 묶어 둠 (폭발 직전까지)")]
    [SerializeField] private bool bindEnemies = true;
    [Tooltip("마지막 폭발 그림 크기 (1 = 화면 높이)")]
    [SerializeField, Min(0.2f)] private float blastScreenSize = 1.1f;

    public override ArcherBranch Branch => ArcherBranch.Storm;
    public override string DisplayName => "오리진 · 폭풍 소환";
    public override string Description => "하늘로 쏜 화살이 폭풍을 불러\n토네이도가 화면을 휩쓴 뒤 하나로 합쳐져 대폭발";

    private void Reset() { key = KeyCode.C; maxCharges = 1; chargeSeconds = 90f; cutInId = "storm_origin"; }

    protected override IEnumerator Execute()
    {
        Camera cam = GameCamera.Get();
        Vector2 center = cam != null ? (Vector2)cam.transform.position : Origin;
        float halfH = cam != null ? cam.orthographicSize : 5f;
        float halfW = cam != null ? halfH * cam.aspect : 9f;

        // 0) 바인드: 화면 안의 적을 폭발 직전까지 묶음 (토네이도가 빨아들이는 건 그대로)
        if (bindEnemies)
        {
            float bindSeconds = 0.35f + roamSeconds + mergeSeconds;
            foreach (EnemyHealth e in OnScreenNow(Origin, 1f))
            {
                SkillStun.Apply(e, bindSeconds);
                SkillFx.Burst(e.transform.position, 1.2f, 0.4f, new Color(0.7f, 1f, 0.9f, 0.7f), 0.3f, true);
            }
        }

        // 1) 하늘로 화살
        SkillFx.Anim("sky_shot", Origin + Vector2.up * 1.6f, 2.6f, 0.35f, 0f, 63);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            JobTierUtil.FlashLine(Origin + Vector2.up * (t * 30f), Origin + Vector2.up * (t * 30f + 1.5f), JobTierUtil.StormColor, 0.15f, 0.15f, false);
            yield return null;
        }

        // 2) 폭풍 (화면이 청록빛 + 대각선 바람)
        GameObject tint = new GameObject("StormTint");
        SpriteRenderer tsr = tint.AddComponent<SpriteRenderer>();
        tsr.sprite = SkillUtil.CircleSprite();
        tsr.color = new Color(0.15f, 0.35f, 0.32f, 0f);
        tsr.sortingOrder = 50;
        tint.transform.localScale = new Vector3(halfW * 3f, halfH * 3f, 1f);
        // 비바람 그림을 화면에 타일처럼 깔기 (카메라를 따라감)
        GameObject rain = null;
        if (SkillFx.Frames("storm_rain") != null)
        {
            rain = new GameObject("StormRain");
            const float tile = 4.5f, step = 3.4f; // 칸끼리 겹치게 깔아서 이음매가 안 보이게
            int nx = Mathf.CeilToInt(halfW * 2f / step) + 2, ny = Mathf.CeilToInt(halfH * 2f / step) + 2;
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                {
                    var cell = new GameObject("Tile");
                    cell.transform.SetParent(rain.transform, false);
                    cell.transform.localPosition = new Vector3(-halfW - step * 0.5f + ix * step + Random.Range(-0.4f, 0.4f),
                                                               -halfH - step * 0.5f + iy * step + Random.Range(-0.4f, 0.4f), 0f);
                    SpriteRenderer r = SkillFx.Loop("storm_rain", cell.transform, tile, 8f, 51);
                    if (r != null)
                    {
                        r.color = new Color(1f, 1f, 1f, 0.32f);
                        r.flipX = Random.value < 0.5f;
                        r.GetComponent<SkillFxLoop>().Set("storm_rain", 8f, r, Random.Range(0f, 1f)); // 칸마다 다른 순간부터
                    }
                }
        }

        List<WindVortex> vortices = new List<WindVortex>();
        for (int i = 0; i < tornadoCount; i++)
        {
            Vector2 p = center + new Vector2(Random.Range(-halfW, halfW) * 0.7f, Random.Range(-halfH, halfH) * 0.6f);
            WindVortex v = WindVortex.Spawn(p, 1.5f, Enemies);
            v.lifetime = roamSeconds + mergeSeconds + 0.2f;
            v.radius = 2.2f; v.coreRadius = 1.2f; v.pullSpeed = 4f; v.tickDamage = Dmg(tickDamage);
            v.velocity = Random.insideUnitCircle.normalized * 3.5f;
            vortices.Add(v);
        }

        float elapsed = 0f;
        while (elapsed < roamSeconds)
        {
            elapsed += Time.deltaTime;
            if (cam != null) tint.transform.position = cam.transform.position;
            if (rain != null && cam != null) rain.transform.position = (Vector2)cam.transform.position;
            tsr.color = new Color(0.15f, 0.35f, 0.32f, Mathf.Clamp01(elapsed / 0.4f) * 0.35f);
            if (Random.value < 0.5f) // 바람 줄기 (연출 풀 사용)
            {
                Vector2 s = center + new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));
                // 하늘에서 바람 화살이 떨어지고 땅에서 터짐 (그림 없으면 바람 줄기)
                if (!SkillFx.FallFrames("storm_arrow", s, 1.8f, 0.2f, 1.3f, "storm_hit", 1.4f, 0.3f))
                    JobTierUtil.FlashLine(s, s + new Vector2(-1.4f, -1f), new Color(0.85f, 1f, 0.95f, 0.6f), 0.05f, 0.25f, false);
            }
            yield return null;
        }

        // 3) 가운데로 모여 하나로
        if (cam != null) center = cam.transform.position;
        foreach (WindVortex v in vortices) if (v != null) v.velocity = Vector2.zero;
        SkillFx.Anim("tornado_merge", center, 4.5f, mergeSeconds, 0f, 63); // 회오리 3개가 하나로
        WindVortex big = WindVortex.Spawn(center, 1f, Enemies);
        big.lifetime = mergeSeconds + 0.3f; big.radius = 4f; big.pullSpeed = 6f; big.tickDamage = Dmg(tickDamage);
        for (float t = 0f; t < mergeSeconds; t += Time.deltaTime)
        {
            float k = t / mergeSeconds;
            foreach (WindVortex v in vortices)
                if (v != null) v.transform.position = Vector2.Lerp(v.transform.position, center, 6f * Time.deltaTime);
            big.SetSize(1f + 2.5f * k);
            yield return null;
        }
        foreach (WindVortex v in vortices) if (v != null) Destroy(v.gameObject);

        // 4) 대폭발
        PlayImpact();
        if (SkillFx.Anim("storm_blast", center, halfH * 2f * blastScreenSize, 0.75f, 0f, 64))
            SkillFx.Burst(center, 1f, halfW * 2.4f, new Color(0.85f, 1f, 0.95f, 0.5f), 0.5f, true); // 바깥으로 퍼지는 충격파
        else
        {
            SkillFx.Burst(center, 1f, halfW * 2.4f, new Color(0.85f, 1f, 0.95f, 0.9f), 0.5f, true);
            SkillFx.Burst(center, 1f, halfW * 1.6f, new Color(0.9f, 1f, 0.97f, 0.6f), 0.4f);
        }
        foreach (EnemyHealth e in OnScreenNow(center, 1f))
        {
            Vector2 away = ((Vector2)e.transform.position - center).normalized;
            if (JobTierUtil.IsBoss(e)) JobTierUtil.Hit(e, Dmg(bossBlastDamage));
            else
            {
                JobTierUtil.Knockback(e, away * 14f, 0.3f);
                JobTierUtil.Hit(e, blastKillsMobs ? 999999 : Dmg(blastDamage));
            }
        }
        if (big != null) Destroy(big.gameObject);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            tsr.color = new Color(0.15f, 0.35f, 0.32f, 0.35f * (1f - t / 0.4f));
            yield return null;
        }
        Destroy(tint);
        if (rain != null) Destroy(rain);
    }
}
