#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Game UI > Setup Boss + Mob HP Bars
// 현재 씬에 "BossSystem" 오브젝트(BossDirector + MobHealthBars + GameClear)를 만듭니다.
public static class BossSetup
{
    [MenuItem("Tools/Game UI/Setup Boss + Mob HP Bars")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }

        BossDirector existing = Object.FindAnyObjectByType<BossDirector>(FindObjectsInactive.Include);
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = new GameObject("BossSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create BossSystem");
            Undo.AddComponent<BossDirector>(go);
        }
        if (go.GetComponent<MobHealthBars>() == null)
            Undo.AddComponent<MobHealthBars>(go);
        if (go.GetComponent<GameClear>() == null)
            Undo.AddComponent<GameClear>(go);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[BossSetup] BossSystem 준비 완료 (5/10/15분 보스 + 몹 HP바 + 15분 보스 처치 시 클리어). 씬을 저장해주세요.", go);
    }
}
#endif
