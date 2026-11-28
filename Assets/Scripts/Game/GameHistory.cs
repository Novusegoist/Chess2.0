using System;
using System.Collections.Generic;
using System.IO;
using Chess2.Core;
using UnityEngine;

/// <summary>
/// Прошлые партии: каждая законченная партия (с ботом, вдвоём, по сети) — отдельный файл в папке
/// history в папке данных игры (на ПК — папка пользователя, на телефоне — личная папка приложения).
/// Хранятся последние Limit партий, старые удаляются. Уроки и задачи дня сюда не попадают.
/// </summary>
public static class GameHistory
{
    public const int Limit = 50;

    private static string Folder => Path.Combine(Application.persistentDataPath, "history");

    public static void Add(GameRecord record)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string name = record.Date.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
            File.WriteAllText(Path.Combine(Folder, name), record.Serialize());

            // Лишние — самые старые (имена файлов сортируются по дате).
            string[] files = Directory.GetFiles(Folder, "*.txt");
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length - Limit; i++)
                File.Delete(files[i]);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось записать партию в историю: " + e.Message);
        }
    }

    /// <summary>Все сохранённые партии, новые сверху. Повреждённые файлы пропускаются.</summary>
    public static List<GameRecord> Load()
    {
        var list = new List<GameRecord>();
        try
        {
            if (!Directory.Exists(Folder))
                return list;
            string[] files = Directory.GetFiles(Folder, "*.txt");
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = files.Length - 1; i >= 0; i--)
                if (GameRecord.TryParse(File.ReadAllText(files[i]), out GameRecord r))
                    list.Add(r);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось прочитать историю партий: " + e.Message);
        }
        return list;
    }
}
