using UnityEngine;

// 스킬 화살 크기를 한 곳에서 조절.
//   Hierarchy에서 Player 선택 → Inspector 맨 아래 "Skill Settings" → Arrow Length
//   (없으면 메뉴 Tools > Game UI > Skill Settings (화살 크기) 를 한 번 누르면 Player에 붙고 씬에 저장됨)
// 플레이 중에 바꾸면 그다음 쏘는 화살부터 바로 바뀜. 플레이 중에 바꾼 값은 멈추면 원래대로 돌아가니,
// 마음에 드는 숫자를 찾으면 멈춘 뒤 같은 숫자를 다시 넣어 주세요.
[DisallowMultipleComponent]
public class SkillSettings : MonoBehaviour
{
    public static SkillSettings Instance { get; private set; }

    [Tooltip("모든 스킬 화살의 기본 길이 (칸). 1 = 기본 공격 화살보다 조금 작게, 1.5 = 지금, 2 = 크게.\n" +
             "실제 길이 = 이 값 × 스킬마다의 Arrow Scale (예: 2차 Z 부채꼴 1.5 → 1.5 × 1.5 = 2.25칸)")]
    [Range(0.3f, 4f)] public float arrowLength = 1.5f;

    private void OnEnable() { Instance = this; }
    private void OnDisable() { if (Instance == this) Instance = null; }
}
