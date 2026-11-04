using UnityEngine;

// 키보드/마우스 입력을 한 곳에서 읽습니다. (구 Input Manager / 새 Input System 모두 지원)
public static class UIKeyInput
{
    public static bool EscapeDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }

    // 캐릭터 창 키 (기본 Tab, 설정 > KEY SETTINGS에서 변경 가능)
    public static bool TabDown
    {
        get
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyBindings.Get(GameAction.Character));
#endif
        }
    }

    public static bool TabHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.tabKey.isPressed;
#else
            return Input.GetKey(KeyBindings.Get(GameAction.Character));
#endif
        }
    }

    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }
}
