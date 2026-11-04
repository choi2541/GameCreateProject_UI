using UnityEngine;

// 게임 카메라 찾기. Tag가 MainCamera가 아니어도(Untagged) 씬에서 켜진 카메라를 찾아 씁니다.
// 데미지 숫자, 보스 화살표/소환 위치, 스킬 화살 화면 밖 판정이 모두 이걸 사용합니다.
public static class GameCamera
{
    private static Camera cached;

    public static Camera Get()
    {
        Camera main = Camera.main;
        if (main != null)
            return main;

        if (cached != null && cached.isActiveAndEnabled)
            return cached;

        cached = null;
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (!cam.isActiveAndEnabled)
                continue;
            // 게임 화면을 그리는 카메라(화면 전체에 그리는 것)를 우선
            if (cached == null || cam.depth < cached.depth)
                cached = cam;
        }
        return cached;
    }
}
