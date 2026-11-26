namespace Chess2.Core
{
    public enum WinCondition : byte
    {
        /// <summary>
        /// «Хаос»: король не умирает, а слабеет как все. Проигрывает тот, у кого не осталось фигур.
        /// Шаха нет — король может стоять под ударом.
        /// </summary>
        Annihilation,

        /// <summary>
        /// Классика: нельзя оставлять короля под ударом, мат = победа, пат = ничья.
        /// Деградация работает для всех остальных фигур.
        /// </summary>
        Checkmate,

        /// <summary>
        /// «Корона» (основной режим). Партия идёт как обычно, с деградацией фигур.
        /// Когда королю ставят мат, он уходит с доски, а 1–3 случайные фигуры его армии
        /// загораются и получают способности. Армия без короля играет дальше:
        /// она побеждает, если поставит мат второму королю, и проигрывает, когда у неё не останется фигур.
        /// </summary>
        KingFall
    }

    /// <summary>
    /// Все спорные правила вынесены сюда, чтобы их можно было менять без переписывания логики
    /// (и показывать в меню как режимы игры).
    /// </summary>
    public sealed class RuleSet
    {
        public WinCondition WinCondition = WinCondition.KingFall;

        /// <summary>
        /// true — способности есть у всех фигур с самого начала («Хаос»).
        /// false — только у «зажжённых» фигур (Piece.Empowered).
        /// </summary>
        public bool AbilitiesForAll;

        /// <summary>Сколько фигур загорается, когда пал король (случайно в этих пределах).</summary>
        public int EmpowerMin = 1;
        public int EmpowerMax = 3;

        /// <summary>Способность ферзя: ходит и как конь.</summary>
        public bool QueenMovesAsKnight = true;

        /// <summary>Способность слона и ладьи: раз за игру удар сквозь одну фигуру.</summary>
        public bool EnablePierce = true;

        /// <summary>Можно ли пробивать сквозь свои фигуры (иначе только сквозь вражеские).</summary>
        public bool PierceThroughOwnPieces = true;

        /// <summary>
        /// Способность коня — «троянский»: возит одну свою фигуру, раз за игру прыгает дважды,
        /// может взорваться (соседние фигуры слабеют).
        /// </summary>
        public bool TrojanKnight = true;

        /// <summary>Во что возрождается пешка, которая с самого начала была пешкой.</summary>
        public PieceType PawnPromotionFallback = PieceType.Queen;

        /// <summary>Ничья, если столько полуходов подряд не было ударов и ходов пешек.</summary>
        public int NoProgressDrawPlies = 100;

        /// <summary>Основной режим «Корона».</summary>
        public static RuleSet Default => new RuleSet();

        /// <summary>«Хаос»: все способности сразу, король слабеет как все, победа — уничтожение.</summary>
        public static RuleSet Chaos => new RuleSet { WinCondition = WinCondition.Annihilation, AbilitiesForAll = true };

        /// <summary>Мат как в классике, но со всеми способностями сразу (для тестов и экспериментов).</summary>
        public static RuleSet ClassicMate => new RuleSet { WinCondition = WinCondition.Checkmate, AbilitiesForAll = true };
    }
}
