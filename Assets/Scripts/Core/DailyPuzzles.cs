using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chess2.Core
{
    /// <summary>О чём задача — подсказка игроку.</summary>
    public enum PuzzleTheme
    {
        /// <summary>Поставить мат (или уронить короля в режиме «Корона»).</summary>
        Mate,
        /// <summary>Ход способностью: пробивание, двойной прыжок, взрыв, посадка в коня.</summary>
        Ability,
        /// <summary>Выиграть материал.</summary>
        Material
    }

    /// <summary>Задача: позиция, кто ходит и ходы, которые считаются решением.</summary>
    public sealed class Puzzle
    {
        public GameState State;
        public PuzzleTheme Theme;
        public readonly List<Move> Solutions = new List<Move>();

        public bool IsSolution(Move m)
        {
            foreach (Move s in Solutions)
                if (s.SameAs(m))
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Задачи дня. Они заранее найдены ботом в партиях (Tools/CoreTests: puzzles) и лежат в игре файлом,
    /// поэтому одинаковы у всех игроков в один день и работают без интернета.
    /// Формат: блоки SavedGame, разделённые строкой «===», плюс строки «theme …» и «solution from to kind via».
    /// </summary>
    public static class DailyPuzzles
    {
        public const string Separator = "===";

        /// <summary>День отсчёта: задача №0 — 1 января 2026.</summary>
        public static readonly DateTime Epoch = new DateTime(2026, 1, 1);

        public static int IndexFor(DateTime localDate, int count)
        {
            if (count <= 0)
                return -1;
            int days = (int)Math.Floor((localDate.Date - Epoch).TotalDays);
            int i = days % count;
            return i < 0 ? i + count : i;
        }

        public static string Serialize(Puzzle p)
        {
            var sb = new StringBuilder();
            sb.Append(new SavedGame { State = p.State }.Serialize());
            sb.Append("theme ").Append((int)p.Theme).Append('\n');
            foreach (Move m in p.Solutions)
                sb.Append("solution ").Append(m.From).Append(' ').Append(m.To).Append(' ')
                  .Append((int)m.Kind).Append(' ').Append(m.Via).Append('\n');
            return sb.ToString();
        }

        public static List<Puzzle> Parse(string text)
        {
            var list = new List<Puzzle>();
            if (string.IsNullOrEmpty(text))
                return list;
            foreach (string block in text.Replace("\r", "").Split(new[] { Separator + "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!SavedGame.TryParse(block, out SavedGame g))
                    continue;
                var p = new Puzzle { State = g.State };
                foreach (string line in block.Split('\n'))
                {
                    string[] f = line.Trim().Split(' ');
                    try
                    {
                        if (f[0] == "theme")
                            p.Theme = (PuzzleTheme)int.Parse(f[1], CultureInfo.InvariantCulture);
                        else if (f[0] == "solution")
                            p.Solutions.Add(new Move(int.Parse(f[1], CultureInfo.InvariantCulture), int.Parse(f[2], CultureInfo.InvariantCulture),
                                (MoveKind)int.Parse(f[3], CultureInfo.InvariantCulture), int.Parse(f[4], CultureInfo.InvariantCulture)));
                    }
                    catch (Exception)
                    {
                        // битая строка — пропускаем
                    }
                }
                if (p.Solutions.Count > 0)
                    list.Add(p);
            }
            return list;
        }
    }
}
