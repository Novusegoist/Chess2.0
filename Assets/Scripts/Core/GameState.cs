using System.Text;

namespace Chess2.Core
{
    public enum GameResult : byte
    {
        Ongoing,
        WhiteWins,
        BlackWins,
        Draw
    }

    public enum EndReason : byte
    {
        None,
        Checkmate,
        Annihilation,      // у стороны не осталось фигур
        Stalemate,         // ходить нечем
        NoProgress,        // долго не было ударов и ходов пешек
        OnlyGhosts,        // на доске одни призраки — никто никого не может срубить
        Resignation,
        Timeout
    }

    /// <summary>
    /// Полное состояние партии. Никакого Unity: это можно копировать, пересылать по сети,
    /// перебирать в ИИ и проверять в тестах.
    /// </summary>
    public sealed class GameState
    {
        public readonly Piece[] Cells = new Piece[64];

        /// <summary>Пассажиры троянских коней: Cargo[клетка коня]. Пусто — коня никто не занял.</summary>
        public readonly Piece[] Cargo = new Piece[64];

        public RuleSet Rules;

        /// <summary>Журнал изменений клеток для быстрой отмены хода в переборе (см. Rules.UndoMove).</summary>
        internal readonly System.Collections.Generic.List<JournalEntry> Journal =
            new System.Collections.Generic.List<JournalEntry>(32);

        internal struct JournalEntry
        {
            public int Sq;
            public Piece Cell;
            public Piece Cargo;
        }

        public Side SideToMove = Side.White;
        public int Ply;
        public int NoProgressPlies;
        public Move? LastMove;

        public GameResult Result = GameResult.Ongoing;
        public EndReason EndReason = EndReason.None;

        /// <summary>
        /// Зерно случайности партии (какие фигуры загорятся после падения короля).
        /// Одинаковое зерно — одинаковый результат, поэтому в сетевой игре его достаточно передать один раз.
        /// </summary>
        public int Seed;

        private int _nextId = 1;

        public GameState(RuleSet rules = null)
        {
            Rules = rules ?? RuleSet.Default;
        }

        public bool IsOver => Result != GameResult.Ongoing;

        public Piece this[int sq] => Cells[sq];
        public Piece this[string square] => Cells[Sq.Parse(square)];

        public static GameState CreateStandard(RuleSet rules = null, int? seed = null)
        {
            var s = new GameState(rules) { Seed = seed ?? System.Environment.TickCount };
            PieceType[] back =
            {
                PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
                PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
            };

            foreach (Side side in new[] { Side.White, Side.Black })
            {
                int home = side.HomeRank();
                int pawns = home + side.Forward();
                for (int x = 0; x < 8; x++)
                {
                    s.Put(Sq.Of(x, home), side, back[x], hasMoved: false);
                    s.Put(Sq.Of(x, pawns), side, PieceType.Pawn, hasMoved: false);
                }
            }
            return s;
        }

        /// <summary>Поставить фигуру (для расстановок, тестов, редактора позиций).</summary>
        public void Put(int sq, Side side, PieceType type, PieceType original = PieceType.None, bool hasMoved = true)
        {
            Cells[sq] = new Piece
            {
                Type = type,
                Side = side,
                OriginalType = original == PieceType.None ? type : original,
                HasMoved = hasMoved,
                Id = _nextId++
            };
        }

        public void Put(string square, Side side, PieceType type, PieceType original = PieceType.None, bool hasMoved = true) =>
            Put(Sq.Parse(square), side, type, original, hasMoved);

        /// <summary>Посадить пассажира в коня на клетке knightSquare (для расстановок и тестов).</summary>
        public void PutCargo(string knightSquare, Side side, PieceType type, PieceType original = PieceType.None)
        {
            Cargo[Sq.Parse(knightSquare)] = new Piece
            {
                Type = type,
                Side = side,
                OriginalType = original == PieceType.None ? type : original,
                HasMoved = true,
                Id = _nextId++
            };
        }

        public GameState Clone()
        {
            var c = new GameState(Rules)
            {
                SideToMove = SideToMove,
                Ply = Ply,
                NoProgressPlies = NoProgressPlies,
                LastMove = LastMove,
                Result = Result,
                EndReason = EndReason,
                _nextId = _nextId,
                Seed = Seed
            };
            System.Array.Copy(Cells, c.Cells, 64);
            System.Array.Copy(Cargo, c.Cargo, 64);
            return c;
        }

        public int FindKing(Side side)
        {
            for (int sq = 0; sq < 64; sq++)
                if (Cells[sq].Type == PieceType.King && Cells[sq].Side == side)
                    return sq;
            return Sq.None;
        }

        public int CountPieces(Side side)
        {
            int n = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                if (!Cells[sq].IsEmpty && Cells[sq].Side == side)
                    n++;
                if (!Cargo[sq].IsEmpty && Cargo[sq].Side == side)
                    n++;
            }
            return n;
        }

        /// <summary>Доска текстом (белые заглавными, чёрные строчными, G — призрак).</summary>
        public string ToAscii()
        {
            var sb = new StringBuilder();
            for (int y = 7; y >= 0; y--)
            {
                sb.Append((char)('1' + y)).Append(' ');
                for (int x = 0; x < 8; x++)
                    sb.Append(Cells[Sq.Of(x, y)].ToChar()).Append(' ');
                sb.AppendLine();
            }
            sb.AppendLine("  a b c d e f g h");
            return sb.ToString();
        }
    }
}
