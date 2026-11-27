using System;
using System.Threading;

namespace Chess2.Core
{
    /// <summary>Насколько хорош сыгранный ход по мнению бота.</summary>
    public enum MoveQuality
    {
        /// <summary>Других ходов не было.</summary>
        Forced,
        /// <summary>Совпал с лучшим ходом бота.</summary>
        Best,
        /// <summary>Почти не хуже лучшего.</summary>
        Good,
        Inaccuracy,
        Mistake,
        Blunder
    }

    /// <summary>Разбор одного хода: что сыграли, что советует бот и сколько потеряли.</summary>
    public sealed class MoveReview
    {
        public Move Played;
        public Move? Best;
        /// <summary>Потеря в очках оценки бота (пешка ≈ 100). 0 — ход не хуже лучшего.</summary>
        public int Loss;
        public MoveQuality Quality;
    }

    /// <summary>
    /// Разбор партии ботом: для позиции перед ходом ищем лучший ход и сравниваем его оценку
    /// с оценкой сыгранного хода. Без Unity — можно гонять в фоне и в тестах.
    /// </summary>
    public static class GameAnalysis
    {
        public const int InaccuracyLoss = 60;
        public const int MistakeLoss = 150;
        public const int BlunderLoss = 350;

        private const int WinScore = 100_000;

        /// <summary>Настройки бота для разбора: глубже обычной игры не нужно, важна скорость.</summary>
        public static BotSettings DefaultSettings => new BotSettings { MaxDepth = 3, TimeLimitMs = 350, QuiescenceDepth = 6 };

        public static MoveReview Review(GameState before, Move played, BotSettings settings = null,
            CancellationToken cancel = default)
        {
            settings ??= DefaultSettings;
            var review = new MoveReview { Played = played };

            var legal = Rules.GetLegalMoves(before);
            if (legal.Count <= 1)
            {
                review.Quality = MoveQuality.Forced;
                review.Best = legal.Count == 1 ? legal[0] : (Move?)null;
                return review;
            }

            var bot = new Bot(settings, seed: 1);
            Move? best = bot.FindBestMove(before, cancel);
            int bestScore = bot.LastScore;
            review.Best = best;
            if (best.HasValue && best.Value.SameAs(played))
            {
                review.Quality = MoveQuality.Best;
                return review;
            }

            int playedScore = ScoreAfter(before, played, settings, cancel);
            review.Loss = Math.Max(0, Math.Min(10_000, bestScore - playedScore));
            review.Quality = review.Loss >= BlunderLoss ? MoveQuality.Blunder
                : review.Loss >= MistakeLoss ? MoveQuality.Mistake
                : review.Loss >= InaccuracyLoss ? MoveQuality.Inaccuracy
                : MoveQuality.Good;
            return review;
        }

        /// <summary>Оценка позиции после хода — с точки зрения того, кто ходил.</summary>
        private static int ScoreAfter(GameState before, Move played, BotSettings settings, CancellationToken cancel)
        {
            Side mover = before.SideToMove;
            GameState after = before.Clone();
            if (!Rules.TryMakeMove(after, played))
                return -WinScore; // такого хода нет — считаем худшим

            // Вынужденные ответы (единственный ход) доигрываем сразу: при одном ходе бот не ищет и не даёт оценку.
            for (int i = 0; i < 6 && !after.IsOver; i++)
            {
                var moves = Rules.GetLegalMoves(after);
                if (moves.Count != 1)
                    break;
                Rules.TryMakeMove(after, moves[0]);
            }

            if (after.IsOver)
            {
                if (after.Result == GameResult.Draw)
                    return 0;
                bool won = (after.Result == GameResult.WhiteWins) == (mover == Side.White);
                return won ? WinScore : -WinScore;
            }
            if (Rules.GetLegalMoves(after).Count <= 1)
                return 0;

            var bot = new Bot(settings, seed: 1);
            bot.FindBestMove(after, cancel);
            return after.SideToMove == mover ? bot.LastScore : -bot.LastScore;
        }
    }
}
