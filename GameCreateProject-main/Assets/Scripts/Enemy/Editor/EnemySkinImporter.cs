using UnityEditor;
using UnityEngine;

// Resources/EnemySkins 폴더의 적 그림은 넣는 순간 자동으로 도트 설정 (선명하게, 작은 그림이라 압축 없음)
public class EnemySkinImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/EnemySkins/")) return;
        var imp = (TextureImporter)assetImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 32f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.isReadable = false;
        imp.alphaIsTransparency = true;
    }
}
