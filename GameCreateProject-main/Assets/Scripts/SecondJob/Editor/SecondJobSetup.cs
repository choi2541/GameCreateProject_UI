#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Game UI > Setup Archer Skills (Dash + 2nd Job)
// 기존 충전 대쉬를 유지하고 2차 전직과 Z/X/C/회피 사격을 연결합니다.
public static class SecondJobSetup
{
    [MenuItem("Tools/Game UI/Setup Archer Skills (Dash + 2nd Job)")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }

        PlayerHealth player = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (player == null)
        {
            EditorUtility.DisplayDialog("Game UI", "씬에서 Player(PlayerHealth)를 찾지 못했습니다.", "확인");
            return;
        }
        GameObject go = player.gameObject;
        if (go.GetComponent<PlayerMovement>() == null || go.GetComponentInChildren<PlayerDash>(true) != null)
        {
            EditorUtility.DisplayDialog("2차 궁수 설정", "PlayerMovement가 필요합니다. 기존 PlayerDash가 붙어 있다면 해당 컴포넌트만 제거한 뒤 다시 실행해주세요. Shift는 PlayerMovement만 담당합니다.", "확인");
            return;
        }

        // 기존 관통탄 스킬에서 화살 이미지와 적 컨테이너를 가져옵니다.
        PiercingProjectileSkill piercing = go.GetComponentInChildren<PiercingProjectileSkill>(true);
        Sprite arrow = null;
        Object enemyContainer = null;
        if (piercing != null)
        {
            SerializedObject p = new SerializedObject(piercing);
            Component prefab = p.FindProperty("projectilePrefab")?.objectReferenceValue as Component;
            SpriteRenderer sr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
            arrow = sr != null ? sr.sprite : null;
            enemyContainer = p.FindProperty("enemyContainer")?.objectReferenceValue;
        }

        EvasiveShotSkill evasion = Ensure<EvasiveShotSkill>(go);
        SerializedObject evasionObject = new SerializedObject(evasion);
        if (evasionObject.FindProperty("arrowSprite").objectReferenceValue == null)
            evasionObject.FindProperty("arrowSprite").objectReferenceValue = arrow;
        if (evasionObject.FindProperty("enemyContainer").objectReferenceValue == null)
            evasionObject.FindProperty("enemyContainer").objectReferenceValue = enemyContainer;
        evasionObject.ApplyModifiedProperties();
        Configure(Ensure<ArcherVolleySkill>(go), KeyCode.Z, 6f, arrow, enemyContainer);
        Configure(Ensure<ArrowRainSkill>(go), KeyCode.X, 8f, arrow, enemyContainer);
        Configure(Ensure<CrystalArrowSkill>(go), KeyCode.C, 20f, arrow, enemyContainer);

        // 2차 전직 목록 연결
        SecondJobSystem system = Ensure<SecondJobSystem>(go);
        SerializedObject so = new SerializedObject(system);
        so.FindProperty("requiredLevel").intValue = 30;
        so.FindProperty("requiredFirstJob").intValue = (int)PlayerJob.Archer;
        SerializedProperty disable = so.FindProperty("disableOnAdvance");
        disable.arraySize = piercing != null ? 1 : 0;
        if (piercing != null)
            disable.GetArrayElementAtIndex(0).objectReferenceValue = piercing;

        SerializedProperty jobs = so.FindProperty("jobs");
        if (jobs.arraySize == 0)
            jobs.arraySize = 1;
        SerializedProperty first = jobs.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("displayName").stringValue = "2차 궁수";
        first.FindPropertyRelative("description").stringValue = "부채꼴 연사 · 화살비\n가시 덩굴 화살 · 회피 사격";
        SerializedProperty enable = first.FindPropertyRelative("enableSkills");
        MonoBehaviour[] skills =
        {
            go.GetComponent<ArcherVolleySkill>(), go.GetComponent<ArrowRainSkill>(), go.GetComponent<CrystalArrowSkill>(), evasion
        };
        enable.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
            enable.GetArrayElementAtIndex(i).objectReferenceValue = skills[i];
        so.ApplyModifiedProperties();
        foreach (MonoBehaviour skill in skills)
        {
            Undo.RecordObject(skill, "2차 스킬 전직 전 비활성화");
            skill.enabled = false;
            EditorUtility.SetDirty(skill);
        }
        // 기존 HUD는 같은 키의 활성 스킬로 자동 전환합니다. Shift 충전 어댑터는 유지합니다.
        Ensure<StackDashUIAdapter>(go);

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene); // 바로 저장 (저장 안 하고 다른 씬을 열면 사라지므로)
        Selection.activeGameObject = go;
        Debug.Log("[SecondJobSetup] 30레벨 2차 궁수와 Z/X/C/회피 사격을 연결하고 씬을 저장했습니다. 기존 충전 대쉬는 유지됩니다."
            + (arrow == null ? " (화살 이미지를 못 찾아 임시 화살 모양 사용)" : ""), go);
        EditorUtility.DisplayDialog("Game UI", "2차 전직을 설정하고 씬을 저장했어요.\n(2차: 궁수 1차 전직 후 Lv30)", "확인");
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static void Configure(ArcherSkillBase skill, KeyCode key, float cooldown, Sprite arrow, Object enemyContainer)
    {
        SerializedObject so = new SerializedObject(skill);
        so.FindProperty("key").intValue = (int)key;
        so.FindProperty("cooldown").floatValue = cooldown;
        if (arrow != null && so.FindProperty("arrowSprite").objectReferenceValue == null)
            so.FindProperty("arrowSprite").objectReferenceValue = arrow;
        if (enemyContainer != null && so.FindProperty("enemyContainer").objectReferenceValue == null)
            so.FindProperty("enemyContainer").objectReferenceValue = enemyContainer;
        so.ApplyModifiedProperties();
    }
}
#endif
