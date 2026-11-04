using UnityEngine;

// 보스의 "보이지 않는 판정점" 표시용. (몹 HP바 등에서 제외하기 위해 붙임)
// 실제 동작은 BossController가 관리합니다.
public class BossHitProxy : MonoBehaviour
{
    public BossController Owner { get; set; }
}
