namespace Chess2.Core
{
    public enum Side : byte
    {
        White = 0,
        Black = 1
    }

    /// <summary>
    /// Порядок значений = цепочка деградации снизу вверх:
    /// Король → Ферзь → Ладья → Слон → Конь → Пешка → Призрак → (убит).
    /// </summary>
    public enum PieceType : byte
    {
        None = 0,
        Ghost = 1,
        Pawn = 2,
        Knight = 3,
        Bishop = 4,
        Rook = 5,
        Queen = 6,
        King = 7
    }

    public static class SideExtensions
    {
        public static Side Opponent(this Side side) => side == Side.White ? Side.Black : Side.White;

        /// <summary>Направление движения пешек по Y: белые вверх (+1), чёрные вниз (-1).</summary>
        public static int Forward(this Side side) => side == Side.White ? 1 : -1;

        /// <summary>Последняя горизонталь, на которой пешка «возрождается».</summary>
        public static int LastRank(this Side side) => side == Side.White ? 7 : 0;

        public static int HomeRank(this Side side) => side == Side.White ? 0 : 7;
    }

    public static class PieceTypeExtensions
    {
        /// <summary>На ступень слабее. Призрак деградирует в None, то есть покидает доску.</summary>
        public static PieceType Degrade(this PieceType type) =>
            type <= PieceType.Ghost ? PieceType.None : (PieceType)((byte)type - 1);

        public static char ToChar(this PieceType type)
        {
            switch (type)
            {
                case PieceType.Ghost: return 'G';
                case PieceType.Pawn: return 'P';
                case PieceType.Knight: return 'N';
                case PieceType.Bishop: return 'B';
                case PieceType.Rook: return 'R';
                case PieceType.Queen: return 'Q';
                case PieceType.King: return 'K';
                default: return '.';
            }
        }
    }

    /// <summary>
    /// Фигура на доске. Это значение (struct), поэтому копирование доски дешёвое — важно для ИИ.
    /// </summary>
    public struct Piece
    {
        public PieceType Type;
        public Side Side;

        /// <summary>Кем фигура была в начале партии. Нужна для «возрождения» пешки.</summary>
        public PieceType OriginalType;

        public bool HasMoved;

        /// <summary>Слон/ладья уже использовали пробивание (раз за игру на фигуру).</summary>
        public bool PierceUsed;

        /// <summary>Конь уже использовал двойной прыжок (раз за игру на фигуру).</summary>
        public bool DoubleUsed;

        /// <summary>
        /// Фигура «загорелась» после падения своего короля и получила способность своего типа.
        /// Флаг остаётся при деградации: ослабленный ферзь станет ладьёй с пробиванием.
        /// </summary>
        public bool Empowered;

        /// <summary>Постоянный id фигуры — по нему UI находит свой объект и анимирует его.</summary>
        public int Id;

        public bool IsEmpty => Type == PieceType.None;

        public char ToChar()
        {
            char c = Type.ToChar();
            return Side == Side.Black ? char.ToLowerInvariant(c) : c;
        }

        public override string ToString() => IsEmpty ? "." : $"{Side} {Type} #{Id}";
    }

    /// <summary>Клетки — индексы 0..63, index = y * 8 + x. x — вертикаль (a..h), y — горизонталь (1..8).</summary>
    public static class Sq
    {
        public const int None = -1;

        public static int Of(int x, int y) => y * 8 + x;
        public static int X(int sq) => sq & 7;
        public static int Y(int sq) => sq >> 3;
        public static bool InBounds(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;

        public static string Name(int sq) =>
            sq < 0 ? "-" : new string(new[] { (char)('a' + X(sq)), (char)('1' + Y(sq)) });

        /// <summary>"e4" → индекс клетки.</summary>
        public static int Parse(string name) => Of(name[0] - 'a', name[1] - '1');
    }
}
