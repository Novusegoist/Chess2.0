using UnityEditor;
using UnityEngine;

/// <summary>
/// Фоны (Resources/UI/Backgrounds) импортируются как спрайты без мип-карт и без подгонки
/// под степень двойки — иначе картинка 1920x1080 растягивается и мылится.
/// </summary>
public class BackgroundImportRules : AssetPostprocessor
{
    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/UI/Backgrounds/"))
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
