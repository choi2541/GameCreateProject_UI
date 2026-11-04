using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Game UI > Setup Player Animation
// Assets/Art/Player/player_*.png 그림 7장을 알맞게 가져오기 설정하고, Player에 PlayerSpriteAnimator를 붙여 연결 + 씬 저장.
public static class PlayerAnimSetup
{
    private const string Folder = "Assets/Art/Player";

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
        so.FindProperty("idle").FindPropertyRelative("fps").floatValue = 4f; // 서 있기: 4칸으로 살짝 숨쉬기 (0 = 멈춤)
        so.FindProperty("idle").FindPropertyRelative("facesRight").boolValue = true; // 서 있기 그림은 오른쪽을 봄 → 왼쪽 볼 땐 뒤집기
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        EditorUtility.DisplayDialog("주인공 애니메이션",
            found == 7 ? "그림 7장을 연결하고 씬을 저장했어요.\n플레이해서 걸어 보세요!"
                       : "그림 " + found + "/7장만 찾았어요.\n" + Folder + " 폴더에 player_*.png가 있는지 확인해 주세요.", "확인");
    }

    private static int Link(SerializedObject so, string field, string file, int frames)
    {
        string path = Folder + "/" + file + ".png";
        if (!File.Exists(path)) return 0;
        Import(path);
        SerializedProperty clip = so.FindProperty(field);
        clip.FindPropertyRelative("strip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        clip.FindPropertyRelative("frames").intValue = frames;
        return 1;
    }

    // 그림 줄 가져오기 설정: 실행 중 칸을 잘라 쓰므로 일반 텍스처, 작게 보여도 깨끗하게 밉맵, 고화질 압축
    private static void Import(string path)
    {
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = imp.textureType != TextureImporterType.Default || !imp.mipmapEnabled || imp.isReadable
            || imp.filterMode != FilterMode.Bilinear || imp.textureCompression != TextureImporterCompression.CompressedHQ
            || imp.npotScale != TextureImporterNPOTScale.None || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp;
        if (!changed) return;
        imp.textureType = TextureImporterType.Default;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.isReadable = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
    }
}
