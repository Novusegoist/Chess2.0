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
        PlayerPrefs.DeleteKey(Prefix + "rating");
        PlayerPrefs.DeleteKey(Prefix + "ratingPeak");
        PlayerPrefs.DeleteKey(Prefix + "ratedGames");
        PlayerPrefs.DeleteKey(Prefix + "ratingHistory");
        PlayerPrefs.Save();
    }

    #region Рейтинг

    public static int Rating => PlayerPrefs.GetInt(Prefix + "rating", Elo.Start);
    public static int PeakRating => PlayerPrefs.GetInt(Prefix + "ratingPeak", Elo.Start);
    public static int RatedGames => PlayerPrefs.GetInt(Prefix + "ratedGames", 0);

    /// <summary>Рейтинг после каждой из последних HistoryLength партий (для графика), старые — первыми.</summary>
    public static int[] RatingHistory
    {
        get
        {
            string s = PlayerPrefs.GetString(Prefix + "ratingHistory", "");
            if (s.Length == 0)
                return new int[0];
            string[] parts = s.Split(',');
            var list = new System.Collections.Generic.List<int>(parts.Length);
            foreach (string p in parts)
                if (int.TryParse(p, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int v))
                    list.Add(v);
            return list.ToArray();
        }
    }

    public const int HistoryLength = 40;

    /// <summary>Учесть рейтинговую партию. score: 1 — победа, 0.5 — ничья, 0 — поражение. Возвращает изменение.</summary>
    public static int ApplyRated(int opponentRating, double score)
    {
        int before = Rating;
        int delta = Elo.Change(before, opponentRating, score, RatedGames);
        int after = before + delta;
        PlayerPrefs.SetInt(Prefix + "rating", after);
        PlayerPrefs.SetInt(Prefix + "ratedGames", RatedGames + 1);
        if (after > PeakRating)
            PlayerPrefs.SetInt(Prefix + "ratingPeak", after);

        var history = new System.Collections.Generic.List<int>(RatingHistory);
        if (history.Count == 0)
            history.Add(before); // начальная точка графика
        history.Add(after);
        if (history.Count > HistoryLength)
            history.RemoveRange(0, history.Count - HistoryLength);
        PlayerPrefs.SetString(Prefix + "ratingHistory", string.Join(",", history));
        PlayerPrefs.Save();
        return delta;
    }

    #endregion

    public static int BotRow(BotDifficulty d) => Mathf.Clamp((int)d, 0, 3);

    private static string Key(int row, Outcome outcome) => Prefix + row + "." + outcome;
}
