using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 4차 스킬 공통 (어센트 / 오리진)
//  - Ctrl + 키 (X 또는 C) 또는 X/C 위 강화 칸 클릭으로 사용
//  - 충전 칸 방식: 칸이 하나라도 차 있으면 사용 가능 (어센트 3칸 / 오리진 1칸)
//  - 사용하면 컷인 재생 → 그동안 게임이 느려지고 플레이어는 무적 → 컷인이 끝나면 스킬 발동
// JobTierSystem이 4차 전직 때 켭니다. 켜지면 X/C 위 강화 칸(FourthJobHUD)이 자동으로 생깁니다.
public abstract class FourthJobSkill : MonoBehaviour, IFourthJobSkill
{
    [Header("공통")]
    [Tooltip("Ctrl과 같이 누를 키")]
    [SerializeField] protected KeyCode key = KeyCode.X;
    [SerializeField, Range(1, 5)] protected int maxCharges = 3;
    [Tooltip("1칸이 차는 시간(초)")]
    [SerializeField, Min(1f)] protected float chargeSeconds = 20f;
    [Tooltip("Resources/CutIn/ 안의 컷인 이름")]
    [SerializeField] protected string cutInId = "storm_ascent";
    [Tooltip("컷인 동안 게임 속도 (0.2 = 5배 느리게)")]
    [SerializeField, Range(0.05f, 1f)] protected float slowScale = 0.2f;
    [Tooltip("강화 칸 아이콘. 비우면 컷인 그림에서 잘라서 씁니다.")]
    [SerializeField] protected Sprite icon;
    [SerializeField] protected Sprite arrowSprite;
    [SerializeField] protected Transform enemyContainer;

    [Header("사운드 · 음성 (비워두면 소리 없음)")]
    [Tooltip("컷인이 시작될 때 나오는 대사 (음성)")]
    [SerializeField] protected AudioClip voiceLine;
    [SerializeField, Range(0f, 1f)] protected float voiceVolume = 1f;
    [Tooltip("컷인이 시작될 때 효과음 (쉭, 쿵 같은 연출음)")]
    [SerializeField] protected AudioClip cutInSound;
    [SerializeField, Range(0f, 1f)] protected float cutInVolume = 0.8f;
    [Tooltip("컷인이 끝나고 스킬이 실제로 나갈 때")]
    [SerializeField] protected AudioClip castSound;
    [SerializeField, Range(0f, 1f)] protected float castVolume = 0.8f;
    [Tooltip("명중 · 폭발 순간")]
    [SerializeField] protected AudioClip impactSound;
    [SerializeField, Range(0f, 1f)] protected float impactVolume = 0.8f;

    public abstract ArcherBranch Branch { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public string KeyText => "Ctrl+" + SkillSlotUI.PrettyKey(key.ToString());
    public Sprite Icon
    {
        get
        {
            if (icon == null) icon = SkillCutIn.Icon(cutInId);
            return icon;
        }
    }

    public int MaxCharges => maxCharges;
    public int Charges { get; private set; }
    public float ChargeProgress => Charges >= maxCharges ? 1f : chargeTimer / Mathf.Max(0.01f, SkillModifiers.Cooldown(chargeSeconds));
    public bool Busy { get; private set; }
    public KeyCode Key => key;

    protected Transform Enemies => SkillUtil.EnemyContainer(enemyContainer);
    protected Vector2 Origin => transform.position;
    protected PlayerHealth Player { get; private set; }

    private float chargeTimer;

    // 메모리 절약용 재사용 목록 (스킬마다 하나. yield를 넘어 쓸 때는 복사해서 쓰세요)
    private readonly List<EnemyHealth> screenBuffer = new List<EnemyHealth>(128);
    protected List<EnemyHealth> OnScreenNow(Vector2 from, float margin = 0.2f) =>
        JobTierUtil.OnScreen(Enemies, from, screenBuffer, margin);

    // 컷인 감속 되돌리기 (일시정지와 겹쳐도 안전하게)
    private static float slowedValue = -1f, restoreValue = 1f;

    [Header("계수 (폭풍 = AP · 스킬 피해력 / 그림자 = AD · 힘)")]
    [Tooltip("스탯 보너스를 몇 배로 받을지. 1 = 그대로, 1.5 = 1.5배, 0 = 스탯 영향 없음")]
    [SerializeField, Min(0f)] protected float statRatio = 1.5f;

    public float StatRatio => statRatio;

    protected int Dmg(int baseDamage, EnemyHealth target = null) => JobTierUtil.Damage(baseDamage, Branch, target, statRatio);

    protected virtual void Awake()
    {
        Player = GetComponentInParent<PlayerHealth>();
    }

    protected virtual void OnEnable()
    {
        Charges = maxCharges; // 전직하면 가득 찬 상태로 시작
        chargeTimer = 0f;
        FourthJobHUD.Ensure();
        SkillCutIn.Keep(cutInId); // 그림 미리 불러 두고 계속 들고 있음 (쓸 때 멈칫·잘림 방지)
    }

    protected virtual void OnDisable()
    {
        SkillCutIn.Unkeep(cutInId); // 스킬이 꺼지면 컷인 그림도 내릴 수 있게
        StopAllCoroutines();
        if (Busy) RestoreTime();
        Busy = false;
    }

    protected virtual void Update()
    {
        // 컷인 중에 일시정지 → 해제되면서 느린 속도가 남은 경우 원래대로
        if (!Busy && slowedValue > 0f && Mathf.Approximately(Time.timeScale, slowedValue))
            RestoreTime();

        if (Charges < maxCharges)
        {
            chargeTimer += Time.deltaTime;
            float need = SkillModifiers.Cooldown(chargeSeconds);
            if (chargeTimer >= need) { chargeTimer -= need; Charges++; }
        }
        else chargeTimer = 0f;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(key))
            TryActivate();
    }

    // 키 또는 강화 칸 클릭
    public bool TryActivate()
    {
        if (!isActiveAndEnabled || Busy || Charges <= 0 || Time.timeScale <= 0f)
            return false;
        if (Player != null && Player.IsDead)
            return false;
        Charges--;
        StartCoroutine(Run());
        return true;
    }

    private IEnumerator Run()
    {
        Busy = true;
        SkillSound.Play(voiceLine, voiceVolume);
        SkillSound.Play(cutInSound, cutInVolume);
        float length = SkillCutIn.Play(cutInId) ? SkillCutIn.Duration(cutInId) : 0f;
        if (length > 0f)
        {
            JobTierUtil.Invincible(Player, length + 0.6f);
            restoreValue = Time.timeScale;
            slowedValue = restoreValue * slowScale;
            Time.timeScale = slowedValue;
            yield return new WaitForSecondsRealtime(length);
            if (Mathf.Approximately(Time.timeScale, slowedValue))
                RestoreTime();
        }
        SkillSound.Play(castSound, castVolume);
        yield return Execute();
        Busy = false;
    }

    private static void RestoreTime()
    {
        if (slowedValue > 0f && Mathf.Approximately(Time.timeScale, slowedValue))
            Time.timeScale = restoreValue;
        slowedValue = -1f;
    }

    // 명중 · 폭발 소리 (각 스킬에서 터지는 순간에 호출. 연속으로 불러도 너무 겹치지 않음)
    protected void PlayImpact(float minGap = 0.06f) => SkillSound.Play(impactSound, impactVolume, minGap);

    // 컷인이 끝난 뒤 실제 스킬
    protected abstract IEnumerator Execute();
}
