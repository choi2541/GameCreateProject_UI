#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴: Tools > Game UI > Setup 3rd + 4th Job
// Player에 3·4차 전직 시스템과 스킬(그림자 5개 / 폭풍 5개 / 4차 4개)을 붙이고 키·쿨타임·화살 이미지를 설정합니다.
// (먼저 Setup Archer Skills (Dash + 2nd Job)을 실행해 두세요)
// 4차 스킬은 FourthJob 폴더가 있을 때만 붙습니다. (이름으로 찾기 때문에 폴더를 지워도 에러 없음)
public static class JobTierSetup
{
    [MenuItem("Tools/Game UI/Setup 3rd + 4th Job")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Game UI", "플레이 모드를 끈 상태에서 실행해주세요.", "확인");
            return;
        }
        PlayerHealth player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (player == null)
        {
            EditorUtility.DisplayDialog("Game UI", "씬에서 Player(PlayerHealth)를 찾지 못했습니다.", "확인");
            return;
        }
        GameObject go = player.gameObject;
        if (go.GetComponent<SecondJobSystem>() == null)
            Debug.LogWarning("[JobTierSetup] 2차 전직(SecondJobSystem)이 없습니다. 먼저 Setup Archer Skills를 실행하세요.", go);

        // 2차 스킬에서 화살 이미지·적 컨테이너를 가져옴
        Sprite arrow = null;
        UnityEngine.Object enemies = null;
        ArcherSkillBase source = go.GetComponent<ArcherVolleySkill>() as ArcherSkillBase ?? go.GetComponent<PlayerDash>();
        if (source != null)
        {
            SerializedObject s = new SerializedObject(source);
            arrow = s.FindProperty("arrowSprite").objectReferenceValue as Sprite;
            enemies = s.FindProperty("enemyContainer").objectReferenceValue;
        }

        Ensure<JobTierSystem>(go);

        // 3차 (Shadow / Storm) — 전직 전에는 꺼져 있음
        Configure(Ensure<ShadowPierceSkill>(go), KeyCode.Z, 5f, arrow, enemies);
        Configure(Ensure<CurseBurstSkill>(go), KeyCode.X, 8f, arrow, enemies);
        Configure(Ensure<DarkSnipeSkill>(go), KeyCode.C, 14f, arrow, enemies);
        ConfigureAddon(Ensure<ShadowStepDash>(go), arrow, enemies);      // 팀 대쉬 위에 얹는 효과
        ConfigureAddon(Ensure<ShadowMarkPassive>(go), arrow, enemies);   // 패시브: 확률 표식 + 죽으면 퍼짐
        Configure(Ensure<GaleBarrageSkill>(go), KeyCode.Z, 7f, arrow, enemies);
        Configure(Ensure<WindVortexSkill>(go), KeyCode.X, 9f, arrow, enemies);
        Configure(Ensure<PiercingGaleSkill>(go), KeyCode.C, 12f, arrow, enemies);
        ConfigureAddon(Ensure<WindRushDash>(go), arrow, enemies);        // 팀 대쉬 위에 얹는 효과
        WindSpiritPassive spirit = Ensure<WindSpiritPassive>(go);
        SetRefs(spirit, arrow, enemies);
        spirit.enabled = false;

        // 4차 (FourthJob 폴더가 있으면)
        int fourth = 0;
        foreach (string name in new[] { "ThunderSpiritArrow", "StormSummon", "ShadowHomingArrow", "ShadowLegion" })
        {
            Type type = FindType(name);
            if (type == null) continue;
            Component c = go.GetComponent(type);
            if (c == null) c = Undo.AddComponent(go, type);
            SetRefs(c, arrow, enemies);
            ((Behaviour)c).enabled = false;
            fourth++;
        }

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene); // 바로 저장
        Selection.activeGameObject = go;
        Debug.Log($"[JobTierSetup] 3차 스킬 10개 + 4차 스킬 {fourth}개를 Player에 붙이고 씬을 저장했습니다. " +
                  "(테스트: 2차 전직 후 플레이 중 F9 = 3차 창, F10 = 4차 창)", go);
        EditorUtility.DisplayDialog("Game UI", "3차·4차 전직을 설정하고 씬을 저장했어요.\n(3차 Lv70 / 4차 Lv120, 테스트: 플레이 중 F9 / F10)", "확인");
    }

    private static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static void Configure(ArcherSkillBase skill, KeyCode key, float cooldown, Sprite arrow, UnityEngine.Object enemies)
    {
        SerializedObject so = new SerializedObject(skill);
        so.FindProperty("key").intValue = (int)key;
        so.FindProperty("cooldown").floatValue = cooldown;
        so.ApplyModifiedProperties();
        SetRefs(skill, arrow, enemies);
        skill.enabled = false;
    }

    private static void ConfigureAddon(Behaviour b, Sprite arrow, UnityEngine.Object enemies)
    {
        SetRefs(b, arrow, enemies);
        b.enabled = false;
    }

    private static void SetRefs(Component c, Sprite arrow, UnityEngine.Object enemies)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty a = so.FindProperty("arrowSprite");
        if (a != null && arrow != null && a.objectReferenceValue == null) a.objectReferenceValue = arrow;
        SerializedProperty e = so.FindProperty("enemyContainer");
        if (e != null && enemies != null && e.objectReferenceValue == null) e.objectReferenceValue = enemies;
        so.ApplyModifiedProperties();
    }
}
#endif
