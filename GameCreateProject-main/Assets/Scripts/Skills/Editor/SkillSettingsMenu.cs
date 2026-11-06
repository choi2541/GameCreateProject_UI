#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Game UI > Skill Settings (화살 크기) : Player에 SkillSettings를 붙이고 선택 + 씬 저장
public static class SkillSettingsMenu
{
    [MenuItem("Tools/Game UI/Skill Settings (화살 크기)")]
    private static void Open()
    {
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (player == null)
        {
            EditorUtility.DisplayDialog("스킬 설정", "게임 씬(GameCreateProject)을 열고 다시 눌러 주세요.", "확인");
            return;
        }
        SkillSettings s = player.GetComponent<SkillSettings>();
        if (s == null)
        {
            s = Undo.AddComponent<SkillSettings>(player.gameObject);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
        }
        Selection.activeObject = player.gameObject;
        EditorGUIUtility.PingObject(player.gameObject);
    }
}
#endif
