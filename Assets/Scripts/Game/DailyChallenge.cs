using System;
using System.Collections.Generic;
using Chess2.Core;
using UnityEngine;

/// <summary>
/// «Задача дня»: каждый день новая позиция из Resources/Puzzles/daily.txt (одна и та же у всех игроков
/// в этот день), серия дней подряд и достижения. Решается как урок: неверный ход откатывается с подсказкой.
/// Прогресс хранится в PlayerPrefs — одинаково на ПК, Android и iOS.
/// </summary>
public static class DailyChallenge
{
    private const string Prefix = "chess2.daily.";
    private static List<Puzzle> sPuzzles;

    private static List<Puzzle> Puzzles
    {
        get
        {
            if (sPuzzles == null)
            {
                var asset = Resources.Load<TextAsset>("Puzzles/daily");
                sPuzzles = asset != null ? DailyPuzzles.Parse(asset.text) : new List<Puzzle>();
            }
            return sPuzzles;
        }
    }

    public static bool Available => Puzzles.Count > 0;

    private static DateTime Today => DateTime.Now.Date;
    private static int DayNumber(DateTime d) => (int)(d - DailyPuzzles.Epoch).TotalDays;

    public static bool SolvedToday => PlayerPrefs.GetInt(Prefix + "last", int.MinValue) == DayNumber(Today);
    public static int Streak
    {
        get
        {
            // Серия жива, если последняя решённая задача — сегодня или вчера.
            int last = PlayerPrefs.GetInt(Prefix + "last", int.MinValue);
            return last >= DayNumber(Today) - 1 ? PlayerPrefs.GetInt(Prefix + "streak", 0) : 0;
        }
    }
    public static int BestStreak => PlayerPrefs.GetInt(Prefix + "best", 0);
    public static int TotalSolved => PlayerPrefs.GetInt(Prefix + "total", 0);

    /// <summary>Задача решена: серия, рекорд, достижения.</summary>
    public static void MarkSolved()
    {
        if (SolvedToday)
            return;
        int today = DayNumber(Today);
        int last = PlayerPrefs.GetInt(Prefix + "last", int.MinValue);
        int streak = last == today - 1 ? PlayerPrefs.GetInt(Prefix + "streak", 0) + 1 : 1;
        PlayerPrefs.SetInt(Prefix + "last", today);
        PlayerPrefs.SetInt(Prefix + "streak", streak);
        PlayerPrefs.SetInt(Prefix + "total", TotalSolved + 1);
        if (streak > BestStreak)
            PlayerPrefs.SetInt(Prefix + "best", streak);
        PlayerPrefs.Save();

        Achievements.Unlock("daily_first");
        if (streak >= 7)
            Achievements.Unlock("daily_week");
    }

    private static readonly string[] MonthsRu =
        { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
    private static readonly string[] MonthsEn =
        { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    public static string DateText(DateTime d) =>
        L.English ? $"{MonthsEn[d.Month - 1]} {d.Day}" : $"{d.Day} {MonthsRu[d.Month - 1]}";

    /// <summary>Сегодняшняя задача в виде урока из одного шага. null — задач нет.</summary>
    public static Lesson BuildTodayLesson()
    {
        if (!Available)
            return null;
        DateTime today = Today;
        Puzzle puzzle = Puzzles[DailyPuzzles.IndexFor(today, Puzzles.Count)];
        GameState start = puzzle.State;
        Side side = start.SideToMove;

        string theme;
        switch (puzzle.Theme)
        {
            case PuzzleTheme.Mate: theme = L.T("Найди ход, после которого король соперника падёт."); break;
            case PuzzleTheme.Ability: theme = L.T("Найди сильный ход способностью загоревшейся фигуры."); break;
            default: theme = L.T("Найди ход, который выигрывает материал."); break;
        }
        string mode = start.Rules.WinCondition == WinCondition.Annihilation ? L.T("Режим «Хаос».") : L.T("Режим «Корона».");
        Move first = puzzle.Solutions[0];
        string pieceHint = L.F("Ходит {0} с {1}", GameManager.PieceNameOf(start.Cells[first.From].Type), Sq.Name(first.From));

        return new Lesson
        {
            Id = "daily",
            IsDaily = true,
            PlayerSide = side,
            Title = L.F("Задача дня · {0}", DateText(today)),
            Intro = L.F("Ходят {0}. {1}", side == Side.White ? L.T("белые") : L.T("чёрные"), theme) + "\n" + mode,
            Setup = () => start.Clone(),
            Steps = new[]
            {
                new LessonStep
                {
                    Task = L.T("Найди лучший ход"),
                    Hint = pieceHint,
                    Done = L.F("Верно: {0}!", GameManager.DescribeMove(start, first)),
                    IsDone = (s, m, e) => puzzle.IsSolution(m)
                }
            },
            Outro = ""
        };
    }

    /// <summary>Текст после решения: серия и приглашение завтра.</summary>
    public static string OutroText() =>
        L.F("Серия: {0} дн. подряд (рекорд — {1}). Новая задача — завтра.", Streak, BestStreak);
}
