using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Game UI > Setup Player Animation
// Assets/Art/Player/player_*.png 그림 7장을 알맞게 가져오기 설정하고, Player에 PlayerSpriteAnimator를 붙여 연결 + 씬 저장.
public static class PlayerAnimSetup
{
    private const string Folder = "Assets/Art/Player";
    private const string NoviceFolder = "Assets/Art/Novice";
    // 밉맵 선명도: 0 = 기본(작게 보이면 흐림), -0.7 = 선명 (너무 낮추면 움직일 때 자글거림)
    private const float MipBias = -0.7f;

    [MenuItem("Tools/Game UI/Setup Player Animation")]
    public static void Setup()
    {
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (player == null)
        {
            EditorUtility.DisplayDialog("주인공 애니메이션", "게임 씬(GameCreateProject)을 열고 다시 눌러 주세요.\n(Player를 못 찾았어요)", "확인");
            return;
        }

        PlayerSpriteAnimator anim = player.GetComponent<PlayerSpriteAnimator>();
        if (anim == null) anim = Undo.AddComponent<PlayerSpriteAnimator>(player.gameObject);

        var so = new SerializedObject(anim);
        int found = 0;
        found += Link(so, "idle", "player_idle", 4);
        found += Link(so, "walkDown", "player_walk_down", 6);
        found += Link(so, "walkUp", "player_walk_up", 6);
        found += Link(so, "walkRight", "player_walk_right", 6);
        found += Link(so, "walkLeft", "player_walk_left", 6);
        found += Link(so, "shoot", "player_shoot", 8);
        found += Link(so, "hurt", "player_hurt", 6);
        // 전직 전 (초보) 그림
        int novice = 0;
        novice += Link(so, "noviceIdle", "player_dummy", 0, NoviceFolder + "/novice_idle.png", 4);
        novice += Link(so, "noviceWalkDown", null, 0, NoviceFolder + "/novice_walk_down.png", 6);
        novice += Link(so, "noviceWalkUp", null, 0, NoviceFolder + "/novice_walk_up.png", 6);
        novice += Link(so, "noviceWalkRight", null, 0, NoviceFolder + "/novice_walk_right.png", 6);
        novice += Link(so, "noviceWalkLeft", null, 0, NoviceFolder + "/novice_walk_left.png", 6);
        novice += Link(so, "noviceHurt", null, 0, NoviceFolder + "/novice_hurt.png", 6);
        string[] attacks = { "attack_thrust", "attack_slash", "attack_down" };
        string[] fx = { "fx_thrust", "fx_slash", "fx_down" };
        foreach (string arr in new[] { "noviceAttacks", "noviceAttackFx" })
            if (so.FindProperty(arr).arraySize < 3) so.FindProperty(arr).arraySize = 3;
        SerializedProperty hits = so.FindProperty("noviceHitFrame"), place = so.FindProperty("noviceFxPlace");
        int[] hitDefault = { 3, 3, 4 };
        Vector3[] placeDefault = { new Vector3(0.62f, 0.47f, 1.17f), new Vector3(0.36f, 0.5f, 0.94f), new Vector3(0.2f, 0.03f, 1.02f) };
        if (hits.arraySize < 3) { hits.arraySize = 3; for (int i = 0; i < 3; i++) hits.GetArrayElementAtIndex(i).intValue = hitDefault[i]; }
        if (place.arraySize < 3) { place.arraySize = 3; for (int i = 0; i < 3; i++) place.GetArrayElementAtIndex(i).vector3Value = placeDefault[i]; }
        for (int i = 0; i < 3; i++)
        {
            novice += Link(so, "noviceAttacks.Array.data[" + i + "]", null, 0, NoviceFolder + "/novice_" + attacks[i] + ".png", 6);
            so.FindProperty("noviceAttacks").GetArrayElementAtIndex(i).FindPropertyRelative("fps").floatValue = 12f;
            so.FindProperty("noviceAttackFx").GetArrayElementAtIndex(i).FindPropertyRelative("fps").floatValue = 24f;
            novice += Link(so, "noviceAttackFx.Array.data[" + i + "]", null, 0, NoviceFolder + "/Fx/novice_" + fx[i] + ".png", 6);
        }
        so.FindProperty("idle").FindPropertyRelative("fps").floatValue = 4f;
        // 그림 줄은 가볍게 줄인 크기 (캐릭터 키 128픽셀, 발 아래 여백 3픽셀)
        so.FindProperty("characterPixelHeight").floatValue = 128f;
        so.FindProperty("feetMarginPixels").floatValue = 3f; // 서 있기: 4칸으로 살짝 숨쉬기 (0 = 멈춤)
        so.FindProperty("idle").FindPropertyRelative("facesRight").boolValue = true; // 서 있기 그림은 오른쪽을 봄 → 왼쪽 볼 땐 뒤집기
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        EditorUtility.DisplayDialog("주인공 애니메이션",
            found == 7 ? "그림 7장을 연결하고 씬을 저장했어요.\n플레이해서 걸어 보세요!"
                       : "그림 " + found + "/7장만 찾았어요.\n" + Folder + " 폴더에 player_*.png가 있는지 확인해 주세요.", "확인");
        if (novice < 12) Debug.LogWarning("[주인공 애니메이션] 전직 전 그림 " + novice + "/12장만 찾았어요 (" + NoviceFolder + ")");
    }

    private static int Link(SerializedObject so, string field, string file, int frames) => Link(so, field, null, 0, Folder + "/" + file + ".png", frames);

    private static int Link(SerializedObject so, string field, string unused, int unused2, string path, int frames)
    {
        if (!File.Exists(path)) return 0;
        Import(path);
        SerializedProperty clip = so.FindProperty(field);
        clip.FindPropertyRelative("strip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        clip.FindPropertyRelative("frames").intValue = frames;
        return 1;
    }

    // 그림 줄 가져오기 설정: 실행 중 칸을 잘라 쓰므로 일반 텍스처, 밉맵(선명하게 -0.7), 고화질 압축
    private static void Import(string path)
    {
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = imp.textureType != TextureImporterType.Default || !imp.mipmapEnabled || imp.isReadable
            || imp.filterMode != FilterMode.Bilinear || imp.textureCompression != TextureImporterCompression.CompressedHQ
            || imp.npotScale != TextureImporterNPOTScale.None || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp
            || !Mathf.Approximately(imp.mipMapBias, MipBias);
        if (!changed) return;
        imp.textureType = TextureImporterType.Default;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.mipMapBias = MipBias;   // 작게 보일 때 덜 흐리게 (밉맵을 한 단계 덜 씀)
        imp.isReadable = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
    }
}
