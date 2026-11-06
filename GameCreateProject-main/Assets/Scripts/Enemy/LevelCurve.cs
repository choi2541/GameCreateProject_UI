using UnityEngine;

// 레벨 곡선 한 곳에 모음: 레벨업에 필요한 경험치, 적 1마리 경험치.
// 숫자는 EnemyLevelScaling 인스펙터에서 바꿈 (씬에 없으면 아래 기본값).
// 팀원 코드(ExperienceProgress, EnemyHealth)가 이걸 쓰도록 한 줄씩 바꾸면 적용됨 → "팀원_확인후_적용" 폴더 참고
public static class LevelCurve
{
    // 이 레벨에서 다음 레벨까지 필요한 경험치
    public static int Required(int level)
    {
        EnemyLevelScaling s = EnemyLevelScaling.Instance;
        if (level <= 1) return s != null ? s.RequiredFirst : 5;
        float baseExp = s != null ? s.RequiredBase : 8f;
        float perLevel = s != null ? s.RequiredPerLevel : 3f;
        return Mathf.Max(1, Mathf.RoundToInt(baseExp + perLevel * (level - 2)));
    }

    // 1레벨 0경험치부터 이 레벨 시작까지 모은 경험치 합
    public static long TotalBefore(int level)
    {
        long total = 0;
        for (int l = 1; l < level; l++) total += Required(l);
        return total;
    }

    // 적 1마리를 잡으면 주는 경험치 (플레이어 레벨이 오를수록 조금씩 많아짐)
    public static int KillExp()
    {
        EnemyLevelScaling s = EnemyLevelScaling.Instance;
        int level = PlayerProgression.Instance != null ? PlayerProgression.Instance.Level : 1;
        float b = s != null ? s.KillExpBase : 1f;
        float per = s != null ? s.KillExpPerLevel : 0.04f;
        return Mathf.Max(1, Mathf.RoundToInt(b * (1f + per * (level - 1))));
    }
}
