using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Меню «Ash & Crown → Собрать установщик для ПК»: чистая сборка Windows x64 в Builds/Windows
/// и установщик Inno Setup в Builds/Installer (AshAndCrown-Setup-&lt;версия&gt;.exe).
/// Нужен Inno Setup 6 (https://jrsoftware.org/isinfo.php). Скрипт установщика — Tools/Installer/AshAndCrown.iss.
/// </summary>
public static class WindowsInstaller
{
    private const string BuildDir = "Builds/Windows";
    private const string InstallerDir = "Builds/Installer";
    private const string Script = "Tools/Installer/AshAndCrown.iss";

    [MenuItem("Ash & Crown/Собрать установщик для ПК")]
    private static void BuildInstaller()
    {
        string iscc = FindIscc();
        if (iscc == null)
        {
            EditorUtility.DisplayDialog("Установщик", "Не найден Inno Setup 6 (ISCC.exe).\nУстанови его с jrsoftware.org и попробуй снова.", "OK");
            return;
        }

        // Сборка под Windows при другой выбранной платформе (Android/iOS) переключит проект — это несколько минут.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64
            && !EditorUtility.DisplayDialog("Установщик",
                "Сейчас выбрана платформа " + EditorUserBuildSettings.activeBuildTarget +
                ". Unity переключится на Windows — это займёт несколько минут. Продолжить?", "Да", "Отмена"))
            return;

        string root = Directory.GetParent(Application.dataPath).FullName;
        string buildDir = Path.Combine(root, BuildDir);
        if (Directory.Exists(buildDir))
            Directory.Delete(buildDir, true); // чистая сборка — без файлов от прошлых версий

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(buildDir, PlayerSettings.productName + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Установщик", "Сборка не удалась — подробности в консоли.", "OK");
            return;
        }

        // Тестовый AppID Steam рядом с игрой (см. README, «Игра через Steam»).
        string appId = Path.Combine(root, "steam_appid.txt");
        if (File.Exists(appId))
            File.Copy(appId, Path.Combine(buildDir, "steam_appid.txt"), true);

        string version = PlayerSettings.bundleVersion;
        string outDir = Path.Combine(root, InstallerDir);
        Directory.CreateDirectory(outDir);
        var psi = new ProcessStartInfo(iscc,
            $"/Q \"/DSourceDir={buildDir}\" \"/DAppVersion={version}\" \"/DOutputDir={outDir}\" \"{Path.Combine(root, Script)}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        EditorUtility.DisplayProgressBar("Установщик", "Inno Setup сжимает файлы…", 0.5f);
        try
        {
            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    Debug.LogError("Inno Setup:\n" + output);
                    EditorUtility.DisplayDialog("Установщик", "Inno Setup завершился с ошибкой — подробности в консоли.", "OK");
                    return;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string setup = Path.Combine(outDir, $"AshAndCrown-Setup-{version}.exe");
        Debug.Log("Установщик готов: " + setup);
        EditorUtility.RevealInFinder(setup);
    }

    private static string FindIscc()
    {
        string[] candidates =
        {
            @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            @"C:\Program Files\Inno Setup 6\ISCC.exe",
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Inno Setup 6\ISCC.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
