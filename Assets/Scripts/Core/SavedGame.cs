using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Chess2.Core
{
    /// <summary>
    /// Сохранённая партия: позиция со всеми флагами фигур, правила, с кем играем и время на часах.
    /// Формат — простой текст «ключ значения…» построчно: его легко читать глазами и не страшно
    /// расширять (неизвестные строки при загрузке пропускаются).
    /// </summary>
    public sealed class SavedGame
    {
        public const int Version = 1;

        public GameState State;
        public bool VsBot;
        public Side BotSide = Side.Black;
        public BotDifficulty Difficulty = BotDifficulty.Medium;
        public int TimeMinutes;
        public double WhiteSeconds;
        public double BlackSeconds;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string Serialize()
        {
            GameState s = State;
            RuleSet r = s.Rules;
            var sb = new StringBuilder();
            void Line(string key, params object[] values)
            {
                sb.Append(key);
                foreach (object v in values)
                    sb.Append(' ').Append(Convert.ToString(v, Inv));
                sb.Append('\n');
            }
            int B(bool v) => v ? 1 : 0;

            Line("version", Version);
            Line("game", B(VsBot), (int)BotSide, (int)Difficulty, TimeMinutes);
            Line("clock", WhiteSeconds.ToString("R", Inv), BlackSeconds.ToString("R", Inv));
            Line("rules", (int)r.WinCondition, B(r.AbilitiesForAll), r.EmpowerMin, r.EmpowerMax, B(r.QueenMovesAsKnight),
                B(r.EnablePierce), B(r.PierceThroughOwnPieces), B(r.TrojanKnight), (int)r.PawnPromotionFallback,
                r.NoProgressDrawPlies);
            Line("state", (int)s.SideToMove, s.Ply, s.NoProgressPlies, (int)s.Result, (int)s.EndReason, s.Seed, s.NextId);
            if (s.LastMove is Move m)
                Line("last", m.From, m.To, (int)m.Kind, m.Via);
            for (int sq = 0; sq < 64; sq++)
            {
                if (!s.Cells[sq].IsEmpty)
                    PieceLine(sb, "cell", sq, s.Cells[sq]);
                if (!s.Cargo[sq].IsEmpty)
                    PieceLine(sb, "cargo", sq, s.Cargo[sq]);
            }
            return sb.ToString();
        }

        private static void PieceLine(StringBuilder sb, string key, int sq, Piece p)
        {
            int flags = (p.HasMoved ? 1 : 0) | (p.PierceUsed ? 2 : 0) | (p.DoubleUsed ? 4 : 0) | (p.Empowered ? 8 : 0);
            sb.Append(key).Append(' ').Append(sq).Append(' ').Append((int)p.Type).Append(' ').Append((int)p.Side)
              .Append(' ').Append((int)p.OriginalType).Append(' ').Append(flags).Append(' ').Append(p.Id).Append('\n');
        }

        /// <summary>Прочитать сохранение. false — файл повреждён или от несовместимой версии.</summary>
        public static bool TryParse(string text, out SavedGame game)
        {
            game = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            try
            {
                var g = new SavedGame();
                var rules = new RuleSet();
                var s = new GameState(rules);
                bool hasVersion = false, hasState = false;

                foreach (string raw in text.Split('\n'))
                {
                    string[] f = raw.Trim().Split(' ');
                    if (f.Length == 0 || f[0].Length == 0)
                        continue;
                    int I(int i) => int.Parse(f[i], Inv);
                    bool Bo(int i) => I(i) != 0;

                    switch (f[0])
                    {
                        case "version":
                            if (I(1) != Version)
                                return false;
                            hasVersion = true;
                            break;
                        case "game":
                            g.VsBot = Bo(1);
                            g.BotSide = (Side)I(2);
                            g.Difficulty = (BotDifficulty)I(3);
                            g.TimeMinutes = I(4);
                            break;
                        case "clock":
                            g.WhiteSeconds = double.Parse(f[1], Inv);
                            g.BlackSeconds = double.Parse(f[2], Inv);
                            break;
                        case "rules":
                            rules.WinCondition = (WinCondition)I(1);
                            rules.AbilitiesForAll = Bo(2);
                            rules.EmpowerMin = I(3);
                            rules.EmpowerMax = I(4);
                            rules.QueenMovesAsKnight = Bo(5);
                            rules.EnablePierce = Bo(6);
                            rules.PierceThroughOwnPieces = Bo(7);
                            rules.TrojanKnight = Bo(8);
                            rules.PawnPromotionFallback = (PieceType)I(9);
                            rules.NoProgressDrawPlies = I(10);
                            break;
                        case "state":
                            s.SideToMove = (Side)I(1);
                            s.Ply = I(2);
                            s.NoProgressPlies = I(3);
                            s.Result = (GameResult)I(4);
                            s.EndReason = (EndReason)I(5);
                            s.Seed = I(6);
                            s.NextId = I(7);
                            hasState = true;
                            break;
                        case "last":
                            s.LastMove = new Move(I(1), I(2), (MoveKind)I(3), I(4));
                            break;
                        case "cell":
                        case "cargo":
                        {
                            int sq = I(1);
                            if (sq < 0 || sq >= 64)
                                return false;
                            int flags = I(5);
                            var p = new Piece
                            {
                                Type = (PieceType)I(2),
                                Side = (Side)I(3),
                                OriginalType = (PieceType)I(4),
                                HasMoved = (flags & 1) != 0,
                                PierceUsed = (flags & 2) != 0,
                                DoubleUsed = (flags & 4) != 0,
                                Empowered = (flags & 8) != 0,
                                Id = I(6)
                            };
                            if (f[0] == "cell")
                                s.Cells[sq] = p;
                            else
                                s.Cargo[sq] = p;
                            break;
                        }
                    }
                }

                if (!hasVersion || !hasState || s.CountPieces(Side.White) + s.CountPieces(Side.Black) == 0)
                    return false;
                g.State = s;
                game = g;
                return true;
            }
            catch (Exception)
            {
                return false; // повреждённый файл — просто нет сохранения
            }
        }
    }
}
