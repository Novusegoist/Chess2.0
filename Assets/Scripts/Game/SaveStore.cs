using System;
using System.IO;
using Chess2.Core;
using UnityEngine;

/// <summary>
/// Файл с незаконченной партией (против бота или вдвоём) в папке данных игры.
/// На ПК это папка пользователя, на телефоне — личная папка приложения; работает везде одинаково.
/// Сетевые партии и уроки не сохраняются.
/// </summary>
public static class SaveStore
{
    private static string FilePath => Path.Combine(Application.persistentDataPath, "savegame.txt");

    public static bool Exists => File.Exists(FilePath);

    public static void Save(SavedGame game)
    {
        try
        {
            // Сначала во временный файл, потом подмена — чтобы при сбое не остаться с половиной файла.
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, game.Serialize());
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось сохранить партию: " + e.Message);
        }
    }

    public static bool TryLoad(out SavedGame game)
    {
        game = null;
        try
        {
            if (!Exists)
                return false;
            if (SavedGame.TryParse(File.ReadAllText(FilePath), out game) && !game.State.IsOver)
                return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось прочитать сохранение: " + e.Message);
        }
        Delete(); // повреждённое или законченное — убираем, чтобы не висела кнопка «Продолжить»
        game = null;
        return false;
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось удалить сохранение: " + e.Message);
        }
    }
}
