namespace Chess2.Core
{
    public enum MoveKind : byte
    {
        /// <summary>Обычный ход на пустую клетку.</summary>
        Quiet,

        /// <summary>Удар по вражеской фигуре. To — клетка цели.</summary>
        Capture,

        /// <summary>Удар слона/ладьи сквозь одну фигуру (раз за игру).</summary>
        Pierce,

        /// <summary>Рокировка. To — куда встаёт король.</summary>
        Castle,

        /// <summary>Двойной прыжок коня на пустую клетку (раз за игру). Via — промежуточная клетка.</summary>
        DoubleJump,

        /// <summary>Двойной прыжок коня с ударом в конце. Конь остаётся на Via, если цель не исчезла.</summary>
        DoubleCapture,

        /// <summary>Фигура садится в своего коня. To — клетка коня.</summary>
        Embark,

        /// <summary>Пассажир выходит из коня. From — клетка коня, To — соседняя пустая клетка.</summary>
        Disembark,

        /// <summary>Конь взрывается вместе с пассажиром, соседние фигуры слабеют. From = To.</summary>
        Explode
    }

    public struct Move
    {
        public int From;
        public int To;
        public MoveKind Kind;

        /// <summary>Промежуточная клетка двойного прыжка коня (иначе Sq.None).</summary>
        public int Via;

        public Move(int from, int to, MoveKind kind, int via = Sq.None)
        {
            From = from;
            To = to;
            Kind = kind;
            Via = via;
        }

        public bool IsCapture => Kind == MoveKind.Capture || Kind == MoveKind.Pierce || Kind == MoveKind.DoubleCapture;

        public bool IsDouble => Kind == MoveKind.DoubleJump || Kind == MoveKind.DoubleCapture;

        public bool SameAs(Move other) => From == other.From && To == other.To && Kind == other.Kind;

        public override string ToString()
        {
            switch (Kind)
            {
                case MoveKind.Capture: return Sq.Name(From) + "x" + Sq.Name(To);
                case MoveKind.Pierce: return Sq.Name(From) + "*" + Sq.Name(To);
                case MoveKind.Castle: return Sq.Name(From) + "~" + Sq.Name(To);
                case MoveKind.DoubleJump: return Sq.Name(From) + ":" + Sq.Name(Via) + ":" + Sq.Name(To);
                case MoveKind.DoubleCapture: return Sq.Name(From) + ":" + Sq.Name(Via) + "x" + Sq.Name(To);
                case MoveKind.Embark: return Sq.Name(From) + "@" + Sq.Name(To);
                case MoveKind.Disembark: return Sq.Name(From) + "^" + Sq.Name(To);
                case MoveKind.Explode: return Sq.Name(From) + "!";
                default: return Sq.Name(From) + "-" + Sq.Name(To);
            }
        }
    }

    public enum EffectKind : byte
    {
        /// <summary>Фигура переместилась From → To.</summary>
        Moved,

        /// <summary>Фигура на клетке To ослабла: OldType → NewType.</summary>
        Degraded,

        /// <summary>Фигура покинула доску (срубленный призрак, пассажир взорванного коня, некуда выпасть).</summary>
        Removed,

        /// <summary>Пешка дошла до конца и возродилась: OldType → NewType.</summary>
        Promoted,

        /// <summary>Фигура села в коня: From → To (клетка коня).</summary>
        Embarked,

        /// <summary>Пассажир вышел из коня: From (конь) → To.</summary>
        Disembarked,

        /// <summary>Конь ослаб, и пассажир выпал: From (конь) → To.</summary>
        Ejected,

        /// <summary>Конь взорвался на клетке From.</summary>
        Exploded,

        /// <summary>Королю поставили мат — он ушёл с доски (клетка From).</summary>
        KingFell,

        /// <summary>Фигура загорелась и получила способность (клетка From).</summary>
        Empowered
    }

    /// <summary>
    /// Что произошло на доске в результате хода. UI проигрывает эти эффекты по порядку
    /// (анимации, звуки, «прошлая позиция»), сеть может их не пересылать — они выводятся из хода.
    /// </summary>
    public struct MoveEffect
    {
        public EffectKind Kind;
        public int PieceId;
        public Side Side;
        public int From;
        public int To;
        public PieceType OldType;
        public PieceType NewType;

        public override string ToString() =>
            $"{Kind} #{PieceId} {Sq.Name(From)}→{Sq.Name(To)} {OldType}→{NewType}";
    }
}
