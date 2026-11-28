using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Chess2.Core
{
    /// <summary>С кем была партия (для списка прошлых партий).</summary>
    public enum RecordOpponent : byte
    {
        Bot,
        Local,
        Net
    }

    /// <summary>
    /// Сыгранная партия для повтора: начальная позиция и ходы. Позиции восстанавливаются заново
    /// по правилам (Replay), поэтому файл маленький. Формат текстовый, как у SavedGame:
    /// строки «record …» и «move …», дальше — начальная позиция в формате SavedGame.
    /// </summary>
    public sealed class GameRecord
    {
        public const int Version = 1;

        public DateTime Date;
        public RecordOpponent Opponent;
        public BotDifficulty Difficulty;

        /// <summary>За кого играл человек (вдвоём — белые): так доска и разворачивается при повторе.</summary>
        public Side PlayerSide = Side.White;

        public GameResult Result;
        public EndReason EndReason;

        /// <summary>Партию прислал друг (вставлена кодом): итог показывается как «белые/чёрные победили».</summary>
        public bool Imported;

        public GameState Start;
        public readonly List<Move> Moves = new List<Move>();

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Собрать запись из позиций партии: positions[0] — начало, positions[i] — после i-го хода
        /// (ход берётся из LastMove). null — если ходов нет или позиции неполные.
        /// </summary>
        public static GameRecord FromPositions(IReadOnlyList<GameState> positions, GameState final)
        {
            if (positions == null || positions.Count == 0 || final == null)
                return null;
            var r = new GameRecord { Start = positions[0].Clone(), Result = final.Result, EndReason = final.EndReason };
            for (int i = 1; i <= positions.Count; i++)
            {
                GameState after = i < positions.Count ? positions[i] : final;
                if (after.LastMove is not Move m)
                    return null;
                r.Moves.Add(m);
            }
            return r;
        }

        /// <summary>Победил ли человек: true/false, null — ничья (или партия вдвоём).</summary>
        public bool? PlayerWon => Result == GameResult.Draw || Opponent == RecordOpponent.Local || Imported ? (bool?)null
            : (Result == GameResult.WhiteWins) == (PlayerSide == Side.White);

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("record ").Append(Version).Append(' ').Append(Date.Ticks.ToString(Inv)).Append(' ')
              .Append((int)Opponent).Append(' ').Append((int)Difficulty).Append(' ').Append((int)PlayerSide).Append(' ')
              .Append((int)Result).Append(' ').Append((int)EndReason).Append(' ').Append(Imported ? 1 : 0).Append('\n');
            foreach (Move m in Moves)
                sb.Append("move ").Append(m.From).Append(' ').Append(m.To).Append(' ').Append((int)m.Kind).Append(' ')
                  .Append(m.Via).Append('\n');
            sb.Append(new SavedGame { State = Start }.Serialize());
            return sb.ToString();
        }

        public static bool TryParse(string text, out GameRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(text) || !SavedGame.TryParse(text, out SavedGame start))
                return false;
            try
            {
                var r = new GameRecord { Start = start.State };
                bool hasHeader = false;
                foreach (string raw in text.Split('\n'))
                {
                    string[] f = raw.Trim().Split(' ');
                    int I(int i) => int.Parse(f[i], Inv);
                    if (f[0] == "record")
                    {
                        if (I(1) != Version)
                            return false;
                        r.Date = new DateTime(long.Parse(f[2], Inv));
                        r.Opponent = (RecordOpponent)I(3);
                        r.Difficulty = (BotDifficulty)I(4);
                        r.PlayerSide = (Side)I(5);
                        r.Result = (GameResult)I(6);
                        r.EndReason = (EndReason)I(7);
                        r.Imported = f.Length > 8 && I(8) != 0; // поле появилось позже — в старых записях его нет
                        hasHeader = true;
                    }
                    else if (f[0] == "move")
                        r.Moves.Add(new Move(I(1), I(2), (MoveKind)I(3), I(4)));
                }
                if (!hasHeader || r.Moves.Count == 0)
                    return false;
                record = r;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Приставка кода партии: по ней видно, что это партия «Пепла и короны» и какой версии.</summary>
        public const string CodePrefix = "ASHC1:";

        /// <summary>
        /// Код партии, чтобы отправить другу: запись, сжатая и записанная буквами (base64url).
        /// Обычная партия — несколько сотен символов, очень длинная (400 полуходов) — около 2000;
        /// помещается в одно сообщение мессенджера (в Telegram до 4096).
        /// </summary>
        public string ToCode()
        {
            byte[] raw = Encoding.UTF8.GetBytes(Serialize());
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
                    deflate.Write(raw, 0, raw.Length);
                string b64 = Convert.ToBase64String(output.ToArray());
                return CodePrefix + b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
            }
        }

        /// <summary>
        /// Прочитать код партии. Пробелы и переносы внутри (мессенджер мог разбить строку) не мешают,
        /// текст вокруг кода — тоже. false — кода нет или он испорчен.
        /// </summary>
        public static bool TryFromCode(string text, out GameRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(text))
                return false;
            int start = text.IndexOf(CodePrefix, StringComparison.Ordinal);
            if (start < 0)
                return false;
            var sb = new StringBuilder();
            for (int i = start + CodePrefix.Length; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                    continue;
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                    break; // код кончился
                sb.Append(c);
            }
            try
            {
                string b64 = sb.ToString().Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                byte[] packed = Convert.FromBase64String(b64);
                using (var input = new MemoryStream(packed))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var reader = new StreamReader(deflate, Encoding.UTF8))
                {
                    // Защита от «бомбы»: настоящая запись — несколько килобайт.
                    char[] buf = new char[256 * 1024];
                    int n = reader.ReadBlock(buf, 0, buf.Length);
                    if (n >= buf.Length)
                        return false;
                    return TryParse(new string(buf, 0, n), out record) && record.Replay().Count > 1;
                }
            }
            catch (Exception)
            {
                record = null;
                return false;
            }
        }

        /// <summary>
        /// Все позиции партии: [0] — начало, [i] — после i-го хода. Если ход вдруг не проходит
        /// (файл от старой версии правил) — повтор обрывается на нём. Сдача, время и отключение — не ходы:
        /// итог из записи ставится последней позиции.
        /// </summary>
        public List<GameState> Replay()
        {
            var positions = new List<GameState> { Start.Clone() };
            GameState s = Start.Clone();
            foreach (Move m in Moves)
            {
                if (s.IsOver || !Rules.TryMakeMove(s, m))
                    break;
                positions.Add(s.Clone());
            }
            GameState last = positions[positions.Count - 1];
            if (!last.IsOver && Result != GameResult.Ongoing)
            {
                last.Result = Result;
                last.EndReason = EndReason;
            }
            return positions;
        }
    }
}
