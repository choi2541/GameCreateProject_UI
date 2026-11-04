using UnityEngine;

// Display the existing stack dash without installing a second Shift movement handler.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class StackDashUIAdapter : MonoBehaviour, ISkillCooldown
{
    private PlayerMovement movement;
    private void Awake() { movement = GetComponent<PlayerMovement>(); }
    public int Charges => movement != null ? movement.DashCharges : 0;
    public int MaxCharges => movement != null ? movement.MaxDashCharges : 3;
    public float Cooldown => movement != null ? movement.DashRechargeDuration : 3f;
    public float CooldownRemaining => movement != null ? movement.DashRechargeRemaining : 0f;
    // 팀 PlayerMovement에 dashKey 칸이 있으면 그 키를, 없으면 Shift를 표시
    private static readonly System.Reflection.FieldInfo dashKeyField = typeof(PlayerMovement).GetField("dashKey",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
    public string KeyLabel => movement != null && dashKeyField != null && dashKeyField.FieldType == typeof(KeyCode)
        ? ((KeyCode)dashKeyField.GetValue(movement)).ToString() : "LeftShift";
}
