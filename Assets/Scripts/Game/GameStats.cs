using Chess2.Core;
using UnityEngine;

/// <summary>
/// Статистика партий: против каждого уровня бота, по сети и вдвоём, серии побед.
/// Хранится в PlayerPrefs — работает одинаково на ПК, Android и iOS.
/// </summary>
public static class GameStats
{
    private const string Prefix = "chess2.stats.";

    /// <summary>Строки таблицы: 0–3 — уровни бота, 4 — по сети, 5 — вдвоём.</summary>
    public const int NetRow = 4;
    public const int LocalRow = 5;
    public const int Rows = 6;

    public enum Outcome { Win, Loss, Draw }

    public static int Get(int row, Outcome outcome) => PlayerPrefs.GetInt(Key(row, outcome), 0);

    public static int CurrentStreak => PlayerPrefs.GetInt(Prefix + "streak", 0);
    public static int BestStreak => PlayerPrefs.GetInt(Prefix + "bestStreak", 0);
    public static int KingsFelled => PlayerPrefs.GetInt(Prefix + "kingsFelled", 0);

    public static int TotalGames
    {
        get
        {
            int n = 0;
            for (int r = 0; r < Rows; r++)
                n += Get(r, Outcome.Win) + Get(r, Outcome.Loss) + Get(r, Outcome.Draw);
            return n;
        }
    }

    /// <summary>
    /// Записать итог партии. Для игры вдвоём Win/Loss — это победа белых/чёрных (там нет «нас»).
    /// Серия побед считается только в партиях против бота и по сети.
    /// </summary>
    public static void Record(int row, Outcome outcome)
    {
        string key = Key(row, outcome);
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);

        if (row != LocalRow)
        {
            int streak = outcome == Outcome.Win ? CurrentStreak + 1 : 0;
            PlayerPrefs.SetInt(Prefix + "streak", streak);
            if (streak > BestStreak)
                PlayerPrefs.SetInt(Prefix + "bestStreak", streak);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Мы поставили мат чужому королю (он пал, армия загорелась) — против бота или по сети.</summary>
    public static void AddKingFelled()
    {
        PlayerPrefs.SetInt(Prefix + "kingsFelled", KingsFelled + 1);
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        for (int r = 0; r < Rows; r++)
            foreach (Outcome o in new[] { Outcome.Win, Outcome.Loss, Outcome.Draw })
                PlayerPrefs.DeleteKey(Key(r, o));
        PlayerPrefs.DeleteKey(Prefix + "streak");
        PlayerPrefs.DeleteKey(Prefix + "bestStreak");
        PlayerPrefs.DeleteKey(Prefix + "kingsFelled");
        PlayerPrefs.Save();
    }

    public static int BotRow(BotDifficulty d) => Mathf.Clamp((int)d, 0, 3);

    private static string Key(int row, Outcome outcome) => Prefix + row + "." + outcome;
}
