using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Меню «Ash & Crown → Собрать для macOS»: сборка .app в Builds/macOS и архив для игроков
/// Builds/Installer/AshAndCrown-&lt;версия&gt;-macOS.zip (внутри — игра и «Как открыть.txt»).
///
/// Почему zip, а не .dmg/.pkg: их делают и подписывают только на Mac (hdiutil, codesign, нотаризация Apple).
/// Сборка универсальная (Intel + Apple Silicon): Unity и на Windows ставит ей простую подпись (ad-hoc),
/// без которой Apple Silicon код не запустил бы. Подписи разработчика Apple нет — поэтому при первом запуске
/// macOS спрашивает (см. «Как открыть.txt»).
/// Архив пишется своим кодом: стандартный ZipArchive на Windows не сохраняет права «исполняемый файл»,
/// и распакованная игра на Mac не запустилась бы.
///
/// Нужен модуль Unity «Mac Build Support (Mono)» (Unity Hub → Installs → Add modules).
/// </summary>
public static class MacBuild
{
    private const string BuildDir = "Builds/macOS";
    private const string InstallerDir = "Builds/Installer";

    [MenuItem("Ash & Crown/Собрать для macOS")]
    private static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
        {
            EditorUtility.DisplayDialog("macOS",
                "Нет модуля «Mac Build Support (Mono)».\nUnity Hub → Installs → эта версия Unity → Add modules.", "OK");
            return;
        }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX
            && !EditorUtility.DisplayDialog("macOS",
                "Сейчас выбрана платформа " + EditorUserBuildSettings.activeBuildTarget +
                ". Unity переключится на macOS — это займёт несколько минут. Продолжить?", "Да", "Отмена"))
            return;

        string root = Directory.GetParent(Application.dataPath).FullName;
        string buildDir = Path.Combine(root, BuildDir);
        if (Directory.Exists(buildDir))
            Directory.Delete(buildDir, true);

        // Идентификатор как на телефонах (на Mac по нему хранятся сохранения). Mono: IL2CPP для Mac собирается только на Mac.
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, ProjectSetup.BundleId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        string appName = PlayerSettings.productName + ".app";
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(buildDir, appName),
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("macOS", "Сборка не удалась — подробности в консоли.", "OK");
            return;
        }

        string app = Path.Combine(buildDir, appName);
        // Тестовый AppID Steam (480) — рядом с игрой, как на ПК (см. README, «Игра через Steam»).
        string appId = Path.Combine(root, "steam_appid.txt");

        string version = PlayerSettings.bundleVersion;
        string outDir = Path.Combine(root, InstallerDir);
        Directory.CreateDirectory(outDir);
        string zip = Path.Combine(outDir, $"AshAndCrown-{version}-macOS.zip");
        string folder = "Ash & Crown/"; // в архиве всё в одной папке

        var files = new List<(string path, string name, bool exec)>();
        foreach (string f in Directory.GetFiles(app, "*", SearchOption.AllDirectories))
        {
            string rel = f.Substring(buildDir.Length + 1).Replace('\\', '/');
            // Исполняемые: сам бинарник в Contents/MacOS и библиотеки.
            // lib_burst_generated.bundle — не папка, а сама библиотека.
            bool exec = rel.Contains("/Contents/MacOS/") || rel.EndsWith(".dylib", StringComparison.Ordinal)
                        || rel.EndsWith(".bundle", StringComparison.Ordinal);
            files.Add((f, folder + rel, exec));
        }
        if (File.Exists(appId))
            files.Add((appId, folder + "steam_appid.txt", false));

        try
        {
            EditorUtility.DisplayProgressBar("macOS", "Упаковка в zip…", 0.5f);
            UnixZip.Write(zip, files, folder + "Как открыть.txt", HowToOpen);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log("Архив для macOS готов: " + zip);
        EditorUtility.RevealInFinder(zip);
    }

    private const string HowToOpen =
        "Пепел и корона / Ash & Crown — macOS\n\n" +
        "1. Перетащи «Ash & Crown.app» в папку «Программы» (Applications).\n" +
        "2. Первый запуск: правый клик (или Control+клик) по игре → «Открыть» → «Открыть».\n" +
        "   Игра не подписана в Apple, поэтому macOS спрашивает один раз.\n" +
        "   На macOS 15 и новее: попробуй открыть, затем Системные настройки → Конфиденциальность и безопасность →\n" +
        "   внизу «Ash & Crown» → «Всё равно открыть».\n" +
        "3. Если macOS пишет, что приложение «повреждено»: открой Терминал и выполни\n" +
        "   xattr -cr \"/Applications/Ash & Crown.app\"\n" +
        "   (снимает отметку «скачано из интернета»), затем снова открой игру.\n\n" +
        "Работает на macOS 12 и новее, на Mac с Intel и с Apple Silicon (M1–M4).\n\n" +
        "---\n\n" +
        "1. Drag \"Ash & Crown.app\" to Applications.\n" +
        "2. First launch: right-click the game → Open → Open (the game is not signed with Apple).\n" +
        "   On macOS 15+: try to open it, then System Settings → Privacy & Security → \"Open Anyway\".\n" +
        "3. If macOS says the app is damaged, run in Terminal:  xattr -cr \"/Applications/Ash & Crown.app\"\n";

    /// <summary>
    /// Простой zip, который сохраняет права Unix (rwxr-xr-x для исполняемых файлов): в заголовке «создано в Unix»,
    /// права — в старших 16 битах внешних атрибутов. Так архив распаковывают Finder и unzip на Mac.
    /// </summary>
    private static class UnixZip
    {
        public static void Write(string zipPath, List<(string path, string name, bool exec)> files, string textName, string text)
        {
            using (var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(stream))
            {
                var central = new List<(string name, uint crc, uint packed, uint size, uint offset, bool exec, ushort method)>();
                foreach (var f in files)
                    central.Add(AddEntry(w, f.name, File.ReadAllBytes(f.path), f.exec));
                central.Add(AddEntry(w, textName, Encoding.UTF8.GetBytes(text), false));

                uint cdStart = (uint)stream.Position;
                foreach (var e in central)
                {
                    byte[] name = Encoding.UTF8.GetBytes(e.name);
                    w.Write(0x02014b50u);
                    w.Write((ushort)((3 << 8) | 20)); // создано в Unix, версия 2.0
                    w.Write((ushort)20);
                    w.Write((ushort)0x0800);           // имена в UTF-8
                    w.Write(e.method);
                    w.Write((ushort)0); w.Write((ushort)0x21); // время/дата — 1980-01-01
                    w.Write(e.crc); w.Write(e.packed); w.Write(e.size);
                    w.Write((ushort)name.Length); w.Write((ushort)0); w.Write((ushort)0);
                    w.Write((ushort)0); w.Write((ushort)0);
                    uint mode = e.exec ? 0x81EDu : 0x81A4u; // обычный файл, 755 или 644
                    w.Write(mode << 16);
                    w.Write(e.offset);
                    w.Write(name);
                }
                uint cdSize = (uint)stream.Position - cdStart;
                w.Write(0x06054b50u);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)central.Count); w.Write((ushort)central.Count);
                w.Write(cdSize); w.Write(cdStart);
                w.Write((ushort)0);
            }
        }

        private static (string, uint, uint, uint, uint, bool, ushort) AddEntry(BinaryWriter w, string entryName, byte[] data, bool exec)
        {
            uint crc = Crc32(data);
            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var d = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                    d.Write(data, 0, data.Length);
                packed = ms.ToArray();
            }
            ushort method = 8;
            if (packed.Length >= data.Length) // не сжимается — храним как есть
            {
                packed = data;
                method = 0;
            }
            uint offset = (uint)w.BaseStream.Position;
            byte[] name = Encoding.UTF8.GetBytes(entryName);
            w.Write(0x04034b50u);
            w.Write((ushort)20);
            w.Write((ushort)0x0800);
            w.Write(method);
            w.Write((ushort)0); w.Write((ushort)0x21);
            w.Write(crc); w.Write((uint)packed.Length); w.Write((uint)data.Length);
            w.Write((ushort)name.Length); w.Write((ushort)0);
            w.Write(name);
            w.Write(packed);
            return (entryName, crc, (uint)packed.Length, (uint)data.Length, offset, exec, method);
        }

        private static uint[] sTable;

        private static uint Crc32(byte[] data)
        {
            if (sTable == null)
            {
                sTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++)
                        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    sTable[i] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data)
                crc = sTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
