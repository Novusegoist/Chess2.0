#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Дописывает в Info.plist сборки для iOS то, чего нет в настройках Unity:
/// - объяснение доступа к локальной сети (игра по IP в одной сети Wi-Fi, iOS 14+ спрашивает разрешение);
/// - отказ от шифрования, кроме стандартного (иначе App Store Connect спрашивает об этом при каждой загрузке).
/// </summary>
public static class IosBuildSettings
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        PlistElementDict root = plist.root;
        root.SetString("NSLocalNetworkUsageDescription",
            "Нужно для игры с другом по сети Wi-Fi.");
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
