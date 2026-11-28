using System;
using System.Collections.Generic;
using System.Globalization;
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
        public bool? PlayerWon => Result == GameResult.Draw || Opponent == RecordOpponent.Local ? (bool?)null
            : (Result == GameResult.WhiteWins) == (PlayerSide == Side.White);

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("record ").Append(Version).Append(' ').Append(Date.Ticks.ToString(Inv)).Append(' ')
              .Append((int)Opponent).Append(' ').Append((int)Difficulty).Append(' ').Append((int)PlayerSide).Append(' ')
              .Append((int)Result).Append(' ').Append((int)EndReason).Append('\n');
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
