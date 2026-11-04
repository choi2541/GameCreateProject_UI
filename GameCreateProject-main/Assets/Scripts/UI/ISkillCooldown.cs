// 스킬 슬롯 UI가 쿨타임을 읽어가기 위한 인터페이스입니다.
// 스킬/대쉬 스크립트에 이 3가지만 구현하면 하단 스킬 슬롯에 쿨타임이 표시됩니다.
//
//   public class PlayerDash : MonoBehaviour, ISkillCooldown
//   {
//       public float Cooldown => cooldown;                                         // 전체 쿨타임
//       public float CooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time); // 남은 시간
//       public string KeyLabel => dashKey.ToString();                              // 표시할 키
//   }
//
// 클래스 이름에 "Dash"가 들어가면 대쉬 슬롯에 자동으로 연결됩니다. (Player 또는 자식 오브젝트에 붙일 것)
public interface ISkillCooldown
{
    // 전체 쿨타임(초)
    float Cooldown { get; }

    // 남은 쿨타임(초). 0이면 사용 가능
    float CooldownRemaining { get; }

    // 슬롯 아래 표시할 키 이름 (예: "Z", "LeftShift")
    string KeyLabel { get; }
}
