using UnityEditor;
using UnityEngine;

/// <summary>
/// Настройки импорта звука:
/// - музыка (Resources/Audio/Music) проигрывается потоково с диска — длинные треки не занимают
///   десятки мегабайт памяти;
/// - короткие звуки (Resources/Audio/Sfx) распаковываются при загрузке — играют без задержки.
/// </summary>
public class AudioImportRules : AssetPostprocessor
{
    // Версия правил: при её смене Unity сам переимпортирует звуки с новыми настройками.
    public override uint GetVersion() => 1;

    void OnPreprocessAudio()
    {
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (assetPath.Contains("/Audio/Music/"))
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.loadInBackground = true;
        }
        else if (assetPath.Contains("/Audio/Sfx/"))
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.forceToMono = true;
        }
        else
            return;

        importer.defaultSampleSettings = settings;
    }
}
