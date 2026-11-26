using UnityEditor;
using UnityEngine;

/// <summary>
/// Фоны (Resources/UI/Backgrounds) импортируются как спрайты без мип-карт и без подгонки
/// под степень двойки — иначе картинка 1920x1080 растягивается и мылится.
/// Иконка игры (Art/Icon) — без сжатия, чтобы не мылилась в панели задач.
/// Курсор (Resources/UI/cursor) — тип «Cursor»: без сжатия и мип-карт, доступен для чтения.
/// </summary>
public class BackgroundImportRules : AssetPostprocessor
{
    public override uint GetVersion() => 3;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (assetPath.Contains("/Art/Icon/"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            return;
        }
        if (assetPath.EndsWith("/UI/cursor.png"))
        {
            importer.textureType = TextureImporterType.Cursor;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        if (!assetPath.Contains("/UI/Backgrounds/"))
            return;

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
