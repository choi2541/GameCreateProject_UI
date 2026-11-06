using System.Collections;
using UnityEngine;

// [2차 Z] 부채꼴 일제 사격 (애쉬 W 느낌)
// 가장 가까운 적 방향으로 화살 여러 발을 부채꼴로 한 번에 발사. (없으면 마지막 이동 방향)
// Inspector에서 "한 발씩 쓸기" 방식으로도 바꿀 수 있음.
[DisallowMultipleComponent]
public class ArcherVolleySkill : ArcherSkillBase, ISecondJobSkill
{
    [Header("부채꼴 사격")]
    [Tooltip("켜면 한 번에 부채꼴 발사 (애쉬 W) / 끄면 한 발씩 쓸면서 발사 (예전 방식)")]
    [SerializeField] private bool fireAllAtOnce = true;
    [SerializeField, Range(1, 30)] private int arrowCount = 9;
    [Tooltip("부채꼴 전체 각도")]
    [SerializeField, Range(0f, 120f)] private float sweepAngle = 50f;
    [Tooltip("화살 사이 간격(초). 작을수록 빠르게 따다다닥")]
    [SerializeField, Min(0.01f)] private float interval = 0.035f;
    [Tooltip("한 번 쓸고 끝(false) / 갔다가 돌아오기(true)")]
    [SerializeField] private bool sweepBack = false;
    [SerializeField, Min(1)] private int damage = 6;
    [SerializeField, Min(1f)] private float speed = 18f;
    [SerializeField, Min(1f)] private float range = 12f;
    [Tooltip("화살 크기 배율 (1 = 기본)")]
    [SerializeField, Min(0.5f)] private float arrowScale = 1.5f;

    [Header("한 발 효과음 (선택)")]
    [SerializeField] private AudioClip shotSound;
    [SerializeField, Range(0f, 1f)] private float shotVolume = 0.5f;
    private AudioSource shotSource;
    private bool firing;

    private void Awake()
    {
        shotSource = gameObject.AddComponent<AudioSource>();
        shotSource.playOnAwake = false;
        shotSource.loop = false;
        shotSource.spatialBlend = 0f;
    }

    private void Reset()
    {
        key = KeyCode.Z;
        cooldown = 6f;
    }

    protected override bool Cast()
    {
        if (firing || OwnerDead) return false;
        if (fireAllAtOnce)
        {
            FireFan(AimDirection());
            GetComponentInParent<PlayerAttackVisual>()?.PlayAttack();
            PlayShot();
        }
        else
        {
            firing = true;
            StartCoroutine(Sweep(AimDirection()));
        }
        return true;
    }

    // 한 번에 부채꼴로
    private void FireFan(Vector2 aim)
    {
        for (int i = 0; i < arrowCount; i++)
        {
            float t = arrowCount == 1 ? 0.5f : i / (arrowCount - 1f);
            float angle = Mathf.Lerp(-sweepAngle * 0.5f, sweepAngle * 0.5f, t);
            SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, SkillUtil.Rotate(aim, angle),
                speed, range, SkillModifiers.Damage(damage), 0.28f, true, false, Enemies, arrowScale);
        }
        // 발사 빛: 그림의 왼쪽 끝(부채꼴 꼭짓점)이 플레이어 앞에 오도록 (그림 없으면 예전 원)
        float flashH = 2.4f, flashW = flashH * 71f / 91f;
        if (!SkillFx.Anim("volley_flash", Origin + aim * (flashW * 0.5f + 0.2f), flashH, 0.25f,
                Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 62))
            SkillFx.Burst(Origin, 0.3f, 1.6f, new Color(arrowColor.r, arrowColor.g, arrowColor.b, 0.6f), 0.2f, true);
    }

    // 왼쪽 끝 → 오른쪽 끝으로 한 발씩 (매 발 플레이어 현재 위치에서)
    private IEnumerator Sweep(Vector2 aim)
    {
        int total = sweepBack ? arrowCount * 2 - 1 : arrowCount;
        for (int i = 0; i < total; i++)
        {
            while (Time.timeScale <= 0f && !OwnerDead) yield return null;
            if (OwnerDead) break;
            int step = i < arrowCount ? i : (arrowCount - 1) - (i - arrowCount + 1);
            float t = arrowCount == 1 ? 0.5f : step / (arrowCount - 1f);
            float angle = Mathf.Lerp(-sweepAngle * 0.5f, sweepAngle * 0.5f, t);

            SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, SkillUtil.Rotate(aim, angle),
                speed, range, SkillModifiers.Damage(damage), 0.28f, true, false, Enemies, arrowScale);
            GetComponentInParent<PlayerAttackVisual>()?.PlayAttack();
            PlayShot();
            yield return new WaitForSeconds(Mathf.Max(0.01f, interval));
        }
        firing = false;
    }

    private void PlayShot()
    {
        if (shotSound == null || shotSource == null) return;
        FindAnyObjectByType<AudioVolumeController>()?.RegisterSource(shotSource);
        shotSource.PlayOneShot(shotSound, shotVolume);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        firing = false;
        if (shotSource != null) shotSource.Stop();
    }
}
