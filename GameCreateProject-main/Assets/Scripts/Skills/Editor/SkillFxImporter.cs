#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Resources/SkillFx/ 안의 이펙트 그림을 도트가 선명하게 가져오도록 자동 설정
// (번짐 없음, 압축 없음, 밉맵 없음). 다른 폴더의 그림은 건드리지 않습니다.
public class SkillFxImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/SkillFx/"))
            return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; // 고화질 압축(BC7): 화질 거의 그대로, 메모리 1/4
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
#endif
